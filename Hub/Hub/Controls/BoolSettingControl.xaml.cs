using System.Windows;
using System.Windows.Controls;

namespace Hub.Controls;

public partial class BoolSettingControl : UserControl
{
    public static readonly DependencyProperty BoolValueProperty =
        DependencyProperty.Register(nameof(BoolValue), typeof(bool), typeof(BoolSettingControl), new PropertyMetadata(false));

    public bool BoolValue
    {
        get => (bool)GetValue(BoolValueProperty);
        set => SetValue(BoolValueProperty, value);
    }

    public BoolSettingControl()
    {
        InitializeComponent();
    }
}