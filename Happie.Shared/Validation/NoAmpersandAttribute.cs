using System.ComponentModel.DataAnnotations;

namespace Happie.Shared.Validation;

/// <summary>Validates that a string property does not contain the ampersand (&amp;) character.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NoAmpersandAttribute : ValidationAttribute
{
    /// <inheritdoc/>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
            return ValidationResult.Success;

        if (value is string text && !text.Contains('&'))
            return ValidationResult.Success;

        var memberName = validationContext.MemberName ?? validationContext.DisplayName;
        return new ValidationResult(ErrorMessage ?? $"{memberName} must not contain the '&' character.");
    }
}
