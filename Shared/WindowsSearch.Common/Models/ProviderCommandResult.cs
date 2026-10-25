using ProtoBuf;
using System.Xml.Serialization;

namespace WindowsSearch.Common.Models;

[ProtoContract]
public sealed class ProviderCommandResult
{
    [ProtoMember(1)]
    [XmlElement("Success")]
    public bool Success { get; set; } = true;

    [ProtoMember(2)]
    [XmlElement("Message")]
    public string Message { get; set; } = string.Empty;

    [ProtoMember(3)]
    [XmlElement("IsRunning")]
    public bool IsRunning { get; set; }

    [ProtoMember(4)]
    [XmlElement("ProgressPercentage")]
    public double? ProgressPercentage { get; set; }
    [ProtoMember(5)]
    [XmlArray("Details")]
    [XmlArrayItem("Detail")]
    public List<CommandDetail> Details { get; set; } = [];
}
