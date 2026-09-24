using System.Collections.Concurrent;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Hub.Services.Results;

public sealed class ConcurrentDictionaryImageResolver : IImageResolver
{
    private readonly ConcurrentDictionary<string, ImageSource?> cache = new(StringComparer.OrdinalIgnoreCase);

    public Task<ImageSource?> ResolveAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.FromResult<ImageSource?>(null);
        }

        if (cache.TryGetValue(key, out var existing))
        {
            return Task.FromResult(existing);
        }

        var resolved = LoadImage(key);
        cache[key] = resolved;
        return Task.FromResult(resolved);
    }

    private static ImageSource? LoadImage(string key)
    {
        try
        {
            if (!File.Exists(key))
            {
                return null;
            }

            if (IsImageFile(key))
            {
                using var stream = File.OpenRead(key);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }

            using var icon = Icon.ExtractAssociatedIcon(key);
            if (icon is null)
            {
                return null;
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
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsImageFile(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".ico";
    }
}
