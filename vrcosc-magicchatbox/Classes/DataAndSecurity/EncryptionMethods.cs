using System;
using System.Security.Cryptography;
using System.Text;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.DataAndSecurity;

internal static class EncryptionMethods
{

    public static string DecryptString(string cipherText)
    {
        try
        {
            byte[] iv = new byte[16];
            byte[] buffer = Convert.FromBase64String(cipherText);

            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(ViewModel.Instance.aesKey);
                aes.IV = iv;

                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                using (System.IO.MemoryStream memoryStream = new System.IO.MemoryStream(buffer))
                {
                    using (CryptoStream cryptoStream = new CryptoStream((System.IO.Stream)memoryStream, decryptor, CryptoStreamMode.Read))
                    {
                        using (System.IO.StreamReader streamReader = new System.IO.StreamReader((System.IO.Stream)cryptoStream))
                        {
                            return streamReader.ReadToEnd();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {

            Logging.WriteException(ex, MSGBox: false);
            return null;
        }

    }

    public static string EncryptString(string plainText)
    {
        byte[] iv = new byte[16];
        byte[] array;

        using (Aes aes = Aes.Create())
        {
            aes.Key = Encoding.UTF8.GetBytes(ViewModel.Instance.aesKey);
            aes.IV = iv;

            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            using (System.IO.MemoryStream memoryStream = new System.IO.MemoryStream())
            {
                using (CryptoStream cryptoStream = new CryptoStream((System.IO.Stream)memoryStream, encryptor, CryptoStreamMode.Write))
                {
                    using (System.IO.StreamWriter streamWriter = new System.IO.StreamWriter((System.IO.Stream)cryptoStream))
                    {
                        streamWriter.Write(plainText);
                    }

                    array = memoryStream.ToArray();
                }
            }
        }

        return Convert.ToBase64String(array);
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
            return true;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            destination = null;
            return false;
        }
    }
}
