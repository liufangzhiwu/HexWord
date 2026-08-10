// ------------------------------------------------------------------------------
// Copyright (c) 2018-2019 Beijing Bytedance Technology Co., Ltd.
// All Right Reserved.
// Unauthorized copying of this file, via any medium is strictly prohibited.
// Proprietary and confidential.
// ------------------------------------------------------------------------------

#if UNITY_EDITOR || (!UNITY_ANDROID && !UNITY_IOS)

namespace ByteDance.Union.Mediation
{
    using System;
    using System.Collections.Generic;

    public sealed class MediationNativeManager : IDisposable
    {

        public void Dispose() { }

        public bool IsReady() { return true; }

        public List<MediationAdLoadInfo> GetAdLoadInfo() { return null; }

        public MediationAdEcpmInfo GetBestEcpm() { return null; }

        public List<MediationAdEcpmInfo> GetMultiBiddingEcpm() { return null; }

        public MediationAdEcpmInfo GetShowEcpm() { return null; }

        public List<MediationAdEcpmInfo> GetCacheList() { return null; }

        public bool HasDislike() { return false; }

        public void SetUseCustomVideo(bool isUseCustomVideo) { }

        public void SetShakeViewListener(MediationShakeViewListener listener) { }

        public bool IsExpress() { return false; }
    }
}

#endif