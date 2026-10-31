using System.IO.Abstractions;
using FileIndexProvider.Database.Repositories;
using FileIndexProvider.Models;

namespace FileIndexProvider.Database;

public sealed class FileDatabase : IDisposable
{
    private readonly string _dbPath;

    public IFileRepository Files { get; }
    public IFileSearchRepository Search { get; }
    public IBuildHistoryRepository BuildHistory { get; }
    public IDirectoryRepository Directories { get; }

    public string DatabasePath => _dbPath;

    public FileDatabase(string? dbPath = null, IFileSystem? fileSystem = null)
    {
        var fs = fileSystem ?? new FileSystem();
        _dbPath = dbPath ?? fs.Path.Combine(AppContext.BaseDirectory, "cache", "file_index.db");
        FileDatabaseInitializer.Initialize(_dbPath, fs);

        Func<FileIndexDbContext> contextFactory = () => new FileIndexDbContext(_dbPath);

        var dirRepo = new DirectoryRepository(contextFactory, fs);
        var historyRepo = new BuildHistoryRepository(contextFactory);
        var fileRepo = new FileRepository(contextFactory, dirRepo, historyRepo, _dbPath, fs);
        var searchRepo = new FileSearchRepository(contextFactory, fs);

        Directories = dirRepo;
        BuildHistory = historyRepo;
        Files = fileRepo;
        Search = searchRepo;
    }

    public Task InsertBatchAsync(IReadOnlyList<FileIndexRecord> batch, CancellationToken cancellationToken = default)
        => Files.InsertBatchAsync(batch, cancellationToken);

    public Task<List<FileIndexRecord>> SearchAsync(string query, int candidateLimit, CancellationToken cancellationToken = default)
        => Search.SearchAsync(query, candidateLimit, cancellationToken);

    public Task UpsertFileAsync(FileIndexRecord record, CancellationToken cancellationToken = default)
        => Files.UpsertFileAsync(record, cancellationToken);

    public Task DeleteFileAsync(string fullPath, CancellationToken cancellationToken = default)
        => Files.DeleteFileAsync(fullPath, cancellationToken);

    public Task RecordAccessAsync(int fileId, CancellationToken cancellationToken = default)
        => Files.RecordAccessAsync(fileId, cancellationToken);

    public Task ClearAllAsync(CancellationToken cancellationToken = default)
        => Files.ClearAllAsync(cancellationToken);

    public Task<DatabaseStats> GetStatsAsync(CancellationToken cancellationToken = default)
        => Files.GetStatsAsync(cancellationToken);
    public Task OptimizeFtsAsync(CancellationToken cancellationToken = default)
        => Files.OptimizeFtsAsync(cancellationToken);

    public Task<long> StartBuildHistoryAsync(CancellationToken cancellationToken = default)
        => BuildHistory.StartBuildAsync(cancellationToken);

    public Task CompleteBuildHistoryAsync(long buildId, long filesIndexed, long elapsedMs, string status, CancellationToken cancellationToken = default)
        => BuildHistory.CompleteBuildAsync(buildId, filesIndexed, elapsedMs, status, cancellationToken);

    public Task<BuildHistoryEntity?> GetLastCompletedBuildAsync(CancellationToken cancellationToken = default)
        => BuildHistory.GetLastCompletedBuildAsync(cancellationToken);

    public void Dispose()
    {
    }
}
