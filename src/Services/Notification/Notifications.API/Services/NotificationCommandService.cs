using BuildingBlocks.Common.Results;
using Notifications.API.Repositories.Interfaces;
using Notifications.API.Services.Interfaces;

namespace Notifications.API.Services;

public class NotificationCommandService(INotificationRepository notifications, INotificationStateRepository states,
    TimeProvider clock) : INotificationCommandService
{
    public async Task MarkAllReadAsync(Guid userId, IReadOnlyCollection<string> roles, CancellationToken ct)
    {
        var ids = await notifications.GetUnreadIdsAsync(userId, roles, ct);
        var readAt = clock.GetUtcNow().UtcDateTime;
        foreach (var id in ids)
            await states.MarkReadAsync(id, userId, readAt, ct);
    }

    public async Task<Result> DeleteAsync(Guid notificationId, Guid userId, IReadOnlyCollection<string> roles,
        CancellationToken ct = default)
    {
        if (notificationId == Guid.Empty || userId == Guid.Empty)
            return Result.Failure("Notification not found.");

        // DELETE remains idempotent for audience members even when their state is already deleted.
        if (!await notifications.IsInAudienceAsync(notificationId, userId, roles, ct)) return Result.Failure("Notification not found.");
        if (!await states.DeleteAsync(notificationId, userId, clock.GetUtcNow().UtcDateTime, ct))
            return Result.Failure("Notification not found.");
        return await notifications.IsInAudienceAsync(notificationId, userId, roles, ct)
            ? Result.Success() : Result.Failure("Notification not found.");
    }

    public async Task<Result> MarkReadAsync(Guid notificationId, Guid userId, IReadOnlyCollection<string> roles,
        CancellationToken ct = default)
    {
        if (notificationId == Guid.Empty || userId == Guid.Empty)
            return Result.Failure("Notification not found.");

        if (!await notifications.IsVisibleAsync(notificationId, userId, roles, ct)) return Result.Failure("Notification not found.");

        if (!await states.MarkReadAsync(notificationId, userId, clock.GetUtcNow().UtcDateTime, ct))
            return Result.Failure("Notification not found.");

        // Recheck after writes/duplicate recovery, including a concurrent soft delete.
        return await notifications.IsVisibleAsync(notificationId, userId, roles, ct)
            ? Result.Success() : Result.Failure("Notification not found.");
    }
}
