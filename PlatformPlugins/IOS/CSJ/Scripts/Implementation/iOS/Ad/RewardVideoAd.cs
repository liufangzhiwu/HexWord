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
    /// The reward video Ad.
    /// </summary>
    public sealed class RewardVideoAd : IDisposable,IClientBidding
    {
        private static int loadContextID = 0;
        private static Dictionary<int, IRewardVideoAdListener> loadListeners = new Dictionary<int, IRewardVideoAdListener>();
        private static Dictionary<int, RewardVideoAd> rewardVideoAds = new Dictionary<int, RewardVideoAd>();

        private static int interactionContextID = 0;
        private static Dictionary<int, IRewardAdInteractionListener> interactionListeners = new Dictionary<int, IRewardAdInteractionListener>();

        private delegate void RewardVideoAd_OnError(int code, string message, int context);
        private delegate void RewardVideoAd_OnRewardVideoAdLoad(IntPtr rewardVideoAd, int context);
        private delegate void RewardVideoAd_OnRewardVideoCached(int context);

        private delegate void RewardVideoAd_OnAdShow(int context);
        private delegate void RewardVideoAd_OnAdVideoBarClick(int context);
        private delegate void RewardVideoAd_OnAdClose(int context);
        private delegate void RewardVideoAd_OnVideoComplete(int context);
        private delegate void RewardVideoAd_OnVideoError(int context);
        private delegate void RewardVideoAd_OnVideoSkip(int context);
        private delegate void RewardVideoAd_OnRewardVerify(
            bool rewardVerify,
            int rewardAmount,
            string rewardName,
            int rewardType,
            float rewardPropose,
            int serverErrorCode,
            string serverErrorMsg,
            bool isGromoreServersideVerify,
            string gromoreExtra,
            string transId,
            int reason,
            int errCode,
            string errMsg,
            string adnName,
            string ecpm,
            int context);

        private IntPtr rewardVideoAd;
        private bool disposed;

        private static bool _callbackOnMainThread;
        internal RewardVideoAd(IntPtr rewardVideoAd)
        {
            this.rewardVideoAd = rewardVideoAd;
        }

        ~RewardVideoAd()
        {
            this.Dispose(false);
        }

        internal static void LoadRewardVideoAd(AdSlot adSlot, IRewardVideoAdListener listener, bool callbackOnMainThread)
        {
            _callbackOnMainThread = callbackOnMainThread;
            var context = loadContextID++;
            loadListeners.Add(context, listener);
            AdSlotStruct slot = AdSlotBuilder.getAdSlot(adSlot);
            UnionPlatform_RewardVideoAd_Load(
                ref slot,
                RewardVideoAd_OnErrorMethod,
                RewardVideoAd_OnRewardVideoAdLoadMethod,
                RewardVideoAd_OnRewardVideoCachedMethod,
                context);
        }

        public void Dispose()
        {
            if (this.mediationManager != null)
            {
                this.mediationManager.Dispose();
                this.mediationManager = null;
            }
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }
            UnionPlatform_RewardVideoAd_Dispose(this.rewardVideoAd);
            this.disposed = true;
        }

        /// <summary>
        /// Sets the interaction listener for this Ad.
        /// </summary>
        public void SetRewardAdInteractionListener(IRewardAdInteractionListener listener, bool callbackOnMainThread = true)
        {
            _callbackOnMainThread = callbackOnMainThread;
            var context = interactionContextID++;
            interactionListeners.Add(context, listener);

            UnionPlatform_RewardVideoAd_SetInteractionListener(
                this.rewardVideoAd,
                RewardVideoAd_OnAdShowMethod,
                RewardVideoAd_OnAdVideoBarClickMethod,
                RewardVideoAd_OnAdCloseMethod,
                RewardVideoAd_OnVideoCompleteMethod,
                RewardVideoAd_OnVideoErrorMethod,
                RewardVideoAd_OnVideoSkipMethod,
                RewardVideoAd_OnRewardVerifyMethod,
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
        /// Show the reward video Ad.
        /// </summary>
        public void ShowRewardVideoAd()
        {
            UnionPlatform_RewardVideoAd_ShowRewardVideoAd(this.rewardVideoAd);
        }

        /// <summary>
        /// return the video is From preload
        /// </summary>
        /// <returns>bool</returns>
        public bool materialMetaIsFromPreload()
        {
            return UnionPlatform_RewardVideoAd_MaterialMetaIsFromPreload(this.rewardVideoAd);
        }

        /// <summary>
        /// return the expire time of the video
        /// </summary>
        /// <returns>time stamp</returns>
        public long expireTime()
        {
            return UnionPlatform_RewardVideoAd_ExpireTime(this.rewardVideoAd);
        }

       [DllImport("__Internal")]
        private static extern void UnionPlatform_RewardVideoAd_Load(
            ref AdSlotStruct adSlot,
            RewardVideoAd_OnError onError,
            RewardVideoAd_OnRewardVideoAdLoad onRewardVideoAdLoad,
            RewardVideoAd_OnRewardVideoCached onRewardVideoCached,
            int context);

        [DllImport("__Internal")]
        private static extern void UnionPlatform_RewardVideoAd_SetInteractionListener(
            IntPtr rewardVideoAd,
            RewardVideoAd_OnAdShow onAdShow,
            RewardVideoAd_OnAdVideoBarClick onAdVideoBarClick,
            RewardVideoAd_OnAdClose onAdClose,
            RewardVideoAd_OnVideoComplete onVideoComplete,
            RewardVideoAd_OnVideoError onVideoError,
            RewardVideoAd_OnVideoSkip onVideoSkip,
            RewardVideoAd_OnRewardVerify onRewardVerify,
            int context);

        [DllImport("__Internal")]
        private static extern void UnionPlatform_RewardVideoAd_ShowRewardVideoAd(
            IntPtr rewardVideoAd);

        [DllImport("__Internal")]
        private static extern void UnionPlatform_RewardVideoAd_Dispose(
            IntPtr rewardVideoAd);
        
        [DllImport("__Internal")]
        private static extern bool UnionPlatform_RewardVideoAd_MaterialMetaIsFromPreload(IntPtr rewardVideoAd);

        [DllImport("__Internal")]
        private static extern long UnionPlatform_RewardVideoAd_ExpireTime(IntPtr rewardVideoAd);

        [DllImport("__Internal")]
        private static extern bool UnionPlatform_RewardVideoAd_HaveMediationManager(IntPtr rewardVideoAd);


        [AOT.MonoPInvokeCallback(typeof(RewardVideoAd_OnError))]
        private static void RewardVideoAd_OnErrorMethod(int code, string message, int context)
        {
            Debug.Log("CSJM_Unity "+"OnRewardError:" + message);
            UnityDispatcher.PostTask(() =>
            {
                IRewardVideoAdListener listener;
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

        [AOT.MonoPInvokeCallback(typeof(RewardVideoAd_OnRewardVideoAdLoad))]
        private static void RewardVideoAd_OnRewardVideoAdLoadMethod(IntPtr rewardVideoAd, int context)
        {
            Debug.Log("CSJM_Unity "+"OnRewardVideoAdLoad");
            UnityDispatcher.PostTask(() =>
            {
                IRewardVideoAdListener listener;
                if (loadListeners.TryGetValue(context, out listener))
                {
                    var ad = new RewardVideoAd(rewardVideoAd);
                    rewardVideoAds.Add(context, ad);
                    listener.OnRewardVideoAdLoad(ad);
                }
                else
                {
                    Debug.LogError("CSJM_Unity "+
                        "The OnRewardVideoAdLoad can not find the context.");
                }
            }, _callbackOnMainThread);
        }

        [AOT.MonoPInvokeCallback(typeof(RewardVideoAd_OnRewardVideoCached))]
        private static void RewardVideoAd_OnRewardVideoCachedMethod(int context)
        {
            Debug.Log("CSJM_Unity "+"OnRewardVideoCached");
            UnityDispatcher.PostTask(() =>
            {
                IRewardVideoAdListener listener;
                if (loadListeners.TryGetValue(context, out listener))
                {
                    listener.OnRewardVideoCached();
                    RewardVideoAd ad;
                    if (rewardVideoAds.TryGetValue(context, out ad))
                    {
                        listener.OnRewardVideoCached(ad);
                        rewardVideoAds.Remove(context);
                    }
                    loadListeners.Remove(context);
                }
                else
                {
                    Debug.LogError("CSJM_Unity "+
                        "The OnRewardVideoCached can not find the context.");
                }
            }, _callbackOnMainThread);
        }

        [AOT.MonoPInvokeCallback(typeof(RewardVideoAd_OnAdShow))]
        private static void RewardVideoAd_OnAdShowMethod(int context)
        {
            Debug.Log("CSJM_Unity "+"rewardVideoAd show");
            UnityDispatcher.PostTask(() =>
            {
                IRewardAdInteractionListener listener;
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

        [AOT.MonoPInvokeCallback(typeof(RewardVideoAd_OnAdVideoBarClick))]
        private static void RewardVideoAd_OnAdVideoBarClickMethod(int context)
        {
            UnityDispatcher.PostTask(() =>
            {
                IRewardAdInteractionListener listener;
                if (interactionListeners.TryGetValue(context, out listener))
                {
                    listener.OnAdVideoBarClick();
                }
                else
                {
                    Debug.LogError("CSJM_Unity "+
                        "The OnAdVideoBarClick can not find the context.");
                }
            }, _callbackOnMainThread);
        }

        [AOT.MonoPInvokeCallback(typeof(RewardVideoAd_OnAdClose))]
        private static void RewardVideoAd_OnAdCloseMethod(int context)
        {
            Debug.Log("CSJM_Unity "+"rewardVideoAd close");
            UnityDispatcher.PostTask(() =>
            {
                IRewardAdInteractionListener listener;
                if (interactionListeners.TryGetValue(context, out listener))
                {
                    listener.OnAdClose();
                    interactionListeners.Remove(context);
                }
                else
                {
                    Debug.LogError("CSJM_Unity "+
                        "The OnAdClose can not find the context.");
                }
            }, _callbackOnMainThread);
        }

        [AOT.MonoPInvokeCallback(typeof(RewardVideoAd_OnVideoComplete))]
        private static void RewardVideoAd_OnVideoCompleteMethod(int context)
        {
            Debug.Log("CSJM_Unity "+"rewardVideoAd complete");
            UnityDispatcher.PostTask(() =>
            {
                IRewardAdInteractionListener listener;
                if (interactionListeners.TryGetValue(context, out listener))
                {
                    listener.OnVideoComplete();
                }
                else
                {
                    Debug.LogError("CSJM_Unity "+
                        "The OnVideoComplete can not find the context.");
                }
            }, _callbackOnMainThread);
        }

        [AOT.MonoPInvokeCallback(typeof(RewardVideoAd_OnVideoError))]
        private static void RewardVideoAd_OnVideoErrorMethod(int context)
        {
            Debug.Log("CSJM_Unity "+"rewardVideoAd error");
            UnityDispatcher.PostTask(() =>
            {
                IRewardAdInteractionListener listener;
                if (interactionListeners.TryGetValue(context, out listener))
                {
                    listener.OnVideoError();
                }
                else
                {
                    Debug.LogError("CSJM_Unity "+
                        "The OnVideoError can not find the context.");
                }
            }, _callbackOnMainThread);
        }

        [AOT.MonoPInvokeCallback(typeof(RewardVideoAd_OnVideoSkip))]
        private static void RewardVideoAd_OnVideoSkipMethod(int context)
        {
            Debug.Log("CSJM_Unity "+"rewardVideoAd skip");
            UnityDispatcher.PostTask(() =>
            {
                IRewardAdInteractionListener listener;
                if (interactionListeners.TryGetValue(context, out listener))
                {
                    listener.OnVideoSkip();
                }
                else
                {
                    Debug.LogError("CSJM_Unity "+
                        "The onVideoSkip can not find the context.");
                }
            }, _callbackOnMainThread);
        }

        [AOT.MonoPInvokeCallback(typeof(RewardVideoAd_OnRewardVerify))]
        private static void RewardVideoAd_OnRewardVerifyMethod(
            bool rewardVerify,
            int rewardAmount,
            string rewardName,
            int rewardType,
            float rewardPropose,
            int serverErrorCode,
            string serverErrorMsg,
            bool isGromoreServersideVerify,
            string gromoreExtra,
            string transId,
            int reason,
            int errCode,
            string errMsg,
            string adnName,
            string ecpm,
            int context)
        {
            Debug.Log("CSJM_Unity "+"rewardVideoAd verify:" + rewardVerify.ToString());
            UnityDispatcher.PostTask(() =>
            {
                IRewardAdInteractionListener listener;
                if (interactionListeners.TryGetValue(context, out listener))
                {
                    var model = new RewardBundleModel(serverErrorCode,
                        serverErrorMsg,
                        rewardName,
                        rewardAmount,
                        rewardPropose,
                        isGromoreServersideVerify,
                        gromoreExtra,
                        transId,
                        reason,
                        errCode,
                        errMsg,
                        adnName,
                        ecpm);
                    listener.OnRewardArrived(rewardVerify, rewardType, model);
                }
                else
                {
                    Debug.LogError("CSJM_Unity "+
                        "The OnRewardVerify can not find the context.");
                }
            }, _callbackOnMainThread);
        }

        public void setAuctionPrice(double price)
        {
            ClientBidManager.SetAuctionPrice(this.rewardVideoAd,price);
        }

        public void win(double price)
        {
            ClientBidManager.Win(this.rewardVideoAd,price);
        }

        public void Loss(double price, string reason, string bidder)
        {
            ClientBidManager.Loss(this.rewardVideoAd,price,reason,bidder);
        }

        public void SetAdInteractionListener(ITTAdInteractionListener listener, bool callbackOnMainThread = true) {}

        private MediationRewardManager mediationManager;
        public MediationRewardManager GetMediationManager()
        {
            bool haveMediationManager = UnionPlatform_RewardVideoAd_HaveMediationManager(this.rewardVideoAd);
            if (haveMediationManager == false)
            {
                return null;
            }
            else {
                if (mediationManager == null) {
                    mediationManager = new MediationRewardManager(this.rewardVideoAd);
                }
                return mediationManager;
            }
        }


    }
#endif
}
