using FileIndexProvider.Models;
using FileIndexProvider.Services;

namespace FileIndexProvider.Tests.Services;

public class FileRankingServiceTests
{
    private readonly FileRankingService _rankingService = new();

    [Fact]
    public void Rank_ExactMatch_RanksHigherThanSubstring()
    {
        var candidates = new List<FileIndexRecord>
        {
            new() { FileName = "my_test_runner.cs", DirectoryPath = @"C:\Src" },
            new() { FileName = "test.cs", DirectoryPath = @"C:\Src" },
            new() { FileName = "latest_test_results.log", DirectoryPath = @"C:\Src" }
        };

        var ranked = _rankingService.Rank("test", candidates, 10);

        Assert.Equal(3, ranked.Count);
        Assert.Equal("test.cs", ranked[0].FileName);
    }

    [Fact]
    public void Rank_AccessedFile_RanksHigherDueToFrecency()
    {
        var candidates = new List<FileIndexRecord>
        {
            new()
            {
                FileName = "config_helper.py",
                DirectoryPath = @"C:\App",
                AccessCount = 0
            },
            new()
            {
                FileName = "config_loader.py",
                DirectoryPath = @"C:\App",
                AccessCount = 5 // Frecency bonus
            }
        };

        var ranked = _rankingService.Rank("config", candidates, 10);

        Assert.Equal(2, ranked.Count);
        Assert.Equal("config_loader.py", ranked[0].FileName);
    }

    [Fact]
    public void Rank_WordBoundary_RanksHigherThanInteriorSubstring()
    {
        var candidates = new List<FileIndexRecord>
        {
            new() { FileName = "mySearchProvider.cs", DirectoryPath = @"C:\App" },
            new() { FileName = "deep_research_log.txt", DirectoryPath = @"C:\App" }
        };

        var ranked = _rankingService.Rank("Search", candidates, 10);

        Assert.Equal(2, ranked.Count);
        // CamelCase 'Search' in mySearchProvider should outrank substring 'search' inside 'research'
        Assert.Equal("mySearchProvider.cs", ranked[0].FileName);
    }

    [Fact]
    public void Rank_WindowsSearchQuery_ParentFolderOutranksNestedBenchmarkSubfolder()
    {
        var candidates = new List<FileIndexRecord>
        {
            new()
            {
                FileName = "WindowsSearch.Benchmark.Cli",
                DirectoryPath = @"C:\Studia\magisterka\WindowsSearch\Benchmarking",
                IsDirectory = true
            },
            new()
            {
                FileName = "WindowsSearch",
                DirectoryPath = @"C:\Studia\magisterka",
                IsDirectory = true
            }
        };

        var ranked = _rankingService.Rank("windowssearch", candidates, 10);

        Assert.Equal(2, ranked.Count);
        Assert.Equal("WindowsSearch", ranked[0].FileName);
        Assert.Equal(@"C:\Studia\magisterka\WindowsSearch", ranked[0].FullPath);
        Assert.Equal("WindowsSearch.Benchmark.Cli", ranked[1].FileName);
    }

    [Theory]
    [InlineData("benchmarking")]
    [InlineData("benchmark")]
    public void Rank_BenchmarkQuery_ParentBenchmarkingFolderOutranksNestedSubfolder(string query)
    {
        var candidates = new List<FileIndexRecord>
        {
            new()
            {
                FileName = "WindowsSearch.Benchmark.Core",
                DirectoryPath = @"C:\Studia\magisterka\WindowsSearch\Benchmarking",
                IsDirectory = true
            },
            new()
            {
                FileName = "Benchmarking",
                DirectoryPath = @"C:\Studia\magisterka\WindowsSearch",
                IsDirectory = true
            }
        };

        var ranked = _rankingService.Rank(query, candidates, 10);

        Assert.Equal(2, ranked.Count);
        Assert.Equal("Benchmarking", ranked[0].FileName);
        Assert.Equal(@"C:\Studia\magisterka\WindowsSearch\Benchmarking", ranked[0].FullPath);
        Assert.Equal("WindowsSearch.Benchmark.Core", ranked[1].FileName);
    }
}
