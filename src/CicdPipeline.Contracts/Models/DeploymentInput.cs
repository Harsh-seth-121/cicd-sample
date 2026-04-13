namespace CicdPipeline.Contracts.Models;

public record DeploymentInput(
    NormalizedPipelineMetadata Metadata,
    ReleaseManifest Manifest);
