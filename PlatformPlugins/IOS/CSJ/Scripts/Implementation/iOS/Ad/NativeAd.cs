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
    /// The native Ad.
    /// </summary>
    public class NativeAd : IDisposable,IClientBidding
    {
        private static int loadContextID = 0;
        private static Dictionary<int, INativeAdListener> loadListeners =
            new Dictionary<int, INativeAdListener>();

        private static int interactionContextID = 0;
        private static Dictionary<int, IInteractionAdInteractionListener> interactionListeners =
            new Dictionary<int, IInteractionAdInteractionListener>();

        private delegate void NativeAd_OnError(int code, string message, int context);
        private delegate void NativeAd_OnNativeAdLoad(IntPtr nativeAd, int context, int adType);

        private delegate void NativeAd_OnAdShow(int context);
        private delegate void NativeAd_OnAdDidClick(int context);
        private delegate void NativeAd_OnAdClose(int context);
        private delegate void NativeAd_OnAdRemove(int context);

        private IntPtr nativeAd;
        private bool disposed;
        private AdSlotType slotType;

        private static bool _callbackOnMainThread;
        internal NativeAd(IntPtr nativeAd, AdSlotType slotType)
        {
            this.nativeAd = nativeAd;
            this.slotType = slotType;
        }

        ~NativeAd()
        {
            this.Dispose(false);
        }

        public AdSlotType GetAdType()
        {
            return slotType;
        }

        public void ShowNativeAd(AdSlotType type, float x, float y)
        {
            UnionPlatform_NativeAd_ShowNativeAd(this.nativeAd, slotType, x, y);
        }

        internal static void LoadNativeAd(
            AdSlot adSlot, INativeAdListener listener, bool callbackOnMainThread)
        {
            _callbackOnMainThread = callbackOnMainThread;
            var context = loadContextID++;
            loadListeners.Add(context, listener);
            Debug.Log("CSJM_Unity "+adSlot.CodeId);
            AdSlotStruct slot = AdSlotBuilder.getAdSlot(adSlot);
            UnionPlatform_NativeAd_Load(
                ref slot,
                NativeAd_OnErrorMethod,
                NativeAd_OnNativeAdLoadMethod,
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

            UnionPlatform_NativeAd_Dispose(this.nativeAd);
            this.disposed = true;
        }

        public void SetNativeAdInteractionListener(
        IInteractionAdInteractionListener listener, bool callbackOnMainThread = true)
        {
            _callbackOnMainThread = callbackOnMainThread;
            var context = interactionContextID++;
            interactionListeners.Add(context, listener);
            UnionPlatform_NativeAd_SetInteractionListener(
                this.nativeAd,
                NativeAd_OnAdShowMethod,
                NativeAd_OnAdDidClickMethed,
                NativeAd_OnAdCloseMethod,
                NativeAd_OnAdRemoveMethod,
                context);
        }

        public void SetNativeAdDislikeListener(IDislikeInteractionListener dislikeInteractionListener)
        {
        }

        [DllImport("__Internal")]
        private static extern void UnionPlatform_NativeAd_Dispose(
            IntPtr nativeAd);

        [DllImport("__Internal")]
        private static extern void UnionPlatform_NativeAd_Load(
            ref AdSlotStruct slot,
            NativeAd_OnError onError,
            NativeAd_OnNativeAdLoad onNativeAdLoad,
            int context);

        [DllImport("__Internal")]
        private static extern void UnionPlatform_NativeAd_ShowNativeAd(IntPtr nativeAd, AdSlotType slotType, float x, float y);

        [DllImport("__Internal")]
        private static extern void UnionPlatform_NativeAd_SetInteractionListener(
            IntPtr nativeAd,
            NativeAd_OnAdShow onAdShow,
            NativeAd_OnAdDidClick onAdNativeClick,
            NativeAd_OnAdClose onAdClose,
            NativeAd_OnAdRemove onAdRemove,
            int context);

        [AOT.MonoPInvokeCallback(typeof(NativeAd_OnError))]
        private static void NativeAd_OnErrorMethod(int code, string message, int context)
        {
            Debug.Log("CSJM_Unity "+"OnNativeAdError:");
            UnityDispatcher.PostTask(() =>
            {
                INativeAdListener listener;
                if (loadListeners.TryGetValue(context, out listener))
                {
                    loadListeners.Remove(context);
                    listener.OnError(code, message);
                }
                else
                {
                    Debug.LogError("CSJM_Unity "+
                        "The OnError can not find the context.");
                }
            }, _callbackOnMainThread);
        }

        [AOT.MonoPInvokeCallback(typeof(NativeAd_OnNativeAdLoad))]
        private static void NativeAd_OnNativeAdLoadMethod(IntPtr nativeAd, int context, int slotType)
        {
            Debug.Log("CSJM_Unity "+"OnNativeAdLoad:");
            UnityDispatcher.PostTask(() =>
            {
                INativeAdListener listener;
                if (loadListeners.TryGetValue(context, out listener))
                {
                    loadListeners.Remove(context);

                    /**
                     * BUAdSlotAdTypeBanner        = 1,       // banner ads
    BUAdSlotAdTypeInterstitial  = 2,       // interstitial ads
                    BUAdSlotAdTypeFeed = 5,
                     */
                    AdSlotType adType = AdSlotType.Banner;
                    if (slotType == 1)
                    {
                        adType = AdSlotType.Banner;
                    } else if (slotType == 2)
                    {
                        adType = AdSlotType.InteractionAd;
                    } else if (slotType == 5)
                    {
                        adType = AdSlotType.Feed;
                    }

                    NativeAd[] ads = new NativeAd[1];
                    ads[0] = new NativeAd(nativeAd, adType);
                    listener.OnNativeAdLoad(ads);
                } else
                {
                    Debug.LogError("CSJM_Unity "+
                        "The NativeAd_OnNativeAdLoad can not find the context.");
                }
            }, _callbackOnMainThread);
        }

        [AOT.MonoPInvokeCallback(typeof(NativeAd_OnAdShow))]
        private static void NativeAd_OnAdShowMethod(int context)
        {
            UnityDispatcher.PostTask(() =>
            {
                IInteractionAdInteractionListener listener;
                if (interactionListeners.TryGetValue(context, out listener))
                {
                    listener.OnAdShow();
                }
                else
                {
                    Debug.LogError("CSJM_Unity "+
                        "The OnAdShow can not find the context.");
                }
            }, _callbackOnMainThread);
        }

        [AOT.MonoPInvokeCallback(typeof(NativeAd_OnAdDidClick))]
        private static void NativeAd_OnAdDidClickMethed(int context)
        {
            UnityDispatcher.PostTask(() =>
            {
                IInteractionAdInteractionListener listener;
                if (interactionListeners.TryGetValue(context, out listener))
                {
                    listener.OnAdClicked();
                }
                else
                {
                    Debug.LogError("CSJM_Unity "+
                        "The OnAdVideoBarClick can not find the context.");
                }
            }, _callbackOnMainThread);
        }

        [AOT.MonoPInvokeCallback(typeof(NativeAd_OnAdClose))]
        private static void NativeAd_OnAdCloseMethod(int context)
        {
            UnityDispatcher.PostTask(() =>
            {
                IInteractionAdInteractionListener listener;
                if (interactionListeners.TryGetValue(context, out listener))
                {
                    listener.OnAdDismiss();
                }
                else
                {
                    Debug.LogError("CSJM_Unity "+
                        "The OnAdClose can not find the context.");
                }
            }, _callbackOnMainThread);
        }

        [AOT.MonoPInvokeCallback(typeof(NativeAd_OnAdRemove))]
        private static void NativeAd_OnAdRemoveMethod(int context)
        {
            Debug.Log("CSJM_Unity "+"onAdRemoved");
            UnityDispatcher.PostTask(() =>
            {
                IInteractionAdInteractionListener listener;
                if (interactionListeners.TryGetValue(context, out listener))
                {
                    listener.onAdRemoved();
                }
                else
                {
                    Debug.LogError("CSJM_Unity "+
                        "The onAdRemoved can not find the context.");
                }
            }, _callbackOnMainThread);
        }

        public void setAuctionPrice( double price)
        {
            ClientBidManager.SetAuctionPrice(this.nativeAd,price);
        }

        public void win(double price)
        {
            ClientBidManager.Win(this.nativeAd,price);
        }

        public void Loss( double price, string reason, string bidder)
        {
            ClientBidManager.Loss(this.nativeAd,price,reason,bidder);
        }

        public void SetAdInteractionListener(ITTAdInteractionListener listener, bool callbackOnMainThread = true) {}

        public void UploadDislikeEvent(string dislikeStr) {}

        public string GetTitle()
        {
            return string.Empty;
        }

        public string GetDescription()
        {
            return string.Empty;
        }

        public string GetButtonText()
        {
            return string.Empty;
        }

        public int GetAppScore()
        {
            return 0;
        }

        public int GetAppCommentNum()
        {
            return 0;
        }

        public int GetAppSize()
        {
            return 0;
        }

        public string GetSource()
        {
            return string.Empty;
        }

        public int GetInteractionType()
        {
            return 0;
        }

        public int GetImageMode()
        {
            return 0;
        }

        public void SetDownloadListener(IAppDownloadListener listener)
        {
        }

        public void RenderNative(AndroidJavaObject activity,
            IDislikeInteractionListener dislikeInteractionListener, AdSlotType type, bool callbackOnMainThread = true)
        {
        }

        public MediationNativeManager GetMediationManager()
        {
            return null;
        }
    }
#endif
}
