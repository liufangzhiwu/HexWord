#if UNITY_IOS
using System.Threading;
using ByteDance.Union;
using Middleware;
using UnityEngine;

/**
 * 自渲染banner代码示例
 * 注：仅支持穿山甲代码位，不支持融合
 */
public class ExampleBannerAd
{
    public static void LoadNativeBannerAd(Ads_ios ads_ios)
    {
        if (ads_ios.bannerAd != null)
        {
            ads_ios.bannerAd.Dispose();
            ads_ios.bannerAd = null;
        }

        int width = UnityEngine.Screen.width;
        int height = width / 600 * 257;

        var adSlot = new AdSlot.Builder()
            .SetCodeId(CSJMDAdPositionId.CSJ_NATIVE_BANNER_ID) // 必传
            .SetImageAcceptedSize(width, height) // 单位px
            .SetNativeAdType(AdSlotType.Banner) // 仅支持banner
            .Build();
        // LoadNativeAd接口仅支持自渲染Banner
        SDK.CreateAdNative().LoadNativeAd(adSlot, new NativeBannerAdListener(ads_ios));
    }

    public static void ShowNativeBannerAd(Ads_ios Ads_ios)
    {
        if (Ads_ios.bannerAd == null)
        {
            Debug.LogError("CSJM_Unity "+ "Ads_ios " + "请先加载广告");
            //Ads_ios.information.text = "请先加载广告";
            return;
        }
        
        Ads_ios.bannerAd.SetNativeAdInteractionListener(new NativeBannerAdInteractionListener(Ads_ios));
        Ads_ios.bannerAd.SetNativeAdDislikeListener(new NativeBannerAdDislikeCallback(Ads_ios));
        Ads_ios.bannerAd.SetDownloadListener(new AppDownloadListener(Ads_ios));
        Ads_ios.bannerAd.SetAdInteractionListener(new TTAdInteractionListener());
        Ads_ios.bannerAd.ShowNativeAd(AdSlotType.Banner, 0, 500); // ShowNativeAd仅支持自渲染Banner
    }

    // 广告加载监听器
    public sealed class NativeBannerAdListener : INativeAdListener
    {
        private Ads_ios ads_ios;
        public NativeBannerAdListener(Ads_ios ads_ios)
        {
            this.ads_ios = ads_ios;
        }

        public void OnError(int code, string message)
        {
            Debug.LogError("CSJM_Unity "+ "Ads_ios " + "OnNativeBannerAdError: " + message);
            if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            {
                //this.ads_ios.information.text = "OnNativeBannerAdError: " + message;
            }
        }

        public void OnNativeAdLoad(NativeAd[] ads)
        {
            if (ads == null || ads.Length <= 0)
            {
                Debug.Log("CSJM_Unity "+ "Ads_ios " + $"OnNativeBannerAdLoad ads array is null on main thread: {Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId}");
                if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
                {
                    //this.ads_ios.information.text = "OnNativeBannerAdLoad ads array is null ";
                }

                return;
            }
            this.ads_ios.bannerAd = ads[0];

            Debug.Log("CSJM_Unity "+ "Ads_ios " + $"OnNativeAdLoad on main thread: {Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId}");
            if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            {
                //this.Ads_ios.information.text = "OnNativeAdLoad";
            }
        }
    }

    public sealed class NativeBannerAdInteractionListener : IInteractionAdInteractionListener
    {
        private Ads_ios Ads_ios;

        public NativeBannerAdInteractionListener(Ads_ios Ads_ios)
        {
            this.Ads_ios = Ads_ios;
        }

        public void OnAdCreativeClick()
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + $"NativeAd creative click on main thread: {Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId}");
            if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            {
                //this.Ads_ios.information.text = "NativeAd creative click";
            }
        }

        public void OnAdShow()
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + $"NativeAd show on main thread: {Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId}");
            if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            {
                //this.Ads_ios.information.text = "NativeAd show";
            }
        }

        public void OnAdClicked()
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + $"NativeAd click  on main thread: {Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId}");
            if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            {
                //this.Ads_ios.information.text = "NativeAd click";
            }
            this.Ads_ios.bannerAd.UploadDislikeEvent("csjm_unity nativeBanner dislike test");
        }

        public void OnAdDismiss()
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + $"NativeAd close  on main thread: {Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId}");
            if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            {
                //this.Ads_ios.information.text = "NativeAd close";
            }

            //释放广告资源
            Ads_ios.bannerAd?.Dispose();
        }

        public void onAdRemoved()
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + $"NativeAd onAdRemoved  on main thread: {Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId}");
            if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            {
                //this.Ads_ios.information.text = "NativeAd onAdRemoved";
            }
        }
    }

    public class NativeBannerAdDislikeCallback : IDislikeInteractionListener
    {
        private Ads_ios Ads_ios;

        public NativeBannerAdDislikeCallback(Ads_ios Ads_ios)
        {
            this.Ads_ios = Ads_ios;
        }

        public void OnCancel()
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + "native banner ad dislike OnCancel");
            if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            {
                //this.Ads_ios.information.text = "native banner ad dislike OnCancel";
            }
        }

        public void OnShow()
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + "native banner ad dislike OnShow");
            if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            {
                //this.Ads_ios.information.text = "native banner ad OnShow:";
            }
        }

        public void OnSelected(int var1, string var2, bool enforce)
        {
            Debug.Log("CSJM_Unity " + "Ads_ios " + "native banner ad dislike OnSelected:" + var2);
            if (Thread.CurrentThread.ManagedThreadId == Ads_ios.MainThreadId)
            {
                //this.Ads_ios.information.text = "native banner ad OnSelected:" + var2;
            }
        }
    }
}
#endif