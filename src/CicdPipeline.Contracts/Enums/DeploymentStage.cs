namespace CicdPipeline.Contracts.Enums;

public enum DeploymentStage
{
    Published,
    DeployDev,
    VerifyDev,
    DevReady,
    BranchDecision,
    DeployQa,
    VerifyQa,
    SuccessDevOnly,
    SuccessDevQa,
    DevDeployFailed,
    DevVerifyFailed,
    QaDeployFailed,
    QaVerifyFailed,
    Failed,
}
