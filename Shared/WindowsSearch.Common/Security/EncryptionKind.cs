namespace WindowsSearch.Common.Security;

public enum EncryptionKind
{
    None = 0,
    Aes128Gcm = 1,
    Aes256Gcm = 2,
    Aes256Ccm = 3,
    Aes256CbcHmac = 4,
    ChaCha20Poly1305 = 5
}
