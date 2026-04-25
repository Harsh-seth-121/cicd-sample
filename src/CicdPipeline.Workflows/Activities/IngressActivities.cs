using System.Diagnostics;
using CicdPipeline.Contracts;
using CicdPipeline.Contracts.Enums;
using CicdPipeline.Contracts.Models;
using CicdPipeline.ServiceDefaults;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;

namespace CicdPipeline.Workflows.Activities;

public class IngressActivities
{
    private readonly ILogger<IngressActivities> _logger;

    public IngressActivities(ILogger<IngressActivities> logger)
    {
        _logger = logger;
    }

    [Activity]
    public async Task<bool> ValidateEventAsync(PipelineTrigger trigger)
    {
        _logger.LogInformation(
            "Validating event: {EventType} for {Repository} at {Sha}",
            trigger.EventType, trigger.Repository, trigger.CommitSha);

        if (string.IsNullOrEmpty(trigger.Repository) || string.IsNullOrEmpty(trigger.CommitSha))
        {
            _logger.LogWarning("Invalid trigger: missing repository or commit SHA");
            return false;
        }

        if (string.IsNullOrEmpty(trigger.Ref))
        {
            _logger.LogWarning("Invalid trigger: missing ref");
            return false;
        }

        // TODO: Validate webhook signature from RawHeaders
        await Task.CompletedTask;

        CicdPipelineMetrics.ActivityExecuted.Add(1, new TagList
        {
            { "activity", "ValidateEvent" },
            { "task_queue", "cicd.pipeline.orchestrator" },
        });

        return true;
    }

    [Activity]
    public async Task<NormalizedPipelineMetadata> NormalizeMetadataAsync(PipelineTrigger trigger)
    {
        await Task.CompletedTask;

        var branch = trigger.Ref.Replace("refs/heads/", "");
        var shortSha = trigger.CommitSha[..Math.Min(7, trigger.CommitSha.Length)];
        var classification = ClassifyBranch(branch);
        var pipelineId = WorkflowIds.PipelineIngress(trigger.Repository, shortSha);

        _logger.LogInformation(
            "Normalized pipeline {PipelineId}: branch={Branch} classification={Classification}",
            pipelineId, branch, classification);

        CicdPipelineMetrics.ActivityExecuted.Add(1, new TagList
        {
            { "activity", "NormalizeMetadata" },
            { "task_queue", "cicd.pipeline.orchestrator" },
        });

        return new NormalizedPipelineMetadata(
            PipelineId: pipelineId,
            Repository: trigger.Repository,
            CommitSha: trigger.CommitSha,
            ShortSha: shortSha,
            Branch: branch,
            BranchClassification: classification,
            TriggerType: trigger.TriggerType,
            ReceivedAt: trigger.ReceivedAt);
    }

    [Activity]
    public async Task<bool> CheckDuplicateAsync(string pipelineId)
    {
        _logger.LogInformation("Checking for duplicate pipeline: {PipelineId}", pipelineId);
        // TODO: Query Temporal for existing workflow with this ID pattern
        await Task.CompletedTask;
        return false; // Stub: no duplicates
    }

    private static BranchClassification ClassifyBranch(string branch) => branch switch
    {
        "main" or "master" => BranchClassification.Main,
        _ when branch.StartsWith("feature/") => BranchClassification.Feature,
        _ when branch.StartsWith("release/") => BranchClassification.Release,
        _ when branch.StartsWith("hotfix/") => BranchClassification.Hotfix,
        _ => BranchClassification.Other,
    };
}
