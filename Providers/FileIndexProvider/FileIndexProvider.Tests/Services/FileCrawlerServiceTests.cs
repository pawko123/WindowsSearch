using System.IO.Abstractions.TestingHelpers;
using FileIndexProvider.Database;
using FileIndexProvider.Services;
using FileIndexProvider.Settings;

namespace FileIndexProvider.Tests.Services;

public sealed class FileCrawlerServiceTests
{
    [Theory]
    [InlineData("GENKI 1 .pdf.lnk", @"C:\Users\Pawel\AppData\Roaming\Microsoft\Windows\Recent\GENKI 1 .pdf.lnk", true)]
    [InlineData("GENKI 1 ワークブック .pdf.lnk", @"C:\Users\Pawel\AppData\Roaming\Microsoft\Windows\Recent\GENKI 1 ワークブック .pdf.lnk", true)]
    [InlineData("GENKI 1 .pdf", @"C:\Users\Pawel\Downloads\GENKI 1 .pdf", false)]
    [InlineData("GENKI 1 ワークブック .pdf", @"C:\Users\Pawel\Downloads\GENKI 1 ワークブック .pdf", false)]
    [InlineData("app.exe", @"C:\Program Files\App\app.exe", false)]
    [InlineData("shortcut.lnk", @"C:\Desktop\shortcut.lnk", true)]
    public void IsFileExcluded_MatchesLnkWildcard(string fileName, string fullPath, bool expected)
    {
        var excludedList = new[] { "*.lnk" };

        var result = FileCrawlerService.IsFileExcluded(fileName, fullPath, excludedList);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("any.file", @"C:\Users\Pawel\AppData\Roaming\Microsoft\Windows\Recent\any.file", true)]
    [InlineData("secret.txt", @"C:\Users\Pawel\AppData\Local\Temp\secret.txt", true)]
    [InlineData("normal.txt", @"C:\Users\Pawel\Documents\normal.txt", false)]
    public void IsFileExcluded_MatchesPathSegment(string fileName, string fullPath, bool expected)
    {
        var excludedList = new[]
        {
            @"AppData\Roaming\Microsoft\Windows\Recent",
            @"AppData\Local\Temp"
        };

        var result = FileCrawlerService.IsFileExcluded(fileName, fullPath, excludedList);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(".git", @"C:\Project\.git", true)]
    [InlineData("node_modules", @"C:\Project\node_modules", true)]
    [InlineData("src", @"C:\Project\src", false)]
    public void IsExcluded_DirectoryMatches(string dirName, string fullPath, bool expected)
    {
        var excludedList = new[] { ".git", "node_modules", "bin", "obj" };

        var result = FileCrawlerService.IsExcluded(dirName, fullPath, excludedList);

        Assert.Equal(expected, result);
    }
    [Fact]
    public async Task RebuildAsync_WithMockFileSystem_CrawlsFilesAndSkipsExcluded()
    {
        var mockFs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            [@"C:\Data\Document.pdf"] = new("content"),
            [@"C:\Data\Sub\Code.cs"] = new("content"),
            [@"C:\Data\.git\HEAD"] = new("ref: refs/heads/main"),
            [@"C:\Data\Sub\Shortcut.lnk"] = new("shortcut")
        });

        var realDbPath = Path.Combine(Path.GetTempPath(), $"crawler_test_{Guid.NewGuid():N}.db");
        using var db = new FileDatabase(realDbPath);

        var crawler = new FileCrawlerService(db, mockFs);
        var settings = new FileIndexProviderSettings
        {
            IndexedDirectories = [@"C:\Data"],
            ExcludedDirectories = [".git", "*.lnk"]
        };

        await crawler.RebuildAsync(settings, CancellationToken.None);

        Assert.Equal(2, crawler.FilesIndexed);

        var stats = await db.GetStatsAsync();
        Assert.Equal(2, stats.TotalFiles);
    }
}
