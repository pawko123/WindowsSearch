using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;

public sealed class PathExistsAttribute : ValidationAttribute
{
    public static IFileSystem FileSystem { get; set; } = new FileSystem();

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
            fullPath = FileSystem.Path.Combine(Environment.ExpandEnvironmentVariables(BasePath), pathOrName);
        }
        else
        {
            fullPath = Environment.ExpandEnvironmentVariables(fullPath);
        }

        if (!FileSystem.Directory.Exists(fullPath) && !FileSystem.File.Exists(fullPath))
        {
            return new ValidationResult(ErrorMessage ?? $"At given path '{fullPath}', directory was not found", new[] { validationContext.MemberName! });
        }

        return ValidationResult.Success;
    }
}
