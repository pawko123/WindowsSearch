using WindowsSearch.Common.Models;
using WindowsSearch.Common.Serialization;
using WindowsSearch.Common.Logging;
using System.IO.Abstractions.TestingHelpers;
namespace WindowsSearch.Common.Tests.Serialization;

public class ProviderSettingsYamlTests
{
    private readonly MockFileSystem _mockFileSystem = new();

    public ProviderSettingsYamlTests()
    {
        ProviderSettingsYaml.FileSystem = _mockFileSystem;
    }

    [Fact]
    public void Parse_ValidYaml_MapsUnderscoredPropertiesCorrectly()
    {
        var yaml = "is_enabled: false\nlog_level: 0\n";
        var parsed = ProviderSettingsYaml.Parse<GenericProviderSettings>(yaml);
        
        Assert.NotNull(parsed);
        Assert.False(parsed.IsEnabled);
        Assert.Equal(LogLevel.Debug, parsed.LogLevel); // 0 = Debug
    }
    [Fact]
    public void Parse_Weight_MappedCorrectly()
    {
        var yaml = "weight: 15\n";
        var parsed = ProviderSettingsYaml.Parse<GenericProviderSettings>(yaml);
        
        Assert.NotNull(parsed);
        Assert.Equal(15, parsed.Weight);
    }

    [Fact]
    public void Parse_MissingProperties_KeepsDefaults()
    {
        var yaml = "endpoints:\n  http: \"http://custom:8080\"\n";
        var parsed = ProviderSettingsYaml.Parse<GenericProviderSettings>(yaml);
        
        Assert.NotNull(parsed);
        Assert.True(parsed.IsEnabled); // Default
        Assert.Equal(LogLevel.Info, parsed.LogLevel); // Default
        Assert.Equal("http://custom:8080", parsed.Endpoints.Http); // Parsed
        Assert.Equal("http://localhost:5001", parsed.Endpoints.Grpc); // Default preserved
    }

    [Fact]
    public void Parse_EmptyYaml_ReturnsNull()
    {
        var parsed = ProviderSettingsYaml.Parse<GenericProviderSettings>("");
        Assert.Null(parsed);
    }

    [Fact]
    public void Load_FileExists_ReturnsParsedSettings()
    {
        var path = @"C:\settings.yaml";
        _mockFileSystem.AddFile(path, new MockFileData("is_enabled: false\nweight: 42"));

        var settings = ProviderSettingsYaml.Load<GenericProviderSettings>(path);

        Assert.NotNull(settings);
        Assert.False(settings.IsEnabled);
        Assert.Equal(42, settings.Weight);
    }

    [Fact]
    public void Load_FileDoesNotExist_ReturnsDefaultSettings()
    {
        var path = @"C:\missing.yaml";

        var settings = ProviderSettingsYaml.Load<GenericProviderSettings>(path);

        Assert.NotNull(settings);
        Assert.True(settings.IsEnabled); // Default value
        Assert.Equal(0, settings.Weight); // Default value
    }

    [Fact]
    public void Save_WritesYamlToFile()
    {
        var path = @"C:\saved.yaml";
        var settings = new GenericProviderSettings { IsEnabled = false, Weight = 99 };

        ProviderSettingsYaml.Save(path, settings);

        Assert.True(_mockFileSystem.FileExists(path));
        var content = _mockFileSystem.GetFile(path).TextContents;
        Assert.Contains("is_enabled: false", content);
        Assert.Contains("weight: 99", content);
    }
}
