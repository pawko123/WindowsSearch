using VsCodeProvider.Settings;
using WindowsSearch.Common.Validation;

namespace VsCodeProvider.Tests.Settings;

public class VsCodeProviderSettingsTests
{
    [Fact]
    public void Validate_MissingJumpListId_ReturnsRequiredErrorMessage()
    {
        var settings = new VsCodeProviderSettings { JumpListId = "" };
        var errors = SettingsValidationHelper.Validate(settings);
        
        Assert.Single(errors);
        Assert.Equal("Jump List ID is required.", errors[0]);
    }

    [Fact]
    public void Validate_InvalidJumpListId_ReturnsRegexErrorMessage()
    {
        var settings = new VsCodeProviderSettings { JumpListId = "invalid-hex" };
        var errors = SettingsValidationHelper.Validate(settings);
        
        Assert.Single(errors);
        Assert.Equal("Jump List ID must be exactly 16 hexadecimal characters.", errors[0]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1441)]
    public void Validate_CacheTtlMinutes_OutOfRange_ReturnsSpecificErrorMessage(int invalidValue)
    {
        var settings = new VsCodeProviderSettings { CacheTtlMinutes = invalidValue };
        var errors = SettingsValidationHelper.Validate(settings);
        
        Assert.Single(errors);
        Assert.Equal("Cache TTL must be between 1 and 1440 minutes.", errors[0]);
    }
}
