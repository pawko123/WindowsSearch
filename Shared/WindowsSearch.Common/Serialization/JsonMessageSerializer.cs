using System.Text.Json;

namespace WindowsSearch.Common.Serialization;

public class JsonMessageSerializer : IMessageSerializer
{
    public string ContentType => "application/json";

    private readonly JsonSerializerOptions _options;

    public JsonMessageSerializer()
    {
        _options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

    public byte[] Serialize<T>(T value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value, _options);
    }

    public T Deserialize<T>(byte[] data)
    {
        return JsonSerializer.Deserialize<T>(data, _options) 
               ?? throw new InvalidOperationException("Failed to deserialize JSON message.");
    }

    public string FormatForLog<T>(T value)
    {
        return JsonSerializer.Serialize(value, _options);
    }
}
