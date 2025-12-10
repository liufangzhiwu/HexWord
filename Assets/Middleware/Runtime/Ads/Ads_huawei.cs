#if UNITY_ANDROID
using UnityEngine;
using System;
using HuaweiService;
using HuaweiService.ads;
using Object = UnityEngine.Object;

namespace Middleware
{
    public class Ads_huawei : IAds
    {
        public bool IsPlaying { get; set; }
        private string _uniqueId;
        Define.AdKey _currentAdKey;
        
        public void Init(float delay)
        {
           
            UnityTimer.Delay(delay, () =>
            {
                _uniqueId = Game.GetUniqueId();
            });
        }

        public bool IsReady(Define.AdKey key)
        {
            return true;
        }

        public void ShowReward(Define.AdKey key, Action<bool> callback)
        {
#if UNITY_EDITOR 
            callback(true);
            return;
#endif
            _currentAdKey = key;
            _completeCallback = callback;
            
            RewardAd ad = new RewardAd(new Context(), GetAdId(key));
            ad.setMobileDataAlertSwitch(false);
            AdParam adParam = new AdParam.Builder().build();
            MRewardLoadListener listener = new MRewardLoadListener(ad, CallbackAd);
            ad.loadAd(adParam, listener);
        }

        public void ShowInterstitial(Action<bool> callback)
        {
#if UNITY_EDITOR 
            callback(true);
            return;
#endif
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
                    adId = Define.ConfigAndroid.HUAWEIInterstitialAdId;
                    break;
                default:
                    adId = Define.ConfigAndroid.HUAWEIRewardAdId;
                    break;
            }
            return adId;
#endif
        }

        private void CallbackAd(bool success)
        {
            _completeCallback?.Invoke(success);
            _completeCallback = null;
        }
        #endregion
        private class MRewardLoadListener : RewardAdLoadListener
        {
            private RewardAd _ad;
            private Action<bool> _callback;

            public MRewardLoadListener(RewardAd ad, Action<bool> callback)
            {
                this._ad = ad;
                this._callback = callback;
            }

            public override void onRewardAdFailedToLoad(int errorCode)
            {
                _callback?.Invoke(false);
                Debug.Log($"[MRewardLoadListener]RewardAdFailedToLoad errorCode:{errorCode}");
            }
            
            public override void onRewardedLoaded()
            {
                _ad.show(new Context(), new MRewardAdStatusListener(_callback));
            }
        }
        
        private class MRewardAdStatusListener : RewardAdStatusListener
        {
            private Action<bool> _callback;

            public MRewardAdStatusListener(Action<bool> callback)
            {
                this._callback = callback;
            }
            public override void onRewardAdOpened()
            {
                // base.onRewardAdOpened();
                MessageSystem.Instance.ShowTip($"[激励广告被打开]RewardAdOpened show");
            }
            public override void onRewardAdClosed()
            {
                // base.onRewardAdClosed();
                MessageSystem.Instance.ShowTip($"[激励广告被关闭]RewardAdClosed");
                _callback?.Invoke(false);
            }
            public override void onRewardAdFailedToShow(int arg0)
            {
                // base.onRewardAdFailedToShow(arg0);
                _callback?.Invoke(false);
                MessageSystem.Instance.ShowTip($"[激励广告展示失败] RewardAdFailedToShow errorCode:{arg0}");
            }
            public override void onRewarded(Reward arg0)
            {
                // base.onRewarded(arg0);
                MessageSystem.Instance.ShowTip($"[激励广告完成] RewardAdFailedToShow errorCode:{arg0}");
                _callback?.Invoke(true);
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
                MessageSystem.Instance.ShowTip("AdListener Ad Clicked");
            }

            public override void onAdClosed()
            {
                // base.onAdClosed();
                MessageSystem.Instance.ShowTip("AdListener Ad Closed");
            }

            public override void onAdFailed(int arg0)
            {
                MessageSystem.Instance.ShowTip("AdListener Ad failed to load with error code "+ arg0);
                // base.onAdFailed(arg0);
                _callback?.Invoke(false);
                
            }

            public override void onAdImpression()
            {
                // base.onAdImpression();
                MessageSystem.Instance.ShowTip("AdListener onAdImpression");
            }

            public override void onAdLeave()
            {
                // base.onAdLeave();
                MessageSystem.Instance.ShowTip("AdListener Ad Leave");
            }

            public override void onAdLoaded()
            {
                // base.onAdLoaded();
                 MessageSystem.Instance.ShowTip("AdListener onAdLoaded");
                _ad.show(new Context());
            }

            public override void onAdOpened()
            {
                // base.onAdOpened();
                MessageSystem.Instance.ShowTip("AdListener Ad Opened");
                _callback?.Invoke(true);
            }
        }
    }
}
#endif