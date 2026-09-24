using WindowsSearch.Common.Models;

namespace Hub.Services.Providers.Transports;

public interface IProviderClient : IAsyncDisposable
{
    ProviderTransportKind TransportKind { get; }

    Task<ProviderSearchResponse> SearchAsync(ProviderSearchRequest request, CancellationToken cancellationToken);
}
