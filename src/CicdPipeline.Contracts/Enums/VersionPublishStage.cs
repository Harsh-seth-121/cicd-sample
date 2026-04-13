namespace CicdPipeline.Contracts.Enums;

public enum VersionPublishStage
{
    Pending,
    LoadRepoContext,
    ComputeVersion,
    VersionComputed,
    PersistVersionMetadata,
    PrepareImageMetadata,
    BuildOrFinalizeImage,
    PushTagsAndDigest,
    CaptureDigest,
    WriteReleaseManifest,
    Published,
    RepoContextInvalid,
    VersionError,
    ImageBuildFailed,
    RegistryPushFailed,
    ManifestWriteFailed,
    Failed,
}
