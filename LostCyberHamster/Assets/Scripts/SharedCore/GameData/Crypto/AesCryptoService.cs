using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Vues.GameCore
{
    public class AesCryptoService : ICryptoService
    {
        private const string _encryptionKey = "oJgkCMaf4MSVyVez0V8DVA==";

        public string Decrypt(string data)
        {
            // UnityEngine.Debug.Log($"Encrypted data: {data}");
            byte[] encryptedBytes = Convert.FromBase64String(data);

            using (Aes aes = Aes.Create())
            {
                aes.Key = Convert.FromBase64String(_encryptionKey);
                aes.IV = new byte[16]; // Replace with your own initialization vector if needed

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (CryptoStream cryptoStream = new CryptoStream(memoryStream, aes.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cryptoStream.Write(encryptedBytes, 0, encryptedBytes.Length);
                        cryptoStream.FlushFinalBlock();
                    }

                    byte[] decryptedBytes = memoryStream.ToArray();
                    return Encoding.UTF8.GetString(decryptedBytes);
                }
            }
        }

        public string Encrypt(string data)
        {
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);

            using (Aes aes = Aes.Create())
            {
                aes.Key = Convert.FromBase64String(_encryptionKey);
                aes.IV = new byte[16]; // Replace with your own initialization vector if needed

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (CryptoStream cryptoStream = new CryptoStream(memoryStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cryptoStream.Write(dataBytes, 0, dataBytes.Length);
                        cryptoStream.FlushFinalBlock();
                    }

                    byte[] encryptedBytes = memoryStream.ToArray();
                    return Convert.ToBase64String(encryptedBytes);
                }
            }
        }
    }
}