//------------------------------------------------------------------------------
// Copyright (c) 2018-2019 Beijing Bytedance Technology Co., Ltd.
// All Right Reserved.
// Unauthorized copying of this file, via any medium is strictly prohibited.
// Proprietary and confidential.
//------------------------------------------------------------------------------

using ByteDance.Union.Mediation;

namespace ByteDance.Union {
#if (DEV || !UNITY_EDITOR) && UNITY_ANDROID

    using UnityEngine;
    /// <summary>
    /// The express banner Ad.
    /// </summary>
    public sealed class ExpressBannerAd : IClientBidding 
    {
        public AndroidJavaObject javaObject;

        private IExpressBannerInteractionListener interactionListener;
        private IDislikeInteractionListener dislikeListener;

        internal ExpressBannerAd (AndroidJavaObject expressAd) {
            this.javaObject = expressAd;
        }
        public AndroidJavaObject handle{get { return this.javaObject; }}

        /// <summary>
        /// Sets the interaction listener for this Ad.
        /// </summary>
        public void SetExpressInteractionListener (IExpressBannerInteractionListener listener, bool callbackOnMainThread = true)
        {
            this.interactionListener = listener;
        }

        /// <summary>
        /// Sets the dislike callback.
        /// </summary>
        /// <param name="dislikeCallback">Dislike callback.</param>
        public void SetDislikeCallback(IDislikeInteractionListener dislikeCallback, bool callbackOnMainThread = true)
        {
            this.dislikeListener = dislikeCallback;
        }

        /// <summary>
        /// Sets the download listener.
        /// </summary>
        public void SetDownloadListener(IAppDownloadListener listener, bool callbackOnMainThread = true)
        {
            var androidListener = new AppDownloadListener(listener, callbackOnMainThread);
            this.javaObject.Call("setDownloadListener", androidListener);
        }

        /// <summary>
        /// show the  express Ad
        /// <param name="x">the x of th ad</param>
        /// <param name="y">the y of th ad</param>
        /// </summary>
        public void ShowExpressAd(float x, float y)
        {
            NativeAdManager.Instance().ShowExpressBannerAd(Utils.GetActivity(), handle, interactionListener,
                dislikeListener, true, (int)x, (int)y);
        }

        /// <inheritdoc/>
        public void Dispose () { 
            NativeAdManager.Instance().DestroyExpressBannerAd(javaObject);
        }

        /// <summary>
        /// Sets the slide interval time.
        /// </summary>
        /// <param name="intervalTime">Interval time.</param>
        public void SetSlideIntervalTime(int intervalTime){
            this.javaObject.Call("setSlideIntervalTime", intervalTime);
        }

        public void win(double auctionBidToWin)
        {
            ClientBiddingUtils.Win(javaObject, auctionBidToWin);
        }

        public void Loss(double auctionPrice = double.NaN, string lossReason = null, string winBidder = null)
        {
            ClientBiddingUtils.Loss(javaObject, auctionPrice, lossReason, winBidder);
        }

        public void setAuctionPrice(double auctionPrice = double.NaN)
        {
            ClientBiddingUtils.SetPrice(javaObject, auctionPrice);
        }

        public void SetAdInteractionListener(ITTAdInteractionListener ilistener, bool callbackOnMainThread = true)
        {
            TTAdInteractionListener listener = new TTAdInteractionListener(ilistener, callbackOnMainThread);
            javaObject.Call("setAdInteractionListener", listener);
        }

        public void UploadDislikeEvent(string dislikeStr)
        {
            if (dislikeStr != null)
            {
                javaObject.Call("uploadDislikeEvent", dislikeStr);
            }
        }

        public MediationBannerManager GetMediationManager()
        {
            return new MediationBannerManager(javaObject.Call<AndroidJavaObject>("getMediationManager"));
        }

    }
#endif
}
