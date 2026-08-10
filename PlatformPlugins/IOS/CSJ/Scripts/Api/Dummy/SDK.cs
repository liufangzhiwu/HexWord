//------------------------------------------------------------------------------
// Copyright (c) 2018-2019 Beijing Bytedance Technology Co., Ltd.
// All Right Reserved.
// Unauthorized copying of this file, via any medium is strictly prohibited.
// Proprietary and confidential.
//------------------------------------------------------------------------------

#if UNITY_EDITOR || (!UNITY_ANDROID && !UNITY_IOS)

namespace ByteDance.Union
{
    using System;
    using System.Collections.Generic;
    using ByteDance.Union.Mediation;
    using UnityEngine;

    /// <summary>
    /// The union platform SDK.
    /// </summary>
    public static class SDK
    {
        /// <summary>
        /// Gets the version of this SDK.
        /// </summary>
        public static string Version { get { return "1.0.0.0"; } }

        /// <summary>
        /// Create the advertisement native object.
        /// </summary>
        public static AdNative CreateAdNative() { return new AdNative(); }

        /// <summary>
        /// Request permission if necessary on some device, for example ask
        /// for READ_PHONE_STATE.
        /// </summary>
        public static void RequestPermissionIfNecessary() { }

        /// <summary>
        /// Try to show install dialog when exit the app.
        /// </summary>
        /// <returns>True means show dialog.</returns>
        public static bool TryShowInstallDialogWhenExit(Action onExitInstall) { return false; }

        /// <summary>
        /// 触发相应广告位的预加载。sdk内部每次会发起parallelNum个广告位的请求，间隔requestIntervalS秒后，再发起第二轮，以此类推，
        /// 直到所有的广告位都发起完成。
        /// 注意：
        /// 1. 由于有些adn是单例模式，这些adn在展示广告期间如果又进行了一次加载操作，会导致当前这次展示的回调因覆盖丢失。
        /// 因此要避免在广告展示期间调用preload预加载。建议在开屏广告展示完毕以后，或者在MainActivity中进行预加载。
        /// 2. iOS暂不支持
        /// </summary>
        /// <param name="activity">预加载需要的context，开发者传入activity</param>
        /// <param name="requestInfos">需要预加载的广告位，adslot等信息</param>
        /// <param name="parallelNum">预请求一次同时发起的广告位数量</param>
        /// <param name="requestIntervalS">相邻两次请求之间的间隔，单位秒</param>
        public static void MediationPreload(
            AndroidJavaObject activity,
            List<MediationPreloadRequestInfo> requestInfos,
            int parallelNum,
            int requestIntervalS)
        { }

        /// <summary>
        /// sdk运行中，更改流量分组功能
        /// </summary>
        public static void MediationSetUserInfoForSegment(MediationConfigUserInfoForSegment segment) { }

        /// <summary>
        /// sdk运行中，更改publisherDid
        /// </summary>
        public static void MediationSetPublisherDid(string publisherDid) { }

        /// <summary>
        /// sdk运行中，更改更改夜间模式
        /// </summary>
        /// <param name="themeStatus">0:正常模式，1:夜间模式</param>
        public static void MediationSetThemeStatus(int themeStatus) { }

        /// <summary>
        /// sdk运行中，更改隐私合规信息的设置
        /// </summary>
        public static void MediationUpdatePrivacyConfig(PrivacyConfiguration privacyConfig) { }

        /// <summary>
        /// 获取sdk的一些额外信息
        /// </summary>
        public static Dictionary<string, System.Object> GetMediationExtraInfo() { return null; }
    }

}

#endif
