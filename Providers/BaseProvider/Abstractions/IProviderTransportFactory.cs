using BaseProvider.Models;

namespace BaseProvider.Abstractions;

public interface IProviderTransportFactory
{
    IProviderTransport Create(ProviderSettings settings);
}
