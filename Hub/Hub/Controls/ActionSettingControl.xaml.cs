using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Hub.Controls;

public partial class ActionSettingControl : UserControl
{
    public static readonly DependencyProperty ButtonTextProperty =
        DependencyProperty.Register(nameof(ButtonText), typeof(string), typeof(ActionSettingControl), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty StatusMessageProperty =
        DependencyProperty.Register(nameof(StatusMessage), typeof(string), typeof(ActionSettingControl), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsRunningProperty =
        DependencyProperty.Register(nameof(IsRunning), typeof(bool), typeof(ActionSettingControl), new PropertyMetadata(false));

    public static readonly DependencyProperty CanCancelProperty =
        DependencyProperty.Register(nameof(CanCancel), typeof(bool), typeof(ActionSettingControl), new PropertyMetadata(false));

    public static readonly DependencyProperty ProgressPercentageProperty =
        DependencyProperty.Register(nameof(ProgressPercentage), typeof(double?), typeof(ActionSettingControl), new PropertyMetadata(null));

    public static readonly DependencyProperty DetailsProperty =
        DependencyProperty.Register(nameof(Details), typeof(IEnumerable), typeof(ActionSettingControl), new PropertyMetadata(null));

    public static readonly DependencyProperty ExecuteCommandProperty =
        DependencyProperty.Register(nameof(ExecuteCommand), typeof(ICommand), typeof(ActionSettingControl), new PropertyMetadata(null));

    public static readonly DependencyProperty CancelCommandProperty =
        DependencyProperty.Register(nameof(CancelCommand), typeof(ICommand), typeof(ActionSettingControl), new PropertyMetadata(null));

    public string ButtonText
    {
        get => (string)GetValue(ButtonTextProperty);
        set => SetValue(ButtonTextProperty, value);
    }

    public string StatusMessage
    {
        get => (string)GetValue(StatusMessageProperty);
        set => SetValue(StatusMessageProperty, value);
    }

    public bool IsRunning
    {
        get => (bool)GetValue(IsRunningProperty);
        set => SetValue(IsRunningProperty, value);
    }

    public bool CanCancel
    {
        get => (bool)GetValue(CanCancelProperty);
        set => SetValue(CanCancelProperty, value);
    }

    public double? ProgressPercentage
    {
        get => (double?)GetValue(ProgressPercentageProperty);
        set => SetValue(ProgressPercentageProperty, value);
    }

    public IEnumerable? Details
    {
        get => (IEnumerable?)GetValue(DetailsProperty);
        set => SetValue(DetailsProperty, value);
    }

    public ICommand? ExecuteCommand
    {
        get => (ICommand?)GetValue(ExecuteCommandProperty);
        set => SetValue(ExecuteCommandProperty, value);
    }

    public ICommand? CancelCommand
    {
        get => (ICommand?)GetValue(CancelCommandProperty);
        set => SetValue(CancelCommandProperty, value);
    }

    public ActionSettingControl()
    {
        InitializeComponent();
    }
}
