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

public class VersionAndPublishWorkflowTests : IAsyncLifetime
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
    public async Task HappyPath_ReturnsReleaseManifest()
    {
        var metadata = TestDataFactory.CreateMetadata();

        var manifest = await ExecuteWorkflowAsync(metadata, new VersionPublishActivitiesStub());

        Assert.NotNull(manifest);
        Assert.Equal(metadata.Repository, manifest.Repository);
        Assert.Equal(metadata.CommitSha, manifest.CommitSha);
        Assert.NotNull(manifest.Version);
        Assert.NotNull(manifest.Image);
        Assert.NotNull(manifest.Image.Digest);
    }

    [Fact]
    public async Task ComputeVersionFails_ThrowsWorkflowFailure()
    {
        var metadata = TestDataFactory.CreateMetadata();
        var stub = new VersionPublishActivitiesStub { ComputeVersionShouldFail = true };

        await Assert.ThrowsAsync<WorkflowFailedException>(
            () => ExecuteWorkflowAsync(metadata, stub));
    }

    [Fact]
    public async Task ImageBuildFails_ThrowsWorkflowFailure()
    {
        var metadata = TestDataFactory.CreateMetadata();
        var stub = new VersionPublishActivitiesStub { ImageBuildShouldFail = true };

        await Assert.ThrowsAsync<WorkflowFailedException>(
            () => ExecuteWorkflowAsync(metadata, stub));
    }

    [Fact]
    public async Task RegistryPushFails_ThrowsWorkflowFailure()
    {
        var metadata = TestDataFactory.CreateMetadata();
        var stub = new VersionPublishActivitiesStub { PushShouldFail = true };

        await Assert.ThrowsAsync<WorkflowFailedException>(
            () => ExecuteWorkflowAsync(metadata, stub));
    }

    private async Task<ReleaseManifest> ExecuteWorkflowAsync(
        NormalizedPipelineMetadata metadata, VersionPublishActivitiesStub stub)
    {
        var handle = await _env.Client.StartWorkflowAsync(
            (VersionAndPublishWorkflow wf) => wf.RunAsync(metadata),
            new WorkflowOptions
            {
                Id = $"test-verpub-{Guid.NewGuid()}",
                TaskQueue = TaskQueues.GitVersion,
            });

        var resultTask = handle.GetResultAsync();

        using var cts = new CancellationTokenSource();

        using var workflowWorker = new TemporalWorker(
            _env.Client,
            new TemporalWorkerOptions(TaskQueues.GitVersion)
                .AddWorkflow<VersionAndPublishWorkflow>()
                .AddAllActivities(stub));

        using var publishWorker = new TemporalWorker(
            _env.Client,
            new TemporalWorkerOptions(TaskQueues.Publish)
                .AddAllActivities(stub));

        var publishTask = publishWorker.ExecuteAsync(cts.Token);
        try
        {
            return await workflowWorker.ExecuteAsync(() => resultTask);
        }
        finally
        {
            cts.Cancel();
            try { await publishTask; } catch (OperationCanceledException) { }
        }
    }

    private class VersionPublishActivitiesStub
    {
        public bool ComputeVersionShouldFail { get; set; }
        public bool ImageBuildShouldFail { get; set; }
        public bool PushShouldFail { get; set; }

        [Temporalio.Activities.Activity]
        public Task LoadRepoContextAsync(string repository, string commitSha, string branch) =>
            Task.CompletedTask;

        [Temporalio.Activities.Activity]
        public Task<VersionInfo> ComputeVersionAsync(string repository, string branch) =>
            ComputeVersionShouldFail
                ? throw new ApplicationFailureException("GitVersion failed", nonRetryable: true)
                : Task.FromResult(TestDataFactory.CreateVersionInfo(branch: branch));

        [Temporalio.Activities.Activity]
        public Task PersistVersionMetadataAsync(VersionInfo versionInfo) =>
            Task.CompletedTask;

        [Temporalio.Activities.Activity]
        public Task<ImageMetadata> PrepareImageMetadataAsync(
            NormalizedPipelineMetadata metadata, VersionInfo version) =>
            Task.FromResult(new ImageMetadata(
                metadata.Repository, version.SemVer, null,
                "registry.example.com",
                $"registry.example.com/{metadata.Repository}:{version.SemVer}"));

        [Temporalio.Activities.Activity]
        public Task BuildOrFinalizeImageAsync(ImageMetadata image, string commitSha) =>
            ImageBuildShouldFail
                ? throw new ApplicationFailureException("Image build failed", nonRetryable: true)
                : Task.CompletedTask;

        [Temporalio.Activities.Activity]
        public Task PushImageAsync(ImageMetadata image) =>
            PushShouldFail
                ? throw new ApplicationFailureException("Push failed", nonRetryable: true)
                : Task.CompletedTask;

        [Temporalio.Activities.Activity]
        public Task<ImageMetadata> CaptureDigestAsync(ImageMetadata image) =>
            Task.FromResult(image with
            {
                Digest = "sha256:test-digest-123",
                FullImageRef = $"{image.Registry}/{image.ImageName}@sha256:test-digest-123",
            });

        [Temporalio.Activities.Activity]
        public Task<ReleaseManifest> WriteReleaseManifestAsync(
            NormalizedPipelineMetadata metadata, VersionInfo version, ImageMetadata image) =>
            Task.FromResult(new ReleaseManifest(
                metadata.PipelineId, metadata.Repository, metadata.CommitSha,
                version, image, DateTimeOffset.UtcNow,
                new Dictionary<string, string> { ["branch"] = metadata.Branch }));
    }
}
