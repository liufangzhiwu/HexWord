#if UNITY_OPENHARMONY
using UnityEngine;
using System;
using OpenHarmonyKits.Param;
using OpenHarmonyKits.Signal;
using Unity.VisualScripting;
using Object = UnityEngine.Object;

namespace Middleware
{
    public class Ads_harmony : IAds
    {
        public bool IsPlaying { get; set; }
        private string _uniqueId;
        Define.AdKey _currentAdKey;
        
        public void Init(float delay)
        {
            var go = new GameObject("SignalHandler").AddComponent<SignalHandler>();
            var go2 = new GameObject("SignalReceive").AddComponent<AdsStatusSignalHandle>();
            Object.DontDestroyOnLoad(go);
            Object.DontDestroyOnLoad(go2);
            
            UnityTimer.Delay(delay, () =>
            {
                SignalHandler.Instance.RegisterSignalDelegate<AdsLoadSignal>(OnLoadAdsTrigger);
                SignalHandler.Instance.RegisterSignalDelegate<AdsShowSignal>(OnShowAdsTrigger);
                SignalHandler.Instance.RegisterSignalDelegate<AdsStatusSignal>(OnAdsStatusTrigger);
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
            _adType = AdType.Reward;

            var adRequestParams = new AdRequestParams()
            {
                adType = (int)_adType,
                adId = GetAdId(key),
                oaid = _uniqueId,
                isPreload = true
            };
            var adOptions = new AdOptions();
            OHSDKKitManager.Instance.LoadAds(adRequestParams, adOptions);
        }

        public void ShowInterstitial(Action<bool> callback)
        {
            _completeCallback = callback;
            _adType = AdType.Interstitial;

            var adRequestParams = new AdRequestParams()
            {
                adType = (int)_adType,
                adId = GetAdId(Define.AdKey.InterstitialAdId),
                oaid = _uniqueId,
                isPreload = true
            };
            var adOptions = new AdOptions();
            OHSDKKitManager.Instance.LoadAds(adRequestParams, adOptions);
        }
        
        public void LoadBannerAD()
        {
           
        }

        private AdRequestParams BanneradRequestParams;
        
        public void ShowBanner()
        {
            if(_isBannerShow) return;
            _isBannerShow = true;
            _adType = AdType.Banner;

            BanneradRequestParams = new AdRequestParams()
            {
                adType = (int)_adType,
                adId = GetAdId(Define.AdKey.BannerAdUnitId),
                oaid = _uniqueId,
                isPreload = true
            };
            
            BanneradRequestParams.adWidth = 360;
            BanneradRequestParams.adHeight = 57;
            
            var adOptions = new AdOptions();
            var adDisplayOptions = new AdDisplayOptions();
            
            OHSDKKitManager.Instance.LoadBanner(BanneradRequestParams, adOptions, adDisplayOptions);
        }

        public void HideBanner()
        {
            if(!_isBannerShow) return;
            _isBannerShow = false;
        }
        
        #region 通用逻辑
        private Action<bool> _completeCallback;
        private AdType _adType;
        private bool _isBannerShow;
        
        private string GetAdId(Define.AdKey key)
        {
            var adId = "";
#if Unity_Release
            return ConfigManager.Instance.GetString(key.ToString());
#else
            switch (key)
            {
                case Define.AdKey.BannerAdUnitId:
                    adId = Define.ConfigHarmony.TestBannerAdId;
                    break;
                case Define.AdKey.InterstitialAdId:
                    adId = Define.ConfigHarmony.TestInterstitialAdId;
                    break;
                default:
                    adId = Define.ConfigHarmony.TestRewardAdId;
                    break;
            }
            return adId;
#endif
        }
        
        private void DisplayAd(Advertisement ad)
        {
            Debug.Log("[AD]展示广告: " + (AdType)ad.adType);
            var adDisplayOptions = new AdDisplayOptions()
            {
                refreshTime = 30000
            };
            ad.isFullScreen = true;
            OHSDKKitManager.Instance.ShowAds(ad, adDisplayOptions);

            if ((AdType)ad.adType == AdType.Reward)
            {
                string desc = "";
                switch (_currentAdKey)
                {
                    case Define.AdKey.RewardAdIdStoreGold:
                        desc = "奖励广告-商店金币";
                        break;
                    case Define.AdKey.RewardAdIdItemGold:
                        desc = "奖励广告-物品金币";
                        break;
                    case Define.AdKey.RewardAdIdCheckinGold1:
                        desc = "奖励广告-签到金币1";
                        break;
                    case Define.AdKey.RewardAdIdCheckinGold2:
                        desc = "奖励广告-签到金币2";
                        break;
                    case Define.AdKey.RewardAdIdCheckinGold3:
                        desc = "奖励广告-签到金币3";
                        break;
                }
                
                AnalyticMgr.VideoStart(desc);
            }
        }
        
        private void CallbackAd(bool success)
        {
            _completeCallback?.Invoke(success);
            _completeCallback = null;
        }

        private void OnLoadAdsTrigger(SignalBase signal)
        {
            if (!signal.hasError())
            {
                var targetSignal = (AdsLoadSignal)signal;
                var ad = targetSignal.ads[0];
                if (ad != null)
                {
                    Debug.Log($"[OnLoadAdsTrigger]type：{(AdType)ad.adType},uniqueId：{ad.uniqueId},rewarded：{ad.rewarded},clicked：{ad.clicked}");
                    DisplayAd(ad);
                }
                else
                {
                    Debug.Log($"[OnLoadAdsTrigger]targetSignal Ad null, Code :{signal.code} Message : {signal.message}");
                    CallbackAd(false);
                }
            }
            else
            {
                Debug.Log($"[OnLoadAdsTrigger]LoadAds Error, Code :{signal.code} Message : {signal.message}");
                CallbackAd(false);
            }
        }

        private void OnShowAdsTrigger(SignalBase signal)
        {
            if (!signal.hasError())
            {
                var targetSignal = (AdsShowSignal)signal;
                Debug.Log($"[OnShowAdsTrigger] type:{(AdType)targetSignal.adType},uniqueId：{targetSignal.uniqueId}");
                Game.PauseGame();
            }

            if (_adType == AdType.Interstitial)
            {
                CallbackAd(true);
            }
        }

        private void OnAdsStatusTrigger(SignalBase signal)
        {
            if (!signal.hasError())
            {
                var targetSignal = (AdsStatusSignal)signal;
                Debug.Log($"[OnAdsStatusTrigger] type:{(AdType)targetSignal.AdType} status:{targetSignal.AdStatus}");

                if (targetSignal.AdStatus == "onAdReward" ||
                    targetSignal.AdStatus == "onVideoPlayEnd" && _adType == AdType.Reward)
                {
                    CallbackAd(true);
                }
               
                if (targetSignal.AdStatus == "onAdClose" || targetSignal.AdStatus == "onAdFail")
                {
                    Game.ResumeGame();
                }
            }
            else
            {
                CallbackAd(false);
                Game.ResumeGame();
            }
        }

        #endregion

    }
}
#endif