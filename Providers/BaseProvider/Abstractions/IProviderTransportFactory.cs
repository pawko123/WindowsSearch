using WindowsSearch.Common.Models;

namespace BaseProvider.Abstractions;

public interface IProviderTransportFactory
{
    IProviderTransport Create(ProviderTransportKind transportKind, string endpoint);
}
