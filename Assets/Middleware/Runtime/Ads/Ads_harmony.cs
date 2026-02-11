#if UNITY_OPENHARMONY
using UnityEngine;
using System;
using System.Collections.Generic;
using OpenHarmonyKits.Param;
using OpenHarmonyKits.Signal;
using Unity.VisualScripting;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Middleware
{
    public class Ads_harmony : IAds
    {
        public bool isLoadReady;
        public bool IsPlaying { get; set; }
        private string _uniqueId;
        Define.AdKey _currentAdKey;
        
        SignalHandler SignalHandlerObj;
        AdsStatusSignalHandle SignalReceiveObj;
        
        // 预加载相关字段
        private Dictionary<AdType, Advertisement> _preloadedAds = new Dictionary<AdType, Advertisement>();
        private bool _isNeedShow = false;
        private bool _isPreloading = false;
        private float _preloadInterval = 30f; // 预加载间隔时间（秒）
        private DateTime _lastPreloadTime = DateTime.MinValue;
        
        public void Init(float delay)
        {
            CreateAdsObj();
            
            UnityTimer.Delay(delay, () =>
            {
                SignalHandler.Instance.RegisterSignalDelegate<AdsLoadSignal>(OnLoadAdsTrigger);
                SignalHandler.Instance.RegisterSignalDelegate<AdsShowSignal>(OnShowAdsTrigger);
                SignalHandler.Instance.RegisterSignalDelegate<AdsStatusSignal>(OnAdsStatusTrigger);
                _uniqueId = Game.self.GetOAID();
                
                // 初始化后立即预加载广告
                PreloadAds();
            });
        }

        public bool IsReady(Define.AdKey key)
        {
            return isLoadReady;
        }
        
        public void DestoryAdsObj()
        {
            Object.Destroy(SignalHandlerObj);
            Object.Destroy(SignalReceiveObj);
        }
        
        public void CreateAdsObj()
        {
            if(SignalHandlerObj!=null) return;
            if(SignalReceiveObj!=null) return;
            
            SignalHandlerObj = new GameObject("SignalHandler").AddComponent<SignalHandler>();
            SignalReceiveObj = new GameObject("SignalReceive").AddComponent<AdsStatusSignalHandle>();
            Object.DontDestroyOnLoad(SignalHandlerObj);
            Object.DontDestroyOnLoad(SignalReceiveObj);
        }

        #region 预加载逻辑
        /// <summary>
        /// 预加载广告（激励视频和插屏广告）
        /// </summary>
        public void PreloadAds()
        {
            if (_isPreloading) return;
            
            // 检查是否需要重新预加载（基于时间间隔）
            if ((DateTime.Now - _lastPreloadTime).TotalSeconds < _preloadInterval)
            {
                // 还未到预加载间隔时间
                return;
            }

            _isNeedShow = false;
            _isPreloading = true;
            _lastPreloadTime = DateTime.Now;
            
            Debug.Log("[AD]开始预加载广告");
            
            // 预加载激励视频
            PreloadRewardVideo();
            
            // 预加载插屏广告
            //PreloadInterstitial();
        }
        
        /// <summary>
        /// 预加载激励视频广告
        /// </summary>
        private void PreloadRewardVideo()
        {
            if (_preloadedAds.ContainsKey(AdType.Reward) && _preloadedAds[AdType.Reward] != null)
            {
                Debug.Log("[AD]激励视频已预加载，跳过");
                return;
            }
            
            var adRequestParams = new AdRequestParams()
            {
                adType = (int)AdType.Reward,
                adId = "s1emwq0ad9", // 使用默认的激励视频广告ID
                oaid = _uniqueId,
                isPreload = true
            };
            var adOptions = new AdOptions();
            _isNeedShow = false;
            Debug.Log("[AD]预加载激励视频广告");
            OHSDKKitManager.Instance.LoadAds(adRequestParams, adOptions);
        }
        
        /// <summary>
        /// 预加载插屏广告
        /// </summary>
        private void PreloadInterstitial()
        {
            if (_preloadedAds.ContainsKey(AdType.Interstitial) && _preloadedAds[AdType.Interstitial] != null)
            {
                Debug.Log("[AD]插屏广告已预加载，跳过");
                return;
            }
            
            var adRequestParams = new AdRequestParams()
            {
                adType = (int)AdType.Interstitial,
                adId = "i0tgl4g0bw",
                oaid = _uniqueId,
                isPreload = true
            };
            var adOptions = new AdOptions();
            _isNeedShow = false;
            Debug.Log("[AD]预加载插屏广告");
            OHSDKKitManager.Instance.LoadAds(adRequestParams, adOptions);
        }
        
        /// <summary>
        /// 获取预加载的广告
        /// </summary>
        private Advertisement GetPreloadedAd(AdType adType)
        {
            if (_preloadedAds.ContainsKey(adType) && _preloadedAds[adType] != null)
            {
                var ad = _preloadedAds[adType];
                _preloadedAds.Remove(adType); // 使用后移除，需要重新预加载
                return ad;
            }
            return null;
        }
        
        /// <summary>
        /// 检查是否有预加载的广告可用
        /// </summary>
        public bool HasPreloadedAd(AdType adType)
        {
            return _preloadedAds.ContainsKey(adType) && _preloadedAds[adType] != null;
        }
        
        /// <summary>
        /// 手动触发重新预加载（例如在广告展示失败后）
        /// </summary>
        public void ForcePreloadAds()
        {
            _lastPreloadTime = DateTime.MinValue; // 重置时间，强制重新预加载
            PreloadAds();
        }
        #endregion

        public void ShowReward(Define.AdKey key, Action<bool> callback)
        {
            CreateAdsObj();
            
            _currentAdKey = key;
            _completeCallback = callback;
            _adType = AdType.Reward;
            _isNeedShow = true;
            
            // 检查是否有预加载的激励视频广告
            var preloadedAd = GetPreloadedAd(AdType.Reward);
            if (preloadedAd != null)
            {
                Debug.Log("[AD]使用预加载的激励视频广告");
                DisplayAd(preloadedAd);
                
                // 展示后立即重新预加载新的广告
                UnityTimer.Delay(1f, () => PreloadRewardVideo());
                return;
            }
            
            // 没有预加载的广告，正常加载
            var adRequestParams = new AdRequestParams()
            {
                adType = (int)_adType,
                adId = "s1emwq0ad9",
                oaid = _uniqueId,
                isPreload = true
            };
            var adOptions = new AdOptions();
            MessageSystem.Instance.ShowLoadingAnimation();
            OHSDKKitManager.Instance.LoadAds(adRequestParams, adOptions);
        }

        public void ShowInterstitial(Action<bool> callback)
        {
            CreateAdsObj();
            
            _completeCallback = callback;
            _adType = AdType.Interstitial;
            _isNeedShow = true;
            
            // 检查是否有预加载的插屏广告
            var preloadedAd = GetPreloadedAd(AdType.Interstitial);
            if (preloadedAd != null)
            {
                Debug.Log("[AD]使用预加载的插屏广告");
                DisplayAd(preloadedAd);
                
                // 展示后立即重新预加载新的广告
                UnityTimer.Delay(1f, () => PreloadInterstitial());
                return;
            }
            
            // 没有预加载的广告，正常加载
            var adRequestParams = new AdRequestParams()
            {
                adType = (int)_adType,
                adId = "i0tgl4g0bw",
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

            CreateAdsObj();
            
            // if(_isBannerShow) return;
            // _isBannerShow = true;
            _adType = AdType.Banner;

            BanneradRequestParams = new AdRequestParams()
            {
                adType = (int)_adType,
                adId = "n56633mwpp",
                oaid = _uniqueId,
                isPreload = true
            };
            
            BanneradRequestParams.adWidth = 360;
            BanneradRequestParams.adHeight = 57;
            _isNeedShow = true;
            var adOptions = new AdOptions();
            var adDisplayOptions = new AdDisplayOptions();
            
            OHSDKKitManager.Instance.LoadBanner(BanneradRequestParams, adOptions, adDisplayOptions);
        }

        public void HideBanner()
        {
            // if(!_isBannerShow) return;
            // DestoryAdsObj();
        }
        
        #region 通用逻辑
        private Action<bool> _completeCallback;
        private AdType _adType;
        
//         private string GetAdId(Define.AdKey key)
//         {
//             var adId = "";
// #if Unity_Release
//             return ConfigManager.Instance.GetString(key.ToString());
// #else
//             switch (key)
//             {
//                 case Define.AdKey.BannerAdUnitId:
//                     adId = Define.ConfigHarmony.TestBannerAdId;
//                     break;
//                 case Define.AdKey.InterstitialAdId:
//                     adId = Define.ConfigHarmony.TestInterstitialAdId;
//                     break;
//                 default:
//                     adId = Define.ConfigHarmony.TestRewardAdId;
//                     break;
//             }
//             return adId;
// #endif
//         }
        
        private void DisplayAd(Advertisement ad)
        {
            if(!_isNeedShow) return;
          
            Debug.Log("[AD]展示广告: " + (AdType)ad.adType);
            var adDisplayOptions = new AdDisplayOptions();
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
                //MessageSystem.Instance.HideLoadingAnimation();
                AnalyticMgr.VideoStart(desc);
            }
        }
        
        private void CallbackAd(bool success)
        {
            _completeCallback?.Invoke(success);
            _completeCallback = null;
            
            // 广告展示完成后，触发重新预加载
            if (success)
            {
                UnityTimer.Delay(2f, () => PreloadAds());
            }
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
                    
                    // 判断是否为预加载的广告
                    bool isPreloadAd = _isPreloading || !HasPreloadedAd((AdType)ad.adType);
                    
                    if (isPreloadAd && _isPreloading)
                    {
                        // 预加载完成，存储广告
                        _preloadedAds[(AdType)ad.adType] = ad;
                        _isPreloading = false;
                        Debug.Log($"[AD]预加载广告完成: {(AdType)ad.adType}");
                    }
                    else
                    {
                        // 立即展示的广告
                        DisplayAd(ad);
                    }
                }
                else
                {
                    Debug.Log($"[OnLoadAdsTrigger]targetSignal Ad null, Code :{signal.code} Message : {signal.message}");
                    CallbackAd(false);
                    MessageSystem.Instance.HideLoadingAnimation();
                }
            }
            else
            {
                Debug.Log($"[OnLoadAdsTrigger]LoadAds Error, Code :{signal.code} Message : {signal.message}");
                _isPreloading = false; // 预加载失败，重置状态
                CallbackAd(false);
                MessageSystem.Instance.HideLoadingAnimation();
            }
        }

        private void OnShowAdsTrigger(SignalBase signal)
        {
            if (!signal.hasError())
            {
                var targetSignal = (AdsShowSignal)signal;
                Debug.Log($"[OnShowAdsTrigger] type:{(AdType)targetSignal.adType},uniqueId：{targetSignal.uniqueId}");
            }

            Game.self.PauseGame();
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
                    Game.self.ResumeGame();
                    
                    // 广告关闭或失败后，尝试重新预加载
                    if (targetSignal.AdStatus == "onAdFail")
                    {
                        Debug.Log("[AD]广告展示失败，重新预加载");
                        UnityTimer.Delay(3f, () => ForcePreloadAds());
                    }
                }
                
                UnityTimer.Delay(3f, () =>  MessageSystem.Instance.HideLoadingAnimation());
            }
            else
            {
                CallbackAd(false);
                Game.self.ResumeGame();
                // 发生错误时重新预加载
                UnityTimer.Delay(5f, () => ForcePreloadAds());
            }
        }

        #endregion

    }
}
#endif