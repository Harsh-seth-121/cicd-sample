using CicdPipeline.Contracts.Enums;

namespace CicdPipeline.Contracts.Models;

public record OperatorOverride(
    OperatorAction Action,
    string? Reason,
    string? OverrideStage,
    string OperatorId);
