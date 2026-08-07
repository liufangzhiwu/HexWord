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
    using System;
    using UnityEngine;
    /// <summary>
    /// The express feed Ad. 模板信息流广告
    /// </summary>
    public sealed class ExpressAd : IDisposable, IClientBidding
    {

        private IExpressAdInteractionListener interactionListener;
        private IDislikeInteractionListener dislikeListener;
        
        public AndroidJavaObject javaObject;
        public int index;

        internal ExpressAd (AndroidJavaObject expressAd) {
            this.javaObject = expressAd;
        }
        public AndroidJavaObject handle{get { return this.javaObject; }}

        /// <summary>
        /// Gets the interaction type.
        /// </summary>
        public int GetInteractionType () {

          return this.javaObject.Call<int> ("getInteractionType");
            
        }

        /// <summary>
        /// Sets the interaction listener for this Ad.
        /// </summary>
        public void SetExpressInteractionListener (
            IExpressAdInteractionListener listener, bool callbackOnMainThread = true)
        {
            this.interactionListener = listener;
        }

        // 设置dislike监听器
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
        /// Set this Ad not allow sdk count down.
        /// </summary>
        public void SetNotAllowSdkCountdown () { }

        /// <summary>
        /// show the  express Ad
        /// <param name="x">the x of th ad</param>
        /// <param name="y">the y of th ad</param>
        /// </summary>
        public void ShowExpressAd(float x, float y)
        {
            NativeAdManager.Instance().ShowExpressFeedAd(Utils.GetActivity(), handle, interactionListener,
                dislikeListener, true, (int)x, (int)y);
        }

        /// <inheritdoc/>
        public void Dispose () { 
            NativeAdManager.Instance().DestroyExpressFeedAd(javaObject);
        }

        /// <summary>
        /// Sets the slide interval time.
        /// </summary>
        /// <param name="intervalTime">Interval time.</param>
        public void SetSlideIntervalTime(int intervalTime){
            this.javaObject.Call("setSlideIntervalTime", intervalTime);
        }


        private sealed class ExpressAdInteractionCallback : AndroidJavaProxy {
            private IExpressAdInteractionListener listener;
            private bool callbackOnMainThread;
            public ExpressAdInteractionCallback(IExpressAdInteractionListener callback, bool callbackOnMainThread) : base("com.bytedance.sdk.openadsdk.TTNativeExpressAd$ExpressAdInteractionListener") {
                this.listener = callback;
                this.callbackOnMainThread = callbackOnMainThread;
            }

            public void onAdClicked(AndroidJavaObject view, int type) {
                UnityDispatcher.PostTask(
                   () => this.listener.OnAdClicked(null), callbackOnMainThread);
            }


            public void onAdShow(AndroidJavaObject view, int type) {
                UnityDispatcher.PostTask(
                   () => this.listener.OnAdShow(null), callbackOnMainThread);
            }


            public void onRenderFail(AndroidJavaObject view, string msg, int code) {
                UnityDispatcher.PostTask(
                   () => this.listener.OnAdViewRenderError(null,code,msg), callbackOnMainThread);
            }


            public void onRenderSuccess(AndroidJavaObject view, float width, float height) {
                Debug.Log("CSJM_Unity "+"onRenderSuccess ");
                UnityDispatcher.PostTask(
                () => listener.OnAdViewRenderSucc(null, width, height), callbackOnMainThread);
            }
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

        public MediationNativeManager GetMediationManager()
        {
            return new MediationNativeManager(javaObject.Call<AndroidJavaObject>("getMediationManager"));
        }

    }

#endif
}