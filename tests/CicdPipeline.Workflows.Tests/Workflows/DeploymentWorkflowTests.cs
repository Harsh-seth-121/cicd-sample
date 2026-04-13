using CicdPipeline.Contracts;
using CicdPipeline.Contracts.Enums;
using CicdPipeline.Contracts.Models;
using CicdPipeline.Workflows.Activities;
using CicdPipeline.Workflows.Tests.Fixtures;
using CicdPipeline.Workflows.Workflows;
using Temporalio.Client;
using Temporalio.Testing;
using Temporalio.Worker;

namespace CicdPipeline.Workflows.Tests.Workflows;

public class DeploymentWorkflowTests : IAsyncLifetime
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
    public async Task FeatureBranch_DeploysDevOnly_ReturnsSuccess()
    {
        var input = TestDataFactory.CreateDeploymentInput(BranchClassification.Feature);

        var result = await ExecuteWorkflowAsync(input, stubActivities: new DeployActivitiesStub());

        Assert.True(result.Succeeded);
        Assert.Equal(DeployEnvironment.Dev, result.Environment);
    }

    [Fact]
    public async Task MainBranch_DeploysDevAndQa_ReturnsSuccess()
    {
        var input = TestDataFactory.CreateDeploymentInput(BranchClassification.Main);

        var result = await ExecuteWorkflowAsync(input, stubActivities: new DeployActivitiesStub());

        Assert.True(result.Succeeded);
        Assert.Equal(DeployEnvironment.Qa, result.Environment);
    }

    [Fact]
    public async Task DevDeployFails_ReturnsFailed()
    {
        var input = TestDataFactory.CreateDeploymentInput(BranchClassification.Main);
        var stub = new DeployActivitiesStub { DevDeploySucceeds = false };

        var result = await ExecuteWorkflowAsync(input, stubActivities: stub);

        Assert.False(result.Succeeded);
        Assert.Equal(DeployEnvironment.Dev, result.Environment);
    }

    [Fact]
    public async Task DevVerifyFails_ReturnsFailed()
    {
        var input = TestDataFactory.CreateDeploymentInput(BranchClassification.Main);
        var stub = new DeployActivitiesStub { DevVerifyHealthy = false };

        var result = await ExecuteWorkflowAsync(input, stubActivities: stub);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task QaDeployFails_AfterDevSuccess_ReturnsFailed()
    {
        var input = TestDataFactory.CreateDeploymentInput(BranchClassification.Main);
        var stub = new DeployActivitiesStub { QaDeploySucceeds = false };

        var result = await ExecuteWorkflowAsync(input, stubActivities: stub);

        Assert.False(result.Succeeded);
        Assert.Equal(DeployEnvironment.Qa, result.Environment);
    }

    [Fact]
    public async Task SameImageDigest_FlowsToBothEnvironments()
    {
        var input = TestDataFactory.CreateDeploymentInput(BranchClassification.Main);
        var stub = new DeployActivitiesStub();

        await ExecuteWorkflowAsync(input, stubActivities: stub);

        // Both deploy calls should have received the exact same image reference
        Assert.Equal(2, stub.DeployedImages.Count);
        Assert.Equal(stub.DeployedImages[0].FullImageRef, stub.DeployedImages[1].FullImageRef);
        Assert.Equal(stub.DeployedImages[0].Digest, stub.DeployedImages[1].Digest);
    }

    private async Task<DeploymentResult> ExecuteWorkflowAsync(
        DeploymentInput input, DeployActivitiesStub stubActivities)
    {
        var handle = await _env.Client.StartWorkflowAsync(
            (DeploymentWorkflow wf) => wf.RunAsync(input),
            new WorkflowOptions
            {
                Id = $"test-deploy-{Guid.NewGuid()}",
                TaskQueue = TaskQueues.Deploy,
            });

        var resultTask = handle.GetResultAsync();

        using var worker = new TemporalWorker(
            _env.Client,
            new TemporalWorkerOptions(TaskQueues.Deploy)
                .AddWorkflow<DeploymentWorkflow>()
                .AddAllActivities(stubActivities));

        return await worker.ExecuteAsync(() => resultTask);
    }

    private class DeployActivitiesStub
    {
        public bool DevDeploySucceeds { get; set; } = true;
        public bool DevVerifyHealthy { get; set; } = true;
        public bool QaDeploySucceeds { get; set; } = true;
        public bool QaVerifyHealthy { get; set; } = true;
        public List<ImageMetadata> DeployedImages { get; } = new();

        [Temporalio.Activities.Activity]
        public Task<DeploymentResult> DeployToEnvironmentAsync(
            DeployEnvironment environment, ImageMetadata image)
        {
            DeployedImages.Add(image);
            var succeeds = environment == DeployEnvironment.Dev ? DevDeploySucceeds : QaDeploySucceeds;
            return Task.FromResult(new DeploymentResult(
                environment, succeeds, succeeds ? null : "Deploy failed",
                DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1)));
        }

        [Temporalio.Activities.Activity]
        public Task<EnvironmentVerificationResult> VerifyEnvironmentAsync(
            DeployEnvironment environment, ImageMetadata image)
        {
            var healthy = environment == DeployEnvironment.Dev ? DevVerifyHealthy : QaVerifyHealthy;
            return Task.FromResult(new EnvironmentVerificationResult(
                environment, healthy,
                new List<string> { healthy ? "passed" : "failed" },
                healthy ? null : "Verification failed"));
        }
    }
}
