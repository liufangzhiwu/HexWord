#if UNITY_huawei
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;

namespace Middleware
{
    public class HuaWeiAttribution : IAttribute
    {
        private AndroidJavaObject activity;
        private string packageName;

        private bool initFinished = false;
        private readonly List<Action> pendingActions = new List<Action>();

        /// <summary>
        /// 初始化：延迟后获取 Android Activity，初始化分析服务，并自动获取/上报归因
        /// </summary>
        public void Init(float delay)
        {
            // 假设项目中已有 UnityTimer 工具类
            UnityTimer.Delay(delay, () =>
            {
                // 1. 获取 Unity Activity
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                }

                // 2. 获取包名
                packageName = Application.identifier;

                // 3. 初始化华为分析服务
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

                // 标记初始化完成
                initFinished = true;
                Debug.Log("[HuaweiAttr] Init completed.");

                // 执行所有缓存的操作
                lock (pendingActions)
                {
                    foreach (var action in pendingActions)
                    {
                        try { action?.Invoke(); }
                        catch (Exception ex) { Debug.LogError("[HuaweiAttr] Pending action error: " + ex.Message); }
                    }
                    pendingActions.Clear();
                }

                // 自动获取归因并上报
                string json = GetAttributionInfo();
                if (!string.IsNullOrEmpty(json))
                {
                    Debug.Log("归因数据：" + json);
                    ReportAttribution(json);
                }
            });
        }

        /// <summary>
        /// 获取华为归因信息（通过 ContentProvider，无需额外 SDK）
        /// </summary>
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

        /// <summary>
        /// 上报转化事件（带事件ID和可选参数JSON）。若未初始化完成则缓存，初始化后自动上报。
        /// </summary>
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

        /// <summary>
        /// 上报转化事件（int 重载，兼容旧接口）
        /// </summary>
        public void ReportConversion(int eventCode)
        {
            ReportConversion(eventCode.ToString(), null);
        }

        /// <summary>
        /// 实际执行上报的逻辑（此时 activity 应该已就绪）
        /// </summary>
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
                Debug.Log($"[HuaweiAttr] Event reported: {eventId}");
            }
            catch (Exception e)
            {
                Debug.LogError("[HuaweiAttr] Report event failed: " + e.Message);
            }
        }

        /// <summary>
        /// 解析归因JSON并上报到华为分析（使用 $AppLaunch 事件）
        /// </summary>
        private void ReportAttribution(string attributionJson)
        {
            if (string.IsNullOrEmpty(attributionJson)) return;

            try
            {
                var data = JsonUtility.FromJson<AttributionData>(attributionJson);
                var dict = new Dictionary<string, string>();

                if (!string.IsNullOrEmpty(data.enterAgTime))
                    dict["enter_ag_time"] = data.enterAgTime;
                if (!string.IsNullOrEmpty(data.installedFinishTime))
                    dict["installed_finish_time"] = data.installedFinishTime;
                if (!string.IsNullOrEmpty(data.startDownloadTime))
                    dict["start_download_time"] = data.startDownloadTime;

                if (data.trackId != null)
                {
                    if (!string.IsNullOrEmpty(data.trackId.channel)) dict["channel"] = data.trackId.channel;
                    if (!string.IsNullOrEmpty(data.trackId.taskid)) dict["taskid"] = data.trackId.taskid;
                    if (!string.IsNullOrEmpty(data.trackId.callback)) dict["callback"] = data.trackId.callback;
                    if (!string.IsNullOrEmpty(data.trackId.subTaskId)) dict["sub_task_id"] = data.trackId.subTaskId;
                    if (!string.IsNullOrEmpty(data.trackId.RTAID)) dict["rta_id"] = data.trackId.RTAID;
                }

                if (!string.IsNullOrEmpty(data.referrerEx))
                    dict["referrer_ex"] = data.referrerEx;

                string paramsJson = ConvertToJson(dict);
                ReportConversion("$AppLaunch", paramsJson);
                Debug.Log("[HuaweiAttr] Attribution reported via $AppLaunch.");
            }
            catch (Exception e)
            {
                Debug.LogError("[HuaweiAttr] ReportAttribution error: " + e.Message);
            }
        }

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