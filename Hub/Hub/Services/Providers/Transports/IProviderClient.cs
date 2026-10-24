using WindowsSearch.Common.Models;
using WindowsSearch.Common.Models.Benchmark;

namespace Hub.Services.Providers.Transports;

public interface IProviderClient : IAsyncDisposable
{
    ProviderTransportKind TransportKind { get; }
    ProviderBenchmarkMetrics? LastMetrics { get; }

    Task<ProviderSearchResponse> SearchAsync(ProviderSearchRequest request, CancellationToken cancellationToken);
}
