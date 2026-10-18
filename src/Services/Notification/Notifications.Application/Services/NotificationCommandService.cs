using BuildingBlocks.Common.Results;
using Microsoft.EntityFrameworkCore;
using Notifications.Application.Interfaces;

namespace Notifications.Application.Services;

public class NotificationCommandService(INotificationsDbContext db, INotificationReadStateStore states, TimeProvider clock)
{
    public async Task<Result> DeleteAsync(Guid notificationId, Guid userId, IReadOnlyCollection<string> roles,
        CancellationToken ct = default)
    {
        if (notificationId == Guid.Empty || userId == Guid.Empty)
            return Result.Failure("Notification not found.");

        // DELETE remains idempotent for audience members even when their state is already deleted.
        var audience = NotificationReadQuery.AudienceFor(db, userId, roles).Where(item => item.Id == notificationId);
        if (!await audience.AnyAsync(ct)) return Result.Failure("Notification not found.");
        if (!await states.DeleteAsync(notificationId, userId, clock.GetUtcNow().UtcDateTime, ct))
            return Result.Failure("Notification not found.");
        return await audience.AnyAsync(ct) ? Result.Success() : Result.Failure("Notification not found.");
    }

    public async Task<Result> MarkReadAsync(Guid notificationId, Guid userId, IReadOnlyCollection<string> roles,
        CancellationToken ct = default)
    {
        if (notificationId == Guid.Empty || userId == Guid.Empty)
            return Result.Failure("Notification not found.");

        var visible = NotificationReadQuery.VisibleTo(db, userId, roles).Where(item => item.Id == notificationId);
        if (!await visible.AnyAsync(ct)) return Result.Failure("Notification not found.");

        if (!await states.MarkReadAsync(notificationId, userId, clock.GetUtcNow().UtcDateTime, ct))
            return Result.Failure("Notification not found.");

        // Recheck after writes/duplicate recovery, including a concurrent soft delete.
        return await visible.AnyAsync(ct) ? Result.Success() : Result.Failure("Notification not found.");
    }
}
