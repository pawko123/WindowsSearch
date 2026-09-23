using System.IO;
using System.Globalization;
using System.Text;
using Hub.Models.Providers;
using Hub.Models.Settings;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using CommonLogging;

namespace Hub.Services.Settings;

public sealed class AppSettingsService
{
    private const string SettingsFileName = "app_config.yaml";
    private readonly IDeserializer deserializer;
    private readonly ISerializer serializer;

    public AppSettingsService(string? settingsDirectory = null)
    {
        SettingsDirectory = settingsDirectory ?? AppContext.BaseDirectory;
        SettingsPath = Path.Combine(SettingsDirectory, SettingsFileName);
        deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
        serializer = new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
    }

    public string SettingsDirectory { get; }

    public string SettingsPath { get; }

    public (AppSettings Settings, IReadOnlyList<string> Errors, bool WasCreated) Load()
    {
        Directory.CreateDirectory(SettingsDirectory);

        if (!File.Exists(SettingsPath))
        {
            var defaults = new AppSettings();
            Save(defaults);
            return (defaults, [], true);
        }

        try
        {
            var yaml = File.ReadAllText(SettingsPath, Encoding.UTF8);
            var document = deserializer.Deserialize<SettingsDocument>(yaml) ?? new SettingsDocument();
            var result = Convert(document);
            return result;
        }
        catch (Exception ex)
        {
            var defaults = new AppSettings();
            return (defaults, ["Failed to read settings file.", ex.Message], false);
        }
    }

    public IReadOnlyList<string> Save(AppSettings settings)
    {
        var validationErrors = SettingsValidator.Validate(settings);
        if (validationErrors.Count > 0)
        {
            return validationErrors;
        }

        Directory.CreateDirectory(SettingsDirectory);

        AppLogger.Info($"[AppSettingsService] Saving Hub settings. Transport: {settings.ProviderTransportKind}, Serialization: {settings.ProviderSerialization}, SearchLimit: {settings.SearchLimit}, ProviderTimeout: {settings.ProviderTimeoutSeconds}");

        var document = new SettingsDocument
        {
            Search = new SearchSettingsDocument
            {
                ImageResolver = settings.ImageResolverKind.ToString(),
                Limit = settings.SearchLimit,
            },
            Provider = new ProviderSettingsDocument
            {
                Transport = settings.ProviderTransportKind.ToString(),
                Serialization = settings.ProviderSerialization,
                TimeoutSeconds = settings.ProviderTimeoutSeconds,
            }
        };

        var yaml = serializer.Serialize(document);
        File.WriteAllText(SettingsPath, yaml, Encoding.UTF8);
        return [];
    }

    private static (AppSettings Settings, IReadOnlyList<string> Errors, bool WasCreated) Convert(SettingsDocument document)
    {
        var settings = new AppSettings();
        var errors = new List<string>();

        if (!string.IsNullOrWhiteSpace(document.Search.ImageResolver))
        {
            if (Enum.TryParse<ImageResolverKind>(document.Search.ImageResolver, true, out var resolverKind))
            {
                settings.ImageResolverKind = resolverKind;
            }
            else
            {
                errors.Add($"Unknown image resolver: '{document.Search.ImageResolver}'.");
            }
        }

        if (document.Search.Limit is { } limit)
        {
            settings.SearchLimit = limit;
        }

        if (!string.IsNullOrWhiteSpace(document.Provider.Transport))
        {
            if (Enum.TryParse<ProviderTransportKind>(document.Provider.Transport, true, out var transportKind))
            {
                settings.ProviderTransportKind = transportKind;
            }
            else
            {
                errors.Add($"Unknown provider transport: '{document.Provider.Transport}'.");
            }
        }

        if (!string.IsNullOrWhiteSpace(document.Provider.Serialization))
        {
            settings.ProviderSerialization = document.Provider.Serialization;
        }

        if (document.Provider.TimeoutSeconds is { } timeoutSeconds)
        {
            settings.ProviderTimeoutSeconds = timeoutSeconds;
        }

        var validationErrors = SettingsValidator.Validate(settings);
        foreach (var validationError in validationErrors)
        {
            errors.Add(validationError);
        }

        return (settings, errors, false);
    }
}