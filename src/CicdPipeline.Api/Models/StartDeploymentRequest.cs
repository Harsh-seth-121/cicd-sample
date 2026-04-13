using CicdPipeline.Contracts.Models;

namespace CicdPipeline.Api.Models;

public record StartDeploymentRequest(
    NormalizedPipelineMetadata Metadata,
    ReleaseManifest Manifest);
