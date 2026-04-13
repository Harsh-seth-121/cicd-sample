namespace CicdPipeline.Contracts.Models;

public record ImageMetadata(
    string ImageName,
    string Tag,
    string? Digest,
    string Registry,
    string FullImageRef);
