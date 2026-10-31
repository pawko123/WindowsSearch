using System.Collections.Concurrent;
using System.IO;
using System.IO.Abstractions;
using System.Threading.Channels;
using FileIndexProvider.Database;
using FileIndexProvider.Models;
using FileIndexProvider.Settings;
using WindowsSearch.Common.Logging;

namespace FileIndexProvider.Services;

public sealed class FileWatcherService : IDisposable
{
    private readonly FileDatabase _database;
    private readonly IFileSystem _fileSystem;
    private readonly List<IFileSystemWatcher> _watchers = [];
    private readonly Channel<FileChangeEvent> _channel = Channel.CreateUnbounded<FileChangeEvent>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _cts = new();
    private Task? _consumerTask;
    private List<string> _excludedList = [];
    private int _syncIntervalSeconds = 60;
    private bool _isStarted;

    public FileWatcherService(FileDatabase database, IFileSystem? fileSystem = null)
    {
        _database = database;
        _fileSystem = fileSystem ?? new FileSystem();
    }

    public void Start(FileIndexProviderSettings settings)
    {
        if (_isStarted) return;
        _isStarted = true;
        _excludedList = settings.ExcludedDirectories ?? [];
        _syncIntervalSeconds = Math.Max(5, settings.WatcherSyncIntervalSeconds);
        if (!settings.EnableLiveWatcher)
        {
            AppLogger.Info("Live file watcher disabled in settings.");
            return;
        }

        foreach (var root in settings.IndexedDirectories)
        {
            if (!_fileSystem.Directory.Exists(root)) continue;

            try
            {
                var watcher = _fileSystem.FileSystemWatcher.New(root);
                watcher.IncludeSubdirectories = true;
                watcher.InternalBufferSize = 65536; // 64 KB (max OS buffer to prevent buffer overruns on drive roots)
                watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName;

                watcher.Created += OnCreated;
                watcher.Deleted += OnDeleted;
                watcher.Renamed += OnRenamed;
                watcher.Error += (s, e) => AppLogger.Warn($"FileSystemWatcher error on {root}: {e.GetException().Message}");

                watcher.EnableRaisingEvents = true;
                _watchers.Add(watcher);
                AppLogger.Info($"Started file watcher on: {root}");
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Failed to start file watcher on {root}: {ex.Message}");
            }
        }

        _consumerTask = Task.Run(ConsumeEventsAsync);
    }

    private void OnCreated(object sender, FileSystemEventArgs e) =>
        _channel.Writer.TryWrite(new FileChangeEvent { ChangeType = WatcherChangeTypes.Created, FullPath = e.FullPath });

    private void OnDeleted(object sender, FileSystemEventArgs e) =>
        _channel.Writer.TryWrite(new FileChangeEvent { ChangeType = WatcherChangeTypes.Deleted, FullPath = e.FullPath });

    private void OnRenamed(object sender, RenamedEventArgs e) =>
        _channel.Writer.TryWrite(new FileChangeEvent { ChangeType = WatcherChangeTypes.Renamed, FullPath = e.FullPath, OldFullPath = e.OldFullPath });

    private async Task ConsumeEventsAsync()
    {
        var token = _cts.Token;
        var pendingChanges = new ConcurrentDictionary<string, FileChangeEvent>(StringComparer.OrdinalIgnoreCase);

        while (!token.IsCancellationRequested)
        {
            try
            {
                // Wait for the sync interval (default 60s)
                var delayTask = Task.Delay(TimeSpan.FromSeconds(_syncIntervalSeconds), token);

                // Drain any incoming events during the interval into memory
                while (!delayTask.IsCompleted && !token.IsCancellationRequested)
                {
                    while (_channel.Reader.TryRead(out var evt))
                    {
                        pendingChanges[evt.FullPath] = evt;
                    }

                    var waitTask = _channel.Reader.WaitToReadAsync(token).AsTask();
                    var completed = await Task.WhenAny(delayTask, waitTask);
                    if (completed == waitTask && waitTask.Result)
                    {
                        while (_channel.Reader.TryRead(out var evt))
                        {
                            pendingChanges[evt.FullPath] = evt;
                        }
                    }
                }

                // Batch flush accumulated changes to database once per interval
                if (!pendingChanges.IsEmpty)
                {
                    var eventsToApply = pendingChanges.Values.ToList();
                    pendingChanges.Clear();

                    AppLogger.Info($"[FileWatcherService] Periodic flush: applying {eventsToApply.Count} buffered file system change(s)...");

                    foreach (var change in eventsToApply)
                    {
                        if (token.IsCancellationRequested) break;

                        try
                        {
                            await ProcessChangeEventAsync(change, token);
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Debug($"Failed to process change {change.ChangeType} for {change.FullPath}: {ex.Message}");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                AppLogger.Error("Watcher consumer loop error:", ex);
            }
        }
    }

    private async Task ProcessChangeEventAsync(FileChangeEvent change, CancellationToken token)
    {
        switch (change.ChangeType)
        {
            case WatcherChangeTypes.Deleted:
                await _database.DeleteFileAsync(change.FullPath, token);
                break;

            case WatcherChangeTypes.Renamed:
                if (!string.IsNullOrEmpty(change.OldFullPath))
                {
                    await _database.DeleteFileAsync(change.OldFullPath, token);
                }

                if (_fileSystem.File.Exists(change.FullPath))
                {
                    await UpsertPathAsync(change.FullPath, token);
                }
                else if (_fileSystem.Directory.Exists(change.FullPath))
                {
                    await HandleDirectoryChangeAsync(change.FullPath, token);
                }
                break;

            case WatcherChangeTypes.Created:
            case WatcherChangeTypes.Changed:
                if (_fileSystem.File.Exists(change.FullPath))
                {
                    await UpsertPathAsync(change.FullPath, token);
                }
                else if (_fileSystem.Directory.Exists(change.FullPath))
                {
                    await HandleDirectoryChangeAsync(change.FullPath, token);
                }
                break;
        }
    }

    private async Task UpsertPathAsync(string fullPath, CancellationToken token)
    {
        try
        {
            var fileName = _fileSystem.Path.GetFileName(fullPath);
            if (FileCrawlerService.IsFileExcluded(fileName, fullPath, _excludedList))
            {
                return;
            }

            var fileInfo = _fileSystem.FileInfo.New(fullPath);
            if (!fileInfo.Exists) return;
            var dir = fileInfo.DirectoryName ?? string.Empty;
            var record = new FileIndexRecord
            {
                DirectoryPath = dir,
                FileName = fileInfo.Name,
                Extension = fileInfo.Extension.ToLowerInvariant(),
                FileSizeBytes = fileInfo.Length,
                LastWriteTimeUtc = new DateTimeOffset(fileInfo.LastWriteTimeUtc).ToUnixTimeSeconds()
            };

            await _database.UpsertFileAsync(record, token);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or PathTooLongException or IOException)
        {
            // Ignore files temporarily locked or inaccessible
        }
    }

    private async Task HandleDirectoryChangeAsync(string dirPath, CancellationToken token)
    {
        try
        {
            var dirName = _fileSystem.Path.GetFileName(dirPath);
            if (string.IsNullOrEmpty(dirName)) dirName = dirPath;
            if (!FileCrawlerService.IsExcluded(dirName, dirPath, _excludedList))
            {
                await _database.Directories.GetOrCreateDirectoryIdAsync(dirPath, null, token);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or PathTooLongException or IOException)
        {
            // Ignore transient file system access errors
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        foreach (var watcher in _watchers)
        {
            try
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            catch { }
        }
        _watchers.Clear();
        _cts.Dispose();
        AppLogger.Info("[FileWatcherService] File watchers stopped and disposed.");
    }
}
