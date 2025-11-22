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
            Firebase = 1 << 1,
            All = Think | Firebase
        }
    }
}

