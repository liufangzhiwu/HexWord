using System;
using System.Collections;
using System.Collections.Generic;
using FourWordIdiom.LocalGame.GameScripts.RootContainer;
using Middleware;
using UnityEngine;

namespace Middleware.Runtime.Ads
{
    public class Ads_honor : IAds
    {
        public bool IsPlaying { get; set; }
        public bool IsLoaded;
        
        private AndroidJavaObject _currentActivity;

        public void Init(float delay)
        {
            UnityTimer.Delay(delay, () =>
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    _currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    _currentActivity.Call("adsInit");
                }
            });
        }

        public bool IsReady(Define.AdKey key)
        {
            if (!IsLoaded)
            {
                _currentActivity.Call("loadRewardAd");
                HonorManager.Instance.OnRewardAdReady += () =>
                {
                    IsLoaded = true;
                };
            }
            return IsLoaded;
        }

        public void ShowReward(Define.AdKey key, Action<bool> callback)
        {
            _currentActivity.Call("showRewardAd");
            HonorManager.Instance.OnUserEarnedReward += (res) =>
            {
                callback?.Invoke(res);
            };
            IsLoaded = false;
        }

        public void ShowInterstitial(Action<bool> callback)
        {
            _currentActivity.Call("showInterstitialAd");
        }

        public void LoadBannerAD()
        {
            _currentActivity.Call("loadInterstitialAd");
        }
        public void ShowBanner()
        {
           _currentActivity.Call("showBannerAd");
        }
        public void HideBanner()
        {
           _currentActivity.Call("releaseBanner");
        }
    }
}
