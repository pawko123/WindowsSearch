using WebBaseProvider.Settings;
using WindowsSearch.Common.Validation;

namespace WebBaseProvider.Tests.Settings;

public class WebBaseProviderSettingsTests
{
    [Fact]
    public void Validate_DefaultSettings_ReturnsEmpty()
    {
        var settings = new WebBaseProviderSettings();
        var errors = SettingsValidationHelper.Validate(settings);
        
        Assert.Empty(errors);
    }
}
