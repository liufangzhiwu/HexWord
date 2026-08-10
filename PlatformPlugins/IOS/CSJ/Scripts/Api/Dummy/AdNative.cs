//------------------------------------------------------------------------------
// Copyright (c) 2018-2019 Beijing Bytedance Technology Co., Ltd.
// All Right Reserved.
// Unauthorized copying of this file, via any medium is strictly prohibited.
// Proprietary and confidential.
//------------------------------------------------------------------------------

#if UNITY_EDITOR || (!UNITY_ANDROID && !UNITY_IOS)

namespace ByteDance.Union
{

    /// <summary>
    /// The advertisement native object for dummy.
    /// </summary>
    public sealed class AdNative
    {
        /// <summary> 
        /// Load the feed Ad asynchronously and notice on listener.
        /// 5600版本增加支持聚合维度广告加载
        /// </summary>
        public void LoadFeedAd(AdSlot adSlot, IFeedAdListener listener, bool callbackOnMainThread = true)
        {
            listener.OnError(0, "Not Support on this platform");
        }

        /// <summary>
        /// Load the draw feed Ad asynchronously and notice on listener.
        /// 5600版本增加支持聚合维度广告加载
        /// 仅支持聚合维度广告加载
        /// </summary>
        public void LoadDrawFeedAd(AdSlot adSlot, IDrawFeedAdListener listener, bool callbackOnMainThread = true)
        {
            listener.OnError(0, "Not Support on this platform");
        }

        /// <summary>
        /// Load the native Ad asynchronously and notice on listener.
        /// </summary>
        public void LoadNativeAd(AdSlot adSlot, INativeAdListener listener, bool callbackOnMainThread = true)
        {
            listener.OnError(0, "Not Support on this platform");
        }

        /// <summary>
        /// Load the splash Ad asynchronously and notice on listener.
        /// 5600版本增加支持聚合维度广告加载
        /// 6000版本将LoadSplashAd_iOS收敛至该接口
        /// specify timeout.
        /// </summary>
        public void LoadSplashAd(AdSlot adSlot, ISplashAdListener listener, int timeOut, bool callbackOnMainThread = true)
        {
            listener.OnSplashLoadFail(0, "Not Support on this platform");
        }

        /// <summary>
        /// Load the splash Ad asynchronously and notice on listener.
        /// 5600版本增加支持聚合维度广告加载
        /// 6000版本将LoadSplashAd_iOS收敛至该接口
        /// </summary>
        public void LoadSplashAd(AdSlot adSlot, ISplashAdListener listener, bool callbackOnMainThread = true)
        {
            listener.OnSplashLoadFail(0, "Not Support on this platform");
        }

        /// <summary>
        /// Load the reward video Ad asynchronously and notice on listener.
        /// 5600版本增加支持聚合维度广告加载
        /// 6000版本将LoadExpressRewardAd收敛至该接口
        /// </summary>
        public void LoadRewardVideoAd(AdSlot adSlot, IRewardVideoAdListener listener, bool callbackOnMainThread = true)
        {
            listener.OnError(0, "Not Support on this platform");
        }

        /// <summary>
        /// 支持穿山甲新插屏广告加载
        /// 5600版本增加支持聚合维度广告加载
        /// 6000版本将LoadExpressFullScreenVideoAd收敛至该接口
        /// </summary>
        public void LoadFullScreenVideoAd(AdSlot adSlot, IFullScreenVideoAdListener listener, bool callbackOnMainThread = true)
        {
            listener.OnError(0, "Not Support on this platform");
        }

        public void LoadNativeExpressAd(AdSlot adSlot, IExpressAdListener listener, bool callbackOnMainThread = true)
        {
            listener.OnError(0, "Not Support on this platform");
        }

        /// <summary>
        /// 支持穿山甲banner广告加载
        /// 5600版本增加支持聚合维度广告加载
        /// </summary>
        public void LoadExpressBannerAd(AdSlot adSlot, IExpressBannerAdListener listener, bool callbackOnMainThread = true)
        {
            listener.OnError(0, "Not Support on this platform");
        }
    }

}

#endif
