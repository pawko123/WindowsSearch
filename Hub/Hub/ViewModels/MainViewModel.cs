using System.ComponentModel;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.ObjectModel;
using WindowsSearch.Common.Logging;
using System.Runtime.CompilerServices;
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
    private static readonly MemoryCache Cache = new(new MemoryCacheOptions());
    private List<AppEntry> filteredApps = [];
    private readonly Dispatcher uiDispatcher;
    private AppEntry? selectedApp;
    private string searchText = string.Empty;
    private IImageResolver? imageResolver;
    private CancellationTokenSource? providerSearchCts;
    private int providerSearchGeneration;
    private int providerSearchLimit = 25;
    private AppSettings currentSettings;

    public MainViewModel(Dispatcher dispatcher, AppSettings settings)
    {
        uiDispatcher = dispatcher;
        currentSettings = settings;
        AppResults = new ObservableCollection<AppEntry>();
        ProviderSections = new ObservableCollection<ProviderCategoryResultUi>();
        VisibleApps = new ObservableCollection<AppEntry>();
        selectedApp = null;
    }

    private IReadOnlyList<AppEntry> GetAllApps()
    {
        return Cache.GetOrCreate("InstalledApps", entry =>
        {
            AppLogger.Info("Cache miss or expired for InstalledApps. Rebuilding...");
            var apps = appIndexService.GetInstalledApps().ToList();
            AppLogger.Info($"Rebuilt InstalledApps. Found {apps.Count} items.");
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(currentSettings.AppCacheTtlMinutes);
            return apps;
        }) ?? [];
    }


    public void ApplySettings(AppSettings settings)
    {
        currentSettings = settings;
    }

    public ObservableCollection<AppEntry> AppResults { get; }

    public ObservableCollection<ProviderCategoryResultUi> ProviderSections { get; }
    public ObservableCollection<ProviderSearchOutcome> PendingProviders { get; } = new();
    public IEnumerable<ProviderSearchOutcome> TopPendingProviders => PendingProviders.Take(3);


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


    public void ClearSearch()
    {
        SearchText = string.Empty;
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

    private void ApplyFilter()
    {
        var query = SearchText ?? string.Empty;
        var matches = GetAllApps()
            .Where(entry => Matches(entry, query))
            .OrderByDescending(entry => IsPrefixMatch(entry, query))
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        filteredApps = matches;
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

        var toShow = filteredApps.Take(maxVisible).ToList();
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

        var cts = new CancellationTokenSource();
        providerSearchCts = cts;
        var generation = Interlocked.Increment(ref providerSearchGeneration);
        _ = RunProviderSearchWithDelayAsync(query, limit, generation, currentSettings.Search.ProviderDebounceDelayMs, cts);
    }

    private async Task RunProviderSearchWithDelayAsync(string query, int limit, int generation, int delayMs, CancellationTokenSource cts)
    {
        try
        {
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (cts.IsCancellationRequested || Volatile.Read(ref providerSearchGeneration) != generation)
        {
            return;
        }

        await RunProviderSearchAsync(query, limit, generation, cts);
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
    }

    private async Task RunProviderSearchAsync(string query, int limit, int generation, CancellationTokenSource cts)
    {
        var initialPending = providerSearchService.GetActiveProviderOutcomes();
        
        if (imageResolver is not null)
        {
            foreach (var p in initialPending)
            {
                try
                {
                    var img = await imageResolver.ResolveAsync(p.IconPath);
                    p.IconImage = img;
                }
                catch (Exception ex) 
                { 
                    AppLogger.Error($"[MainViewModel] Failed to resolve initial icon for {p.ProviderName}", ex);
                }
            }
        }

        uiDispatcher.Invoke(() =>
        {
            ProviderSections.Clear();
            PendingProviders.Clear();
            foreach (var p in initialPending)
            {
                PendingProviders.Add(p);
            }
            OnPropertyChanged(nameof(TopPendingProviders));
            
            
            VisibleApps.Clear();
            foreach (var app in AppResults)
            {
                VisibleApps.Add(app);
            }
            SelectedApp = VisibleApps.FirstOrDefault();
        });

        try
        {
            await foreach (var outcome in providerSearchService.SearchAsync(query, limit, currentSettings, cts.Token))
            {
                if (Volatile.Read(ref providerSearchGeneration) != generation || cts.IsCancellationRequested)
                {
                    return;
                }

                uiDispatcher.Invoke(() =>
                {
                    var toRemove = PendingProviders.FirstOrDefault(p => p.ProviderName == outcome.ProviderName);
                    if (toRemove != null)
                    {
                        PendingProviders.Remove(toRemove);
                        OnPropertyChanged(nameof(TopPendingProviders));
                    }


                    foreach (var section in outcome.Categories)
                    {
                        ProviderSections.Add(section);
                        foreach (var result in section.Items)
                        {
                            VisibleApps.Add(result);
                        }
                    }
                    if (SelectedApp == null)
                    {
                        SelectedApp = VisibleApps.FirstOrDefault();
                    }
                });

                if (imageResolver is not null)
                {
                    await ResolveProviderIconsAsync(outcome.Categories);
                }
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            AppLogger.Error("[MainViewModel] Unexpected error during provider search stream.", ex);
        }
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
            catch (Exception ex) 
            { 
                AppLogger.Error($"[MainViewModel] Failed to resolve app icon for {app.Name}", ex);
            }
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
                catch (Exception ex)
                {
                    AppLogger.Error($"[MainViewModel] Failed to resolve category icon for {section.Name}", ex);
                }
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
                catch (Exception ex)
                {
                    AppLogger.Error($"[MainViewModel] Failed to resolve item icon for {result.Name}", ex);
                }
            }
        }
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
        providerSearchCts?.Dispose();
    }
}
