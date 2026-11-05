using BuildingBlocks.Common.Results;
using Notifications.API.DTOs;

namespace Notifications.API.Services.Interfaces;

public interface INotificationQueryService
{
    Task<Result<NotificationPageDto>> GetAsync(Guid userId, IReadOnlyCollection<string> roles,
        int pageIndex, int pageSize, bool unreadOnly, CancellationToken ct = default);
    Task<Result<UnreadNotificationCountDto>> GetUnreadCountAsync(Guid userId,
        IReadOnlyCollection<string> roles, CancellationToken ct = default);
}
