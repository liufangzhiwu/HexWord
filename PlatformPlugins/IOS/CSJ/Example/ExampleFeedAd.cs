#if UNITY_IOS
using System.Collections.Generic;
using System.Threading;
using ByteDance.Union;
using ByteDance.Union.Mediation;
using Middleware;
using UnityEngine;

/**
 * 信息流广告代码示例
 * 注：该接口支持融合信息流，并且支持混出功能，即该接口同时支持信息流模板和自渲染。
 * 也支持直接加载csj信息流自渲染代码位。
 */
public class ExampleFeedAd
{
    public static void LoadFeedAd(Ads_ios Ads_ios, bool isM)
    {
        if (Ads_ios.feedAd != null)
        {
            Ads_ios.feedAd.Dispose();
            Ads_ios.feedAd = null;
        }
        var adSlot = new AdSlot.Builder()
            .SetCodeId(isM ? CSJMDAdPositionId.M_NATIVE_NORMAL_ID : CSJMDAdPositionId.CSJ_NATIVE_ID) // 必传
            .SetExpressViewAcceptedSize(350, 400)//期望模板广告view的size,单位dp，高度设置为0,则高度会自适应
            .SetImageAcceptedSize(1080, 600) // 自渲染广告尺寸，单位px
            .SetAdCount(1) //请求广告数量为1条，只支持同一时间显示1条
            .SetMediationAdSlot(
                new MediationAdSlot.Builder()
                    .SetBidNotify(true) // 可选
                    .SetScenarioId("unity-SetScenarioId") // 可选
                    .SetWxAppId("unity-wxAppId") // 可选
                    .SetMuted(true)
                    .SetVolume(0.7f)
                    .SetShakeViewSize(90.0f, 90.0f) // 可选，百度自渲染信息流的摇一摇功能，设置摇一摇图标的大小，单位dp
                    .SetExtraObject(AdConst.KEY_M_MEDIA_AD_ROTATE_VIEW_ENABLE, "0")
                    .SetExtraObject(AdConst.KEY_M_AUTO_PLAY_POLICY, "2")
                    .Build())
            .Build();
        SDK.CreateAdNative().LoadFeedAd(adSlot, new FeedAdListener(Ads_ios));
    }

    public static void ShowFeedAd(Ads_ios Ads_ios)
    {
        if (Ads_ios.feedAd == null)
        {
            Debug.LogError("CSJM_Unity "+ "Ads_ios " + "请先加载广告");
            //Ads_ios.information.text = "请先加载广告";
            return;
        }
        Ads_ios.feedAd.SetFeedAdInteractionListener(new FeedAdInteractionListener(Ads_ios));
        Ads_ios.feedAd.SetFeedAdDislikeListener(new FeedAdDislikeCallback(Ads_ios));
        Ads_ios.feedAd.SetVideoAdListener(new FeedVideoListener());
        Ads_ios.feedAd.SetDownloadListener(new AppDownloadListener(Ads_ios));
        Ads_ios.feedAd.SetAdInteractionListener(new TTAdInteractionListener());
        Ads_ios.feedAd.ShowFeedAd(0, 500);
    }

    // 广告加载监听器
    public class FeedAdListener : IFeedAdListener
    {

        private Ads_ios Ads_ios;

        public FeedAdListener(Ads_ios Ads_ios)
        {
            this.Ads_ios = Ads_ios;
        }
        
        public void OnFeedAdLoad(IList<FeedAd> ads)
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + "feedAd loaded, ad size: " + ads.Count);
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            //     this.Ads_ios.information.text = "OnFeedAdLoad";
            if (ads.Count > 0)
            {
                this.Ads_ios.feedAd = ads[0];
                this.Ads_ios.feedAd.GetMediationManager().SetShakeViewListener(new MyMediationShakeViewListener());
            }
        }

        public void OnError(int code, string message)
        {
            Debug.Log("CSJM_Unity" + "Ads_ios " + "feed load fail code: " + code + ", msg: " + message);
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            //     this.Ads_ios.information.text = "OnFeedAdLoadFail, code: " + code + ", msg: " + message;
        }
        
    }

    // 百度自渲染信息流摇一摇功能，摇一摇view消失时回调
    public class MyMediationShakeViewListener : MediationShakeViewListener
    {
        public void OnDismissed()
        {
            Debug.Log("CSJM_Unity" + "Ads_ios" + ": baidu feed shakeView onDismissed");
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
            Debug.Log("CSJM_Unity " + "Ads_ios " + "feedAd ad clicked");
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            //     this.Ads_ios.information.text = "feed ad clicked";
            this.Ads_ios.feedAd.UploadDislikeEvent("csjm_unity feed dislike test");
        }

        public void OnAdCreativeClick()
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + "feedAd ad CreativeClick");
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            //     this.Ads_ios.information.text = "feed ad CreativeClick";
        }

        public void OnAdShow()
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + "feedAd ad show");
            // if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            //     this.Ads_ios.information.text = "feed ad show";
            
            // log
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
            Debug.Log("CSJM_Unity "+ "Ads_ios " + "feed ad dislike OnCancel");
            if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            {
                //this.Ads_ios.information.text = "feed ad dislike OnCancel";
            }
        }

        public void OnShow()
        {
            Debug.Log("CSJM_Unity "+ "Ads_ios " + "feed ad dislike OnShow");
            if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            {
                //this.Ads_ios.information.text = "feed ad OnShow:";
            }
        }

        public void OnSelected(int var1, string var2, bool enforce)
        {
            Debug.Log("CSJM_Unity "+ "Ads_ios " + "feed ad dislike OnSelected:" + var2);
            if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            {     
                //this.Ads_ios.information.text = "feed ad OnSelected: " + var2;
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
            Debug.Log("CSJM_Unity" + "Ads_ios " + "OnVideoLoad");
        }

        /// <summary>
        /// Invoke when the video error.
        /// </summary>
        public void OnVideoError(int var1, int var2)
        {
            Debug.Log("CSJM_Unity" + "Ads_ios " + "OnVideoError");
        }

        /// <summary>
        /// Invoke when the video Ad start to play.
        /// </summary>
        public void OnVideoAdStartPlay(FeedAd feedAd)
        {
            Debug.Log("CSJM_Unity" + "Ads_ios " + "OnVideoAdStartPlay");
        }

        /// <summary>
        /// Invoke when the video Ad paused.
        /// </summary>
        public void OnVideoAdPaused(FeedAd feedAd)
        {
            Debug.Log("CSJM_Unity" + "Ads_ios " + "OnVideoAdPaused");
        }

        /// <summary>
        /// Invoke when the video continue to play.
        /// </summary>
        public void OnVideoAdContinuePlay(FeedAd feedAd)
        {
            Debug.Log("CSJM_Unity" + "Ads_ios " + "OnVideoAdContinuePlay");
        }

        public void OnProgressUpdate(long current, long duration)
        {
            Debug.Log("CSJM_Unity" + "Ads_ios " + "OnProgressUpdate curr: " + current + ", duration: " + duration);
        }

        public void OnVideoAdComplete(FeedAd feedAd)
        {
            Debug.Log("CSJM_Unity" + "Ads_ios " + "OnVideoAdComplete");
        }
    }
    
    // 打印广告相关信息
    private static void LogMediationInfo(Ads_ios Ads_ios)
    {
        MediationAdEcpmInfo showEcpm = Ads_ios.feedAd.GetMediationManager().GetShowEcpm();
        if (showEcpm != null)
        {
            LogUtils.LogMediationAdEcpmInfo(showEcpm, "GetShowEcpm");
        }

        MediationAdEcpmInfo bestEcpm = Ads_ios.feedAd.GetMediationManager().GetBestEcpm();
        if (bestEcpm != null)
        {
            LogUtils.LogMediationAdEcpmInfo(bestEcpm, "GetBestEcpm");
        }

        List<MediationAdEcpmInfo> multiBiddingEcpmList = Ads_ios.feedAd.GetMediationManager().GetMultiBiddingEcpm();
        foreach (MediationAdEcpmInfo item in multiBiddingEcpmList)
        {
            LogUtils.LogMediationAdEcpmInfo(item, "GetMultiBiddingEcpm");
        }

        List<MediationAdEcpmInfo> cacheList = Ads_ios.feedAd.GetMediationManager().GetCacheList();
        foreach (MediationAdEcpmInfo item in cacheList)
        {
            LogUtils.LogMediationAdEcpmInfo(item, "GetCacheList");
        }

        List<MediationAdLoadInfo> adLoadInfoList = Ads_ios.feedAd.GetMediationManager().GetAdLoadInfo();
        foreach (MediationAdLoadInfo item in adLoadInfoList)
        {
            LogUtils.LogAdLoadInfo(item);
        }
    }
}
#endif