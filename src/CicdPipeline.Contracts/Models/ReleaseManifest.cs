namespace CicdPipeline.Contracts.Models;

public record ReleaseManifest(
    string PipelineId,
    string Repository,
    string CommitSha,
    VersionInfo Version,
    ImageMetadata Image,
    DateTimeOffset CreatedAt,
    Dictionary<string, string> Labels);
