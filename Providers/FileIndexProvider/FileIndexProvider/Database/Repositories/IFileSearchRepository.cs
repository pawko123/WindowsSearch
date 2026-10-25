using FileIndexProvider.Models;

namespace FileIndexProvider.Database.Repositories;

public interface IFileSearchRepository
{
    Task<List<FileIndexRecord>> SearchAsync(string query, int candidateLimit, CancellationToken cancellationToken = default);
}
