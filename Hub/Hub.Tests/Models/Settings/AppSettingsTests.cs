using Hub.Models.Settings;
using WindowsSearch.Common.Validation;

namespace Hub.Tests.Models.Settings;

public class AppSettingsTests
{
    [Fact]
    public void DefaultAppSettings_PassesValidation()
    {
        var appSettings = new AppSettings();
        var errors = SettingsValidationHelper.Validate(appSettings);
        
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1441)]
    public void Validate_AppCacheTtlMinutes_OutOfRange_ReturnsSpecificErrorMessage(int invalidValue)
    {
        var settings = new AppSettings { AppCacheTtlMinutes = invalidValue };
        var errors = SettingsValidationHelper.Validate(settings);
        
        Assert.Single(errors);
        Assert.Equal("App cache TTL must be between 1 and 1440 minutes.", errors[0]);
    }

    [Fact]
    public void Validate_SearchLimit_OutOfRange_ReturnsSpecificErrorMessage()
    {
        var settings = new AppSettings();
        settings.Search.SearchLimit = 0; // Less than 1
        var errors = SettingsValidationHelper.Validate(settings);
        
        Assert.Single(errors);
        Assert.Equal("Search limit must be greater than zero.", errors[0]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5001)]
    public void Validate_ProviderDebounceDelayMs_OutOfRange_ReturnsSpecificErrorMessage(int invalidValue)
    {
        var settings = new AppSettings();
        settings.Search.ProviderDebounceDelayMs = invalidValue;
        var errors = SettingsValidationHelper.Validate(settings);
        
        Assert.Single(errors);
        Assert.Equal("Debounce delay must be between 0 and 5000 ms.", errors[0]);
    }

    [Fact]
    public void Validate_ProviderTimeoutSeconds_OutOfRange_ReturnsSpecificErrorMessage()
    {
        var settings = new AppSettings();
        settings.Provider.ProviderTimeoutSeconds = 0; // Less than 1
        var errors = SettingsValidationHelper.Validate(settings);
        
        Assert.Single(errors);
        Assert.Equal("Provider timeout must be greater than zero.", errors[0]);
    }
}
