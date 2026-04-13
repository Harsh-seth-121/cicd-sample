using CicdPipeline.Contracts.Enums;

namespace CicdPipeline.Contracts.Models;

public record PipelineTrigger(
    string Repository,
    string CommitSha,
    string Ref,
    string EventType,
    TriggerType TriggerType,
    string? SenderLogin,
    DateTimeOffset ReceivedAt,
    Dictionary<string, string> RawHeaders,
    string? RawPayload);
