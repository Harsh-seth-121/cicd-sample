namespace CicdPipeline.Contracts.Enums;

public enum PipelineStatus
{
    Received,
    Validating,
    Building,
    Testing,
    Scanning,
    Versioning,
    Publishing,
    DeployingDev,
    VerifyingDev,
    DeployingQa,
    VerifyingQa,
    Succeeded,
    Failed,
    Cancelled,
    Paused,
    Skipped,
}
