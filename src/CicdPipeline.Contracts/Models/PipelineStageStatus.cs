using CicdPipeline.Contracts.Enums;

namespace CicdPipeline.Contracts.Models;

public record PipelineStageStatus(
    string PipelineId,
    PipelineStatus Status,
    string CurrentStage,
    bool IsPaused,
    bool IsCancelled,
    DateTimeOffset LastUpdated,
    List<FailureEvidence> Failures);
