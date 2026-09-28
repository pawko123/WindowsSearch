using System.IO.Abstractions.TestingHelpers;
using Hub.Services.Settings;
using WindowsSearch.Common.Models;

namespace Hub.Tests.Services.Settings;

public class ProviderSettingsServiceTests
{
    private readonly MockFileSystem _fileSystem;
    private readonly ProviderSettingsService _service;
    private readonly string _providersRoot;

    public ProviderSettingsServiceTests()
    {
        _fileSystem = new MockFileSystem();
        _providersRoot = _fileSystem.Path.Combine(AppContext.BaseDirectory, "Providers");
        _fileSystem.Directory.CreateDirectory(_providersRoot);
        
        _service = new ProviderSettingsService(_fileSystem);
    }

    [Fact]
    public void LoadAll_WithValidYaml_LoadsAndValidatesSettings()
    {
        // Arrange
        var providerDir = _fileSystem.Path.Combine(_providersRoot, "MockProvider");
        _fileSystem.Directory.CreateDirectory(providerDir);
        
        var yamlPath = _fileSystem.Path.Combine(providerDir, "settings.yaml");
        var yamlContent = "is_enabled: true\n";
        _fileSystem.AddFile(yamlPath, new MockFileData(yamlContent));

        // Act
        var models = _service.LoadAll();

        // Assert
        Assert.Single(models);
        var model = models[0];
        Assert.Equal("MockProvider", model.ProviderName);
        Assert.True(model.Settings.IsEnabled);
        Assert.NotNull(model.Settings.Endpoints);
    }

    [Fact]
    public void Save_WithValidModel_WritesToDisk()
    {
        // Arrange
        var providerDir = _fileSystem.Path.Combine(_providersRoot, "SaveTestProvider");
        _fileSystem.Directory.CreateDirectory(providerDir);
        var yamlPath = _fileSystem.Path.Combine(providerDir, "settings.yaml");

        var model = new Hub.Models.Settings.ProviderSettingsModel
        {
            ProviderName = "SaveTestProvider",
            SettingsPath = yamlPath,
            Settings = new GenericProviderSettings { IsEnabled = false }
        };

        // Act
        var errors = _service.Save(model);

        // Assert
        Assert.Empty(errors);
        Assert.True(_fileSystem.FileExists(yamlPath));
        var content = _fileSystem.File.ReadAllText(yamlPath);
        Assert.Contains("is_enabled: false", content);
    }
}
