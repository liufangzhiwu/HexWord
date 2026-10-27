#if UNITY_OPENHARMONY
using System;
using UnityEngine;
using UnityEngine.OpenHarmony;

namespace Middleware
{
    /// <summary>
    /// 鸿蒙（HarmonyOS）归因实现。
    ///
    /// 走「华为应用归因服务」（AppGallery Kit / Store Kit 的 attributionManager）：
    /// 客户端只负责调用 AttributionBridge.registerTrigger 登记转化事件，
    /// 归因结果由华为回传到「应用归因云端管理台」里配置的回传地址（服务端接收）。
    ///
    /// 注意：
    /// 1. 鸿蒙侧没有安卓那套「智能分包 content://com.huawei.appmarket.commondata」
    ///    的客户端渠道查询接口，客户端拿不到 channel / taskid / callback，
    ///    因此不能照搬安卓华为「客户端查归因 + 调 AGC activate 接口回传」的做法。
    /// 2. triggerData 使用「应用归因云端管理台」分配的转化事件编码
    ///    （激活应用 1 / 次日留存 3 / 付费 4）。
    /// </summary>
    public class HarmonyAttribution : IAttribute
    {
        // ====== 转化事件编码（triggerData，来自「应用归因云端管理台」） ======
        // 激活应用 = 1，启动应用 = 2，次日留存 = 3，付费 = ?
        private const int CODE_ACTIVATION = 1; // 激活应用（首次激活，仅上报一次）
        private const int CODE_RETENTION  = 3; // 次日留存
        private const int CODE_PURCHASE   = 4; // 付费（⚠️ 表格里"付费"编码缺失，按 0/1/2/3 顺延推测为 4，务必与云端确认）

        // ====== 业务场景标识（取值范围 [0,99]，与云端管理台登记的业务场景保持一致） ======
        private const int BUSINESS_SCENE = 5;

        // 激活事件仅首次启动上报一次
        private const string KEY_ACTIVATION_REPORTED = "HarmonyAttr_ActivationReported";

        private static OpenHarmonyJSObject attributionBridge;

        public void Init(float delay)
        {
            UnityTimer.Delay(delay, () => { EnsureBridge(); });
        }

        /// <summary>
        /// 懒加载桥接对象：ArkTS 侧的注册在引擎启动阶段完成，
        /// 这里既做首次创建，也承担"首次创建失败后再次调用"的重试。
        /// </summary>
        private static bool EnsureBridge()
        {
            if (attributionBridge != null) return true;

            try
            {
                attributionBridge = new OpenHarmonyJSObject("AttributionBridge");
            }
            catch (Exception e)
            {
                attributionBridge = null;
                Debug.LogError("[HarmonyAttr] 创建 AttributionBridge 异常: " + e.Message);
                return false;
            }

            if (attributionBridge == null)
            {
                Debug.LogError("[HarmonyAttr] AttributionBridge 为空，请检查 " +
                               "AttributionBridge.tslib 的 RegisterAttributionBridge 是否被注册");
                return false;
            }

            return true;
        }

        public void ReportConversion(string eventCode)
        {
            // AnalyticMgr 登录时会传 "2"（安卓遗留的"启动"动作码）。
            // 鸿蒙侧按你们的 3 个归因事件处理：登录时上报「激活应用」(1)，且仅首次上报一次。
            // 「启动应用」(2) 不在你们的归因事件列表里，暂不上报；
            // 如后续需要，在这里加 ReportTrigger(2) 即可。
            if (eventCode == "2")
            {
                ReportActivationOnce();
                return;
            }

            if (!TryParseCode(eventCode, out int code))
            {
                Debug.LogWarning("[HarmonyAttr] 非法转化事件编码，已忽略: " + eventCode);
                return;
            }

            ReportTrigger(code);
        }

        public void ReportRetention(long actionTime)
        {
            ReportTrigger(CODE_RETENTION);
        }

        public void ReportPurchase(long actionTime, decimal amount, string currency = "CNY")
        {
            // 应用归因服务的转化事件本身不带金额，金额只用于本地核对日志
            Debug.Log($"[HarmonyAttr] 付费转化上报: amount={amount}, currency={currency}");
            ReportTrigger(CODE_PURCHASE);
        }

        private static void ReportActivationOnce()
        {
            if (PlayerPrefs.GetInt(KEY_ACTIVATION_REPORTED, 0) == 1)
            {
                return;
            }

            PlayerPrefs.SetInt(KEY_ACTIVATION_REPORTED, 1);
            PlayerPrefs.Save();
            ReportTrigger(CODE_ACTIVATION);
        }

        private static void ReportTrigger(int code)
        {
            if (!IsValidTriggerCode(code))
            {
                Debug.LogWarning($"[HarmonyAttr] triggerData 越界: {code}。转化事件编码范围：标准 1~200 / 自定义 501~600，" +
                                 "请检查 CODE_ACTIVATION/CODE_RETENTION/CODE_PURCHASE 是否误填成「事件ID」。");
                return;
            }

            if (!EnsureBridge()) return;

            try
            {
                // 统一用字符串传参，由 ArkTS 侧转成 number，避免 JS 桥 number/string 类型不一致
                attributionBridge.Call("reportConversion", code.ToString(), BUSINESS_SCENE.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError($"[HarmonyAttr] 登记归因转化事件失败 code={code}: {e.Message}");
            }
        }

        private static bool IsValidTriggerCode(int code)
        {
            return (code >= 1 && code <= 200) || (code >= 501 && code <= 600);
        }

        private static bool TryParseCode(string raw, out int code)
        {
            code = 0;
            if (string.IsNullOrEmpty(raw)) return false;
            return int.TryParse(raw.Trim(), out code) && code > 0;
        }
    }
}
#endif
