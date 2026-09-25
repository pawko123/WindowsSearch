using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Hub.Models.Settings;

namespace Hub.Controls;

public partial class DictionarySettingControl : UserControl
{
    public static readonly DependencyProperty EntriesProperty =
        DependencyProperty.Register(nameof(Entries), typeof(ObservableCollection<ProviderSettingsEntry>), typeof(DictionarySettingControl), new PropertyMetadata(null));

    public ObservableCollection<ProviderSettingsEntry> Entries
    {
        get => (ObservableCollection<ProviderSettingsEntry>)GetValue(EntriesProperty);
        set => SetValue(EntriesProperty, value);
    }

    public DictionarySettingControl()
    {
        InitializeComponent();
    }
}