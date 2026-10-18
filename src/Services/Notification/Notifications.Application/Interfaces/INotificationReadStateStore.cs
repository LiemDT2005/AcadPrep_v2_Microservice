namespace Notifications.Application.Interfaces;

public interface INotificationReadStateStore
{
    Task<bool> DeleteAsync(Guid notificationId, Guid userId, DateTime deletedAt, CancellationToken ct = default);

    Task<bool> MarkReadAsync(Guid notificationId, Guid userId, DateTime readAt, CancellationToken ct = default);
}
