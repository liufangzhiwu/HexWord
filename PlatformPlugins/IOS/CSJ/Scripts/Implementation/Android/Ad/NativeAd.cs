//------------------------------------------------------------------------------
// Copyright (c) 2018-2019 Beijing Bytedance Technology Co., Ltd.
// All Right Reserved.
// Unauthorized copying of this file, via any medium is strictly prohibited.
// Proprietary and confidential.
//------------------------------------------------------------------------------

using ByteDance.Union.Mediation;

namespace ByteDance.Union
{
#if (DEV || !UNITY_EDITOR) && UNITY_ANDROID
    using UnityEngine;

    /// <summary>
    /// The native Ad.
    /// </summary>
    public class NativeAd : IClientBidding
    {
        protected readonly AndroidJavaObject ad;
        private IInteractionAdInteractionListener interactionListener;
        private IDislikeInteractionListener dislikeLinstener;

        /// <summary>
        /// Initializes a new instance of the <see cref="NativeAd"/> class.
        /// </summary>
        internal NativeAd(AndroidJavaObject ad)
        {
            this.ad = ad;
        }

        /// <summary>
        /// Gets the title for this Ad.
        /// </summary>
        public string GetTitle()
        {
            return this.ad.Call<string>("getTitle");
        }

        /// <summary>
        /// Gets the description for this Ad.
        /// </summary>
        public string GetDescription()
        {
            return this.ad.Call<string>("getDescription");
        }

        /// <summary>
        /// Gets the button text.
        /// </summary>
        public string GetButtonText()
        {
            return this.ad.Call<string>("getButtonText");
        }

        /// <summary>
        /// Gets the app score.
        /// </summary>
        public int GetAppScore()
        {
            return this.ad.Call<int>("getAppScore");
        }

        /// <summary>
        /// Gets the app comment number.
        /// </summary>
        public int GetAppCommentNum()
        {
            return this.ad.Call<int>("getAppCommentNum");
        }

        /// <summary>
        /// Gets the app size.
        /// </summary>
        public int GetAppSize()
        {
            return this.ad.Call<int>("getAppSize");
        }

        /// <summary>
        /// Gets the source.
        /// </summary>
        public string GetSource()
        {
            return this.ad.Call<string>("getSource");
        }

        /// <summary>
        /// Gets the interaction type.
        /// </summary>
        public int GetInteractionType()
        {
            return this.ad.Call<int>("getInteractionType");
        }

        /// <summary>
        /// Gets the image mode.
        /// </summary>
        public int GetImageMode()
        {
            return this.ad.Call<int>("getImageMode");
        }

        /// <summary>
        /// Sets the download listener.
        /// </summary>
        public void SetDownloadListener(IAppDownloadListener listener, bool callbackOnMainThread = true)
        {
            var androidListener = new AppDownloadListener(listener, callbackOnMainThread);
            this.ad.Call("setDownloadListener", androidListener);
        }

        public void Dispose()
        {
            NativeAdManager.Instance().DestroyNativeAd(this.ad);
        }

        public void win(double auctionBidToWin)
        {
            ClientBiddingUtils.Win(ad, auctionBidToWin);
        }

        public void Loss(double auctionPrice = double.NaN, string lossReason = null, string winBidder = null)
        {
            ClientBiddingUtils.Loss(ad, auctionPrice, lossReason, winBidder);
        }

        public void setAuctionPrice(double auctionPrice = double.NaN)
        {
            ClientBiddingUtils.SetPrice(ad, auctionPrice);
        }

        public void SetAdInteractionListener(ITTAdInteractionListener ilistener, bool callbackOnMainThread = true)
        {
            TTAdInteractionListener listener = new TTAdInteractionListener(ilistener, callbackOnMainThread);
            ad.Call("setAdInteractionListener", listener);
        }

        public void UploadDislikeEvent(string dislikeStr)
        {
            if (dislikeStr != null)
            {
                ad.Call("uploadDislikeEvent", dislikeStr);
            }
        }

        public MediationNativeManager GetMediationManager()
        {
            return new MediationNativeManager(ad.Call<AndroidJavaObject>("getMediationManager"));
        }

        public void RenderNative(AndroidJavaObject activity,
            IDislikeInteractionListener dislikeInteractionListener, AdSlotType type, bool callbackOnMainThread = true)
        {
        }

        // 安卓只支持自渲染banner
        public void ShowNativeAd(AdSlotType type, float x, float y)
        {
            NativeAdManager.Instance().ShowNativeBannerAd(Utils.GetActivity(), this.ad, interactionListener,
                dislikeLinstener, (int)x, (int)y);
        }

        public void SetNativeAdInteractionListener(IInteractionAdInteractionListener listener, bool callbackOnMainThread = true)
        {
            this.interactionListener = listener;
        }

        public void SetNativeAdDislikeListener(IDislikeInteractionListener dislikeInteractionListener)
        {
            this.dislikeLinstener = dislikeInteractionListener;
        }
    }
#endif
}