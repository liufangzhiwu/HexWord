//------------------------------------------------------------------------------
// Copyright (c) 2018-2019 Beijing Bytedance Technology Co., Ltd.
// All Right Reserved.
// Unauthorized copying of this file, via any medium is strictly prohibited.
// Proprietary and confidential.
//------------------------------------------------------------------------------

using ByteDance.Union;

namespace ByteDance.Union
{
#if !UNITY_EDITOR && UNITY_IOS

    /// <summary>
    /// The advertisement native object for iOS.
    /// </summary>
    public sealed class AdNative
    {
        public void LoadNativeAd(AdSlot adSlot, INativeAdListener listener, bool callbackOnMainThread = true)
        {
            NativeAd.LoadNativeAd(adSlot, listener, callbackOnMainThread);
        }

        public void LoadSplashAd(AdSlot adSlot, ISplashAdListener listener, int timeOut, bool callbackOnMainThread = true)
        {
            BUSplashAd.LoadSplashAd(adSlot, listener, timeOut, callbackOnMainThread);
        }

        public void LoadSplashAd(AdSlot adSlot, ISplashAdListener listener, bool callbackOnMainThread = true)
        {
            BUSplashAd.LoadSplashAd(adSlot, listener, -1, callbackOnMainThread);
        }

        public void LoadRewardVideoAd(AdSlot adSlot, IRewardVideoAdListener listener, bool callbackOnMainThread = true)
        {
            RewardVideoAd.LoadRewardVideoAd(adSlot, listener, callbackOnMainThread);
        }

        public void LoadFullScreenVideoAd(AdSlot adSlot, IFullScreenVideoAdListener listener, bool callbackOnMainThread = true)
        {
            FullScreenVideoAd.LoadFullScreenVideoAd(adSlot, listener, callbackOnMainThread);
        }

        public void LoadNativeExpressAd(AdSlot adSlot, IExpressAdListener listener, bool callbackOnMainThread = true)
        {
            ExpressAd.LoadExpressAdAd(adSlot, listener, callbackOnMainThread);
        }

        public void LoadExpressBannerAd(AdSlot adSlot, IExpressBannerAdListener listener, bool callbackOnMainThread = true)
        {
            ExpressBannerAd.LoadExpressAd(adSlot, listener, callbackOnMainThread);
        }

        public void LoadFeedAd(AdSlot adSlot, IFeedAdListener listener, bool callbackOnMainThread = true)
        {
            FeedAd.LoadFeedAd(adSlot, listener, callbackOnMainThread);
        }

        public void LoadDrawFeedAd(AdSlot adSlot, IDrawFeedAdListener listener, bool callbackOnMainThread = true)
        {
            DrawFeedAd.LoadDrawFeedAd(adSlot, listener, callbackOnMainThread);
        }
    }
#endif
}