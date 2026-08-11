using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using Hub.Models;
using Hub.Services;

namespace Hub.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly AppIndexService appIndexService = new();
    private readonly List<AppEntry> allApps;
    private readonly Dispatcher uiDispatcher;
    private AppEntry? selectedApp;
    private string searchText = string.Empty;
    private IImageResolver? imageResolver;
    private System.Threading.Timer? refreshTimer;
    private bool launcherVisible;

    public MainViewModel(Dispatcher dispatcher)
    {
        uiDispatcher = dispatcher;
        allApps = appIndexService.GetInstalledApps().ToList();
        FilteredApps = new ObservableCollection<AppEntry>(allApps);
        VisibleApps = new ObservableCollection<AppEntry>();
        selectedApp = null;

        refreshTimer = new System.Threading.Timer(async _ => await RefreshInBackgroundAsync(), null, 60000, 60000);
    }

    public ObservableCollection<AppEntry> FilteredApps { get; }
    public ObservableCollection<AppEntry> VisibleApps { get; }

    public AppEntry? SelectedApp
    {
        get => selectedApp;
        set
        {
            if (!ReferenceEquals(selectedApp, value))
            {
                selectedApp = value;
                OnPropertyChanged();
            }
        }
    }

    public string SearchText
    {
        get => searchText;
        set
        {
            if (searchText != value)
            {
                searchText = value;
                OnPropertyChanged();
                ApplyFilter();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshApps()
    {
        var apps = appIndexService.GetInstalledApps();
        lock (allApps)
        {
            allApps.Clear();
            allApps.AddRange(apps);
        }
        ApplyFilter();
        uiDispatcher.Invoke(() =>
        {
            VisibleApps.Clear();
            SelectedApp = null;
        });
    }

    public void ClearSearch()
    {
        SearchText = string.Empty;
    }

    public bool MoveSelection(int offset)
    {
        if (VisibleApps.Count == 0)
        {
            SelectedApp = null;
            return false;
        }
        var currentIndex = SelectedApp is null ? -1 : VisibleApps.IndexOf(SelectedApp);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, VisibleApps.Count - 1);
        SelectedApp = VisibleApps[nextIndex];
        return true;
    }

    public void SelectFirst()
    {
        SelectedApp = FilteredApps.FirstOrDefault();
    }

    public void LaunchSelected()
    {
        if (SelectedApp is not null)
        {
            AppIndexService.Launch(SelectedApp);
        }
    }

    private void ApplyFilter()
    {
        var query = SearchText ?? string.Empty;
        var matches = allApps
            .Where(app => string.IsNullOrWhiteSpace(query) || app.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(app => !string.IsNullOrWhiteSpace(query) && app.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            .ThenBy(app => app.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        FilteredApps.Clear();
        foreach (var app in matches)
            FilteredApps.Add(app);

        uiDispatcher.Invoke(() =>
        {
            VisibleApps.Clear();
            SelectedApp = null;
        });
    }

    public void SetImageResolver(IImageResolver resolver)
    {
        imageResolver = resolver;
    }

    public void SetLauncherVisible(bool isVisible)
    {
        launcherVisible = isVisible;
    }

    public async Task UpdateVisibleAndResolveAsync(double windowWidth)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            uiDispatcher.Invoke(() =>
            {
                VisibleApps.Clear();
                SelectedApp = null;
            });
            return;
        }

        const double itemWidth = 176;
        int maxVisible = Math.Max(1, (int)Math.Floor((windowWidth - 80) / itemWidth));

        var toShow = FilteredApps.Take(maxVisible).ToList();

        uiDispatcher.Invoke(() =>
        {
            VisibleApps.Clear();
            foreach (var a in toShow)
                VisibleApps.Add(a);
            SelectedApp = VisibleApps.FirstOrDefault();
        });

        if (imageResolver is null)
            return;

        foreach (var app in toShow)
        {
            if (app.IconImage is not null)
                continue;

            var key = app.IconPath ?? app.ExecutablePath;
            try
            {
                var img = await imageResolver.ResolveAsync(key);
                if (img is not null)
                {
                    app.IconImage = img;
                    OnPropertyChanged(nameof(VisibleApps));
                }
            }
            catch { }
        }
    }

    private async Task RefreshInBackgroundAsync()
    {
        try
        {
            if (launcherVisible)
            {
                return;
            }

            var apps = appIndexService.GetInstalledApps();
            lock (allApps)
            {
                allApps.Clear();
                allApps.AddRange(apps);
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                ApplyFilter();
                await uiDispatcher.InvokeAsync(async () => await UpdateVisibleAndResolveAsync(SystemParameters.PrimaryScreenWidth));
            }
        }
        catch { }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
