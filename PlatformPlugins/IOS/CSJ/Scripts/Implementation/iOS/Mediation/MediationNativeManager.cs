// ------------------------------------------------------------------------------
// Copyright (c) 2018-2019 Beijing Bytedance Technology Co., Ltd.
// All Right Reserved.
// Unauthorized copying of this file, via any medium is strictly prohibited.
// Proprietary and confidential.
// ------------------------------------------------------------------------------

using UnityEngine;

#if !UNITY_EDITOR && UNITY_IOS

namespace ByteDance.Union.Mediation
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using Newtonsoft.Json;
    using UnityEngine;

    public sealed class MediationNativeManager : IDisposable
    {

        private IntPtr ad;
        internal MediationNativeManager(IntPtr ad)
        {
            this.ad = ad;
        }

        public void Dispose()
        {

        }

        public bool IsReady()
        {
            return UnionPlatform_FeedAdMediationisReady(ad);
        }

        public List<MediationAdLoadInfo> GetAdLoadInfo()
        {
            List<MediationAdLoadInfo> infos = new List<MediationAdLoadInfo>();
            string json = UnionPlatform_FeedAdMediationGetAdLoadInfoList(ad);
            if (json == null)
            {
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

        public MediationAdEcpmInfo GetBestEcpm()
        {
            string json = UnionPlatform_FeedAdMediationGetCurrentBestEcpmInfo(ad);
            if (json == null)
            {
                return null;
            }
            // json反序列化
            MediationAdEcpmInfo info = JsonUtility.FromJson<MediationAdEcpmInfo>(json);
            return info;
        }

        public List<MediationAdEcpmInfo> GetMultiBiddingEcpm()
        {
            List<MediationAdEcpmInfo> infos = new List<MediationAdEcpmInfo>();
            string json = UnionPlatform_FeedAdMediationMultiBiddingEcpmInfos(ad);
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

        public MediationAdEcpmInfo GetShowEcpm()
        {
            string json = UnionPlatform_FeedAdMediationGetShowEcpmInfo(ad);
            if (json == null)
            {
                return null;
            }
            // json反序列化
            MediationAdEcpmInfo info = JsonUtility.FromJson<MediationAdEcpmInfo>(json);
            return info;
        }

        public List<MediationAdEcpmInfo> GetCacheList()
        {
            List<MediationAdEcpmInfo> infos = new List<MediationAdEcpmInfo>();
            string json = UnionPlatform_FeedAdMediationCacheRitList(ad);
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

        public bool HasDislike()
        {
            return false;
        }

        public void SetUseCustomVideo(bool isUseCustomVideo)
        {
            
        }

        public void SetShakeViewListener(MediationShakeViewListener listener)
        {

        }

        public bool IsExpress()
        {
            return false;
        }

        [DllImport("__Internal")]
        private static extern bool UnionPlatform_FeedAdMediationisReady(IntPtr ad);
        [DllImport("__Internal")]
        private static extern string UnionPlatform_FeedAdMediationGetShowEcpmInfo(IntPtr ad);
        [DllImport("__Internal")]
        private static extern string UnionPlatform_FeedAdMediationGetCurrentBestEcpmInfo(IntPtr ad);
        [DllImport("__Internal")]
        private static extern string UnionPlatform_FeedAdMediationMultiBiddingEcpmInfos(IntPtr ad);
        [DllImport("__Internal")]
        private static extern string UnionPlatform_FeedAdMediationCacheRitList(IntPtr ad);
        [DllImport("__Internal")]
        private static extern string UnionPlatform_FeedAdMediationGetAdLoadInfoList(IntPtr ad);
    }
}

#endif