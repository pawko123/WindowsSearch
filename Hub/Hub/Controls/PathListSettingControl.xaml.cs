using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Hub.Controls;

public partial class PathListSettingControl : UserControl
{
    public static readonly DependencyProperty PathsProperty =
        DependencyProperty.Register(nameof(Paths), typeof(ObservableCollection<string>), typeof(PathListSettingControl), new PropertyMetadata(null));

    public static readonly DependencyProperty AvailableDrivesProperty =
        DependencyProperty.Register(nameof(AvailableDrives), typeof(ObservableCollection<string>), typeof(PathListSettingControl), new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedDriveProperty =
        DependencyProperty.Register(nameof(SelectedDrive), typeof(string), typeof(PathListSettingControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public ObservableCollection<string>? Paths
    {
        get => (ObservableCollection<string>?)GetValue(PathsProperty);
        set => SetValue(PathsProperty, value);
    }

    public ObservableCollection<string>? AvailableDrives
    {
        get => (ObservableCollection<string>?)GetValue(AvailableDrivesProperty);
        set => SetValue(AvailableDrivesProperty, value);
    }

    public string? SelectedDrive
    {
        get => (string?)GetValue(SelectedDriveProperty);
        set => SetValue(SelectedDriveProperty, value);
    }

    public PathListSettingControl()
    {
        InitializeComponent();
    }
}
