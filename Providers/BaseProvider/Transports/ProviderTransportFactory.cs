using BaseProvider.Abstractions;
using BaseProvider.Models;

namespace BaseProvider.Transports;

public sealed class ProviderTransportFactory : IProviderTransportFactory
{
    public IProviderTransport Create(ProviderSettings settings)
    {
        return settings.Transport switch
        {
            ProviderTransportKind.NamedPipe => new NamedPipeProviderTransport(settings.Endpoint, settings.TimeoutSeconds),
            ProviderTransportKind.Http => new HttpProviderTransport(settings.Endpoint, settings.TimeoutSeconds),
            ProviderTransportKind.Grpc => new GrpcProviderTransport(settings.Endpoint, settings.TimeoutSeconds),
            _ => new NamedPipeProviderTransport(settings.Endpoint, settings.TimeoutSeconds),
        };
    }
}
