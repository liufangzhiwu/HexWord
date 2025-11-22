using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Middleware
{
    public interface IAds
    {
        bool IsPlaying { get; set; }
        void Init(float delay);
        bool IsReady(Define.AdKey key);
        void ShowReward(Define.AdKey key, Action<bool> callback);
        void ShowInterstitial(Action<bool> callback);
        
        void LoadBannerAD();
        
        void ShowBanner();
        void HideBanner();
    }
}