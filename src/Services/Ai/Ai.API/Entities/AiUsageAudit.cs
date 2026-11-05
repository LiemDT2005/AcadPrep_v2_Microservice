namespace Ai.API.Entities;

public class AiUsageAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AdminId { get; set; }
    public required string Action { get; set; }
    public Guid? UserId { get; set; }
    public string? PlanCode { get; set; }
    public long? OldValue { get; set; }
    public long? NewValue { get; set; }
    public DateTime CreatedAt { get; set; }
}
