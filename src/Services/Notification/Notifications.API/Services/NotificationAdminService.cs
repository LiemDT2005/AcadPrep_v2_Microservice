using System.ComponentModel.DataAnnotations;
using AutoMapper;
using BuildingBlocks.Common.Results;
using Notifications.API.DTOs;
using Notifications.API.Data;
using Notifications.API.Repositories.Interfaces;
using Notifications.API.Services.Interfaces;
using Notifications.API.Entities;
using Notifications.API.Enums;

namespace Notifications.API.Services;

public class NotificationAdminService(INotificationRepository notifications, TimeProvider clock, IMapper mapper,
    IIdentityUserClient identity) : INotificationAdminService
{
    public const string InvalidRequest = "Invalid notification request.";
    public const string TargetNotFound = "Target user not found.";
    public const string IdentityUnavailable = "Recipient verification is temporarily unavailable.";

    public async Task<Result<NotificationCreatedDto>> CreateAsync(CreateAdminNotificationRequest request,
        Guid adminId, CancellationToken ct = default)
    {
        if (adminId == Guid.Empty || !Validator.TryValidateObject(request, new ValidationContext(request), [], true))
            return Result.Failure<NotificationCreatedDto>(InvalidRequest);

        // A personal notification must target an existing account (any role, any status). Broadcasts are
        // resolved by role at read time and need no lookup. Fail closed when Identity cannot answer.
        if (request.Audience == nameof(NotificationAudience.User))
        {
            var lookup = await identity.FindUserAsync(request.TargetUserId!.Value, ct);
            if (lookup == IdentityLookupStatus.NotFound) return Result.Failure<NotificationCreatedDto>(TargetNotFound);
            if (lookup != IdentityLookupStatus.Found) return Result.Failure<NotificationCreatedDto>(IdentityUnavailable);
        }

        var notification = mapper.Map<Notification>(request);
        notification.Id = Guid.NewGuid();
        notification.Type = NotificationType.System;
        notification.CreatedBy = adminId;
        notification.CreatedAt = clock.GetUtcNow().UtcDateTime;
        notification.SourceEventId = null;
        notification.DataJson = null;
        await notifications.AddAsync(notification, ct);
        return Result.Success(mapper.Map<NotificationCreatedDto>(notification));
    }
}
