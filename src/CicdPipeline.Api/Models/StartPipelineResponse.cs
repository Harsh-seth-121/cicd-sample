namespace CicdPipeline.Api.Models;

public record StartPipelineResponse(
    string WorkflowId,
    string RunId);
