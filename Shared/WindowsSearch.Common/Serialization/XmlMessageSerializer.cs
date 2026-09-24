using System.Xml.Serialization;

namespace WindowsSearch.Common.Serialization;

public class XmlMessageSerializer : IMessageSerializer
{
    public string ContentType => "application/xml";

    public byte[] Serialize<T>(T value)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var stream = new MemoryStream();
        serializer.Serialize(stream, value);
        return stream.ToArray();
    }

    public T Deserialize<T>(byte[] data)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var stream = new MemoryStream(data);
        return (T)serializer.Deserialize(stream)!;
    }

    public string FormatForLog<T>(T value)
    {
        var rawXml = System.Text.Encoding.UTF8.GetString(Serialize(value));
        return rawXml.Replace("\r", "").Replace("\n", "");
    }
}
