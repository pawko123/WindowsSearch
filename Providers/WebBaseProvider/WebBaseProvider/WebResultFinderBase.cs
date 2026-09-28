using System.Net.Http.Json;
using System.Text.Json;
using BaseProvider.Abstractions;
using WindowsSearch.Common.Logging;
using WindowsSearch.Common.Models;
using WebBaseProvider.Models;
using WebBaseProvider.Settings;
using System.Diagnostics;

namespace WebBaseProvider;

public abstract class WebResultFinderBase<TSettings> : IResultFinder<TSettings> where TSettings : WebBaseProviderSettings, new()
{
    private static readonly HttpClient _httpClient = new HttpClient();

    static WebResultFinderBase()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }

    public abstract Task<ProviderSearchResponse> FindAsync(ProviderSearchRequest request, TSettings settings, CancellationToken cancellationToken);
    
    protected abstract string GetCategoryIconPath();
    
    protected abstract string GetBrowserActionPath();

    protected async Task<ProviderResultCategory?> GetWebSearchCategoryAsync(ProviderSearchRequest request, TSettings settings, CancellationToken cancellationToken)
    {
        if (!settings.EnableSearch || string.IsNullOrWhiteSpace(request.Query))
            return null;
        var items = new List<ProviderResultItem>();
        int apiLimit = Math.Max(0, request.Limit - 1);
        var (suggestFormat, searchFormat) = GetEngineUrlFormats(settings.Engine);
        
        if (apiLimit > 0)
        {
            var sw = Stopwatch.StartNew();
            AppLogger.Info($"Starting web search query for '{request.Query}' using {settings.Engine}...");

            try
            {
                string suggestUrl = string.Format(suggestFormat, Uri.EscapeDataString(request.Query));
                
                var response = await _httpClient.GetAsync(suggestUrl, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    var result = JsonSerializer.Deserialize<OpenSearchResponse>(json);
                    
                    if (result != null && result.Suggestions.Any())
                    {
                        foreach (var suggestionText in result.Suggestions.Take(apiLimit))
                        {
                            items.Add(CreateSearchItem(suggestionText, settings.Engine, searchFormat));
                        }
                    }
                }
                
                sw.Stop();
                AppLogger.Info($"Finished web search query in {sw.ElapsedMilliseconds}ms.");
            }
            catch (Exception ex)
            {
                sw.Stop();
                AppLogger.Error($"Error executing web search query for '{request.Query}'", ex);
            }
        }
        // Always append the final fallback item
        items.Add(CreateSearchItem(request.Query, settings.Engine, searchFormat, isFallback: true));

        return new ProviderResultCategory
        {
            Name = "Web Search",
            IconPath = GetCategoryIconPath(),
            Items = items
        };
    }

    private ProviderResultItem CreateSearchItem(string text, SearchEngine engine, string searchFormat, bool isFallback = false)
    {
        string actionUrl = string.Format(searchFormat, Uri.EscapeDataString(text));

        return new ProviderResultItem
        {
            Title = isFallback ? $"Search for '{text}'" : text,
            Subtitle = $"Search {engine}",
            Score = isFallback ? 50 : 100,
            ActionPath = GetBrowserActionPath(),
            ActionArgs = [actionUrl],
            IconPath = "Icons/web.png"
        };
    }

    private static (string SuggestFormat, string SearchFormat) GetEngineUrlFormats(SearchEngine engine) => engine switch
    {
        SearchEngine.Google => ("https://suggestqueries.google.com/complete/search?client=firefox&q={0}", "https://www.google.com/search?q={0}"),
        SearchEngine.Bing => ("https://api.bing.com/osjson.aspx?query={0}", "https://www.bing.com/search?q={0}"),
        SearchEngine.DuckDuckGo => ("https://duckduckgo.com/ac/?q={0}&type=list", "https://duckduckgo.com/?q={0}"),
        SearchEngine.Ecosia => ("https://ac.ecosia.org/autocomplete?q={0}&type=list", "https://www.ecosia.org/search?q={0}"),
        SearchEngine.Brave => ("https://search.brave.com/api/suggest?q={0}", "https://search.brave.com/search?q={0}"),
        _ => ("https://suggestqueries.google.com/complete/search?client=firefox&q={0}", "https://www.google.com/search?q={0}")
    };
}