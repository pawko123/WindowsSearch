using System.Text;
using WindowsSearch.Common.Models;
using WindowsSearch.Common.Serialization;

namespace WindowsSearch.Common.Tests.Serialization;

public class MessageSerializerFactoryTests
{
    [Fact]
    public void Create_WithJson_ReturnsJsonSerializer()
    {
        var serializer = MessageSerializerFactory.Create(SerializationKind.Json);
        Assert.IsType<JsonMessageSerializer>(serializer);
    }

    [Fact]
    public void Create_WithProtobuf_ReturnsProtobufSerializer()
    {
        var serializer = MessageSerializerFactory.Create(SerializationKind.Protobuf);
        Assert.IsType<ProtobufMessageSerializer>(serializer);
    }

    [Fact]
    public void Create_WithXml_ReturnsXmlSerializer()
    {
        var serializer = MessageSerializerFactory.Create(SerializationKind.Xml);
        Assert.IsType<XmlMessageSerializer>(serializer);
    }

    [Fact]
    public void Create_WithUnknown_ReturnsJsonSerializerAsDefault()
    {
        var serializer = MessageSerializerFactory.Create((SerializationKind)999);
        Assert.IsType<JsonMessageSerializer>(serializer);
    }
}
