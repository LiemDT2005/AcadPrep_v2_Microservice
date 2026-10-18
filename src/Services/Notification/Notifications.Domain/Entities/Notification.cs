using Notifications.Domain.Enums;

namespace Notifications.Domain.Entities;

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public NotificationType Type { get; set; } = NotificationType.System;
    public NotificationAudience Audience { get; set; }
    public Guid? TargetUserId { get; set; }
    public string? DataJson { get; set; }
    public Guid? SourceEventId { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<UserNotificationState> UserStates { get; set; } = [];
}
