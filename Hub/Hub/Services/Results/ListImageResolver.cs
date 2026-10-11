using System.IO.Abstractions;
using System.Windows.Media;

namespace Hub.Services.Results;

public sealed class ListImageResolver : BaseImageResolver
{
    private readonly List<(string Key, ImageSource? Image)> _cache = [];

    public ListImageResolver(IFileSystem? fileSystem = null, bool useCache = true)
        : base(fileSystem, useCache)
    {
    }

    protected override ImageSource? GetOrCreateCached(string key, Func<ImageSource?> factory)
    {
        lock (_cache)
        {
            var match = _cache.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match.Key) || match.Image is not null)
            {
                return match.Image;
            }

            var resolved = factory();
            _cache.Add((key, resolved));
            return resolved;
        }
    }
}
