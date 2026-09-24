using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Hub.Models.App;

public sealed class AppEntry : INotifyPropertyChanged
{
    public required string Name { get; init; }

    public string? Subtitle { get; init; }

    public required string ExecutablePath { get; init; }
    public string? Arguments { get; init; }
    public string? Source { get; init; }
    public string? IconPath { get; set; }

    private System.Windows.Media.ImageSource? iconImage;

    public System.Windows.Media.ImageSource? IconImage
    {
        get => iconImage;
        set
        {
            if (!ReferenceEquals(iconImage, value))
            {
                iconImage = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
