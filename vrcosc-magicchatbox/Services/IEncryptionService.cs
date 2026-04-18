namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Abstraction for encrypting/decrypting sensitive data (tokens, credentials).
/// Default implementation uses DPAPI bound to the current Windows user.
/// </summary>
public interface IEncryptionService
{
    string? Encrypt(string plainText);
    string? Decrypt(string cipherText);
}
