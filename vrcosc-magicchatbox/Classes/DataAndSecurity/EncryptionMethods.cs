using System;
using System.Text;
using System.Security.Cryptography;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.DataAndSecurity
{
    internal class EncryptionMethods
    {
        private ViewModel _VM;

        public EncryptionMethods(ViewModel vm)
        {
            _VM = vm;
        }

        //public string EncryptString(string plainText)
        //{
        //    byte[] iv = new byte[16];
        //    byte[] array;

        //    using (Aes aes = Aes.Create())
        //    {
        //        aes.Key = Encoding.UTF8.GetBytes(_VM.aesKey);
        //        aes.IV = iv;

        //        ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

        //        using (System.IO.MemoryStream memoryStream = new System.IO.MemoryStream())
        //        {
        //            using (CryptoStream cryptoStream = new CryptoStream((System.IO.Stream)memoryStream, encryptor, CryptoStreamMode.Write))
        //            {
        //                using (System.IO.StreamWriter streamWriter = new System.IO.StreamWriter((System.IO.Stream)cryptoStream))
        //                {
        //                    streamWriter.Write(plainText);
        //                }

        //                array = memoryStream.ToArray();
        //            }
        //        }
        //    }

        //    return Convert.ToBase64String(array);
        //}

        public string DecryptString(string cipherText)
        {
            byte[] iv = new byte[16];
            byte[] buffer = Convert.FromBase64String(cipherText);

            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(_VM.aesKey);
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
    }
}
