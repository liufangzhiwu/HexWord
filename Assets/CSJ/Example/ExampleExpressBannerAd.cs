using System.Collections.Generic;
using System.Threading;
using ByteDance.Union;
using ByteDance.Union.Mediation;
using Middleware;
using UnityEngine;

/**
 * 模板banner代码示例
 * 注：该接口支持融合功能。并且支持将信息流混出到banner中。
 */
public class ExampleExpressBannerAd
{
    public static void LoadExpressBannerAd(Ads_ios Ads_ios, bool isM)
    {
        if (Ads_ios.mExpressBannerAd != null)
        {
            Ads_ios.mExpressBannerAd.Dispose();
            Ads_ios.mExpressBannerAd = null;
        }

        int width = (int) (UnityEngine.Screen.width / UnityEngine.Screen.dpi * 160);
        int height = (int)((float)width / 250 * 150); 
        Debug.Log("CSJM_Unity " + "Ads_ios " + "express banner w: " + width + ", h: " + height + ", dpi: " + (UnityEngine.Screen.dpi/160));

        string adsRit = isM ? CSJMDAdPositionId.M_BANNER_ID : CSJMDAdPositionId.CSJ_BANNER_ID;
        
        var adSlot = new AdSlot.Builder()
            .SetCodeId(adsRit) // 必传
            .SetSlideIntervalTime(30) // 单位秒。仅当单独使用csj是生效，启用融合时使用的是Gromore线上配置。
            //期望模板广告view的size,单位dp
            .SetExpressViewAcceptedSize(width, height)
            .SetMediationAdSlot(
                new MediationAdSlot.Builder()
                    .SetBidNotify(true) // 可选
                    .SetScenarioId("unity-SetScenarioId") // 可选
                    .SetWxAppId("unity-wxAppId") // 可选
                    .SetAllowShowCloseBtn(true) // 可选
                    .SetMuted(true)
                    .SetVolume(0.7f)
                    .Build())
            .Build();

        SDK.CreateAdNative().LoadExpressBannerAd(adSlot, new ExpressBannerAdListener(Ads_ios));
    }

    public static void ShowExpressBannerAd(Ads_ios Ads_ios)
    {
        if (Ads_ios.mExpressBannerAd == null)
        {
            Debug.LogError("CSJM_Unity "+ "Ads_ios " + "请先加载广告");
            //Ads_ios.information.text = "请先加载广告";
            return;
        }

#if UNITY_ANDROID
        Ads_ios.mExpressBannerAd.SetSlideIntervalTime(30 * 1000);
#endif
        Ads_ios.mExpressBannerAd.SetExpressInteractionListener(new ExpressBannerInteractionListener(Ads_ios));
        Ads_ios.mExpressBannerAd.SetDislikeCallback(new ExpressAdDislikeCallback(Ads_ios));
        Ads_ios.mExpressBannerAd.SetDownloadListener(new AppDownloadListener(Ads_ios));
        Ads_ios.mExpressBannerAd.SetAdInteractionListener(new TTAdInteractionListener());
        Ads_ios.mExpressBannerAd.ShowExpressAd(0, 500);
    }

    // 广告加载监听器
    public sealed class ExpressBannerAdListener : IExpressBannerAdListener
    {
        private Ads_ios Ads_ios;

        public ExpressBannerAdListener(Ads_ios Ads_ios)
        {
            this.Ads_ios = Ads_ios;
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            //     this.Ads_ios.information.text = "ExpressBannerAdListener";
        }

        public void OnError(int code, string message)
        {
            Debug.Log("CSJM_Unity "+ "Ads_ios " + "onExpressAdError: " + message);
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            //     this.Ads_ios.information.text = "onExpressBannerAdError";
        }

        public void OnBannerAdLoad(ExpressBannerAd ad)
        {
            Debug.Log("CSJM_Unity "+ "Ads_ios " + "OnExpressBannerAdLoad");
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            //     this.Ads_ios.information.text = "OnExpressBannerAdLoad";
            this.Ads_ios.mExpressBannerAd = ad;
        }
    }

    // 广告展示监听器
    public sealed class ExpressBannerInteractionListener : IExpressBannerInteractionListener
    {
        private Ads_ios Ads_ios;

        public ExpressBannerInteractionListener(Ads_ios Ads_ios)
        {
            this.Ads_ios = Ads_ios;
        }

        public void OnAdClicked()
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + "OnAdClicked");
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            //     this.Ads_ios.information.text = "OnAdClicked:";
            this.Ads_ios.mExpressBannerAd.UploadDislikeEvent("csjm_unity expressBanner dislike test");
        }

        public void OnAdShow()
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + "OnAdShow");
            if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            {
                //this.Ads_ios.information.text = "OnAdShow";
            }

            LogMediationInfo(Ads_ios);
        }

        public void OnAdViewRenderError(int code, string message)
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + "express banner OnAdViewRenderError");
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            //     this.Ads_ios.information.text = "express banner OnAdViewRenderError code: " + code + ", msg: " + message;
        }

        public void OnAdViewRenderSucc(float width, float height)
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + "express banner OnAdViewRenderSucc");
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            //     this.Ads_ios.information.text = "express banner OnAdViewRenderSucc:";
        }

        public void OnAdClose()
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + "OnAdClose");
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            //     this.Ads_ios.information.text = "OnAdClose:";
        }

        public void onAdRemoved()
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + "onAdRemoved");
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            //     this.Ads_ios.information.text = "onAdRemoved:";
        }
    }

    // dislike监听器
    public sealed class ExpressAdDislikeCallback : IDislikeInteractionListener
    {
        private Ads_ios Ads_ios;

        public ExpressAdDislikeCallback(Ads_ios Ads_ios)
        {
            this.Ads_ios = Ads_ios;
        }

        public void OnCancel()
        {
            Debug.Log("CSJM_Unity "+ "Ads_ios " + "express banner dislike OnCancel");
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            //     this.Ads_ios.information.text = "ExpressBannerAdDislikeCallback cancle";
        }

        public void OnShow()
        {
            Debug.Log("CSJM_Unity "+ "Ads_ios " + "express banner dislike OnShow");
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            //     this.Ads_ios.information.text = "ExpressBannerAdDislikeCallback OnShow";
        }

        public void OnSelected(int var1, string var2, bool enforce)
        {
            Debug.Log("CSJM_Unity "+ "Ads_ios " + "express banner dislike OnSelected:" + var2);
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            //     this.Ads_ios.information.text = "ExpressBannerAdDislikeCallback OnSelected";
            //释放广告资源
            if (this.Ads_ios.mExpressBannerAd != null)
            {
                this.Ads_ios.mExpressBannerAd.Dispose();
                this.Ads_ios.mExpressBannerAd = null;
            }
        }
    }

    // 打印广告相关信息
    private static void LogMediationInfo(Ads_ios Ads_ios)
    {
        MediationAdEcpmInfo showEcpm = Ads_ios.mExpressBannerAd.GetMediationManager().GetShowEcpm();
        if (showEcpm != null)
        {
            LogUtils.LogMediationAdEcpmInfo(showEcpm, "GetShowEcpm");
        }

        MediationAdEcpmInfo bestEcpm = Ads_ios.mExpressBannerAd.GetMediationManager().GetBestEcpm();
        if (bestEcpm != null)
        {
            LogUtils.LogMediationAdEcpmInfo(bestEcpm, "GetBestEcpm");
        }

        List<MediationAdEcpmInfo> multiBiddingEcpmList = Ads_ios.mExpressBannerAd.GetMediationManager().GetMultiBiddingEcpm();
        foreach (MediationAdEcpmInfo item in multiBiddingEcpmList)
        {
            LogUtils.LogMediationAdEcpmInfo(item, "GetMultiBiddingEcpm");
        }

        List<MediationAdEcpmInfo> cacheList = Ads_ios.mExpressBannerAd.GetMediationManager().GetCacheList();
        foreach (MediationAdEcpmInfo item in cacheList)
        {
            LogUtils.LogMediationAdEcpmInfo(item, "GetCacheList");
        }

        List<MediationAdLoadInfo> adLoadInfoList = Ads_ios.mExpressBannerAd.GetMediationManager().GetAdLoadInfo();
        foreach (MediationAdLoadInfo item in adLoadInfoList)
        {
            LogUtils.LogAdLoadInfo(item);
        }
    }
}
