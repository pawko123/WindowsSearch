using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using WindowsSearch.Common.Logging;
using WindowsSearch.Common.Models;
using WebBaseProvider;
using ChromeProvider.Settings;
using ChromeProvider.Models;

namespace ChromeProvider.ResultFinders;
public class ChromeResultFinder : WebResultFinderBase<ChromeProviderSettings>
{
    protected override string GetBrowserActionPath() => "chrome";
    protected override string GetCategoryIconPath() => "Icons/chrome.png";

    public override async Task<ProviderSearchResponse> FindAsync(ProviderSearchRequest request, ChromeProviderSettings settings, CancellationToken cancellationToken)
    {
        var response = new ProviderSearchResponse();
        
        if (string.IsNullOrWhiteSpace(request.Query))
            return response;

        using var localCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (request.TimeoutMs > 0)
        {
            int internalTimeout = Math.Max(50, request.TimeoutMs - 150);
            localCts.CancelAfter(internalTimeout);
        }

        if (!string.IsNullOrWhiteSpace(settings.ProfileName))
        {
            var bookmarksCategory = await GetBookmarksCategoryAsync(request, settings.ProfileName, localCts.Token);
            if (bookmarksCategory != null && bookmarksCategory.Items.Any())
                response.Categories.Add(bookmarksCategory);
            
            var historyCategory = await GetHistoryCategoryAsync(request, settings.ProfileName, localCts.Token);
            if (historyCategory != null && historyCategory.Items.Any())
                response.Categories.Add(historyCategory);
        }

        var webCategory = await GetWebSearchCategoryAsync(request, settings, localCts.Token);
        if (webCategory != null && webCategory.Items.Any())
            response.Categories.Add(webCategory);

        return response;
    }

    private async Task<ProviderResultCategory?> GetBookmarksCategoryAsync(ProviderSearchRequest request, string profileName, CancellationToken cancellationToken)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var profilePath = Path.Combine(localAppData, "Google", "Chrome", "User Data", profileName);
        
        string[] possibleFiles = [ "AccountBookmarks", "Bookmarks" ];
        string? targetFile = null;

        foreach (var file in possibleFiles)
        {
            var path = Path.Combine(profilePath, file);
            if (File.Exists(path))
            {
                targetFile = path;
                break; // Take the first one that exists (AccountBookmarks takes precedence if both exist)
            }
        }

        if (targetFile == null) return null;

        var items = new List<ProviderResultItem>();
        var sw = Stopwatch.StartNew();
        AppLogger.Info($"Starting local query for Chrome Bookmarks for '{request.Query}'...");

        try
        {
            using var stream = File.OpenRead(targetFile);
            var root = await JsonSerializer.DeserializeAsync<ChromeBookmarkRoot>(stream, cancellationToken: cancellationToken);

            if (root?.Roots != null)
            {
                foreach (var kvp in root.Roots)
                {
                    ExtractBookmarks(kvp.Value, request.Query, items, request.Limit);
                    if (items.Count >= request.Limit) break;
                }
            }

            sw.Stop();
            AppLogger.Info($"Finished Chrome Bookmarks query in {sw.ElapsedMilliseconds}ms.");
        }
        catch (Exception ex)
        {
            sw.Stop();
            AppLogger.Error($"Error querying Chrome Bookmarks for '{request.Query}'", ex);
        }

        if (items.Count == 0) return null;

        return new ProviderResultCategory
        {
            Name = "Bookmarks",
            IconPath = GetCategoryIconPath(),
            Items = items
        };
    }

    private void ExtractBookmarks(ChromeBookmarkNode? node, string query, List<ProviderResultItem> items, int limit)
    {
        if (node == null || items.Count >= limit) return;

        if (node.Type == "url" && !string.IsNullOrEmpty(node.Url) && !string.IsNullOrEmpty(node.Name))
        {
            if (node.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || 
                node.Url.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                items.Add(new ProviderResultItem
                {
                    Title = node.Name,
                    Subtitle = node.Url,
                    ActionPath = GetBrowserActionPath(),
                    ActionArgs = [node.Url],
                    IconPath = "Icons/bookmark.png"
                });
            }
        }

        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                if (items.Count >= limit) break;
                ExtractBookmarks(child, query, items, limit);
            }
        }
    }

    private async Task<ProviderResultCategory?> GetHistoryCategoryAsync(ProviderSearchRequest request, string profileName, CancellationToken cancellationToken)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dbPath = Path.Combine(localAppData, "Google", "Chrome", "User Data", profileName, "History");

        if (!File.Exists(dbPath)) return null;

        var items = new List<ProviderResultItem>();
        var sw = Stopwatch.StartNew();
        AppLogger.Info($"Starting local query for Chrome History for '{request.Query}'...");

        try
        {
            string uriPath = dbPath.Replace("\\", "/");
            using var connection = new SqliteConnection($"Data Source=file:{uriPath}?immutable=1;Mode=ReadOnly;");
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            
            command.CommandText = @"
                SELECT title, url 
                FROM urls 
                WHERE hidden = 0 
                  AND visit_count > 0 
                  AND (title LIKE @q OR url LIKE @q) 
                ORDER BY visit_count DESC 
                LIMIT @limit";

            command.Parameters.AddWithValue("@q", $"%{request.Query}%");
            command.Parameters.AddWithValue("@limit", Math.Max(1, request.Limit));

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var title = reader.IsDBNull(0) ? "Unknown" : reader.GetString(0);
                var url = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);

                if (string.IsNullOrEmpty(url)) continue;

                items.Add(new ProviderResultItem
                {
                    Title = string.IsNullOrWhiteSpace(title) ? url : title,
                    Subtitle = url,
                    ActionPath = GetBrowserActionPath(),
                    ActionArgs = [url],
                    IconPath = "Icons/history.png"
                });
            }
            
            sw.Stop();
            AppLogger.Info($"Finished Chrome History query in {sw.ElapsedMilliseconds}ms.");
        }
        catch (Exception ex)
        {
            sw.Stop();
            AppLogger.Error($"Error querying Chrome History for '{request.Query}'", ex);
        }

        if (items.Count == 0) return null;

        return new ProviderResultCategory
        {
            Name = "History",
            IconPath = GetCategoryIconPath(),
            Items = items
        };
    }
}