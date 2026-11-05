using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Storefront.Web.Validation;

// Custom FE/BE cross-field validation: the end date must not be before the start date.
[AttributeUsage(AttributeTargets.Property)]
public sealed class BillingDateRangeAttribute(string startProperty) : ValidationAttribute, IClientModelValidator
{
    public const string Message = "Đến ngày phải bằng hoặc sau Từ ngày.";

    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        var start = context.ObjectType.GetProperty(startProperty)?.GetValue(context.ObjectInstance) as DateOnly?;
        return value is DateOnly end && start is { } from && end < from
            ? new ValidationResult(Message, [context.MemberName!])
            : ValidationResult.Success;
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        context.Attributes.TryAdd("data-val", "true");
        context.Attributes.TryAdd("data-val-billingdaterange", Message);
        context.Attributes.TryAdd("data-val-billingdaterange-other", startProperty);
    }
}
