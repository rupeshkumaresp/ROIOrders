using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ROI
{
    /// <summary>
    /// ENCRYPTION TO GENERATE THE TOKEN FOR ROI
    /// </summary>
    public class EncryptHelper
    {
        public static string Encrypt(string decryptedString, string key)
        {
            MemoryStream memoryStream = new MemoryStream();
            DESCryptoServiceProvider provider = new DESCryptoServiceProvider
            {
                Key = new ASCIIEncoding().GetBytes(key),
                Mode = CipherMode.ECB,
                Padding = PaddingMode.Zeros
            };
            CryptoStream cryptoStream = new CryptoStream(memoryStream, provider.CreateEncryptor(), CryptoStreamMode.Write);
            StreamWriter writer = new StreamWriter(cryptoStream);
            writer.Write(decryptedString);
            writer.Flush();
            cryptoStream.FlushFinalBlock();
            return Convert.ToBase64String(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
        }

    }
}
