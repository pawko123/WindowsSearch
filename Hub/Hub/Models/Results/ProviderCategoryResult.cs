using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Hub.Models.App;

namespace Hub.Models.Results;

public sealed class ProviderCategoryResultUi : INotifyPropertyChanged
{
    public required string Name { get; init; }

    public string? IconPath { get; init; }

    private ImageSource? iconImage;

    public ImageSource? IconImage
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

    public List<AppEntry> Items { get; init; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
