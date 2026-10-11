using System.Windows.Media;

namespace Hub.Services.Results;

public interface IImageResolver
{
    bool UseCache { get; set; }

    Task<ImageSource?> ResolveAsync(string key);
}
