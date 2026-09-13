using BaseProvider.Models;

namespace BaseProvider.Abstractions;

public interface IProviderTransport : IAsyncDisposable
{
    ProviderTransportKind TransportKind { get; }

    Task RunAsync(Func<ProviderSearchRequest, CancellationToken, Task<ProviderSearchResponse>> handler, CancellationToken cancellationToken);
}
