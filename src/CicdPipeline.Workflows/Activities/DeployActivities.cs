using CicdPipeline.Contracts.Enums;
using CicdPipeline.Contracts.Models;
using CicdPipeline.ServiceDefaults;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;

namespace CicdPipeline.Workflows.Activities;

public class DeployActivities
{
    private const string TaskQueue = "cicd.deploy";

    private readonly ILogger<DeployActivities> _logger;

    public DeployActivities(ILogger<DeployActivities> logger)
    {
        _logger = logger;
    }

    [Activity]
    public Task<DeploymentResult> DeployToEnvironmentAsync(
        DeployEnvironment environment, ImageMetadata image) =>
        CicdPipelineMetrics.TrackActivity(
            "DeployToEnvironment",
            TaskQueue,
            async () =>
            {
                _logger.LogInformation(
                    "Deploying {FullImageRef} to {Environment}",
                    image.FullImageRef, environment);

                // TODO: Shell out to deployment tool (kubectl, helm, argo, etc.)
                for (var i = 0; i < 2; i++)
                {
                    ActivityExecutionContext.Current.Heartbeat();
                    await Task.Delay(100);
                }

                return new DeploymentResult(
                    Environment: environment,
                    Succeeded: true,
                    FailureReason: null,
                    DeployedAt: DateTimeOffset.UtcNow,
                    Duration: TimeSpan.FromSeconds(5));
            },
            ("environment", environment.ToString()));

    [Activity]
    public Task<EnvironmentVerificationResult> VerifyEnvironmentAsync(
        DeployEnvironment environment, ImageMetadata image) =>
        CicdPipelineMetrics.TrackActivity(
            "VerifyEnvironment",
            TaskQueue,
            async () =>
            {
                _logger.LogInformation(
                    "Verifying {Environment} deployment of {FullImageRef}",
                    environment, image.FullImageRef);

                // TODO: Run health checks, smoke tests, policy checks
                for (var i = 0; i < 2; i++)
                {
                    ActivityExecutionContext.Current.Heartbeat();
                    await Task.Delay(100);
                }

                return new EnvironmentVerificationResult(
                    Environment: environment,
                    Healthy: true,
                    CheckResults: new List<string>
                    {
                        "health-check: passed",
                        "smoke-test: passed",
                        "policy-check: passed",
                    },
                    FailureDetail: null);
            },
            ("environment", environment.ToString()));
}
