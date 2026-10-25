using FileIndexProvider.Database;

namespace FileIndexProvider.Database.Repositories;

public interface IDirectoryRepository
{
    Task<int> GetOrCreateDirectoryIdAsync(
        string directoryPath,
        FileIndexDbContext? existingContext = null,
        CancellationToken cancellationToken = default);
}
