using System;
using System.Security.Cryptography;
using System.Text;

namespace vrcosc_magicchatbox.Services;

public sealed class DpapiEncryptionService : IEncryptionService
{
    public string? Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return null;

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] encryptedBytes = ProtectedData.Protect(
            plainBytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encryptedBytes);
    }

    public string? Decrypt(string cipherText)
    {
        try
        {
            if (string.IsNullOrEmpty(cipherText))
                return null;

            byte[] encryptedBytes = Convert.FromBase64String(cipherText);
            byte[] plainBytes = ProtectedData.Unprotect(
                encryptedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
