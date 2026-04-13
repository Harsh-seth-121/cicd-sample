using CicdPipeline.Contracts.Enums;

namespace CicdPipeline.Contracts.Models;

public record EnvironmentVerificationResult(
    DeployEnvironment Environment,
    bool Healthy,
    List<string> CheckResults,
    string? FailureDetail);
