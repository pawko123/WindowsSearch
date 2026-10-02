using System.ComponentModel.DataAnnotations;
using WindowsSearch.Common.Validation;
using System.IO.Abstractions.TestingHelpers;

namespace WindowsSearch.Common.Tests.Validation;

public class PathExistsAttributeTests
{
    private readonly MockFileSystem _mockFileSystem;
    private readonly string _testBaseDir = @"C:\MockTemp\PathExistsAttributeTests";

    public PathExistsAttributeTests()
    {
        _mockFileSystem = new MockFileSystem();
        _mockFileSystem.AddDirectory(_testBaseDir);
        PathExistsAttribute.FileSystem = _mockFileSystem;
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
        var settings = new SettingsWithNoItem { DirPath = _mockFileSystem.Path.Combine(_testBaseDir, "non_existent") };
        var errors = SettingsValidationHelper.Validate(settings);
        
        Assert.Single(errors);
        Assert.Contains("directory was not found", errors[0]);
    }

    [Fact]
    public void Validate_BasePathAndValueExists_ReturnsEmpty()
    {
        var dirName = _mockFileSystem.Path.GetFileName(_testBaseDir); // Just the folder name
        
        // We need to make sure the mocked path matches the expanded base path in the mock file system.
        // The base path is %TEMP%. Let's add that to the mock file system for this test.
        var tempPath = Environment.ExpandEnvironmentVariables("%TEMP%");
        _mockFileSystem.AddDirectory(_mockFileSystem.Path.Combine(tempPath, dirName));
        
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
