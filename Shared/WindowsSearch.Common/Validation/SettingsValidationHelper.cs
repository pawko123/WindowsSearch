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
        var results = ValidateDetailed(settings);
        return results.Select(r => r.ErrorMessage ?? "Invalid setting.").ToList();
    }

    public static IReadOnlyList<ValidationResult> ValidateDetailed(object settings)
    {
        var results = new List<ValidationResult>();
        ValidateRecursive(settings, results, new HashSet<object>());
        return results;
    }

    private static void ValidateRecursive(object? obj, List<ValidationResult> results, HashSet<object> visited)
    {
        if (obj == null || !visited.Add(obj)) return; // Prevent infinite loops

        var context = new ValidationContext(obj);
        var localResults = new List<ValidationResult>();
        Validator.TryValidateObject(obj, context, localResults, validateAllProperties: true);
        results.AddRange(localResults);

        // Recurse into complex nested objects
        var properties = obj.GetType().GetProperties();
        foreach (var property in properties)
        {
            if (property.PropertyType.IsClass && 
                property.PropertyType != typeof(string) && 
                !typeof(System.Collections.IEnumerable).IsAssignableFrom(property.PropertyType))
            {
                var childValue = property.GetValue(obj);
                ValidateRecursive(childValue, results, visited);
            }
        }
    }
}
