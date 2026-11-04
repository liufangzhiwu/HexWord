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
    /// 2. triggerData 使用标准转化事件编码（见下方常量表），与云端管理台的
    ///    「转化事件」一一对应，不能填云端列表里显示的「事件ID」。
    /// </summary>
    public class HarmonyAttribution : IAttribute
    {
        // ====== 标准转化事件编码（triggerData） ======
        // 0 安装应用 —— 由华为系统/分发平台产生，客户端不上报
        //   （AdTriggerInfo.triggerData 取值范围为 [1,200]，传 0 会被服务端判参数错误）
        // 1 激活应用 —— 历史首次激活，华为侧按「历史首次」自动去重
        // 2 启动应用 —— 打开应用
        // 3 次日留存 —— 次日仍然使用应用
        // 4 付费     —— 应用内发生付费
        private const int CODE_INSTALL    = 0;
        private const int CODE_ACTIVATION = 1;
        private const int CODE_LAUNCH     = 2;
        private const int CODE_RETENTION  = 3;
        private const int CODE_PURCHASE   = 4;

        // ====== 业务场景标识（取值范围 [0,99]） ======
        // 华为文档标注为预留字段、当前无需关注，官方示例值即 5，保持一致。
        private const int BUSINESS_SCENE = 5;

        // 桥接对象创建/调用失败后的重试次数。
        // 归因初始化已提前到 Awake 阶段，此时引擎的 JS 桥可能还没注册完，
        // 留够重试窗口（约 5 秒）。
        private const int BRIDGE_MAX_RETRY = 5;
        private const float BRIDGE_RETRY_INTERVAL = 1f;

        private static OpenHarmonyJSObject attributionBridge;

        // 「启动应用」(2) 的进程内去重：AnalyticMgr.GameStart 在从后台切回前台时也会调用，
        // 不去重会把一次冷启动刷成多次启动。
        private static bool launchReported;

        // 「次日留存」(3) 的进程内去重：同样受 GameStart 多次调用影响，
        // 第 2 天内反复切前后台会重复上报。
        private static bool retentionReported;

        public void Init(float delay)
        {
#if !UNITY_EDITOR
            UnityTimer.Delay(delay, () => TryReportActivation(0));
#endif
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

        /// <summary>
        /// 启动阶段上报「激活应用」(1)，不依赖登录。
        ///
        /// 旧实现有两个问题：
        /// 1. 挂在登录成功之后，玩家没登录就不上报；
        /// 2. 用 PlayerPrefs 先置位再上报，首次上报失败（桥接未就绪/华为侧报错）
        ///    标记已写死，之后永远不再上报。
        /// 现在改为启动即上报，且不做本地去重——华为侧对「1 激活应用」按
        /// 「历史首次」自动去重，重复上报不会把激活量冲高。
        /// </summary>
        private static void TryReportActivation(int attempt)
        {
            if (EnsureBridge() && ReportTrigger(CODE_ACTIVATION))
            {
                return;
            }

            if (attempt >= BRIDGE_MAX_RETRY)
            {
                Debug.LogError($"[HarmonyAttr] 激活事件上报失败：重试 {BRIDGE_MAX_RETRY} 次仍未成功" +
                               "（桥接未就绪或调用异常），请检查 AttributionBridge.tslib 是否已注册");
                return;
            }

            UnityTimer.Delay(BRIDGE_RETRY_INTERVAL, () => TryReportActivation(attempt + 1));
        }

        /// <summary>
        /// 按标准转化事件编码上报（0 安装 / 1 激活 / 2 启动 / 3 次日留存 / 4 付费）。
        /// 直接透传给华为，不再做本地语义改写。
        /// </summary>
        public void ReportConversion(string eventCode)
        {
            if (!TryParseCode(eventCode, out int code))
            {
                Debug.LogWarning("[HarmonyAttr] 非法转化事件编码，已忽略: " + eventCode);
                return;
            }

            if (code == CODE_INSTALL)
            {
                Debug.LogWarning("[HarmonyAttr] 安装事件(0) 由华为系统/分发平台产生，" +
                                 "客户端 registerTrigger 不支持上报（triggerData 取值 [1,200]），已忽略");
                return;
            }

            if (code == CODE_LAUNCH)
            {
                if (launchReported) return;
                // 桥接没就绪或调用失败时不置位，留待下次调用再发，避免把事件吞掉
                if (!EnsureBridge()) return;
                if (!ReportTrigger(code)) return;
                launchReported = true;
                return;
            }

            ReportTrigger(code);
        }

        public void ReportRetention(long actionTime)
        {
            if (retentionReported) return;
            if (!EnsureBridge()) return;
            if (!ReportTrigger(CODE_RETENTION)) return;
            retentionReported = true;
        }

        public void ReportPurchase(long actionTime, decimal amount, string currency = "CNY")
        {
            // 应用归因服务的转化事件本身不带金额，金额只用于本地核对日志
            Debug.Log($"[HarmonyAttr] 付费转化上报: amount={amount}, currency={currency}");
            ReportTrigger(CODE_PURCHASE);
        }

        /// <returns>是否成功发起上报（桥接可用且调用未抛异常）</returns>
        private static bool ReportTrigger(int code)
        {
            if (!IsValidTriggerCode(code))
            {
                Debug.LogWarning($"[HarmonyAttr] triggerData 越界: {code}。转化事件编码范围：标准 1~200 / 自定义 501~600，" +
                                 "请检查 CODE_ACTIVATION/CODE_LAUNCH/CODE_RETENTION/CODE_PURCHASE 是否误填成「事件ID」。");
                return false;
            }

            if (!EnsureBridge()) return false;

            try
            {
                // 统一用字符串传参，由 ArkTS 侧转成 number，避免 JS 桥 number/string 类型不一致
                attributionBridge.Call("reportConversion", code.ToString(), BUSINESS_SCENE.ToString());
                return true;
            }
            catch (Exception e)
            {
                // 桥接可能尚未注册完成，置空以便下次调用重建
                attributionBridge = null;
                Debug.LogError($"[HarmonyAttr] 登记归因转化事件失败 code={code}: {e.Message}");
                return false;
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
            return int.TryParse(raw.Trim(), out code) && code >= 0;
        }
    }
}
#endif
