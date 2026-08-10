#if UNITY_IOS
using System.Collections.Generic;
using System.Threading;
using ByteDance.Union;
using ByteDance.Union.Mediation;
using Middleware;
using UnityEngine;

/**
 * 开屏广告代码示例
 * 注：该接口支持融合功能
 */
public class ExampleSplashAd
{
    public static void LoadAndShowSplashAd(Ads_ios Ads_ios, bool isM)
    {
        if (Ads_ios.splashAd != null)
        {
            Ads_ios.splashAd.Dispose();
            Ads_ios.splashAd = null;
        }

        int mSplashExpressWidthDp = 0;
        int mSplashExpressHeightDp = 0;
#if UNITY_ANDROID
        AndroidJavaClass unityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject unityContext = unityClass.GetStatic<AndroidJavaObject>("currentActivity");
        float scale = unityContext.Call<AndroidJavaObject>("getResources").
            Call<AndroidJavaObject>("getDisplayMetrics").Get<float>("density");
        mSplashExpressWidthDp = (int)(Screen.width/scale + 0.5f);//根据设备像素宽度获取设备宽度DP
        mSplashExpressHeightDp = (int)(Screen.height/scale + 0.5f);//根据设备像素高度获取设备高度DP
#endif

        // 开屏自定义兜底，可选
        var mediationSplashReqInfo = new MediationSplashRequestInfo();
        mediationSplashReqInfo.AdnName = AdConst.ADN_PANGLE;
        mediationSplashReqInfo.AppId = CSJMDAdPositionId.M_SPLASH_BASELINE_APPID;
        mediationSplashReqInfo.Appkey = ""; // 穿山甲不需要appkey
        mediationSplashReqInfo.AdnSlotId = CSJMDAdPositionId.M_SPLASH_BASELINE_ID;

        string codeId = isM == false ? CSJMDAdPositionId.CSJ_SPLASH_V_ID : CSJMDAdPositionId.M_SPLASH_EXPRESS_ID;
        var adSlot = new AdSlot.Builder()
            .SetCodeId(codeId) // 必传
            .SetExpressViewAcceptedSize(mSplashExpressWidthDp, mSplashExpressHeightDp)  //普通开屏也需要设置模版size，单位dp
            .SetMediationAdSlot(new MediationAdSlot.Builder()
                .SetScenarioId("ScenarioId") // 可选
                .SetBidNotify(true) // 可选
                .SetMediationSplashRequestInfo(mediationSplashReqInfo) // 可选
                .Build())
            .Build();
        SDK.CreateAdNative().LoadSplashAd(adSlot, new SplashAdListener(Ads_ios), 3500);
    }

    private static void ShowSplashAd(Ads_ios Ads_ios)
    {
        Debug.Log("CSJM_Unity " + "Ads_ios " + "SetSplashInteractionListener Invoke");
        // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
        //     Ads_ios.information.text = "show Splash";

        Ads_ios.splashAd.SetSplashInteractionListener(new SplashAdInteractionListener(Ads_ios));
        Ads_ios.splashAd.SetDownloadListener(new AppDownloadListener(Ads_ios));
        Ads_ios.splashAd.SetAdInteractionListener(new TTAdInteractionListener());
        Ads_ios.splashAd.ShowSplashAd();
    }

    // 打印广告相关信息
    private static void LogMediationInfo(Ads_ios Ads_ios)
    {
        MediationAdEcpmInfo showEcpm = Ads_ios.splashAd.GetMediationManager().GetShowEcpm();
        if (showEcpm != null)
        {
            LogUtils.LogMediationAdEcpmInfo(showEcpm, "GetShowEcpm");
        }

        MediationAdEcpmInfo bestEcpm = Ads_ios.splashAd.GetMediationManager().GetBestEcpm();
        if (bestEcpm != null)
        {
            LogUtils.LogMediationAdEcpmInfo(bestEcpm, "GetBestEcpm");
        }

        List<MediationAdEcpmInfo> multiBiddingEcpmList = Ads_ios.splashAd.GetMediationManager().GetMultiBiddingEcpm();
        foreach (MediationAdEcpmInfo item in multiBiddingEcpmList)
        {
            LogUtils.LogMediationAdEcpmInfo(item, "GetMultiBiddingEcpm");
        }

        List<MediationAdEcpmInfo> cacheList = Ads_ios.splashAd.GetMediationManager().GetCacheList();
        foreach (MediationAdEcpmInfo item in cacheList)
        {
            LogUtils.LogMediationAdEcpmInfo(item, "GetCacheList");
        }

        List<MediationAdLoadInfo> adLoadInfoList = Ads_ios.splashAd.GetMediationManager().GetAdLoadInfo();
        foreach (MediationAdLoadInfo item in adLoadInfoList)
        {
            LogUtils.LogAdLoadInfo(item);
        }
    }

    // 广告加载监听器
    public sealed class SplashAdListener : ISplashAdListener
    {
        private Ads_ios Ads_ios;
        public SplashAdListener(Ads_ios Ads_ios)
        {
            this.Ads_ios = Ads_ios;
        }

        public void OnSplashLoadFail(int code, string message)
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + "splash load OnSplashLoadFail:" + code + ":" + message +
                      $" on main thread: {Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId}");
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            //     this.Ads_ios.information.text = "OnSplashLoadFail:" + code + ":" + message;

            if (this.Ads_ios.splashAd != null)
            {
                this.Ads_ios.splashAd.Dispose();
                this.Ads_ios.splashAd = null;
            }
        }

        public void OnSplashLoadSuccess(BUSplashAd ad)
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + "OnSplashLoadSuccess");
#if UNITY_IOS
            Ads_ios.splashAd = ad;
            ShowSplashAd(Ads_ios);
#endif
        }

        public void OnSplashRenderSuccess(BUSplashAd ad)
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + $"splash load OnRenderSuccess:on main thread: {Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId}");
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            //     this.Ads_ios.information.text = "OnRenderSuccess";
#if UNITY_ANDROID
            Ads_ios.splashAd = ad;
            ShowSplashAd(Ads_ios);
#endif
        }

        public void OnSplashRenderFail(int code, string message)
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + "OnRenderFailed");
        }
    }

    // 广告展示监听器
    private sealed class SplashAdInteractionListener : ISplashAdInteractionListener
    {
        private Ads_ios Ads_ios;

        public SplashAdInteractionListener(Ads_ios Ads_ios)
        {
            this.Ads_ios = Ads_ios;
        }

        /// <summary>
        /// Invoke when the Ad is clicked.
        /// </summary>
        public void OnAdClicked(int type)
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + $"splash Ad OnAdClicked type {type} on main thread: {Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId}");
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            //     this.Ads_ios.information.text = "Splash OnAdClicked";
        }

        /// <summary>
        /// Invoke when the Ad is shown.
        /// </summary>
        public void OnAdDidShow(int type)
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + $"splash Ad OnAdDidShow on main thread: {Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId}");
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            //     this.Ads_ios.information.text = "Splash OnAdDidShow";

            LogMediationInfo(Ads_ios);
        }

        public void OnAdWillShow(int type)
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + $"splash Ad OnAdWillShow on main thread: {Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId}");
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            //     this.Ads_ios.information.text = "Splash OnAdWillShow";
        }

        public void OnAdClose(int type)
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + "OnAdClose tpye = " + type);
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            //     this.Ads_ios.information.text = "Splash OnAdClose";

            this.Ads_ios.splashAd.Dispose();
            this.Ads_ios.splashAd = null;
        }
    }
}
#endif