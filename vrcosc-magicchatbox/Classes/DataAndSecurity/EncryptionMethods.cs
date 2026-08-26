using System;
using System.Security.Cryptography;
using System.Text;

namespace vrcosc_magicchatbox.Classes.DataAndSecurity;

internal static class EncryptionMethods
{
    public static string? DecryptString(string cipherText)
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

    public static string? EncryptString(string plainText)
    {
        try
        {
            if (string.IsNullOrEmpty(plainText))
                return null;

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] encryptedBytes = ProtectedData.Protect(
                plainBytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedBytes);
        }
        catch (CryptographicException ex)
        {
            Logging.WriteInfo($"Encryption failed (token may need re-entry): {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return null;
        }
    }

    public static bool TryProcessToken(ref string source, ref string destination, bool isEncryption)
    {
        try
        {
            if (string.IsNullOrEmpty(source))
            {
                destination = string.Empty;
                return true;
            }

            string? result = isEncryption ? EncryptString(source) : DecryptString(source);
            destination = result ?? string.Empty;

            // Success is decided by the result, not by the coalesced destination:
            // a value that legitimately round-trips to an empty string still succeeded.
            return result != null;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            destination = string.Empty;
            return false;
        }
    }
}
