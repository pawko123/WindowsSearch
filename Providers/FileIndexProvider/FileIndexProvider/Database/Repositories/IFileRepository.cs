using FileIndexProvider.Models;

namespace FileIndexProvider.Database.Repositories;

public interface IFileRepository
{
    Task InsertBatchAsync(IReadOnlyList<FileIndexRecord> batch, CancellationToken cancellationToken = default);
    Task UpsertFileAsync(FileIndexRecord record, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string fullPath, CancellationToken cancellationToken = default);
    Task RecordAccessAsync(int fileId, CancellationToken cancellationToken = default);
    Task ClearAllAsync(CancellationToken cancellationToken = default);
    Task<DatabaseStats> GetStatsAsync(CancellationToken cancellationToken = default);
    Task OptimizeFtsAsync(CancellationToken cancellationToken = default);
}
