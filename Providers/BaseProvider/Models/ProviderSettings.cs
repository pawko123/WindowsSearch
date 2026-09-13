namespace BaseProvider.Models;

public sealed class ProviderSettings
{
    public ProviderTransportKind Transport { get; set; } = ProviderTransportKind.NamedPipe;

    public string Endpoint { get; set; } = @"\\.\pipe\base_provider";

    public int TimeoutSeconds { get; set; } = 5;

    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}