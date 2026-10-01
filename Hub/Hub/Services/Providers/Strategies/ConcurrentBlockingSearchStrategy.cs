using System.Runtime.CompilerServices;
using WindowsSearch.Common.Logging;

namespace Hub.Services.Providers.Strategies;

public sealed class ConcurrentBlockingSearchStrategy : IProviderSearchStrategy
{
    public async IAsyncEnumerable<ProviderSearchOutcome> SearchAsync(
        IReadOnlyList<string> providerNames,
        Func<string, CancellationToken, Task<ProviderSearchOutcome>> searchFunc,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var tasks = new List<Task<ProviderSearchOutcome>>();

        foreach (var providerName in providerNames)
        {
            var name = providerName;
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    return await searchFunc(name, cancellationToken);
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"[ConcurrentBlockingSearchStrategy] Error executing search task for '{name}'", ex);
                    return new ProviderSearchOutcome { ProviderName = name };
                }
            }, cancellationToken));
        }

        ProviderSearchOutcome[] outcomes = [];
        try
        {
            outcomes = await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            AppLogger.Error("[ConcurrentBlockingSearchStrategy] Exception waiting for all tasks.", ex);
        }

        // Yield all at once at the end
        foreach (var outcome in outcomes)
        {
            yield return outcome;
        }
    }
}