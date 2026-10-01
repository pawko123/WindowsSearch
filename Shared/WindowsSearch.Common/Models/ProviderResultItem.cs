using ProtoBuf;
using System.Xml.Serialization;

namespace WindowsSearch.Common.Models;

[ProtoContract]
public sealed class ProviderResultItem
{
    [ProtoMember(1)]
    [XmlElement("Title")]
    public string Title { get; set; } = string.Empty;

    [ProtoMember(2)]
    [XmlElement("Subtitle")]
    public string Subtitle { get; set; } = string.Empty;

    [ProtoMember(3)]
    [XmlElement("ActionPath")]
    public string ActionPath { get; set; } = string.Empty;

    [ProtoMember(4)]
    [XmlArray("ActionArgs")]
    [XmlArrayItem("Arg")]
    public List<string> ActionArgs { get; set; } = [];

    [ProtoMember(5)]
    [XmlElement("IconPath")]
    public string? IconPath { get; set; }
}
