using System.ComponentModel.DataAnnotations;

namespace WindowsSearch.Common.Validation;

public sealed class NamedPipeEndpointAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string str || string.IsNullOrWhiteSpace(str))
        {
            return new ValidationResult("Named pipe endpoint is required.", new[] { validationContext.MemberName! });
        }

        if (!str.StartsWith(@"\\.\pipe\", StringComparison.OrdinalIgnoreCase))
        {
            return new ValidationResult(@"Named pipe endpoints must start with \\.\pipe\.", new[] { validationContext.MemberName! });
        }

        return ValidationResult.Success;
    }
}
