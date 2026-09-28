using WindowsSearch.Common.Models;
using WindowsSearch.Common.Serialization;

namespace WindowsSearch.Common.Tests.Serialization;

public class JsonMessageSerializerTests
{
    private readonly JsonMessageSerializer _serializer = new();

    [Fact]
    public void Serialize_Deserialize_RoundtripsSuccessfully()
    {
        var request = new ProviderSearchRequest
        {
            Query = "test query",
            Limit = 10,
            SettingsYaml = "key: value",
            TimeoutMs = 1000
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

    [Fact]
    public void Deserialize_InvalidData_ThrowsException()
    {
        var invalidBytes = "Not a JSON"u8.ToArray();

        Assert.ThrowsAny<Exception>(() => _serializer.Deserialize<ProviderSearchRequest>(invalidBytes));
    }
}
