using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;

namespace Hub.Converters;

public class PathExistsToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrWhiteSpace(path))
        {
            // Only check disk existence for absolute/rooted paths without wildcards.
            // Relative names (e.g. '.git', 'node_modules', 'bin') and wildcard patterns (e.g. '*.tmp')
            // are filter patterns, not physical paths on disk.
            if (!Path.IsPathRooted(path) || path.Contains('*') || path.Contains('?'))
            {
                return Visibility.Collapsed;
            }

            var exists = Directory.Exists(path) || File.Exists(path);
            var showWhenMissing = parameter is string param && param.Equals("Missing", StringComparison.OrdinalIgnoreCase);
            var visible = showWhenMissing ? !exists : exists;
            return visible ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
