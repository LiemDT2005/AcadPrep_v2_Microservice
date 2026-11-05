using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Storefront.Web.Models;

namespace Storefront.Web.Validation;

// Spec custom validation (FE + BE): the selected plan must be one of the paid plans on sale, as loaded from the
// Billing API for this request. The Billing API validates again (404/422); this never trusts the client.
[AttributeUsage(AttributeTargets.Property)]
public sealed class ValidPlanForCheckoutAttribute : ValidationAttribute, IClientModelValidator
{
    public const string Message = "Vui lòng chọn một gói trả phí đang bán.";
    public const string AllowedItemKey = "Billing.CheckoutPlanIds";

    public ValidPlanForCheckoutAttribute() : base(Message) { }

    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is not Guid id || id == Guid.Empty) return new ValidationResult(Message, [context.MemberName!]);
        var allowed = (context.ObjectInstance as BillingCheckoutInput)?.AllowedPlanIds;
        return allowed is null || allowed.Contains(id) ? ValidationResult.Success : new ValidationResult(Message, [context.MemberName!]);
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        context.Attributes.TryAdd("data-val", "true");
        context.Attributes.TryAdd("data-val-validplan", Message);
        var allowed = context.ActionContext.HttpContext.Items[AllowedItemKey] as IEnumerable<Guid> ?? [];
        context.Attributes.TryAdd("data-val-validplan-allowed", string.Join(',', allowed));
    }
}
