namespace Hub.Models.Providers;

public sealed class ProviderSearchRequest
{
    public string Query { get; set; } = string.Empty;

    public int Limit { get; set; }

    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}