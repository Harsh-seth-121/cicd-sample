using CicdPipeline.Contracts;
using CicdPipeline.Contracts.Enums;
using CicdPipeline.Contracts.Models;
using CicdPipeline.Workflows.Activities;
using Temporalio.Common;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace CicdPipeline.Workflows.Workflows;

[Workflow]
public class VersionAndPublishWorkflow
{
    private VersionPublishStage _stage = VersionPublishStage.Pending;
    private PipelineStatus _status = PipelineStatus.Versioning;
    private bool _isPaused;
    private bool _isCancelled;
    private string? _overrideStage;
    private VersionInfo? _versionInfo;
    private ImageMetadata? _imageMetadata;
    private readonly List<FailureEvidence> _failures = new();

    private static readonly string[] NonRetryableErrors =
        { "PolicyViolation", "AuthenticationError", "ConfigurationError" };

    private static readonly ActivityOptions GitVersionShortOptions = new()
    {
        TaskQueue = TaskQueues.GitVersion,
        StartToCloseTimeout = TimeSpan.FromMinutes(2),
        RetryPolicy = new RetryPolicy
        {
            MaximumAttempts = 3,
            InitialInterval = TimeSpan.FromSeconds(5),
            BackoffCoefficient = 2.0f,
            NonRetryableErrorTypes = NonRetryableErrors,
        },
    };

    private static readonly ActivityOptions PublishShortOptions = new()
    {
        TaskQueue = TaskQueues.Publish,
        StartToCloseTimeout = TimeSpan.FromMinutes(1),
        RetryPolicy = new RetryPolicy
        {
            MaximumAttempts = 3,
            InitialInterval = TimeSpan.FromSeconds(2),
            BackoffCoefficient = 2.0f,
            NonRetryableErrorTypes = NonRetryableErrors,
        },
    };

    [WorkflowRun]
    public async Task<ReleaseManifest> RunAsync(NormalizedPipelineMetadata metadata)
    {
        // ===== GitVersion Phase (TaskQueue: cicd.gitversion) =====

        // --- Load Repo Context ---
        await CheckPauseAndCancel();
        SetStage(VersionPublishStage.LoadRepoContext, PipelineStatus.Versioning);

        try
        {
            await Workflow.ExecuteActivityAsync(
                (GitVersionActivities act) => act.LoadRepoContextAsync(
                    metadata.Repository, metadata.CommitSha, metadata.Branch),
                GitVersionShortOptions);
        }
        catch (ActivityFailureException ex) when (!_isCancelled)
        {
            RecordFailure(VersionPublishStage.LoadRepoContext.ToString(), ex.InnerException?.Message ?? ex.Message);
            SetStage(VersionPublishStage.RepoContextInvalid, PipelineStatus.Failed);
            SetStage(VersionPublishStage.Failed, PipelineStatus.Failed);
            throw;
        }

        // --- Compute Version ---
        await CheckPauseAndCancel();
        SetStage(VersionPublishStage.ComputeVersion, PipelineStatus.Versioning);

        try
        {
            _versionInfo = await Workflow.ExecuteActivityAsync(
                (GitVersionActivities act) => act.ComputeVersionAsync(
                    metadata.Repository, metadata.Branch),
                GitVersionShortOptions);
        }
        catch (ActivityFailureException ex) when (!_isCancelled)
        {
            RecordFailure(VersionPublishStage.ComputeVersion.ToString(), ex.InnerException?.Message ?? ex.Message);
            SetStage(VersionPublishStage.VersionError, PipelineStatus.Failed);
            SetStage(VersionPublishStage.Failed, PipelineStatus.Failed);
            throw;
        }

        // --- Version Computed (checkpoint) ---
        SetStage(VersionPublishStage.VersionComputed, PipelineStatus.Versioning);

        Workflow.UpsertTypedSearchAttributes(
            PipelineSearchAttributes.SemVer.ValueSet(_versionInfo.SemVer));

        // --- Persist Version Metadata ---
        await CheckPauseAndCancel();
        SetStage(VersionPublishStage.PersistVersionMetadata, PipelineStatus.Versioning);

        await Workflow.ExecuteActivityAsync(
            (GitVersionActivities act) => act.PersistVersionMetadataAsync(_versionInfo),
            new ActivityOptions
            {
                TaskQueue = TaskQueues.GitVersion,
                StartToCloseTimeout = TimeSpan.FromSeconds(30),
                RetryPolicy = new RetryPolicy
                {
                    MaximumAttempts = 3,
                    InitialInterval = TimeSpan.FromSeconds(2),
                    BackoffCoefficient = 2.0f,
                    NonRetryableErrorTypes = NonRetryableErrors,
                },
            });

        // ===== Publish Phase (TaskQueue: cicd.publish) =====

        // --- Prepare Image Metadata ---
        await CheckPauseAndCancel();
        SetStage(VersionPublishStage.PrepareImageMetadata, PipelineStatus.Publishing);

        _imageMetadata = await Workflow.ExecuteActivityAsync(
            (PublishActivities act) => act.PrepareImageMetadataAsync(metadata, _versionInfo),
            new ActivityOptions
            {
                TaskQueue = TaskQueues.Publish,
                StartToCloseTimeout = TimeSpan.FromSeconds(30),
                RetryPolicy = new RetryPolicy
                {
                    MaximumAttempts = 3,
                    InitialInterval = TimeSpan.FromSeconds(2),
                    BackoffCoefficient = 2.0f,
                    NonRetryableErrorTypes = NonRetryableErrors,
                },
            });

        // --- Build or Finalize Image ---
        await CheckPauseAndCancel();
        SetStage(VersionPublishStage.BuildOrFinalizeImage, PipelineStatus.Publishing);

        try
        {
            await Workflow.ExecuteActivityAsync(
                (PublishActivities act) => act.BuildOrFinalizeImageAsync(
                    _imageMetadata, metadata.CommitSha),
                new ActivityOptions
                {
                    TaskQueue = TaskQueues.Publish,
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
        }
        catch (ActivityFailureException ex) when (!_isCancelled)
        {
            RecordFailure(VersionPublishStage.BuildOrFinalizeImage.ToString(), ex.InnerException?.Message ?? ex.Message);
            SetStage(VersionPublishStage.ImageBuildFailed, PipelineStatus.Failed);
            SetStage(VersionPublishStage.Failed, PipelineStatus.Failed);
            throw;
        }

        // --- Push Image ---
        await CheckPauseAndCancel();
        SetStage(VersionPublishStage.PushTagsAndDigest, PipelineStatus.Publishing);

        try
        {
            await Workflow.ExecuteActivityAsync(
                (PublishActivities act) => act.PushImageAsync(_imageMetadata),
                new ActivityOptions
                {
                    TaskQueue = TaskQueues.Publish,
                    StartToCloseTimeout = TimeSpan.FromMinutes(10),
                    HeartbeatTimeout = TimeSpan.FromSeconds(60),
                    RetryPolicy = new RetryPolicy
                    {
                        MaximumAttempts = 3,
                        InitialInterval = TimeSpan.FromSeconds(10),
                        BackoffCoefficient = 2.0f,
                        NonRetryableErrorTypes = NonRetryableErrors,
                    },
                });
        }
        catch (ActivityFailureException ex) when (!_isCancelled)
        {
            RecordFailure(VersionPublishStage.PushTagsAndDigest.ToString(), ex.InnerException?.Message ?? ex.Message);
            SetStage(VersionPublishStage.RegistryPushFailed, PipelineStatus.Failed);
            SetStage(VersionPublishStage.Failed, PipelineStatus.Failed);
            throw;
        }

        // --- Capture Digest ---
        await CheckPauseAndCancel();
        SetStage(VersionPublishStage.CaptureDigest, PipelineStatus.Publishing);

        _imageMetadata = await Workflow.ExecuteActivityAsync(
            (PublishActivities act) => act.CaptureDigestAsync(_imageMetadata),
            PublishShortOptions);

        Workflow.UpsertTypedSearchAttributes(
            PipelineSearchAttributes.ImageDigest.ValueSet(_imageMetadata.Digest ?? "unknown"));

        // --- Write Release Manifest ---
        await CheckPauseAndCancel();
        SetStage(VersionPublishStage.WriteReleaseManifest, PipelineStatus.Publishing);

        ReleaseManifest manifest;
        try
        {
            manifest = await Workflow.ExecuteActivityAsync(
                (PublishActivities act) => act.WriteReleaseManifestAsync(
                    metadata, _versionInfo, _imageMetadata),
                PublishShortOptions);
        }
        catch (ActivityFailureException ex) when (!_isCancelled)
        {
            RecordFailure(VersionPublishStage.WriteReleaseManifest.ToString(), ex.InnerException?.Message ?? ex.Message);
            SetStage(VersionPublishStage.ManifestWriteFailed, PipelineStatus.Failed);
            SetStage(VersionPublishStage.Failed, PipelineStatus.Failed);
            throw;
        }

        SetStage(VersionPublishStage.Published, PipelineStatus.Publishing);
        return manifest;
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

    [WorkflowQuery]
    public VersionInfo? GetVersionInfo() => _versionInfo;

    [WorkflowQuery]
    public ImageMetadata? GetImageMetadata() => _imageMetadata;

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

    private void SetStage(VersionPublishStage stage, PipelineStatus status)
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
