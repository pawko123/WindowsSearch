using ProtoBuf;
using System.Text.Json.Serialization;
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

    [ProtoMember(3)]
    [XmlIgnore]
    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [XmlArray("Settings")]
    [XmlArrayItem("Setting")]
    [JsonIgnore]
    public List<StringStringPair> SettingsXml
    {
        get => Settings.Select(kvp => new StringStringPair { Key = kvp.Key, Value = kvp.Value }).ToList();
        set
        {
            Settings.Clear();
            if (value != null)
            {
                foreach (var pair in value)
                {
                    Settings[pair.Key] = pair.Value;
                }
            }
        }
    }
}
