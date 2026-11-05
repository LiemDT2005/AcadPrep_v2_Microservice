using System.ComponentModel.DataAnnotations;

namespace Billing.API.Validation;

[AttributeUsage(AttributeTargets.Property)]
public sealed class NotEmptyGuidAttribute : ValidationAttribute
{
    public NotEmptyGuidAttribute() : base("The {0} field must be a non-empty GUID.") { }

    // Null is left to [Required]; an explicit empty GUID is never a valid identifier.
    public override bool IsValid(object? value) => value is not Guid id || id != Guid.Empty;
}
