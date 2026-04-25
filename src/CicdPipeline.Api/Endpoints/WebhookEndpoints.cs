using System.Diagnostics;
using CicdPipeline.Api.Models;
using CicdPipeline.Contracts;
using CicdPipeline.Contracts.Enums;
using CicdPipeline.Contracts.Models;
using CicdPipeline.ServiceDefaults;
using CicdPipeline.Workflows.Workflows;
using Temporalio.Client;

namespace CicdPipeline.Api.Endpoints;

public static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/webhooks").WithTags("Webhooks");

        group.MapPost("/github", HandleGitHubWebhookAsync);
        group.MapPost("/generic", HandleGenericWebhookAsync);
    }

    private static async Task<IResult> HandleGitHubWebhookAsync(
        WebhookPayload payload,
        HttpRequest request,
        TemporalClient client)
    {
        var headers = request.Headers
            .ToDictionary(h => h.Key, h => h.Value.ToString());

        var trigger = new PipelineTrigger(
            Repository: payload.Repository,
            CommitSha: payload.CommitSha,
            Ref: payload.Ref,
            EventType: payload.EventType,
            TriggerType: TriggerType.Webhook,
            SenderLogin: payload.SenderLogin,
            ReceivedAt: DateTimeOffset.UtcNow,
            RawHeaders: headers,
            RawPayload: null);

        return await StartPipelineAsync(client, trigger);
    }

    private static async Task<IResult> HandleGenericWebhookAsync(
        WebhookPayload payload,
        TemporalClient client)
    {
        var trigger = new PipelineTrigger(
            Repository: payload.Repository,
            CommitSha: payload.CommitSha,
            Ref: payload.Ref,
            EventType: payload.EventType,
            TriggerType: TriggerType.Webhook,
            SenderLogin: payload.SenderLogin,
            ReceivedAt: DateTimeOffset.UtcNow,
            RawHeaders: new Dictionary<string, string>(),
            RawPayload: null);

        return await StartPipelineAsync(client, trigger);
    }

    private static async Task<IResult> StartPipelineAsync(TemporalClient client, PipelineTrigger trigger)
    {
        var shortSha = trigger.CommitSha[..Math.Min(7, trigger.CommitSha.Length)];
        var workflowId = WorkflowIds.PipelineIngress(trigger.Repository, shortSha);

        var handle = await client.StartWorkflowAsync(
            (PipelineIngressWorkflow wf) => wf.RunAsync(trigger),
            new WorkflowOptions
            {
                Id = workflowId,
                TaskQueue = TaskQueues.Orchestrator,
            });

        var branch = trigger.Ref.Replace("refs/heads/", "");
        CicdPipelineMetrics.PipelineStarted.Add(1, new TagList
        {
            { "repository", trigger.Repository },
            { "trigger_type", trigger.TriggerType.ToString() },
            { "branch", branch },
        });

        return Results.Accepted(
            $"/api/ops/pipelines/{handle.Id}/status",
            new StartPipelineResponse(handle.Id, handle.ResultRunId!));
    }
}
