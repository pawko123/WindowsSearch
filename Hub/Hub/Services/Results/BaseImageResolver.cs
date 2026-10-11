using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Abstractions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsSearch.Common.Logging;

namespace Hub.Services.Results;

public abstract class BaseImageResolver : IImageResolver
{
    protected readonly IFileSystem FileSystem;

    public bool UseCache { get; set; }

    protected BaseImageResolver(IFileSystem? fileSystem = null, bool useCache = true)
    {
        FileSystem = fileSystem ?? new FileSystem();
        UseCache = useCache;
    }

    public virtual Task<ImageSource?> ResolveAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.FromResult<ImageSource?>(null);
        }

        if (!UseCache)
        {
            var uncached = LoadImage(key);
            return Task.FromResult(uncached);
        }

        var resolved = GetOrCreateCached(key, () => LoadImage(key));
        return Task.FromResult(resolved);
    }

    protected abstract ImageSource? GetOrCreateCached(string key, Func<ImageSource?> factory);

    protected ImageSource? LoadImage(string key)
    {
        try
        {
            if (!FileSystem.File.Exists(key))
            {
                return null;
            }

            if (IsImageFile(key))
            {
                using var stream = FileSystem.File.OpenRead(key);
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
        catch (Exception ex)
        {
            AppLogger.Error($"[BaseImageResolver] Failed to load image from '{key}'", ex);
            return null;
        }
    }

    protected bool IsImageFile(string path)
    {
        return FileSystem.Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".ico";
    }
}
