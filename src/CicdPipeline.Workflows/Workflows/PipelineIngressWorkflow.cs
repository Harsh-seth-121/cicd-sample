using CicdPipeline.Contracts;
using CicdPipeline.Contracts.Enums;
using CicdPipeline.Contracts.Models;
using CicdPipeline.Workflows.Activities;
using Temporalio.Common;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace CicdPipeline.Workflows.Workflows;

[Workflow]
public class PipelineIngressWorkflow
{
    private PipelineStatus _status = PipelineStatus.Received;
    private IngressStage _stage = IngressStage.Received;
    private bool _isPaused;
    private bool _isCancelled;
    private string? _overrideStage;
    private NormalizedPipelineMetadata? _metadata;
    private readonly List<FailureEvidence> _failures = new();

    private static readonly ActivityOptions IngressActivityOptions = new()
    {
        TaskQueue = TaskQueues.Orchestrator,
        StartToCloseTimeout = TimeSpan.FromSeconds(30),
        RetryPolicy = new RetryPolicy
        {
            MaximumAttempts = 3,
            InitialInterval = TimeSpan.FromSeconds(1),
            BackoffCoefficient = 2.0f,
            NonRetryableErrorTypes = new[] { "PolicyViolation", "AuthenticationError", "ConfigurationError" },
        },
    };

    [WorkflowRun]
    public async Task<DeploymentResult?> RunAsync(PipelineTrigger trigger)
    {
        // --- Validate Event ---
        SetStage(IngressStage.ValidatingEvent, PipelineStatus.Validating);

        var isValid = await Workflow.ExecuteActivityAsync(
            (IngressActivities act) => act.ValidateEventAsync(trigger),
            IngressActivityOptions);

        if (!isValid)
        {
            SetStage(IngressStage.Invalid, PipelineStatus.Failed);
            return null;
        }

        // --- Normalize Metadata ---
        SetStage(IngressStage.Normalizing, PipelineStatus.Validating);

        _metadata = await Workflow.ExecuteActivityAsync(
            (IngressActivities act) => act.NormalizeMetadataAsync(trigger),
            IngressActivityOptions);

        // --- Derive Identity ---
        SetStage(IngressStage.DeriveIdentity, PipelineStatus.Validating);

        Workflow.UpsertTypedSearchAttributes(
            PipelineSearchAttributes.TriggerType.ValueSet(trigger.TriggerType.ToString()),
            PipelineSearchAttributes.Repository.ValueSet(_metadata.Repository),
            PipelineSearchAttributes.Branch.ValueSet(_metadata.Branch),
            PipelineSearchAttributes.CommitSha.ValueSet(_metadata.CommitSha),
            PipelineSearchAttributes.PipelineStartedAt.ValueSet(Workflow.UtcNow));

        // --- Deduplicate ---
        SetStage(IngressStage.Deduplicate, PipelineStatus.Validating);

        var isDuplicate = await Workflow.ExecuteActivityAsync(
            (IngressActivities act) => act.CheckDuplicateAsync(_metadata.PipelineId),
            IngressActivityOptions);

        if (isDuplicate)
        {
            SetStage(IngressStage.DuplicateIgnored, PipelineStatus.Skipped);
            return null;
        }

        // --- Start Workflow ---
        SetStage(IngressStage.StartWorkflow, PipelineStatus.Validating);

        // --- Upsert Search Attributes ---
        SetStage(IngressStage.UpsertSearchAttributes, PipelineStatus.Validating);

        Workflow.UpsertTypedSearchAttributes(
            PipelineSearchAttributes.PipelineStatus.ValueSet(PipelineStatus.Building.ToString()));

        // --- Dispatch Build Validation (child workflow) ---
        await CheckPauseAndCancel();
        SetStage(IngressStage.DispatchBuildValidation, PipelineStatus.Building);

        var buildResult = await Workflow.ExecuteChildWorkflowAsync(
            (BuildValidationWorkflow wf) => wf.RunAsync(_metadata),
            new ChildWorkflowOptions
            {
                Id = WorkflowIds.BuildValidation(_metadata.PipelineId),
                TaskQueue = TaskQueues.Orchestrator,
            });

        if (!buildResult.Passed)
        {
            RecordFailure("BuildValidation", buildResult.FailureReason ?? "Build validation failed");
            SetStage(IngressStage.Completed, PipelineStatus.Failed);
            return null;
        }

        // --- Version and Publish (child workflow) ---
        await CheckPauseAndCancel();

        ReleaseManifest manifest;
        try
        {
            manifest = await Workflow.ExecuteChildWorkflowAsync(
                (VersionAndPublishWorkflow wf) => wf.RunAsync(_metadata),
                new ChildWorkflowOptions
                {
                    Id = WorkflowIds.VersionAndPublish(_metadata.PipelineId),
                    TaskQueue = TaskQueues.Orchestrator,
                });
        }
        catch (ChildWorkflowFailureException ex)
        {
            RecordFailure("VersionAndPublish", ex.InnerException?.Message ?? ex.Message);
            SetStage(IngressStage.Completed, PipelineStatus.Failed);
            return null;
        }

        // --- Deployment (child workflow) ---
        await CheckPauseAndCancel();

        var deployInput = new DeploymentInput(_metadata, manifest);

        DeploymentResult deployResult;
        try
        {
            deployResult = await Workflow.ExecuteChildWorkflowAsync(
                (DeploymentWorkflow wf) => wf.RunAsync(deployInput),
                new ChildWorkflowOptions
                {
                    Id = WorkflowIds.Deployment(_metadata.PipelineId),
                    TaskQueue = TaskQueues.Orchestrator,
                });
        }
        catch (ChildWorkflowFailureException ex)
        {
            RecordFailure("Deployment", ex.InnerException?.Message ?? ex.Message);
            SetStage(IngressStage.Completed, PipelineStatus.Failed);
            return null;
        }

        // --- Final status ---
        var finalStatus = deployResult.Succeeded ? PipelineStatus.Succeeded : PipelineStatus.Failed;
        SetStage(IngressStage.Completed, finalStatus);

        return deployResult;
    }

    [WorkflowQuery]
    public PipelineStageStatus GetStatus() => new(
        PipelineId: Workflow.Info.WorkflowId,
        Status: _status,
        CurrentStage: _stage.ToString(),
        IsPaused: _isPaused,
        IsCancelled: _isCancelled,
        LastUpdated: Workflow.UtcNow,
        Failures: _failures.ToList());

    [WorkflowSignal]
    public async Task CancelAsync(string reason)
    {
        _isCancelled = true;
        _failures.Add(new FailureEvidence(
            _stage.ToString(), $"Cancelled: {reason}", Workflow.UtcNow, new Dictionary<string, string>()));
    }

    [WorkflowSignal]
    public async Task PauseAsync(string reason)
    {
        _isPaused = true;
    }

    [WorkflowUpdate]
    public async Task ResumeAsync(OperatorOverride command)
    {
        if (command.Action == OperatorAction.Override && command.OverrideStage != null)
        {
            _overrideStage = command.OverrideStage;
        }

        _isPaused = false;
    }

    [WorkflowUpdateValidator(nameof(ResumeAsync))]
    public void ValidateResume(OperatorOverride command)
    {
        if (command.Action != OperatorAction.Resume && command.Action != OperatorAction.Override)
            throw new ApplicationFailureException("Expected Resume or Override action");
        if (string.IsNullOrEmpty(command.OperatorId))
            throw new ApplicationFailureException("OperatorId is required");
    }

    private async Task CheckPauseAndCancel()
    {
        if (_isCancelled)
            throw new ApplicationFailureException("Pipeline cancelled by operator");

        if (_isPaused)
        {
            _status = PipelineStatus.Paused;
            Workflow.UpsertTypedSearchAttributes(
                PipelineSearchAttributes.PipelineStatus.ValueSet(PipelineStatus.Paused.ToString()));
            await Workflow.WaitConditionAsync(() => !_isPaused || _isCancelled);

            if (_isCancelled)
                throw new ApplicationFailureException("Pipeline cancelled while paused");
        }
    }

    private void SetStage(IngressStage stage, PipelineStatus status)
    {
        _stage = stage;
        _status = status;
        Workflow.UpsertTypedSearchAttributes(
            PipelineSearchAttributes.PipelineStatus.ValueSet(status.ToString()),
            PipelineSearchAttributes.Stage.ValueSet(stage.ToString()));
    }

    private void RecordFailure(string stage, string reason)
    {
        _failures.Add(new FailureEvidence(
            stage, reason, Workflow.UtcNow, new Dictionary<string, string>()));
    }
}
