using System.IO.Abstractions;
using FileIndexProvider.Models;
using Microsoft.EntityFrameworkCore;
using WindowsSearch.Common.Logging;

namespace FileIndexProvider.Database.Repositories;

public sealed class FileRepository : IFileRepository
{
    private readonly Func<FileIndexDbContext> _contextFactory;
    private readonly IDirectoryRepository _directoryRepository;
    private readonly IBuildHistoryRepository _buildHistoryRepository;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly string _dbPath;
    private readonly IFileSystem _fileSystem;
    public FileRepository(
        Func<FileIndexDbContext> contextFactory,
        IDirectoryRepository directoryRepository,
        IBuildHistoryRepository buildHistoryRepository,
        string dbPath,
        IFileSystem? fileSystem = null)
    {
        _contextFactory = contextFactory;
        _directoryRepository = directoryRepository;
        _buildHistoryRepository = buildHistoryRepository;
        _dbPath = dbPath;
        _fileSystem = fileSystem ?? new FileSystem();
    }

    public async Task InsertBatchAsync(IReadOnlyList<FileIndexRecord> batch, CancellationToken cancellationToken = default)
    {
        if (batch.Count == 0) return;

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var context = _contextFactory();

            var uniqueDirs = batch.Select(b => b.DirectoryPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var dir in uniqueDirs)
            {
                await _directoryRepository.GetOrCreateDirectoryIdAsync(dir, context, cancellationToken);
            }

            var entities = new List<FileEntity>(batch.Count);
            foreach (var record in batch)
            {
                var dirId = await _directoryRepository.GetOrCreateDirectoryIdAsync(record.DirectoryPath, context, cancellationToken);
                record.DirectoryId = dirId;

                entities.Add(new FileEntity
                {
                    DirectoryId = dirId,
                    FileName = record.FileName,
                    Extension = record.Extension,
                    FileSizeBytes = record.FileSizeBytes,
                    LastWriteTimeUtc = record.LastWriteTimeUtc,
                    AccessCount = record.AccessCount,
                    LastAccessedUtc = record.LastAccessedUtc
                });
            }

            context.Files.AddRange(entities);
            await context.SaveChangesAsync(cancellationToken);

            for (int i = 0; i < batch.Count; i++)
            {
                batch[i].Id = entities[i].Id;
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task UpsertFileAsync(FileIndexRecord record, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var context = _contextFactory();
            var dirId = await _directoryRepository.GetOrCreateDirectoryIdAsync(record.DirectoryPath, context, cancellationToken);
            record.DirectoryId = dirId;

            var existing = await context.Files
                .FirstOrDefaultAsync(f => f.DirectoryId == dirId && f.FileName == record.FileName, cancellationToken);

            if (existing != null)
            {
                existing.FileSizeBytes = record.FileSizeBytes;
                existing.LastWriteTimeUtc = record.LastWriteTimeUtc;
                record.Id = existing.Id;
            }
            else
            {
                var newEntity = new FileEntity
                {
                    DirectoryId = dirId,
                    FileName = record.FileName,
                    Extension = record.Extension,
                    FileSizeBytes = record.FileSizeBytes,
                    LastWriteTimeUtc = record.LastWriteTimeUtc,
                    AccessCount = record.AccessCount,
                    LastAccessedUtc = record.LastAccessedUtc
                };
                context.Files.Add(newEntity);
                await context.SaveChangesAsync(cancellationToken);
                record.Id = newEntity.Id;
            }

            await context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task DeleteFileAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        var dir = _fileSystem.Path.GetDirectoryName(fullPath) ?? string.Empty;
        var fileName = _fileSystem.Path.GetFileName(fullPath);
        if (string.IsNullOrEmpty(fileName)) return;

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var context = _contextFactory();
            var target = await context.Files
                .Include(f => f.Directory)
                .FirstOrDefaultAsync(f => f.Directory!.Path == dir && f.FileName == fileName, cancellationToken);

            if (target != null)
            {
                context.Files.Remove(target);
                await context.SaveChangesAsync(cancellationToken);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task RecordAccessAsync(int fileId, CancellationToken cancellationToken = default)
    {
        if (fileId <= 0) return;

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var context = _contextFactory();
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await context.Files
                .Where(f => f.Id == fileId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(f => f.AccessCount, f => f.AccessCount + 1)
                    .SetProperty(f => f.LastAccessedUtc, nowUnix), cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            if (_directoryRepository is DirectoryRepository dirRepo)
            {
                dirRepo.ClearCache();
            }

            await using var context = _contextFactory();
            await context.Files.ExecuteDeleteAsync(cancellationToken);
            await context.Directories.ExecuteDeleteAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<DatabaseStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var stats = new DatabaseStats();
        try
        {
            if (_fileSystem.File.Exists(_dbPath))
            {
                stats.DatabaseSizeBytes = _fileSystem.FileInfo.New(_dbPath).Length;
            }

            await using var context = _contextFactory();
            stats.TotalFiles = await context.Files.CountAsync(cancellationToken);
            stats.TotalDirectories = await context.Directories.CountAsync(cancellationToken);

            var lastBuild = await _buildHistoryRepository.GetLastCompletedBuildAsync(cancellationToken);
            if (lastBuild != null)
            {
                stats.LastFullScanUtc = lastBuild.Summary;
            }
        }
        catch (Exception ex)
        {
            AppLogger.Debug($"Failed to get stats: {ex.Message}");
        }

        return stats;
    }

    public async Task OptimizeFtsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = _contextFactory();
            await context.Database.ExecuteSqlRawAsync("INSERT INTO FilesFts(FilesFts) VALUES('optimize');", cancellationToken);
            await context.Database.ExecuteSqlRawAsync("INSERT INTO DirectoriesFts(DirectoriesFts) VALUES('optimize');", cancellationToken);
            AppLogger.Info("[FileRepository] SQLite FTS5 indexes optimized successfully.");
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"[FileRepository] FTS5 optimize skipped: {ex.Message}");
        }
    }
}
