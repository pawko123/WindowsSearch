using System.ComponentModel.DataAnnotations;
using WindowsSearch.Common.Validation;

namespace WindowsSearch.Common.Tests.Validation;

public class SettingsValidationHelperTests
{
    private class SimpleSettings
    {
        [Required]
        public string? RequiredString { get; set; }
        
        [Range(1, 100)]
        public int Number { get; set; }
    }

    private class NestedSettings
    {
        [Required]
        public string? Name { get; set; }
        
        public SimpleSettings Child { get; set; } = new();
    }

    private class CyclicSettings
    {
        [Required]
        public string? Name { get; set; }
        
        public CyclicSettings? SelfRef { get; set; }
    }

    [Fact]
    public void Validate_ValidObject_ReturnsEmpty()
    {
        var settings = new SimpleSettings { RequiredString = "hello", Number = 50 };
        var errors = SettingsValidationHelper.Validate(settings);
        
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_MissingRequiredProperty_ReturnsError()
    {
        var settings = new SimpleSettings { Number = 50 }; // RequiredString is null
        var errors = SettingsValidationHelper.Validate(settings);
        
        Assert.Single(errors);
        Assert.Contains("RequiredString", errors[0]);
    }

    [Fact]
    public void Validate_InvalidRange_ReturnsError()
    {
        var settings = new SimpleSettings { RequiredString = "hello", Number = 200 }; // outside range
        var errors = SettingsValidationHelper.Validate(settings);
        
        Assert.Single(errors);
        Assert.Contains("Number", errors[0]);
    }

    [Fact]
    public void Validate_NestedObject_ValidatesChildren()
    {
        var settings = new NestedSettings 
        { 
            Name = "Parent",
            Child = new SimpleSettings { Number = 50 } // Child's RequiredString is missing
        };
        
        var errors = SettingsValidationHelper.Validate(settings);
        
        Assert.Single(errors);
        Assert.Contains("RequiredString", errors[0]);
    }

    [Fact]
    public void Validate_CyclicReference_DoesNotLoopInfinitely()
    {
        var settings = new CyclicSettings { Name = "Cyclic" };
        settings.SelfRef = settings; // cycle
        
        var errors = SettingsValidationHelper.Validate(settings);
        
        Assert.Empty(errors);
    }
}
