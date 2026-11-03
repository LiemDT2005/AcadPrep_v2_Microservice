using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Notifications.Application.Interfaces;
using Notifications.Domain.Entities;

namespace Notifications.Infrastructure.Persistence;

public class NotificationReadStateStore(NotificationsDbContext db) : INotificationReadStateStore
{
    public async Task<bool> DeleteAsync(Guid notificationId, Guid userId, DateTime deletedAt, CancellationToken ct = default)
    {
        await DeleteExistingStateAsync(notificationId, userId, deletedAt, ct);
        var existing = await db.UserNotificationStates.AsNoTracking()
            .SingleOrDefaultAsync(state => state.NotificationId == notificationId && state.UserId == userId, ct);
        if (existing is not null)
        {
            if (existing.IsDeleted) return true;
            // A reader can insert after our first UPDATE but before this SELECT.
            await DeleteExistingStateAsync(notificationId, userId, deletedAt, ct);
            return await db.UserNotificationStates.AsNoTracking().AnyAsync(state =>
                state.NotificationId == notificationId && state.UserId == userId && state.IsDeleted, ct);
        }

        var inserted = new UserNotificationState
        {
            NotificationId = notificationId, UserId = userId, IsDeleted = true, DeletedAt = deletedAt,
            IsRead = false, ReadAt = null
        };
        try
        {
            await InsertStateAsync(inserted, ct);
        }
        catch (DbUpdateException exception) when (IsDuplicateStateConflict(exception, inserted))
        {
            db.Entry(inserted).State = EntityState.Detached;
            // A concurrent reader may have inserted first; preserve its read fields while deleting.
            await DeleteExistingStateAsync(notificationId, userId, deletedAt, ct);
        }

        return await db.UserNotificationStates.AsNoTracking().AnyAsync(state =>
            state.NotificationId == notificationId && state.UserId == userId && state.IsDeleted, ct);
    }

    protected virtual Task<int> DeleteExistingStateAsync(Guid notificationId, Guid userId, DateTime deletedAt,
        CancellationToken ct) => db.UserNotificationStates
        .Where(state => state.NotificationId == notificationId && state.UserId == userId && !state.IsDeleted)
        .ExecuteUpdateAsync(setters => setters.SetProperty(state => state.IsDeleted, true)
            .SetProperty(state => state.DeletedAt, state => state.DeletedAt ?? deletedAt), ct);

    public async Task<bool> MarkReadAsync(Guid notificationId, Guid userId, DateTime readAt, CancellationToken ct = default)
    {
        await MarkExistingStateAsync(notificationId, userId, readAt, ct);
        var existing = await db.UserNotificationStates.AsNoTracking()
            .SingleOrDefaultAsync(state => state.NotificationId == notificationId && state.UserId == userId, ct);
        if (existing is not null) return existing.IsRead && !existing.IsDeleted;

        var inserted = new UserNotificationState
        {
            NotificationId = notificationId, UserId = userId, IsRead = true, ReadAt = readAt,
            IsDeleted = false, DeletedAt = null
        };
        try
        {
            await InsertStateAsync(inserted, ct);
        }
        catch (DbUpdateException exception) when (IsDuplicateStateConflict(exception, inserted))
        {
            // Only this failed insert is detached. Other database failures propagate to error handling.
            db.Entry(inserted).State = EntityState.Detached;
            await MarkExistingStateAsync(notificationId, userId, readAt, ct);
        }

        // Always read fresh state; a duplicate key alone does not prove success or visibility.
        return await db.UserNotificationStates.AsNoTracking().AnyAsync(state =>
            state.NotificationId == notificationId && state.UserId == userId && state.IsRead && !state.IsDeleted, ct);
    }

    protected virtual Task<int> MarkExistingStateAsync(Guid notificationId, Guid userId, DateTime readAt, CancellationToken ct) =>
        db.UserNotificationStates
            .Where(state => state.NotificationId == notificationId && state.UserId == userId && !state.IsDeleted && !state.IsRead)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(state => state.IsRead, true)
                .SetProperty(state => state.ReadAt, state => state.ReadAt ?? readAt), ct);

    protected virtual async Task InsertStateAsync(UserNotificationState state, CancellationToken ct)
    {
        db.UserNotificationStates.Add(state);
        await db.SaveChangesAsync(ct);
    }

    protected virtual bool IsDuplicateStateConflict(DbUpdateException exception, UserNotificationState inserted) =>
        exception.Entries.Count == 1 && ReferenceEquals(exception.Entries[0].Entity, inserted) &&
        exception.Entries[0].State == EntityState.Added &&
        exception.InnerException is SqlException sqlException && sqlException.Errors.Cast<SqlError>().Any(error =>
            IsExpectedSqlServerDuplicate(error.Number, error.Message));

    internal static bool IsExpectedSqlServerDuplicate(int number, string message) =>
        number is 2601 or 2627 && message.Contains("'PK_UserNotificationStates'", StringComparison.Ordinal);
}
