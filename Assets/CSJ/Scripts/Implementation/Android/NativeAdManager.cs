namespace ByteDance.Union {
#if !UNITY_EDITOR && UNITY_ANDROID
    using System;
    using UnityEngine;

    /// <summary>
    ///manager for native ad and express ad.
    /// </summary>
    public class NativeAdManager {

        protected readonly AndroidJavaObject nativeAdManager;
        private static NativeAdManager sNativeAdManager = new NativeAdManager();

        /// <summary>
        /// Initializes a new instance of the <see cref="NativeAd"/> class.
        /// </summary>
        private NativeAdManager()
        {
            var jc = new AndroidJavaClass(
                       "com.bytedance.android.NativeAdManager");
            AndroidJavaObject manager = jc.CallStatic<AndroidJavaObject>("getNativeAdManager");
            this.nativeAdManager = manager;
        }
        public static NativeAdManager Instance()
        {
            return sNativeAdManager;
        }
        /// <summary>
        /// Shows the express ad.
        /// </summary>
        /// <param name="expressAd">Express ad.</param>
        /// <param name="listener">Listener.</param>
        /// <param name="dislikeInteractionListener">Dislike interaction listener.</param>
        public void ShowExpressFeedAd(AndroidJavaObject activity, AndroidJavaObject expressAd,IExpressAdInteractionListener listener,
            IDislikeInteractionListener dislikeInteractionListener, bool callbackOnMainThread, int x, int y)
        {
            // this.nativeAdManager.Call("showExpressFeedAd", activity, expressAd,
            //                           new ExpressAdInteractionCallback(listener),
            //                           new DisLikeCallback(dislikeInteractionListener));
            object[] objs = {activity, expressAd,new ExpressAdInteractionCallback(listener,callbackOnMainThread),
                new DisLikeCallback(dislikeInteractionListener,callbackOnMainThread), x, y};
            var signature = "(Landroid.content.Context;Lcom.bytedance.sdk.openadsdk.TTNativeExpressAd;" +
                            "Lcom.bytedance.sdk.openadsdk.TTNativeExpressAd$AdInteractionListener;" +
                            "Lcom.bytedance.sdk.openadsdk.TTAdDislike$DislikeInteractionCallback;II)V";
            CallJavaMethod("showExpressFeedAd",signature, objs);
        }

        private void CallJavaMethod(string methodName,string signature, object[] objs)
        {
            var methodID =
                AndroidJNIHelper.GetMethodID(nativeAdManager.GetRawClass(), methodName, signature);
            var jniArgArray = AndroidJNIHelper.CreateJNIArgArray(objs);
            try
            {
                AndroidJNI.CallVoidMethod(nativeAdManager.GetRawObject(), methodID, jniArgArray);
            }
            finally
            {
                AndroidJNIHelper.DeleteJNIArgArray(objs, jniArgArray);
            }
        }
        
        public void ShowFeedAd(AndroidJavaObject activity, AndroidJavaObject feedAd, IFeedAdInteractionListener listener,
            IDislikeInteractionListener dislike, bool callbackOnMainThread, int x, int y)
        {
            object[] objs =
            {
                activity, feedAd, new FeedInteractionListener(listener),
                new DisLikeCallback(dislike, callbackOnMainThread), x, y
            };
            var signature = "(Landroid.content.Context;Lcom.bytedance.sdk.openadsdk.TTNativeAd;" +
                            "Lcom.bytedance.sdk.openadsdk.TTNativeAd$AdInteractionListener;" +
                            "Lcom.bytedance.sdk.openadsdk.TTAdDislike$DislikeInteractionCallback;II)V";
            CallJavaMethod("showFeedAd", signature, objs);
        }

        /// <summary>
        /// Shows the express banner ad.
        /// </summary>
        /// <param name="expressAd">Express ad.</param>
        /// <param name="listener">Listener.</param>
        /// <param name="dislikeInteractionListener">Dislike interaction listener.</param>
        public void ShowExpressBannerAd(AndroidJavaObject activity, AndroidJavaObject expressAd,IExpressBannerInteractionListener listener,
            IDislikeInteractionListener dislikeInteractionListener,bool callbackOnMainThread, int x, int y)
        { 
            // this.nativeAdManager.Call("showExpressBannerAd", activity, expressAd,
            //                           new ExpressAdInteractionCallback(listener),
            //                           new DisLikeCallback(dislikeInteractionListener));
            object[] objs = {activity, expressAd,new ExpressBannerAdInteractionCallback(listener, callbackOnMainThread),
                new DisLikeCallback(dislikeInteractionListener, callbackOnMainThread), x, y};
            var signature = "(Landroid.content.Context;Lcom.bytedance.sdk.openadsdk.TTNativeExpressAd;" +
                            "Lcom.bytedance.sdk.openadsdk.TTNativeExpressAd$AdInteractionListener;" +
                            "Lcom.bytedance.sdk.openadsdk.TTAdDislike$DislikeInteractionCallback;II)V";
            CallJavaMethod("showExpressBannerAd",signature, objs);

        }

        /// <summary>
        /// Destroy the express feed ad.
        /// </summary>
        /// <param name="expressFeedAd">Express feed ad.</param>
        public void DestroyExpressFeedAd(AndroidJavaObject expressFeedAd) {
            object[] objs = {Utils.GetActivity(), expressFeedAd};
            var signature = "(Landroid.content.Context;Lcom.bytedance.sdk.openadsdk.TTNativeExpressAd;)V";
            CallJavaMethod("destroyExpressFeedAd",signature, objs);
        }

        /// <summary>
        /// Destroy the express ad.
        /// </summary>
        /// <param name="expressBannerAd">Express banner ad.</param>
        public void DestroyExpressBannerAd(AndroidJavaObject expressBannerAd) {
            object[] objs = {Utils.GetActivity(), expressBannerAd};
            var signature = "(Landroid.content.Context;Lcom.bytedance.sdk.openadsdk.TTNativeExpressAd;)V";
            CallJavaMethod("destroyExpressBannerAd",signature, objs);
        }

        /// <summary>
        /// Destroy the feed ad.
        /// </summary>
        /// <param name="feedAd">feed ad.</param>
        public void DestroyFeedAd(AndroidJavaObject feedAd) {
            object[] objs = {Utils.GetActivity(), feedAd};
            var signature = "(Landroid.content.Context;Lcom.bytedance.sdk.openadsdk.TTNativeAd;)V";
            CallJavaMethod("destroyFeedAd",signature, objs);
        }

        /// <summary>
        /// Destroy the native ad. 目前只有banner
        /// </summary>
        /// <param name="nativeAd">native ad.</param>
        public void DestroyNativeAd(AndroidJavaObject nativeAd) {
            object[] objs = {Utils.GetActivity(), nativeAd};
            var signature = "(Landroid.content.Context;Lcom.bytedance.sdk.openadsdk.TTNativeAd;)V";
            CallJavaMethod("destroyBannerAd",signature, objs);
        }

        private sealed class FeedInteractionListener : AndroidJavaProxy
        {
            private IFeedAdInteractionListener mInteractionListener;
            public FeedInteractionListener(IFeedAdInteractionListener listener)
                : base("com.bytedance.sdk.openadsdk.TTNativeAd$AdInteractionListener")
            {
                this.mInteractionListener = listener;
            }

            public void onAdClicked(AndroidJavaObject view, AndroidJavaObject feedAd)
            {
                if (mInteractionListener != null)
                {
                    mInteractionListener.OnAdClicked();
                }
            }

            public void onAdCreativeClick(AndroidJavaObject view, AndroidJavaObject feedAd)
            {
                if (mInteractionListener != null)
                {
                    mInteractionListener.OnAdCreativeClick();
                }
            }

            public void onAdShow(AndroidJavaObject feedAd)
            {
                if (mInteractionListener != null)
                {
                    mInteractionListener.OnAdShow();
                }
            }
        }

        private sealed class ExpressAdInteractionCallback : AndroidJavaProxy
        {
            private IExpressAdInteractionListener listener;
            private readonly bool callbackOnMainThread;

            public ExpressAdInteractionCallback(IExpressAdInteractionListener callback, bool callbackOnMainThread) :
                base("com.bytedance.sdk.openadsdk.TTNativeExpressAd$AdInteractionListener")
            {
                this.listener = callback;
                this.callbackOnMainThread = callbackOnMainThread;
            }

            public void onAdDismiss()
            {
                UnityDispatcher.PostTask(
                    () => this.listener.OnAdClose(null), callbackOnMainThread);
            }

            public void onAdClicked(AndroidJavaObject view, int type)
            {
                UnityDispatcher.PostTask(
                    () => this.listener.OnAdClicked(null), callbackOnMainThread);
            }


            public void onAdShow(AndroidJavaObject view, int type)
            {
                UnityDispatcher.PostTask(
                    () => this.listener.OnAdShow(null), callbackOnMainThread);
            }


            public void onRenderFail(AndroidJavaObject view, string msg, int code)
            {
                UnityDispatcher.PostTask(
                    () => this.listener.OnAdViewRenderError(null, code, msg), callbackOnMainThread);
            }


            public void onRenderSuccess(AndroidJavaObject view, float width, float height)
            {
                UnityDispatcher.PostTask(
                    () => listener.OnAdViewRenderSucc(null, width, height), callbackOnMainThread);
            }
        }

        private sealed class ExpressBannerAdInteractionCallback : AndroidJavaProxy
        {
            private IExpressBannerInteractionListener listener;
            private readonly bool callbackOnMainThread;

            public ExpressBannerAdInteractionCallback(IExpressBannerInteractionListener callback, bool callbackOnMainThread) :
                base("com.bytedance.sdk.openadsdk.TTNativeExpressAd$AdInteractionListener")
            {
                this.listener = callback;
                this.callbackOnMainThread = callbackOnMainThread;
            }

            public void onAdDismiss()
            {
                UnityDispatcher.PostTask(
                    () => this.listener.OnAdClose(), callbackOnMainThread);
            }

            public void onAdClicked(AndroidJavaObject view, int type)
            {
                UnityDispatcher.PostTask(
                    () => this.listener.OnAdClicked(), callbackOnMainThread);
            }


            public void onAdShow(AndroidJavaObject view, int type)
            {
                UnityDispatcher.PostTask(
                    () => this.listener.OnAdShow(), callbackOnMainThread);
            }


            public void onRenderFail(AndroidJavaObject view, string msg, int code)
            {
                UnityDispatcher.PostTask(
                    () => this.listener.OnAdViewRenderError(code, msg), callbackOnMainThread);
            }


            public void onRenderSuccess(AndroidJavaObject view, float width, float height)
            {
                UnityDispatcher.PostTask(
                    () => listener.OnAdViewRenderSucc(width, height), callbackOnMainThread);
            }
        }

        private sealed class DisLikeCallback : AndroidJavaProxy
        {
            private IDislikeInteractionListener dislikeInteractionCallback;
            private readonly bool callbackOnMainThread;

            public DisLikeCallback(IDislikeInteractionListener dislike, bool callbackOnMainThread) : base(
                "com.bytedance.sdk.openadsdk.TTAdDislike$DislikeInteractionCallback")
            {
                this.dislikeInteractionCallback = dislike;
            }

            private void onSelected(int position, string value, bool enforce)
            {
                Debug.Log("CSJM_Unity "+"DisLikeCallback -->onSelected position -" + position + " value---" + value);
                Debug.Log("CSJM_Unity "+$"position -{position} value---{value} dislike onSelected");
                UnityDispatcher.PostTask(
                    () => this.dislikeInteractionCallback.OnSelected(position, value, enforce), callbackOnMainThread);
            }


            private void onCancel()
            {
                UnityDispatcher.PostTask(
                    () => this.dislikeInteractionCallback.OnCancel(), callbackOnMainThread);
            }

            public void onShow()
            {
                UnityDispatcher.PostTask(
                    () => this.dislikeInteractionCallback.OnShow(), callbackOnMainThread);
            }
        }

        public void ShowNativeBannerAd(AndroidJavaObject activity, AndroidJavaObject nativeAd,
            IInteractionAdInteractionListener listener, IDislikeInteractionListener dislikeInteractionListener, int x, int y)
        {
            object[] objs = {activity, nativeAd, new NativeAdInteractionListener(listener), 
                new DisLikeCallback(dislikeInteractionListener, true), x, y};
            var signature = "(Landroid.content.Context;Lcom.bytedance.sdk.openadsdk.TTNativeAd;" +
                            "Lcom.bytedance.sdk.openadsdk.TTNativeAd$AdInteractionListener;" +
                            "Lcom.bytedance.sdk.openadsdk.TTAdDislike$DislikeInteractionCallback;II)V";
            CallJavaMethod("showNativeBannerAd", signature, objs);
        }

        public class NativeAdInteractionListener : AndroidJavaProxy
        {
            private IInteractionAdInteractionListener listener;
            private bool callbackOnMainThread;

            public NativeAdInteractionListener(IInteractionAdInteractionListener listener, bool callbackOnMainThread = true) :
                base("com.bytedance.sdk.openadsdk.TTNativeAd$AdInteractionListener")
            {
                this.listener = listener;
                this.callbackOnMainThread = callbackOnMainThread;
            }

            /**
             * 广告点击的回调，点击后的动作由sdk控制
             */
            public void onAdClicked(AndroidJavaObject view, AndroidJavaObject ad)
            {
                UnityDispatcher.PostTask((() => { listener?.OnAdClicked(); }), callbackOnMainThread);
            }

            /**
             * 创意广告点击回调
             */
            public void onAdCreativeClick(AndroidJavaObject view, AndroidJavaObject ad)
            {
                UnityDispatcher.PostTask((() => { listener?.OnAdCreativeClick(); }), callbackOnMainThread);
            }

            /**
             * 广告展示回调 每个广告仅回调一次
             */
            public void onAdShow(AndroidJavaObject ad)
            {
                UnityDispatcher.PostTask((() => { listener?.OnAdShow(); }), callbackOnMainThread);
            }
        }
    }
#endif
}
