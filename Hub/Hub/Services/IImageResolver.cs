using System.Threading.Tasks;
using System.Windows.Media;

namespace Hub.Services;

public interface IImageResolver
{
    Task<ImageSource?> ResolveAsync(string key);
}
