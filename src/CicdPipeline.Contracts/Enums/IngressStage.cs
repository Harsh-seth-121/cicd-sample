namespace CicdPipeline.Contracts.Enums;

public enum IngressStage
{
    Received,
    ValidatingEvent,
    Invalid,
    Normalizing,
    DeriveIdentity,
    Deduplicate,
    DuplicateIgnored,
    StartWorkflow,
    UpsertSearchAttributes,
    DispatchBuildValidation,
    Completed,
}
