using System;
using System.Collections;
using System.Collections.Generic;
using FourWordIdiom.LocalGame.GameScripts.RootContainer;
using Middleware;
using Unity.VisualScripting;
using UnityEngine;

namespace Middleware.Runtime.Ads
{
    public class Ads_honor : IAds
    {
        public bool IsPlaying { get; set; }
        public bool IsRewardVideoLoaded;
        public bool IsInterstitialLoaded;
        
        // 🔥 新增：防止重复加载的锁
        private bool _isRewardLoading = false;
        private bool _isIntLoading = false;
        // 🔥 新增：防止重复注册事件
        private bool _isInitialized = false;
        
        private AndroidJavaObject _currentActivity;
        
        private Action<bool> _currentIntCallback;
        private Action<bool> _currentRewardCallback;
        
        public void Init(float delay)
        {
            UnityTimer.Delay(delay, () =>
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    _currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    _currentActivity.Call("adsInit");

                    if (HonorManager.Instance != null && !_isInitialized)
                    {
                        _isInitialized = true;
                        Debug.Log("初始化监听器完成");
                        // ============ 插屏监听 ============
                        HonorManager.Instance.OnInterstitialReady += ()=>
                        {
                            IsInterstitialLoaded = true;
                            _isIntLoading = false;
                        };
                        HonorManager.Instance.OnInterstitialClose += () =>
                        {
                            IsInterstitialLoaded = false;
                            _isIntLoading = false;
                            LoadInterstitial();
                            
                            _currentIntCallback?.Invoke(true);
                            _currentIntCallback = null;
                        };
                        HonorManager.Instance.OnInterstitialLoadFail += (string msg) =>
                        {
                            IsInterstitialLoaded = false;
                            _isIntLoading = false;
                            _currentIntCallback?.Invoke(false);
                            _currentIntCallback = null;
                        };
                        
                        // ============ 激励视频监听 ============
                        HonorManager.Instance.OnRewardAdReady += () =>
                        {
                            IsRewardVideoLoaded = true;
                            _isRewardLoading = false;
                        };
                        HonorManager.Instance.OnUserEarnedReward += (string msg) =>
                        {
                            IsRewardVideoLoaded = false;
                            _isRewardLoading = false;
                            _currentRewardCallback?.Invoke(true);
                            _currentRewardCallback = null;
                        };
                        HonorManager.Instance.OnRewardAdLoadFail += (string msg) =>
                        {
                            IsRewardVideoLoaded = false;
                            _isRewardLoading = false;
                            _currentRewardCallback?.Invoke(false);
                            _currentRewardCallback = null;
                            Debug.Log("错误回调了" + msg);
                        };
                    }
                }
            });
        }

        public bool IsReady(Define.AdKey key)
        {
            if (!IsRewardVideoLoaded && !_isRewardLoading)
            {
                _isRewardLoading = true;
                _currentActivity.Call("loadRewardAd");
            }
            return IsRewardVideoLoaded;
        }

        public void ShowReward(Define.AdKey key, Action<bool> callback)
        {
            if (IsRewardVideoLoaded)
            {
                _currentRewardCallback = callback;
                IsRewardVideoLoaded = false;
                _currentActivity.Call("showRewardAd");
            }
            else
            {
                Debug.Log("再一次进入点击？" + _isRewardLoading);
                callback?.Invoke(false);
                // if(!_isRewardLoading) 
                    _currentActivity.Call("loadRewardAd");
            }
        }

        public void ShowInterstitial(Action<bool> callback)
        {
            if (IsInterstitialLoaded)
            {
                _currentIntCallback = callback;
                IsInterstitialLoaded = false;
                _currentActivity.Call("showInterstitialAd");
            }
            else
            {
                callback?.Invoke(false);
                LoadInterstitial();
            }
            
        }
        private void LoadInterstitial()
        {
            if (!_isIntLoading)
            {
                _isIntLoading = true;
                _currentActivity.Call("loadInterstitialAd");
            }
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
