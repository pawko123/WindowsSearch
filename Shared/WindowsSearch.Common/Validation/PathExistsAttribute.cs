using System.ComponentModel.DataAnnotations;
namespace WindowsSearch.Common.Validation;

public sealed class PathExistsAttribute : ValidationAttribute
{
    public string? BasePath { get; set; }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string pathOrName || string.IsNullOrWhiteSpace(pathOrName))
        {
            return new ValidationResult("Path is required.", new[] { validationContext.MemberName! });
        }

        string fullPath = pathOrName;
        
        if (!string.IsNullOrWhiteSpace(BasePath))
        {
            fullPath = Path.Combine(Environment.ExpandEnvironmentVariables(BasePath), pathOrName);
        }
        else
        {
            fullPath = Environment.ExpandEnvironmentVariables(fullPath);
        }

        if (!Path.Exists(fullPath))
        {
            return new ValidationResult(ErrorMessage ?? $"At given path '{fullPath}', directory was not found", new[] { validationContext.MemberName! });
        }

        return ValidationResult.Success;
    }
}
