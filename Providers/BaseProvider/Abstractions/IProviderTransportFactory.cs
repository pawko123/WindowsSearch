using BaseProvider.Models;
using WindowsSearch.Common.Models;

namespace BaseProvider.Abstractions;

public interface IProviderTransportFactory
{
    IProviderTransport Create(ProviderSettings settings);
}
