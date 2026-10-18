using System.ComponentModel.DataAnnotations;
using Notifications.Application.DTOs;

namespace Notifications.Application.Validation;

[AttributeUsage(AttributeTargets.Property)]
public sealed class ValidAudienceAttribute : ValidationAttribute
{
    public const string TargetRequired = "A non-empty targetUserId is required for audience User.";
    public const string TargetForbidden = "targetUserId must be null for broadcast audiences.";

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (validationContext.ObjectInstance is not CreateAdminNotificationRequest request)
            return new ValidationResult("Invalid notification validation context.");
        if (request.Audience == "User" && (request.TargetUserId is null || request.TargetUserId == Guid.Empty))
            return new ValidationResult(TargetRequired, [nameof(request.TargetUserId)]);
        if (request.Audience is "All" or "Learner" or "Moderator" && request.TargetUserId is not null)
            return new ValidationResult(TargetForbidden, [nameof(request.TargetUserId)]);
        return ValidationResult.Success;
    }
}
