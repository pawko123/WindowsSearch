using FileIndexProvider.Database;
using FileIndexProvider.Models;
using FileIndexProvider.ResultFinders;
using FileIndexProvider.Services;
using FileIndexProvider.Settings;
using WindowsSearch.Common.Models;

namespace FileIndexProvider.Tests.ResultFinders;

public class FileIndexResultFinderTests : IDisposable
{
    private readonly string _testDir;
    private readonly FileDatabase _database;
    private readonly FileIndexResultFinder _finder;

    public FileIndexResultFinderTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"finder_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        var dbPath = Path.Combine(_testDir, "test.db");
        _database = new FileDatabase(dbPath);
        _finder = new FileIndexResultFinder(database: _database);
    }

    [Fact]
    public async Task FindAsync_ReturnsCategoriesWithResolvedIcons()
    {
        // Seed database
        var record = new FileIndexRecord
        {
            DirectoryPath = @"C:\Workspace\Project",
            FileName = "Program.cs",
            Extension = ".cs",
            FileSizeBytes = 2048,
            LastWriteTimeUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        await _database.InsertBatchAsync([record]);

        var settings = new FileIndexProviderSettings();
        var request = new ProviderSearchRequest { Query = "Program", Limit = 10 };

        var response = await _finder.FindAsync(request, settings, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Single(response.Categories);
        Assert.Equal("Files", response.Categories[0].Name);
        Assert.False(string.IsNullOrWhiteSpace(response.Categories[0].IconPath));
        Assert.Contains("category", response.Categories[0].IconPath);
        Assert.Single(response.Categories[0].Items);

        var item = response.Categories[0].Items[0];
        Assert.Equal("Program.cs", item.Title);
        Assert.Equal(@"C:\Workspace\Project", item.Subtitle);
        Assert.Equal(@"C:\Workspace\Project\Program.cs", item.ActionPath);
        Assert.Contains("csharp", item.IconPath);
    }

    [Fact]
    public async Task FindAsync_ReturnsMergedDirectories_WithFolderIcon()
    {
        var record = new FileIndexRecord
        {
            DirectoryPath = @"C:\Studia\magisterka",
            FileName = "thesis.docx",
            Extension = ".docx",
            FileSizeBytes = 10000,
            LastWriteTimeUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        await _database.InsertBatchAsync([record]);

        var settings = new FileIndexProviderSettings();
        var request = new ProviderSearchRequest { Query = "magisterka", Limit = 10 };

        var response = await _finder.FindAsync(request, settings, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Single(response.Categories);

        var folderItem = response.Categories[0].Items.FirstOrDefault(i => i.Title == "magisterka");
        Assert.NotNull(folderItem);
        Assert.Equal(@"C:\Studia", folderItem.Subtitle);
        Assert.Equal(@"C:\Studia\magisterka", folderItem.ActionPath);
        Assert.Contains("folder", folderItem.IconPath);
    }

    [Theory]
    [InlineData(nameof(FileIndexProviderAction.Rebuild), true)]
    [InlineData(nameof(FileIndexProviderAction.Sync), true)]
    [InlineData(nameof(FileIndexProviderAction.Status), true)]
    [InlineData(nameof(FileIndexProviderAction.Cancel), true)]
    [InlineData("rebuild", true)]
    [InlineData("unknown_cmd", false)]
    public async Task ExecuteCommandAsync_HandlesCommandsProperly(string action, bool expectedSuccess)
    {
        var settings = new FileIndexProviderSettings();
        var command = new ProviderCommand { Action = action };

        var result = await _finder.ExecuteCommandAsync(command, settings, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(expectedSuccess, result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    public void Dispose()
    {
        _finder.Dispose();
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
