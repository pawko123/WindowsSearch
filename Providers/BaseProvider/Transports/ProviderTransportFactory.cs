using BaseProvider.Abstractions;
using WindowsSearch.Common.Models;

namespace BaseProvider.Transports;

public sealed class ProviderTransportFactory : IProviderTransportFactory
{
    public IProviderTransport Create(ProviderSettingsBase settings)
    {
        return settings.Transport switch
        {
            ProviderTransportKind.NamedPipe => new NamedPipeProviderTransport(settings.ActiveEndpoint, settings.TimeoutSeconds),
            ProviderTransportKind.Http => new HttpProviderTransport(settings.ActiveEndpoint, settings.TimeoutSeconds),
            ProviderTransportKind.Grpc => new GrpcProviderTransport(settings.ActiveEndpoint, settings.TimeoutSeconds),
            _ => new NamedPipeProviderTransport(settings.ActiveEndpoint, settings.TimeoutSeconds),
        };
    }
}
