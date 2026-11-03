using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Storefront.Web.Models;

namespace Storefront.Web.Validation;

[AttributeUsage(AttributeTargets.Property)]
public sealed class NotificationAudienceAttribute : ValidationAttribute, IClientModelValidator
{
    public const string Message = "Chọn User: nhập GUID hợp lệ, khác rỗng. Đối tượng khác: để trống ID người nhận.";

    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (context.ObjectInstance is not NotificationInput input) return new ValidationResult(Message);
        var target = value as string;
        var valid = input.Audience == "User"
            ? Guid.TryParse(target, out var id) && id != Guid.Empty
            : string.IsNullOrEmpty(target);
        return valid ? ValidationResult.Success : new ValidationResult(Message, [nameof(input.TargetUserId)]);
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        context.Attributes.TryAdd("data-val", "true");
        context.Attributes.TryAdd("data-val-notificationaudience", Message);
        context.Attributes.TryAdd("data-val-notificationaudience-other", "Audience");
    }
}
