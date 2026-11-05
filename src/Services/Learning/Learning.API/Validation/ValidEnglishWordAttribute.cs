using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Learning.API.Validation;

/// <summary>
/// Custom Validation Attribute kiem tra tu vung tieng Anh chuan TOEIC:
/// Chi cho phep chu cai Latin (a-z, A-Z), khoang trang, dau gach ngang (-) hoac dau nhay don (').
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public partial class ValidEnglishWordAttribute : ValidationAttribute
{
    [GeneratedRegex(@"^[a-zA-Z\s\-']+$")]
    private static partial Regex EnglishWordRegex();

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
            return ValidationResult.Success;

        if (value is not string word || string.IsNullOrWhiteSpace(word))
            return new ValidationResult("Tu vung tieng Anh khong duoc de trong.");

        var trimmed = word.Trim();
        if (trimmed.Length > 200)
            return new ValidationResult("Tu vung khong duoc vuot qua 200 ky tu.");

        if (!EnglishWordRegex().IsMatch(trimmed))
        {
            return new ValidationResult(
                "Tu vung chi duoc chua chu cai tieng Anh (A-Z), khoang trang hoac dau gach noi (-).");
        }

        return ValidationResult.Success;
    }
}
