namespace Hub.Services.Providers.Strategies;

public interface IProviderSearchStrategy
{
    IAsyncEnumerable<ProviderSearchOutcome> SearchAsync(
        IReadOnlyList<string> providerNames,
        Func<string, CancellationToken, Task<ProviderSearchOutcome>> searchFunc,
        CancellationToken cancellationToken);
}