using BaseProvider.Abstractions;
using WindowsSearch.Common.Models;

namespace BaseProvider.Transports;

public sealed class ProviderTransportFactory : IProviderTransportFactory
{
    public IProviderTransport Create(ProviderTransportKind transportKind, string endpoint)
    {
        return transportKind switch
        {
            ProviderTransportKind.NamedPipe => new NamedPipeProviderTransport(endpoint),
            ProviderTransportKind.Http => new HttpProviderTransport(endpoint),
            ProviderTransportKind.Grpc => new GrpcProviderTransport(endpoint),
            _ => new NamedPipeProviderTransport(endpoint),
        };
    }
}
