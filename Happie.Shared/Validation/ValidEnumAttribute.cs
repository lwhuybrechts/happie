using System.ComponentModel.DataAnnotations;

namespace Happie.Shared.Validation;

/// <summary>Validates that an enum property holds a value that is defined in the enum type.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ValidEnumAttribute : ValidationAttribute
{
    /// <inheritdoc/>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not null && Enum.IsDefined(value.GetType(), value))
            return ValidationResult.Success;

        var memberName = validationContext.MemberName ?? validationContext.DisplayName;
        return new ValidationResult(ErrorMessage ?? $"Invalid value for {memberName}.");
    }
}
