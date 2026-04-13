using CicdPipeline.Contracts.Enums;
using CicdPipeline.Contracts.Models;

namespace CicdPipeline.Workflows.Tests.Fixtures;

public static class TestDataFactory
{
    public static PipelineTrigger CreateTrigger(
        string repository = "my-org/my-repo",
        string commitSha = "abc1234567890def",
        string @ref = "refs/heads/feature/test",
        string eventType = "push",
        TriggerType triggerType = TriggerType.Webhook) => new(
        Repository: repository,
        CommitSha: commitSha,
        Ref: @ref,
        EventType: eventType,
        TriggerType: triggerType,
        SenderLogin: "test-user",
        ReceivedAt: DateTimeOffset.UtcNow,
        RawHeaders: new Dictionary<string, string>(),
        RawPayload: null);

    public static PipelineTrigger CreateMainBranchTrigger() =>
        CreateTrigger(@ref: "refs/heads/main");

    public static NormalizedPipelineMetadata CreateMetadata(
        string pipelineId = "pipeline-my-repo-abc1234-1234567890",
        string branch = "feature/test",
        BranchClassification classification = BranchClassification.Feature) => new(
        PipelineId: pipelineId,
        Repository: "my-org/my-repo",
        CommitSha: "abc1234567890def",
        ShortSha: "abc1234",
        Branch: branch,
        BranchClassification: classification,
        TriggerType: TriggerType.Webhook,
        ReceivedAt: DateTimeOffset.UtcNow);

    public static NormalizedPipelineMetadata CreateMainBranchMetadata() =>
        CreateMetadata(branch: "main", classification: BranchClassification.Main);

    public static VersionInfo CreateVersionInfo(
        string semVer = "1.0.0",
        string branch = "main") => new(
        SemVer: semVer,
        MajorMinorPatch: "1.0.0",
        PreReleaseTag: null,
        BuildMetadata: null,
        FullSemVer: semVer,
        InformationalVersion: $"{semVer}+Branch.{branch}",
        BranchName: branch,
        Sha: "abc1234567890def");

    public static ImageMetadata CreateImageMetadata(
        string digest = "sha256:abc123def456") => new(
        ImageName: "my-org/my-repo",
        Tag: "1.0.0",
        Digest: digest,
        Registry: "registry.example.com",
        FullImageRef: $"registry.example.com/my-org/my-repo@{digest}");

    public static ReleaseManifest CreateReleaseManifest(
        string? pipelineId = null) => new(
        PipelineId: pipelineId ?? "pipeline-my-repo-abc1234-1234567890",
        Repository: "my-org/my-repo",
        CommitSha: "abc1234567890def",
        Version: CreateVersionInfo(),
        Image: CreateImageMetadata(),
        CreatedAt: DateTimeOffset.UtcNow,
        Labels: new Dictionary<string, string>
        {
            ["branch"] = "main",
            ["sha"] = "abc1234567890def",
            ["semver"] = "1.0.0",
        });

    public static DeploymentInput CreateDeploymentInput(
        BranchClassification classification = BranchClassification.Feature)
    {
        var metadata = classification == BranchClassification.Main
            ? CreateMainBranchMetadata()
            : CreateMetadata(classification: classification);
        return new DeploymentInput(metadata, CreateReleaseManifest(metadata.PipelineId));
    }
}
