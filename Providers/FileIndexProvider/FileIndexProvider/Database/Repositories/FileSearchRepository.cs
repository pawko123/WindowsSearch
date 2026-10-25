using System.IO.Abstractions;
using FileIndexProvider.Models;
using Microsoft.EntityFrameworkCore;
using WindowsSearch.Common.Logging;

namespace FileIndexProvider.Database.Repositories;

public sealed class FileSearchRepository : IFileSearchRepository
{
    private readonly Func<FileIndexDbContext> _contextFactory;
    private readonly IFileSystem _fileSystem;

    public FileSearchRepository(Func<FileIndexDbContext> contextFactory, IFileSystem? fileSystem = null)
    {
        _contextFactory = contextFactory;
        _fileSystem = fileSystem ?? new FileSystem();
    }

    public async Task<List<FileIndexRecord>> SearchAsync(
        string query,
        int candidateLimit,
        CancellationToken cancellationToken = default)
    {
        var results = new List<FileIndexRecord>();
        if (string.IsNullOrWhiteSpace(query))
        {
            return results;
        }

        var trimmedQuery = query.Trim();
        await using var context = _contextFactory();

        // 1. Trigram indexing requires >= 3 characters. For 1-2 chars, use fast B-tree prefix match.
        if (trimmedQuery.Length < 3)
        {
            var prefixFiles = await context.Files
                .AsNoTracking()
                .Include(f => f.Directory)
                .Where(f => EF.Functions.Like(f.FileName, $"{trimmedQuery}%"))
                .Take(candidateLimit)
                .ToListAsync(cancellationToken);

            var prefixDirs = await context.Directories
                .AsNoTracking()
                .Where(d => EF.Functions.Like(d.DirectoryName, $"{trimmedQuery}%"))
                .Take(candidateLimit / 2)
                .ToListAsync(cancellationToken);

            results.AddRange(prefixFiles.Select(MapToRecord));
            results.AddRange(prefixDirs.Select(MapDirectoryToRecord));
            return results;
        }

        // 2. For >= 3 characters, query SQLite FTS5 trigram inverted index for both files and directories
        try
        {
            var sanitized = "\"" + trimmedQuery.Replace("\"", "\"\"") + "\"";
            var fileIds = await context.Database
                .SqlQuery<int>($"SELECT rowid AS Value FROM FilesFts WHERE FilesFts MATCH {sanitized} LIMIT {candidateLimit}")
                .ToListAsync(cancellationToken);

            var dirIds = await context.Database
                .SqlQuery<int>($"SELECT rowid AS Value FROM DirectoriesFts WHERE DirectoriesFts MATCH {sanitized} LIMIT {candidateLimit}")
                .ToListAsync(cancellationToken);

            if (fileIds.Count > 0)
            {
                var ftsFiles = await context.Files
                    .AsNoTracking()
                    .Include(f => f.Directory)
                    .Where(f => fileIds.Contains(f.Id))
                    .ToListAsync(cancellationToken);

                results.AddRange(ftsFiles.Select(MapToRecord));
            }

            if (dirIds.Count > 0)
            {
                var ftsDirs = await context.Directories
                    .AsNoTracking()
                    .Where(d => dirIds.Contains(d.Id))
                    .ToListAsync(cancellationToken);

                results.AddRange(ftsDirs.Select(MapDirectoryToRecord));
            }

            return results;
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"[FileSearchRepository] FTS5 search error: {ex.Message}");

            // Graceful fallback to prefix search if FTS5 table is temporarily unavailable
            var fallbackFiles = await context.Files
                .AsNoTracking()
                .Include(f => f.Directory)
                .Where(f => EF.Functions.Like(f.FileName, $"{trimmedQuery}%"))
                .Take(candidateLimit)
                .ToListAsync(cancellationToken);

            return fallbackFiles.Select(MapToRecord).ToList();
        }
    }

    private static FileIndexRecord MapToRecord(FileEntity entity)
    {
        return new FileIndexRecord
        {
            Id = entity.Id,
            DirectoryId = entity.DirectoryId,
            DirectoryPath = entity.Directory?.Path ?? string.Empty,
            FileName = entity.FileName,
            Extension = entity.Extension,
            FileSizeBytes = entity.FileSizeBytes,
            LastWriteTimeUtc = entity.LastWriteTimeUtc,
            AccessCount = entity.AccessCount,
            LastAccessedUtc = entity.LastAccessedUtc
        };
    }

    private FileIndexRecord MapDirectoryToRecord(DirectoryEntity entity)
    {
        var dirName = string.IsNullOrEmpty(entity.DirectoryName)
            ? _fileSystem.Path.GetFileName(entity.Path)
            : entity.DirectoryName;
        if (string.IsNullOrEmpty(dirName))
        {
            dirName = entity.Path;
        }
        var parentPath = _fileSystem.Path.GetDirectoryName(entity.Path) ?? string.Empty;

        return new FileIndexRecord
        {
            Id = entity.Id,
            DirectoryId = entity.Id,
            DirectoryPath = parentPath,
            FileName = dirName,
            Extension = string.Empty,
            IsDirectory = true
        };
    }
}
