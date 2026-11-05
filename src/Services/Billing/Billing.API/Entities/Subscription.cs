namespace Billing.API.Entities;

public static class SubscriptionStatus
{
    public const string Active = "Active";
    public const string Expired = "Expired";
}

public class Subscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    // Snapshot for renewal reminder emails: from the order's email claim or Identity at grant time.
    public string? UserEmail { get; set; }
    public Guid PlanId { get; set; }
    public Plan Plan { get; set; } = null!;
    // A persisted Active row is effective only while EndDate is in the future (checked on read).
    public string Status { get; set; } = SubscriptionStatus.Active;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public Guid? SourceOrderId { get; set; }
    public Guid? GrantedBy { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    // Optimistic concurrency token, regenerated on every write; portable across SQL Server and test SQLite.
    public Guid Version { get; set; } = Guid.NewGuid();
}
