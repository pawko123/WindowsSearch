using ProtoBuf;
using System.Xml.Serialization;

namespace WindowsSearch.Common.Models;

[ProtoContract]
[XmlRoot("ProviderSearchResponse")]
public sealed class ProviderSearchResponse
{
    [ProtoMember(1)]
    [XmlArray("Categories")]
    [XmlArrayItem("Category")]
    public List<ProviderResultCategory> Categories { get; set; } = [];
}
