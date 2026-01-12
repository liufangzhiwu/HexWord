#if UNITY_huawei
using UnityEngine;
using System;
using HuaweiService;
using HuaweiService.ads;

namespace Middleware
{
    public class Ads_huawei : IAds
    {
        public bool IsPlaying { get; set; }
        private string _uniqueId;
        Define.AdKey _currentAdKey;
        
        private RewardAd _cachedRewardAd;         // 缓存的激励广告对象
        private DateTime _cacheTime;              // 缓存创建的时间
        private bool _isLoadingReward;            // 是否正在加载激励广告（防止重复请求）
        private bool _isShowingReward;            // 是否正在处理展示流程（防止快速点击）
        private bool _isUserWaiting;
        private const double CacheExpireHours = 1.0; // 缓存过期时间（1小时）
 
        public void Init(float delay)
        {
           
            UnityTimer.Delay(delay, () =>
            {
                _uniqueId = Game.self.GetUniqueId();
                
            });
        }

        public bool IsReady(Define.AdKey key)
        {
            return IsCacheValid();
        }

        public void ShowReward(Define.AdKey key, Action<bool> callback)
        {
            if (_isShowingReward)
            {
                Debug.Log("[Ads_huawei] ShowReward ignored: Action in progress.");
                if (_isLoadingReward && _isUserWaiting)
                {
                    MessageSystem.Instance.ShowTip("广告加载中, 请稍等");
                }
                return;
            }

            _isShowingReward = true;
            _isUserWaiting = true;
            _currentAdKey = key;
            _completeCallback = callback;

            if (IsCacheValid())
            {
                Debug.Log("[Ads_huawei] ShowReward: Cache Hit.");
                ShowCacheAd();
            }
            else
            {
                Debug.Log("[Ads_huawei] ShowReward: Cache Miss. Loading new ad.");
                // 缓存无效或不存在，发起加载并自动播放
                MessageSystem.Instance.ShowLoadingAnimation();
                LoadRewardAd(key, true);
            }
       
        }

        private void LoadRewardAd(Define.AdKey key, bool autoShow)
        {
            if (_isLoadingReward) return;
            if (!autoShow && IsCacheValid()) return;

            _isLoadingReward = true;
            _currentAdKey = key;
                 
            RewardAd ad = new RewardAd(new Context(), GetAdId(key));
            ad.setMobileDataAlertSwitch(false);
            AdParam adParam = new AdParam.Builder().build();
            
            MRewardLoadListener listener = new MRewardLoadListener(this,ad, autoShow);
            ad.loadAd(adParam, listener);
        }

        public void ShowInterstitial(Action<bool> callback)
        {
            _completeCallback = callback;
            InterstitialAd ad = new InterstitialAd(new Context());
            ad.setAdId(GetAdId(Define.AdKey.InterstitialAdId));
            ad.setAdListener(new MAdListener(ad, _completeCallback));
            ad.loadAd(new AdParam.Builder().build());
        }

        public void LoadBannerAD()
        {
            Debug.Log("华为的安卓没有实现横幅广告");
        }


        public void ShowBanner()
        {
            // 未实现的功能
            return;
        }

        public void HideBanner()
        {
            if(!_isBannerShow) return;
            _isBannerShow = false;
        }
        
        #region 通用逻辑
        private Action<bool> _completeCallback;
        private bool _isBannerShow;
        
        private string GetAdId(Define.AdKey key)
        {
            var adId = "";
#if Unity_Release
            return ConfigManager.Instance.GetString(key.ToString());
#else
            switch (key)
            {
                case Define.AdKey.InterstitialAdId:
                    adId = Define.ConfigHuaweiAndroid.TestInterstitialAdId;
                    break;
                default:
                    adId = Define.ConfigHuaweiAndroid.TestRewardAdId;
                    break;
            }
            return adId;
#endif
        }

        private bool IsCacheValid()
        {
            if (_cachedRewardAd == null) return false;
            if ((DateTime.Now - _cacheTime).TotalHours >= CacheExpireHours)
            {
                Debug.Log("[Ads_huawei] reward ad expired");
                _cachedRewardAd = null;
                return false;
            }
            return true;
        }

        private void ShowCacheAd()
        {
            if (_cachedRewardAd == null)
            {
                LoadRewardAd(_currentAdKey, true);
                return;
            }

            RewardAd ad = _cachedRewardAd;
            _cachedRewardAd = null;
            ad.show(new Context(), new MRewardAdStatusListener(this, _completeCallback));
        }
        // 加载成功回调
        private void OnRewardAdLoaded(RewardAd ad, bool autoShow)
        {
            _isLoadingReward = false;
            
            // 更新缓存
            _cachedRewardAd = ad;
            _cacheTime = DateTime.Now;

            if (autoShow || _isUserWaiting)
            {
                ShowCacheAd();
            }
            else
            {
                Debug.Log("[Ads_huawei] Preload success.");
            }
        }

        // 加载失败回调
        private void OnRewardAdFailedToLoad(int errorCode)
        {
            _isLoadingReward = false;

            // 如果是准备播放时失败，需要回调给业务层并重置展示状态
            if (_isUserWaiting)
            {
                
                _isShowingReward = false;
                _isUserWaiting = false;
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    _completeCallback?.Invoke(false);
                    _completeCallback = null;
                });
            }
        }

        // 广告打开回调（在此处预加载下一个视频）
        private void OnAdOpened()
        {
            _isUserWaiting = false;
            // 注意：此时 autoShow = false，仅进行预加载
            LoadRewardAd(_currentAdKey, false);
        }

        // 广告关闭或展示失败回调
        private void OnAdFinishedOrClosed()
        {
            _isShowingReward = false; // 重置展示状态，允许下一次点击
            _isUserWaiting = false;
        }
        #endregion
        private class MRewardLoadListener : RewardAdLoadListener
        {
            private Ads_huawei _parent;
            private RewardAd _ad;
            private bool _autoShow;

            public MRewardLoadListener(Ads_huawei parent, RewardAd ad, bool autoShow)
            {
                this._parent = parent;
                this._ad = ad;
                this._autoShow = autoShow;
            }

            public override void onRewardAdFailedToLoad(int errorCode)
            {
                Debug.Log($"[MRewardLoadListener]RewardAdFailedToLoad errorCode:{errorCode}");
                _parent.OnRewardAdFailedToLoad(errorCode);
            }
            
            public override void onRewardedLoaded()
            {
                _parent.OnRewardAdLoaded(_ad, _autoShow);
            }
        }
        
        private class MRewardAdStatusListener : RewardAdStatusListener
        {
            private Ads_huawei _parent;
            private Action<bool> _callback;

            public MRewardAdStatusListener(Ads_huawei parent, Action<bool> callback)
            {
                this._parent = parent;
                this._callback = callback;
            }
            public override void onRewardAdOpened()
            {
                // base.onRewardAdOpened();
                // MessageSystem.Instance.ShowTip($"[激励广告被打开]RewardAdOpened show");
                _parent.OnAdOpened();
            }
            public override void onRewardAdClosed()
            {
                _parent.OnAdFinishedOrClosed();
                //可以领取奖励关闭回调
                // base.onRewardAdClosed();
                // MessageSystem.Instance.ShowTip($"[激励广告被关闭]RewardAdClosed");
                // UnityMainThreadDispatcher.Instance().Enqueue(() =>
                // {
                //     _callback?.Invoke(false);
                // });
            }
            public override void onRewardAdFailedToShow(int arg0)
            {
                _parent.OnAdFinishedOrClosed();
                // base.onRewardAdFailedToShow(arg0);
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    _callback?.Invoke(false);
                });
                // MessageSystem.Instance.ShowTip($"[激励广告展示失败] RewardAdFailedToShow errorCode:{arg0}");
            }
            public override void onRewarded(Reward arg0)
            {
                // base.onRewarded(arg0);
                // MessageSystem.Instance.ShowTip($"[激励广告完成] RewardAdFailedToShow errorCode:{arg0}");
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    _callback?.Invoke(true);
                });
            }
        }
        
        private class MAdListener : AdListener
        {
            private readonly InterstitialAd _ad;
            private readonly Action<bool> _callback;
            public MAdListener(InterstitialAd ad, Action<bool> callback = null): base()
            {
                this._ad = ad;
                this._callback = callback;
            }

            public override void onAdClicked()
            {
                // base.onAdClicked();
                // MessageSystem.Instance.ShowTip("AdListener Ad Clicked");
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    MessageSystem.Instance.HideLoadingAnimation();
                });
            }

            public override void onAdClosed()
            {
                // base.onAdClosed();
                // MessageSystem.Instance.ShowTip("AdListener Ad Closed");
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    //_callback?.Invoke(false);
                    MessageSystem.Instance.HideLoadingAnimation();
                });
            }

            public override void onAdFailed(int arg0)
            {
                // MessageSystem.Instance.ShowTip("AdListener Ad failed to load with error code "+ arg0);
                // base.onAdFailed(arg0);
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    _callback?.Invoke(false);
                });
            }

            public override void onAdImpression()
            {
                // base.onAdImpression();
                // MessageSystem.Instance.ShowTip("AdListener onAdImpression");
            }

            public override void onAdLeave()
            {
                // base.onAdLeave();
                // MessageSystem.Instance.ShowTip("AdListener Ad Leave");
            }

            public override void onAdLoaded()
            {
                // base.onAdLoaded();
                 // MessageSystem.Instance.ShowTip("AdListener onAdLoaded");
                _ad.show(new Context());
            }

            public override void onAdOpened()
            {
                // base.onAdOpened();
                // MessageSystem.Instance.ShowTip("AdListener Ad Opened");
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    _callback?.Invoke(true);
                });
            }
        }
    }
}
#endif