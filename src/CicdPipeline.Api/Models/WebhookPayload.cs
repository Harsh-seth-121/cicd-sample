namespace CicdPipeline.Api.Models;

public record WebhookPayload(
    string Repository,
    string CommitSha,
    string Ref,
    string EventType,
    string? SenderLogin);
