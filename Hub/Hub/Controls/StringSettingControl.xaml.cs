using System.Windows;
using System.Windows.Controls;

namespace Hub.Controls;

public partial class StringSettingControl : UserControl
{
    public static readonly DependencyProperty TextValueProperty =
        DependencyProperty.Register(nameof(TextValue), typeof(string), typeof(StringSettingControl), new PropertyMetadata(string.Empty));

    public string TextValue
    {
        get => (string)GetValue(TextValueProperty);
        set => SetValue(TextValueProperty, value);
    }

    public StringSettingControl()
    {
        InitializeComponent();
    }
}