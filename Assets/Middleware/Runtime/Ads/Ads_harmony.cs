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
        
        // 预加载相关字段（仅用于激励视频）
        private Dictionary<AdType, Advertisement> _preloadedAds = new Dictionary<AdType, Advertisement>();
        private bool _isNeedShow = false;      // 标记是否有待立即展示的广告
        private bool _isPreloading = false;    // 标记是否正在预加载
        private bool _isRewarded = false;
        private float _preloadInterval = 30f;  // 预加载间隔（秒）
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
                
                // 初始化后仅预加载激励视频
                PreloadRewardVideo();
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

        #region 预加载逻辑（仅激励视频）
        /// <summary>
        /// 外部调用的预加载入口（仅激励视频）
        /// </summary>
        public void PreloadAds()
        {
            PreloadRewardVideo();
        }
        
        /// <summary>
        /// 预加载激励视频广告
        /// </summary>
        private void PreloadRewardVideo()
        {
            if (_isPreloading) return;
            
            // 检查时间间隔
            if ((DateTime.Now - _lastPreloadTime).TotalSeconds < _preloadInterval)
                return;
            
            // 如果已有缓存则跳过
            if (_preloadedAds.ContainsKey(AdType.Reward) && _preloadedAds[AdType.Reward] != null)
                return;
            
            _isPreloading = true;
            _lastPreloadTime = DateTime.Now;
            _isNeedShow = false;  // 预加载不立即展示
            
            var adRequestParams = new AdRequestParams()
            {
                adType = (int)AdType.Reward,
                adId = "s1emwq0ad9",   // 激励视频广告ID
                oaid = _uniqueId,
                isPreload = true       // 标记为预加载
            };
            Debug.Log("[AD]开始预加载激励视频");
            OHSDKKitManager.Instance.LoadAds(adRequestParams, new AdOptions());
        }
        
        /// <summary>
        /// 获取预加载的广告（仅激励视频）
        /// </summary>
        private Advertisement GetPreloadedAd(AdType adType)
        {
            if (_preloadedAds.ContainsKey(adType) && _preloadedAds[adType] != null)
            {
                var ad = _preloadedAds[adType];
                _preloadedAds.Remove(adType);
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
        /// 强制重新预加载（例如失败后重试）
        /// </summary>
        public void ForcePreloadAds()
        {
            _lastPreloadTime = DateTime.MinValue;
            PreloadRewardVideo();
        }
        #endregion

        // ---------- 广告展示接口 ----------
        
        public void ShowReward(Define.AdKey key, Action<bool> callback)
        {
            CreateAdsObj();
            
            _currentAdKey = key;
            _completeCallback = callback;
            _adType = AdType.Reward;
            _isNeedShow = true;
            _isRewarded = false;
            
            // 优先使用预加载的激励视频
            var preloadedAd = GetPreloadedAd(AdType.Reward);
            if (preloadedAd != null)
            {
                Debug.Log("[AD]使用预加载的激励视频");
                DisplayAd(preloadedAd);
                // 展示后立即重新预加载
                UnityTimer.Delay(1f, () => PreloadRewardVideo());
                return;
            }
            
            // 无预加载，即时加载（非预加载模式）
            var adRequestParams = new AdRequestParams()
            {
                adType = (int)_adType,
                adId = "s1emwq0ad9",
                oaid = _uniqueId,
                isPreload = false      // 即时加载，不缓存
            };
            MessageSystem.Instance.ShowLoadingAnimation();
            OHSDKKitManager.Instance.LoadAds(adRequestParams, new AdOptions());
        }

        public void ShowInterstitial(Action<bool> callback)
        {
            CreateAdsObj();
            
            _completeCallback = callback;
            _adType = AdType.Interstitial;
            _isNeedShow = true;          // 标记需要立即展示
            _isRewarded = false;
            
            // 插屏广告不复用预加载，每次都即时加载并展示
            var adRequestParams = new AdRequestParams()
            {
                adType = (int)_adType,
                adId = "i0tgl4g0bw",     // 插屏广告ID
                oaid = _uniqueId,
                isPreload = false        // 关键：非预加载模式
            };
            MessageSystem.Instance.ShowLoadingAnimation();
            OHSDKKitManager.Instance.LoadAds(adRequestParams, new AdOptions());
        }
        
        public void LoadBannerAD()
        {
            // 保留，未实现
        }

        private AdRequestParams BanneradRequestParams;
        
        public void ShowBanner()
        {
            CreateAdsObj();
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
            OHSDKKitManager.Instance.LoadBanner(BanneradRequestParams, new AdOptions(), new AdDisplayOptions());
        }

        public void HideBanner()
        {
            // 保留
        }
        
        #region 通用回调处理
        
        private Action<bool> _completeCallback;
        private AdType _adType;
        
        private void DisplayAd(Advertisement ad)
        {
            if (!_isNeedShow) 
            {
                Debug.LogWarning("[AD]DisplayAd called but _isNeedShow is false, ignore.");
                return;
            }
            
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
                AnalyticMgr.VideoStart(desc);
            }
        }
        
        private void CallbackAd(bool success)
        {
            _completeCallback?.Invoke(success);
            _completeCallback = null;
            _isRewarded = false;
            _isNeedShow = false;    // 清除等待标记
            // 如果是激励视频成功，则重新预加载
            if (success && _adType == AdType.Reward)
            {
                UnityTimer.Delay(2f, () => PreloadRewardVideo());
            }
        }

        // ----- 信号回调 -----
        private void OnLoadAdsTrigger(SignalBase signal)
        {
            if (!signal.hasError())
            {
                var targetSignal = (AdsLoadSignal)signal;
                var ad = targetSignal.ads[0];
                if (ad != null)
                {
                    AdType adType = (AdType)ad.adType;
                    Debug.Log($"[OnLoadAdsTrigger]type：{adType}, uniqueId：{ad.uniqueId}");
                    
                    // 关键判断：如果有等待展示的请求且类型匹配，则立即展示
                    if (_isNeedShow && _adType == adType)
                    {
                        DisplayAd(ad);
                        _isNeedShow = false;   // 已展示，清除等待
                    }
                    else
                    {
                        // 否则当作预加载缓存（仅激励视频会进入这里）
                        _preloadedAds[adType] = ad;
                        _isPreloading = false;
                        Debug.Log($"[AD]预加载广告缓存: {adType}");
                    }
                    MessageSystem.Instance.HideLoadingAnimation();
                }
                else
                {
                    Debug.Log($"[OnLoadAdsTrigger]Ad null, Code:{signal.code} Msg:{signal.message}");
                    if (_isNeedShow)
                    {
                        CallbackAd(false);
                        _isNeedShow = false;
                    }
                    else
                    {
                        _isPreloading = false;
                    }
                    MessageSystem.Instance.HideLoadingAnimation();
                }
            }
            else
            {
                Debug.Log($"[OnLoadAdsTrigger]Load Error, Code:{signal.code} Msg:{signal.message}");
                if (_isNeedShow)
                {
                    CallbackAd(false);
                    _isNeedShow = false;
                }
                else
                {
                    _isPreloading = false;
                }
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
                // 插屏展示成功后立即回调成功（通常在OnShow时即认为成功）
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
                    _isRewarded = true;
                }
               
                if (targetSignal.AdStatus == "onAdClose" || targetSignal.AdStatus == "onAdFail")
                {
                    if (_adType == AdType.Reward)
                    {
                        CallbackAd(_isRewarded);
                    }
                    Game.self.ResumeGame();
                    
                    // 广告关闭或失败后尝试重新预加载（仅激励视频）
                    if (_adType == AdType.Reward && targetSignal.AdStatus == "onAdFail")
                    {
                        Debug.Log("[AD]激励视频展示失败，重新预加载");
                        UnityTimer.Delay(3f, () => ForcePreloadAds());
                    }
                }
                
                UnityTimer.Delay(0.5f, () => MessageSystem.Instance.HideLoadingAnimation());
            }
            else
            {
                // 信号错误，回调失败
                if (_isNeedShow)
                {
                    CallbackAd(false);
                    _isNeedShow = false;
                }
                Game.self.ResumeGame();
                if (_adType == AdType.Reward)
                {
                    UnityTimer.Delay(5f, () => ForcePreloadAds());
                }
            }
        }

        #endregion
    }
}
#endif