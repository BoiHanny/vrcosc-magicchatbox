using System;
using System.Security.Cryptography;
using System.Text;

namespace vrcosc_magicchatbox.Classes.DataAndSecurity;

/// <summary>
/// Provides encryption/decryption using Windows DPAPI (ProtectedData).
/// Data is bound to the current Windows user — no hardcoded keys or IVs.
/// </summary>
internal static class EncryptionMethods
{
    public static string DecryptString(string cipherText)
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
        catch (CryptographicException ex)
        {
            // Old AES-encrypted data or corrupted — token must be re-entered
            Logging.WriteInfo($"Decryption failed (token may need re-entry): {ex.Message}");
            return null;
        }
        catch (FormatException ex)
        {
            Logging.WriteInfo($"Decryption failed (invalid base64): {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return null;
        }
    }

    public static string EncryptString(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return null;

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] encryptedBytes = ProtectedData.Protect(
            plainBytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encryptedBytes);
    }

    public static bool TryProcessToken(ref string source, ref string destination, bool isEncryption)
    {
        try
        {
            if (string.IsNullOrEmpty(source))
            {
                destination = null;
                return true;
            }

            destination = isEncryption ? EncryptString(source) : DecryptString(source);
            return destination != null;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            destination = null;
            return false;
        }
    }
}
