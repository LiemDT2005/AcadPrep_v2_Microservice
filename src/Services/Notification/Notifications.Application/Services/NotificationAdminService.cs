using System.ComponentModel.DataAnnotations;
using BuildingBlocks.Common.Results;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using Notifications.Domain.Entities;
using Notifications.Domain.Enums;

namespace Notifications.Application.Services;

public class NotificationAdminService(INotificationWriter writer, TimeProvider clock)
{
    public async Task<Result<NotificationCreatedDto>> CreateAsync(CreateAdminNotificationRequest request,
        Guid adminId, CancellationToken ct = default)
    {
        if (adminId == Guid.Empty || !Validator.TryValidateObject(request, new ValidationContext(request), [], true))
            return Result.Failure<NotificationCreatedDto>("Invalid notification request.");

        var notification = new Notification
        {
            Id = Guid.NewGuid(), Title = request.Title!, Content = request.Content!,
            Type = NotificationType.System, Audience = Enum.Parse<NotificationAudience>(request.Audience!),
            TargetUserId = request.TargetUserId, CreatedBy = adminId, CreatedAt = clock.GetUtcNow().UtcDateTime,
            SourceEventId = null, DataJson = null
        };
        await writer.AddAsync(notification, ct);
        return Result.Success(new NotificationCreatedDto(notification.Id));
    }
}
