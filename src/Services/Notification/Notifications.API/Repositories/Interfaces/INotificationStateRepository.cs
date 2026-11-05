namespace Notifications.API.Repositories.Interfaces;

public interface INotificationStateRepository
{
    Task<bool> DeleteAsync(Guid notificationId, Guid userId, DateTime deletedAt, CancellationToken ct = default);

    Task<bool> MarkReadAsync(Guid notificationId, Guid userId, DateTime readAt, CancellationToken ct = default);
}
