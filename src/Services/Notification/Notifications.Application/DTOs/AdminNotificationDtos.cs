using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Notifications.Application.Validation;

namespace Notifications.Application.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record CreateAdminNotificationRequest
{
    private string? title;

    [Required, StringLength(200, MinimumLength = 1)]
    public string? Title { get => title; init => title = value?.Trim(); }

    [Required, StringLength(2000, MinimumLength = 1)]
    public string? Content { get; init; }

    [Required, RegularExpression("^(User|All|Learner|Moderator)$")]
    public string? Audience { get; init; }

    [ValidAudience]
    public Guid? TargetUserId { get; init; }
}

public record NotificationCreatedDto(Guid Id);
