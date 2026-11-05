using Ai.API.Entities.Enums;

namespace Ai.API.Entities;

// One row per client RequestId: idempotency (a retry never calls Gemini or charges twice), the
// one-in-flight-request-per-user rule, and the token ledger behind the 7-day usage chart.
public class AiChatRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid RequestId { get; set; }
    public Guid? SessionId { get; set; }
    public string Status { get; set; } = ChatRequestStatus.Processing;
    public int? InputTokens { get; set; }
    public int? TotalTokens { get; set; }
    public Guid? ReplyMessageId { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LeaseUntil { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
