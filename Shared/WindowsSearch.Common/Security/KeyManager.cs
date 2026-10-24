using System.Security.Cryptography;
using System.Text;

namespace WindowsSearch.Common.Security;

public static class KeyManager
{
    private const string MasterKeyEnvVar = "WINDOWS_SEARCH_MASTER_KEY";
    private static readonly object LockObj = new();
    private static byte[]? _cachedMasterKey;

    public static byte[] GetOrCreateMasterKey()
    {
        lock (LockObj)
        {
            if (_cachedMasterKey != null)
            {
                return (byte[])_cachedMasterKey.Clone();
            }

            var envKey = Environment.GetEnvironmentVariable(MasterKeyEnvVar);
            if (!string.IsNullOrWhiteSpace(envKey))
            {
                _cachedMasterKey = ParseMasterKeyString(envKey);
                return (byte[])_cachedMasterKey.Clone();
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(localAppData, "WindowsSearch");
            var keyFile = Path.Combine(dir, "security.dat");

            if (File.Exists(keyFile))
            {
                try
                {
                    var encryptedBytes = File.ReadAllBytes(keyFile);
                    var decryptedKey = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                    if (decryptedKey.Length == 32)
                    {
                        _cachedMasterKey = decryptedKey;
                        return (byte[])_cachedMasterKey.Clone();
                    }
                }
                catch
                {
                    // If corrupted or cannot unprotect, regenerate below
                }
            }

            Directory.CreateDirectory(dir);
            var newKey = RandomNumberGenerator.GetBytes(32);
            var protectedBytes = ProtectedData.Protect(newKey, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(keyFile, protectedBytes);

            _cachedMasterKey = newKey;
            return (byte[])_cachedMasterKey.Clone();
        }
    }

    public static byte[] DeriveSubkey(byte[] masterKey, string purpose, int keyLengthBytes)
    {
        ArgumentNullException.ThrowIfNull(masterKey);
        ArgumentException.ThrowIfNullOrEmpty(purpose);

        if (keyLengthBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(keyLengthBytes), "Key length must be greater than zero.");
        }

        return HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            ikm: masterKey,
            outputLength: keyLengthBytes,
            salt: null,
            info: Encoding.UTF8.GetBytes(purpose));
    }

    public static void ResetCache()
    {
        lock (LockObj)
        {
            _cachedMasterKey = null;
        }
    }

    private static byte[] ParseMasterKeyString(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.Length == 64 && IsHexString(trimmed))
        {
            return Convert.FromHexString(trimmed);
        }

        try
        {
            var base64Bytes = Convert.FromBase64String(trimmed);
            if (base64Bytes.Length == 32)
            {
                return base64Bytes;
            }
        }
        catch
        {
            // Not valid base64
        }

        return SHA256.HashData(Encoding.UTF8.GetBytes(trimmed));
    }

    private static bool IsHexString(string s)
    {
        foreach (var c in s)
        {
            if (!char.IsAsciiHexDigit(c))
            {
                return false;
            }
        }
        return true;
    }
}
