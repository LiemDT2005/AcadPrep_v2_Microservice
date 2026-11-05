namespace Notifications.API.Entities;

public static class EmailDeliveryStatus
{
    public const string Pending = "Pending";
    public const string Sent = "Sent";
    public const string Failed = "Failed";
    // No address in the event, or SMTP not configured: only the in-app notification exists.
    public const string Skipped = "Skipped";
}

// Email state is kept apart from the in-app notification: a mail failure never removes or repeats the
// notification, and a redelivered event never re-sends a mail that was already sent.
public class EmailDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceEventId { get; set; }
    public Guid NotificationId { get; set; }
    public string? Recipient { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = EmailDeliveryStatus.Pending;
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? SentAt { get; set; }
}
