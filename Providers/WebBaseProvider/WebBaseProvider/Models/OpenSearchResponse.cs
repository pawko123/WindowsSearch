using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebBaseProvider.Models;

[JsonConverter(typeof(OpenSearchResponseConverter))]
public class OpenSearchResponse
{
    public string Query { get; set; } = string.Empty;
    public List<string> Suggestions { get; set; } = [];
}

public class OpenSearchResponseConverter : JsonConverter<OpenSearchResponse>
{
    public override OpenSearchResponse Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var response = new OpenSearchResponse();

        if (reader.TokenType != JsonTokenType.StartArray)
            return response; // Return empty if not an array

        reader.Read();
        if (reader.TokenType == JsonTokenType.String)
        {
            response.Query = reader.GetString() ?? string.Empty;
        }

        reader.Read();
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    var suggestion = reader.GetString();
                    if (!string.IsNullOrEmpty(suggestion))
                    {
                        response.Suggestions.Add(suggestion);
                    }
                }
            }
        }

        // Skip the rest of the array (e.g., metadata returned by Google/Bing)
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.StartArray || reader.TokenType == JsonTokenType.StartObject)
            {
                reader.Skip();
            }
        }

        return response;
    }

    public override void Write(Utf8JsonWriter writer, OpenSearchResponse value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}