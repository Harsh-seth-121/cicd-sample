namespace CicdPipeline.Contracts;

public static class TaskQueues
{
    public const string Orchestrator = "cicd.pipeline.orchestrator";
    public const string BuildTest = "cicd.build.test";
    public const string GitVersion = "cicd.gitversion";
    public const string Publish = "cicd.publish";
    public const string Deploy = "cicd.deploy";
}
