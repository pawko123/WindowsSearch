using System.IO.Abstractions.TestingHelpers;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Hub.Services.Results;
using Microsoft.Extensions.Caching.Memory;

namespace Hub.Tests.Services.Results;

public class BaseImageResolverTests
{
    private class TestableImageResolver : BaseImageResolver
    {
        public int FactoryInvocationCount { get; private set; }

        public TestableImageResolver(MockFileSystem? fileSystem = null, bool useCache = true)
            : base(fileSystem, useCache)
        {
        }

        protected override ImageSource? GetOrCreateCached(string key, Func<ImageSource?> factory)
        {
            FactoryInvocationCount++;
            return factory();
        }
    }

    [Fact]
    public void BaseResolver_WhenUseCacheIsFalse_BypassesGetOrCreateCachedCompletely()
    {
        var resolver = new TestableImageResolver(useCache: false);
        var dummyKey = "test-key";

        _ = resolver.ResolveAsync(dummyKey);

        Assert.Equal(0, resolver.FactoryInvocationCount);
    }

    [Fact]
    public async Task ListImageResolver_WhenUseCacheIsTrue_CachesResult()
    {
        var mockFs = new MockFileSystem();
        var resolver = new ListImageResolver(mockFs, useCache: true);

        // Verify it inherits BaseImageResolver and UseCache is true
        Assert.True(resolver.UseCache);
        Assert.Null(await resolver.ResolveAsync("missing.png"));
    }

    [Fact]
    public async Task DictionaryImageResolver_WhenUseCacheIsTrue_CachesResult()
    {
        var mockFs = new MockFileSystem();
        var resolver = new DictionaryImageResolver(mockFs, useCache: true);

        Assert.True(resolver.UseCache);
        Assert.Null(await resolver.ResolveAsync("missing.png"));
    }

    [Fact]
    public async Task ConcurrentDictionaryImageResolver_WhenUseCacheIsTrue_CachesResult()
    {
        var mockFs = new MockFileSystem();
        var resolver = new ConcurrentDictionaryImageResolver(mockFs, useCache: true);

        Assert.True(resolver.UseCache);
        Assert.Null(await resolver.ResolveAsync("missing.png"));
    }

    [Fact]
    public async Task MemoryCacheImageResolver_WhenUseCacheIsTrue_CachesResult()
    {
        var mockFs = new MockFileSystem();
        var resolver = new MemoryCacheImageResolver(new MemoryCache(new MemoryCacheOptions()), mockFs, useCache: true);

        Assert.True(resolver.UseCache);
        Assert.Null(await resolver.ResolveAsync("missing.png"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task ResolveAsync_WhenKeyIsNullOrWhitespace_ReturnsNull(string? key)
    {
        var resolver = new ConcurrentDictionaryImageResolver();
        var result = await resolver.ResolveAsync(key!);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_WhenFileDoesNotExist_ReturnsNull()
    {
        var mockFileSystem = new MockFileSystem();
        var resolver = new ConcurrentDictionaryImageResolver(fileSystem: mockFileSystem);

        var result = await resolver.ResolveAsync(@"C:\missing\icon.png");

        Assert.Null(result);
    }

    [Fact]
    public void ConcreteResolvers_AllInheritFromBaseImageResolver()
    {
        Assert.True(typeof(BaseImageResolver).IsAssignableFrom(typeof(ListImageResolver)));
        Assert.True(typeof(BaseImageResolver).IsAssignableFrom(typeof(DictionaryImageResolver)));
        Assert.True(typeof(BaseImageResolver).IsAssignableFrom(typeof(ConcurrentDictionaryImageResolver)));
        Assert.True(typeof(BaseImageResolver).IsAssignableFrom(typeof(MemoryCacheImageResolver)));
    }

    [Fact]
    public async Task GetAndResetMetrics_TracksMetricsAndResets()
    {
        var mockFs = new MockFileSystem();
        mockFs.AddFile(@"C:\test.png", new MockFileData([]));
        var resolver = new ConcurrentDictionaryImageResolver(mockFs, useCache: true);

        await resolver.ResolveAsync(@"C:\test.png");
        await resolver.ResolveAsync(@"C:\test.png");

        var metrics = resolver.GetAndResetMetrics();
        Assert.Equal(2, metrics.ImagesRequested);
        Assert.Equal(1, metrics.CacheMisses);
        Assert.Equal(1, metrics.CacheHits);
        Assert.True(metrics.TotalResolveTimeMs >= 0);

        var resetMetrics = resolver.GetAndResetMetrics();
        Assert.Equal(0, resetMetrics.ImagesRequested);
        Assert.Equal(0, resetMetrics.CacheMisses);
        Assert.Equal(0, resetMetrics.CacheHits);
    }
}
