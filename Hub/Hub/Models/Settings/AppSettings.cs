using Hub.Models.Providers;

namespace Hub.Models.Settings;

public sealed class AppSettings
{
    public ImageResolverKind ImageResolverKind { get; set; } = ImageResolverKind.ConcurrentDictionary;

    public ProviderTransportKind ProviderTransportKind { get; set; } = ProviderTransportKind.NamedPipe;

    public string ProviderEndpoint { get; set; } = @"\\.\pipe\hub-provider";

    public int ProviderTimeoutSeconds { get; set; } = 5;

    public int SearchLimit { get; set; } = 50;
}