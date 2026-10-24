using System.Security.Cryptography;
using System.Text;
using WindowsSearch.Common.Security;

namespace WindowsSearch.Common.Tests.Security;

public class WireEnvelopeCodecTests
{
    private static readonly byte[] MasterKey = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef");

    [Theory]
    [InlineData(EncryptionKind.None)]
    [InlineData(EncryptionKind.Aes128Gcm)]
    [InlineData(EncryptionKind.Aes256Gcm)]
    [InlineData(EncryptionKind.Aes256Ccm)]
    [InlineData(EncryptionKind.Aes256CbcHmac)]
    [InlineData(EncryptionKind.ChaCha20Poly1305)]
    public void EncryptDecrypt_Roundtrip_MatchesOriginalPlaintext(EncryptionKind kind)
    {
        var plaintext = Encoding.UTF8.GetBytes("The quick brown fox jumps over the lazy dog. 12345! @#$%");

        var envelope = WireEnvelopeCodec.Encrypt(plaintext, kind, MasterKey);
        var decrypted = WireEnvelopeCodec.Decrypt(envelope, kind, MasterKey);

        Assert.Equal(plaintext, decrypted);
    }

    [Theory]
    [InlineData(EncryptionKind.None)]
    [InlineData(EncryptionKind.Aes128Gcm)]
    [InlineData(EncryptionKind.Aes256Gcm)]
    [InlineData(EncryptionKind.Aes256Ccm)]
    [InlineData(EncryptionKind.Aes256CbcHmac)]
    [InlineData(EncryptionKind.ChaCha20Poly1305)]
    public void EncryptDecrypt_EmptyPayload_Succeeds(EncryptionKind kind)
    {
        var plaintext = Array.Empty<byte>();

        var envelope = WireEnvelopeCodec.Encrypt(plaintext, kind, MasterKey);
        var decrypted = WireEnvelopeCodec.Decrypt(envelope, kind, MasterKey);

        Assert.Equal(plaintext, decrypted);
    }

    [Theory]
    [InlineData(EncryptionKind.None)]
    [InlineData(EncryptionKind.Aes128Gcm)]
    [InlineData(EncryptionKind.Aes256Gcm)]
    [InlineData(EncryptionKind.Aes256Ccm)]
    [InlineData(EncryptionKind.Aes256CbcHmac)]
    [InlineData(EncryptionKind.ChaCha20Poly1305)]
    public void EncryptDecrypt_LargePayload_1MB_Succeeds(EncryptionKind kind)
    {
        var plaintext = new byte[1024 * 1024];
        RandomNumberGenerator.Fill(plaintext);

        var envelope = WireEnvelopeCodec.Encrypt(plaintext, kind, MasterKey);
        var decrypted = WireEnvelopeCodec.Decrypt(envelope, kind, MasterKey);

        Assert.Equal(plaintext, decrypted);
    }

    [Theory]
    [InlineData(EncryptionKind.Aes128Gcm)]
    [InlineData(EncryptionKind.Aes256Gcm)]
    [InlineData(EncryptionKind.Aes256Ccm)]
    [InlineData(EncryptionKind.Aes256CbcHmac)]
    [InlineData(EncryptionKind.ChaCha20Poly1305)]
    public void Decrypt_TamperedCiphertext_ThrowsCryptographicException(EncryptionKind kind)
    {
        var plaintext = Encoding.UTF8.GetBytes("Sensitive payload that must not be altered!");
        var envelope = WireEnvelopeCodec.Encrypt(plaintext, kind, MasterKey);

        // Tamper last byte (part of ciphertext)
        envelope[^1] ^= 0xFF;

        Assert.ThrowsAny<CryptographicException>(() => WireEnvelopeCodec.Decrypt(envelope, kind, MasterKey));
    }

    [Theory]
    [InlineData(EncryptionKind.Aes128Gcm)]
    [InlineData(EncryptionKind.Aes256Gcm)]
    [InlineData(EncryptionKind.Aes256Ccm)]
    [InlineData(EncryptionKind.Aes256CbcHmac)]
    [InlineData(EncryptionKind.ChaCha20Poly1305)]
    public void Decrypt_TamperedTag_ThrowsCryptographicException(EncryptionKind kind)
    {
        var plaintext = Encoding.UTF8.GetBytes("Sensitive payload that must not be altered!");
        var envelope = WireEnvelopeCodec.Encrypt(plaintext, kind, MasterKey);

        // Tamper byte in tag section (index 15 for AEAD, index 20 for CBC-HMAC)
        int tagIndex = kind == EncryptionKind.Aes256CbcHmac ? 20 : 15;
        envelope[tagIndex] ^= 0xFF;

        Assert.ThrowsAny<CryptographicException>(() => WireEnvelopeCodec.Decrypt(envelope, kind, MasterKey));
    }

    [Fact]
    public void Decrypt_CorruptVersion_ThrowsCryptographicException()
    {
        var plaintext = Encoding.UTF8.GetBytes("Payload");
        var envelope = WireEnvelopeCodec.Encrypt(plaintext, EncryptionKind.Aes256Gcm, MasterKey);

        envelope[0] = 0x99; // corrupt version

        Assert.Throws<CryptographicException>(() => WireEnvelopeCodec.Decrypt(envelope, EncryptionKind.Aes256Gcm, MasterKey));
    }

    [Fact]
    public void Decrypt_KindMismatch_ThrowsCryptographicException()
    {
        var plaintext = Encoding.UTF8.GetBytes("Payload");
        var envelope = WireEnvelopeCodec.Encrypt(plaintext, EncryptionKind.Aes256Gcm, MasterKey);

        Assert.Throws<CryptographicException>(() => WireEnvelopeCodec.Decrypt(envelope, EncryptionKind.ChaCha20Poly1305, MasterKey));
    }

    [Fact]
    public void FormatEnvelopeForLog_ReturnsNonEmptyFormattedString()
    {
        var plaintext = Encoding.UTF8.GetBytes("Sample message");
        var envelope = WireEnvelopeCodec.Encrypt(plaintext, EncryptionKind.Aes256Gcm, MasterKey);

        var formatted = WireEnvelopeCodec.FormatEnvelopeForLog(envelope, EncryptionKind.Aes256Gcm);

        Assert.Contains("Aes256Gcm", formatted);
        Assert.Contains("bytes", formatted);
        Assert.Contains("Hex:", formatted);
    }
}
