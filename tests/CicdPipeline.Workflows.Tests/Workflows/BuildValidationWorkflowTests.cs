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

public class BuildValidationWorkflowTests : IAsyncLifetime
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
    public async Task AllGatesPass_ReturnsValidationPassed()
    {
        var metadata = TestDataFactory.CreateMetadata();
        var stub = new BuildTestActivitiesStub();

        var result = await ExecuteWorkflowAsync(metadata, stub);

        Assert.True(result.Passed);
        Assert.Null(result.FailedAtStage);
        Assert.True(result.Duration > TimeSpan.Zero);
        Assert.Contains("passed", result.TestEvidence.Keys);
    }

    [Fact]
    public async Task BuildFails_ReturnsFailed_AtBuildStage()
    {
        var metadata = TestDataFactory.CreateMetadata();
        var stub = new BuildTestActivitiesStub { BuildShouldFail = true };

        var result = await ExecuteWorkflowAsync(metadata, stub);

        Assert.False(result.Passed);
        Assert.Equal(BuildStage.BuildFailed, result.FailedAtStage);
        Assert.NotNull(result.FailureReason);
    }

    [Fact]
    public async Task UnitTestsFail_ReturnsFailed()
    {
        var metadata = TestDataFactory.CreateMetadata();
        var stub = new BuildTestActivitiesStub { UnitTestsShouldFail = true };

        var result = await ExecuteWorkflowAsync(metadata, stub);

        Assert.False(result.Passed);
        Assert.Equal(BuildStage.UnitFailed, result.FailedAtStage);
    }

    [Fact]
    public async Task IntegrationTestsFail_ReturnsFailed()
    {
        var metadata = TestDataFactory.CreateMetadata();
        var stub = new BuildTestActivitiesStub { IntegrationTestsShouldFail = true };

        var result = await ExecuteWorkflowAsync(metadata, stub);

        Assert.False(result.Passed);
        Assert.Equal(BuildStage.IntegrationFailed, result.FailedAtStage);
    }

    [Fact]
    public async Task ScansFail_ReturnsFailed()
    {
        var metadata = TestDataFactory.CreateMetadata();
        var stub = new BuildTestActivitiesStub { ScansShouldFail = true };

        var result = await ExecuteWorkflowAsync(metadata, stub);

        Assert.False(result.Passed);
        Assert.Equal(BuildStage.ScanFailed, result.FailedAtStage);
    }

    private async Task<BuildValidationResult> ExecuteWorkflowAsync(
        NormalizedPipelineMetadata metadata, BuildTestActivitiesStub stubActivities)
    {
        var handle = await _env.Client.StartWorkflowAsync(
            (BuildValidationWorkflow wf) => wf.RunAsync(metadata),
            new WorkflowOptions
            {
                Id = $"test-build-{Guid.NewGuid()}",
                TaskQueue = TaskQueues.BuildTest,
            });

        var resultTask = handle.GetResultAsync();

        using var worker = new TemporalWorker(
            _env.Client,
            new TemporalWorkerOptions(TaskQueues.BuildTest)
                .AddWorkflow<BuildValidationWorkflow>()
                .AddAllActivities(stubActivities));

        return await worker.ExecuteAsync(() => resultTask);
    }

    private class BuildTestActivitiesStub
    {
        public bool BuildShouldFail { get; set; }
        public bool UnitTestsShouldFail { get; set; }
        public bool IntegrationTestsShouldFail { get; set; }
        public bool ScansShouldFail { get; set; }

        [Temporalio.Activities.Activity]
        public Task CheckoutSourceAsync(string repository, string commitSha, string branch) =>
            Task.CompletedTask;

        [Temporalio.Activities.Activity]
        public Task BuildAsync(string repository, string commitSha) =>
            BuildShouldFail
                ? throw new ApplicationFailureException("Build failed", nonRetryable: true)
                : Task.CompletedTask;

        [Temporalio.Activities.Activity]
        public Task<Dictionary<string, string>> RunUnitTestsAsync(string repository, string commitSha) =>
            UnitTestsShouldFail
                ? throw new ApplicationFailureException("Unit tests failed", nonRetryable: true)
                : Task.FromResult(new Dictionary<string, string> { ["passed"] = "100", ["failed"] = "0" });

        [Temporalio.Activities.Activity]
        public Task<Dictionary<string, string>> RunIntegrationTestsAsync(string repository, string commitSha) =>
            IntegrationTestsShouldFail
                ? throw new ApplicationFailureException("Integration tests failed", nonRetryable: true)
                : Task.FromResult(new Dictionary<string, string> { ["passed"] = "50", ["failed"] = "0" });

        [Temporalio.Activities.Activity]
        public Task<Dictionary<string, string>> RunRequiredScansAsync(string repository, string commitSha) =>
            ScansShouldFail
                ? throw new ApplicationFailureException("Scans failed", nonRetryable: true)
                : Task.FromResult(new Dictionary<string, string> { ["policy_compliant"] = "true" });
    }
}
