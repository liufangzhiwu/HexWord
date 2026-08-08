#if UNITY_OPENHARMONY
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using JWT;
using JWT.Algorithms;
using JWT.Serializers;
using UnityEngine;
using UnityEngine.Networking;
using OpenHarmonyKits.Signal;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;

namespace Middleware
{
    public class Push_harmony : IPushs
    {
        public string pushToken { get; set; }

        public void Init(float delay)
        {
            UnityTimer.Delay(delay, () =>
            {
                RequestEnableNotification();

                // 注册Token获取信号回调
                SignalHandler.Instance.RegisterSignalDelegate<Push_GetTokenSignal>(OnGetTokenTrigger);

                GetToken();
            });
        }

        private void OnDestroy()
        {
            if (SignalHandler.Instance != null)
            {
                SignalHandler.Instance.UnRegisterSignalDelegate<Push_GetTokenSignal>(OnGetTokenTrigger);
            }
        }

        /// <summary>
        /// 申请消息推送权限
        /// </summary>
        public void RequestEnableNotification()
        {
            OHSDKKitManager.Instance.RequestEnableNotification();
        }

        /// <summary>
        /// 获取Push Token
        /// </summary>
        public void GetToken()
        {
            OHSDKKitManager.Instance.GetPushToken();
        }

        /// <summary>
        /// 发送推送消息（需先获取到Token）
        /// </summary>
        /// <param name="title">通知标题</param>
        /// <param name="body">通知内容</param>
        public void Push(string title, string body)
        {
            if (string.IsNullOrEmpty(pushToken))
            {
                Debug.LogError("Push Token is null or empty. Please call GetToken() first.");
                return;
            }

            Game.self.StartCoroutine(SendPostRequest(title, body));
        }

        /// <summary>
        /// Token获取回调
        /// </summary>
        private void OnGetTokenTrigger(SignalBase signal)
        {
            if (!signal.hasError())
            {
                Push_GetTokenSignal targetSignal = (Push_GetTokenSignal)signal;
                pushToken = targetSignal.pushToken;
                Debug.Log($"[PushManager] GetToken Success. Token: {pushToken}");
                GameDataManager.Instance.UserData.PushToken = pushToken;
                // 可将Token上报至游戏服务端（需自行实现）
                //UploadTokenToServer(pushToken);
            }
            else
            {
                Debug.LogError($"[PushManager] GetToken Error. Code: {signal.code}, Message: {signal.message}");
            }
        }

        /// <summary>
        /// 向华为Push Kit服务端发送推送请求（模拟服务端行为）
        /// </summary>
        IEnumerator SendPostRequest(string title, string body)
        {
            // 设置用于JWT签名的公私钥（请替换为您自己的密钥）
            OHPushHelper.PrivateKeyPem =
                @"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"
                    .Replace("\n", "");
            OHPushHelper.PublicKeyPem =
                @"MIICIjANBgkqhkiG9w0BAQEFAAOCAg8AMIICCgKCAgEApqRX1ByaQznlOCfBPTZR52lVjEw1pU9YbV0f1Gxwz2YQrdIpQuq2RuxB/gnPVt7MatibloyxGxJuOR4AZspUpWTRCoQb3IFiZ09ZjjoKa/AmnUWM++FvCTiGoZ1xG5SHcf77H+cuc0BEjfh4giXQWzSsQLtK+hH1sfjJIsf6qWPRa/JJkZFFdfFIB19AimdM6kGtIZ64LOeS/UQFITa3Q1MTvjNICnc3QS9Oo6dqyM/etOS8U8Ka3N904RqJuGkgzdbda54QF1tk7hN3OCeZipHoDd/A73xT5ByYE1xkKAjB5QH3u4nVB/qUEF3pbFQiD2Gaw0UuOHJQ8Kvds+gkJ6KHe6zba9tOeIwRsji/2Iu7C4d83FbKs7LkazXyJnKMgc40AqvNTR9TrYkUKJ1dpzu/Gt0zYfkUWjoA60nLZ2vAlnQwwx41O47z2/pvhRg9iOJiWKypXVgVQ6iYG9qkGdRsbp+DDmHdC+wfaUQBA63pk8VQcE6D0f2ujF9k4KEFYP1q84zltqdsee++4JibaT6cIJ0C3kxM+u5PLgob1xhO6AD329lBHipNH/tYyez5cLh+vMq05GTkgt3rG+6pXL0W+ClMegRNVIjyjpyN93ozVeEFguW67YI8vQi9PvP2UCYrh2A7Mnd85feLVEwO1Qm6lpX7cJpZAuZq6cb2ONkCAwEAAQ=="
                    .Replace("\n", "");

            // 请求URL
            string url = "https://push-api.cloud.huawei.com/v3/101653523862654451/messages:send";
            string aud = "https://oauth-login.cloud.huawei.com/oauth2/v3/token";
          
            
            PostRequest requestBody = new PostRequest();
            requestBody.payload.notification.title = title;
            requestBody.payload.notification.body = body;
            requestBody.target.token = new string[] { pushToken };
            requestBody.payload.notification.clickAction = new ClickAction();
            string json = JsonUtility.ToJson(requestBody);
            Debug.Log(json);
            UnityWebRequest request = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("push-type", "0");
            long iat = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            long exp = (long)(DateTime.UtcNow.AddSeconds(3600) - new DateTime(1970, 1, 1)).TotalSeconds;
            var payload = new Dictionary<string, object>
            {
                {"aud",aud },
                {"iss","118571447" },
                {"exp",exp },
                {"iat",iat }
            };
            string privateKeyStr = ConvertPrivateKeyPkcs8ToPcks1(OHPushHelper.PrivateKeyPem);
            RSA publicKey = CreateRsaFromPublicKey(OHPushHelper.PublicKeyPem);
            RSA privateKey = CreateRsaFromPrivateKey(privateKeyStr);

            IJwtAlgorithm algorithm = new RS256Algorithm(publicKey, privateKey);
            IJsonSerializer serializer = new JsonNetSerializer();
            IBase64UrlEncoder urlEncoder = new JwtBase64UrlEncoder();
            IJwtEncoder encoder = new JwtEncoder(algorithm, serializer, urlEncoder);
            IDictionary<string, object> extraHeaders = new Dictionary<string, object>();

            extraHeaders["kid"] = "a6ace628adc6485e9a6b480a45c033f4";
            extraHeaders["typ"] = "JWT";

            string key = "";
            var token = encoder.Encode(extraHeaders, payload, key);
            Debug.Log(token);
            string Authorization = "Bearer " + token;
            request.SetRequestHeader("Authorization", Authorization);
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"[PushManager] Push Error: {request.error}");
            }
            else
            {
                Debug.Log($"[PushManager] Push Response: {request.downloadHandler.text}");
            }
        }
        
        private static RSA CreateRsaFromPrivateKey(string privateKey)
        {
            var privateKeyBits = System.Convert.FromBase64String(privateKey);
            var rsa = RSA.Create();
            var RSAparams = new RSAParameters();

            using (var binr = new BinaryReader(new MemoryStream(privateKeyBits)))
            {
                byte bt = 0;
                ushort twobytes = 0;
                twobytes = binr.ReadUInt16();
                if (twobytes == 0x8130)
                    binr.ReadByte();
                else if (twobytes == 0x8230)
                    binr.ReadInt16();
                else
                    throw new Exception("Unexpected value read binr.ReadUInt16()");

                twobytes = binr.ReadUInt16();
                if (twobytes != 0x0102)
                    throw new Exception("Unexpected version");

                bt = binr.ReadByte();
                if (bt != 0x00)
                    throw new Exception("Unexpected value read binr.ReadByte()");
                RSAparams.Modulus = binr.ReadBytes(GetIntegerSize(binr));
                RSAparams.Exponent = binr.ReadBytes(GetIntegerSize(binr));
                RSAparams.D = binr.ReadBytes(GetIntegerSize(binr));
                RSAparams.P = binr.ReadBytes(GetIntegerSize(binr));
                RSAparams.Q = binr.ReadBytes(GetIntegerSize(binr));
                RSAparams.DP = binr.ReadBytes(GetIntegerSize(binr));
                RSAparams.DQ = binr.ReadBytes(GetIntegerSize(binr));
                RSAparams.InverseQ = binr.ReadBytes(GetIntegerSize(binr));
            }
            rsa.ImportParameters(RSAparams);
            return rsa;
        }
        
        private static int GetIntegerSize(BinaryReader binr)
        {
            byte bt = 0;
            int count = 0;
            bt = binr.ReadByte();
            if (bt != 0x02)
                return 0;
            bt = binr.ReadByte();

            if (bt == 0x81)
                count = binr.ReadByte();
            else
            if (bt == 0x82)
            {
                var highbyte = binr.ReadByte();
                var lowbyte = binr.ReadByte();
                byte[] modint = { lowbyte, highbyte, 0x00, 0x00 };
                count = BitConverter.ToInt32(modint, 0);
            }
            else
            {
                count = bt;
            }

            while (binr.ReadByte() == 0x00)
            {
                count -= 1;
            }
            binr.BaseStream.Seek(-1, SeekOrigin.Current);
            return count;
        }

         public static RSA CreateRsaFromPublicKey(string publicKeyString)
    {
        byte[] SeqOID = { 0x30, 0x0D, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01, 0x05, 0x00 };
        byte[] x509key;
        byte[] seq = new byte[15];
        int x509size;

        x509key = Convert.FromBase64String(publicKeyString);
        x509size = x509key.Length;

        using (var mem = new MemoryStream(x509key))
        {
            using (var binr = new BinaryReader(mem))
            {
                byte bt = 0;
                ushort twobytes = 0;

                twobytes = binr.ReadUInt16();
                if (twobytes == 0x8130)
                    binr.ReadByte();
                else if (twobytes == 0x8230)
                    binr.ReadInt16();
                else
                    return null;

                seq = binr.ReadBytes(15);
                if (!CompareBytearrays(seq, SeqOID))
                    return null;

                twobytes = binr.ReadUInt16();
                if (twobytes == 0x8103)
                    binr.ReadByte();
                else if (twobytes == 0x8203)
                    binr.ReadInt16();
                else
                    return null;

                bt = binr.ReadByte();
                if (bt != 0x00)
                    return null;

                twobytes = binr.ReadUInt16();
                if (twobytes == 0x8130)
                    binr.ReadByte();
                else if (twobytes == 0x8230)
                    binr.ReadInt16();
                else
                    return null;

                twobytes = binr.ReadUInt16();
                byte lowbyte = 0x00;
                byte highbyte = 0x00;

                if (twobytes == 0x8102)
                    lowbyte = binr.ReadByte();
                else if (twobytes == 0x8202)
                {
                    highbyte = binr.ReadByte();
                    lowbyte = binr.ReadByte();
                }
                else
                    return null;
                byte[] modint = { lowbyte, highbyte, 0x00, 0x00 };
                int modsize = BitConverter.ToInt32(modint, 0);

                int firstbyte = binr.PeekChar();
                if (firstbyte == 0x00)
                {
                    binr.ReadByte();
                    modsize -= 1;
                }

                byte[] modulus = binr.ReadBytes(modsize);

                if (binr.ReadByte() != 0x02)
                    return null;
                int expbytes = (int)binr.ReadByte();
                byte[] exponent = binr.ReadBytes(expbytes);

                var rsa = RSA.Create();
                var rsaKeyInfo = new RSAParameters
                {
                    Modulus = modulus,
                    Exponent = exponent
                };
                rsa.ImportParameters(rsaKeyInfo);
                return rsa;
            }

        }
    }
         
    private static bool CompareBytearrays(byte[] a, byte[] b)
    {
        if (a.Length != b.Length)
            return false;
        int i = 0;
        foreach (byte c in a)
        {
            if (c != b[i])
                return false;
            i++;
        }
        return true;
    }

        public string ConvertPrivateKeyPkcs8ToPcks1(string privateKey)
        {
            return Convert.ToBase64String(ConvertPrivateKeyPkcs8ToPcks1(Convert.FromBase64String(privateKey)));
        }

        public static byte[] ConvertPrivateKeyPkcs8ToPcks1(byte[] privateKey)
        {
            RsaPrivateCrtKeyParameters privateKeyParam =
                (RsaPrivateCrtKeyParameters)PrivateKeyFactory.CreateKey(privateKey);
            PrivateKeyInfo pkInfo = PrivateKeyInfoFactory.CreatePrivateKeyInfo(privateKeyParam);
            return pkInfo.ParsePrivateKey().ToAsn1Object().GetEncoded();
        }
    }
}
#endif