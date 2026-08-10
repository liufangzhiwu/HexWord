//------------------------------------------------------------------------------
// Copyright (c) 2018-2019 Beijing Bytedance Technology Co., Ltd.
// All Right Reserved.
// Unauthorized copying of this file, via any medium is strictly prohibited.
// Proprietary and confidential.
//------------------------------------------------------------------------------

namespace ByteDance.Union
{
#if !UNITY_EDITOR && UNITY_IOS
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using ByteDance.Union.Mediation;
    using UnityEngine;

    /// <summary>
    /// The banner Ad.
    /// </summary>
    public class BUSplashAd : IDisposable, IClientBidding
    {

        private static int loadContextID = 0;
        private static Dictionary<int, ISplashAdListener> loadListeners = new Dictionary<int, ISplashAdListener>();

        private static int interactionContextID = 0;
        private static Dictionary<int, ISplashAdInteractionListener> interactionListeners = new Dictionary<int, ISplashAdInteractionListener>();

        private delegate void SplashAd_OnError(int code, string message, int context);
        private delegate void SplashAd_OnLoad(IntPtr splashAd, int context);

        private delegate void SplashAd_RenderSuccess(int context);
        private delegate void SplashAd_RenderFailed(int context);

        private delegate void SplashAd_WillShow(int context, int type);
        private delegate void SplashAd_DidShow(int context, int type);
        private delegate void SplashAd_OnAdClick(int context, int type);
        private delegate void SplashAd_OnAdClose(int context, int type);

        private delegate void SplashAd_VideoAdDidPlayFinish(int context);

        private IntPtr splashAd;
        private bool disposed;


        protected static bool CallbackOnMainThead;
        public BUSplashAd()
        {
        }

        private BUSplashAd(IntPtr splashAd)
        {
            this.splashAd = splashAd;
        }

        ~BUSplashAd()
        {
            this.Dispose(false);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            UnionPlatform_SplashAd_Dispose(this.splashAd);
            this.disposed = true;
        }

        /// <summary>
        /// Load the  splash Ad.
        /// </summary>
        internal static void LoadSplashAd(AdSlot adSlot, ISplashAdListener listener, int timeOut, bool callbackOnMainThread = true)
        {
            CallbackOnMainThead = callbackOnMainThread;
            var context = loadContextID++;

            loadListeners.Add(context, listener);
            AdSlotStruct slot = AdSlotBuilder.getAdSlot(adSlot);
            UnionPlatform_SplashAd_Load(
                ref slot,
                timeOut,
                SplashAd_OnErrorMethod,
                SplashAd_OnLoadMethod,
                SplashAd_OnRenderSuccessMethod,
                SplashAd_OnRenderFailedMethod,
                context);
        }

        /// <summary>
        /// Sets the interaction listener for this Ad.
        /// </summary>
        public void SetSplashInteractionListener(
            ISplashAdInteractionListener listener, bool callbackOnMainThread = true)
        {
            CallbackOnMainThead = callbackOnMainThread;
            var context = interactionContextID++;
            interactionListeners.Add(context, listener);

            UnionPlatform_SplashAd_SetInteractionListener(
                this.splashAd,
                SplashAd_WillShowMethod,
                SplashAd_DidShowMethod,
                SplashAd_OnAdClickMethod,
                SplashAd_OnAdCloseMethod,
                SplashAd_OnVideoAdDidPlayFinishMethod,
                context);
        }

        /// <summary>
        /// Sets the listener for the Ad download.
        /// </summary>
        public void SetDownloadListener(IAppDownloadListener listener, bool callbackOnMainThread = true)
        {
        }

        /// <summary>
        /// Show the full screen video.
        /// </summary>
        public void ShowSplashAd()
        {
            UnionPlatform_SplashAd_Show(this.splashAd);
        }


        [DllImport("__Internal")]
        private static extern IntPtr UnionPlatform_SplashAd_Load(
            ref AdSlotStruct slot,
            int timeOut,
            SplashAd_OnError onError,
            SplashAd_OnLoad onAdLoad,
            SplashAd_RenderSuccess renderSuccess,
            SplashAd_RenderFailed renderFailed,
            int context);

        [DllImport("__Internal")]
        private static extern void UnionPlatform_SplashAd_SetInteractionListener(
            IntPtr splashAd,
            SplashAd_WillShow willShow,
            SplashAd_DidShow didShow,
            SplashAd_OnAdClick onAdClick,
            SplashAd_OnAdClose onClose,
            SplashAd_VideoAdDidPlayFinish playFinish,
            int context);

        [DllImport("__Internal")]
        private static extern void UnionPlatform_SplashAd_Show(
            IntPtr splashAd);

        [DllImport("__Internal")]
        private static extern void UnionPlatform_SplashAd_Dispose(
            IntPtr splashAd);

        [DllImport("__Internal")]
        private static extern bool UnionPlatform_SplashAdHaveMediationManager(IntPtr splashAd);


        [AOT.MonoPInvokeCallback(typeof(SplashAd_OnError))]
        private static void SplashAd_OnErrorMethod(int code, string message, int context)
        {
            Debug.Log("CSJM_Unity " + "splash load OnError");
            UnityDispatcher.PostTask(() =>
            {
                ISplashAdListener listener;
                if (loadListeners.TryGetValue(context, out listener))
                {
                    loadListeners.Remove(context);
                    listener.OnSplashLoadFail(code, message);
                }
                else
                {
                    Debug.LogError("CSJM_Unity " +
                        "The SplashAd_OnError can not find the context.");
                }
            }, CallbackOnMainThead);
        }

        [AOT.MonoPInvokeCallback(typeof(SplashAd_OnLoad))]
        private static void SplashAd_OnLoadMethod(IntPtr splashAd, int context)
        {
            Debug.Log("CSJM_Unity " + "splash load OnSuccess");
            UnityDispatcher.PostTask(() =>
            {
                ISplashAdListener listener;
                if (loadListeners.TryGetValue(context, out listener))
                {
                    listener.OnSplashLoadSuccess(new BUSplashAd(splashAd));
                }
                else
                {
                    Debug.LogError("CSJM_Unity " +
                        "The SplashAd_OnLoad can not find the context.");
                }
            }, CallbackOnMainThead);
        }

        [AOT.MonoPInvokeCallback(typeof(SplashAd_DidShow))]
        private static void SplashAd_DidShowMethod(int context, int type)
        {
            Debug.Log("CSJM_Unity " + "splash Ad DidShow");
            UnityDispatcher.PostTask(() =>
            {
                ISplashAdInteractionListener listener;
                if (interactionListeners.TryGetValue(context, out listener))
                {
                    listener.OnAdDidShow(type);
                }
                else
                {
                    Debug.LogError("CSJM_Unity " +
                        "The SplashAd_DidShow can not find the context.");
                }
            }, CallbackOnMainThead);
        }

        [AOT.MonoPInvokeCallback(typeof(SplashAd_WillShow))]
        private static void SplashAd_WillShowMethod(int context, int type)
        {
            Debug.Log("CSJM_Unity " + "splash Ad WillShow");
            UnityDispatcher.PostTask(() =>
            {
                ISplashAdInteractionListener listener;
                if (interactionListeners.TryGetValue(context, out listener))
                {
                    listener.OnAdWillShow(type);
                }
                else
                {
                    Debug.LogError("CSJM_Unity " +
                        "The SplashAd_WillShow can not find the context.");
                }
            }, CallbackOnMainThead);
        }

        [AOT.MonoPInvokeCallback(typeof(SplashAd_OnAdClick))]
        private static void SplashAd_OnAdClickMethod(int context, int type)
        {
            Debug.Log("CSJM_Unity " + "splash Ad OnAdClicked type");
            UnityDispatcher.PostTask(() =>
            {
                ISplashAdInteractionListener listener;
                if (interactionListeners.TryGetValue(context, out listener))
                {

                    listener.OnAdClicked(type);
                }
                else
                {
                    Debug.LogError("CSJM_Unity " +
                        "The SplashAd_OnAdClick can not find the context.");
                }
            }, CallbackOnMainThead);
        }

        [AOT.MonoPInvokeCallback(typeof(SplashAd_OnAdClose))]
        private static void SplashAd_OnAdCloseMethod(int context, int type)
        {
            Debug.Log("CSJM_Unity " + "splash Ad OnAdClose");
            UnityDispatcher.PostTask(() =>
            {
                ISplashAdInteractionListener listener;
                if (interactionListeners.TryGetValue(context, out listener))
                {
                    interactionListeners.Remove(context);
                    listener.OnAdClose(type);
                }
                else
                {
                    Debug.LogError("CSJM_Unity " +
                        "The SplashAd_OnAdClose can not find the context.");
                }
            }, CallbackOnMainThead);
        }

        [AOT.MonoPInvokeCallback(typeof(SplashAd_RenderSuccess))]
        private static void SplashAd_OnRenderSuccessMethod(int context)
        {
            Debug.Log("CSJM_Unity " + "splash Ad OnRenderSuccess");
            UnityDispatcher.PostTask(() =>
            {
                ISplashAdListener listener;
                if (loadListeners.TryGetValue(context, out listener))
                {
                    listener.OnSplashRenderSuccess(null);
                }
                else
                {
                    Debug.LogError("CSJM_Unity " +
                        "The SplashAd_OnRenderSuccess can not find the context.");
                }
            }, CallbackOnMainThead);
        }

        [AOT.MonoPInvokeCallback(typeof(SplashAd_RenderFailed))]
        private static void SplashAd_OnRenderFailedMethod(int context)
        {
            Debug.Log("CSJM_Unity " + "splash Ad OnRenderFailed");
            UnityDispatcher.PostTask(() =>
            {
                ISplashAdListener listener;
                if (loadListeners.TryGetValue(context, out listener))
                {
                    loadListeners.Remove(context);
                    listener.OnSplashRenderFail(0, null);
                }
                else
                {
                    Debug.LogError("CSJM_Unity " +
                        "The SplashAd_OnRenderFailed can not find the context.");
                }
            }, CallbackOnMainThead);
        }

        [AOT.MonoPInvokeCallback(typeof(SplashAd_VideoAdDidPlayFinish))]
        private static void SplashAd_OnVideoAdDidPlayFinishMethod(int context)
        {
            Debug.Log("CSJM_Unity " + "splash Ad VideoAdDidPlayFinish");
            UnityDispatcher.PostTask(() =>
            {
                ISplashAdInteractionListener listener;
                if (interactionListeners.TryGetValue(context, out listener))
                {
                    interactionListeners.Remove(context);
                    // todo:增加回调
                }
                else
                {
                    Debug.LogError("CSJM_Unity " +
                        "The SplashAd_VideoAdDidPlayFinish can not find the context.");
                }
            }, CallbackOnMainThead);
        }

        public void setAuctionPrice(double price)
        {
            ClientBidManager.SetAuctionPrice(this.splashAd, price);
        }

        public void win(double price)
        {
            ClientBidManager.Win(this.splashAd, price);
        }

        public void Loss(double price, string reason, string bidder)
        {
            ClientBidManager.Loss(this.splashAd, price, reason, bidder);
        }

        public void SetAdInteractionListener(ITTAdInteractionListener listener, bool callbackOnMainThread = true) { }

        private MediationSplashManager mediationManager;
        public MediationSplashManager GetMediationManager()
        {
            bool haveMediationManager = UnionPlatform_SplashAdHaveMediationManager(this.splashAd);
            if (haveMediationManager == false)
            {
                return null;
            }
            else
            {
                if (mediationManager == null)
                {
                    mediationManager = new MediationSplashManager(this.splashAd);
                }
                return mediationManager;
            }
        }
    }
#endif
}
