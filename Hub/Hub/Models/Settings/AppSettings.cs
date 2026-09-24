using WindowsSearch.Common.Models;
using WindowsSearch.Common.Serialization;
using WindowsSearch.Common.Logging;

namespace Hub.Models.Settings;

public sealed class AppSettings
{
    public ImageResolverKind ImageResolverKind { get; set; } = ImageResolverKind.ConcurrentDictionary;

    public ProviderTransportKind ProviderTransportKind { get; set; } = ProviderTransportKind.NamedPipe;

    public SerializationKind ProviderSerialization { get; set; } = SerializationKind.Json;
    
    public LogLevel LogLevel { get; set; } = LogLevel.Info;

    public int ProviderTimeoutSeconds { get; set; } = 5;

    public int SearchLimit { get; set; } = 50;
}
