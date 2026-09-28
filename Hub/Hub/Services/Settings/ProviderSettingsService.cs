using System.Collections.Concurrent;
using System.IO;
using System.IO.Abstractions;
using System.Runtime.Loader;
using Hub.Models.Settings;
using WindowsSearch.Common.Models;
using WindowsSearch.Common.Serialization;
using WindowsSearch.Common.Validation;

namespace Hub.Services.Settings;

/// <summary>
/// Discovers provider settings purely from what is on disk under Providers/&lt;name&gt;/ - a new
/// provider needs no change here. If a provider ships "&lt;name&gt;.Settings.dll" it is loaded via
/// reflection to get a strongly-typed, validated settings model; otherwise settings fall back to
/// the free-form GenericProviderSettings editor.
/// </summary>
public sealed class ProviderSettingsService
{
    private readonly IFileSystem _fileSystem;
    private static readonly ConcurrentDictionary<string, Type> SettingsTypeCache = new(StringComparer.OrdinalIgnoreCase);

    public ProviderSettingsService(IFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
    }

    public IReadOnlyList<ProviderSettingsModel> LoadAll()
    {
        var providersRoot = _fileSystem.Path.Combine(AppContext.BaseDirectory, "Providers");
        if (!_fileSystem.Directory.Exists(providersRoot))
        {
            return [];
        }

        var models = new List<ProviderSettingsModel>();
        foreach (var settingsPath in _fileSystem.Directory.EnumerateFiles(providersRoot, "settings.yaml", SearchOption.AllDirectories))
        {
            var providerDirectory = _fileSystem.Path.GetDirectoryName(settingsPath)!;
            var providerName = _fileSystem.DirectoryInfo.New(providerDirectory).Name;
            var settingsType = ResolveSettingsType(providerName);

            var yaml = _fileSystem.File.Exists(settingsPath) ? _fileSystem.File.ReadAllText(settingsPath) : null;
            var settingsObj = ProviderSettingsYaml.Parse(yaml, settingsType) ?? Activator.CreateInstance(settingsType)!;

            models.Add(new ProviderSettingsModel
            {
                ProviderName = providerName,
                SettingsPath = settingsPath,
                Settings = (ProviderSettingsBase)settingsObj
            });
        }

        return models.OrderBy(model => model.ProviderName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public IReadOnlyList<string> Save(ProviderSettingsModel model)
    {
        if (string.IsNullOrWhiteSpace(model.SettingsPath))
        {
            return ["Provider settings path is missing."];
        }

        var errors = SettingsValidationHelper.Validate(model.Settings);
        if (errors.Count > 0)
        {
            return errors;
        }

        var yaml = ProviderSettingsYaml.Serialize(model.Settings);
        _fileSystem.File.WriteAllText(model.SettingsPath, yaml);
        return [];
    }

    private Type ResolveSettingsType(string providerName)
    {
        return SettingsTypeCache.GetOrAdd(providerName, _ =>
        {
            var assemblyPath = _fileSystem.Path.Combine(AppContext.BaseDirectory, "bin", $"{providerName}.Settings.dll");
            if (!_fileSystem.File.Exists(assemblyPath))
            {
                return typeof(GenericProviderSettings);
            }

            try
            {
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
                var settingsType = assembly.GetTypes()
                    .FirstOrDefault(t => !t.IsAbstract && typeof(ProviderSettingsBase).IsAssignableFrom(t));

                return settingsType ?? typeof(GenericProviderSettings);
            }
            catch
            {
                return typeof(GenericProviderSettings);
            }
        });
    }
}
