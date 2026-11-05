namespace Ai.API.Entities;

// Rolling 24h quota window (Huy, 2026-11-05): opened by the first successfully answered chat, ends 24h later.
// Locked = the input of a request used up the remaining budget; chat stays closed until WindowEnd.
public class AiUsageWindow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public DateTime WindowStart { get; set; }
    public DateTime WindowEnd { get; set; }
    public long TokensUsed { get; set; }
    public bool Locked { get; set; }
    public string PlanCode { get; set; } = "FREE";
    public DateTime UpdatedAt { get; set; }
}
