using CicdPipeline.Contracts.Models;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;

namespace CicdPipeline.Workflows.Activities;

public class BuildTestActivities
{
    private readonly ILogger<BuildTestActivities> _logger;

    public BuildTestActivities(ILogger<BuildTestActivities> logger)
    {
        _logger = logger;
    }

    [Activity]
    public async Task CheckoutSourceAsync(string repository, string commitSha, string branch)
    {
        _logger.LogInformation(
            "Checking out {Repository}@{Sha} (branch: {Branch})",
            repository, commitSha, branch);

        // TODO: Shell out to runner pool to clone repo at specific SHA
        await Task.Delay(100); // Simulate work
    }

    [Activity]
    public async Task BuildAsync(string repository, string commitSha)
    {
        _logger.LogInformation("Building {Repository}@{Sha}", repository, commitSha);

        // TODO: Shell out to runner pool to compile/package
        for (var i = 0; i < 3; i++)
        {
            ActivityExecutionContext.Current.Heartbeat();
            await Task.Delay(100); // Simulate build progress
        }
    }

    [Activity]
    public async Task<Dictionary<string, string>> RunUnitTestsAsync(string repository, string commitSha)
    {
        _logger.LogInformation("Running unit tests for {Repository}@{Sha}", repository, commitSha);

        // TODO: Shell out to test runner
        for (var i = 0; i < 2; i++)
        {
            ActivityExecutionContext.Current.Heartbeat();
            await Task.Delay(100);
        }

        return new Dictionary<string, string>
        {
            ["passed"] = "142",
            ["failed"] = "0",
            ["skipped"] = "3",
            ["coverage"] = "87.2%",
        };
    }

    [Activity]
    public async Task<Dictionary<string, string>> RunIntegrationTestsAsync(string repository, string commitSha)
    {
        _logger.LogInformation("Running integration tests for {Repository}@{Sha}", repository, commitSha);

        // TODO: Shell out to integration test runner
        for (var i = 0; i < 3; i++)
        {
            ActivityExecutionContext.Current.Heartbeat();
            await Task.Delay(100);
        }

        return new Dictionary<string, string>
        {
            ["passed"] = "48",
            ["failed"] = "0",
            ["skipped"] = "1",
        };
    }

    [Activity]
    public async Task<Dictionary<string, string>> RunRequiredScansAsync(string repository, string commitSha)
    {
        _logger.LogInformation("Running required scans for {Repository}@{Sha}", repository, commitSha);

        // TODO: Shell out to security/policy scanner
        for (var i = 0; i < 2; i++)
        {
            ActivityExecutionContext.Current.Heartbeat();
            await Task.Delay(100);
        }

        return new Dictionary<string, string>
        {
            ["vulnerabilities_critical"] = "0",
            ["vulnerabilities_high"] = "0",
            ["policy_compliant"] = "true",
        };
    }
}
