using System.Collections.Concurrent;
using System.IO.Abstractions;
using System.Windows.Media;

namespace Hub.Services.Results;

public sealed class ConcurrentDictionaryImageResolver : BaseImageResolver
{
    private readonly ConcurrentDictionary<string, ImageSource?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ConcurrentDictionaryImageResolver(IFileSystem? fileSystem = null, bool useCache = true)
        : base(fileSystem, useCache)
    {
    }

    protected override ImageSource? GetOrCreateCached(string key, Func<ImageSource?> factory)
    {
        if (_cache.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var resolved = factory();
        _cache[key] = resolved;
        return resolved;
    }
}
