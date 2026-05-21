namespace CicdPipeline.Contracts;

public static class WorkflowIds
{
    public static string PipelineIngress(string repo, string shortSha) =>
        $"pipeline-{repo.Replace('/', '_')}-{shortSha}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

    public static string BuildValidation(string pipelineId) =>
        $"build-{pipelineId}";

    public static string VersionAndPublish(string pipelineId) =>
        $"verpub-{pipelineId}";

    public static string Deployment(string pipelineId) =>
        $"deploy-{pipelineId}";
}
