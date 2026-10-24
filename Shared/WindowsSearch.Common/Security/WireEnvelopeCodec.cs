using System.Security.Cryptography;

namespace WindowsSearch.Common.Security;

public static class WireEnvelopeCodec
{
    public const byte CurrentVersion = 0x01;
    public const int NonceLength = 12;
    public const int TagLength = 16;
    public const int AeadHeaderAndOverhead = 2 + NonceLength + TagLength; // 30 bytes
    public const int CbcIvLength = 16;
    public const int CbcHmacTagLength = 32;
    public const int CbcHeaderAndOverhead = 2 + CbcIvLength + CbcHmacTagLength; // 50 bytes

    public static byte[] Encrypt(ReadOnlySpan<byte> plaintext, EncryptionKind kind, byte[] masterKey)
    {
        if (kind == EncryptionKind.None)
        {
            var noneEnvelope = new byte[2 + plaintext.Length];
            noneEnvelope[0] = CurrentVersion;
            noneEnvelope[1] = (byte)EncryptionKind.None;
            plaintext.CopyTo(noneEnvelope.AsSpan(2));
            return noneEnvelope;
        }

        ArgumentNullException.ThrowIfNull(masterKey);
        if (masterKey.Length == 0)
        {
            throw new ArgumentException("Master key cannot be empty.", nameof(masterKey));
        }

        return kind switch
        {
            EncryptionKind.Aes128Gcm => EncryptAead(plaintext, kind, KeyManager.DeriveSubkey(masterKey, "WindowsSearch-Aes128Gcm", 16), AeadCipherType.AesGcm),
            EncryptionKind.Aes256Gcm => EncryptAead(plaintext, kind, KeyManager.DeriveSubkey(masterKey, "WindowsSearch-Aes256Gcm", 32), AeadCipherType.AesGcm),
            EncryptionKind.Aes256Ccm => EncryptAead(plaintext, kind, KeyManager.DeriveSubkey(masterKey, "WindowsSearch-Aes256Ccm", 32), AeadCipherType.AesCcm),
            EncryptionKind.ChaCha20Poly1305 => EncryptAead(plaintext, kind, KeyManager.DeriveSubkey(masterKey, "WindowsSearch-ChaCha20Poly1305", 32), AeadCipherType.ChaCha20Poly1305),
            EncryptionKind.Aes256CbcHmac => EncryptAes256CbcHmac(plaintext, masterKey),
            _ => throw new NotSupportedException($"Unsupported encryption kind: {kind}")
        };
    }

    public static byte[] Decrypt(ReadOnlySpan<byte> envelopeBytes, EncryptionKind expectedKind, byte[] masterKey)
    {
        if (envelopeBytes.Length < 2)
        {
            throw new CryptographicException($"Envelope is too short ({envelopeBytes.Length} bytes). Minimum header is 2 bytes.");
        }

        byte version = envelopeBytes[0];
        if (version != CurrentVersion)
        {
            throw new CryptographicException($"Unsupported envelope version: {version}. Expected: {CurrentVersion}.");
        }

        var envelopeKind = (EncryptionKind)envelopeBytes[1];
        if (expectedKind != EncryptionKind.None && envelopeKind != expectedKind)
        {
            throw new CryptographicException($"Envelope encryption kind '{envelopeKind}' does not match expected kind '{expectedKind}'.");
        }

        if (envelopeKind == EncryptionKind.None)
        {
            return envelopeBytes[2..].ToArray();
        }

        ArgumentNullException.ThrowIfNull(masterKey);
        if (masterKey.Length == 0)
        {
            throw new ArgumentException("Master key cannot be empty.", nameof(masterKey));
        }

        return envelopeKind switch
        {
            EncryptionKind.Aes128Gcm => DecryptAead(envelopeBytes, KeyManager.DeriveSubkey(masterKey, "WindowsSearch-Aes128Gcm", 16), AeadCipherType.AesGcm),
            EncryptionKind.Aes256Gcm => DecryptAead(envelopeBytes, KeyManager.DeriveSubkey(masterKey, "WindowsSearch-Aes256Gcm", 32), AeadCipherType.AesGcm),
            EncryptionKind.Aes256Ccm => DecryptAead(envelopeBytes, KeyManager.DeriveSubkey(masterKey, "WindowsSearch-Aes256Ccm", 32), AeadCipherType.AesCcm),
            EncryptionKind.ChaCha20Poly1305 => DecryptAead(envelopeBytes, KeyManager.DeriveSubkey(masterKey, "WindowsSearch-ChaCha20Poly1305", 32), AeadCipherType.ChaCha20Poly1305),
            EncryptionKind.Aes256CbcHmac => DecryptAes256CbcHmac(envelopeBytes, masterKey),
            _ => throw new NotSupportedException($"Unsupported encryption kind in envelope: {envelopeKind}")
        };
    }

    public static string FormatEnvelopeForLog(ReadOnlySpan<byte> envelopeBytes, EncryptionKind kind)
    {
        if (envelopeBytes.IsEmpty)
        {
            return $"[{kind} envelope: 0 bytes (empty)]";
        }

        int previewLength = Math.Min(24, envelopeBytes.Length);
        string hexPreview = Convert.ToHexString(envelopeBytes[..previewLength]);
        string suffix = envelopeBytes.Length > previewLength ? "..." : string.Empty;
        return $"[{kind} envelope: {envelopeBytes.Length} bytes | Hex: {hexPreview}{suffix}]";
    }

    private enum AeadCipherType
    {
        AesGcm,
        AesCcm,
        ChaCha20Poly1305
    }

    private static byte[] EncryptAead(ReadOnlySpan<byte> plaintext, EncryptionKind kind, byte[] subkey, AeadCipherType cipherType)
    {
        int envelopeLength = AeadHeaderAndOverhead + plaintext.Length;
        var envelope = new byte[envelopeLength];

        envelope[0] = CurrentVersion;
        envelope[1] = (byte)kind;

        var headerSpan = envelope.AsSpan(0, 2);
        var nonceSpan = envelope.AsSpan(2, NonceLength);
        var tagSpan = envelope.AsSpan(2 + NonceLength, TagLength);
        var ciphertextSpan = envelope.AsSpan(AeadHeaderAndOverhead, plaintext.Length);

        RandomNumberGenerator.Fill(nonceSpan);

        switch (cipherType)
        {
            case AeadCipherType.AesGcm:
                using (var aesGcm = new AesGcm(subkey, TagLength))
                {
                    aesGcm.Encrypt(nonceSpan, plaintext, ciphertextSpan, tagSpan, headerSpan);
                }
                break;

            case AeadCipherType.AesCcm:
                using (var aesCcm = new AesCcm(subkey))
                {
                    aesCcm.Encrypt(nonceSpan, plaintext, ciphertextSpan, tagSpan, headerSpan);
                }
                break;

            case AeadCipherType.ChaCha20Poly1305:
                using (var chaCha = new ChaCha20Poly1305(subkey))
                {
                    chaCha.Encrypt(nonceSpan, plaintext, ciphertextSpan, tagSpan, headerSpan);
                }
                break;

            default:
                throw new NotSupportedException($"Unsupported AEAD cipher type: {cipherType}");
        }

        return envelope;
    }

    private static byte[] DecryptAead(ReadOnlySpan<byte> envelopeBytes, byte[] subkey, AeadCipherType cipherType)
    {
        if (envelopeBytes.Length < AeadHeaderAndOverhead)
        {
            throw new CryptographicException($"Envelope too short ({envelopeBytes.Length} bytes). Minimum for AEAD is {AeadHeaderAndOverhead} bytes.");
        }

        var headerSpan = envelopeBytes[..2];
        var nonceSpan = envelopeBytes.Slice(2, NonceLength);
        var tagSpan = envelopeBytes.Slice(2 + NonceLength, TagLength);
        var ciphertextSpan = envelopeBytes[AeadHeaderAndOverhead..];

        var plaintext = new byte[ciphertextSpan.Length];

        switch (cipherType)
        {
            case AeadCipherType.AesGcm:
                using (var aesGcm = new AesGcm(subkey, TagLength))
                {
                    aesGcm.Decrypt(nonceSpan, ciphertextSpan, tagSpan, plaintext, headerSpan);
                }
                break;

            case AeadCipherType.AesCcm:
                using (var aesCcm = new AesCcm(subkey))
                {
                    aesCcm.Decrypt(nonceSpan, ciphertextSpan, tagSpan, plaintext, headerSpan);
                }
                break;

            case AeadCipherType.ChaCha20Poly1305:
                using (var chaCha = new ChaCha20Poly1305(subkey))
                {
                    chaCha.Decrypt(nonceSpan, ciphertextSpan, tagSpan, plaintext, headerSpan);
                }
                break;

            default:
                throw new NotSupportedException($"Unsupported AEAD cipher type: {cipherType}");
        }

        return plaintext;
    }

    private static byte[] EncryptAes256CbcHmac(ReadOnlySpan<byte> plaintext, byte[] masterKey)
    {
        var derivedKeys = KeyManager.DeriveSubkey(masterKey, "WindowsSearch-Aes256Cbc", 64);
        var encKey = derivedKeys.AsSpan(0, 32).ToArray();
        var hmacKey = derivedKeys.AsSpan(32, 32).ToArray();

        var iv = new byte[CbcIvLength];
        RandomNumberGenerator.Fill(iv);

        byte[] ciphertext;
        using (var aes = Aes.Create())
        {
            aes.Key = encKey;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            using var encryptor = aes.CreateEncryptor();
            ciphertext = encryptor.TransformFinalBlock(plaintext.ToArray(), 0, plaintext.Length);
        }

        int envelopeLength = CbcHeaderAndOverhead + ciphertext.Length;
        var envelope = new byte[envelopeLength];
        envelope[0] = CurrentVersion;
        envelope[1] = (byte)EncryptionKind.Aes256CbcHmac;

        iv.CopyTo(envelope.AsSpan(2, CbcIvLength));
        ciphertext.CopyTo(envelope.AsSpan(CbcHeaderAndOverhead));

        using (var hmac = new HMACSHA256(hmacKey))
        {
            hmac.TransformBlock(envelope, 0, 2, null, 0); // Header
            hmac.TransformBlock(envelope, 2, CbcIvLength, null, 0); // IV
            hmac.TransformFinalBlock(ciphertext, 0, ciphertext.Length); // Ciphertext
            hmac.Hash!.CopyTo(envelope.AsSpan(2 + CbcIvLength, CbcHmacTagLength));
        }

        return envelope;
    }

    private static byte[] DecryptAes256CbcHmac(ReadOnlySpan<byte> envelopeBytes, byte[] masterKey)
    {
        if (envelopeBytes.Length < CbcHeaderAndOverhead)
        {
            throw new CryptographicException($"Envelope too short ({envelopeBytes.Length} bytes). Minimum for CBC-HMAC is {CbcHeaderAndOverhead} bytes.");
        }

        var derivedKeys = KeyManager.DeriveSubkey(masterKey, "WindowsSearch-Aes256Cbc", 64);
        var encKey = derivedKeys.AsSpan(0, 32).ToArray();
        var hmacKey = derivedKeys.AsSpan(32, 32).ToArray();

        var iv = envelopeBytes.Slice(2, CbcIvLength).ToArray();
        var expectedTag = envelopeBytes.Slice(2 + CbcIvLength, CbcHmacTagLength);
        var ciphertextSpan = envelopeBytes[CbcHeaderAndOverhead..];

        using (var hmac = new HMACSHA256(hmacKey))
        {
            var headerBytes = envelopeBytes[..2].ToArray();
            hmac.TransformBlock(headerBytes, 0, 2, null, 0);
            hmac.TransformBlock(iv, 0, CbcIvLength, null, 0);
            var ciphertextBytes = ciphertextSpan.ToArray();
            hmac.TransformFinalBlock(ciphertextBytes, 0, ciphertextBytes.Length);
            byte[] computedTag = hmac.Hash!;

            if (!CryptographicOperations.FixedTimeEquals(computedTag, expectedTag))
            {
                throw new CryptographicException("HMAC-SHA256 verification failed; envelope data was tampered with.");
            }
        }

        using (var aes = Aes.Create())
        {
            aes.Key = encKey;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(ciphertextSpan.ToArray(), 0, ciphertextSpan.Length);
        }
    }
}
