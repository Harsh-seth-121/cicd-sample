using CicdPipeline.Api.Models;
using CicdPipeline.Contracts;
using CicdPipeline.Contracts.Enums;
using CicdPipeline.Contracts.Models;
using CicdPipeline.Workflows.Workflows;
using Temporalio.Client;

namespace CicdPipeline.Api.Endpoints;

public static class OpsEndpoints
{
    public static void MapOpsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/ops").WithTags("Operations");

        group.MapGet("/pipelines/{workflowId}/status", GetPipelineStatusAsync);
        group.MapPost("/pipelines/{workflowId}/pause", PausePipelineAsync);
        group.MapPost("/pipelines/{workflowId}/cancel", CancelPipelineAsync);
        group.MapPost("/pipelines/{workflowId}/resume", ResumePipelineAsync);
        group.MapPost("/deployments/start", StartDeploymentAsync);
        group.MapGet("/pipelines", ListPipelinesAsync);
    }

    private static async Task<IResult> GetPipelineStatusAsync(
        string workflowId,
        TemporalClient client)
    {
        var status = await QueryStatusByPrefix(workflowId, client);
        return Results.Ok(status);
    }

    private static async Task<IResult> PausePipelineAsync(
        string workflowId,
        SignalRequest request,
        TemporalClient client)
    {
        await SignalByPrefix(workflowId, client, "PauseAsync", request.Reason);
        return Results.Ok(new { status = "paused", workflowId });
    }

    private static async Task<IResult> CancelPipelineAsync(
        string workflowId,
        SignalRequest request,
        TemporalClient client)
    {
        await SignalByPrefix(workflowId, client, "CancelAsync", request.Reason);
        return Results.Ok(new { status = "cancelled", workflowId });
    }

    private static async Task<IResult> ResumePipelineAsync(
        string workflowId,
        ResumeRequest request,
        TemporalClient client)
    {
        var command = new OperatorOverride(
            Action: OperatorAction.Resume,
            Reason: request.Reason,
            OverrideStage: null,
            OperatorId: request.OperatorId);

        await UpdateByPrefix(workflowId, client, command);
        return Results.Ok(new { status = "resumed", workflowId });
    }

    private static async Task<IResult> StartDeploymentAsync(
        StartDeploymentRequest request,
        TemporalClient client)
    {
        var input = new DeploymentInput(request.Metadata, request.Manifest);
        var workflowId = WorkflowIds.Deployment(request.Metadata.PipelineId);

        var handle = await client.StartWorkflowAsync(
            (DeploymentWorkflow wf) => wf.RunAsync(input),
            new WorkflowOptions
            {
                Id = workflowId,
                TaskQueue = TaskQueues.Orchestrator,
            });

        return Results.Accepted(
            $"/api/ops/pipelines/{handle.Id}/status",
            new StartPipelineResponse(handle.Id, handle.ResultRunId!));
    }

    private static async Task<IResult> ListPipelinesAsync(
        TemporalClient client,
        string? repository = null,
        string? status = null)
    {
        var queryParts = new List<string>();
        if (!string.IsNullOrEmpty(repository))
            queryParts.Add($"CicdRepository = '{repository}'");
        if (!string.IsNullOrEmpty(status))
            queryParts.Add($"CicdPipelineStatus = '{status}'");

        var query = queryParts.Count > 0
            ? string.Join(" AND ", queryParts)
            : null;

        var workflows = new List<object>();
        await foreach (var wf in client.ListWorkflowsAsync(query ?? ""))
        {
            workflows.Add(new
            {
                wf.Id,
                wf.RunId,
                Status = wf.Status.ToString(),
                wf.StartTime,
                wf.CloseTime,
            });
        }

        return Results.Ok(workflows);
    }

    private static async Task<PipelineStageStatus> QueryStatusByPrefix(string workflowId, TemporalClient client)
    {
        if (workflowId.StartsWith("build-"))
        {
            var handle = client.GetWorkflowHandle<BuildValidationWorkflow>(workflowId);
            return await handle.QueryAsync(wf => wf.GetStatus());
        }

        if (workflowId.StartsWith("verpub-"))
        {
            var handle = client.GetWorkflowHandle<VersionAndPublishWorkflow>(workflowId);
            return await handle.QueryAsync(wf => wf.GetStatus());
        }

        if (workflowId.StartsWith("deploy-"))
        {
            var handle = client.GetWorkflowHandle<DeploymentWorkflow>(workflowId);
            return await handle.QueryAsync(wf => wf.GetStatus());
        }

        // Default: PipelineIngressWorkflow (prefix: pipeline-)
        var ingressHandle = client.GetWorkflowHandle<PipelineIngressWorkflow>(workflowId);
        return await ingressHandle.QueryAsync(wf => wf.GetStatus());
    }

    private static async Task SignalByPrefix(string workflowId, TemporalClient client, string signalName, string reason)
    {
        if (workflowId.StartsWith("build-"))
        {
            var handle = client.GetWorkflowHandle<BuildValidationWorkflow>(workflowId);
            if (signalName == "PauseAsync")
                await handle.SignalAsync(wf => wf.PauseAsync(reason));
            else
                await handle.SignalAsync(wf => wf.CancelAsync(reason));
            return;
        }

        if (workflowId.StartsWith("verpub-"))
        {
            var handle = client.GetWorkflowHandle<VersionAndPublishWorkflow>(workflowId);
            if (signalName == "PauseAsync")
                await handle.SignalAsync(wf => wf.PauseAsync(reason));
            else
                await handle.SignalAsync(wf => wf.CancelAsync(reason));
            return;
        }

        if (workflowId.StartsWith("deploy-"))
        {
            var handle = client.GetWorkflowHandle<DeploymentWorkflow>(workflowId);
            if (signalName == "PauseAsync")
                await handle.SignalAsync(wf => wf.PauseAsync(reason));
            else
                await handle.SignalAsync(wf => wf.CancelAsync(reason));
            return;
        }

        // Default: PipelineIngressWorkflow
        var ingressHandle = client.GetWorkflowHandle<PipelineIngressWorkflow>(workflowId);
        if (signalName == "PauseAsync")
            await ingressHandle.SignalAsync(wf => wf.PauseAsync(reason));
        else
            await ingressHandle.SignalAsync(wf => wf.CancelAsync(reason));
    }

    private static async Task UpdateByPrefix(string workflowId, TemporalClient client, OperatorOverride command)
    {
        if (workflowId.StartsWith("build-"))
        {
            var handle = client.GetWorkflowHandle<BuildValidationWorkflow>(workflowId);
            await handle.ExecuteUpdateAsync(wf => wf.ResumeAsync(command));
            return;
        }

        if (workflowId.StartsWith("verpub-"))
        {
            var handle = client.GetWorkflowHandle<VersionAndPublishWorkflow>(workflowId);
            await handle.ExecuteUpdateAsync(wf => wf.ResumeAsync(command));
            return;
        }

        if (workflowId.StartsWith("deploy-"))
        {
            var handle = client.GetWorkflowHandle<DeploymentWorkflow>(workflowId);
            await handle.ExecuteUpdateAsync(wf => wf.ResumeAsync(command));
            return;
        }

        // Default: PipelineIngressWorkflow
        var ingressHandle = client.GetWorkflowHandle<PipelineIngressWorkflow>(workflowId);
        await ingressHandle.ExecuteUpdateAsync(wf => wf.ResumeAsync(command));
    }
}
