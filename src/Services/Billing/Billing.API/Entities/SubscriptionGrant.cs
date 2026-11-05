namespace Billing.API.Entities;

// Immutable audit of a manual Admin grant. A grant never creates an Order or revenue.
public class SubscriptionGrant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestId { get; set; }
    public Guid AdminId { get; set; }
    public Guid TargetUserId { get; set; }
    public Guid PlanId { get; set; }
    public Guid SubscriptionId { get; set; }
    public int DurationDays { get; set; }
    public string? Note { get; set; }
    public DateTime? PreviousEndDate { get; set; }
    public DateTime NewStartDate { get; set; }
    public DateTime NewEndDate { get; set; }
    public DateTime CreatedAt { get; set; }
}
