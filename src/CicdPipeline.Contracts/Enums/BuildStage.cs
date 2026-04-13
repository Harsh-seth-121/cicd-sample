namespace CicdPipeline.Contracts.Enums;

public enum BuildStage
{
    Pending,
    Checkout,
    Build,
    UnitTests,
    IntegrationTests,
    RequiredScans,
    ValidationPassed,
    CheckoutFailed,
    BuildFailed,
    UnitFailed,
    IntegrationFailed,
    ScanFailed,
    Failed,
}
