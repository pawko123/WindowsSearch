using FileIndexProvider.Database;
using FileIndexProvider.Models;

namespace FileIndexProvider.Tests.Database;

public class FileDatabaseTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _dbPath;
    private readonly FileDatabase _database;

    public FileDatabaseTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"filedb_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _dbPath = Path.Combine(_testDir, "test_index.db");
        _database = new FileDatabase(_dbPath);
    }

    [Fact]
    public async Task InsertBatch_10000Items_SearchesAndCalculatesStatsCorrectly()
    {
        var records = new List<FileIndexRecord>(10000);
        for (int i = 0; i < 10000; i++)
        {
            var dir = i < 5000 ? @"C:\Projects\Alpha" : @"C:\Projects\Beta";
            var name = i == 42 ? "special_target_file.cs" : $"file_{i:D5}.txt";
            records.Add(new FileIndexRecord
            {
                DirectoryPath = dir,
                FileName = name,
                Extension = Path.GetExtension(name),
                FileSizeBytes = 1024 + i,
                LastWriteTimeUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
        }

        await _database.InsertBatchAsync(records);

        var stats = await _database.GetStatsAsync();
        Assert.Equal(10000, stats.TotalFiles);
        Assert.Equal(2, stats.TotalDirectories);
        Assert.True(stats.DatabaseSizeBytes > 0);

        // Search for the specific file
        var searchResults = await _database.SearchAsync("special_target", 10);
        Assert.Single(searchResults);
        Assert.Equal("special_target_file.cs", searchResults[0].FileName);
        Assert.Equal(@"C:\Projects\Alpha", searchResults[0].DirectoryPath);

        // Increment access count
        var fileId = searchResults[0].Id;
        await _database.RecordAccessAsync(fileId);

        var refreshed = await _database.SearchAsync("special_target", 10);
        Assert.Single(refreshed);
        Assert.Equal(1, refreshed[0].AccessCount);
        Assert.True(refreshed[0].LastAccessedUtc > 0);

        // Clear all
        await _database.ClearAllAsync();
        var emptyStats = await _database.GetStatsAsync();
        Assert.Equal(0, emptyStats.TotalFiles);
    }

    [Fact]
    public async Task UpsertAndDelete_ModifiesIndexCorrectly()
    {
        var record = new FileIndexRecord
        {
            DirectoryPath = @"C:\Docs",
            FileName = "notes.md",
            Extension = ".md",
            FileSizeBytes = 500,
            LastWriteTimeUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        await _database.UpsertFileAsync(record);

        var found = await _database.SearchAsync("notes", 10);
        Assert.Single(found);
        Assert.Equal("notes.md", found[0].FileName);

        await _database.DeleteFileAsync(@"C:\Docs\notes.md");
        var afterDelete = await _database.SearchAsync("notes", 10);
        Assert.Empty(afterDelete);
    }

    [Fact]
    public async Task BuildHistory_TracksRunsAndRetrievesLatest()
    {
        var build1 = await _database.StartBuildHistoryAsync();
        Assert.True(build1 > 0);
        await _database.CompleteBuildHistoryAsync(build1, 1500, 3200, "Completed");

        var build2 = await _database.StartBuildHistoryAsync();
        await _database.CompleteBuildHistoryAsync(build2, 500, 1000, "Cancelled");

        var lastCompleted = await _database.GetLastCompletedBuildAsync();
        Assert.NotNull(lastCompleted);
        Assert.Equal(build1, lastCompleted.Id);
        Assert.Equal(1500, lastCompleted.FilesIndexed);
        Assert.Equal(3200, lastCompleted.ElapsedMs);
        Assert.Equal("Completed", lastCompleted.Status);
        Assert.Contains("1,500 files", lastCompleted.Summary);
    }
    [Fact]
    public async Task SearchAsync_Fts5TrigramSubstring_FindsMiddlePattern()
    {
        var records = new List<FileIndexRecord>
        {
            new() { DirectoryPath = @"C:\Books", FileName = "GENKI 1 .pdf", Extension = ".pdf", FileSizeBytes = 1000 },
            new() { DirectoryPath = @"C:\Books", FileName = "GENKI 1 Workbook.pdf", Extension = ".pdf", FileSizeBytes = 2000 },
            new() { DirectoryPath = @"C:\Code", FileName = "AppIndexService.cs", Extension = ".cs", FileSizeBytes = 3000 }
        };

        await _database.InsertBatchAsync(records);

        // Substring in the middle (trigram FTS5)
        var results = await _database.SearchAsync("Workbook", 10);
        Assert.Single(results);
        Assert.Equal("GENKI 1 Workbook.pdf", results[0].FileName);

        // Multi-character phrase search
        var genkiResults = await _database.SearchAsync("GENKI 1", 10);
        Assert.Equal(2, genkiResults.Count);

        // Short query prefix (<3 chars)
        var prefixResults = await _database.SearchAsync("App", 10);
        Assert.Single(prefixResults);
        Assert.Equal("AppIndexService.cs", prefixResults[0].FileName);
    }

    [Fact]
    public async Task SearchAsync_FindsDirectoryResults_WhenDirectoryMatches()
    {
        var records = new List<FileIndexRecord>
        {
            new() { DirectoryPath = @"C:\Studia\magisterka", FileName = "Rozdzial1.docx", Extension = ".docx", FileSizeBytes = 5000 },
            new() { DirectoryPath = @"C:\Users\Pawel\Downloads", FileName = "installer.exe", Extension = ".exe", FileSizeBytes = 10000 }
        };

        await _database.InsertBatchAsync(records);

        // Search for directory name
        var results = await _database.SearchAsync("magisterka", 10);
        Assert.Contains(results, r => r.IsDirectory && r.FileName == "magisterka" && r.DirectoryPath == @"C:\Studia");

        // Searching for parent path component "Studia" does NOT match the child directory "magisterka"
        var parentSearchResults = await _database.SearchAsync("Studia", 10);
        Assert.DoesNotContain(parentSearchResults, r => r.IsDirectory && r.FileName == "magisterka");
        // Search for file name
        var fileResults = await _database.SearchAsync("Rozdzial1", 10);
        Assert.Contains(fileResults, r => !r.IsDirectory && r.FileName == "Rozdzial1.docx");
    }

    [Fact]
    public async Task OptimizeFtsAsync_RunsSuccessfully()
    {
        var records = new List<FileIndexRecord>
        {
            new() { DirectoryPath = @"C:\Temp", FileName = "file.txt", Extension = ".txt", FileSizeBytes = 100 }
        };
        await _database.InsertBatchAsync(records);

        // Optimize should run without throwing
        await _database.OptimizeFtsAsync();

        var results = await _database.SearchAsync("file", 10);
        Assert.NotEmpty(results);
    }

    public void Dispose()
    {
        _database.Dispose();
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch { }
    }
}
