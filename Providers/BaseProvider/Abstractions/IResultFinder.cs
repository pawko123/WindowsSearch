using BaseProvider.Models;

namespace BaseProvider.Abstractions;

public interface IResultFinder
{
    Task<ProviderSearchResponse> FindAsync(ProviderSearchRequest request, CancellationToken cancellationToken);
}
