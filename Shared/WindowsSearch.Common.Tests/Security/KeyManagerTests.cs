using System.Security.Cryptography;
using System.Text;
using WindowsSearch.Common.Security;

namespace WindowsSearch.Common.Tests.Security;

public class KeyManagerTests : IDisposable
{
    private readonly string? _originalEnv;

    public KeyManagerTests()
    {
        _originalEnv = Environment.GetEnvironmentVariable("WINDOWS_SEARCH_MASTER_KEY");
        KeyManager.ResetCache();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("WINDOWS_SEARCH_MASTER_KEY", _originalEnv);
        KeyManager.ResetCache();
    }

    [Fact]
    public void GetOrCreateMasterKey_Returns32ByteKey()
    {
        var key = KeyManager.GetOrCreateMasterKey();

        Assert.NotNull(key);
        Assert.Equal(32, key.Length);
    }

    [Fact]
    public void GetOrCreateMasterKey_WithEnvironmentVariableHex_ReturnsConfiguredKey()
    {
        var expectedHex = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        Environment.SetEnvironmentVariable("WINDOWS_SEARCH_MASTER_KEY", expectedHex);
        KeyManager.ResetCache();

        var key = KeyManager.GetOrCreateMasterKey();

        Assert.Equal(Convert.FromHexString(expectedHex), key);
    }

    [Fact]
    public void DeriveSubkey_DeterministicForSamePurpose()
    {
        var masterKey = RandomNumberGenerator.GetBytes(32);
        var subkey1 = KeyManager.DeriveSubkey(masterKey, "TestPurpose", 32);
        var subkey2 = KeyManager.DeriveSubkey(masterKey, "TestPurpose", 32);

        Assert.Equal(subkey1, subkey2);
    }

    [Fact]
    public void DeriveSubkey_DifferentForDifferentPurposes()
    {
        var masterKey = RandomNumberGenerator.GetBytes(32);
        var subkeyA = KeyManager.DeriveSubkey(masterKey, "PurposeA", 32);
        var subkeyB = KeyManager.DeriveSubkey(masterKey, "PurposeB", 32);

        Assert.NotEqual(subkeyA, subkeyB);
    }

    [Fact]
    public void DeriveSubkey_RespectsRequestedLength()
    {
        var masterKey = RandomNumberGenerator.GetBytes(32);
        var subkey16 = KeyManager.DeriveSubkey(masterKey, "LengthTest", 16);
        var subkey64 = KeyManager.DeriveSubkey(masterKey, "LengthTest", 64);

        Assert.Equal(16, subkey16.Length);
        Assert.Equal(64, subkey64.Length);
    }
}
