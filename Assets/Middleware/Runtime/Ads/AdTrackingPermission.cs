using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Middleware
{
    /// <summary>
    /// 广告追踪（关联应用）权限状态。
    /// 数值与 iOS ATTrackingManagerAuthorizationStatus 保持一致，便于原生直接回传。
    /// </summary>
    public enum AdTrackingStatus
    {
        NotDetermined = 0, // 还没问过（需要出引导页 + 系统弹窗）
        Restricted = 1,    // 系统限制（家长控制等），无法请求
        Denied = 2,        // 已拒绝（只能去系统设置里手动开）
        Authorized = 3,    // 已同意
        Unsupported = 4,   // 当前平台没有这个权限概念
    }

    /// <summary>
    /// 广告追踪权限的原生封装（iOS ATT）。
    ///
    /// 平台差异：
    ///   - iOS：ATTrackingManager（原生实现见 Assets/Plugins/iOS/AdTrackingBridge.mm）
    ///   - 编辑器：本地模拟（PlayerPrefs: att_mock_status），方便跑通引导/奖励/设置全流程
    ///   - 其它平台（Android / 鸿蒙）：IsSupported = false，由 AdTrackingManager 走平台自己的通道
    ///
    /// 原生回调约定：UnitySendMessage("AdTrackingReceiver", "OnTrackingResult", "0..3")
    /// </summary>
    public static class AdTrackingPermission
    {
        public const string ReceiverName = "AdTrackingReceiver";
        public const string CallbackMethod = "OnTrackingResult";

        /// <summary>编辑器模拟状态存档键（0 未决定 / 2 拒绝 / 3 同意）</summary>
        private const string EditorStatusKey = "att_mock_status";

        /// <summary>编辑器模拟：授权请求的返回策略（0 随机 / 1 固定同意 / 2 固定拒绝）</summary>
        private const string EditorModeKey = "att_mock_mode";

        /// <summary>旧调试键（历史遗留：1 = 固定拒绝），仅用于兼容</summary>
        private const string EditorDenyKey = "att_mock_deny";

        /// <summary>编辑器模拟：系统授权弹窗的返回策略</summary>
        public enum EditorMockMode
        {
            /// <summary>随机同意 / 拒绝（默认，各 50%）</summary>
            Random = 0,
            /// <summary>固定同意</summary>
            AlwaysAllow = 1,
            /// <summary>固定拒绝</summary>
            AlwaysDeny = 2,
        }

        /// <summary>编辑器模拟：当前返回策略（可在 Tools/广告追踪/ 菜单里切）</summary>
        public static EditorMockMode MockMode
        {
            get
            {
                int raw = PlayerPrefs.GetInt(EditorModeKey, (int)EditorMockMode.Random);
                // 兼容旧调试键：老版本用 att_mock_deny=1 表示固定拒绝
                if (raw == 0 && PlayerPrefs.GetInt(EditorDenyKey, 0) == 1)
                    return EditorMockMode.AlwaysDeny;

                return (EditorMockMode)Mathf.Clamp(raw, 0, 2);
            }
            set
            {
                PlayerPrefs.SetInt(EditorModeKey, (int)value);
                PlayerPrefs.DeleteKey(EditorDenyKey);
                PlayerPrefs.Save();
                Debug.Log($"[AdTracking] 编辑器模拟策略已设为：{value}");
            }
        }

        private static Action<AdTrackingStatus> _pendingCallback;
        private static AdTrackingReceiver _receiver;

        /// <summary>当前平台是否走原生 ATT 通道（iOS 与编辑器）</summary>
        public static bool IsSupported
        {
            get
            {
#if UNITY_IOS || UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>查询当前授权状态（不弹窗）</summary>
        public static AdTrackingStatus GetStatus()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return (AdTrackingStatus)_AdTracking_GetStatus();
#elif UNITY_EDITOR
            return (AdTrackingStatus)PlayerPrefs.GetInt(EditorStatusKey, 0);
#else
            return AdTrackingStatus.Unsupported;
#endif
        }

        /// <summary>
        /// 发起系统授权弹窗。已决定过时原生会直接回传结果，不会重复弹窗。
        /// </summary>
        public static void Request(Action<AdTrackingStatus> callback)
        {
            if (callback == null) return;

            AdTrackingStatus current = GetStatus();
            if (current != AdTrackingStatus.NotDetermined)
            {
                callback(current);
                return;
            }

#if UNITY_IOS && !UNITY_EDITOR
            _pendingCallback = callback;
            EnsureReceiver();
            _AdTracking_Request();
#elif UNITY_EDITOR
            // ★ 编辑器模拟：默认**随机**返回同意/拒绝（各 50%），可用 Tools/广告追踪/ 菜单
            //   切成"固定同意 / 固定拒绝"做确定性验证。
            AdTrackingStatus mock = RollMockResult();
            SetMockStatus(mock);
            Debug.Log($"[AdTracking] 编辑器模拟授权结果：{mock}（策略={MockMode}）");
            callback(mock);
#else
            callback(AdTrackingStatus.Unsupported);
#endif
        }

#if UNITY_EDITOR
        /// <summary>编辑器模拟：按当前策略掷一次结果（随机模式各 50%）</summary>
        private static AdTrackingStatus RollMockResult()
        {
            EditorMockMode mode = MockMode;
            if (mode == EditorMockMode.AlwaysAllow) return AdTrackingStatus.Authorized;
            if (mode == EditorMockMode.AlwaysDeny) return AdTrackingStatus.Denied;

            // 随机：50% 拒绝 / 50% 同意（用 UnityEngine.Random，日志里能看出掷到了哪一边）
            AdTrackingStatus rolled = UnityEngine.Random.value < 0.5f
                ? AdTrackingStatus.Denied
                : AdTrackingStatus.Authorized;
            Debug.Log($"[AdTracking] 编辑器随机模拟：本次掷到 {rolled}");
            return rolled;
        }
#endif

        /// <summary>跳转系统设置页（iOS：本 App 的设置页，可修改"允许跟踪"开关）</summary>
        public static void OpenSystemSettings()
        {
#if UNITY_IOS && !UNITY_EDITOR
            _AdTracking_OpenSettings();
#elif UNITY_EDITOR
            // ★ 编辑器没有真实的"系统设置"页，这里模拟玩家在设置里拨动「允许跟踪」开关：
            //     已同意 → 变成拒绝；未决定/拒绝 → 变成同意。
            //
            // 原来的实现是"把模拟状态删掉"，删完只可能是 NotDetermined，永远不会是 Denied。
            // 于是 AdTrackingManager.CheckSettingsResultRoutine 里"从系统设置返回且未开启 →
            // 写 adTrackingEnabled=0 + 关开关"这条分支在编辑器里根本走不到，
            // 表现为：点了广告关联开关什么都不会变，广告追踪永远显示"开启"。
            AdTrackingStatus current = GetStatus();
            AdTrackingStatus next = current == AdTrackingStatus.Authorized
                ? AdTrackingStatus.Denied
                : AdTrackingStatus.Authorized;
            SetMockStatus(next);
            Debug.Log($"[AdTracking] 编辑器模拟系统设置：广告追踪 {current} → {next}");
#else
            Debug.LogWarning("[AdTracking] 当前平台未实现跳转系统广告追踪设置页");
#endif
        }

#if UNITY_EDITOR
        /// <summary>编辑器模拟：直接设置系统授权状态（0 未决定 / 1 受限 / 2 拒绝 / 3 同意）</summary>
        public static void SetMockStatus(AdTrackingStatus status)
        {
            PlayerPrefs.SetInt(EditorStatusKey, (int)status);
            PlayerPrefs.Save();
            Debug.Log($"[AdTracking] 编辑器模拟授权状态已设为：{status}");
        }

        /// <summary>编辑器模拟：清掉模拟状态，回到"未决定"（会重新弹引导页，并重新随机结果）</summary>
        public static void ResetMockStatus()
        {
            PlayerPrefs.DeleteKey(EditorStatusKey);
            PlayerPrefs.Save();
            Debug.Log("[AdTracking] 编辑器模拟授权状态已清空（NotDetermined），下次请求会按策略重新掷一次");
        }
#endif

        /// <summary>供原生 UnitySendMessage 回调（不要手动调用）</summary>
        internal static void HandleNativeResult(string value)
        {
            int raw;
            if (!int.TryParse(value, out raw))
            {
                Debug.LogWarning($"[AdTracking] 原生回调参数异常：{value}");
                return;
            }

            AdTrackingStatus status = (AdTrackingStatus)Mathf.Clamp(raw, 0, 4);
            Debug.Log($"[AdTracking] 原生授权回调：{status}");

            Action<AdTrackingStatus> cb = _pendingCallback;
            _pendingCallback = null;
            if (cb != null) cb(status);
        }

        private static void EnsureReceiver()
        {
            if (_receiver != null) return;

            GameObject go = new GameObject(ReceiverName);
            UnityEngine.Object.DontDestroyOnLoad(go);
            _receiver = go.AddComponent<AdTrackingReceiver>();
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int _AdTracking_GetStatus();

        [DllImport("__Internal")]
        private static extern void _AdTracking_Request();

        [DllImport("__Internal")]
        private static extern void _AdTracking_OpenSettings();
#endif
    }
}
