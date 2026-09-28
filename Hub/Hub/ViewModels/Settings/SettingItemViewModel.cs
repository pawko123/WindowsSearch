using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Hub.ViewModels.Settings;

public abstract class SettingItemViewModel : INotifyPropertyChanged
{
    public PropertyInfo Property { get; }
    public string Label { get; }
    public string Description { get; }

    public int IndentDepth { get; set; }
    public virtual System.Windows.Thickness LeftMargin => new(IndentDepth * 24, 0, 0, 16);
    public string? ErrorText
    {
        get => field;
        set { field = value; OnPropertyChanged(); }
    }

    protected SettingItemViewModel(PropertyInfo property, string label, string description)
    {
        Property = property;
        Label = label;
        Description = description;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public abstract void ApplyTo(object target);
}