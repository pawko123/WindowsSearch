using BaseProvider.Models;
using WindowsSearch.Common.Models;

namespace BaseProvider.Abstractions;

public interface IResultFinder
{
    Task<ProviderSearchResponse> FindAsync(ProviderSearchRequest request, CancellationToken cancellationToken);
}
