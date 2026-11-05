using Notifications.API.DTOs;
using Notifications.API.Entities;

namespace Notifications.API.Repositories.Interfaces;

// Notifications as seen by one user (audience by role/target, minus their soft-deleted ones) and the writes that
// create them. Results are materialized; no IQueryable leaves the repository.
public interface INotificationRepository
{
    // In the user's audience, whether or not they deleted it (keeps DELETE idempotent).
    Task<bool> IsInAudienceAsync(Guid notificationId, Guid userId, IReadOnlyCollection<string> roles, CancellationToken ct);
    // In the user's audience and not deleted by them.
    Task<bool> IsVisibleAsync(Guid notificationId, Guid userId, IReadOnlyCollection<string> roles, CancellationToken ct);
    // Newest first (CreatedAt desc, then Id). An offset past the end returns no items without querying them.
    Task<(IReadOnlyList<NotificationListItemDto> Items, int TotalCount)> GetVisiblePageAsync(Guid userId,
        IReadOnlyCollection<string> roles, bool unreadOnly, long offset, int pageSize, CancellationToken ct);
    Task<int> CountUnreadAsync(Guid userId, IReadOnlyCollection<string> roles, CancellationToken ct);
    Task<IReadOnlyList<Guid>> GetUnreadIdsAsync(Guid userId, IReadOnlyCollection<string> roles, CancellationToken ct);

    Task AddAsync(Notification notification, CancellationToken ct = default);
    // Event-sourced notifications (unique SourceEventId).
    Task<Guid?> FindIdBySourceEventAsync(Guid eventId, CancellationToken ct);
    // False when a concurrent redelivery inserted the same SourceEventId first (pending changes are discarded).
    Task<bool> TryAddAsync(Notification notification, CancellationToken ct);
}
