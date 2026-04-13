using Temporalio.Common;

namespace CicdPipeline.Contracts;

public static class PipelineSearchAttributes
{
    public static readonly SearchAttributeKey<string> PipelineStatus =
        SearchAttributeKey.CreateKeyword("CicdPipelineStatus");

    public static readonly SearchAttributeKey<string> Branch =
        SearchAttributeKey.CreateKeyword("CicdBranch");

    public static readonly SearchAttributeKey<string> Repository =
        SearchAttributeKey.CreateKeyword("CicdRepository");

    public static readonly SearchAttributeKey<string> CommitSha =
        SearchAttributeKey.CreateKeyword("CicdCommitSha");

    public static readonly SearchAttributeKey<string> Stage =
        SearchAttributeKey.CreateKeyword("CicdStage");

    public static readonly SearchAttributeKey<DateTimeOffset> PipelineStartedAt =
        SearchAttributeKey.CreateDateTimeOffset("CicdPipelineStartedAt");

    public static readonly SearchAttributeKey<string> SemVer =
        SearchAttributeKey.CreateKeyword("CicdSemVer");

    public static readonly SearchAttributeKey<string> ImageDigest =
        SearchAttributeKey.CreateKeyword("CicdImageDigest");

    public static readonly SearchAttributeKey<string> TriggerType =
        SearchAttributeKey.CreateKeyword("CicdTriggerType");
}
