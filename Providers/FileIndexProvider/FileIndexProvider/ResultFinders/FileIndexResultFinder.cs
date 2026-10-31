using System.IO.Abstractions;
using BaseProvider.Abstractions;
using FileIndexProvider.Database;
using FileIndexProvider.Models;
using FileIndexProvider.Services;
using FileIndexProvider.Settings;
using WindowsSearch.Common.Logging;
using WindowsSearch.Common.Models;

namespace FileIndexProvider.ResultFinders;

public sealed class FileIndexResultFinder : IResultFinder<FileIndexProviderSettings>, ICommandHandler<FileIndexProviderSettings>, IDisposable
{
    private readonly IFileSystem _fileSystem;
    private readonly FileDatabase _database;
    private readonly FileCrawlerService _crawler;
    private readonly FileWatcherService _watcher;
    private readonly FileIconResolver _iconResolver;
    private readonly FileSearchService _searchService;
    private readonly FileIndexCommandHandler _commandHandler;
    private bool _watcherStarted;

    public FileDatabase Database => _database;
    public FileCrawlerService Crawler => _crawler;

    public FileIndexResultFinder(
        IFileSystem? fileSystem = null,
        FileDatabase? database = null,
        FileCrawlerService? crawler = null,
        FileWatcherService? watcher = null,
        FileRankingService? ranking = null,
        FileIconResolver? iconResolver = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
        _database = database ?? new FileDatabase(fileSystem: _fileSystem);
        _crawler = crawler ?? new FileCrawlerService(_database, _fileSystem);
        _watcher = watcher ?? new FileWatcherService(_database, _fileSystem);
        _iconResolver = iconResolver ?? new FileIconResolver(_fileSystem);
        var rankingService = ranking ?? new FileRankingService();
        _searchService = new FileSearchService(_database.Search, rankingService);
        _commandHandler = new FileIndexCommandHandler(_crawler, _database.Files, _database.BuildHistory);
    }

    public async Task<ProviderSearchResponse> FindAsync(
        ProviderSearchRequest request,
        FileIndexProviderSettings settings,
        CancellationToken cancellationToken)
    {
        var response = new ProviderSearchResponse();
        EnsureWatcherStarted(settings);

        var query = request.Query?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
        {
            return response;
        }

        var limit = request.Limit > 0 ? request.Limit : 50;
        var ranked = await _searchService.SearchAsync(query, limit, cancellationToken);

        // If index is empty and not running, kick off background crawl
        if (ranked.Count == 0 && !_crawler.IsRunning)
        {
            var stats = await _database.GetStatsAsync(cancellationToken);
            if (stats.TotalFiles == 0)
            {
                AppLogger.Info("File index is empty on search request. Starting initial rebuild in background.");
                _crawler.StartRebuild(settings);
            }
        }

        var items = new List<ProviderResultItem>(ranked.Count);
        foreach (var record in ranked)
        {
            items.Add(new ProviderResultItem
            {
                Title = record.FileName,
                Subtitle = record.DirectoryPath,
                ActionPath = record.FullPath,
                IconPath = _iconResolver.ResolveIcon(record.FullPath, record.Extension, record.IsDirectory)
            });
        }

        if (items.Count > 0)
        {
            response.Categories.Add(new ProviderResultCategory
            {
                Name = "Files",
                IconPath = _iconResolver.GetCategoryIconPath(),
                Items = items
            });
        }

        return response;
    }

    public Task<ProviderCommandResult> ExecuteCommandAsync(
        ProviderCommand command,
        FileIndexProviderSettings settings,
        CancellationToken cancellationToken)
    {
        return _commandHandler.ExecuteCommandAsync(command, settings, cancellationToken);
    }

    private void EnsureWatcherStarted(FileIndexProviderSettings settings)
    {
        if (!_watcherStarted)
        {
            _watcherStarted = true;
            try
            {
                _watcher.Start(settings);
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Failed to start file watcher: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _database.Dispose();
    }
}
