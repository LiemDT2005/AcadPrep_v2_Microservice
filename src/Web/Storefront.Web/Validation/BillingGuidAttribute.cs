using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Storefront.Web.Validation;

// Custom FE/BE validation: a non-empty GUID. Null/empty is left to [Required].
[AttributeUsage(AttributeTargets.Property)]
public sealed class BillingGuidAttribute : ValidationAttribute, IClientModelValidator
{
    public const string Message = "ID người dùng phải là GUID hợp lệ, khác rỗng.";

    public BillingGuidAttribute() : base(Message) { }

    public override bool IsValid(object? value) => value is not string text || string.IsNullOrEmpty(text) ||
        Guid.TryParse(text.Trim(), out var id) && id != Guid.Empty;

    public void AddValidation(ClientModelValidationContext context)
    {
        context.Attributes.TryAdd("data-val", "true");
        context.Attributes.TryAdd("data-val-billingguid", Message);
    }
}
