using ChromeProvider.Settings;
using WindowsSearch.Common.Validation;

namespace ChromeProvider.Tests;

public class ChromeProviderSettingsTests
{
    [Fact]
    public void DefaultSettings_AreValid()
    {
        var settings = new ChromeProviderSettings();
        var errors = SettingsValidationHelper.Validate(settings);
        Assert.Empty(errors);
    }

    [Fact]
    public void EmptyProfileName_IsInvalid()
    {
        var settings = new ChromeProviderSettings { ProfileName = "" };
        var errors = SettingsValidationHelper.Validate(settings);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("profile name is required", StringComparison.OrdinalIgnoreCase));
    }
}