using WindowsSearch.Common.Models;
using WindowsSearch.Common.Serialization;

namespace WindowsSearch.Common.Tests.Serialization;

public class ProtobufMessageSerializerTests
{
    private readonly ProtobufMessageSerializer _serializer = new();

    [Fact]
    public void Serialize_Deserialize_RoundtripsSuccessfully()
    {
        var request = new ProviderSearchRequest
        {
            Query = "test proto",
            Limit = 5,
            SettingsYaml = "is_enabled: true",
            TimeoutMs = 5000
        };

        var bytes = _serializer.Serialize(request);
        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);

        var deserialized = _serializer.Deserialize<ProviderSearchRequest>(bytes);
        
        Assert.NotNull(deserialized);
        Assert.Equal(request.Query, deserialized.Query);
        Assert.Equal(request.Limit, deserialized.Limit);
        Assert.Equal(request.SettingsYaml, deserialized.SettingsYaml);
        Assert.Equal(request.TimeoutMs, deserialized.TimeoutMs);
    }
}
