using YamlDotNet.Serialization;

namespace Hub.Models.Settings;

internal sealed class SettingsDocument
{
    [YamlMember(Alias = "search")]
    public SearchSettingsDocument Search { get; set; } = new();

    [YamlMember(Alias = "provider")]
    public ProviderSettingsDocument Provider { get; set; } = new();
}