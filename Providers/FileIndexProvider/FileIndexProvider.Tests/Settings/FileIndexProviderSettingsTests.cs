using FileIndexProvider.Settings;
using WindowsSearch.Common.Validation;

namespace FileIndexProvider.Tests.Settings;

public class FileIndexProviderSettingsTests
{
    [Fact]
    public void DefaultSettings_AreValid()
    {
        var settings = new FileIndexProviderSettings();
        var errors = SettingsValidationHelper.Validate(settings);

        Assert.Empty(errors);
        Assert.True(settings.IsEnabled);
        Assert.Single(settings.IndexedDirectories);
        Assert.Equal(@"C:\", settings.IndexedDirectories[0]);
        Assert.True(settings.EnableLiveWatcher);
        Assert.Equal(@"\\.\pipe\file_index_provider", settings.Endpoints.NamedPipe);
        Assert.Equal("http://localhost:5060", settings.Endpoints.Http);
        Assert.Equal("http://localhost:5061", settings.Endpoints.Grpc);
    }
}
