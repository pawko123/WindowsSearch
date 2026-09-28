using DemoProvider.Settings;
using WindowsSearch.Common.Validation;

namespace DemoProvider.Tests.Settings;

public class DemoProviderSettingsTests
{
    [Fact]
    public void Validate_MissingEchoPrefix_ReturnsRequiredErrorMessage()
    {
        var settings = new DemoProviderSettings { EchoPrefix = "" };
        var errors = SettingsValidationHelper.Validate(settings);
        
        Assert.Single(errors);
        Assert.Equal("Echo prefix is required.", errors[0]);
    }

    [Fact]
    public void Validate_MissingActionPath_ReturnsRequiredErrorMessage()
    {
        var settings = new DemoProviderSettings { ActionPath = "" };
        var errors = SettingsValidationHelper.Validate(settings);
        
        Assert.Single(errors);
        Assert.Equal("Action path is required.", errors[0]);
    }
}
