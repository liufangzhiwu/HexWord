//------------------------------------------------------------------------------
// Copyright (c) 2018-2019 Beijing Bytedance Technology Co., Ltd.
// All Right Reserved.
// Unauthorized copying of this file, via any medium is strictly prohibited.
// Proprietary and confidential.
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ByteDance.Union.Mediation;
using UnityEngine;

namespace ByteDance.Union
{
#if !UNITY_EDITOR && UNITY_IOS
    /// <summary>
    /// The feed Ad.
    /// </summary>
    public class FeedAd : NativeAd
    {

        private static int loadContextID = 0;
        private static Dictionary<int, IFeedAdListener> loadListeners = new Dictionary<int, IFeedAdListener>();

        private static int interactionContextID = 0;
        private static Dictionary<int, IFeedAdInteractionListener> interactionListeners = new Dictionary<int, IFeedAdInteractionListener>();

        public delegate void FeedAd_OnError(int code, string message, int context);
        public delegate void FeedAd_OnNativeAdLoad(IntPtr feedAd, int context, int adType);

        private delegate void FeedAd_OnAdShow(int context);
        private delegate void FeedAd_OnAdDidClick(int context);
        private delegate void FeedAd_OnAdClose(int context);
        private delegate void FeedAd_OnAdRemove(int context);

        private IntPtr feedAd;
        private bool disposed;

        private static bool _callbackOnMainThread;

        internal FeedAd(IntPtr nativeAd, AdSlotType slotType) : base(nativeAd, slotType)
        {
            this.feedAd = nativeAd;
        }

        ~FeedAd()
        {
            this.Dispose(false);
        }

        public new void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public new void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            UnionPlatform_FeedAd_Dispose(this.feedAd);
            this.disposed = true;
        }

        internal static void LoadFeedAd(AdSlot adSlot, IFeedAdListener listener, bool callbackOnMainThread)
        {
            _callbackOnMainThread = callbackOnMainThread;
            var context = loadContextID++;
            loadListeners.Add(context, listener);
            Debug.Log("CSJM_Unity " + adSlot.CodeId);
            AdSlotStruct slot = AdSlotBuilder.getAdSlot(adSlot);
            UnionPlatform_FeedAd_Load(
                ref slot,
                FeedAd_OnErrorMethod,
                FeedAd_OnNativeAdLoadMethod,
                context);
        }

        /// <summary>
        /// Set the video Ad listener.
        /// </summary>
        public void SetVideoAdListener(IVideoAdListener listener)
        {
        }

        public void SetFeedAdInteractionListener(IFeedAdInteractionListener listener)
        {
            var context = interactionContextID++;
            interactionListeners.Add(context, listener);

            UnionPlatform_FeedAd_SetInteractionListener(
                this.feedAd,
                FeedAd_OnAdShowMethod,
                FeedAd_OnAdDidClickMethed,
                FeedAd_OnAdCloseMethod,
                FeedAd_OnAdRemoveMethod,
                context);
        }

        public void SetFeedAdDislikeListener(IDislikeInteractionListener dislikeInteractionListener)
        {
            
        }

        public void ShowFeedAd(float x, float y)
        {
            UnionPlatform_FeedAd_ShowNativeAd(this.feedAd, 0, x, y);
        }

        [DllImport("__Internal")]
        private static extern void UnionPlatform_FeedAd_Dispose(
           IntPtr nativeAd);

        [DllImport("__Internal")]
        public static extern void UnionPlatform_FeedAd_Load(
            ref AdSlotStruct slot,
            FeedAd_OnError onError,
            FeedAd_OnNativeAdLoad onNativeAdLoad,
            int context);

        [DllImport("__Internal")]
        private static extern void UnionPlatform_FeedAd_ShowNativeAd(
            IntPtr nativeAd,
            AdSlotType slotType,
            float x,
            float y);

        [DllImport("__Internal")]
        private static extern void UnionPlatform_FeedAd_SetInteractionListener(
            IntPtr nativeAd,
            FeedAd_OnAdShow onAdShow,
            FeedAd_OnAdDidClick onAdNativeClick,
            FeedAd_OnAdClose onAdClose,
            FeedAd_OnAdRemove onAdRemove,
            int context);
        [DllImport("__Internal")]
        private static extern bool UnionPlatform_FeedAd_HaveMediationManager(IntPtr nativeAd);

        [AOT.MonoPInvokeCallback(typeof(FeedAd_OnError))]
        private static void FeedAd_OnErrorMethod(int code, string message, int context)
        {
            Debug.Log("CSJM_Unity " + "OnFeedAdError:");
            UnityDispatcher.PostTask(() =>
            {
                IFeedAdListener listener;
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

        [AOT.MonoPInvokeCallback(typeof(FeedAd_OnNativeAdLoad))]
        private static void FeedAd_OnNativeAdLoadMethod(IntPtr nativeAd, int context, int slotType)
        {
            Debug.Log("CSJM_Unity " + "OnFeedAdLoad:");
            UnityDispatcher.PostTask(() =>
            {
                IFeedAdListener listener;
                if (loadListeners.TryGetValue(context, out listener))
                {
                    loadListeners.Remove(context);

                    FeedAd[] ads = new FeedAd[1];
                    ads[0] = new FeedAd(nativeAd, 0);//slotType暂时无用
                    listener.OnFeedAdLoad(ads);
                }
                else
                {
                    Debug.LogError("CSJM_Unity " +
                        "The FeedAd_OnFeedAdLoad can not find the context.");
                }
            }, _callbackOnMainThread);
        }

        [AOT.MonoPInvokeCallback(typeof(FeedAd_OnAdShow))]
        private static void FeedAd_OnAdShowMethod(int context)
        {
            UnityDispatcher.PostTask(() =>
            {
                IFeedAdInteractionListener listener;
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

        [AOT.MonoPInvokeCallback(typeof(FeedAd_OnAdDidClick))]
        private static void FeedAd_OnAdDidClickMethed(int context)
        {
            UnityDispatcher.PostTask(() =>
            {
                IFeedAdInteractionListener listener;
                if (interactionListeners.TryGetValue(context, out listener))
                {
                    listener.OnAdClicked();
                }
                else
                {
                    Debug.LogError("CSJM_Unity " +
                        "The OnAdDidClick can not find the context.");
                }
            }, _callbackOnMainThread);
        }

        [AOT.MonoPInvokeCallback(typeof(FeedAd_OnAdClose))]
        private static void FeedAd_OnAdCloseMethod(int context)
        {
            UnityDispatcher.PostTask(() =>
            {
                IFeedAdInteractionListener listener;
                if (interactionListeners.TryGetValue(context, out listener))
                {
                    //listener.OnAdDismiss();
                }
                else
                {
                    Debug.LogError("CSJM_Unity " +
                        "The OnAdClose can not find the context.");
                }
            }, _callbackOnMainThread);
        }

        [AOT.MonoPInvokeCallback(typeof(FeedAd_OnAdRemove))]
        private static void FeedAd_OnAdRemoveMethod(int context)
        {
            Debug.Log("CSJM_Unity " + "onAdRemoved");
            UnityDispatcher.PostTask(() =>
            {
                IFeedAdInteractionListener listener;
                if (interactionListeners.TryGetValue(context, out listener))
                {
                    //listener.onAdRemoved();
                }
                else
                {
                    Debug.LogError("CSJM_Unity " +
                        "The onAdRemoved can not find the context.");
                }
            }, _callbackOnMainThread);
        }

        private MediationNativeManager mediationManager;
        public MediationNativeManager GetMediationManager()
        {
            bool haveMediationManager = UnionPlatform_FeedAd_HaveMediationManager(this.feedAd);
            if (haveMediationManager == false)
            {
                return null;
            }
            else
            {
                if (mediationManager == null)
                {
                    mediationManager = new MediationNativeManager(this.feedAd);
                }
                return mediationManager;
            }
        }
    }
#endif
}
