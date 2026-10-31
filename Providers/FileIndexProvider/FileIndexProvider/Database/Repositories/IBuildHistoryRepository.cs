using FileIndexProvider.Models;

namespace FileIndexProvider.Database.Repositories;

public interface IBuildHistoryRepository
{
    Task<long> StartBuildAsync(CancellationToken cancellationToken = default);
    Task CompleteBuildAsync(long buildId, long filesIndexed, long elapsedMs, string status, CancellationToken cancellationToken = default);
    Task<BuildHistoryEntity?> GetLastCompletedBuildAsync(CancellationToken cancellationToken = default);
}
