//------------------------------------------------------------------------------
// Copyright (c) 2018-2019 Beijing Bytedance Technology Co., Ltd.
// All Right Reserved.
// Unauthorized copying of this file, via any medium is strictly prohibited.
// Proprietary and confidential.
//------------------------------------------------------------------------------

#if UNITY_EDITOR || (!UNITY_ANDROID && !UNITY_IOS)

namespace ByteDance.Union
{

    using ByteDance.Union.Mediation;
    using UnityEngine;
    /// <summary>
    /// The express Ad.
    /// </summary>
    public sealed class ExpressBannerAd : IClientBidding
    {

        public AndroidJavaObject handle { get { return null; } }


        /// <summary>
        /// Sets the interaction listener for this Ad.
        /// </summary>
        public void SetExpressInteractionListener(IExpressBannerInteractionListener listener, bool callbackOnMainThread = true) { }

        /// <summary>
        /// Sets the dislike callback.
        /// </summary>
        /// <param name="dislikeCallback">Dislike callback.</param>
        public void SetDislikeCallback(IDislikeInteractionListener dislikeCallback, bool callbackOnMainThread = true) { }

        /// <summary>
        /// Sets the download listener.
        /// </summary>
        public void SetDownloadListener(IAppDownloadListener listener, bool callbackOnMainThread = true) { }

        /// <summary>
        /// show the  express Ad
        /// <param name="x">the x of th ad</param>
        /// <param name="y">the y of th ad</param>
        /// </summary>
		public void ShowExpressAd(float x, float y) { }

        /// <summary>
        /// Sets the slide interval time.
        /// </summary>
        /// <param name="intervalTime">Interval time.</param>
        public void SetSlideIntervalTime(int intervalTime) { }

        /// <inheritdoc/>
        public void Dispose() { }

        public void setAuctionPrice(double price) { }

        public void win(double price) { }

        public void Loss(double price, string reason, string bidder) { }

        public MediationBannerManager GetMediationManager() { return null; }

        public void SetAdInteractionListener(ITTAdInteractionListener listener, bool callbackOnMainThread = true) {}
        
        public void UploadDislikeEvent(string dislikeStr) {}

    }

}

#endif