#if UNITY_IOS
using System.Collections.Generic;
using System.Threading;
using ByteDance.Union;
using ByteDance.Union.Mediation;
using Middleware;
using UnityEngine;

public class ExampleFullScreenVideoAd
{
    public static void LoadFullScreenVideoAd(Ads_ios Ads_ios, bool isM)
    {
        if (Ads_ios.fullScreenVideoAd != null)
        {
            Ads_ios.fullScreenVideoAd.Dispose();
            Ads_ios.fullScreenVideoAd = null;
        }
        var adSlot = new AdSlot.Builder()
            .SetCodeId(isM == false ? CSJMDAdPositionId.CSJ_ExpressFullScreen_V_ID :
            CSJMDAdPositionId.M_INTERSTITAL_FULL_SCREEN_ID) // 必传
            .SetOrientation(AdOrientation.Vertical)
            .SetMediationAdSlot(new MediationAdSlot.Builder()
                .SetScenarioId("ScenarioId") // 可选
                .SetUseSurfaceView(false) // 可选
                .SetBidNotify(true) // 可选
                .Build())
            .Build();
        SDK.CreateAdNative().LoadFullScreenVideoAd(adSlot, new FullScreenVideoAdListener(Ads_ios));
    }

    public static void ShowFullScreenVideoAd(Ads_ios Ads_ios)
    {
        if (Ads_ios.fullScreenVideoAd == null)
        {
            Debug.LogError("CSJM_Unity "+ "Ads_ios " + "请先加载广告");
            //Ads_ios.information.text = "请先加载广告";
            return;
        }

        Ads_ios.fullScreenVideoAd.SetFullScreenVideoAdInteractionListener(new FullScreenAdInteractionListener(Ads_ios));
        Ads_ios.fullScreenVideoAd.SetDownloadListener(new AppDownloadListener(Ads_ios));
        Ads_ios.fullScreenVideoAd.SetAdInteractionListener(new TTAdInteractionListener());

        Ads_ios.fullScreenVideoAd.ShowFullScreenVideoAd();
    }

    // 广告加载监听器
    public sealed class FullScreenVideoAdListener : IFullScreenVideoAdListener
    {
        private Ads_ios Ads_ios;

        public FullScreenVideoAdListener(Ads_ios Ads_ios)
        {
            this.Ads_ios = Ads_ios;
        }

        public void OnError(int code, string message)
        {
            Debug.LogError("CSJM_Unity "+ "Ads_ios " + $"OnFullScreenError: {message}  on main thread: {Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId}");
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            //     this.Ads_ios.information.text = "OnFullScreenError: " + message;
        }

        public void OnFullScreenVideoAdLoad(FullScreenVideoAd ad)
        {
            Debug.Log("CSJM_Unity "+ "Ads_ios " + $"OnFullScreenAdLoad  on main thread: {Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId}");
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            //     this.Ads_ios.information.text = "OnFullScreenAdLoad";

            this.Ads_ios.fullScreenVideoAd = ad;
        }

        public void OnFullScreenVideoCached()
        {
            Debug.Log("CSJM_Unity "+ "Ads_ios " + $"OnFullScreenVideoCached  on main thread: {Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId}");
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            //     this.Ads_ios.information.text = "OnFullScreenVideoCached";
        }

        public void OnFullScreenVideoCached(FullScreenVideoAd ad)
        {
        }
    }

    // 广告展示监听器
    public sealed class FullScreenAdInteractionListener : IFullScreenVideoAdInteractionListener
    {
        private Ads_ios Ads_ios;

        public FullScreenAdInteractionListener(Ads_ios Ads_ios)
        {
            this.Ads_ios = Ads_ios;
        }

        public void OnAdShow()
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + $"fullScreenVideoAd show  on main thread: {Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId}");
            if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            {
                //this.Ads_ios.information.text = "fullScreenVideoAd show";
            }

            // log
            LogMediationInfo(Ads_ios);
        }

        public void OnAdVideoBarClick()
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + $"fullScreenVideoAd bar click  on main thread: {Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId}");
            if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            {
                //this.Ads_ios.information.text = "fullScreenVideoAd bar click";
            }
        }

        public void OnAdClose()
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + $"fullScreenVideoAd close  on main thread: {Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId}");
            if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            {
                //this.Ads_ios.information.text = "fullScreenVideoAd close";
            }

            if (this.Ads_ios.fullScreenVideoAd != null)
            {
                this.Ads_ios.fullScreenVideoAd.Dispose();
                this.Ads_ios.fullScreenVideoAd = null;
            }
        }

        public void OnVideoComplete()
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + $"fullScreenVideoAd complete  on main thread: {Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId}");
            if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            {
                Ads_ios._adCompletedCallBackI?.Invoke(true);
                Ads_ios._adCompletedCallBackI = null;
                //this.Ads_ios.information.text = "fullScreenVideoAd complete";
            }
        }

        public void OnVideoError()
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + $"fullScreenVideoAd OnVideoError  on main thread: {Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId}");
            if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            {
                Ads_ios._adCompletedCallBackI?.Invoke(false);
                Ads_ios._adCompletedCallBackI = null;
                //this.Ads_ios.information.text = "fullScreenVideoAd OnVideoError";
            }
        }

        public void OnSkippedVideo()
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + $"fullScreenVideoAd OnSkippedVideo  on main thread: {Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId}");
            if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            {
                //this.Ads_ios.information.text = "fullScreenVideoAd skipped";
            }
        }
    }

    // 打印广告相关信息
    private static void LogMediationInfo(Ads_ios Ads_ios)
    {
        MediationAdEcpmInfo showEcpm = Ads_ios.fullScreenVideoAd.GetMediationManager().GetShowEcpm();
        if (showEcpm != null)
        {
            LogUtils.LogMediationAdEcpmInfo(showEcpm, "GetShowEcpm");
        }

        MediationAdEcpmInfo bestEcpm = Ads_ios.fullScreenVideoAd.GetMediationManager().GetBestEcpm();
        if (bestEcpm != null)
        {
            LogUtils.LogMediationAdEcpmInfo(bestEcpm, "GetBestEcpm");
        }

        List<MediationAdEcpmInfo> multiBiddingEcpmList = Ads_ios.fullScreenVideoAd.GetMediationManager().GetMultiBiddingEcpm();
        foreach (MediationAdEcpmInfo item in multiBiddingEcpmList)
        {
            LogUtils.LogMediationAdEcpmInfo(item, "GetMultiBiddingEcpm");
        }

        List<MediationAdEcpmInfo> cacheList = Ads_ios.fullScreenVideoAd.GetMediationManager().GetCacheList();
        foreach (MediationAdEcpmInfo item in cacheList)
        {
            LogUtils.LogMediationAdEcpmInfo(item, "GetCacheList");
        }

        List<MediationAdLoadInfo> adLoadInfoList = Ads_ios.fullScreenVideoAd.GetMediationManager().GetAdLoadInfo();
        foreach (MediationAdLoadInfo item in adLoadInfoList)
        {
            LogUtils.LogAdLoadInfo(item);
        }
    }
}
#endif
