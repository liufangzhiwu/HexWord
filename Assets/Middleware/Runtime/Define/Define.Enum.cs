using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Middleware
{
    public partial class Define 
    {
        public enum AdKey
        {
            BannerAdUnitId,
            InterstitialAdId,
            RewardAdIdStoreGold,
            RewardAdIdItemGold,
            RewardAdIdCheckinGold1,
            RewardAdIdCheckinGold2,
            RewardAdIdCheckinGold3,
        }
        
        [Flags]
        public enum DataTarget
        {
            None =  0,
            Think = 1 << 0,
            //Firebase = 1 << 1,
            //All = Think | Firebase
        }
        
         
        /// <summary>
        /// 广告类型枚举
        /// 注意：这里的具体数字（1, 2, 3）需要和你们接入的鸿蒙 SDK 要求的传参对应。
        /// 一般来说 1 是激励，2 是插屏，3 是 Banner。
        /// </summary>
        public enum AdType
        {
            Reward = 1,       // 激励视频
            Interstitial = 2, // 插屏广告
            Banner = 3,       // 横幅广告
            Native = 4        // 原生广告 (备用)
        }
    }
}

