using FirefoxProvider.Settings;
using WindowsSearch.Common.Validation;

namespace FirefoxProvider.Tests.Settings;

public class FirefoxProviderSettingsTests
{
    [Fact]
    public void Validate_MissingProfileName_ReturnsRequiredErrorMessage()
    {
        var settings = new FirefoxProviderSettings { ProfileName = "" };
        var errors = SettingsValidationHelper.Validate(settings);
        
        Assert.Single(errors);
        Assert.Equal("Firefox profile name is required.", errors[0]);
    }
}
