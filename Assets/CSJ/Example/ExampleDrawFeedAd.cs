using System.Collections.Generic;
using System.Threading;
using ByteDance.Union;
using ByteDance.Union.Mediation;
using Middleware;
using UnityEngine;

/**
 * Draw信息流广告代码示例
 * 注：该接口支持融合Draw信息流，并且支持混出功能，即该接口同时支持模板和自渲染。
 */
public class ExampleDrawFeedAd
{
    public static void LoadDrawFeedAd(Ads_ios Ads_ios)
    {
        if (Ads_ios.drawFeedAd != null)
        {
            Ads_ios.drawFeedAd.Dispose();
            Ads_ios.drawFeedAd = null;
        }
        var adSlot = new AdSlot.Builder()
            .SetCodeId(CSJMDAdPositionId.M_DRAW_ID) // 必传
            .SetExpressViewAcceptedSize(350, 400) //期望模板广告view的size,单位dp
            .SetImageAcceptedSize(1080, 600) //自渲染广告尺寸，单位px
            .SetAdCount(1) //请求广告数量为1条，只支持同一时间显示1条
            .SetMediationAdSlot(
                new MediationAdSlot.Builder()
                    .SetBidNotify(true) // 可选
                    .SetScenarioId("unity-SetScenarioId") // 可选
                    .SetWxAppId("unity-wxAppId") // 可选
                    .SetMuted(true)
                    .SetVolume(0.7f)
                    .Build())
            .Build();
        SDK.CreateAdNative().LoadDrawFeedAd(adSlot, new DrawFeedAdListener(Ads_ios));
    }

    public static void ShowDrawFeedAd(Ads_ios Ads_ios)
    {
        if (Ads_ios.drawFeedAd == null)
        {
            Debug.LogError("CSJM_Unity "+ "Ads_ios " + "请先加载广告");
            //Ads_ios.information.text = "请先加载广告";
            return;
        }
        Ads_ios.drawFeedAd.SetFeedAdInteractionListener(new FeedAdInteractionListener(Ads_ios));
        Ads_ios.drawFeedAd.SetFeedAdDislikeListener(new FeedAdDislikeCallback(Ads_ios));
        Ads_ios.drawFeedAd.SetVideoAdListener(new FeedVideoListener());
        Ads_ios.drawFeedAd.SetDownloadListener(new AppDownloadListener(Ads_ios));
        Ads_ios.drawFeedAd.SetAdInteractionListener(new TTAdInteractionListener());
        Ads_ios.drawFeedAd.ShowFeedAd(0, 500);
    }

    // 广告加载监听器
    public class DrawFeedAdListener : IDrawFeedAdListener
    {

        private Ads_ios Ads_ios;

        public DrawFeedAdListener(Ads_ios Ads_ios)
        {
            this.Ads_ios = Ads_ios;
        }
        
        public void OnDrawFeedAdLoad(IList<DrawFeedAd> ads)
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + "drawFeedAd loaded, ad size: " + ads.Count);
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            //     this.Ads_ios.information.text = "OnDrawFeedAdLoad";
            if (ads.Count > 0)
            {
                this.Ads_ios.drawFeedAd = ads[0];
            }
        }

        public void OnError(int code, string message)
        {
            Debug.Log("CSJM_Unity"+ "Ads_ios"+ "drawFeed load fail code: " + code + ", msg: " + message);
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            //     this.Ads_ios.information.text = "OnDrawFeedAdLoadFail, code: " + code + ", msg: " + message;
        }
    }

    // 广告展示监听器
    public class FeedAdInteractionListener : IFeedAdInteractionListener
    {

        private Ads_ios Ads_ios;
        
        public FeedAdInteractionListener(Ads_ios Ads_ios)
        {
            this.Ads_ios = Ads_ios;
        }
        public void OnAdClicked()
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + "draw feedAd ad clicked");
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            //     this.Ads_ios.information.text = "draw feed ad clicked";
            this.Ads_ios.drawFeedAd.UploadDislikeEvent("csjm_unity drawFeed dislike test");
        }

        public void OnAdCreativeClick()
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + "draw feedAd ad CreativeClick");
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            //     this.Ads_ios.information.text = "draw feed ad CreativeClick";
        }

        public void OnAdShow()
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + "draw feedAd ad show");
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            //     this.Ads_ios.information.text = "draw feed ad show";

            LogMediationInfo(Ads_ios);
        }
    }

    // dislike监听器
    public class FeedAdDislikeCallback : IDislikeInteractionListener
    {
        private Ads_ios Ads_ios;

        public FeedAdDislikeCallback(Ads_ios Ads_ios)
        {
            this.Ads_ios = Ads_ios;
        }

        public void OnCancel()
        {
            Debug.Log("CSJM_Unity "+ "Ads_ios " + "draw feed ad dislike OnCancel");
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            // {
            //     this.Ads_ios.information.text = "draw feed ad dislike OnCancel";
            // }
        }

        public void OnShow()
        {
            Debug.Log("CSJM_Unity "+ "Ads_ios " + "draw feed ad dislike OnShow");
            if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            {
                //this.Ads_ios.information.text = "draw feed ad OnShow:";
            }
        }

        public void OnSelected(int var1, string var2, bool enforce)
        {
            Debug.Log("CSJM_Unity "+ "Ads_ios " + "draw feed ad dislike OnSelected:" + var2);
            if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            {     
                //this.Ads_ios.information.text = "draw feed ad OnSelected: " + var2;
            }
        }
    }

    // 视频播放状态监听器
    public class FeedVideoListener : IVideoAdListener
    {
        /// <summary>
        /// Invoke when the video loaded.
        /// </summary>
        public void OnVideoLoad(FeedAd feedAd)
        {
            Debug.Log("CSJM_Unity"+ "Ads_ios " + "draw feed OnVideoLoad");
        }

        /// <summary>
        /// Invoke when the video error.
        /// </summary>
        public void OnVideoError(int var1, int var2)
        {
            Debug.Log("CSJM_Unity" + "Ads_ios " + "draw feedOnVideoError");
        }

        /// <summary>
        /// Invoke when the video Ad start to play.
        /// </summary>
        public void OnVideoAdStartPlay(FeedAd feedAd)
        {
            Debug.Log("CSJM_Unity" + "Ads_ios " + "draw feed OnVideoAdStartPlay");
        }

        /// <summary>
        /// Invoke when the video Ad paused.
        /// </summary>
        public void OnVideoAdPaused(FeedAd feedAd)
        {
            Debug.Log("CSJM_Unity" + "Ads_ios " + "draw feed OnVideoAdPaused");
        }

        /// <summary>
        /// Invoke when the video continue to play.
        /// </summary>
        public void OnVideoAdContinuePlay(FeedAd feedAd)
        {
            Debug.Log("CSJM_Unity" + "Ads_ios " + "draw feed OnVideoAdContinuePlay");
        }

        public void OnProgressUpdate(long current, long duration)
        {
            Debug.Log("CSJM_Unity" + "Ads_ios " + "draw feed OnProgressUpdate curr: " + current + ", duration: " + duration);
        }

        public void OnVideoAdComplete(FeedAd feedAd)
        {
            Debug.Log("CSJM_Unity" + "Ads_ios " + "draw feed OnVideoAdComplete");
        }
    }

    // 打印广告相关信息
    private static void LogMediationInfo(Ads_ios Ads_ios)
    {
        MediationAdEcpmInfo showEcpm = Ads_ios.drawFeedAd.GetMediationManager().GetShowEcpm();
        if (showEcpm != null)
        {
            LogUtils.LogMediationAdEcpmInfo(showEcpm, "GetShowEcpm");
        }

        MediationAdEcpmInfo bestEcpm = Ads_ios.drawFeedAd.GetMediationManager().GetBestEcpm();
        if (bestEcpm != null)
        {
            LogUtils.LogMediationAdEcpmInfo(bestEcpm, "GetBestEcpm");
        }

        List<MediationAdEcpmInfo> multiBiddingEcpmList = Ads_ios.drawFeedAd.GetMediationManager().GetMultiBiddingEcpm();
        foreach (MediationAdEcpmInfo item in multiBiddingEcpmList)
        {
            LogUtils.LogMediationAdEcpmInfo(item, "GetMultiBiddingEcpm");
        }

        List<MediationAdEcpmInfo> cacheList = Ads_ios.drawFeedAd.GetMediationManager().GetCacheList();
        foreach (MediationAdEcpmInfo item in cacheList)
        {
            LogUtils.LogMediationAdEcpmInfo(item, "GetCacheList");
        }

        List<MediationAdLoadInfo> adLoadInfoList = Ads_ios.drawFeedAd.GetMediationManager().GetAdLoadInfo();
        foreach (MediationAdLoadInfo item in adLoadInfoList)
        {
            LogUtils.LogAdLoadInfo(item);
        }
    }
}
