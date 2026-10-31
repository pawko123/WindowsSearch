using ProtoBuf;
using System.Xml.Serialization;

namespace WindowsSearch.Common.Models;

[ProtoContract]
[XmlRoot("ProviderCommand")]
public sealed class ProviderCommand
{
    [ProtoMember(1)]
    [XmlElement("Action")]
    public string Action { get; set; } = string.Empty;
}
