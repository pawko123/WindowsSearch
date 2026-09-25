using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Hub.ViewModels.Settings;

public abstract class SettingItemViewModel : INotifyPropertyChanged
{
    public PropertyInfo Property { get; }
    public string Label { get; }
    public string Description { get; }

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