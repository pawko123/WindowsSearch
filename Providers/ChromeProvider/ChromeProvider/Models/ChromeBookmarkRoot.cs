using System.Text.Json.Serialization;

namespace ChromeProvider.Models;

public class ChromeBookmarkRoot
{
    [JsonPropertyName("roots")]
    public Dictionary<string, ChromeBookmarkNode> Roots { get; set; } = new();
}