//------------------------------------------------------------------------------
// Copyright (c) 2018-2019 Beijing Bytedance Technology Co., Ltd.
// All Right Reserved.
// Unauthorized copying of this file, via any medium is strictly prohibited.
// Proprietary and confidential.
//------------------------------------------------------------------------------

using System.Collections.Generic;

namespace ByteDance.Union
{
#if !UNITY_EDITOR && UNITY_ANDROID
#pragma warning disable SA1300
#pragma warning disable IDE1006
    using UnityEngine;

    /// <summary>
    /// The android proxy listener for <see cref="IAppDownloadListener"/>.
    /// </summary>
    internal sealed class TTAdInteractionListener : AndroidJavaProxy
    {
        private readonly ITTAdInteractionListener listener;
        private bool callbackOnMainThread;

        public TTAdInteractionListener(
            ITTAdInteractionListener listener, bool callbackOnMainThread)
            : base("com.bytedance.sdk.openadsdk.TTAdInteractionListener")
        {
            this.listener = listener;
            this.callbackOnMainThread = callbackOnMainThread;
        }

        public void onAdEvent(int code, AndroidJavaObject jMap)
        {
            if (listener != null)
            {
                Dictionary<string, System.Object> map = new Dictionary<string, object>();
                if (jMap != null)
                {
                    if (code == AdConst.AD_EVENT_AUTH_DOUYIN)
                    {
                        string uid = jMap.Call<string>("get", "open_uid");
                        map.Add("open_uid", uid);
                    }
                }
                UnityDispatcher.PostTask(() => this.listener.OnAdEvent(code, map), callbackOnMainThread); 
            }
        }
    }

#pragma warning restore SA1300
#pragma warning restore IDE1006
#endif
}
