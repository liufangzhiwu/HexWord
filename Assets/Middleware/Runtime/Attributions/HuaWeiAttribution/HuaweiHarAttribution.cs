#if UNITY_OPENHARMONY
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace Middleware
{
    // ============================================================
    // 华为归因主类（鸿蒙版：通过Native插件获取归因信息）
    // ============================================================
    public class HuaweiHarAttribution : IAttribute
    {
        // ---------- 基础成员 ----------
        private string packageName;                     // 应用包名
        private bool initFinished = false;
        private readonly List<Action> pendingActions = new List<Action>();

        // ---------- Token 相关 ----------
        private string accessToken;
        private bool isTokenFetching;

        // ---------- 归参与设备信息 ----------
        private string appId = "6917590527000396765";               // 你的应用ID
        private string clientId = "1979065602840486336"; // API客户端ID
        private string clientSecret = "0F209BBC00F6599D5BED0CD78736F0E9124E8AAA46273EB0B4379D2B8A280197"; // ★ 必须填写
        private string oaid;                             // 设备OAID
        private string callBack;                     // 归因系统返回的 callback

        // ---------- 激活上报状态 ----------
        private bool isActivationReported = false;

        // ---------- 公共方法 ----------

        public void Init(float delay)
        {
            UnityTimer.Delay(delay, () =>
            {
                // 1. 获取包名
                packageName = Application.identifier;

                // 2. 获取OAID
                oaid = GetOAID();
                if (string.IsNullOrEmpty(oaid))
                {
                    Debug.LogWarning("[HuaweiAttr] Failed to get OAID, server events may not work.");
                }
                else
                {
                    Debug.Log("[HuaweiAttr] Get OAID: " + oaid);
                }

                // 3. 标记初始化完成，执行缓存事件
                initFinished = true;
                Debug.Log("[HuaweiAttr] Init completed.");

                lock (pendingActions)
                {
                    foreach (var action in pendingActions)
                    {
                        try { action?.Invoke(); }
                        catch (Exception ex) { Debug.LogError("[HuaweiAttr] Pending action error: " + ex.Message); }
                    }
                    pendingActions.Clear();
                }

                // 4. 异步获取Access Token
                FetchAccessToken();
            });
        }

        // ---------- OAID获取 ----------
        private string GetOAID()
        {
            return Game.self.GetOAID();
        }

        // 在 HuaweiHarAttribution 类中
        public string GetAttributionInfo()
        {
#if UNITY_OPENHARMONY && !UNITY_EDITOR
        try
        {
            // 鸿蒙侧写入的文件路径：Application.persistentDataPath/files/attribution.txt
            string filePath = System.IO.Path.Combine(Application.persistentDataPath, "files", "attribution.txt");
            if (System.IO.File.Exists(filePath))
            {
                string json = System.IO.File.ReadAllText(filePath);
                Debug.Log("[HuaweiAttr] Read attribution from file: " + json);
#if Unity_ShowLog
                 json ="{\"enterAgTime\":\"2026-07-01 10:00:00\",\"installedFinishTime\":\"2026-07-01 10:00:05\",\"startDownloadTime\":\"2026-07-01 09:59:00\",\"referrerEx\":\"channel=huawei\"}";
#endif
                return json;
            }
            else
            {
                Debug.LogWarning("[HuaweiAttr] Attribution file not found: " + filePath);
                string json=null;
#if Unity_ShowLog
                json ="{\"enterAgTime\":\"2026-07-01 10:00:00\",\"installedFinishTime\":\"2026-07-01 10:00:05\",\"startDownloadTime\":\"2026-07-01 09:59:00\",\"referrerEx\":\"channel=huawei\"}";
#endif
                return json;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[HuaweiAttr] Failed to read attribution file: " + e.Message);
            return null;
        }
#else
            Debug.Log("[HuaweiAttr] GetAttributionInfo (Editor mode)");
            // 编辑器下返回测试数据，方便调试
            return "{\"enterAgTime\":\"2026-07-01 10:00:00\",\"installedFinishTime\":\"2026-07-01 10:00:05\",\"startDownloadTime\":\"2026-07-01 09:59:00\",\"referrerEx\":\"channel=huawei\"}";
#endif
        }

        // ---------- 解析归因数据并自动上报激活 ----------
        private void ReportAttribution(string attributionJson)
        {
            if (string.IsNullOrEmpty(attributionJson)) return;

            try
            {
                var data = JsonUtility.FromJson<AttributionData>(attributionJson);
                var dict = new Dictionary<string, string>();

                // 提取时间字段
                if (!string.IsNullOrEmpty(data.enterAgTime))
                    dict["enter_ag_time"] = data.enterAgTime;
                if (!string.IsNullOrEmpty(data.installedFinishTime))
                    dict["installed_finish_time"] = data.installedFinishTime;
                if (!string.IsNullOrEmpty(data.startDownloadTime))
                    dict["start_download_time"] = data.startDownloadTime;

                // 提取trackId中的字段，并保存callback
                if (data.trackId != null)
                {
                    if (!string.IsNullOrEmpty(data.trackId.channel)) dict["channel"] = data.trackId.channel;
                    if (!string.IsNullOrEmpty(data.trackId.taskid)) dict["taskid"] = data.trackId.taskid;
                    if (!string.IsNullOrEmpty(data.trackId.callback))
                    {
                        callBack = data.trackId.callback;   // ★ 存储callback地址
                        dict["callback"] = data.trackId.callback;
                    }
                    if (!string.IsNullOrEmpty(data.trackId.subTaskId)) dict["sub_task_id"] = data.trackId.subTaskId;
                    if (!string.IsNullOrEmpty(data.trackId.RTAID)) dict["rta_id"] = data.trackId.RTAID;
                }

                if (!string.IsNullOrEmpty(data.referrerEx))
                    dict["referrer_ex"] = data.referrerEx;

                Debug.Log("[HuaweiAttr] Attribution data parsed.");

                // 如果callback和oaid都已获取，且尚未上报激活，则自动上报激活
                if (!string.IsNullOrEmpty(callBack) && !string.IsNullOrEmpty(oaid) && !isActivationReported)
                {
                    long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    ReportActivation(now);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[HuaweiAttr] ReportAttribution error: " + e.Message);
            }
        }

        // ---------- 客户端事件上报 ----------
        public void ReportConversion(string eventId)
        {
            if (!initFinished)
            {
                Debug.LogWarning($"[HuaweiAttr] Not initialized, caching event: {eventId}");
                lock (pendingActions)
                {
                    pendingActions.Add(() => ReportConversion(eventId));
                }
                return;
            }
            ReportCommon(eventId);
        }

        // ---------- Access Token获取 ----------
        private void FetchAccessToken()
        {
            if (isTokenFetching || !string.IsNullOrEmpty(accessToken)) return;
            isTokenFetching = true;

            // 假设 HuaweiTokenManager 是一个单例或静态类，提供了 GetAccessToken 方法
            // 用户需要确保其存在并正确实现
            HuaweiTokenManager.self.GetAccessToken(
                (token) =>
                {
                    accessToken = token;
                    isTokenFetching = false;
                    Debug.Log("[HuaweiAttr] Access Token obtained."+accessToken);
                    
                    // 6. 获取归因信息并上报（客户端事件 $AppLaunch）
                    string json = GetAttributionInfo();
                    if (!string.IsNullOrEmpty(json))
                    {
                        Debug.Log("[HuaweiAttr] Attribution data: " + json);
                        ReportAttribution(json);   // 内部会解析 callBack 并自动上报激活（服务端）
                    }
                    else
                    {
                        Debug.LogWarning("[HuaweiAttr] No attribution data received.");
                    }
                    
                    long snow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    ReportStart(snow);
                },
                (error) =>
                {
                    isTokenFetching = false;
                    Debug.LogError("[HuaweiAttr] Failed to get Access Token: " + error);
                }
            );
        }

        // ---------- 服务端事件上报 ----------
        public void ReportActivation(long actionTime)
        {
            if (isActivationReported)
            {
                Debug.LogWarning("[HuaweiAttr] Activation already reported, ignored.");
                return;
            }
            SendServerEvent("0", actionTime);
            SendServerEvent("1", actionTime);
            isActivationReported = true;
        }

        public void ReportCommon(string actionType)
        {
            long snow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            SendServerEvent(actionType, snow);
        }

        public void ReportStart(long actionTime)
        {
            SendServerEvent("2", actionTime);
        }

        public void ReportRetention(long actionTime)
        {
            SendServerEvent("3", actionTime);
        }

        public void ReportPurchase(long actionTime, decimal amount, string currency = "CNY")
        {
            SendServerEvent("4", actionTime, amount, currency);
        }

        // ---------- 服务端事件发送核心方法 ----------
        private void SendServerEvent(string actionType, long actionTime, decimal? amount = null, string currency = null)
        {
            
#if Unity_ShowLog //方便测试
            if (string.IsNullOrEmpty(callBack))
            {
                callBack =
                    "security:4053FFBAAE7109AE51C6DF9A:D0A9FB392680FE8F2BD82C1215873DF84A6C5F5EEE3CBCCA4D56F65F8A08E9DC481BC0BBBDAA0666FD30B8D07CD21A";
            }
#endif
            
            if (string.IsNullOrEmpty(accessToken))
            {
                Debug.LogWarning($"[HuaweiAttr] No access token, event will not be sent. actionType: {actionType}");
                return;
            }
            if (string.IsNullOrEmpty(callBack) || string.IsNullOrEmpty(oaid))
            {
                Debug.LogWarning($"[HuaweiAttr] Missing callBack or OAID, cannot send server event. actionType: {actionType}");
                return;
            }

            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"actionType\":\"{actionType}\",");
            sb.Append($"\"actionTime\":{actionTime},");
            sb.Append($"\"deviceIdType\":\"OAID\",");
            sb.Append($"\"appId\":\"{appId}\",");
            sb.Append($"\"callBack\":\"{callBack}\",");
            sb.Append($"\"deviceId\":\"{oaid}\"");

            if (actionType == "4" && amount.HasValue)
            {
                decimal amountInYuan = amount.Value / 100m;
                string actionParam = $"[{{\"name\":\"付费金额\",\"value\":{amountInYuan}}}]";
                sb.Append($",\"actionParam\":\"{actionParam}\"");
            }
            else if (amount.HasValue)
            {
                sb.Append($",\"amount\":{amount.Value}");
                sb.Append($",\"currency\":\"{currency ?? "CNY"}\"");
            }

            sb.Append("}");
            string json = sb.ToString();

            Debug.Log($"[HuaweiAttr] Sending server event: {json}");
            CoroutineRunner.StartCoroutine(SendServerEventCoroutine(json));
        }

        private IEnumerator SendServerEventCoroutine(string jsonData)
        {
            string url = "https://connect-api.cloud.huawei.com/api/datasource/v1/track/activate";
            
            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + accessToken);
                request.SetRequestHeader("client_id", clientId);

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[HuaweiAttr] Server event reported successfully. Response: {request.downloadHandler.text}");
                }
                else
                {
                    Debug.LogError($"[HuaweiAttr] Server event failed: {request.error} (Response: {request.downloadHandler.text})");
                    if (request.responseCode == 401)
                    {
                        accessToken = null;
                        FetchAccessToken();
                    }
                }
            }
        }

        // ---------- 数据模型 ----------
        [Serializable]
        private class AttributionData
        {
            public string enterAgTime;
            public string installedFinishTime;
            public string startDownloadTime;
            public TrackIdData trackId;
            public string referrerEx;
        }

        [Serializable]
        private class TrackIdData
        {
            public string channel;
            public string callback;
            public string taskid;
            public string subTaskId;
            public string RTAID;
        }

        [Serializable]
        private class TokenResponse
        {
            public string access_token;
            public string expires_in;
            public string token_type;
        }
    }

    // ---------- 鸿蒙Native接口声明 ----------
    internal static class HarmonyNative
    {
        // 注意：需要将 libharmony_native.so 放置在 Unity 项目中
        private const string DllName = "harmony_native";  // 根据实际 .so 名称修改

        [DllImport(DllName)]
        public static extern string GetOAID();

        [DllImport(DllName)]
        public static extern string GetAttributionInfo(string packageName);
    }

    // ---------- 辅助类：协程启动器 ----------
    public static class CoroutineRunner
    {
        private static MonoBehaviour _coroutineHost;

        public static void StartCoroutine(IEnumerator coroutine)
        {
            if (_coroutineHost == null)
            {
                GameObject go = new GameObject("CoroutineRunner");
                _coroutineHost = go.AddComponent<CoroutineRunnerBehaviour>();
                UnityEngine.Object.DontDestroyOnLoad(go);
            }
            _coroutineHost.StartCoroutine(coroutine);
        }

        private class CoroutineRunnerBehaviour : MonoBehaviour { }
    }
}
#endif