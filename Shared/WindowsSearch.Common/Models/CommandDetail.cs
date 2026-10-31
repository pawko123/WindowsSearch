using ProtoBuf;
using System.Xml.Serialization;

namespace WindowsSearch.Common.Models;

[ProtoContract]
public sealed class CommandDetail
{
    [ProtoMember(1)]
    [XmlElement("Key")]
    public string Key { get; set; } = string.Empty;

    [ProtoMember(2)]
    [XmlElement("Value")]
    public string Value { get; set; } = string.Empty;

    public CommandDetail()
    {
    }

    public CommandDetail(string key, string value)
    {
        Key = key;
        Value = value;
    }
}
