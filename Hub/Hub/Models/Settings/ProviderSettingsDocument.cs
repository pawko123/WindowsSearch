using YamlDotNet.Serialization;

namespace Hub.Models.Settings;

internal sealed class ProviderSettingsDocument
{
    [YamlMember(Alias = "transport")]
    public string? Transport { get; set; }

    [YamlMember(Alias = "endpoint")]
    public string? Endpoint { get; set; }

    [YamlMember(Alias = "timeout_seconds")]
    public int? TimeoutSeconds { get; set; }
}