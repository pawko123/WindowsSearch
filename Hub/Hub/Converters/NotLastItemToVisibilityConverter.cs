using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Hub.Converters;

public class NotLastItemToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DependencyObject container)
        {
            var itemsControl = ItemsControl.ItemsControlFromItemContainer(container);
            if (itemsControl != null)
            {
                var index = itemsControl.ItemContainerGenerator.IndexFromContainer(container);
                if (index >= 0 && index < itemsControl.Items.Count - 1)
                {
                    return Visibility.Visible;
                }
            }
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
