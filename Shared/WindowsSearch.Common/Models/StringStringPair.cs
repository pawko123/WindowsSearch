using ProtoBuf;
using System.Xml.Serialization;

namespace WindowsSearch.Common.Models;

public class StringStringPair
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
