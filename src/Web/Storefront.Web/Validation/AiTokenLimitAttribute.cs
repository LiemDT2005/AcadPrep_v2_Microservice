using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Storefront.Web.Validation;

// Custom FE/BE validation for UC-22.2 limits: a whole number of tokens between 0 and 100,000,000 (spec range).
[AttributeUsage(AttributeTargets.Property)]
public sealed class AiTokenLimitAttribute : ValidationAttribute, IClientModelValidator
{
    public const long Max = 100_000_000;
    public const string Message = "Hạn mức phải là số nguyên từ 0 đến 100.000.000 token.";

    public AiTokenLimitAttribute() : base(Message) { }

    public override bool IsValid(object? value) => value is null || value is long number && number is >= 0 and <= Max;

    public void AddValidation(ClientModelValidationContext context)
    {
        context.Attributes.TryAdd("data-val", "true");
        context.Attributes.TryAdd("data-val-aitokenlimit", Message);
        context.Attributes.TryAdd("data-val-aitokenlimit-max", Max.ToString());
    }
}
