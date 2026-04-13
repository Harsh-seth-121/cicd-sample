namespace CicdPipeline.Contracts.Models;

public record VersionInfo(
    string SemVer,
    string MajorMinorPatch,
    string? PreReleaseTag,
    string? BuildMetadata,
    string FullSemVer,
    string InformationalVersion,
    string BranchName,
    string Sha);
