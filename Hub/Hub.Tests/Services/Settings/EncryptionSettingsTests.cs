using System.IO.Abstractions.TestingHelpers;
using Hub.Models.Settings;
using Hub.Services.Settings;
using WindowsSearch.Common.Security;

namespace Hub.Tests.Services.Settings;

public class EncryptionSettingsTests
{
    private readonly MockFileSystem _mockFileSystem;
    private readonly string _testSettingsDir = @"C:\TestApp";

    public EncryptionSettingsTests()
    {
        _mockFileSystem = new MockFileSystem();
    }

    [Theory]
    [InlineData(EncryptionKind.None, "none")]
    [InlineData(EncryptionKind.Aes128Gcm, "aes128_gcm")]
    [InlineData(EncryptionKind.Aes256Gcm, "aes256_gcm")]
    [InlineData(EncryptionKind.Aes256Ccm, "aes256_ccm")]
    [InlineData(EncryptionKind.Aes256CbcHmac, "aes256_cbc_hmac")]
    [InlineData(EncryptionKind.ChaCha20Poly1305, "cha_cha20_poly1305")]
    public void Load_FileExists_WithEncryption_ReturnsParsedEncryptionKind(EncryptionKind expectedKind, string yamlValue)
    {
        var expectedPath = _mockFileSystem.Path.Combine(_testSettingsDir, "app_config.yaml");
        _mockFileSystem.AddDirectory(_testSettingsDir);
        _mockFileSystem.AddFile(expectedPath, new MockFileData($"provider:\n  encryption: {yamlValue}\n"));

        var service = new AppSettingsService(_mockFileSystem, _testSettingsDir);
        var (settings, errors, wasCreated) = service.Load();

        Assert.NotNull(settings);
        Assert.False(wasCreated);
        Assert.Empty(errors);
        Assert.Equal(expectedKind, settings.Provider.Encryption);
    }

    [Fact]
    public void Save_WithEncryption_WritesEncryptionYaml()
    {
        var service = new AppSettingsService(_mockFileSystem, _testSettingsDir);
        var settings = new AppSettings();
        settings.Provider.Encryption = EncryptionKind.ChaCha20Poly1305;

        var errors = service.Save(settings);

        Assert.Empty(errors);

        var expectedPath = _mockFileSystem.Path.Combine(_testSettingsDir, "app_config.yaml");
        Assert.True(_mockFileSystem.FileExists(expectedPath));
        var content = _mockFileSystem.GetFile(expectedPath).TextContents;

        Assert.Contains("encryption:", content);
    }
}
