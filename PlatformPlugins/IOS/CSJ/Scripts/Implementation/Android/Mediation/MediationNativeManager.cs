// ------------------------------------------------------------------------------
// Copyright (c) 2018-2019 Beijing Bytedance Technology Co., Ltd.
// All Right Reserved.
// Unauthorized copying of this file, via any medium is strictly prohibited.
// Proprietary and confidential.
// ------------------------------------------------------------------------------

#if !UNITY_EDITOR && UNITY_ANDROID

using UnityEngine;

namespace ByteDance.Union.Mediation
{
    using System;
    using System.Collections.Generic;

    public sealed class MediationNativeManager : IDisposable
    {

        private AndroidJavaObject handle;

        internal MediationNativeManager(AndroidJavaObject javaObject)
        {
            this.handle = javaObject;
        }
        
        public void Dispose()
        {

        }

        public bool IsReady()
        {
            if (handle != null)
            {
                return handle.Call<bool>("isReady");
            }
            return true;
        }

        public List<MediationAdLoadInfo> GetAdLoadInfo()
        {
            if (handle != null)
            {
                return Utils.GetAdLoadInfo(handle);
            }
            return null;
        }

        public MediationAdEcpmInfo GetBestEcpm()
        {
            if (handle != null)
            {
                return Utils.GetBestEcpm(handle);
            }
            return null;
        }

        public List<MediationAdEcpmInfo> GetMultiBiddingEcpm()
        {
            if (handle != null)
            {
                return Utils.GetMultiBiddingEcpm(handle);
            }
            return null;
        }

        public MediationAdEcpmInfo GetShowEcpm()
        {
            if (handle != null)
            {
                return Utils.GetShowEcpm(handle);
            }
            return null;
        }

        public List<MediationAdEcpmInfo> GetCacheList()
        {
            if (handle != null)
            {
                return Utils.GetCacheList(handle);
            }

            return null;
        }
        
        public bool HasDislike()
        {
            if (handle != null)
            {
                return handle.Call<bool>("hasDislike");
            }
            return false;
        }

        public void SetUseCustomVideo(bool isUseCustomVideo)
        {
            if (handle != null)
            {
                handle.Call("setUseCustomVideo", isUseCustomVideo);
            }
        }

        public void SetShakeViewListener(MediationShakeViewListener listener)
        {
            if (handle != null)
            {
                handle.Call("setShakeViewListener", new InnerShakeViewListener(listener));
            }
        }

        public bool IsExpress()
        {
            if (handle != null)
            {
                return handle.Call<bool>("isExpress");
            }
            return false;
        }

        private sealed class InnerShakeViewListener : AndroidJavaProxy
        {

            private MediationShakeViewListener listener;
            
            public InnerShakeViewListener(MediationShakeViewListener listener) :
                base("com.bytedance.sdk.openadsdk.mediation.ad.MediationShakeViewListener")
            {
                this.listener = listener;
            }

            public void onDismissed()
            {
                if (listener != null)
                {
                    listener.OnDismissed();
                }
            }
        }
    }
}

#endif