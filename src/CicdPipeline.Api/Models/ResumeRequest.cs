using CicdPipeline.Contracts.Enums;

namespace CicdPipeline.Api.Models;

public record ResumeRequest(string OperatorId, string? Reason);
