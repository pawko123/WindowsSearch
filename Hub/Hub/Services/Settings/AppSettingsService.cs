using System.IO;
using System.Text;
using WindowsSearch.Common.Models;
using WindowsSearch.Common.Serialization;
using WindowsSearch.Common.Logging;
using WindowsSearch.Common.Validation;
using Hub.Models.Settings;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

using System.IO.Abstractions;
namespace Hub.Services.Settings;

public sealed class AppSettingsService
{
    private const string SettingsFileName = "app_config.yaml";
    private readonly IDeserializer deserializer;
    private readonly ISerializer serializer;
    private readonly IFileSystem _fileSystem;

    public AppSettingsService(IFileSystem? fileSystem = null, string? settingsDirectory = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
        SettingsDirectory = settingsDirectory ?? AppContext.BaseDirectory;
        SettingsPath = _fileSystem.Path.Combine(SettingsDirectory, SettingsFileName);
        deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
    }

    public string SettingsDirectory { get; }
    public string SettingsPath { get; }

    public (AppSettings Settings, IReadOnlyList<string> Errors, bool WasCreated) Load()
    {
        _fileSystem.Directory.CreateDirectory(SettingsDirectory);

        if (!_fileSystem.File.Exists(SettingsPath))
        {
            var defaults = new AppSettings();
            Save(defaults);
            return (defaults, [], true);
        }

        try
        {
            var yaml = _fileSystem.File.ReadAllText(SettingsPath, Encoding.UTF8);
            var settings = deserializer.Deserialize<AppSettings>(yaml) ?? new AppSettings();
            var validationErrors = SettingsValidationHelper.Validate(settings);
            return (settings, validationErrors, false);
        }
        catch (Exception ex)
        {
            var defaults = new AppSettings();
            return (defaults, ["Failed to read settings file.", ex.Message], false);
        }
    }

    public IReadOnlyList<string> Save(AppSettings settings)
    {
        var validationErrors = SettingsValidationHelper.Validate(settings);
        if (validationErrors.Count > 0)
        {
            return validationErrors;
        }

        _fileSystem.Directory.CreateDirectory(SettingsDirectory);

        AppLogger.Info($"[AppSettingsService] Saving Hub settings. Transport: {settings.Provider.ProviderTransportKind}, Serialization: {settings.Provider.ProviderSerialization}, SearchLimit: {settings.Search.SearchLimit}, ProviderTimeout: {settings.Provider.ProviderTimeoutSeconds}");

        var yaml = serializer.Serialize(settings);
        _fileSystem.File.WriteAllText(SettingsPath, yaml, Encoding.UTF8);
        return [];
    }
}
