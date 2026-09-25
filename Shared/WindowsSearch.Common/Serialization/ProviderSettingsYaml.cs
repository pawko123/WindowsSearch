using System.Text;
using YamlDotNet.Serialization;

namespace WindowsSearch.Common.Serialization;

/// <summary>
/// Single YAML load/save implementation for provider settings.yaml files, shared by the
/// provider host process and Hub so both sides read/write the exact same on-disk shape.
/// </summary>
public static class ProviderSettingsYaml
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly ISerializer Serializer = new SerializerBuilder().Build();

    public static T Load<T>(string path) where T : new()
    {
        if (!File.Exists(path))
        {
            return new T();
        }

        var yaml = File.ReadAllText(path, Encoding.UTF8);
        return Deserializer.Deserialize<T>(yaml) ?? new T();
    }

    public static object Load(string path, Type settingsType)
    {
        if (!File.Exists(path))
        {
            return Activator.CreateInstance(settingsType)!;
        }

        var yaml = File.ReadAllText(path, Encoding.UTF8);
        return Deserializer.Deserialize(yaml, settingsType) ?? Activator.CreateInstance(settingsType)!;
    }

    /// <summary>Deserializes a settings snapshot received over the wire, e.g. via ProviderSearchRequest.SettingsYaml.</summary>
    public static T? Parse<T>(string? yaml) where T : class
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return null;
        }

        try
        {
            return Deserializer.Deserialize<T>(yaml);
        }
        catch
        {
            return null;
        }
    }

    public static string Serialize(object settings) => Serializer.Serialize(settings);

    public static void Save(string path, object settings) =>
        File.WriteAllText(path, Serialize(settings), Encoding.UTF8);
}
