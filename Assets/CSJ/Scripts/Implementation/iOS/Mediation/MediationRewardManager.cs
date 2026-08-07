// ------------------------------------------------------------------------------
// Copyright (c) 2018-2019 Beijing Bytedance Technology Co., Ltd.
// All Right Reserved.
// Unauthorized copying of this file, via any medium is strictly prohibited.
// Proprietary and confidential.
// ------------------------------------------------------------------------------

#if !UNITY_EDITOR && UNITY_IOS

namespace ByteDance.Union.Mediation
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using Newtonsoft.Json;
    using UnityEngine;

    public sealed class MediationRewardManager : IDisposable
    {
        private IntPtr rewardVideoAd;
        internal MediationRewardManager(IntPtr rewardVideoAd)
        {
            this.rewardVideoAd = rewardVideoAd;
        }

        public void Dispose()
        {
            
        }

        public bool IsReady()
        {
            return UnionPlatform_RewardVideoMediation_isReady(rewardVideoAd);
        }

        public List<MediationAdLoadInfo> GetAdLoadInfo()
        {
            List<MediationAdLoadInfo> infos = new List<MediationAdLoadInfo>();
            string json = UnionPlatform_RewardVideoMediation_GetAdLoadInfoList(rewardVideoAd);
            if (json == null) {
               return infos;
            }
            List<string> listJson = JsonConvert.DeserializeObject<List<string>>(json);
            foreach (var item in listJson)
            {
                // json反序列化
                MediationAdLoadInfo info = JsonConvert.DeserializeObject<MediationAdLoadInfo>(item);
                infos.Add(info);
            }
            return infos;
        }

        public List<MediationAdEcpmInfo> GetCacheList()
        {
            List<MediationAdEcpmInfo> infos = new List<MediationAdEcpmInfo>();
            string json = UnionPlatform_RewardVideoMediation_CacheRitList(rewardVideoAd);
            if (json == null)
            {
                return infos;
            }
            List<string> listJson = JsonConvert.DeserializeObject<List<string>>(json);
            foreach (var item in listJson)
            {
                // json反序列化
                MediationAdEcpmInfo info = JsonConvert.DeserializeObject<MediationAdEcpmInfo>(item);
                infos.Add(info);
            }
            return infos;
        }

        public List<MediationAdEcpmInfo> GetMultiBiddingEcpm()
        {
            List<MediationAdEcpmInfo> infos = new List<MediationAdEcpmInfo>();
            string json = UnionPlatform_RewardVideoMediation_MultiBiddingEcpmInfos(rewardVideoAd);
            if (json == null)
            {
                return infos;
            }
            List<string> listJson = JsonConvert.DeserializeObject<List<string>>(json);
            foreach (var item in listJson)
            {
                // json反序列化
                MediationAdEcpmInfo info = JsonConvert.DeserializeObject<MediationAdEcpmInfo>(item);
                infos.Add(info);
            }
            return infos;
        }

        public MediationAdEcpmInfo GetBestEcpm()
        {
            string json = UnionPlatform_RewardVideoMediation_GetCurrentBestEcpmInfo(rewardVideoAd);
            if (json == null)
            {
                return null;
            }
            // json反序列化
            MediationAdEcpmInfo info = JsonUtility.FromJson<MediationAdEcpmInfo>(json);
            return info;
        }

        public MediationAdEcpmInfo GetShowEcpm()
        {
            string json = UnionPlatform_RewardVideoMediation_GetShowEcpmInfo(rewardVideoAd);
            if (json == null)
            {
                return null;
            }
            // json反序列化
            MediationAdEcpmInfo info = JsonUtility.FromJson<MediationAdEcpmInfo>(json);
            return info;
        }

        public void Destroy()
        {
            
        }

        [DllImport("__Internal")]
        private static extern bool UnionPlatform_RewardVideoMediation_isReady(IntPtr rewardVideoAd);
        [DllImport("__Internal")]
        private static extern string UnionPlatform_RewardVideoMediation_GetShowEcpmInfo(IntPtr rewardVideoAd);
        [DllImport("__Internal")]
        private static extern string UnionPlatform_RewardVideoMediation_GetCurrentBestEcpmInfo(IntPtr rewardVideoAd);
        [DllImport("__Internal")]
        private static extern string UnionPlatform_RewardVideoMediation_MultiBiddingEcpmInfos(IntPtr rewardVideoAd);
        [DllImport("__Internal")]
        private static extern string UnionPlatform_RewardVideoMediation_CacheRitList(IntPtr rewardVideoAd);
        [DllImport("__Internal")]
        private static extern string UnionPlatform_RewardVideoMediation_GetAdLoadInfoList(IntPtr rewardVideoAd);
    }
}

#endif