using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using OpenHarmonyKits.Signal;

namespace Middleware
{
    public class Push_harmony : IPushs
    {
        private string pushToken;

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

            //StartCoroutine(SendPostRequest(title, body));
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
                // 可将Token上报至游戏服务端（需自行实现）
                // UploadTokenToServer(pushToken);
            }
            else
            {
                Debug.LogError($"[PushManager] GetToken Error. Code: {signal.code}, Message: {signal.message}");
            }
        }

        /// <summary>
        /// 向华为Push Kit服务端发送推送请求（模拟服务端行为）
        /// </summary>
        private IEnumerator SendPostRequest(string title, string body)
        {
            // 设置用于JWT签名的公私钥（请替换为您自己的密钥）
            OHPushHelper.PrivateKeyPem =
                @"MIIJRAIBADANBgkqhkiG9w0BAQEFAASCCS4wggkqAgEAAoICAQCbrWQnW4mxL1Pc257W1B2eZjTzyEYWLd4zpPXLZK8ZCjs8uiW9rRT/Lpkb4E32Y5VFCBmO8+Jp3SLvInUBM15e/KSb1jes1Ew+g31YyeLBl7MpYMJpWMnfAWeNJpdNAdjK1tzwOyxnQ83G/ghhaicQnxMJ1PBLQvRKKrge/nk8rdE1gxxsny7tDZRAaOqAP4X5Dnt3gJXC7ohDgBBCobIG4YingOwEmrxLqEuFE5oIPtu1FoFOV0igW5trBV2hv2qseavRrvip8O0no+F3TSv1S6GUxRwxn1WuWp6vcivoWrRkEI07iWe2/sbYxTtNqrHJEIyTt47F7C3R9pfALH4Wu25ECVByvSqu/AoLWp6pGwSPYGGZcC6BmDkO27D1ZiRdzm21KbfOF3XzdW5N+lzKKWg8k5thYVLnW2O6K/gpdP1K7GF+um32mzWUzSbM4BFDWc1VJyNAiuOKyK8xij28Cd62EV06LUb+1RK96ez4lTnHCq7FsOmkootXGXZxbl0tUvmitii6H642qnj+asXIar5yrRD0s1bJMfp4bLmiqTiDXCzPvmE4I+nmeG+WXOvfPypNY6pssuXpb0Efi/Jan9UwOjfvmI64V0fvos+f3XC6c7Zaoh8/YLqTRdYkfKQ/ffDHosYnNkGUcJbejxVh5+9UhjR7a/8ZDJ4oUGzc2QIDAQABAoICAAtASlM/3eE04vI82yt6lBNtHpmZ2VrkAMGpw+vpwG/mWeanK3AccL6Knx0xJmFOzCx7i+FVhgERruMqkds+26ywxLLQliCDDWmdNjNzD9TfAl9Us2hKtvhLkTDV36x8nC74vWoNVMW31Ejz9iYYNm+Ql58nySAfXCl6EemHibqVc1/TNHwzOwB98T+AdaZEMQoRfmHiJLC8bZO3TzWJmyS9KxO0ERq6Fi6/oImjI4hCBr39POYtD74rML3Y2u4EOBD0EDNs2mLp2zH4uaRWKv2k9C4c2s9JTTd3LC3Q6hR2bcAnpBVv9lN28WccRg90bTnzXL2WUBbE3YDlugz1Cj28fd52UF+ss33gcfiuUje3Y18/OwoQwz1nOfHwLV11lMJHGUYh1/vT58mATGT+sEdYiuSZj0+CinDZ5bIZ/UoXKInHfohr0FhQrXOrvdUqoAEIGKrMLN3z8t82lUcLjRcSeglRGYKbZzZxho+GsSTqfGpkU3cUmC/OJ9sYWoFB59IUQAcqzAapBMfGZF5J2HdyXiUA10i/Yp2OkRaSN4iIikLg91sdMp0LWYhp/7oIHcmjeXR3+4/wuYCUKMe3laVl/GybK0Ay1UVDgE3OVDqJrPXoKDQp4w0+bk64PTPWNsrBg+Il9930ROMqq8NkWUUbWFRcoyptHnMahKDt2kwbAoIBAQDIIjRxVKU8MCK13P/g6F79soZOfJQX7BgpiYvm/lFcu5EbgsYaD4N7b0HG5t+Zzvo4G9t61njYcU+N79MXJyMWXR4b9CQTwhpwaOMjX6d+f9ddKITJR2sk2G08jIbX0NJF3YAaPpo/JZaQtT/9AliX9iZ6AVLnHtlUP8gt2c756kyaclLFYygC+O+THZD8rouUp4qgMlowsAMkcbHucTsXbkkNJyfJgYZhpN9s2FTg+xTHCxPNdT9rz8Ugdt5IUtqtOalsSNTL8MAMTKEArI+BBfXb39fb0+AaDP8/w2BRyMyqtXJKvNLWbk6tz9TTzv+Y/Kga8IcV4l6ArN51XpOPAoIBAQDHIki2gLVQKf+JH3+3tgRp2R85Y3NzsxEQrTuNJt1G4KjRiIa90mmxeiV5yJISVMoWhlakFWjafzQsVQOb3LjXEMLZwrphBAGDbKiEwS2736X5r/sL28XJ4VPJj9ZngdK8yI1hk8Nq6S2jhGB7g4yGXe2m7Rhqi2afTg7Xa10Px4ueLJi54A6Hbgau7ZB+8KiJXOfxZb8lXxTmSY7xLxiulPDDe80qejuHtD6oyGk+xILy+AQVDrnCx3wugPjYv/Ttnh2s5CPZDMOG6un2C49hOjpimCYTpyqNiFuYQFMvEOdq9wjRx46TuGMNSXQJ4SdjtLkRb4OTfacCigFFDzUXAoIBAQDAZHF5yV+XPsb/gdbSRVCcHrUSWpybarHHrJy7kRxyQzY59juu7d7+GHRpW6T03Y6Zxd32dptxp2xNDJInHc6TPy0kvky4Wc7E5XAFn30LFKbavYr5XBjaMNzCam1upyadV1RA5pGtxtq5fiRne4vjehR/ESq7WGKgpTgxK8PXaK//gYukia/7O4hEKxYocztnyrBvVDhzuaErtcjRajTeT30Wkd+jzUp8L91Ba64dIgJVXobI6r/vSqs8jEkfydbC1D3VBSbcbzKQIFJerYS/ChXSK5v9je9P29K9X3sG5Dwsl4Wp51/gF+a3HKaCf+ojDjAkxbl3BkjG9mhc+HBtAoIBAQCPA01LXNZakz8FYMzdyGgVCK0HDyiMUG8SFget0NqcG57ClRWH4ESuBHZDp0tYxPI6CRLSVtnuCesTZ57m4jcRpeT6dYJbSIA5veCtLvvEcNOHpd5bXuQGn8AIAzhNMAyELlhzWqa+8mYniFuueQSEP5L9Dkw5wJHcGThJd77nJT1dRNQOsh5dHyTHnq/mqrmvpTyivprvoQCmfu+cwWEtiKP9EL2BIzX7uPRDTWNgg8sz6fEsml3IyHGkCFYvfHTP7n2LfHFOYX9PNwj9/sFjZ2klA/ZcqPLDoMl/Z7sWE0LQLEh2OKZp6sOgeD9RFRWv4swC/J53X4eBKFHPIiPTAoIBAQCLuFXjXvhoJGmXpx4Np4OLBNSgqM5XPO7hGaiMkPUXlDLkN8r4rKDNTrbr9D0E+qY4zXrYv7xbATrtqnIeuVQ27J1/YbzPDR+KYSS/rUMuVNZsrC2ZRnac8An1jUA512cMnu5cv8NVVtXcGoaD2grk8iifzzDddvYa+h08aCA1RThpHk3ke1JtC+nDnh0u1uY4O/FvqP4a1IvgXbpi+uhfFoTNpoiA6TO9Wr3AFYngSCnv1QArFPQHPFPI5DIF0JgcGnF4YAT5mE5gLeVZ2CrAk0ZnV4UIEnXxD5jul5e5FRMtFP4VHJwhG2KpHoXMxgjuLVrhDYKuhVPGEVRL1ctj"
                    .Replace("\n", "");
            OHPushHelper.PublicKeyPem =
                @"MIICIjANBgkqhkiG9w0BAQEFAAOCAg8AMIICCgKCAgEAm61kJ1uJsS9T3Nue1tQdnmY088hGFi3eM6T1y2SvGQo7PLolva0U/y6ZG+BN9mOVRQgZjvPiad0i7yJ1ATNeXvykm9Y3rNRMPoN9WMniwZezKWDCaVjJ3wFnjSaXTQHYytbc8DssZ0PNxv4IYWonEJ8TCdTwS0L0Siq4Hv55PK3RNYMcbJ8u7Q2UQGjqgD+F+Q57d4CVwu6IQ4AQQqGyBuGIp4DsBJq8S6hLhROaCD7btRaBTldIoFubawVdob9qrHmr0a74qfDtJ6Phd00r9UuhlMUcMZ9Vrlqer3Ir6Fq0ZBCNO4lntv7G2MU7TaqxyRCMk7eOxewt0faXwCx+FrtuRAlQcr0qrvwKC1qeqRsEj2BhmXAugZg5Dtuw9WYkXc5ttSm3zhd183VuTfpcyiloPJObYWFS51tjuiv4KXT9Suxhfrpt9ps1lM0mzOARQ1nNVScjQIrjisivMYo9vAnethFdOi1G/tUSvens+JU5xwquxbDppKKLVxl2cW5dLVL5orYouh+uNqp4/mrFyGq+cq0Q9LNWyTH6eGy5oqk4g1wsz75hOCPp5nhvllzr3z8qTWOqbLLl6W9BH4vyWp/VMDo375iOuFdH76LPn91wunO2WqIfP2C6k0XWJHykP33wx6LGJzZBlHCW3o8VYefvVIY0e2v/GQyeKFBs3NkCAwEAAQ=="
                    .Replace("\n", "");

            // 请求URL（请替换为您自己的项目ID）
            string url = "https://push-api.cloud.huawei.com/v3/388421841222410773/messages:send";
            string aud = "https://oauth-login.cloud.huawei.com/oauth2/v3/token";

            var request = OHPushHelper.GetPushRequest(url, aud, title, body, pushToken);
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
    }
}