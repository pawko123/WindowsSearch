using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Hub.ViewModels.Settings;

public abstract class SettingItemViewModel : INotifyPropertyChanged
{
    public object? TargetObject { get; }
    public PropertyInfo? Property { get; }
    public string Label { get; }
    public string Description { get; }

    public int IndentDepth { get; set; }
    public virtual System.Windows.Thickness LeftMargin => new(IndentDepth * 24, 0, 0, 16);

    public ObservableCollection<string> Errors { get; } = new();
    public bool HasErrors => Errors.Count > 0;

    public string? ErrorText
    {
        get => Errors.Count > 0 ? Errors[0] : null;
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                if (Errors.Count > 0)
                {
                    Errors.Clear();
                }
            }
            else
            {
                if (Errors.Count == 0 || Errors[0] != value)
                {
                    Errors.Clear();
                    Errors.Add(value);
                }
            }
            OnPropertyChanged();
        }
    }

    protected SettingItemViewModel(object? targetObject, PropertyInfo? property, string label, string description)
    {
        TargetObject = targetObject;
        Property = property;
        Label = label;
        Description = description;

        Errors.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasErrors));
            OnPropertyChanged(nameof(ErrorText));
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public abstract void Apply();
}
