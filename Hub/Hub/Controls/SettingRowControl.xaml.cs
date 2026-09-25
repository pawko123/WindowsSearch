using System.Windows;
using System.Windows.Controls;

namespace Hub.Controls;

public partial class SettingRowControl : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(SettingRowControl), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(SettingRowControl), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SettingContentProperty =
        DependencyProperty.Register(nameof(SettingContent), typeof(object), typeof(SettingRowControl), new PropertyMetadata(null));

    public static readonly DependencyProperty ErrorTextProperty =
        DependencyProperty.Register(nameof(ErrorText), typeof(string), typeof(SettingRowControl), new PropertyMetadata(null));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public object SettingContent
    {
        get => GetValue(SettingContentProperty);
        set => SetValue(SettingContentProperty, value);
    }

    public string? ErrorText
    {
        get => (string?)GetValue(ErrorTextProperty);
        set => SetValue(ErrorTextProperty, value);
    }

    public SettingRowControl()
    {
        InitializeComponent();
    }
}