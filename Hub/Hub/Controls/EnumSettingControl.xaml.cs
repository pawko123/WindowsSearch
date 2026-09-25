using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Hub.Controls;

public partial class EnumSettingControl : UserControl
{
    public static readonly DependencyProperty EnumOptionsProperty =
        DependencyProperty.Register(nameof(EnumOptions), typeof(IEnumerable<string>), typeof(EnumSettingControl), new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedOptionProperty =
        DependencyProperty.Register(nameof(SelectedOption), typeof(string), typeof(EnumSettingControl), new PropertyMetadata(null));

    public IEnumerable<string> EnumOptions
    {
        get => (IEnumerable<string>)GetValue(EnumOptionsProperty);
        set => SetValue(EnumOptionsProperty, value);
    }

    public string SelectedOption
    {
        get => (string)GetValue(SelectedOptionProperty);
        set => SetValue(SelectedOptionProperty, value);
    }

    public EnumSettingControl()
    {
        InitializeComponent();
    }
}