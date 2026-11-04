using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Learning.API.Validation;

/// <summary>
/// Custom Validation Attribute kiem tra quy tac nghiep vu cho Danh hieu (Achievement):
/// 1. ConditionType chi duoc thuoc tap hop hop le: ExamsCompleted, Score, Streak, VocabCount.
/// 2. Neu ConditionType == "Score", ConditionValue phai nam trong thang diem chuan ETS TOEIC (10 - 990) va la boi so cua 5.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public class ValidAchievementConditionAttribute : ValidationAttribute
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ExamsCompleted",
        "Score",
        "Streak",
        "VocabCount"
    };

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string conditionType || string.IsNullOrWhiteSpace(conditionType))
        {
            return new ValidationResult("Loai dieu kien (ConditionType) khong duoc de trong.");
        }

        if (!AllowedTypes.Contains(conditionType))
        {
            return new ValidationResult(
                $"ConditionType '{conditionType}' khong hop le. Chi chap nhan: {string.Join(", ", AllowedTypes)}.");
        }

        // Cross-property validation: check ConditionValue when ConditionType is Score
        var conditionValueProp = validationContext.ObjectType.GetProperty(
            "ConditionValue",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (conditionValueProp != null && validationContext.ObjectInstance != null)
        {
            var rawVal = conditionValueProp.GetValue(validationContext.ObjectInstance);
            if (rawVal is int conditionValue)
            {
                if (string.Equals(conditionType, "Score", StringComparison.OrdinalIgnoreCase))
                {
                    if (conditionValue < 10 || conditionValue > 990 || conditionValue % 5 != 0)
                    {
                        return new ValidationResult(
                            "Voi dieu kien 'Score' (Diem TOEIC chuan ETS), gia tri phai tu 10 den 990 va chia het cho 5.");
                    }
                }
                else if (conditionValue <= 0)
                {
                    return new ValidationResult("Gia tri dieu kien (ConditionValue) phai lon hon 0.");
                }
            }
        }

        return ValidationResult.Success;
    }
}
