using System.Collections.Concurrent;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Hub.Services;

public sealed class DictionaryImageResolver : IImageResolver
{
    private readonly ConcurrentDictionary<string, ImageSource?> cache = new();

    public Task<ImageSource?> ResolveAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Task.FromResult<ImageSource?>(null);

        if (cache.TryGetValue(key, out var existing))
            return Task.FromResult(existing);

        try
        {
            if (!File.Exists(key))
            {
                cache[key] = null;
                return Task.FromResult<ImageSource?>(null);
            }

            using var icon = Icon.ExtractAssociatedIcon(key);
            if (icon is null)
            {
                cache[key] = null;
                return Task.FromResult<ImageSource?>(null);
            }

            using var bmp = icon.ToBitmap();
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            ms.Position = 0;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();

            cache[key] = bitmap;
            return Task.FromResult<ImageSource?>(bitmap);
        }
        catch
        {
            cache[key] = null;
            return Task.FromResult<ImageSource?>(null);
        }
    }
}
