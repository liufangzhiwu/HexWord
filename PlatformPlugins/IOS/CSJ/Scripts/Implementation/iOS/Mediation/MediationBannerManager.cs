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

    public sealed class MediationBannerManager : IDisposable
    {
        private IntPtr bannerAd;
        internal MediationBannerManager(IntPtr bannerAd)
        {
            this.bannerAd = bannerAd;
        }

        public void Dispose()
        {
            
        }

        public bool IsReady()
        {
            return UnionPlatform_bannerMediationisReady(bannerAd);
        }

        public List<MediationAdLoadInfo> GetAdLoadInfo()
        {
            List<MediationAdLoadInfo> infos = new List<MediationAdLoadInfo>();
            string json = UnionPlatform_bannerMediationGetAdLoadInfoList(bannerAd);
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
            string json = UnionPlatform_bannerMediationCacheRitList(bannerAd);
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
            string json = UnionPlatform_bannerMediationMultiBiddingEcpmInfos(bannerAd);
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
            string json = UnionPlatform_bannerMediationGetCurrentBestEcpmInfo(bannerAd);
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
            string json = UnionPlatform_bannerMediationGetShowEcpmInfo(bannerAd);
            if (json == null)
            {
                return null;
            }
            // json反序列化
            MediationAdEcpmInfo info = JsonUtility.FromJson<MediationAdEcpmInfo>(json);
            return info;
        }

        public void destroy()
        {
            UnionPlatform_bannerMediationDestory(bannerAd);
        }

        [DllImport("__Internal")]
        private static extern bool UnionPlatform_bannerMediationisReady(IntPtr bannerAd);
        [DllImport("__Internal")]
        private static extern string UnionPlatform_bannerMediationGetShowEcpmInfo(IntPtr bannerAd);
        [DllImport("__Internal")]
        private static extern string UnionPlatform_bannerMediationGetCurrentBestEcpmInfo(IntPtr bannerAd);
        [DllImport("__Internal")]
        private static extern string UnionPlatform_bannerMediationMultiBiddingEcpmInfos(IntPtr bannerAd);
        [DllImport("__Internal")]
        private static extern string UnionPlatform_bannerMediationCacheRitList(IntPtr bannerAd);
        [DllImport("__Internal")]
        private static extern string UnionPlatform_bannerMediationGetAdLoadInfoList(IntPtr bannerAd);
        [DllImport("__Internal")]
        private static extern void UnionPlatform_bannerMediationDestory(IntPtr bannerAd);
        
    }
}

#endif