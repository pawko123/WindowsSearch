namespace WindowsSearch.Common.Serialization;

public static class MessageSerializerFactory
{
    public static IMessageSerializer Create(SerializationKind serializationKind)
    {
        return serializationKind switch
        {
            SerializationKind.Json => new JsonMessageSerializer(),
            SerializationKind.Protobuf => new ProtobufMessageSerializer(),
            SerializationKind.Xml => new XmlMessageSerializer(),
            _ => new JsonMessageSerializer() // Default
        };
    }
}
