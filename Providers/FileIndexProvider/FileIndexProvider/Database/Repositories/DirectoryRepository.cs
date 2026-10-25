using System.Collections.Concurrent;
using System.IO.Abstractions;
using FileIndexProvider.Models;
using Microsoft.EntityFrameworkCore;

namespace FileIndexProvider.Database.Repositories;

public sealed class DirectoryRepository : IDirectoryRepository
{
    private readonly Func<FileIndexDbContext> _contextFactory;
    private readonly IFileSystem _fileSystem;
    private readonly ConcurrentDictionary<string, int> _directoryCache = new(StringComparer.OrdinalIgnoreCase);

    public DirectoryRepository(Func<FileIndexDbContext> contextFactory, IFileSystem? fileSystem = null)
    {
        _contextFactory = contextFactory;
        _fileSystem = fileSystem ?? new FileSystem();
    }

    public async Task<int> GetOrCreateDirectoryIdAsync(
        string directoryPath,
        FileIndexDbContext? existingContext = null,
        CancellationToken cancellationToken = default)
    {
        if (_directoryCache.TryGetValue(directoryPath, out var cachedId))
        {
            return cachedId;
        }

        var context = existingContext ?? _contextFactory();
        var shouldDispose = existingContext == null;
        try
        {
            var existing = await context.Directories
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Path == directoryPath, cancellationToken);

            if (existing != null)
            {
                _directoryCache[directoryPath] = existing.Id;
                return existing.Id;
            }

            var dirName = _fileSystem.Path.GetFileName(directoryPath);
            if (string.IsNullOrEmpty(dirName))
            {
                dirName = directoryPath;
            }

            var newDir = new DirectoryEntity
            {
                Path = directoryPath,
                DirectoryName = dirName
            };
            context.Directories.Add(newDir);
            await context.SaveChangesAsync(cancellationToken);

            _directoryCache[directoryPath] = newDir.Id;
            return newDir.Id;
        }
        finally
        {
            if (shouldDispose)
            {
                await context.DisposeAsync();
            }
        }
    }

    public void ClearCache()
    {
        _directoryCache.Clear();
    }
}
