using Hub.Models.Providers;
using Hub.Models.Settings;

namespace Hub.Services.Settings;

public static class SettingsValidator
{
    public static IReadOnlyList<string> Validate(AppSettings settings)
    {
        var errors = new List<string>();

        if (settings.SearchLimit <= 0)
        {
            errors.Add("Search limit must be greater than zero.");
        }

        if (settings.ProviderTimeoutSeconds <= 0)
        {
            errors.Add("Provider timeout must be greater than zero.");
        }

        return errors;
    }
}