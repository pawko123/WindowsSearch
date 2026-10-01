using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using BaseProvider.Abstractions;
using VsCodeProvider.Settings;
using WindowsSearch.Common.Logging;
using Microsoft.Extensions.Caching.Memory;
using WindowsSearch.Common.Models;
using System.IO.Abstractions;
namespace VsCodeProvider.ResultFinders;

public sealed partial class VsCodeResultFinder : IResultFinder<VsCodeProviderSettings>
{
    private readonly IFileSystem _fileSystem;

    public VsCodeResultFinder(IFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
    }

    private static readonly MemoryCache Cache = new(new MemoryCacheOptions());
    private const string WorkspacesCacheKey = "VsCodeWorkspaces";
    private const string FilesCacheKey = "VsCodeFiles";

    public Task<ProviderSearchResponse> FindAsync(ProviderSearchRequest request, VsCodeProviderSettings settings, CancellationToken cancellationToken)
    {
        var workspaces = Cache.GetOrCreate(WorkspacesCacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(settings.CacheTtlMinutes);
            AppLogger.Info("Cache miss or expired for VS Code Workspaces. Rebuilding...");
            var items = ReadWorkspaces();
            AppLogger.Info($"Rebuilt VS Code Workspaces. Found {items.Count} items.");
            return items;
        }) ?? [];

        var files = Cache.GetOrCreate(FilesCacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(settings.CacheTtlMinutes);
            AppLogger.Info("Cache miss or expired for VS Code Recent Files. Rebuilding...");
            var items = ReadRecentFiles(settings.JumpListId);
            AppLogger.Info($"Rebuilt VS Code Recent Files. Found {items.Count} items.");
            return items;
        }) ?? [];

        var response = new ProviderSearchResponse();
        var query = request.Query ?? string.Empty;

        // Filter Workspaces
        var filteredWorkspaces = workspaces
            .Where(w => string.IsNullOrWhiteSpace(query) || w.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(request.Limit > 0 ? request.Limit : int.MaxValue)
            .ToList();

        if (filteredWorkspaces.Count > 0)
        {
            response.Categories.Add(new ProviderResultCategory
            {
                Name = "VS Code - Workspaces",
                IconPath = "Icons\\vscode.png",
                Items = filteredWorkspaces
            });
        }

        // Filter Files
        var filteredFiles = files
            .Where(f => string.IsNullOrWhiteSpace(query) || f.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(request.Limit > 0 ? request.Limit : int.MaxValue)
            .ToList();

        if (filteredFiles.Count > 0)
        {
            response.Categories.Add(new ProviderResultCategory
            {
                Name = "VS Code - Recent Files",
                IconPath = "Icons\\vscode.png",
                Items = filteredFiles
            });
        }

        return Task.FromResult(response);
    }


    private List<ProviderResultItem> ReadWorkspaces()
    {
        var items = new List<ProviderResultItem>();
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var storagePath = _fileSystem.Path.Combine(appData, "Code", "User", "globalStorage", "storage.json");

            if (!_fileSystem.File.Exists(storagePath)) return items;

            var json = _fileSystem.File.ReadAllText(storagePath);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("profileAssociations", out var profileAssociations) &&
                profileAssociations.TryGetProperty("workspaces", out var workspaces))
            {
                foreach (var property in workspaces.EnumerateObject())
                {
                    var uriString = property.Name;
                    var path = DecodeUriToPath(uriString);
                    if (!string.IsNullOrEmpty(path) && _fileSystem.Directory.Exists(path))
                    {
                        var title = _fileSystem.Path.GetFileName(path.TrimEnd('\\', '/'));
                        if (string.IsNullOrWhiteSpace(title)) title = path;

                        items.Add(new ProviderResultItem
                        {
                            Title = title,
                            Subtitle = path,
                            ActionPath = "code.cmd",
                            ActionArgs = [path],
                            IconPath = "Icons\\folder.png"
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to read VS Code workspaces", ex);
        }

        return items;
    }

    private string? DecodeUriToPath(string uri)
    {
        try
        {
            var decoded = Uri.UnescapeDataString(uri);
            if (decoded.StartsWith("file:///"))
            {
                decoded = decoded.Substring("file:///".Length);
            }
            // Convert forward slashes to backslashes
            decoded = decoded.Replace('/', '\\');
            return decoded;
        }
        catch
        {
            return null;
        }
    }

    private List<ProviderResultItem> ReadRecentFiles(string jumpListId)
    {
        var items = new List<ProviderResultItem>();
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var jumpListPath = _fileSystem.Path.Combine(appData, @"Microsoft\Windows\Recent\AutomaticDestinations", $"{jumpListId}.automaticDestinations-ms");

            if (!_fileSystem.File.Exists(jumpListPath)) return items;

            byte[] data = _fileSystem.File.ReadAllBytes(jumpListPath);
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Try ASCII
            var asciiPaths = AsciiPathRegex().Matches(System.Text.Encoding.ASCII.GetString(data));
            foreach (Match match in asciiPaths)
            {
                var clean = match.Value.TrimEnd('\0');
                if (IsValidPath(clean)) paths.Add(clean);
            }

            // Try Unicode
            var unicodePaths = UnicodePathRegex().Matches(System.Text.Encoding.Unicode.GetString(data));
            foreach (Match match in unicodePaths)
            {
                var clean = match.Value.TrimEnd('\0');
                if (IsValidPath(clean)) paths.Add(clean);
            }

            foreach (var path in paths)
            {
                items.Add(new ProviderResultItem
                {
                    Title = _fileSystem.Path.GetFileName(path),
                    Subtitle = path,
                    ActionPath = "code.cmd",
                    ActionArgs = [path],
                    IconPath = "Icons\\file.png"
                });
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to read VS Code recent files", ex);
        }

        return items;
    }

    private bool IsValidPath(string cleanPath)
    {
        if (cleanPath.Contains('?') || cleanPath.Contains('*') || cleanPath.Contains('<') || cleanPath.Contains('>')) return false;
        
        if (cleanPath.Length <= 3 || !cleanPath.Contains('\\')) return false;

        return _fileSystem.File.Exists(cleanPath) || _fileSystem.Directory.Exists(cleanPath);
    }

    [GeneratedRegex(@"[A-Za-z]:\\[^\x00]+")]
    private static partial Regex AsciiPathRegex();

    [GeneratedRegex(@"[A-Za-z]:\\[^\x00]+")]
    private static partial Regex UnicodePathRegex();
}