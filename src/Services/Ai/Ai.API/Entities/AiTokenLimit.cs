namespace Ai.API.Entities;

public class AiTokenLimit
{
    public required string PlanCode { get; set; }
    // Tokens per rolling 24h window.
    public long TokenLimit { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
}
