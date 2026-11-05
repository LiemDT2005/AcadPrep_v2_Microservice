namespace Billing.API.Entities;

public class PlanLimit
{
    public Guid PlanId { get; set; }
    public required string Feature { get; set; }
    // A persisted row with null explicitly means unlimited. A missing row is a configuration error.
    public int? LimitValue { get; set; }
    public required string Period { get; set; }
    public Plan Plan { get; set; } = null!;
}
