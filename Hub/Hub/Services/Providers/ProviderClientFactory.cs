using Hub.Models.Providers;
using Hub.Models.Settings;
using Hub.Services.Providers.Transports;

namespace Hub.Services.Providers;

public static class ProviderClientFactory
{
    public static IProviderClient Create(ProviderTransportKind transport, string endpoint, int timeoutSeconds)
    {
        return transport switch
        {
            ProviderTransportKind.NamedPipe => new NamedPipeProviderClient(endpoint, timeoutSeconds),
            ProviderTransportKind.Http => new HttpProviderClient(endpoint, timeoutSeconds),
            ProviderTransportKind.Grpc => new GrpcProviderClient(endpoint, timeoutSeconds),
            _ => new NamedPipeProviderClient(endpoint, timeoutSeconds),
        };
    }
}