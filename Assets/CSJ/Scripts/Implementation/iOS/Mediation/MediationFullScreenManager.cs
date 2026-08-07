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

    public sealed class MediationFullScreenManager : IDisposable
    {
        private IntPtr ad;
        internal MediationFullScreenManager(IntPtr ad)
        {
            this.ad = ad;
        }

        public void Dispose()
        {
            
        }

        public bool IsReady()
        {
            return UnionPlatform_FullScreenMediation_isReady(ad);
        }

        public List<MediationAdLoadInfo> GetAdLoadInfo()
        {
            List<MediationAdLoadInfo> infos = new List<MediationAdLoadInfo>();
            string json = UnionPlatform_FullScreenMediation_GetAdLoadInfoList(ad);
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
            string json = UnionPlatform_FullScreenMediation_CacheRitList(ad);
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
            string json = UnionPlatform_FullScreenMediation_MultiBiddingEcpmInfos(ad);
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
            string json = UnionPlatform_FullScreenMediation_GetCurrentBestEcpmInfo(ad);
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
            string json = UnionPlatform_FullScreenMediation_GetShowEcpmInfo(ad);
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
        private static extern bool UnionPlatform_FullScreenMediation_isReady(IntPtr ad);
        [DllImport("__Internal")]
        private static extern string UnionPlatform_FullScreenMediation_GetShowEcpmInfo(IntPtr ad);
        [DllImport("__Internal")]
        private static extern string UnionPlatform_FullScreenMediation_GetCurrentBestEcpmInfo(IntPtr ad);
        [DllImport("__Internal")]
        private static extern string UnionPlatform_FullScreenMediation_MultiBiddingEcpmInfos(IntPtr ad);
        [DllImport("__Internal")]
        private static extern string UnionPlatform_FullScreenMediation_CacheRitList(IntPtr ad);
        [DllImport("__Internal")]
        private static extern string UnionPlatform_FullScreenMediation_GetAdLoadInfoList(IntPtr ad);
    }
}

#endif