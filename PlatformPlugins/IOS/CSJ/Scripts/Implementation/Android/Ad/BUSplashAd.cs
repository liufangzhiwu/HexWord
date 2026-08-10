//------------------------------------------------------------------------------
// Copyright (c) 2018-2019 Beijing Bytedance Technology Co., Ltd.
// All Right Reserved.
// Unauthorized copying of this file, via any medium is strictly prohibited.
// Proprietary and confidential.
//------------------------------------------------------------------------------

using System;
using ByteDance.Union.Mediation;

namespace ByteDance.Union
{
#if (DEV || !UNITY_EDITOR) && UNITY_ANDROID
    using UnityEngine;

    /// <summary>
    /// The splash Ad.
    /// </summary>
    public class BUSplashAd : IClientBidding
    {
        private readonly AndroidJavaObject ad;

        /// <summary>
        /// Initializes a new instance of the <see cref="SplashAd"/> class.
        /// </summary>
        internal BUSplashAd(AndroidJavaObject ad)
        {
            this.ad = ad;
        }

        /// <summary>
        /// Gets the interaction type.
        /// </summary>
        public int GetInteractionType()
        {
            return this.ad.Call<int>("getInteractionType");
        }

        public void ShowSplashAd()
        {
            var jc = new AndroidJavaClass("com.bytedance.android.SplashAdManager");
            var splashAdManager = jc.CallStatic<AndroidJavaObject>("getSplashAdManager");

            object[] obs = { Utils.GetActivity(), getCurrentSplshAd() };
            var signature = "(Landroid.content.Context;Lcom.bytedance.sdk.openadsdk.CSJSplashAd;)V";
            CallJavaMethod(splashAdManager, "showSplashAd", signature, obs);
        }

        private static void CallJavaMethod(AndroidJavaObject javaObject, string methodName, string signature, object[] obs)
        { 
            var methodID = AndroidJNIHelper.GetMethodID(javaObject.GetRawClass(), methodName, signature);
            var jniArgArray = AndroidJNIHelper.CreateJNIArgArray(obs);
            try
            { 
                AndroidJNI.CallVoidMethod(javaObject.GetRawObject(), methodID, jniArgArray);
            }
            finally
            { 
                AndroidJNIHelper.DeleteJNIArgArray(obs, jniArgArray);
            }   
        }

        /// <summary>
        /// Get current SpalshAd
        /// </summary>
        public AndroidJavaObject getCurrentSplshAd()
        {
            return this.ad;
        }

        /// <summary>
        /// Sets the interaction listener for this Ad.
        /// </summary>
        public void SetSplashInteractionListener(
            ISplashAdInteractionListener listener, bool callbackOnMainThread = true)
        {
            var androidListener = new AdInteractionListener(listener, callbackOnMainThread);
            this.ad.Call("setSplashAdListener", androidListener);
        }

        /// <summary>
        /// Sets the download listener.
        /// </summary>
        public void SetDownloadListener(IAppDownloadListener listener, bool callbackOnMainThread = true)
        {
            var androidListener = new AppDownloadListener(listener, callbackOnMainThread);
            this.ad.Call("setDownloadListener", androidListener);
        }

        /// <summary>
        /// Set this Ad not allow sdk count down.
        /// </summary>
        public void SetNotAllowSdkCountdown()
        {
            this.ad.Call("hideSkipButton");
        }

        public void Dispose()
        {
            var jc = new AndroidJavaClass("com.bytedance.android.SplashAdManager");
            var splashAdManager = jc.CallStatic<AndroidJavaObject>("getSplashAdManager");

            splashAdManager.Call("destorySplashView", Utils.GetActivity());
        }

#pragma warning disable SA1300
#pragma warning disable IDE1006

        private sealed class AdInteractionListener : AndroidJavaProxy
        {
            private readonly ISplashAdInteractionListener listener;
            private bool callbackOnMainThread;
            public AdInteractionListener(
                ISplashAdInteractionListener listener, bool callbackOnMainThread)
                : base("com.bytedance.sdk.openadsdk.CSJSplashAd$SplashAdListener")
            {
                this.listener = listener;
                this.callbackOnMainThread = callbackOnMainThread;
            }

            public void onSplashAdClick(AndroidJavaObject ad)
            {
                UnityDispatcher.PostTask(
                    () => this.listener.OnAdClicked(getType(ad)), callbackOnMainThread);
            }

            public void onSplashAdShow(AndroidJavaObject ad)
            {
                UnityDispatcher.PostTask(
                    () => this.listener.OnAdDidShow(getType(ad)), callbackOnMainThread);
            }

            public void onSplashAdClose(AndroidJavaObject ad, int closeType)
            {
               
                UnityDispatcher.PostTask(() => this.listener.OnAdClose(closeType), callbackOnMainThread);
            }

            public int getType(AndroidJavaObject ad)
            { 
                var type = 0;
                if (ad != null)
                {
                    type = ad.Call<int>("getInteractionType");
                }
                return type;
            }
        }

#pragma warning restore SA1300
#pragma warning restore IDE1006


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

        public MediationSplashManager GetMediationManager()
        {
            return new MediationSplashManager(ad.Call<AndroidJavaObject>("getMediationManager"));
        }
    }
#endif
}