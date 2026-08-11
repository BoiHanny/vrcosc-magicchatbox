namespace vrcosc_magicchatbox.Services;

public interface IEncryptionService
{
    string? Encrypt(string plainText);
    string? Decrypt(string cipherText);
}
