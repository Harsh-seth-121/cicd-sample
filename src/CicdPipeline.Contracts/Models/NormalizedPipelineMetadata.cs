using CicdPipeline.Contracts.Enums;

namespace CicdPipeline.Contracts.Models;

public record NormalizedPipelineMetadata(
    string PipelineId,
    string Repository,
    string CommitSha,
    string ShortSha,
    string Branch,
    BranchClassification BranchClassification,
    TriggerType TriggerType,
    DateTimeOffset ReceivedAt);
