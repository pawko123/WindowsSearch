using ProtoBuf;
using System.Xml.Serialization;

namespace WindowsSearch.Common.Models;

[ProtoContract]
public sealed class ProviderResultCategory
{
    [ProtoMember(1)]
    [XmlElement("Name")]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(2)]
    [XmlElement("IconPath")]
    public string? IconPath { get; set; }

    [ProtoMember(3)]
    [XmlArray("Items")]
    [XmlArrayItem("Item")]
    public List<ProviderResultItem> Items { get; set; } = [];
}
