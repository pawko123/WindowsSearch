using System.IO.Abstractions.TestingHelpers;
using Hub.Models.Settings;
using Hub.Services.Settings;

namespace Hub.Tests.Services.Settings;

public class AppSettingsServiceTests
{
    private readonly MockFileSystem _mockFileSystem;
    private readonly string _testSettingsDir = @"C:\TestApp";

    public AppSettingsServiceTests()
    {
        _mockFileSystem = new MockFileSystem();
    }

    [Fact]
    public void Load_FileDoesNotExist_CreatesDirectoryAndReturnsDefault()
    {
        var service = new AppSettingsService(_mockFileSystem, _testSettingsDir);

        var (settings, errors, wasCreated) = service.Load();

        Assert.True(_mockFileSystem.Directory.Exists(_testSettingsDir));
        var expectedPath = _mockFileSystem.Path.Combine(_testSettingsDir, "app_config.yaml");
        Assert.True(_mockFileSystem.FileExists(expectedPath));

        Assert.NotNull(settings);
        Assert.True(wasCreated);
        Assert.Empty(errors);
        Assert.Equal(ProviderSearchMode.Sequential, settings.Provider.SearchMode); // Assuming default
    }

    [Fact]
    public void Load_FileExists_ReturnsParsedSettings()
    {
        var expectedPath = _mockFileSystem.Path.Combine(_testSettingsDir, "app_config.yaml");
        _mockFileSystem.AddDirectory(_testSettingsDir);
        _mockFileSystem.AddFile(expectedPath, new MockFileData("provider:\n  search_mode: ConcurrentBlocking\n"));

        var service = new AppSettingsService(_mockFileSystem, _testSettingsDir);
        var (settings, errors, wasCreated) = service.Load();

        Assert.NotNull(settings);
        Assert.False(wasCreated);
        Assert.Empty(errors);
        Assert.Equal(ProviderSearchMode.ConcurrentBlocking, settings.Provider.SearchMode);
    }

    [Fact]
    public void Save_ValidSettings_WritesToFile()
    {
        var service = new AppSettingsService(_mockFileSystem, _testSettingsDir);
        var settings = new AppSettings();
        settings.Provider.SearchMode = ProviderSearchMode.ConcurrentStream;

        var errors = service.Save(settings);

        Assert.Empty(errors);
        
        var expectedPath = _mockFileSystem.Path.Combine(_testSettingsDir, "app_config.yaml");
        Assert.True(_mockFileSystem.FileExists(expectedPath));
        var content = _mockFileSystem.GetFile(expectedPath).TextContents;
        
        Assert.Contains("search_mode: ConcurrentStream", content);
    }
}
