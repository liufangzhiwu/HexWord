using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public static class SecurityProvider
{

#if UNITY_IOS
    // 使用更复杂的密钥，包含大小写字母、数字和特殊字符
    private static readonly byte[] DynamicKey = Encoding.UTF8.GetBytes("iewoiwefrewweveuiomakrynbvuinwzx"); // 32 字节(英文字母)密钥
    private static readonly byte[] DynamicIV  = Encoding.UTF8.GetBytes("1238415634084738"); // 16 字节初始化向量    
#else
    // 使用更复杂的密钥，包含大小写字母、数字和特殊字符
    private static readonly byte[] DynamicKey = Encoding.UTF8.GetBytes("k7#mp!9z@2$vq5&s8*yd6%g4^hj3iewp"); // 32 字节(英文字母)密钥
    private static readonly byte[] DynamicIV = Encoding.UTF8.GetBytes("x5!k9@m3#p7$v2&0"); // 16 字节初始化向量
#endif

    /// <summary>
    /// 是否开启加密
    /// </summary>
    private static bool SECURITY_ENABLED = true;

    /// <summary>
    /// 加密AES算法
    /// </summary>
    /// <param name="plainText"></param>
    /// <returns></returns>
    /// <summary>
    /// 保护数据（替代Encrypt）
    /// </summary>
    public static string ProtectData(string plainContent)
    {
        if (!SECURITY_ENABLED) return plainContent;

        using (var crypto = CreateCryptoProvider())
        using (var encryptor = crypto.CreateEncryptor(DynamicKey, DynamicIV))
        using (var memoryStream = new MemoryStream())
        {
            using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
            using (var writer = new StreamWriter(cryptoStream))
            {
                writer.Write(plainContent);
            }
            return Convert.ToBase64String(memoryStream.ToArray());
        }
    }

    /// <summary>
    /// 解密
    /// </summary>
    /// <param name="cipherText"></param>
    /// <returns></returns>
    /// <summary>
    /// 恢复数据（替代Decrypt）
    /// </summary>
    public static string RestoreData(string protectedContent)
    {
        if (!SECURITY_ENABLED) return protectedContent;
        if (string.IsNullOrEmpty(protectedContent)) return protectedContent;

        try
        {
            using (var crypto = CreateCryptoProvider())
            using (var decryptor = crypto.CreateDecryptor(DynamicKey, DynamicIV))
            using (var memoryStream = new MemoryStream(Convert.FromBase64String(protectedContent)))
            using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
            using (var reader = new StreamReader(cryptoStream))
            {
                return reader.ReadToEnd();
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 创建安全的加密提供器
    /// </summary>
    private static Aes CreateCryptoProvider()
    {
        var provider = Aes.Create();
        provider.Mode = CipherMode.CBC;
        provider.Padding = PaddingMode.PKCS7;
        provider.KeySize = 256;
        provider.BlockSize = 128;
        return provider;
    }

    /// <summary>
    /// 使用AES算法对给定的明文字符串进行加密。
    /// </summary>
    /// <param name="plainText">需要加密的明文字符串。</param>
    /// <returns>返回加密后的字符串（通常为Base64编码形式）。</returns>
    /// <summary>
    /// 保护字节数据（替代Encrypt字节版本）
    /// </summary>
    public static byte[] SecureBytes(byte[] input)
    {
        if (!SECURITY_ENABLED) return input;
        return TransformData(input, true);
    }

    /// <summary>
    /// 使用AES算法对给定的密文数据进行解密。
    /// </summary>
    /// <param name="cipherText">需要解密的密文数据（字节数组形式）。</param>
    /// <returns>返回解密后的明文数据（字节数组形式）。</returns>
    /// <summary>
    /// 恢复字节数据（替代Decrypt字节版本）
    /// </summary>
    public static byte[] RecoverBytes(byte[] input)
    {
        if (!SECURITY_ENABLED) return input;
        return TransformData(input, false);
    }


    /// <summary>
    /// 数据转换核心
    /// </summary>
    private static byte[] TransformData(byte[] data, bool encrypt)
    {
        if (data == null || data.Length == 0) return data;

        using (var crypto = CreateCryptoProvider())
        {
            var transform = encrypt
                ? crypto.CreateEncryptor(DynamicKey, DynamicIV)
                : crypto.CreateDecryptor(DynamicKey, DynamicIV);

            using (var memoryStream = new MemoryStream())
            using (var cryptoStream = new CryptoStream(memoryStream, transform, CryptoStreamMode.Write))
            {
                cryptoStream.Write(data, 0, data.Length);
                cryptoStream.FlushFinalBlock();
                return memoryStream.ToArray();
            }
        }
    }


}

