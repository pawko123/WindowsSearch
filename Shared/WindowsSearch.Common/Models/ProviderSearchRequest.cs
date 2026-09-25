using ProtoBuf;
using System.Xml.Serialization;

namespace WindowsSearch.Common.Models;

[ProtoContract]
[XmlRoot("ProviderSearchRequest")]
public sealed class ProviderSearchRequest
{
    [ProtoMember(1)]
    [XmlElement("Query")]
    public string Query { get; set; } = string.Empty;

    [ProtoMember(2)]
    [XmlElement("Limit")]
    public int Limit { get; set; }

    /// <summary>
    /// YAML snapshot of the provider's current typed settings, as last saved to its settings.yaml.
    /// Sent on every request so edits made in Hub take effect without restarting the provider process.
    /// </summary>
    [ProtoMember(3)]
    [XmlElement("SettingsYaml")]
    public string SettingsYaml { get; set; } = string.Empty;
}
