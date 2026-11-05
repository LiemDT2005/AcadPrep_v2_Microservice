using Microsoft.EntityFrameworkCore;
using Notifications.API.Data;
using Notifications.API.DTOs;
using Notifications.API.Entities;
using Notifications.API.Repositories.Interfaces;

namespace Notifications.API.Repositories;

public class NotificationRepository(INotificationsDbContext db) : INotificationRepository
{
    public Task<bool> IsInAudienceAsync(Guid notificationId, Guid userId, IReadOnlyCollection<string> roles,
        CancellationToken ct) =>
        NotificationReadQuery.AudienceFor(db, userId, roles).AnyAsync(item => item.Id == notificationId, ct);

    public Task<bool> IsVisibleAsync(Guid notificationId, Guid userId, IReadOnlyCollection<string> roles,
        CancellationToken ct) =>
        NotificationReadQuery.VisibleTo(db, userId, roles).AnyAsync(item => item.Id == notificationId, ct);

    public async Task<(IReadOnlyList<NotificationListItemDto> Items, int TotalCount)> GetVisiblePageAsync(Guid userId,
        IReadOnlyCollection<string> roles, bool unreadOnly, long offset, int pageSize, CancellationToken ct)
    {
        var query = NotificationReadQuery.VisibleTo(db, userId, roles);
        if (unreadOnly) query = query.Where(item => !item.IsRead);
        var totalCount = await query.CountAsync(ct);
        // Count first so a very large offset stays safe without overflowing Skip's int argument.
        IReadOnlyList<NotificationListItemDto> items = offset >= totalCount
            ? []
            : await NotificationReadQuery.Page(query, (int)offset, pageSize).ToListAsync(ct);
        return (items, totalCount);
    }

    public Task<int> CountUnreadAsync(Guid userId, IReadOnlyCollection<string> roles, CancellationToken ct) =>
        NotificationReadQuery.VisibleTo(db, userId, roles).CountAsync(item => !item.IsRead, ct);

    public async Task<IReadOnlyList<Guid>> GetUnreadIdsAsync(Guid userId, IReadOnlyCollection<string> roles, CancellationToken ct) =>
        await NotificationReadQuery.VisibleTo(db, userId, roles).Where(item => !item.IsRead)
            .Select(item => item.Id).ToListAsync(ct);

    public async Task AddAsync(Notification notification, CancellationToken ct = default)
    {
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(ct);
    }

    public Task<Guid?> FindIdBySourceEventAsync(Guid eventId, CancellationToken ct) =>
        db.Notifications.AsNoTracking().Where(item => item.SourceEventId == eventId)
            .Select(item => (Guid?)item.Id).FirstOrDefaultAsync(ct);

    public async Task<bool> TryAddAsync(Notification notification, CancellationToken ct)
    {
        db.Notifications.Add(notification);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }
}
