using BuildingBlocks.Common.Results;
using Notifications.API.DTOs;

namespace Notifications.API.Services.Interfaces;

public interface INotificationAdminService
{
    Task<Result<NotificationCreatedDto>> CreateAsync(CreateAdminNotificationRequest request, Guid adminId, CancellationToken ct = default);
}
