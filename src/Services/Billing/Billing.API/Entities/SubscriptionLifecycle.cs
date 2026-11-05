namespace Billing.API.Entities;

public static class ReminderKind
{
    public const string Expiring = "Expiring";
    public const string Expired = "Expired";
}

// One row per (subscription, kind, period end): re-running the job or a second instance never sends twice,
// and a renewal (new EndDate) gets its own reminders while the old period's reminder is no longer due.
public class SubscriptionReminderLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubscriptionId { get; set; }
    public required string Kind { get; set; }
    public DateTime EndDate { get; set; }
    public Guid EventId { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Transactional outbox: written with the business change, published later with retries.
public class OutboxMessage
{
    // Equals the integration event Id, so consumers can deduplicate on it.
    public Guid Id { get; set; }
    public required string Type { get; set; }
    public required string Payload { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}
