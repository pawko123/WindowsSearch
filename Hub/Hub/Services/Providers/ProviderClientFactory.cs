using WindowsSearch.Common.Models;
using Hub.Services.Providers.Transports;
using WindowsSearch.Common.Serialization;

namespace Hub.Services.Providers;

public static class ProviderClientFactory
{
    public static IProviderClient Create(ProviderTransportKind transport, string endpoint, int timeoutSeconds, IMessageSerializer serializer)
    {
        return transport switch
        {
            ProviderTransportKind.NamedPipe => new NamedPipeProviderClient(endpoint, timeoutSeconds, serializer),
            ProviderTransportKind.Http => new HttpProviderClient(endpoint, timeoutSeconds, serializer),
            ProviderTransportKind.Grpc => new GrpcProviderClient(endpoint, timeoutSeconds, serializer),
            _ => new NamedPipeProviderClient(endpoint, timeoutSeconds, serializer),
        };
    }
}
