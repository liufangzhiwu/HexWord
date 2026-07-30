#if UNITY_OPENHARMONY
using UnityEngine;
using System;
using System.Collections.Generic;
using OpenHarmonyKits.Param;
using OpenHarmonyKits.Signal;
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
        private float _preloadInterval = 90f; // 预加载间隔时间（秒）
        private DateTime _lastPreloadTime = DateTime.MinValue;
        private DateTime _lastShowRewardAdsTime = DateTime.MinValue;
        private bool _isGetRewarded = false;
        public void Init(float delay)
        {
            CreateAdsObj();
            
            UnityTimer.Delay(delay, () =>
            {
                SignalHandler.Instance.RegisterSignalDelegate<AdsLoadSignal>(OnLoadAdsTrigger);
                SignalHandler.Instance.RegisterSignalDelegate<AdsShowSignal>(OnShowAdsTrigger);
                SignalHandler.Instance.RegisterSignalDelegate<AdsStatusSignal>(OnAdsStatusTrigger);
                _uniqueId = Game.self.GetUniqueId();
                
                // 初始化后立即预加载广告
                //PreloadAds();
            });
        }

        public bool IsReady(Define.AdKey key)
        {
            return true;
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
            /*if (_isPreloading) return;
            
            /#1#/ 检查是否需要重新预加载（基于时间间隔）
            if ((DateTime.Now - _lastPreloadTime).TotalSeconds < _preloadInterval)
            {
                // 还未到预加载间隔时间
                return;
            }#1#

            _isNeedShow = false;
            _isPreloading = true;
            _lastPreloadTime = DateTime.Now;
            
            Debug.Log("[AD]开始预加载广告");*/
            
            // 预加载激励视频
            //PreloadRewardVideo();
            
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
                adId = "d7m8galprp", // 使用默认的激励视频广告ID
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
            /*if (IsPlaying)
            {
                Debug.LogWarning("[AD] 已有广告在播放，本次激励视频请求被拒绝");
                callback?.Invoke(false);
                return;
            }*/
            CreateAdsObj();
            
            _currentAdKey = key;
            _completeCallback = callback;
            _adType = AdType.Reward;
            _isNeedShow = true;
            _isGetRewarded = false;
            IsPlaying = true;
            
            /*// 检查是否有预加载的激励视频广告
            var preloadedAd = GetPreloadedAd(AdType.Reward);
            if (preloadedAd != null)
            {
                Debug.Log("[AD]使用预加载的激励视频广告");
                DisplayAd(preloadedAd);
                
                // 展示后立即重新预加载新的广告
                //UnityTimer.Delay(1f, () => PreloadRewardVideo());
                return;
            }*/
            
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
            _lastShowRewardAdsTime = DateTime.Now;
        }

        public void ShowInterstitial(Action<bool> callback)
        {
            // if (IsPlaying)
            // {
            //     Debug.LogWarning("[AD] 已有广告在播放，本次插屏广告请求被拒绝");
            //     callback?.Invoke(false);
            //     return;
            // }
            //
            CreateAdsObj();
            
            // 检查广告间隔是否超过90s,未超过则返回
            // if ((DateTime.Now - _lastShowRewardAdsTime).TotalSeconds < _preloadInterval)
            // {
            //     callback?.Invoke(false); // 加上回调 false，防止业务层挂死等待
            //     // 还未到预加载间隔时间
            //     return;
            // }
            
            _completeCallback = callback;
            _adType = AdType.Interstitial;
            _isNeedShow = true;
            // 🔥 同样重置标记！
            _isGetRewarded = false;
            IsPlaying = true;
            
            // 检查是否有预加载的插屏广告
            // var preloadedAd = GetPreloadedAd(AdType.Interstitial);
            // if (preloadedAd != null)
            // {
            //     Debug.Log("[AD]使用预加载的插屏广告");
            //     DisplayAd(preloadedAd);
            //     
            //     // 展示后立即重新预加载新的广告
            //     UnityTimer.Delay(1f, () => PreloadInterstitial());
            //     return;
            // }
            
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

            // CreateAdsObj();
            //
            // // if(_isBannerShow) return;
            // // _isBannerShow = true;
            // _adType = AdType.Banner;
            //
            // BanneradRequestParams = new AdRequestParams()
            // {
            //     adType = (int)_adType,
            //     adId = "a3czsxbndo",
            //     oaid = _uniqueId,
            //     isPreload = true
            // };
            //
            // BanneradRequestParams.adWidth = 360;
            // BanneradRequestParams.adHeight = 57;
            // _isNeedShow = true;
            // var adOptions = new AdOptions();
            // var adDisplayOptions = new AdDisplayOptions();
            //
            // OHSDKKitManager.Instance.LoadBanner(BanneradRequestParams, adOptions, adDisplayOptions);
        }

        public void HideBanner()
        {
            // if(!_isBannerShow) return;
            // DestoryAdsObj();
        }
        
        #region 通用逻辑
        private Action<bool> _completeCallback;
        private AdType _adType;
        
        private string GetAdId(Define.AdKey key)
        {
            var adId = "";
//#if Unity_Release
            return ConfigManager.Instance.GetString(key.ToString());
//#else
            // switch (key)
            // {
            //     case Define.AdKey.BannerAdUnitId:
            //         adId = Define.ConfigHarmony.TestBannerAdId;
            //         break;
            //     case Define.AdKey.InterstitialAdId:
            //         adId = Define.ConfigHarmony.TestInterstitialAdId;
            //         break;
            //     default:
            //         adId = Define.ConfigHarmony.TestRewardAdId;
            //         break;
            // }
            // return adId;
//#endif
        }
        
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
                AnalyticMgr.VideoStart(desc);
            }
            
            if ((AdType)ad.adType == AdType.Interstitial)
            {
                AnalyticMgr.InsetAdStart("关卡插屏");
            }
        }
        
        private void CallbackAd(bool success)
        {
            IsPlaying = false;
            
            if (_completeCallback != null)
            {
                var tempCallback = _completeCallback;
                _completeCallback = null; // 立即置空，防止多次回调
                
                try 
                {
                    tempCallback.Invoke(success);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AD] 业务逻辑回调报错: {e.Message}");
                }
            }
            // 广告展示完成后，触发重新预加载
            if (success)
            {
                //UnityTimer.Delay(2f, () => PreloadAds());
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
                    
                    /*// 判断是否为预加载的广告
                    bool isPreloadAd = _isPreloading || !HasPreloadedAd((AdType)ad.adType);
                    
                    if (isPreloadAd && _isPreloading)
                    {
                        // 预加载完成，存储广告
                        _preloadedAds[(AdType)ad.adType] = ad;
                        _isPreloading = false;
                        Debug.Log($"[AD]预加载广告完成: {(AdType)ad.adType}");
                        MessageSystem.Instance.HideLoadingAnimation();
                    }
                    else
                    {*/
                        // 立即展示的广告
                        // if(!_isPreloading)
                            DisplayAd(ad);
                    //}
                }
                else
                {
                    Debug.Log($"[OnLoadAdsTrigger]targetSignal Ad null, Code :{signal.code} Message : {signal.message}");
                    if(_adType != AdType.Banner)
                        CallbackAd(false);
                    MessageSystem.Instance.HideLoadingAnimation();
                }
            }
            else
            {
                Debug.Log($"[OnLoadAdsTrigger]LoadAds Error, Code :{signal.code} Message : {signal.message}");
               
                if( _adType != AdType.Banner)
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
           
            // 🔥 别在这里立刻发奖了，等玩家关闭插屏时再发，统一生命周期！
            // if (_adType == AdType.Interstitial)
            // {
            //     CallbackAd(true);
            // }
        }

        private void OnAdsStatusTrigger(SignalBase signal)
        {
            try
            {
                var targetSignal = signal as AdsStatusSignal;
                if (targetSignal == null) return;
                string currentStatus = targetSignal.AdStatus ?? "";
                string statusLower = currentStatus.ToLower();
                Debug.LogError($"[AD] 收到鸿蒙底层状态: status={currentStatus}");
                
                if (!signal.hasError())
                {
                    // 1. 无条件解除锁定和UI，不管是什么广告
                    if (statusLower.Contains("close") || statusLower.Contains("fail"))
                    {
                        Game.self.ResumeGame(); 
                        IsPlaying = false;
                        if (MessageSystem.Instance != null) MessageSystem.Instance.HideLoadingAnimation();
                    }
                    
                    // 2. 视频达标，打上发奖标记
                    if (statusLower.Contains("reward") || statusLower.Contains("videoplayend"))
                    {
                        if (_adType == AdType.Reward)
                        {
                            _isGetRewarded = true;
                            Debug.Log("[AD] 激励视频已达标，标记为可发奖！");
                        }
                    }
                    // 3. 关闭广告结算发奖
                    if (statusLower.Contains("close") || statusLower.Contains("fail"))
                    {
                        if (_adType == AdType.Reward)
                        {
                            // 看全局标记发奖
                            CallbackAd(_isGetRewarded);
                        }
                        else if (_adType == AdType.Interstitial)
                        {
                            // 插屏广告只要没 fail，关闭时就发奖
                            CallbackAd(!statusLower.Contains("fail"));
                        }
                        
                        // 广告关闭或失败后，尝试重新预加载
                        // if (statusLower.Contains("fail"))
                        // {
                        //     Debug.Log("[AD] 广告展示失败，重新预加载");
                        //     UnityTimer.Delay(3f, () => ForcePreloadAds());
                        // }
                    }
                }
                else
                {
                    // Error 分支，必须释放锁定状态！
                    Debug.LogError("[AD] 鸿蒙发送信号附带 Error！");
                    Game.self.ResumeGame();
                    IsPlaying = false;
                    if (MessageSystem.Instance != null) MessageSystem.Instance.HideLoadingAnimation();
                    if (_adType != AdType.Banner)
                    {
                        CallbackAd(false);
                    }
            
                    // 发生错误时重新预加载
                    // UnityTimer.Delay(5f, () => ForcePreloadAds());
                }
            }
            catch (Exception ex)
            {
                // 保命大绝招，万一解析崩了，也要让游戏跑下去
                Debug.LogError($"[AD] OnAdsStatusTrigger 崩溃啦: {ex.Message}");
                Game.self.ResumeGame();
                IsPlaying = false;
                if (MessageSystem.Instance != null) MessageSystem.Instance.HideLoadingAnimation();
            }
        }

        #endregion

    }
}
#endif