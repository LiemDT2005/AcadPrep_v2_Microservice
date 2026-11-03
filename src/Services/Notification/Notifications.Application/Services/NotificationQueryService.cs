using BuildingBlocks.Common.Results;
using Microsoft.EntityFrameworkCore;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;

namespace Notifications.Application.Services;

public class NotificationQueryService(INotificationsDbContext db)
{
    public async Task<Result<NotificationPageDto>> GetAsync(Guid userId, IReadOnlyCollection<string> roles,
        int pageIndex, int pageSize, bool unreadOnly, CancellationToken ct = default)
    {
        if (userId == Guid.Empty) return Result.Failure<NotificationPageDto>("A valid user identifier is required.");
        if (pageIndex < 1 || pageSize is < 1 or > 100)
            return Result.Failure<NotificationPageDto>("Pagination parameters are invalid.");

        var query = NotificationReadQuery.VisibleTo(db, userId, roles);
        if (unreadOnly) query = query.Where(item => !item.IsRead);
        var totalCount = await query.CountAsync(ct);
        // Count first so a very large pageIndex stays safe without overflowing Skip's int argument.
        var offset = ((long)pageIndex - 1) * pageSize;
        var items = offset >= totalCount ? [] : await NotificationReadQuery.Page(query, (int)offset, pageSize).ToListAsync(ct);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        return Result.Success(new NotificationPageDto(items, pageIndex, pageSize, totalCount, totalPages));
    }

    public async Task<Result<UnreadNotificationCountDto>> GetUnreadCountAsync(Guid userId,
        IReadOnlyCollection<string> roles, CancellationToken ct = default)
    {
        if (userId == Guid.Empty) return Result.Failure<UnreadNotificationCountDto>("A valid user identifier is required.");
        var count = await NotificationReadQuery.VisibleTo(db, userId, roles).CountAsync(item => !item.IsRead, ct);
        return Result.Success(new UnreadNotificationCountDto(count));
    }
}
