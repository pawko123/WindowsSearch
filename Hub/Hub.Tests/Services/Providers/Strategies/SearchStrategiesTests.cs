using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hub.Services.Providers;
using Hub.Services.Providers.Strategies;
using Xunit;

namespace Hub.Tests.Services.Providers.Strategies;

public class SearchStrategiesTests
{
    private static async Task<List<ProviderSearchOutcome>> DrainStreamAsync(IAsyncEnumerable<ProviderSearchOutcome> stream)
    {
        var results = new List<ProviderSearchOutcome>();
        await foreach (var item in stream)
        {
            results.Add(item);
        }
        return results;
    }

    [Theory]
    [InlineData(typeof(SequentialSearchStrategy))]
    [InlineData(typeof(ConcurrentBlockingSearchStrategy))]
    [InlineData(typeof(ConcurrentStreamSearchStrategy))]
    public async Task SearchAsync_AllStrategies_ReturnsExpectedResults(Type strategyType)
    {
        // Arrange
        var strategy = (IProviderSearchStrategy)Activator.CreateInstance(strategyType)!;
        var providers = new[] { "ProviderA", "ProviderB" };

        Func<string, CancellationToken, Task<ProviderSearchOutcome>> searchFunc = (name, token) =>
        {
            return Task.FromResult(new ProviderSearchOutcome { ProviderName = name });
        };

        // Act
        var stream = strategy.SearchAsync(providers, searchFunc, CancellationToken.None);
        var results = await DrainStreamAsync(stream);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.ProviderName == "ProviderA");
        Assert.Contains(results, r => r.ProviderName == "ProviderB");
    }

    [Theory]
    [InlineData(typeof(SequentialSearchStrategy))]
    [InlineData(typeof(ConcurrentBlockingSearchStrategy))]
    [InlineData(typeof(ConcurrentStreamSearchStrategy))]
    public async Task SearchAsync_AllStrategies_HandlesExceptionsGracefully(Type strategyType)
    {
        // Arrange
        var strategy = (IProviderSearchStrategy)Activator.CreateInstance(strategyType)!;
        var providers = new[] { "GoodProvider", "BadProvider" };

        Func<string, CancellationToken, Task<ProviderSearchOutcome>> searchFunc = (name, token) =>
        {
            if (name == "BadProvider")
                throw new InvalidOperationException("Simulation of failure.");

            return Task.FromResult(new ProviderSearchOutcome { ProviderName = name, IconPath = "success" });
        };

        // Act
        var stream = strategy.SearchAsync(providers, searchFunc, CancellationToken.None);
        var results = await DrainStreamAsync(stream);

        // Assert
        Assert.Equal(2, results.Count);
        
        var good = results.Single(r => r.ProviderName == "GoodProvider");
        Assert.Equal("success", good.IconPath);

        var bad = results.Single(r => r.ProviderName == "BadProvider");
        Assert.Empty(bad.IconPath); // The fallback stub does not have IconPath set
    }

    [Fact]
    public async Task SequentialSearchStrategy_ExecutesInOrder()
    {
        // Arrange
        var strategy = new SequentialSearchStrategy();
        var providers = new[] { "SlowProvider", "FastProvider" };
        var executionLog = new List<string>();

        Func<string, CancellationToken, Task<ProviderSearchOutcome>> searchFunc = async (name, token) =>
        {
            if (name == "SlowProvider")
                await Task.Delay(100, token);
                
            executionLog.Add(name);
            return new ProviderSearchOutcome { ProviderName = name };
        };

        // Act
        var stream = strategy.SearchAsync(providers, searchFunc, CancellationToken.None);
        await DrainStreamAsync(stream);

        // Assert
        // Sequential MUST execute in the exact order requested, despite SlowProvider taking longer.
        Assert.Equal("SlowProvider", executionLog[0]);
        Assert.Equal("FastProvider", executionLog[1]);
    }
}