using Hub.Models.Providers;
using Hub.Models.Settings;
using Hub.Services.Providers.Transports;

namespace Hub.Services.Providers;

public static class ProviderClientFactory
{
    public static IProviderClient Create(AppSettings settings)
    {
        return settings.ProviderTransportKind switch
        {
            ProviderTransportKind.NamedPipe => new NamedPipeProviderClient(settings.ProviderEndpoint, settings.ProviderTimeoutSeconds),
            ProviderTransportKind.Http => new HttpProviderClient(settings.ProviderEndpoint, settings.ProviderTimeoutSeconds),
            ProviderTransportKind.Grpc => new GrpcProviderClient(settings.ProviderEndpoint, settings.ProviderTimeoutSeconds),
            _ => new NamedPipeProviderClient(settings.ProviderEndpoint, settings.ProviderTimeoutSeconds),
        };
    }
}