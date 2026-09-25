using System.ComponentModel.DataAnnotations;

namespace WindowsSearch.Common.Validation;

/// <summary>
/// Single place that runs DataAnnotations validation over a settings object - used by both
/// the provider host (fail fast at process startup) and Hub (before writing settings.yaml).
/// </summary>
public static class SettingsValidationHelper
{
    public static IReadOnlyList<string> Validate(object settings)
    {
        var context = new ValidationContext(settings);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(settings, context, results, validateAllProperties: true);
        return results.Select(r => r.ErrorMessage ?? "Invalid setting.").ToList();
    }
}
