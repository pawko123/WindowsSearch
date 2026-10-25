using FileIndexProvider.Models;
using Microsoft.EntityFrameworkCore;
using WindowsSearch.Common.Logging;

namespace FileIndexProvider.Database.Repositories;

public sealed class BuildHistoryRepository : IBuildHistoryRepository
{
    private readonly Func<FileIndexDbContext> _contextFactory;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public BuildHistoryRepository(Func<FileIndexDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<long> StartBuildAsync(CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var context = _contextFactory();
            var entry = new BuildHistoryEntity
            {
                StartTimeUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Status = "Running"
            };
            context.BuildHistory.Add(entry);
            await context.SaveChangesAsync(cancellationToken);
            return entry.Id;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task CompleteBuildAsync(
        long buildId,
        long filesIndexed,
        long elapsedMs,
        string status,
        CancellationToken cancellationToken = default)
    {
        if (buildId <= 0) return;

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var context = _contextFactory();
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await context.BuildHistory
                .Where(h => h.Id == buildId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(h => h.EndTimeUtc, nowUnix)
                    .SetProperty(h => h.FilesIndexed, filesIndexed)
                    .SetProperty(h => h.ElapsedMs, elapsedMs)
                    .SetProperty(h => h.Status, status), cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<BuildHistoryEntity?> GetLastCompletedBuildAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = _contextFactory();
            return await context.BuildHistory
                .AsNoTracking()
                .Where(h => h.Status == "Completed")
                .OrderByDescending(h => h.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            AppLogger.Debug($"Failed to get last build history: {ex.Message}");
        }

        return null;
    }
}
