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

        switch (settings.ProviderTransportKind)
        {
            case ProviderTransportKind.NamedPipe:
                if (string.IsNullOrWhiteSpace(settings.ProviderEndpoint))
                {
                    errors.Add("Named pipe endpoint cannot be empty.");
                }
                else if (!settings.ProviderEndpoint.StartsWith(@"\\.\pipe\", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("Named pipe endpoints must start with \\.\\pipe\\.");
                }

                break;
            case ProviderTransportKind.Http:
            case ProviderTransportKind.Grpc:
                if (!Uri.TryCreate(settings.ProviderEndpoint, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    errors.Add($"{settings.ProviderTransportKind} endpoints must be an absolute HTTP or HTTPS URI.");
                }

                break;
        }

        return errors;
    }
}