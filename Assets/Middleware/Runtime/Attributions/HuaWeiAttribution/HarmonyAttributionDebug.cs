#if UNITY_OPENHARMONY
using System;
using UnityEngine;
using UnityEngine.OpenHarmony;

namespace Middleware
{
    /// <summary>
    /// validateSource 入参（对应 ArkTS 侧 SourceDebugConfig，字段名必须一致）。
    ///
    /// signature 需要私钥签名，客户端算不了也不该持有私钥：
    /// 联调时由服务端/工具侧按下面的规则算好后传进来。
    ///   待签名串 = adTechId + '\u2063' + campaignId + '\u2063' + destinationId
    ///            + '\u2063' + serviceTag + '\u2063' + mmpIdStr + '\u2063' + nonce + '\u2063' + timestamp
    ///   算法：SHA256withRSA/PSS（RSA3072），空字段不参与拼接。
    /// </summary>
    [Serializable]
    public class AttributionSourceDebugConfig
    {
        public string adTechId;          // 分发平台归因角色ID
        public string campaignId;        // 营销任务ID
        public string destinationId;     // 开发者应用 appId，不带 C
        public int sourceType = 1;       // 0 曝光 / 1 点击
        public string nonce;             // 32 字符，每次唯一
        public long timestamp;           // 毫秒，与当前时间偏差 ≤ 10 分钟
        public string signature;         // 见类注释的签名规则
        public string publicKey;         // 注册归因角色时给云侧的公钥
        public string mmpIds;            // 可选，逗号分隔，最多 2 个
        public string serviceTag;        // 可选
    }

    /// <summary>
    /// setPostback 入参（对应 ArkTS 侧 PostbackDebugConfig，字段名必须一致）。
    /// 用来伪造一条归因结果回传，验证服务端能不能收到。
    /// </summary>
    [Serializable]
    public class AttributionPostbackDebugConfig
    {
        public string adTechId;          // 分发平台归因角色ID
        public string campaignId;        // 营销任务ID
        public string sourceId;          // 媒体应用id
        public string destinationId;     // 开发者应用 appId，不带 C
        public int businessScene = 5;
        public int triggerData = 1;      // 标准转化事件编码：1 激活 / 2 启动 / 3 次留 / 4 付费
        public string postbackUrl;       // 服务端接收归因结果的地址，推荐 HTTPS
        public string serviceTag;        // 可选
    }

    /// <summary>
    /// 华为应用归因「接入调试」入口。
    ///
    /// 对应 attributionTestManager 的三个接口，用于在**没有真实投放**的情况下验证：
    ///   validateSource  —— 归因来源登记是否合法（需要签名，见 config 注释）
    ///   setPostback     —— 构造一条回传数据，指定你的服务端 URL
    ///   flushPostbacks  —— 触发回传，看服务端有没有收到
    ///
    /// 典型用法（只在联调包里调用）：
    ///   var cfg = new AttributionPostbackDebugConfig {
    ///       adTechId = "...", campaignId = "...", sourceId = "...",
    ///       destinationId = "...", triggerData = 1,
    ///       postbackUrl = "https://your-server/attribution/callback"
    ///   };
    ///   HarmonyAttributionDebug.SetPostback(cfg);        // 先塞数据
    ///   HarmonyAttributionDebug.FlushPostbacks(cfg.adTechId);  // 再触发
    /// 结果看 hilog 过滤 [AttributionBridge]。
    /// </summary>
    public static class HarmonyAttributionDebug
    {
        private static OpenHarmonyJSObject bridge;

        private static bool EnsureBridge()
        {
            if (bridge != null) return true;

            try
            {
                bridge = new OpenHarmonyJSObject("AttributionBridge");
            }
            catch (Exception e)
            {
                bridge = null;
                Debug.LogError("[HarmonyAttrDebug] 创建 AttributionBridge 异常: " + e.Message);
                return false;
            }

            if (bridge == null)
            {
                Debug.LogError("[HarmonyAttrDebug] AttributionBridge 为空，请检查 AttributionBridge.tslib 是否已注册");
                return false;
            }

            return true;
        }

        /// <summary>验证归因来源登记（需外部算好 signature）。</summary>
        public static void ValidateSource(AttributionSourceDebugConfig config)
        {
            if (config == null)
            {
                Debug.LogError("[HarmonyAttrDebug] ValidateSource: config 为空");
                return;
            }
            if (string.IsNullOrEmpty(config.signature) || string.IsNullOrEmpty(config.publicKey))
            {
                Debug.LogError("[HarmonyAttrDebug] ValidateSource: signature / publicKey 必填，" +
                               "签名需 SHA256withRSA/PSS，见 AttributionSourceDebugConfig 注释");
                return;
            }

            if (string.IsNullOrEmpty(config.nonce)) config.nonce = GenerateNonce();
            if (config.timestamp <= 0) config.timestamp = NowMilliseconds();

            if (!EnsureBridge()) return;

            try
            {
                bridge.Call("validateSource", JsonUtility.ToJson(config));
            }
            catch (Exception e)
            {
                Debug.LogError("[HarmonyAttrDebug] ValidateSource 调用失败: " + e.Message);
            }
        }

        /// <summary>设置一条待回传的归因结果（调试用）。</summary>
        public static void SetPostback(AttributionPostbackDebugConfig config)
        {
            if (config == null)
            {
                Debug.LogError("[HarmonyAttrDebug] SetPostback: config 为空");
                return;
            }
            if (string.IsNullOrEmpty(config.postbackUrl))
            {
                Debug.LogError("[HarmonyAttrDebug] SetPostback: postbackUrl 必填");
                return;
            }

            if (!EnsureBridge()) return;

            try
            {
                bridge.Call("setPostback", JsonUtility.ToJson(config));
            }
            catch (Exception e)
            {
                Debug.LogError("[HarmonyAttrDebug] SetPostback 调用失败: " + e.Message);
            }
        }

        /// <summary>触发归因结果回传（调试用）。需先 SetPostback。</summary>
        public static void FlushPostbacks(string adTechId)
        {
            if (string.IsNullOrEmpty(adTechId))
            {
                Debug.LogError("[HarmonyAttrDebug] FlushPostbacks: adTechId 必填");
                return;
            }

            if (!EnsureBridge()) return;

            try
            {
                bridge.Call("flushPostbacks", adTechId);
            }
            catch (Exception e)
            {
                Debug.LogError("[HarmonyAttrDebug] FlushPostbacks 调用失败: " + e.Message);
            }
        }

        /// <summary>
        /// 一站式自查：先 setPostback 再 flushPostbacks。
        /// 跑完后去你配置的 postbackUrl 服务端看有没有收到请求。
        /// </summary>
        public static void RunPostbackCheck(AttributionPostbackDebugConfig config)
        {
            if (config == null)
            {
                Debug.LogError("[HarmonyAttrDebug] RunPostbackCheck: config 为空");
                return;
            }

            Debug.Log($"[HarmonyAttrDebug] 开始回传自查，triggerData={config.triggerData}, url={config.postbackUrl}");
            SetPostback(config);
            // 给 setPostback 的 Promise 留出落地时间，再触发 flush
            UnityTimer.Delay(1.5f, () => FlushPostbacks(config.adTechId));
        }

        /// <summary>32 位随机 nonce（不含 '-'）。</summary>
        public static string GenerateNonce()
        {
            var sb = new System.Text.StringBuilder(32);
            for (int i = 0; i < 32; i++) sb.Append(UnityEngine.Random.Range(0, 16).ToString("x"));
            return sb.ToString();
        }

        /// <summary>当前毫秒时间戳。</summary>
        public static long NowMilliseconds()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
#endif
