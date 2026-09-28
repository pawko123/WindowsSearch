using JetBrainsProvider.Settings;
using WindowsSearch.Common.Validation;

namespace JetBrainsProvider.Tests.Settings;

public class JetBrainsProviderSettingsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1441)]
    public void Validate_CacheTtlMinutes_OutOfRange_ReturnsSpecificErrorMessage(int invalidValue)
    {
        var settings = new JetBrainsProviderSettings { CacheTtlMinutes = invalidValue };
        var errors = SettingsValidationHelper.Validate(settings);
        
        Assert.Single(errors);
        Assert.Equal("Cache TTL must be between 1 and 1440 minutes.", errors[0]);
    }
}
