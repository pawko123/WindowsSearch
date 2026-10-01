using WindowsSearch.Common.Models;
using WindowsSearch.Common.Serialization;
using WindowsSearch.Common.Logging;

namespace WindowsSearch.Common.Tests.Serialization;

public class ProviderSettingsYamlTests
{
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
}
