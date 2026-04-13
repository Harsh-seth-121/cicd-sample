using CicdPipeline.Contracts.Enums;

namespace CicdPipeline.Contracts.Models;

public record DeploymentResult(
    DeployEnvironment Environment,
    bool Succeeded,
    string? FailureReason,
    DateTimeOffset DeployedAt,
    TimeSpan Duration);
