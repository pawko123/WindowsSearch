using System.IO.Abstractions;
using System.Windows.Media;
using Microsoft.Extensions.Caching.Memory;

namespace Hub.Services.Results;

public sealed class MemoryCacheImageResolver : BaseImageResolver
{
    private readonly IMemoryCache _cache;

    public MemoryCacheImageResolver(IMemoryCache? cache = null, IFileSystem? fileSystem = null, bool useCache = true)
        : base(fileSystem, useCache)
    {
        _cache = cache ?? new MemoryCache(new MemoryCacheOptions());
    }

    protected override ImageSource? GetOrCreateCached(string key, Func<ImageSource?> factory)
    {
        return _cache.GetOrCreate(key, _ => factory());
    }
}
