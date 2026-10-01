using System.ComponentModel.DataAnnotations;
using WindowsSearch.Common.Validation;

namespace WindowsSearch.Common.Tests.Validation;

public class PathExistsAttributeTests : IDisposable
{
    private readonly string _testBaseDir;

    public PathExistsAttributeTests()
    {
        _testBaseDir = Path.Combine(Path.GetTempPath(), "PathExistsAttributeTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testBaseDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testBaseDir))
        {
            Directory.Delete(_testBaseDir, true);
        }
    }

    private class SettingsWithNoItem
    {
        [PathExists]
        public string? DirPath { get; set; }
    }

    private class SettingsWithBasePath
    {
        [PathExists(BasePath = "%TEMP%")]
        public string? ProfilePath { get; set; }
    }

    [Fact]
    public void Validate_PathExists_ReturnsEmpty()
    {
        var settings = new SettingsWithNoItem { DirPath = _testBaseDir };
        var errors = SettingsValidationHelper.Validate(settings);
        
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_PathDoesNotExist_ReturnsError()
    {
        var settings = new SettingsWithNoItem { DirPath = Path.Combine(_testBaseDir, "non_existent") };
        var errors = SettingsValidationHelper.Validate(settings);
        
        Assert.Single(errors);
        Assert.Contains("directory was not found", errors[0]);
    }

    [Fact]
    public void Validate_BasePathAndValueExists_ReturnsEmpty()
    {
        var dirName = Path.GetFileName(_testBaseDir); // Just the folder name
        var settings = new SettingsWithBasePath { ProfilePath = dirName };
        var errors = SettingsValidationHelper.Validate(settings);
        
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_BasePathAndValueDoesNotExist_ReturnsError()
    {
        var settings = new SettingsWithBasePath { ProfilePath = "non_existent_folder_xyz123" };
        var errors = SettingsValidationHelper.Validate(settings);
        
        Assert.Single(errors);
        Assert.Contains("directory was not found", errors[0]);
    }

    [Fact]
    public void Validate_NullPath_ReturnsError()
    {
        var settings = new SettingsWithNoItem { DirPath = null };
        var errors = SettingsValidationHelper.Validate(settings);
        
        Assert.Single(errors);
        Assert.Contains("Path is required.", errors[0]);
    }
}
