using WindowsSearch.Common.Models;

namespace BaseProvider.Abstractions;

public interface IResultFinder<TSettings> where TSettings : ProviderSettingsBase, new()
{
    Task<ProviderSearchResponse> FindAsync(ProviderSearchRequest request, TSettings settings, CancellationToken cancellationToken);
}
