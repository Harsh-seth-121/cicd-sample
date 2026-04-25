using System.Diagnostics;
using CicdPipeline.Contracts.Models;
using CicdPipeline.ServiceDefaults;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;

namespace CicdPipeline.Workflows.Activities;

public class GitVersionActivities
{
    private readonly ILogger<GitVersionActivities> _logger;

    public GitVersionActivities(ILogger<GitVersionActivities> logger)
    {
        _logger = logger;
    }

    [Activity]
    public async Task LoadRepoContextAsync(string repository, string commitSha, string branch)
    {
        _logger.LogInformation(
            "Loading repo context for {Repository}@{Sha} (branch: {Branch})",
            repository, commitSha, branch);

        // TODO: Verify repo is a full clone with correct ref, GitVersion config exists
        await Task.Delay(50);

        CicdPipelineMetrics.ActivityExecuted.Add(1, new TagList
        {
            { "activity", "LoadRepoContext" },
            { "task_queue", "cicd.gitversion" },
        });
    }

    [Activity]
    public async Task<VersionInfo> ComputeVersionAsync(string repository, string branch)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("Computing version for {Repository} on branch {Branch}", repository, branch);

        // TODO: Run `gitversion /output json` CLI, parse output into VersionInfo
        await Task.Delay(100);

        var preRelease = branch == "main" ? null : $"feature.1";
        var semVer = branch == "main" ? "1.0.0" : "1.0.1-feature.1";

        CicdPipelineMetrics.ActivityExecuted.Add(1, new TagList
        {
            { "activity", "ComputeVersion" },
            { "task_queue", "cicd.gitversion" },
        });
        CicdPipelineMetrics.StageDuration.Record(sw.Elapsed.TotalSeconds, new TagList
        {
            { "stage", "ComputeVersion" },
        });

        return new VersionInfo(
            SemVer: semVer,
            MajorMinorPatch: "1.0.0",
            PreReleaseTag: preRelease,
            BuildMetadata: null,
            FullSemVer: semVer,
            InformationalVersion: $"{semVer}+Branch.{branch}",
            BranchName: branch,
            Sha: "stub-sha");
    }

    [Activity]
    public async Task PersistVersionMetadataAsync(VersionInfo versionInfo)
    {
        _logger.LogInformation("Persisting version metadata: {SemVer}", versionInfo.SemVer);

        // TODO: Write version metadata to a shared store
        await Task.Delay(50);
    }
}
