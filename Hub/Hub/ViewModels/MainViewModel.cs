using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using Hub.Models.App;
using Hub.Models.Results;
using Hub.Models.Settings;
using Hub.Services.Providers;
using Hub.Services.Results;

namespace Hub.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly AppIndexService appIndexService = new();
    private readonly ProviderSearchService providerSearchService = new();
    private readonly List<AppEntry> allApps;
    private readonly Dispatcher uiDispatcher;
    private AppEntry? selectedApp;
    private string searchText = string.Empty;
    private IImageResolver? imageResolver;
    private System.Threading.Timer? refreshTimer;
    private System.Threading.Timer? providerSearchTimer;
    private CancellationTokenSource? providerSearchCts;
    private int providerSearchGeneration;
    private int providerSearchLimit = 25;
    private bool launcherVisible;
    private AppSettings currentSettings;

    public MainViewModel(Dispatcher dispatcher, AppSettings settings)
    {
        uiDispatcher = dispatcher;
        currentSettings = settings;
        allApps = appIndexService.GetInstalledApps().ToList();
        FilteredApps = new ObservableCollection<AppEntry>(allApps);
        AppResults = new ObservableCollection<AppEntry>();
        ProviderSections = new ObservableCollection<ProviderCategoryResultUi>();
        VisibleApps = new ObservableCollection<AppEntry>();
        selectedApp = null;

        refreshTimer = new System.Threading.Timer(async _ => await RefreshInBackgroundAsync(), null, 60000, 60000);
    }

    public void ApplySettings(AppSettings settings)
    {
        currentSettings = settings;
    }

    public ObservableCollection<AppEntry> FilteredApps { get; }

    public ObservableCollection<AppEntry> AppResults { get; }

    public ObservableCollection<ProviderCategoryResultUi> ProviderSections { get; }

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
            AppResults.Clear();
            ProviderSections.Clear();
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
        SelectedApp = VisibleApps.FirstOrDefault() ?? FilteredApps.FirstOrDefault();
    }

    public void LaunchSelected()
    {
        if (SelectedApp is not null)
        {
            AppIndexService.Launch(SelectedApp);
        }
    }

    public void Launch(AppEntry app)
    {
        AppIndexService.Launch(app);
    }

    public bool MoveAppSelection(int offset)
    {
        if (AppResults.Count == 0)
        {
            return false;
        }

        var currentIndex = SelectedApp is null ? -1 : AppResults.IndexOf(SelectedApp);
        if (currentIndex < 0)
        {
            currentIndex = offset > 0 ? 0 : AppResults.Count - 1;
        }

        var nextIndex = Math.Clamp(currentIndex + offset, 0, AppResults.Count - 1);
        SelectedApp = AppResults[nextIndex];
        return true;
    }

    public bool MoveProviderSelection(int offset)
    {
        var providerItems = GetProviderItems();
        if (providerItems.Count == 0)
        {
            return false;
        }

        var currentIndex = SelectedApp is null ? -1 : providerItems.IndexOf(SelectedApp);
        if (currentIndex < 0)
        {
            currentIndex = offset > 0 ? 0 : providerItems.Count - 1;
        }

        var nextIndex = Math.Clamp(currentIndex + offset, 0, providerItems.Count - 1);
        SelectedApp = providerItems[nextIndex];
        return true;
    }

    public bool MoveProviderUpOrBackToApps()
    {
        var providerItems = GetProviderItems();
        if (providerItems.Count == 0)
        {
            return false;
        }

        var currentIndex = SelectedApp is null ? -1 : providerItems.IndexOf(SelectedApp);
        if (currentIndex <= 0)
        {
            var lastApp = AppResults.LastOrDefault();
            if (lastApp is null)
            {
                return false;
            }

            SelectedApp = lastApp;
            return true;
        }

        SelectedApp = providerItems[currentIndex - 1];
        return true;
    }

    public bool MoveFromAppsToProviders()
    {
        var firstProvider = GetProviderItems().FirstOrDefault();
        if (firstProvider is null)
        {
            return false;
        }

        SelectedApp = firstProvider;
        return true;
    }

    public bool IsSelectedInApps()
    {
        return SelectedApp is not null && AppResults.Contains(SelectedApp);
    }

    public bool IsSelectedInProviders()
    {
        return SelectedApp is not null && GetProviderItems().Contains(SelectedApp);
    }

    private void ApplyFilter()
    {
        var query = SearchText ?? string.Empty;
        var matches = allApps
            .Where(entry => Matches(entry, query))
            .OrderByDescending(entry => IsPrefixMatch(entry, query))
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
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

    public void SetProviderSearchLimit(int limit)
    {
        providerSearchLimit = Math.Max(1, limit);
    }

    public void SetLauncherVisible(bool isVisible)
    {
        launcherVisible = isVisible;
    }

    public async Task UpdateVisibleAndResolveAsync(double windowWidth)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            CancelPendingProviderSearch();
            uiDispatcher.Invoke(() =>
            {
                AppResults.Clear();
                ProviderSections.Clear();
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
            AppResults.Clear();
            foreach (var app in toShow)
            {
                AppResults.Add(app);
            }

            VisibleApps.Clear();
            foreach (var a in toShow)
                VisibleApps.Add(a);
            SelectedApp = VisibleApps.FirstOrDefault();
        });

        await ResolveAppIconsAsync(toShow);
        ScheduleProviderSearch(SearchText, providerSearchLimit);
    }

    private void ScheduleProviderSearch(string query, int limit)
    {
        CancelPendingProviderSearch();

        var generation = Interlocked.Increment(ref providerSearchGeneration);
        providerSearchCts = new CancellationTokenSource();
        providerSearchTimer = new System.Threading.Timer(async _ => await RunProviderSearchAsync(query, limit, generation), null, 200, Timeout.Infinite);
    }

    private void CancelPendingProviderSearch()
    {
        try
        {
            providerSearchCts?.Cancel();
            providerSearchCts?.Dispose();
        }
        catch { }

        providerSearchCts = null;

        try
        {
            providerSearchTimer?.Dispose();
        }
        catch { }

        providerSearchTimer = null;
    }

    private async Task RunProviderSearchAsync(string query, int limit, int generation)
    {
        var cts = providerSearchCts;
        if (cts is null)
        {
            return;
        }

        IReadOnlyList<ProviderCategoryResultUi> providerSections = [];
        try
        {
            providerSections = await providerSearchService.SearchAsync(query, limit, currentSettings, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch
        {
            providerSections = [];
        }

        if (generation != providerSearchGeneration || cts.IsCancellationRequested)
        {
            return;
        }

        uiDispatcher.Invoke(() =>
        {
            ProviderSections.Clear();
            foreach (var section in providerSections)
            {
                ProviderSections.Add(section);
            }

            VisibleApps.Clear();
            foreach (var app in AppResults)
            {
                VisibleApps.Add(app);
            }
            foreach (var section in providerSections)
            {
                foreach (var result in section.Items)
                {
                    VisibleApps.Add(result);
                }
            }
            SelectedApp = VisibleApps.FirstOrDefault();
        });

        if (imageResolver is null)
        {
            return;
        }

        await ResolveProviderIconsAsync(providerSections);
    }

    private async Task ResolveAppIconsAsync(IEnumerable<AppEntry> apps)
    {
        foreach (var app in apps)
        {
            if (app.IconImage is not null)
            {
                continue;
            }

            var key = app.IconPath ?? app.ExecutablePath;
            try
            {
                var img = await imageResolver!.ResolveAsync(key);
                if (img is not null)
                {
                    app.IconImage = img;
                }
            }
            catch { }
        }
    }

    private async Task ResolveProviderIconsAsync(IEnumerable<ProviderCategoryResultUi> sections)
    {
        foreach (var section in sections)
        {
            if (!string.IsNullOrWhiteSpace(section.IconPath) && section.IconImage is null)
            {
                try
                {
                    var icon = await imageResolver!.ResolveAsync(section.IconPath);
                    if (icon is not null)
                    {
                        section.IconImage = icon;
                    }
                }
                catch { }
            }

            foreach (var result in section.Items)
            {
                if (result.IconImage is not null || string.IsNullOrWhiteSpace(result.IconPath))
                {
                    continue;
                }

                try
                {
                    var icon = await imageResolver!.ResolveAsync(result.IconPath);
                    if (icon is not null)
                    {
                        result.IconImage = icon;
                    }
                }
                catch { }
            }
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

    private static bool Matches(AppEntry entry, string query)
    {
        return string.IsNullOrWhiteSpace(query)
            || entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.ExecutablePath.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPrefixMatch(AppEntry entry, string query)
    {
        return !string.IsNullOrWhiteSpace(query) && entry.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase);
    }

    private List<AppEntry> GetProviderItems()
    {
        return ProviderSections.SelectMany(section => section.Items).ToList();
    }

    public void Dispose()
    {
        providerSearchService.Dispose();
        refreshTimer?.Dispose();
        providerSearchTimer?.Dispose();
        providerSearchCts?.Dispose();
    }
}
