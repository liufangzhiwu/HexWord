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
        private float _preloadInterval = 5f; // 预加载间隔时间（秒）
        private DateTime _lastPreloadTime = DateTime.MinValue;
        private DateTime _lastShowRewardAdsTime = DateTime.MinValue;
        private bool _isGetRewarded = false;
        private bool _isPreloadingReward = false; // 激励视频预加载中标记

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
                PreloadAds();
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
            if (SignalHandlerObj != null) return;
            if (SignalReceiveObj != null) return;

            SignalHandlerObj = new GameObject("SignalHandler").AddComponent<SignalHandler>();
            SignalReceiveObj = new GameObject("SignalReceive").AddComponent<AdsStatusSignalHandle>();
            Object.DontDestroyOnLoad(SignalHandlerObj);
            Object.DontDestroyOnLoad(SignalReceiveObj);
        }

        #region 预加载逻辑

        /// <summary>
        /// 预加载广告（目前恢复激励视频预加载）
        /// </summary>
        public void PreloadAds()
        {
            if (_isPreloadingReward) return;

            // 如果已经有预加载好的激励视频，并且还没到预加载间隔，就跳过
            if (HasPreloadedAd(AdType.Reward) &&
                (DateTime.Now - _lastPreloadTime).TotalSeconds < _preloadInterval)
            {
                return;
            }

            _lastPreloadTime = DateTime.Now;
            Debug.Log("[AD]开始预加载激励视频广告");
            PreloadRewardVideo();
        }

        /// <summary>
        /// 预加载激励视频广告
        /// </summary>
        private void PreloadRewardVideo()
        {
            if (_isPreloadingReward) return;

            if (HasPreloadedAd(AdType.Reward))
            {
                Debug.Log("[AD]激励视频已预加载，跳过");
                return;
            }

            _isPreloadingReward = true;
            _lastPreloadTime = DateTime.Now;

            var adRequestParams = new AdRequestParams()
            {
                adType = (int)AdType.Reward,
                adId = "s1emwq0ad9", // 与 ShowReward 中保持一致
                oaid = _uniqueId,
                isPreload = true
            };

            var adOptions = new AdOptions();

            Debug.Log("[AD]预加载激励视频广告");
            OHSDKKitManager.Instance.LoadAds(adRequestParams, adOptions);
        }

        /// <summary>
        /// 预加载插屏广告（保留原方法，暂未启用）
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
        
        public void ShowTipToolReward(Define.AdKey key, Action<bool> callback)
        {
            CreateAdsObj();

            _currentAdKey = key;
            _completeCallback = callback;
            _adType = AdType.Reward;
            _isGetRewarded = false;
            IsPlaying = true;
            _lastShowRewardAdsTime = DateTime.Now;

            // 1. 已经有预加载好的激励视频，直接展示
            var preloadedAd = GetPreloadedAd(AdType.Reward);
            if (preloadedAd != null)
            {
                _isNeedShow = true;
                Debug.Log("[AD]使用预加载的激励视频广告");
                DisplayAd(preloadedAd);

                // 展示后重新预加载
                UnityTimer.Delay(1f, () => PreloadRewardVideo());
                return;
            }

            // 2. 预加载正在进行，等待预加载完成后直接展示
            if (_isPreloadingReward)
            {
                _isNeedShow = true;
                MessageSystem.Instance.ShowLoadingAnimation();
                Debug.Log("[AD]激励视频预加载中，等待完成后直接展示");
                return;
            }

            // 3. 没有预加载，正常加载并展示
            _isNeedShow = true;

            var adRequestParams = new AdRequestParams()
            {
                adType = (int)_adType,
                adId = "e5dojfoi6c",
                oaid = _uniqueId,
                isPreload = true
            };

            var adOptions = new AdOptions();
            MessageSystem.Instance.ShowLoadingAnimation();
            OHSDKKitManager.Instance.LoadAds(adRequestParams, adOptions);
        }
        

        public void ShowReward(Define.AdKey key, Action<bool> callback)
        {
            CreateAdsObj();

            _currentAdKey = key;
            _completeCallback = callback;
            _adType = AdType.Reward;
            _isGetRewarded = false;
            IsPlaying = true;
            _lastShowRewardAdsTime = DateTime.Now;

            // 1. 已经有预加载好的激励视频，直接展示
            var preloadedAd = GetPreloadedAd(AdType.Reward);
            if (preloadedAd != null)
            {
                _isNeedShow = true;
                Debug.Log("[AD]使用预加载的激励视频广告");
                DisplayAd(preloadedAd);

                // 展示后重新预加载
                UnityTimer.Delay(1f, () => PreloadRewardVideo());
                return;
            }

            // 2. 预加载正在进行，等待预加载完成后直接展示
            if (_isPreloadingReward)
            {
                _isNeedShow = true;
                MessageSystem.Instance.ShowLoadingAnimation();
                Debug.Log("[AD]激励视频预加载中，等待完成后直接展示");
                return;
            }

            // 3. 没有预加载，正常加载并展示
            _isNeedShow = true;

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
            _isGetRewarded = false;
            IsPlaying = true;

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
            if (!_isNeedShow) return;

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
            _isNeedShow = false; // 重置展示标记

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
            if (success && _adType == AdType.Reward)
            {
                UnityTimer.Delay(2f, () => PreloadRewardVideo());
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
                    var adType = (AdType)ad.adType;

                    Debug.Log($"[OnLoadAdsTrigger]type：{adType},uniqueId：{ad.uniqueId},rewarded：{ad.rewarded},clicked：{ad.clicked}");

                    // 激励视频预加载完成
                    if (_isPreloadingReward && adType == AdType.Reward)
                    {
                        _isPreloadingReward = false;

                        // 如果此时有 ShowReward 在等待，就直接展示
                        if (_isNeedShow && _adType == AdType.Reward && _completeCallback != null)
                        {
                            DisplayAd(ad);
                        }
                        else
                        {
                            // 纯预加载，缓存起来
                            _preloadedAds[adType] = ad;
                            Debug.Log("[AD]激励视频预加载完成，已缓存");
                            // 预加载不显示 loading，无需隐藏
                        }
                        return;
                    }
                    // 非预加载流程，立即展示
                    DisplayAd(ad);
                }
                else
                {
                    Debug.Log($"[OnLoadAdsTrigger]targetSignal Ad null, Code :{signal.code} Message : {signal.message}");

                    _isPreloadingReward = false;

                    if (_isNeedShow && _adType != AdType.Banner)
                        CallbackAd(false);

                    MessageSystem.Instance.HideLoadingAnimation();
                }
            }
            else
            {
                Debug.Log($"[OnLoadAdsTrigger]LoadAds Error, Code :{signal.code} Message : {signal.message}");

                _isPreloadingReward = false;

                if (_isNeedShow && _adType != AdType.Banner)
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
                    bool isClose = statusLower.Contains("close");
                    bool isFail = statusLower.Contains("fail");

                    // 1. 无条件解除锁定和UI，不管是什么广告
                    if (isClose || isFail)
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
                    if (isClose || isFail)
                    {
                        if (_adType == AdType.Reward)
                        {
                            if (_isGetRewarded)
                            {
                                CallbackAd(true);
                            }
                            else if (isFail)
                            {
                                // 只有真正失败时才回调 false
                                CallbackAd(false);
                            }
                            else if (isClose)
                            {
                                // 玩家手动关闭且未获得奖励，不调用 CallbackAd(false)
                                // 但需要清理回调，避免残留
                                _completeCallback = null;
                                _isNeedShow = false;
                                Debug.Log("[AD] 玩家手动关闭激励视频且未获得奖励，不回调 false");
                            }
                        }
                        else if (_adType == AdType.Interstitial)
                        {
                            // 插屏广告只要没 fail，关闭时就发奖
                            CallbackAd(!isFail);
                        }

                        // 广告关闭或失败后，尝试重新预加载激励视频
                        if (isFail && _adType == AdType.Reward)
                        {
                            Debug.Log("[AD] 激励视频展示失败，重新预加载");
                            UnityTimer.Delay(3f, () => ForcePreloadAds());
                        }
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

                    // 发生错误时重新预加载激励视频
                    if (_adType == AdType.Reward)
                    {
                        UnityTimer.Delay(5f, () => ForcePreloadAds());
                    }
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