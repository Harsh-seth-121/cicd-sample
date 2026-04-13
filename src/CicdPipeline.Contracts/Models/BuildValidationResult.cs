using CicdPipeline.Contracts.Enums;

namespace CicdPipeline.Contracts.Models;

public record BuildValidationResult(
    bool Passed,
    BuildStage? FailedAtStage,
    string? FailureReason,
    Dictionary<string, string> TestEvidence,
    TimeSpan Duration);
