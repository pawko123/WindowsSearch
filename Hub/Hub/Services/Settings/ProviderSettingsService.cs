using System.IO;
using System.Text;
using Hub.Models.Settings;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Hub.Services.Settings;

public sealed class ProviderSettingsService
{
    private readonly IDeserializer deserializer;
    private readonly ISerializer serializer;

    public ProviderSettingsService()
    {
        deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
        serializer = new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
    }

    public IReadOnlyList<ProviderSettingsModel> LoadAll()
    {
        var providersRoot = Path.Combine(AppContext.BaseDirectory, "Providers");
        if (!Directory.Exists(providersRoot))
        {
            return [];
        }

        var models = new List<ProviderSettingsModel>();
        foreach (var settingsPath in Directory.EnumerateFiles(providersRoot, "settings.yaml", SearchOption.AllDirectories))
        {
            var providerName = new DirectoryInfo(Path.GetDirectoryName(settingsPath)!).Name;
            models.Add(new ProviderSettingsModel
            {
                ProviderName = providerName,
                SettingsPath = settingsPath,
                Document = Load(settingsPath)
            });
        }

        return models.OrderBy(model => model.ProviderName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public ProviderFileSettingsDocument Load(string settingsPath)
    {
        if (!File.Exists(settingsPath))
        {
            return new ProviderFileSettingsDocument();
        }

        try
        {
            var yaml = File.ReadAllText(settingsPath, Encoding.UTF8);
            return deserializer.Deserialize<ProviderFileSettingsDocument>(yaml) ?? new ProviderFileSettingsDocument();
        }
        catch
        {
            return new ProviderFileSettingsDocument();
        }
    }

    public IReadOnlyList<string> Save(ProviderSettingsModel model)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(model.SettingsPath))
        {
            errors.Add("Provider settings path is missing.");
            return errors;
        }

        var yaml = serializer.Serialize(model.Document);
        File.WriteAllText(model.SettingsPath, yaml, Encoding.UTF8);
        return errors;
    }
}
