using System.IO;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.IO.Compression;

namespace GameUtils
{
    public class EncryptUtil
    {
        #region 加密方法一 AES
        private static string myKey = "scerit";
        public static string AESEncrypt(string Data)
        {
            MemoryStream mStream = new MemoryStream();
            RijndaelManaged aes = new RijndaelManaged();

            byte[] plainBytes = Encoding.UTF8.GetBytes(Data);
            byte[] bKey = new byte[32];
            Array.Copy(Encoding.UTF8.GetBytes(myKey.PadRight(bKey.Length)), bKey, bKey.Length);

            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.PKCS7;
            aes.KeySize = 128;
            //aes.Key = _key;  
            aes.Key = bKey;
            //aes.IV = _iV;  
            CryptoStream cryptoStream = new CryptoStream(mStream, aes.CreateEncryptor(), CryptoStreamMode.Write);
            try
            {
                cryptoStream.Write(plainBytes, 0, plainBytes.Length);
                cryptoStream.FlushFinalBlock();
                return Convert.ToBase64String(mStream.ToArray());
            }
            finally
            {
                cryptoStream.Close();
                mStream.Close();
                aes.Clear();
            }
        }

        public static string AESDecrypt(string Data)
        {
            byte[] encryptedBytes = Convert.FromBase64String(Data);
            byte[] bKey = new byte[32];
            Array.Copy(Encoding.UTF8.GetBytes(myKey.PadRight(bKey.Length)), bKey, bKey.Length);

            MemoryStream mStream = new MemoryStream(encryptedBytes);
            //mStream.Write( encryptedBytes, 0, encryptedBytes.Length );  
            //mStream.Seek( 0, SeekOrigin.Begin );  
            RijndaelManaged aes = new RijndaelManaged();
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.PKCS7;
            aes.KeySize = 128;
            aes.Key = bKey;
            //aes.IV = _iV;  
            CryptoStream cryptoStream = new CryptoStream(mStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
            try
            {
                byte[] tmp = new byte[encryptedBytes.Length + 32];
                int len = cryptoStream.Read(tmp, 0, encryptedBytes.Length + 32);
                byte[] ret = new byte[len];
                Array.Copy(tmp, 0, ret, 0, len);
                return Encoding.UTF8.GetString(ret);
            }
            finally
            {
                cryptoStream.Close();
                mStream.Close();
                aes.Clear();
            }
        }
        #endregion

        #region 加密方法二  字节取反
        public static byte[] Encrypt(byte[] bytes)
        {
            char[] key = myKey.ToCharArray();
            var len = key.Length;
            for (int i = 0; i < bytes.Length; i++)
            {
                var j = i % len;
                bytes[i] ^= (byte)key[j];
            }
            return bytes;
        }

        public static byte[] Decrypt(byte[] bytes)
        {
            return Encrypt(bytes);
        }
        #endregion

        #region 加密方法三 Base64
        public static string Base64Encode(string source)
        {
            string encode = string.Empty;
            byte[] bytes = Encoding.UTF8.GetBytes(source);
            try
            {
                encode = Convert.ToBase64String(bytes);
            }
            catch
            {
                encode = source;
            }
            return encode;
        }

        public static string Base64Decode(string result)
        {
            string decode = string.Empty;
            byte[] bytes = Convert.FromBase64String(result);
            try
            {
                decode = Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                decode = result;
            }
            return decode;
        }
        #endregion
    }
}


