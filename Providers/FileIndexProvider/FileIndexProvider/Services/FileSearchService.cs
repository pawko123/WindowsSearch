using System.Diagnostics;
using FileIndexProvider.Database.Repositories;
using FileIndexProvider.Models;
using WindowsSearch.Common.Logging;

namespace FileIndexProvider.Services;

public sealed class FileSearchService
{
    private readonly IFileSearchRepository _searchRepository;
    private readonly FileRankingService _rankingService;

    public FileSearchService(IFileSearchRepository searchRepository, FileRankingService? rankingService = null)
    {
        _searchRepository = searchRepository;
        _rankingService = rankingService ?? new FileRankingService();
    }

    public async Task<IReadOnlyList<FileIndexRecord>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var sw = Stopwatch.StartNew();
        var candidateLimit = Math.Clamp(limit * 4, 100, 500);
        var candidates = await _searchRepository.SearchAsync(query, candidateLimit, cancellationToken);
        var ranked = _rankingService.Rank(query, candidates, limit);
        sw.Stop();

        AppLogger.Info($"[FileSearchService] Query '{query}': found {candidates.Count} candidates, returned {ranked.Count} ranked results in {sw.ElapsedMilliseconds} ms.");
        return ranked;
    }
}
