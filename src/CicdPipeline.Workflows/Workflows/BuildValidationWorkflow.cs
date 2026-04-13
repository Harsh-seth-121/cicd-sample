using CicdPipeline.Contracts;
using CicdPipeline.Contracts.Enums;
using CicdPipeline.Contracts.Models;
using CicdPipeline.Workflows.Activities;
using Temporalio.Common;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace CicdPipeline.Workflows.Workflows;

[Workflow]
public class BuildValidationWorkflow
{
    private BuildStage _stage = BuildStage.Pending;
    private PipelineStatus _status = PipelineStatus.Building;
    private bool _isPaused;
    private bool _isCancelled;
    private string? _overrideStage;
    private readonly List<FailureEvidence> _failures = new();

    private static readonly string[] NonRetryableErrors =
        { "PolicyViolation", "AuthenticationError", "ConfigurationError" };

    private static readonly Dictionary<BuildStage, BuildStage> StageToFailureState = new()
    {
        { BuildStage.Checkout, BuildStage.CheckoutFailed },
        { BuildStage.Build, BuildStage.BuildFailed },
        { BuildStage.UnitTests, BuildStage.UnitFailed },
        { BuildStage.IntegrationTests, BuildStage.IntegrationFailed },
        { BuildStage.RequiredScans, BuildStage.ScanFailed },
    };

    [WorkflowRun]
    public async Task<BuildValidationResult> RunAsync(NormalizedPipelineMetadata metadata)
    {
        var startTime = Workflow.UtcNow;

        try
        {
            // --- Checkout ---
            await CheckPauseAndCancel();
            SetStage(BuildStage.Checkout, PipelineStatus.Building);

            await Workflow.ExecuteActivityAsync(
                (BuildTestActivities act) => act.CheckoutSourceAsync(
                    metadata.Repository, metadata.CommitSha, metadata.Branch),
                new ActivityOptions
                {
                    TaskQueue = TaskQueues.BuildTest,
                    StartToCloseTimeout = TimeSpan.FromMinutes(5),
                    RetryPolicy = new RetryPolicy
                    {
                        MaximumAttempts = 3,
                        InitialInterval = TimeSpan.FromSeconds(5),
                        BackoffCoefficient = 2.0f,
                        NonRetryableErrorTypes = NonRetryableErrors,
                    },
                });

            // --- Build ---
            await CheckPauseAndCancel();
            SetStage(BuildStage.Build, PipelineStatus.Building);

            await Workflow.ExecuteActivityAsync(
                (BuildTestActivities act) => act.BuildAsync(
                    metadata.Repository, metadata.CommitSha),
                new ActivityOptions
                {
                    TaskQueue = TaskQueues.BuildTest,
                    StartToCloseTimeout = TimeSpan.FromMinutes(15),
                    HeartbeatTimeout = TimeSpan.FromSeconds(60),
                    RetryPolicy = new RetryPolicy
                    {
                        MaximumAttempts = 2,
                        InitialInterval = TimeSpan.FromSeconds(10),
                        BackoffCoefficient = 2.0f,
                        NonRetryableErrorTypes = NonRetryableErrors,
                    },
                });

            // --- Unit Tests ---
            await CheckPauseAndCancel();
            SetStage(BuildStage.UnitTests, PipelineStatus.Testing);

            var unitResults = await Workflow.ExecuteActivityAsync(
                (BuildTestActivities act) => act.RunUnitTestsAsync(
                    metadata.Repository, metadata.CommitSha),
                new ActivityOptions
                {
                    TaskQueue = TaskQueues.BuildTest,
                    StartToCloseTimeout = TimeSpan.FromMinutes(10),
                    HeartbeatTimeout = TimeSpan.FromSeconds(60),
                    RetryPolicy = new RetryPolicy
                    {
                        MaximumAttempts = 2,
                        InitialInterval = TimeSpan.FromSeconds(5),
                        BackoffCoefficient = 2.0f,
                        NonRetryableErrorTypes = NonRetryableErrors,
                    },
                });

            // --- Integration Tests ---
            await CheckPauseAndCancel();
            SetStage(BuildStage.IntegrationTests, PipelineStatus.Testing);

            var integrationResults = await Workflow.ExecuteActivityAsync(
                (BuildTestActivities act) => act.RunIntegrationTestsAsync(
                    metadata.Repository, metadata.CommitSha),
                new ActivityOptions
                {
                    TaskQueue = TaskQueues.BuildTest,
                    StartToCloseTimeout = TimeSpan.FromMinutes(20),
                    HeartbeatTimeout = TimeSpan.FromSeconds(120),
                    RetryPolicy = new RetryPolicy
                    {
                        MaximumAttempts = 2,
                        InitialInterval = TimeSpan.FromSeconds(10),
                        BackoffCoefficient = 2.0f,
                        NonRetryableErrorTypes = NonRetryableErrors,
                    },
                });

            // --- Required Scans ---
            await CheckPauseAndCancel();
            SetStage(BuildStage.RequiredScans, PipelineStatus.Scanning);

            var scanResults = await Workflow.ExecuteActivityAsync(
                (BuildTestActivities act) => act.RunRequiredScansAsync(
                    metadata.Repository, metadata.CommitSha),
                new ActivityOptions
                {
                    TaskQueue = TaskQueues.BuildTest,
                    StartToCloseTimeout = TimeSpan.FromMinutes(15),
                    HeartbeatTimeout = TimeSpan.FromSeconds(60),
                    RetryPolicy = new RetryPolicy
                    {
                        MaximumAttempts = 2,
                        InitialInterval = TimeSpan.FromSeconds(5),
                        BackoffCoefficient = 2.0f,
                        NonRetryableErrorTypes = NonRetryableErrors,
                    },
                });

            // --- All gates passed ---
            SetStage(BuildStage.ValidationPassed, PipelineStatus.Building);

            var evidence = new Dictionary<string, string>(unitResults);
            foreach (var kv in integrationResults)
                evidence[$"integration_{kv.Key}"] = kv.Value;
            foreach (var kv in scanResults)
                evidence[$"scan_{kv.Key}"] = kv.Value;

            return new BuildValidationResult(
                Passed: true,
                FailedAtStage: null,
                FailureReason: null,
                TestEvidence: evidence,
                Duration: Workflow.UtcNow - startTime);
        }
        catch (ActivityFailureException ex) when (!_isCancelled)
        {
            var failureSubState = StageToFailureState.GetValueOrDefault(_stage, BuildStage.Failed);
            RecordFailure(_stage.ToString(), ex.InnerException?.Message ?? ex.Message);
            SetStage(failureSubState, PipelineStatus.Failed);
            SetStage(BuildStage.Failed, PipelineStatus.Failed);

            return new BuildValidationResult(
                Passed: false,
                FailedAtStage: failureSubState,
                FailureReason: ex.InnerException?.Message ?? ex.Message,
                TestEvidence: new Dictionary<string, string>(),
                Duration: Workflow.UtcNow - startTime);
        }
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

    private void SetStage(BuildStage stage, PipelineStatus status)
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
