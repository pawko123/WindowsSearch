using WindowsSearch.Common.Models;
using WindowsSearch.Common.Serialization;
using WindowsSearch.Common.Logging;
namespace BaseProvider.Models;

public sealed class ProviderSettings
{
    public ProviderTransportKind Transport { get; set; } = ProviderTransportKind.NamedPipe;

    public string Endpoint { get; set; } = @"\\.\pipe\base_provider";

    public SerializationKind Serialization { get; set; } = SerializationKind.Json;

    public int TimeoutSeconds { get; set; } = 5;

    public LogLevel LogLevel { get; set; } = LogLevel.Info;

    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
