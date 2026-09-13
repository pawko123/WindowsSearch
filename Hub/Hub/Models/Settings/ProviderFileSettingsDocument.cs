using YamlDotNet.Serialization;

namespace Hub.Models.Settings;

public sealed class ProviderFileSettingsDocument
{
    [YamlMember(Alias = "transport")]
    public string Transport { get; set; } = "NamedPipe";

    [YamlMember(Alias = "endpoint")]
    public string Endpoint { get; set; } = @"\\.\pipe\demo_provider";

    [YamlMember(Alias = "timeout_seconds")]
    public int TimeoutSeconds { get; set; } = 5;

    [YamlMember(Alias = "settings")]
    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}