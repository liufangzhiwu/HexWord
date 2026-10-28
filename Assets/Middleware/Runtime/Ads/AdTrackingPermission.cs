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

        /// <summary>编辑器模拟：把下一次请求结果固定成"拒绝"（调试开关）</summary>
        private const string EditorDenyKey = "att_mock_deny";

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
            bool deny = PlayerPrefs.GetInt(EditorDenyKey, 0) == 1;
            AdTrackingStatus mock = deny ? AdTrackingStatus.Denied : AdTrackingStatus.Authorized;
            PlayerPrefs.SetInt(EditorStatusKey, (int)mock);
            PlayerPrefs.Save();
            Debug.Log($"[AdTracking] 编辑器模拟授权结果：{mock}（PlayerPrefs.att_mock_deny 可切换同意/拒绝）");
            callback(mock);
#else
            callback(AdTrackingStatus.Unsupported);
#endif
        }

        /// <summary>跳转系统设置页（iOS：本 App 的设置页，可修改"允许跟踪"开关）</summary>
        public static void OpenSystemSettings()
        {
#if UNITY_IOS && !UNITY_EDITOR
            _AdTracking_OpenSettings();
#elif UNITY_EDITOR
            // 编辑器里把模拟状态清掉，方便反复验证引导流程
            PlayerPrefs.DeleteKey(EditorStatusKey);
            PlayerPrefs.Save();
            Debug.Log("[AdTracking] 编辑器模拟：已重置模拟授权状态，可重新走引导流程");
#else
            Debug.LogWarning("[AdTracking] 当前平台未实现跳转系统广告追踪设置页");
#endif
        }

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
