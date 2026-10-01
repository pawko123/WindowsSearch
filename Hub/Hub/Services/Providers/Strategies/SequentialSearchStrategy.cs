using System.Runtime.CompilerServices;
using WindowsSearch.Common.Logging;

namespace Hub.Services.Providers.Strategies;

public sealed class SequentialSearchStrategy : IProviderSearchStrategy
{
    public async IAsyncEnumerable<ProviderSearchOutcome> SearchAsync(
        IReadOnlyList<string> providerNames,
        Func<string, CancellationToken, Task<ProviderSearchOutcome>> searchFunc,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var allResults = new List<ProviderSearchOutcome>();

        foreach (var providerName in providerNames)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                var outcome = await searchFunc(providerName, cancellationToken);
                allResults.Add(outcome);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[SequentialSearchStrategy] Error executing search for provider '{providerName}'", ex);
                allResults.Add(new ProviderSearchOutcome { ProviderName = providerName });
            }
        }

        // Sequential blocking yields everything at the very end
        foreach (var outcome in allResults)
        {
            yield return outcome;
        }
    }
}