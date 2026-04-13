using CicdPipeline.Contracts;
using CicdPipeline.Contracts.Enums;
using CicdPipeline.Contracts.Models;
using CicdPipeline.Workflows.Activities;
using Temporalio.Common;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace CicdPipeline.Workflows.Workflows;

[Workflow]
public class DeploymentWorkflow
{
    private DeploymentStage _stage = DeploymentStage.Published;
    private PipelineStatus _status = PipelineStatus.DeployingDev;
    private bool _isPaused;
    private bool _isCancelled;
    private string? _overrideStage;
    private readonly List<FailureEvidence> _failures = new();

    private static readonly ActivityOptions DeployOptions = new()
    {
        TaskQueue = TaskQueues.Deploy,
        StartToCloseTimeout = TimeSpan.FromMinutes(10),
        HeartbeatTimeout = TimeSpan.FromSeconds(60),
        RetryPolicy = new RetryPolicy
        {
            MaximumAttempts = 3,
            InitialInterval = TimeSpan.FromSeconds(10),
            BackoffCoefficient = 2.0f,
            NonRetryableErrorTypes = new[] { "PolicyViolation", "AuthenticationError", "ConfigurationError" },
        },
    };

    private static readonly ActivityOptions VerifyOptions = new()
    {
        TaskQueue = TaskQueues.Deploy,
        StartToCloseTimeout = TimeSpan.FromMinutes(5),
        HeartbeatTimeout = TimeSpan.FromSeconds(30),
        RetryPolicy = new RetryPolicy
        {
            MaximumAttempts = 3,
            InitialInterval = TimeSpan.FromSeconds(10),
            BackoffCoefficient = 2.0f,
            NonRetryableErrorTypes = new[] { "PolicyViolation", "AuthenticationError", "ConfigurationError" },
        },
    };

    [WorkflowRun]
    public async Task<DeploymentResult> RunAsync(DeploymentInput input)
    {
        var startTime = Workflow.UtcNow;
        var image = input.Manifest.Image;

        // --- Deploy to DEV ---
        await CheckPauseAndCancel();
        SetStage(DeploymentStage.DeployDev, PipelineStatus.DeployingDev);

        DeploymentResult devDeployResult;
        try
        {
            devDeployResult = await Workflow.ExecuteActivityAsync(
                (DeployActivities act) => act.DeployToEnvironmentAsync(DeployEnvironment.Dev, image),
                DeployOptions);
        }
        catch (ActivityFailureException ex) when (!_isCancelled)
        {
            RecordFailure("DeployDev", ex.InnerException?.Message ?? ex.Message);
            SetStage(DeploymentStage.DevDeployFailed, PipelineStatus.Failed);
            SetStage(DeploymentStage.Failed, PipelineStatus.Failed);
            return new DeploymentResult(
                DeployEnvironment.Dev, false, ex.InnerException?.Message ?? ex.Message,
                Workflow.UtcNow, Workflow.UtcNow - startTime);
        }

        if (!devDeployResult.Succeeded)
        {
            RecordFailure("DeployDev", devDeployResult.FailureReason ?? "Deploy to DEV failed");
            SetStage(DeploymentStage.DevDeployFailed, PipelineStatus.Failed);
            SetStage(DeploymentStage.Failed, PipelineStatus.Failed);
            return devDeployResult;
        }

        // --- Verify DEV ---
        await CheckPauseAndCancel();
        SetStage(DeploymentStage.VerifyDev, PipelineStatus.VerifyingDev);

        EnvironmentVerificationResult devVerifyResult;
        try
        {
            devVerifyResult = await Workflow.ExecuteActivityAsync(
                (DeployActivities act) => act.VerifyEnvironmentAsync(DeployEnvironment.Dev, image),
                VerifyOptions);
        }
        catch (ActivityFailureException ex) when (!_isCancelled)
        {
            RecordFailure("VerifyDev", ex.InnerException?.Message ?? ex.Message);
            SetStage(DeploymentStage.DevVerifyFailed, PipelineStatus.Failed);
            SetStage(DeploymentStage.Failed, PipelineStatus.Failed);
            return new DeploymentResult(
                DeployEnvironment.Dev, false, ex.InnerException?.Message ?? ex.Message,
                Workflow.UtcNow, Workflow.UtcNow - startTime);
        }

        if (!devVerifyResult.Healthy)
        {
            RecordFailure("VerifyDev", devVerifyResult.FailureDetail ?? "DEV verification failed");
            SetStage(DeploymentStage.DevVerifyFailed, PipelineStatus.Failed);
            SetStage(DeploymentStage.Failed, PipelineStatus.Failed);
            return new DeploymentResult(
                DeployEnvironment.Dev, false, devVerifyResult.FailureDetail,
                Workflow.UtcNow, Workflow.UtcNow - startTime);
        }

        SetStage(DeploymentStage.DevReady, PipelineStatus.VerifyingDev);

        // --- Branch Decision ---
        SetStage(DeploymentStage.BranchDecision, PipelineStatus.VerifyingDev);

        if (input.Metadata.BranchClassification != BranchClassification.Main)
        {
            SetStage(DeploymentStage.SuccessDevOnly, PipelineStatus.Succeeded);
            return new DeploymentResult(
                DeployEnvironment.Dev, true, null,
                Workflow.UtcNow, Workflow.UtcNow - startTime);
        }

        // --- Deploy to QA (main branch only) ---
        await CheckPauseAndCancel();
        SetStage(DeploymentStage.DeployQa, PipelineStatus.DeployingQa);

        DeploymentResult qaDeployResult;
        try
        {
            qaDeployResult = await Workflow.ExecuteActivityAsync(
                (DeployActivities act) => act.DeployToEnvironmentAsync(DeployEnvironment.Qa, image),
                DeployOptions);
        }
        catch (ActivityFailureException ex) when (!_isCancelled)
        {
            RecordFailure("DeployQa", ex.InnerException?.Message ?? ex.Message);
            SetStage(DeploymentStage.QaDeployFailed, PipelineStatus.Failed);
            SetStage(DeploymentStage.Failed, PipelineStatus.Failed);
            return new DeploymentResult(
                DeployEnvironment.Qa, false, ex.InnerException?.Message ?? ex.Message,
                Workflow.UtcNow, Workflow.UtcNow - startTime);
        }

        if (!qaDeployResult.Succeeded)
        {
            RecordFailure("DeployQa", qaDeployResult.FailureReason ?? "Deploy to QA failed");
            SetStage(DeploymentStage.QaDeployFailed, PipelineStatus.Failed);
            SetStage(DeploymentStage.Failed, PipelineStatus.Failed);
            return qaDeployResult;
        }

        // --- Verify QA ---
        await CheckPauseAndCancel();
        SetStage(DeploymentStage.VerifyQa, PipelineStatus.VerifyingQa);

        EnvironmentVerificationResult qaVerifyResult;
        try
        {
            qaVerifyResult = await Workflow.ExecuteActivityAsync(
                (DeployActivities act) => act.VerifyEnvironmentAsync(DeployEnvironment.Qa, image),
                VerifyOptions);
        }
        catch (ActivityFailureException ex) when (!_isCancelled)
        {
            RecordFailure("VerifyQa", ex.InnerException?.Message ?? ex.Message);
            SetStage(DeploymentStage.QaVerifyFailed, PipelineStatus.Failed);
            SetStage(DeploymentStage.Failed, PipelineStatus.Failed);
            return new DeploymentResult(
                DeployEnvironment.Qa, false, ex.InnerException?.Message ?? ex.Message,
                Workflow.UtcNow, Workflow.UtcNow - startTime);
        }

        if (!qaVerifyResult.Healthy)
        {
            RecordFailure("VerifyQa", qaVerifyResult.FailureDetail ?? "QA verification failed");
            SetStage(DeploymentStage.QaVerifyFailed, PipelineStatus.Failed);
            SetStage(DeploymentStage.Failed, PipelineStatus.Failed);
            return new DeploymentResult(
                DeployEnvironment.Qa, false, qaVerifyResult.FailureDetail,
                Workflow.UtcNow, Workflow.UtcNow - startTime);
        }

        SetStage(DeploymentStage.SuccessDevQa, PipelineStatus.Succeeded);
        return new DeploymentResult(
            DeployEnvironment.Qa, true, null,
            Workflow.UtcNow, Workflow.UtcNow - startTime);
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

    private void SetStage(DeploymentStage stage, PipelineStatus status)
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
