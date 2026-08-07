//------------------------------------------------------------------------------
// Copyright (c) 2018-2019 Beijing Bytedance Technology Co., Ltd.
// All Right Reserved.
// Unauthorized copying of this file, via any medium is strictly prohibited.
// Proprietary and confidential.
//------------------------------------------------------------------------------

using System.Collections.Generic;
using ByteDance.Union.Mediation;

namespace ByteDance.Union
{
    using System;
    using UnityEngine;

#if !UNITY_EDITOR && UNITY_ANDROID
    /// <summary>
    /// The android bridge of the union SDK.
    /// </summary>
    public static class SDK
    {
        private static AndroidJavaObject activity;
        private static AndroidJavaObject adManager;

        /// <summary>
        /// Gets the version of this SDK.
        /// </summary>
        public static string Version
        {
            get
            {
                var adManager = GetAdManager();
                return adManager.Call<string>("getSDKVersion");
            }
        }

        /// <summary>
        /// Create the advertisement native object.
        /// </summary>
        public static AdNative CreateAdNative()
        {
            var adManager = GetAdManager();
            var context = Utils.GetActivity();
            var adNative = adManager.Call<AndroidJavaObject>("createAdNative", context);
            return new AdNative(adNative);
        }

        /// <summary>
        /// Request permission if necessary on some device, for example ask
        /// for READ_PHONE_STATE.
        /// </summary>
        public static void RequestPermissionIfNecessary()
        {
            var adManager = GetAdManager();
            var context = Utils.GetActivity();
            adManager.Call("requestPermissionIfNecessary", context);
        }

        /// <summary>
        /// Try to show install dialog when exit the app.
        /// </summary>
        /// <returns>True means show dialog.</returns>
        public static bool TryShowInstallDialogWhenExit(Action onExitInstall)
        {
            var adManager = GetAdManager();
            var context = Utils.GetActivity();
            var listener = new ExitInstallListener(onExitInstall);
            return adManager.Call<bool>("tryShowInstallDialogWhenExit", context, listener);
        }

        private static AndroidJavaObject GetAdManager()
        {
            if (adManager == null)
            {
                var jc = new AndroidJavaClass("com.bytedance.sdk.openadsdk.TTAdSdk");
                adManager = jc.CallStatic<AndroidJavaObject>("getAdManager");
            }

            return adManager;
        }

#pragma warning disable SA1300
#pragma warning disable IDE1006

        private sealed class ExitInstallListener : AndroidJavaProxy
        {
            private readonly Action callback;

            public ExitInstallListener(Action callback)
                : base("com.bytedance.sdk.openadsdk.downloadnew.core.ExitInstallListener")
            {
                this.callback = callback;
            }

            public void onExitInstall()
            {
                UnityDispatcher.PostTask(this.callback,true);
            }
        }

#pragma warning restore SA1300
#pragma warning restore IDE1006

        /// <summary>
        /// 触发相应广告位的预加载。sdk内部每次会发起parallelNum个广告位的请求，间隔requestIntervalS秒后，再发起第二轮，以此类推，
        /// 直到所有的广告位都发起完成。
        /// 注意：由于有些adn是单例模式，这些adn在展示广告期间如果又进行了一次加载操作，会导致当前这次展示的回调因覆盖丢失。
        /// 因此要避免在广告展示期间调用preload预加载。建议在开屏广告展示完毕以后，或者在MainActivity中进行预加载。
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
        {
            var jMediationManager = Utils.GetMediationManager();
            if (jMediationManager != null)
            {
                if (requestInfos == null || requestInfos.Count <= 0)
                {
                    return;
                }
                jMediationManager.Call("preload", activity, Utils.MakeMediationPreloadReqInfos(requestInfos),
                    parallelNum, requestIntervalS);
            }
        }

        /// <summary>
        /// sdk运行中，更改流量分组功能
        /// </summary>
        public static void MediationSetUserInfoForSegment(MediationConfigUserInfoForSegment segment)
        {
            var jMediationManager = Utils.GetMediationManager();
            if (jMediationManager != null)
            {
                var jSegment = Utils.MakeMediationConfigUserInfoForSegment(segment);
                if (jSegment != null)
                {
                    jMediationManager.Call("setUserInfoForSegment", jSegment);
                }
            }
        }
        
        /// <summary>
        /// sdk运行中，更改publisherDid
        /// </summary>
        public static void MediationSetPublisherDid(string publisherDid)
        {
            var jMediationManager = Utils.GetMediationManager();
            if (jMediationManager != null)
            {
                jMediationManager.Call("setPulisherDid", publisherDid);
            }
        }

        /// <summary>
        /// sdk运行中，更改更改夜间模式
        /// </summary>
        /// <param name="themeStatus">0:正常模式，1:夜间模式</param>
        public static void MediationSetThemeStatus(int themeStatus)
        {
            var jMediationManager = Utils.GetMediationManager();
            if (jMediationManager != null)
            {
                jMediationManager.Call("setThemeStatus", themeStatus);
            }
        }

            /// <summary>
        /// sdk运行中，更改隐私合规信息的设置
        /// </summary>
        public static void MediationUpdatePrivacyConfig(PrivacyConfiguration privacyConfig)
        {
            var jMediationManager = Utils.GetMediationManager();
            if (jMediationManager != null)
            {
                var jPrivacyConfig = Utils.MakeCustomController(privacyConfig);
                if (jPrivacyConfig != null)
                {
                    jMediationManager.Call("updatePrivacyConfig", jPrivacyConfig);
                }
            }
        }
        
        /// <summary>
        /// 获取sdk的一些额外信息
        /// </summary>
        public static Dictionary<string, System.Object> GetMediationExtraInfo()
        {
            var jMediationManager = Utils.GetMediationManager();
            if (jMediationManager != null)
            {
                // java端返回的是Map<String, Object>
                AndroidJavaObject map = jMediationManager.Call<AndroidJavaObject>("getMediationExtraInfo");
                if (map != null)
                {
                    Debug.Log("CSJM_Unity getMediationExtraInfo返回的是原生端对象，根据需要通过反射获取信息");
                    Debug.Log("CSJM_Unity getMediationExtraInfo: " + map.Call<string>("toString"));
                }
            }
            return null;
        }
    }
#endif
}
