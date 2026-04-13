using CicdPipeline.Contracts;
using CicdPipeline.Contracts.Enums;
using CicdPipeline.Contracts.Models;
using CicdPipeline.Workflows.Activities;
using CicdPipeline.Workflows.Tests.Fixtures;
using CicdPipeline.Workflows.Workflows;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Testing;
using Temporalio.Worker;

namespace CicdPipeline.Workflows.Tests.Workflows;

public class PipelineIngressWorkflowTests : IAsyncLifetime
{
    private WorkflowEnvironment _env = null!;

    public async Task InitializeAsync()
    {
        _env = await WorkflowEnvironment.StartLocalAsync();
        await SearchAttributeRegistration.RegisterAllAsync(_env.Client);
    }

    public async Task DisposeAsync()
    {
        await _env.DisposeAsync();
    }

    [Fact]
    public async Task HappyPath_FeatureBranch_DeploysToDevOnly()
    {
        var trigger = TestDataFactory.CreateTrigger();
        var stubs = new AllActivitiesStub();

        var result = await ExecuteWorkflowAsync(trigger, stubs);

        Assert.NotNull(result);
        Assert.True(result.Succeeded);
        Assert.Equal(DeployEnvironment.Dev, result.Environment);
    }

    [Fact]
    public async Task HappyPath_MainBranch_DeploysToDevAndQa()
    {
        var trigger = TestDataFactory.CreateMainBranchTrigger();
        var stubs = new AllActivitiesStub();

        var result = await ExecuteWorkflowAsync(trigger, stubs);

        Assert.NotNull(result);
        Assert.True(result.Succeeded);
        Assert.Equal(DeployEnvironment.Qa, result.Environment);
    }

    [Fact]
    public async Task InvalidEvent_ReturnsNull()
    {
        var trigger = TestDataFactory.CreateTrigger();
        var stubs = new AllActivitiesStub { ValidateEventReturns = false };

        var result = await ExecuteWorkflowAsync(trigger, stubs);

        Assert.Null(result);
    }

    [Fact]
    public async Task Duplicate_ReturnsNull()
    {
        var trigger = TestDataFactory.CreateTrigger();
        var stubs = new AllActivitiesStub { IsDuplicate = true };

        var result = await ExecuteWorkflowAsync(trigger, stubs);

        Assert.Null(result);
    }

    [Fact]
    public async Task BuildFails_ReturnsNull()
    {
        var trigger = TestDataFactory.CreateTrigger();
        var stubs = new AllActivitiesStub { BuildPasses = false };

        var result = await ExecuteWorkflowAsync(trigger, stubs);

        Assert.Null(result);
    }

    private async Task<DeploymentResult?> ExecuteWorkflowAsync(
        PipelineTrigger trigger, AllActivitiesStub stubs)
    {
        var handle = await _env.Client.StartWorkflowAsync(
            (PipelineIngressWorkflow wf) => wf.RunAsync(trigger),
            new WorkflowOptions
            {
                Id = $"test-pipeline-{Guid.NewGuid()}",
                TaskQueue = TaskQueues.Orchestrator,
            });

        var resultTask = handle.GetResultAsync();

        using var cts = new CancellationTokenSource();

        // Orchestrator worker: all workflows + ingress activities
        using var orchestratorWorker = new TemporalWorker(
            _env.Client,
            new TemporalWorkerOptions(TaskQueues.Orchestrator)
                .AddWorkflow<PipelineIngressWorkflow>()
                .AddWorkflow<BuildValidationWorkflow>()
                .AddWorkflow<VersionAndPublishWorkflow>()
                .AddWorkflow<DeploymentWorkflow>()
                .AddAllActivities(stubs));

        // Activity workers for each dedicated task queue
        using var buildTestWorker = new TemporalWorker(
            _env.Client,
            new TemporalWorkerOptions(TaskQueues.BuildTest)
                .AddAllActivities(stubs));

        using var gitVersionWorker = new TemporalWorker(
            _env.Client,
            new TemporalWorkerOptions(TaskQueues.GitVersion)
                .AddAllActivities(stubs));

        using var publishWorker = new TemporalWorker(
            _env.Client,
            new TemporalWorkerOptions(TaskQueues.Publish)
                .AddAllActivities(stubs));

        using var deployWorker = new TemporalWorker(
            _env.Client,
            new TemporalWorkerOptions(TaskQueues.Deploy)
                .AddAllActivities(stubs));

        var bgTasks = new[]
        {
            buildTestWorker.ExecuteAsync(cts.Token),
            gitVersionWorker.ExecuteAsync(cts.Token),
            publishWorker.ExecuteAsync(cts.Token),
            deployWorker.ExecuteAsync(cts.Token),
        };

        try
        {
            return await orchestratorWorker.ExecuteAsync(() => resultTask);
        }
        finally
        {
            cts.Cancel();
            foreach (var t in bgTasks)
                try { await t; } catch (OperationCanceledException) { }
        }
    }

    /// <summary>
    /// Unified stub that implements all activity methods across all 5 activity classes.
    /// Used because in the test environment all workflows and activities run on a single task queue.
    /// </summary>
    private class AllActivitiesStub
    {
        public bool ValidateEventReturns { get; set; } = true;
        public bool IsDuplicate { get; set; }
        public bool BuildPasses { get; set; } = true;

        // --- Ingress Activities ---
        [Temporalio.Activities.Activity]
        public Task<bool> ValidateEventAsync(PipelineTrigger trigger) =>
            Task.FromResult(ValidateEventReturns);

        [Temporalio.Activities.Activity]
        public Task<NormalizedPipelineMetadata> NormalizeMetadataAsync(PipelineTrigger trigger)
        {
            var branch = trigger.Ref.Replace("refs/heads/", "");
            var shortSha = trigger.CommitSha[..7];
            var classification = branch is "main" or "master"
                ? BranchClassification.Main : BranchClassification.Feature;
            return Task.FromResult(new NormalizedPipelineMetadata(
                $"test-{shortSha}", trigger.Repository, trigger.CommitSha,
                shortSha, branch, classification, trigger.TriggerType, trigger.ReceivedAt));
        }

        [Temporalio.Activities.Activity]
        public Task<bool> CheckDuplicateAsync(string pipelineId) =>
            Task.FromResult(IsDuplicate);

        // --- Build/Test Activities ---
        [Temporalio.Activities.Activity]
        public Task CheckoutSourceAsync(string repository, string commitSha, string branch) =>
            Task.CompletedTask;

        [Temporalio.Activities.Activity]
        public Task BuildAsync(string repository, string commitSha) =>
            !BuildPasses
                ? throw new ApplicationFailureException("Build failed", nonRetryable: true)
                : Task.CompletedTask;

        [Temporalio.Activities.Activity]
        public Task<Dictionary<string, string>> RunUnitTestsAsync(string repository, string commitSha) =>
            Task.FromResult(new Dictionary<string, string> { ["passed"] = "100" });

        [Temporalio.Activities.Activity]
        public Task<Dictionary<string, string>> RunIntegrationTestsAsync(string repository, string commitSha) =>
            Task.FromResult(new Dictionary<string, string> { ["passed"] = "50" });

        [Temporalio.Activities.Activity]
        public Task<Dictionary<string, string>> RunRequiredScansAsync(string repository, string commitSha) =>
            Task.FromResult(new Dictionary<string, string> { ["policy_compliant"] = "true" });

        // --- GitVersion Activities ---
        [Temporalio.Activities.Activity]
        public Task LoadRepoContextAsync(string repository, string commitSha, string branch) =>
            Task.CompletedTask;

        [Temporalio.Activities.Activity]
        public Task<VersionInfo> ComputeVersionAsync(string repository, string branch) =>
            Task.FromResult(TestDataFactory.CreateVersionInfo(branch: branch));

        [Temporalio.Activities.Activity]
        public Task PersistVersionMetadataAsync(VersionInfo versionInfo) =>
            Task.CompletedTask;

        // --- Publish Activities ---
        [Temporalio.Activities.Activity]
        public Task<ImageMetadata> PrepareImageMetadataAsync(
            NormalizedPipelineMetadata metadata, VersionInfo version) =>
            Task.FromResult(new ImageMetadata(
                metadata.Repository, version.SemVer, null,
                "registry.example.com",
                $"registry.example.com/{metadata.Repository}:{version.SemVer}"));

        [Temporalio.Activities.Activity]
        public Task BuildOrFinalizeImageAsync(ImageMetadata image, string commitSha) =>
            Task.CompletedTask;

        [Temporalio.Activities.Activity]
        public Task PushImageAsync(ImageMetadata image) =>
            Task.CompletedTask;

        [Temporalio.Activities.Activity]
        public Task<ImageMetadata> CaptureDigestAsync(ImageMetadata image) =>
            Task.FromResult(image with
            {
                Digest = "sha256:test-digest",
                FullImageRef = $"{image.Registry}/{image.ImageName}@sha256:test-digest",
            });

        [Temporalio.Activities.Activity]
        public Task<ReleaseManifest> WriteReleaseManifestAsync(
            NormalizedPipelineMetadata metadata, VersionInfo version, ImageMetadata image) =>
            Task.FromResult(new ReleaseManifest(
                metadata.PipelineId, metadata.Repository, metadata.CommitSha,
                version, image, DateTimeOffset.UtcNow,
                new Dictionary<string, string> { ["branch"] = metadata.Branch }));

        // --- Deploy Activities ---
        [Temporalio.Activities.Activity]
        public Task<DeploymentResult> DeployToEnvironmentAsync(
            DeployEnvironment environment, ImageMetadata image) =>
            Task.FromResult(new DeploymentResult(
                environment, true, null, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1)));

        [Temporalio.Activities.Activity]
        public Task<EnvironmentVerificationResult> VerifyEnvironmentAsync(
            DeployEnvironment environment, ImageMetadata image) =>
            Task.FromResult(new EnvironmentVerificationResult(
                environment, true, new List<string> { "passed" }, null));
    }
}
