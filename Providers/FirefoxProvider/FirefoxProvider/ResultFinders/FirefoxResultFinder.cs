using Microsoft.Data.Sqlite;
using WindowsSearch.Common.Models;
using WebBaseProvider;
using WindowsSearch.Common.Logging;
using System.Diagnostics;
using FirefoxProvider.Settings;

namespace FirefoxProvider.ResultFinders;

public class FirefoxResultFinder : WebResultFinderBase<FirefoxProviderSettings>
{


    protected override string GetBrowserActionPath() => "firefox";
    protected override string GetCategoryIconPath() => "Icons/firefox.png";

    public override async Task<ProviderSearchResponse> FindAsync(ProviderSearchRequest request, FirefoxProviderSettings settings, CancellationToken cancellationToken)
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
            var bookmarksCategory = await GetLocalCategoryAsync(request, settings.ProfileName, "Bookmarks", "Icons/bookmark.png", true, localCts.Token);
            if (bookmarksCategory != null && bookmarksCategory.Items.Any())
                response.Categories.Add(bookmarksCategory);
            
            var historyCategory = await GetLocalCategoryAsync(request, settings.ProfileName, "History", "Icons/history.png", false, localCts.Token);
            if (historyCategory != null && historyCategory.Items.Any())
                response.Categories.Add(historyCategory);
        }

        var webCategory = await GetWebSearchCategoryAsync(request, settings, localCts.Token);
        if (webCategory != null && webCategory.Items.Any())
            response.Categories.Add(webCategory);

        return response;
    }



    private async Task<ProviderResultCategory?> GetLocalCategoryAsync(ProviderSearchRequest request, string profileName, string categoryName, string iconPath, bool isBookmarks, CancellationToken cancellationToken)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dbPath = Path.Combine(appData, "Mozilla", "Firefox", "Profiles", profileName, "places.sqlite");

        if (!File.Exists(dbPath))
            return null;


        var items = new List<ProviderResultItem>();
        var sw = Stopwatch.StartNew();
        AppLogger.Info($"Starting local query for {categoryName} for '{request.Query}'...");

        try
        {
            // Format as URI to pass immutable=1 which bypasses locks
            string uriPath = dbPath.Replace("\\", "/");
            using var connection = new SqliteConnection($"Data Source=file:{uriPath}?immutable=1;Mode=ReadOnly;");
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            
            if (isBookmarks)
            {
                command.CommandText = @"
                    SELECT b.title, p.url 
                    FROM moz_bookmarks b 
                    JOIN moz_places p ON b.fk = p.id 
                    WHERE p.hidden = 0 AND (b.title LIKE @q OR p.url LIKE @q)
                    ORDER BY p.frecency DESC
                    LIMIT @limit";
            }
            else
            {
                command.CommandText = @"
                    SELECT title, url 
                    FROM moz_places 
                    WHERE hidden = 0 
                      AND visit_count > 0 
                      AND (title LIKE @q OR url LIKE @q) 
                    ORDER BY frecency DESC 
                    LIMIT @limit";
            }

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
                    Title = title,
                    Subtitle = url,
                    ActionPath = GetBrowserActionPath(),
                    ActionArgs = [url],
                    IconPath = iconPath
                });
            }
            
            sw.Stop();
            AppLogger.Info($"Finished {categoryName} query in {sw.ElapsedMilliseconds}ms.");
        }
        catch (Exception ex)
        {
            sw.Stop();
            AppLogger.Error($"Error querying {categoryName} for '{request.Query}'", ex);
        }

        if (items.Count == 0) return null;

        return new ProviderResultCategory
        {
            Name = categoryName,
            IconPath = GetCategoryIconPath(),
            Items = items
        };
    }
}