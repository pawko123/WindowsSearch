using ProtoBuf;

namespace WindowsSearch.Common.Serialization;

public class ProtobufMessageSerializer : IMessageSerializer
{
    public string ContentType => "application/x-protobuf";

    public byte[] Serialize<T>(T value)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, value);
        return stream.ToArray();
    }

    public T Deserialize<T>(byte[] data)
    {
        using var stream = new MemoryStream(data);
        return Serializer.Deserialize<T>(stream);
    }

    public string FormatForLog<T>(T value)
    {
        var bytes = Serialize(value);
        return $"[Protobuf {bytes.Length} bytes] {Convert.ToBase64String(bytes)}";
    }
}
