using WindowsSearch.Common.Models;
using WindowsSearch.Common.Validation;

namespace WindowsSearch.Common.Tests.Models;

public class ProviderSettingsBaseTests
{

    [Fact]
    public void Validate_ValidObjectReturnsEmpty()
    {
        var settings = new GenericProviderSettings(); // Has default ProviderEndpoints
        var errors = SettingsValidationHelper.Validate(settings);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_InvalidHttpFormat_ReturnsSpecificErrorMessage()
    {
        var settings = new GenericProviderSettings();
        settings.Endpoints.Http = "not-a-valid-url";
        
        var errors = SettingsValidationHelper.Validate(settings);
        
        Assert.Single(errors);
        Assert.Equal("Endpoint must be an absolute HTTP or HTTPS URI.", errors[0]);
    }

    [Fact]
    public void Validate_MissingHttpScheme_ReturnsSpecificErrorMessage()
    {
        var settings = new GenericProviderSettings();
        settings.Endpoints.Grpc = "ftp://localhost:5001";
        
        var errors = SettingsValidationHelper.Validate(settings);
        
        Assert.Single(errors);
        Assert.Equal("Endpoint must be an absolute HTTP or HTTPS URI.", errors[0]);
    }

    [Fact]
    public void Validate_EmptyNamedPipe_ReturnsRequiredErrorMessage()
    {
        var settings = new GenericProviderSettings();
        settings.Endpoints.NamedPipe = "";
        
        var errors = SettingsValidationHelper.Validate(settings);
        
        Assert.Single(errors);
        Assert.Equal("Named pipe endpoint is required.", errors[0]);
    }

    [Fact]
    public void Validate_InvalidNamedPipePrefix_ReturnsSpecificErrorMessage()
    {
        var settings = new GenericProviderSettings();
        settings.Endpoints.NamedPipe = @"C:\pipe\wrong_prefix";
        
        var errors = SettingsValidationHelper.Validate(settings);
        
        Assert.Single(errors);
        Assert.Equal(@"Named pipe endpoints must start with \\.\pipe\.", errors[0]);
    }
}
