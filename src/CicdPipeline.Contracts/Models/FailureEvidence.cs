namespace CicdPipeline.Contracts.Models;

public record FailureEvidence(
    string Stage,
    string Reason,
    DateTimeOffset OccurredAt,
    Dictionary<string, string> DiagnosticData);
