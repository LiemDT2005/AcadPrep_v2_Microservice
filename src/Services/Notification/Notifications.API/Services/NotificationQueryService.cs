using BuildingBlocks.Common.Results;
using Notifications.API.DTOs;
using Notifications.API.Repositories.Interfaces;
using Notifications.API.Services.Interfaces;

namespace Notifications.API.Services;

public class NotificationQueryService(INotificationRepository notifications) : INotificationQueryService
{
    public async Task<Result<NotificationPageDto>> GetAsync(Guid userId, IReadOnlyCollection<string> roles,
        int pageIndex, int pageSize, bool unreadOnly, CancellationToken ct = default)
    {
        if (userId == Guid.Empty) return Result.Failure<NotificationPageDto>("A valid user identifier is required.");
        if (pageIndex < 1 || pageSize is < 1 or > 100)
            return Result.Failure<NotificationPageDto>("Pagination parameters are invalid.");

        var offset = ((long)pageIndex - 1) * pageSize;
        var (items, totalCount) = await notifications.GetVisiblePageAsync(userId, roles, unreadOnly, offset, pageSize, ct);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        return Result.Success(new NotificationPageDto(items, pageIndex, pageSize, totalCount, totalPages));
    }

    public async Task<Result<UnreadNotificationCountDto>> GetUnreadCountAsync(Guid userId,
        IReadOnlyCollection<string> roles, CancellationToken ct = default)
    {
        if (userId == Guid.Empty) return Result.Failure<UnreadNotificationCountDto>("A valid user identifier is required.");
        var count = await notifications.CountUnreadAsync(userId, roles, ct);
        return Result.Success(new UnreadNotificationCountDto(count));
    }
}
