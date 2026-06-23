#if UNITY_huawei
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Middleware
{
    // ============================================================
    // 3. 华为归因主类（实现 IAttribute 接口）
    // ============================================================
    public class HuaWeiAttribution : IAttribute
    {
        // ---------- 基础成员 ----------
        private AndroidJavaObject activity;
        private string packageName;

        private bool initFinished = false;
        private readonly List<Action> pendingActions = new List<Action>();

        // ---------- Token 相关 ----------
        private string accessToken;
        private bool isTokenFetching;

        // ---------- 归参与设备信息 ----------
        private string appId = "116093983";      // ⚠️ 替换
        private string clientId = "116093983";           // ⚠️ 替换
        private string oaid;
        private string callBack;                           // 归因系统返回的 callback

        // ---------- 激活上报状态 ----------
        private bool isActivationReported = false;

        // ---------- 公共方法 ----------

        /// <summary>
        /// 初始化：延迟后获取 Activity，初始化分析 SDK，获取归因并自动上报激活（服务端）
        /// </summary>
        public void Init(float delay)
        {
            UnityTimer.Delay(delay, () =>
            {
                // 1. 获取 Unity Activity
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                }

                // 2. 获取包名
                packageName = Application.identifier;

                // 3. 获取 OAID（华为设备标识）
                oaid = GetOAID();
                if (string.IsNullOrEmpty(oaid))
                {
                    Debug.LogWarning("[HuaweiAttr] Failed to get OAID, server events may not work.");
                }
                else
                {
                    Debug.Log("[HuaweiAttr] Get OAID: " + oaid);
                }

                // 4. 初始化华为分析服务（客户端 SDK）
                try
                {
                    using (AndroidJavaClass bridge = new AndroidJavaClass(
                        "chengyu.idiom.hexa.zen.andriod.huawei.HuaweiAttributionBridge"))
                    {
                        bridge.CallStatic("initAnalytics", activity);
                    }
                    Debug.Log("[HuaweiAttr] HiAnalytics initialized.");
                }
                catch (Exception e)
                {
                    Debug.LogError("[HuaweiAttr] Init analytics failed: " + e.Message);
                }

                // 5. 标记初始化完成，执行缓存事件
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

                // 7. 异步获取 Access Token（供服务端 API 使用）
                FetchAccessToken();
            });
        }

        // ---------- 归因信息获取（通过 ContentProvider） ----------

        public string GetAttributionInfo()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (activity == null)
            {
                Debug.LogWarning("[HuaweiAttr] Activity not ready, cannot get attribution.");
                return null;
            }
            using (AndroidJavaClass bridge = new AndroidJavaClass(
                "chengyu.idiom.hexa.zen.andriod.huawei.HuaweiAttributionBridge"))
            {
                return bridge.CallStatic<string>("getAttributionInfo", activity, packageName);
            }
#else
            Debug.Log("[HuaweiAttr] GetAttributionInfo (non-Android)");
            return null;
#endif
        }

        // ---------- OAID 获取 ----------

        private string GetOAID()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass adIdClient = new AndroidJavaClass("com.huawei.hms.ads.identifier.AdvertisingIdClient"))
                using (AndroidJavaObject adIdInfo = adIdClient.CallStatic<AndroidJavaObject>("getAdvertisingIdInfo", activity))
                {
                    return adIdInfo.Call<string>("getId");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[HuaweiAttr] Failed to get OAID: " + e.Message);
                return null;
            }
#else
            return null;
#endif
        }

        // ---------- 解析归因数据并自动上报激活（服务端） ----------

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

                // 提取 trackId 中的字段，并保存 callback
                if (data.trackId != null)
                {
                    if (!string.IsNullOrEmpty(data.trackId.channel)) dict["channel"] = data.trackId.channel;
                    if (!string.IsNullOrEmpty(data.trackId.taskid)) dict["taskid"] = data.trackId.taskid;
                    if (!string.IsNullOrEmpty(data.trackId.callback))
                    {
                        callBack = data.trackId.callback;   // ★ 存储 callback
                        dict["callback"] = data.trackId.callback;
                    }
                    if (!string.IsNullOrEmpty(data.trackId.subTaskId)) dict["sub_task_id"] = data.trackId.subTaskId;
                    if (!string.IsNullOrEmpty(data.trackId.RTAID)) dict["rta_id"] = data.trackId.RTAID;
                }

                if (!string.IsNullOrEmpty(data.referrerEx))
                    dict["referrer_ex"] = data.referrerEx;

                // 以客户端事件 "$AppLaunch" 上报归因参数（客户端埋点）
                string paramsJson = ConvertToJson(dict);
                ReportConversion("$AppLaunch", paramsJson);
                long snow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                ReportStart(snow);
                Debug.Log("[HuaweiAttr] Attribution reported via $AppLaunch (client).");

                // ★ 如果 callBack 和 oaid 都已获取，且尚未上报激活，则自动上报激活（服务端）
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

        // ---------- 客户端事件上报（通过 Java 桥接） ----------

        public void ReportConversion(string eventId, string eventParamsJson = null)
        {
            if (!initFinished)
            {
                Debug.LogWarning($"[HuaweiAttr] Not initialized, caching event: {eventId}");
                lock (pendingActions)
                {
                    pendingActions.Add(() => ReportConversion(eventId, eventParamsJson));
                }
                return;
            }
            ReportConversionInternal(eventId, eventParamsJson);
        }

        public void ReportConversion(int eventCode)
        {
            ReportConversion(eventCode.ToString(), null);
        }

        private void ReportConversionInternal(string eventId, string eventParamsJson)
        {
            try
            {
                if (activity == null)
                {
                    Debug.LogError("[HuaweiAttr] Activity is null, cannot report event.");
                    return;
                }

                using (AndroidJavaClass bridge = new AndroidJavaClass(
                    "chengyu.idiom.hexa.zen.andriod.huawei.HuaweiAttributionBridge"))
                {
                    if (string.IsNullOrEmpty(eventParamsJson))
                    {
                        bridge.CallStatic("reportConversionEvent", activity, eventId);
                    }
                    else
                    {
                        bridge.CallStatic("reportConversionEvent", activity, eventId, eventParamsJson);
                    }
                }
                Debug.Log($"[HuaweiAttr] Client event reported: {eventId}");
            }
            catch (Exception e)
            {
                Debug.LogError("[HuaweiAttr] Report client event failed: " + e.Message);
            }
        }

        // ---------- Access Token 获取（调用已有的 HuaweiTokenManager） ----------

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
                    Debug.Log("[HuaweiAttr] Access Token obtained.");
                },
                (error) =>
                {
                    isTokenFetching = false;
                    Debug.LogError("[HuaweiAttr] Failed to get Access Token: " + error);
                }
            );
        }

        // ---------- 服务端事件上报（激活、次日留存、付费） ----------

        /// <summary>
        /// 上报激活事件（服务端），仅调用一次
        /// </summary>
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
        
        /// <summary>
        /// 上报次日留存事件（服务端）
        /// </summary>
        public void ReportStart(long actionTime)
        {
            SendServerEvent("2", actionTime);
        }

        /// <summary>
        /// 上报次日留存事件（服务端）
        /// </summary>
        public void ReportRetention(long actionTime)
        {
            SendServerEvent("3", actionTime);
        }

        /// <summary>
        /// 上报付费事件（服务端）
        /// </summary>
        /// <param name="actionTime">毫秒时间戳</param>
        /// <param name="amount">金额（单位：分？请查阅华为文档）</param>
        /// <param name="currency">货币代码，默认 CNY</param>
        public void ReportPurchase(long actionTime, decimal amount, string currency = "CNY")
        {
            SendServerEvent("4", actionTime, amount, currency);
        }

        // ---------- 服务端事件发送核心方法 ----------

        private void SendServerEvent(string actionType, long actionTime, decimal? amount = null, string currency = null)
        {
            // 前置检查
            if (string.IsNullOrEmpty(accessToken))
            {
                Debug.LogWarning("[HuaweiAttr] No access token, event will not be sent.");
                return;
            }
            if (string.IsNullOrEmpty(callBack) || string.IsNullOrEmpty(oaid))
            {
                Debug.LogWarning("[HuaweiAttr] Missing callBack or OAID, cannot send server event.");
                return;
            }

            // 构造 JSON（手工拼接，保证格式准确）
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"actionType\":\"{actionType}\",");
            sb.Append($"\"actionTime\":{actionTime},");
            sb.Append($"\"deviceIdType\":\"OAID\",");
            sb.Append($"\"appId\":\"{appId}\",");
            sb.Append($"\"callBack\":\"{callBack}\",");
            sb.Append($"\"deviceId\":\"{oaid}\"");
            if (amount.HasValue)
            {
                sb.Append($",\"amount\":{amount.Value}");
                sb.Append($",\"currency\":\"{currency ?? "CNY"}\"");
            }
            sb.Append("}");
            string json = sb.ToString();

            Debug.Log($"[HuaweiAttr] Sending server event: {json}");

            // 启动协程发送
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
                    // 可在此处理 401 重新获取 Token 等逻辑
                }
            }
        }

        // ---------- 辅助方法：字典转 JSON ----------

        private string ConvertToJson(Dictionary<string, string> dict)
        {
            if (dict == null || dict.Count == 0) return "{}";
            var sb = new StringBuilder();
            sb.Append("{");
            int i = 0;
            foreach (var kv in dict)
            {
                if (i > 0) sb.Append(",");
                sb.Append("\"")
                  .Append(kv.Key)
                  .Append("\":\"")
                  .Append(kv.Value.Replace("\\", "\\\\").Replace("\"", "\\\""))
                  .Append("\"");
                i++;
            }
            sb.Append("}");
            return sb.ToString();
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
    }
}
#endif