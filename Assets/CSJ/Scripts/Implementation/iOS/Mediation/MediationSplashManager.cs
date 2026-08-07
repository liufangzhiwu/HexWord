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

    public sealed class MediationSplashManager : IDisposable
    {
        private IntPtr ad;
        internal MediationSplashManager(IntPtr ad)
        {
            this.ad = ad;
        }

        public void Dispose()
        {
            
        }

        public bool IsReady()
        {
            return UnionPlatform_SplashAdMediationisReady(ad);
        }

        public List<MediationAdLoadInfo> GetAdLoadInfo()
        {
            List<MediationAdLoadInfo> infos = new List<MediationAdLoadInfo>();
            string json = UnionPlatform_SplashAdMediationGetAdLoadInfoList(ad);
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
            string json = UnionPlatform_SplashAdMediationCacheRitList(ad);
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
            string json = UnionPlatform_SplashAdMediationMultiBiddingEcpmInfos(ad);
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
            string json = UnionPlatform_SplashAdMediationGetCurrentBestEcpmInfo(ad);
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
            string json = UnionPlatform_SplashAdMediationGetShowEcpmInfo(ad);
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
            
        }

        [DllImport("__Internal")]
        private static extern bool UnionPlatform_SplashAdMediationisReady(IntPtr rewardVideoAd);
        [DllImport("__Internal")]
        private static extern string UnionPlatform_SplashAdMediationGetShowEcpmInfo(IntPtr rewardVideoAd);
        [DllImport("__Internal")]
        private static extern string UnionPlatform_SplashAdMediationGetCurrentBestEcpmInfo(IntPtr rewardVideoAd);
        [DllImport("__Internal")]
        private static extern string UnionPlatform_SplashAdMediationMultiBiddingEcpmInfos(IntPtr rewardVideoAd);
        [DllImport("__Internal")]
        private static extern string UnionPlatform_SplashAdMediationCacheRitList(IntPtr rewardVideoAd);
        [DllImport("__Internal")]
        private static extern string UnionPlatform_SplashAdMediationGetAdLoadInfoList(IntPtr rewardVideoAd);
    }
}

#endif