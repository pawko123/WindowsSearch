using System.Text.Json.Serialization;

namespace ChromeProvider.Models;

public class ChromeBookmarkNode
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("children")]
    public List<ChromeBookmarkNode>? Children { get; set; }
}