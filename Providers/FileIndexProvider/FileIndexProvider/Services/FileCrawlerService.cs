using System.Diagnostics;
using System.IO.Abstractions;
using FileIndexProvider.Database;
using FileIndexProvider.Models;
using FileIndexProvider.Settings;
using WindowsSearch.Common.Logging;

namespace FileIndexProvider.Services;

public sealed class FileCrawlerService
{
    private readonly FileDatabase _database;
    private readonly IFileSystem _fileSystem;
    private readonly object _lock = new();
    private CancellationTokenSource? _activeCts;

    public bool IsRunning { get; private set; }
    public long FilesIndexed { get; private set; }
    public string CurrentPath { get; private set; } = string.Empty;
    public long ElapsedMs { get; private set; }

    public FileCrawlerService(FileDatabase database, IFileSystem? fileSystem = null)
    {
        _database = database;
        _fileSystem = fileSystem ?? new FileSystem();
    }

    public bool StartRebuild(FileIndexProviderSettings settings)
    {
        lock (_lock)
        {
            if (IsRunning)
            {
                return false;
            }

            _activeCts?.Dispose();
            _activeCts = new CancellationTokenSource();
            var token = _activeCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await RebuildAsync(settings, token);
                }
                catch (OperationCanceledException)
                {
                    AppLogger.Info("File crawl cancelled.");
                }
                catch (Exception ex)
                {
                    AppLogger.Error("File crawl encountered an error:", ex);
                }
            }, token);

            return true;
        }
    }

    public void Cancel()
    {
        lock (_lock)
        {
            _activeCts?.Cancel();
        }
    }

    public async Task RebuildAsync(FileIndexProviderSettings settings, CancellationToken cancellationToken, Action<long, string>? onProgress = null)
    {
        lock (_lock)
        {
            IsRunning = true;
            FilesIndexed = 0;
            CurrentPath = string.Empty;
            ElapsedMs = 0;
        }

        var buildId = 0L;
        var sw = Stopwatch.StartNew();
        var status = "Completed";
        try
        {
            AppLogger.Info("Starting file index rebuild. Clearing existing database...");
            await _database.ClearAllAsync(cancellationToken);
            buildId = await _database.StartBuildHistoryAsync(cancellationToken);
            var batch = new List<FileIndexRecord>(5000);
            var queue = new Queue<string>();

            foreach (var root in settings.IndexedDirectories)
            {
                if (_fileSystem.Directory.Exists(root))
                {
                    queue.Enqueue(root);
                }
                else
                {
                    AppLogger.Warn($"Indexed directory does not exist, skipping: {root}");
                }
            }

            var excludedList = settings.ExcludedDirectories ?? [];

            while (queue.Count > 0)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var dirPath = queue.Dequeue();
                CurrentPath = dirPath;

                // Ensure directory is registered in database even if it has no loose files
                try
                {
                    await _database.Directories.GetOrCreateDirectoryIdAsync(dirPath, null, cancellationToken);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or PathTooLongException or IOException)
                {
                    continue;
                }

                // Process files in directory
                try
                {
                    var dirInfo = _fileSystem.DirectoryInfo.New(dirPath);

                    // Skip reparse points (symlinks, junction points)
                    if ((dirInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }

                    IFileInfo[] files;
                    try
                    {
                        files = dirInfo.GetFiles();
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or PathTooLongException or IOException)
                    {
                        continue;
                    }

                    foreach (var file in files)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        if (IsFileExcluded(file.Name, file.FullName, excludedList))
                        {
                            continue;
                        }
                        batch.Add(new FileIndexRecord
                        {
                            DirectoryPath = dirPath,
                            FileName = file.Name,
                            Extension = file.Extension.ToLowerInvariant(),
                            FileSizeBytes = file.Length,
                            LastWriteTimeUtc = new DateTimeOffset(file.LastWriteTimeUtc).ToUnixTimeSeconds()
                        });

                        if (batch.Count >= 5000)
                        {
                            await _database.InsertBatchAsync(batch, cancellationToken);
                            FilesIndexed += batch.Count;
                            batch.Clear();
                            ElapsedMs = sw.ElapsedMilliseconds;
                            onProgress?.Invoke(FilesIndexed, dirPath);
                        }
                    }

                    // Enqueue subdirectories
                    IDirectoryInfo[] subDirs;
                    try
                    {
                        subDirs = dirInfo.GetDirectories();
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or PathTooLongException or IOException)
                    {
                        continue;
                    }

                    foreach (var subDir in subDirs)
                    {
                        if ((subDir.Attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            continue;
                        }

                        if (IsExcluded(subDir.Name, subDir.FullName, excludedList))
                        {
                            continue;
                        }

                        queue.Enqueue(subDir.FullName);
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or PathTooLongException or IOException)
                {
                    // Skip inaccessible directories
                }
            }

            // Flush remaining files in batch
            if (batch.Count > 0 && !cancellationToken.IsCancellationRequested)
            {
                await _database.InsertBatchAsync(batch, cancellationToken);
                FilesIndexed += batch.Count;
                batch.Clear();
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                AppLogger.Info("Optimizing SQLite FTS5 indexes after rebuild...");
                await _database.OptimizeFtsAsync(cancellationToken);
            }
            if (cancellationToken.IsCancellationRequested)
            {
                status = "Cancelled";
            }

            sw.Stop();
            ElapsedMs = sw.ElapsedMilliseconds;
            AppLogger.Info($"Rebuild completed with status '{status}' in {ElapsedMs} ms. Indexed {FilesIndexed} files.");
            onProgress?.Invoke(FilesIndexed, "Done");
        }
        catch (OperationCanceledException)
        {
            status = "Cancelled";
            throw;
        }
        catch (Exception)
        {
            status = "Failed";
            throw;
        }
        finally
        {
            sw.Stop();
            ElapsedMs = sw.ElapsedMilliseconds;
            if (buildId > 0)
            {
                try
                {
                    await _database.CompleteBuildHistoryAsync(buildId, FilesIndexed, ElapsedMs, status);
                }
                catch { }
            }

            lock (_lock)
            {
                IsRunning = false;
            }
        }
    }
    public static bool IsExcluded(string dirName, string fullPath, IReadOnlyList<string> excludedList)
    {
        foreach (var excluded in excludedList)
        {
            if (string.IsNullOrWhiteSpace(excluded)) continue;

            var pattern = excluded.Trim();

            // Direct directory name match
            if (string.Equals(dirName, pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Path segment match
            if (fullPath.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Wildcard matching on directory name (e.g. *.tmp, test*, cache_?)
            if (pattern.Contains('*') || pattern.Contains('?'))
            {
                if (System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(pattern, dirName, ignoreCase: true))
                {
                    return true;
                }
            }
        }

        return false;
    }
    public static bool IsFileExcluded(string fileName, string fullPath, IReadOnlyList<string> excludedList)
    {
        foreach (var excluded in excludedList)
        {
            if (string.IsNullOrWhiteSpace(excluded)) continue;

            var pattern = excluded.Trim();

            // Direct file name match
            if (string.Equals(fileName, pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Extension match if pattern starts with dot e.g. ".lnk"
            if (pattern.StartsWith('.') && fileName.EndsWith(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Path segment match (e.g. "Recent" or "Windows\Recent")
            if (fullPath.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Wildcard matching on file name (e.g. *.lnk, *.tmp, ~*.*)
            if (pattern.Contains('*') || pattern.Contains('?'))
            {
                if (System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(pattern, fileName, ignoreCase: true))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
