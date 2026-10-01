using System.Runtime.CompilerServices;
using WindowsSearch.Common.Logging;

namespace Hub.Services.Providers.Strategies;

public sealed class ConcurrentStreamSearchStrategy : IProviderSearchStrategy
{
    public async IAsyncEnumerable<ProviderSearchOutcome> SearchAsync(
        IReadOnlyList<string> providerNames,
        Func<string, CancellationToken, Task<ProviderSearchOutcome>> searchFunc,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var tasks = providerNames.Select(name => Task.Run(async () =>
        {
            try
            {
                return await searchFunc(name, cancellationToken);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[ConcurrentStreamSearchStrategy] Error executing search task for '{name}'", ex);
                return new ProviderSearchOutcome { ProviderName = name };
            }
        }, cancellationToken)).ToList();

        while (tasks.Count > 0)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var completedTask = await Task.WhenAny(tasks);
            tasks.Remove(completedTask);

            ProviderSearchOutcome? outcome = null;
            try
            {
                outcome = await completedTask;
            }
            catch (Exception ex)
            {
                AppLogger.Error("[ConcurrentStreamSearchStrategy] Exception waiting for task completion.", ex);
            }

            if (outcome != null)
            {
                yield return outcome;
            }
        }
    }
}