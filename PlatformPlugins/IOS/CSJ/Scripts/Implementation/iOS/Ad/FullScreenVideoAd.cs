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
    /// The fullScreen video Ad.
    /// </summary>
    public sealed class FullScreenVideoAd : IDisposable, IClientBidding
    {
        private static int loadContextID = 0;
        private static Dictionary<int, IFullScreenVideoAdListener> loadListeners = new Dictionary<int, IFullScreenVideoAdListener>();

        private static int interactionContextID = 0;
        private static Dictionary<int, IFullScreenVideoAdInteractionListener> interactionListeners = new Dictionary<int, IFullScreenVideoAdInteractionListener>();

        private static bool _callbackOnMainThread;

        private delegate void FullScreenVideoAd_OnError(int code, string message, int context);
        private delegate void FullScreenVideoAd_OnFullScreenVideoAdLoad(IntPtr fullScreenVideoAd, int context);
        private delegate void FullScreenVideoAd_OnFullScreenVideoCached(int context);

        private delegate void FullScreenVideoAd_OnAdShow(int context);
        private delegate void FullScreenVideoAd_OnAdVideoBarClick(int context);
        private delegate void FullScreenVideoAd_OnAdVideoClickSkip(int context);
        private delegate void FullScreenVideoAd_OnAdClose(int context);
        private delegate void FullScreenVideoAd_OnVideoComplete(int context);
        private delegate void FullScreenVideoAd_OnVideoError(int context);
        private IntPtr fullScreenVideoAd;
        private bool disposed;

        internal FullScreenVideoAd(IntPtr fullScreenVideoAd)
        {
            this.fullScreenVideoAd = fullScreenVideoAd;
        }

        ~FullScreenVideoAd()
        {
            this.Dispose(false);
        }

        internal static void LoadFullScreenVideoAd(AdSlot adSlot, IFullScreenVideoAdListener listener, bool callbackOnMainThread)
        {
            _callbackOnMainThread = callbackOnMainThread;
            var context = loadContextID++;
            loadListeners.Add(context, listener);

            AdSlotStruct adSlotStruct = AdSlotBuilder.getAdSlot(adSlot);
            UnionPlatform_FullScreenVideoAd_Load(
                ref adSlotStruct,
                FullScreenVideoAd_OnErrorMethod,
                FullScreenVideoAd_OnFullScreenVideoAdLoadMethod,
                FullScreenVideoAd_OnFullScreenVideoCachedMethod,
                context);
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
            UnionPlatform_FullScreenVideoAd_Dispose(this.fullScreenVideoAd);
            this.disposed = true;
        }

        /// <summary>
        /// Sets the interaction listener for this Ad.
        /// </summary>
        public void SetFullScreenVideoAdInteractionListener(
            IFullScreenVideoAdInteractionListener listener, bool callbackOnMainThread = true)
        {
            _callbackOnMainThread = callbackOnMainThread;
            var context = interactionContextID++;
            interactionListeners.Add(context, listener);
            UnionPlatform_FullScreenVideoAd_SetInteractionListener(
                 this.fullScreenVideoAd,
                 FullScreenVideoAd_OnAdShowMethod,
                 FullScreenVideoAd_OnAdVideoBarClickMethod,
                 FullScreenVideoAd_OnAdVideoClickSkipMethod,
                 FullScreenVideoAd_OnAdCloseMethod,
                 FullScreenVideoAd_OnVideoCompleteMethod,
                 FullScreenVideoAd_OnVideoErrorMethod,
                 context);
        }

        /// <summary>
        /// Sets the download listener.
        /// </summary>
        public void SetDownloadListener(IAppDownloadListener listener, bool callbackOnMainThread = true)
        {
        }

        /// <summary>
        /// Gets the interaction type.
        /// </summary>
        public int GetInteractionType()
        {
            return 0;
        }

        /// <summary>
        /// Show the fullScreen video Ad.
        /// </summary>
        public void ShowFullScreenVideoAd()
        {
            UnionPlatform_FullScreenVideoAd_ShowFullScreenVideoAd(this.fullScreenVideoAd);
        }

        /// <summary>
        /// return the video is From preload
        /// </summary>
        /// <returns>bool</returns>
        public bool materialMetaIsFromPreload()
        {
            return UnionPlatform_FullScreenVideoAd_MaterialMetaIsFromPreload(this.fullScreenVideoAd);
        }

        /// <summary>
        /// return the expire time of the video
        /// </summary>
        /// <returns>time stamp</returns>
        public long expireTime()
        {
            return UnionPlatform_FullScreenVideoAd_ExpireTime(this.fullScreenVideoAd);

        }

        public void setAuctionPrice(double price)
        {
            ClientBidManager.SetAuctionPrice(this.fullScreenVideoAd, price);
        }

        public void win(double price)
        {
            ClientBidManager.Win(this.fullScreenVideoAd, price);
        }

        public void Loss(double price, string reason, string bidder)
        {
            ClientBidManager.Loss(this.fullScreenVideoAd, price, reason, bidder);
        }

        public void SetAdInteractionListener(ITTAdInteractionListener listener, bool callbackOnMainThread = true) { }

        private MediationFullScreenManager mediationManager;
        public MediationFullScreenManager GetMediationManager()
        {
            bool haveMediationManager = UnionPlatform_FullScreenVideoAd_HaveMediationManager(this.fullScreenVideoAd);
            if (haveMediationManager == false)
            {
                return null;
            }
            else
            {
                if (mediationManager == null)
                {
                    mediationManager = new MediationFullScreenManager(this.fullScreenVideoAd);
                }
                return mediationManager;
            }
        }





        [DllImport("__Internal")]
        private static extern void UnionPlatform_FullScreenVideoAd_Load(
            ref AdSlotStruct adSlotStruct,
            FullScreenVideoAd_OnError onError,
            FullScreenVideoAd_OnFullScreenVideoAdLoad onFullScreenVideoAdLoad,
            FullScreenVideoAd_OnFullScreenVideoCached onFullScreenVideoCached,
            int context);

        [DllImport("__Internal")]
        private static extern void UnionPlatform_FullScreenVideoAd_SetInteractionListener(
            IntPtr fullScreenVideoAd,
            FullScreenVideoAd_OnAdShow onAdShow,
            FullScreenVideoAd_OnAdVideoBarClick onAdVideoBarClick,
            FullScreenVideoAd_OnAdVideoClickSkip onAdVideoClickSkip,
            FullScreenVideoAd_OnAdClose onAdClose,
            FullScreenVideoAd_OnVideoComplete onVideoComplete,
            FullScreenVideoAd_OnVideoError onVideoError,
            int context);

        [DllImport("__Internal")]
        private static extern void UnionPlatform_FullScreenVideoAd_ShowFullScreenVideoAd(IntPtr fullScreenVideoAd);

        [DllImport("__Internal")]
        private static extern void UnionPlatform_FullScreenVideoAd_Dispose(IntPtr fullScreenVideoAd);

        [DllImport("__Internal")]
        private static extern bool UnionPlatform_FullScreenVideoAd_MaterialMetaIsFromPreload(IntPtr fullscreenVideoAd);

        [DllImport("__Internal")]
        private static extern long UnionPlatform_FullScreenVideoAd_ExpireTime(IntPtr fullscreenVideoAd);

        [DllImport("__Internal")]
        private static extern bool UnionPlatform_FullScreenVideoAd_HaveMediationManager(IntPtr fullscreenVideoAd);







        [AOT.MonoPInvokeCallback(typeof(FullScreenVideoAd_OnError))]
        private static void FullScreenVideoAd_OnErrorMethod(int code, string message, int context)
        {
            Debug.Log("CSJM_Unity " + "OnFullScreenError: " + message);
            UnityDispatcher.PostTask(() =>
            {
                IFullScreenVideoAdListener listener;
                if (loadListeners.TryGetValue(context, out listener))
                {
                    loadListeners.Remove(context);
                    listener.OnError(code, message);
                }
                else
                {
                    Debug.LogError("CSJM_Unity " +
                        "The OnError can not find the context.");
                }
            }, _callbackOnMainThread);
        }

        [AOT.MonoPInvokeCallback(typeof(FullScreenVideoAd_OnFullScreenVideoAdLoad))]
        private static void FullScreenVideoAd_OnFullScreenVideoAdLoadMethod(IntPtr fullScreenVideoAd, int context)
        {
            Debug.Log("CSJM_Unity " + "OnFullScreenAdLoad");
            UnityDispatcher.PostTask(() =>
            {
                IFullScreenVideoAdListener listener;
                if (loadListeners.TryGetValue(context, out listener))
                {
                    listener.OnFullScreenVideoAdLoad(new FullScreenVideoAd(fullScreenVideoAd));
                }
                else
                {
                    Debug.LogError("CSJM_Unity " +
                        "The OnFullScreenVideoAdLoad can not find the context.");
                }
            }, _callbackOnMainThread);
        }

        [AOT.MonoPInvokeCallback(typeof(FullScreenVideoAd_OnFullScreenVideoCached))]
        private static void FullScreenVideoAd_OnFullScreenVideoCachedMethod(int context)
        {
            Debug.Log("CSJM_Unity " + "OnFullScreenVideoCached");
            UnityDispatcher.PostTask(() =>
            {
                IFullScreenVideoAdListener listener;
                if (loadListeners.TryGetValue(context, out listener))
                {
                    loadListeners.Remove(context);
                    listener.OnFullScreenVideoCached();
                }
                else
                {
                    Debug.LogError("CSJM_Unity " +
                        "The OnFullScreenVideoCached can not find the context.");
                }
            }, _callbackOnMainThread);
        }

        [AOT.MonoPInvokeCallback(typeof(FullScreenVideoAd_OnAdShow))]
        private static void FullScreenVideoAd_OnAdShowMethod(int context)
        {
            Debug.Log("CSJM_Unity " + "fullScreenVideoAd show");
            UnityDispatcher.PostTask(() =>
            {
                IFullScreenVideoAdInteractionListener listener;
                if (interactionListeners.TryGetValue(context, out listener))
                {
                    listener.OnAdShow();
                }
                else
                {
                    Debug.LogError("CSJM_Unity " +
                        "The OnAdShow can not find the context.");
                }
            }, _callbackOnMainThread);
        }

        [AOT.MonoPInvokeCallback(typeof(FullScreenVideoAd_OnAdVideoBarClick))]
        private static void FullScreenVideoAd_OnAdVideoBarClickMethod(int context)
        {
            Debug.Log("CSJM_Unity " + "fullScreenVideoAd OnAdVideoBarClick");
            UnityDispatcher.PostTask(() =>
            {
                IFullScreenVideoAdInteractionListener listener;
                if (interactionListeners.TryGetValue(context, out listener))
                {
                    listener.OnAdVideoBarClick();
                }
                else
                {
                    Debug.LogError("CSJM_Unity " +
                        "The OnAdVideoBarClick can not find the context.");
                }
            }, _callbackOnMainThread);
        }

        [AOT.MonoPInvokeCallback(typeof(FullScreenVideoAd_OnAdVideoClickSkip))]
        private static void FullScreenVideoAd_OnAdVideoClickSkipMethod(int context)
        {
            Debug.Log("CSJM_Unity " + "fullScreenVideoAd OnSkippedVideo");
            UnityDispatcher.PostTask(() =>
            {
                IFullScreenVideoAdInteractionListener listener;
                if (interactionListeners.TryGetValue(context, out listener))
                {
                    listener.OnSkippedVideo();
                }
                else
                {
                    Debug.LogError("CSJM_Unity " +
                        "The OnSkippedVideo can not find the context.");
                }
            }, _callbackOnMainThread);
        }

        [AOT.MonoPInvokeCallback(typeof(FullScreenVideoAd_OnAdClose))]
        private static void FullScreenVideoAd_OnAdCloseMethod(int context)
        {
            Debug.Log("CSJM_Unity " + "fullScreenVideoAd close");
            UnityDispatcher.PostTask(() =>
            {
                IFullScreenVideoAdInteractionListener listener;
                if (interactionListeners.TryGetValue(context, out listener))
                {
                    listener.OnAdClose();
                    interactionListeners.Remove(context);
                }
                else
                {
                    Debug.LogError("CSJM_Unity " +
                        "The OnAdClose can not find the context.");
                }
            }, _callbackOnMainThread);
        }

        [AOT.MonoPInvokeCallback(typeof(FullScreenVideoAd_OnVideoComplete))]
        private static void FullScreenVideoAd_OnVideoCompleteMethod(int context)
        {
            UnityDispatcher.PostTask(() =>
            {
                IFullScreenVideoAdInteractionListener listener;
                if (interactionListeners.TryGetValue(context, out listener))
                {
                    listener.OnVideoComplete();
                }
                else
                {
                    Debug.LogError("CSJM_Unity " +
                        "The OnVideoComplete can not find the context.");
                }
            }, _callbackOnMainThread);
        }

        [AOT.MonoPInvokeCallback(typeof(FullScreenVideoAd_OnVideoError))]
        private static void FullScreenVideoAd_OnVideoErrorMethod(int context)
        {
            Debug.Log("CSJM_Unity " + "fullScreenVideoAd OnVideoError");
            UnityDispatcher.PostTask(() =>
            {
                IFullScreenVideoAdInteractionListener listener;
                if (interactionListeners.TryGetValue(context, out listener))
                {
                    listener.OnVideoError();
                }
                else
                {
                    Debug.LogError("CSJM_Unity " +
                        "The OnVideoError can not find the context.");
                }
            }, _callbackOnMainThread);
        }



    }
#endif
}
