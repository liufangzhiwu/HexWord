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
        // ================== 配置常量 ==================
        private const int MAX_POOL_SIZE = 1;                              // 每个广告位预加载池最大数量
        private const int MAX_RETRY_COUNT = 3;                            // 单个预加载失败最大重试次数
        private static readonly float[] RETRY_DELAYS = { 1f, 3f, 5f };    // 重试间隔

        // 广告位 ID
        private const string REWARD_AD_ID = "s1emwq0ad9";
        private const string TIP_TOOL_REWARD_AD_ID = "e5dojfoi6c";
        private const string INTERSTITIAL_AD_ID = "i0tgl4g0bw";

        // ================== 基础字段 ==================
        public bool isLoadReady;
        public bool IsPlaying { get; set; }
        private string _uniqueId;
        Define.AdKey _currentAdKey;

        SignalHandler SignalHandlerObj;
        AdsStatusSignalHandle SignalReceiveObj;

        // ================== 预加载池（按广告位管理） ==================
        private class AdPreloadInfo
        {
            public string adId;
            public AdType adType;
            public Advertisement readyAd;   // 池中已就绪广告
            public bool isLoading;          // 是否有在途预加载请求（含重试中）
            public bool pendingPreload;     // 结束后是否需要补池
            public int retryCount;          // 已重试次数
        }

        private readonly Dictionary<string, AdPreloadInfo> _preloadInfos = new Dictionary<string, AdPreloadInfo>();
        private bool _isInGameScene = false;    // 是否已进入游戏场景
        private bool _isGetRewarded = false;
        private DateTime _lastShowRewardAdsTime = DateTime.MinValue;

        // 通用回调
        private Action<bool> _completeCallback;
        private AdType _adType;
        private AdPreloadInfo _currentSlot;     // 当前播放所对应的预加载槽

        // ================== 生命周期 ==================
        public void Init(float delay)
        {
            CreateAdsObj();

            UnityTimer.Delay(delay, () =>
            {
                SignalHandler.Instance.RegisterSignalDelegate<AdsLoadSignal>(OnLoadAdsTrigger);
                SignalHandler.Instance.RegisterSignalDelegate<AdsShowSignal>(OnShowAdsTrigger);
                SignalHandler.Instance.RegisterSignalDelegate<AdsStatusSignal>(OnAdsStatusTrigger);
                _uniqueId = Game.self.GetUniqueId();

                // 广告 SDK 初始化完成，加载第 1 个
                Debug.Log("[AD] SDK 初始化完成，发起预加载");
                TryPreload(REWARD_AD_ID, AdType.Reward);
            });
        }

        public bool IsReady(Define.AdKey key) => true;

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

        #region 预加载池管理

        private AdPreloadInfo GetOrCreateInfo(string adId, AdType adType)
        {
            if (!_preloadInfos.TryGetValue(adId, out var info))
            {
                info = new AdPreloadInfo { adId = adId, adType = adType };
                _preloadInfos[adId] = info;
            }
            return info;
        }

        /// <summary>
        /// 池计数：已就绪 + 在途（含重试中）
        /// </summary>
        private int GetPoolCount(AdPreloadInfo info)
        {
            int count = 0;
            if (info.readyAd != null) count++;
            if (info.isLoading) count++;
            return count;
        }

        /// <summary>
        /// 外部接口：用户点击【开始游戏/下一关】
        /// </summary>
        public void OnEnterGameScene()
        {
            _isInGameScene = true;
            Debug.Log("[AD] 进入游戏场景，检查补池");
            TryPreload(REWARD_AD_ID, AdType.Reward);
            TryPreload(TIP_TOOL_REWARD_AD_ID, AdType.Reward);
        }

        /// <summary>
        /// 预加载核心入口：满足条件才发起请求
        /// </summary>
        private void TryPreload(string adId, AdType adType)
        {
            var info = GetOrCreateInfo(adId, adType);

            // 播放中禁止预加载
            if (IsPlaying)
            {
                info.pendingPreload = true;
                Debug.Log($"[AD] 播放中，延后预加载，pending={adId}");
                return;
            }

            // 池已满（已就绪 + 在途）
            if (GetPoolCount(info) >= MAX_POOL_SIZE)
            {
                Debug.Log($"[AD] 池已满({GetPoolCount(info)}/{MAX_POOL_SIZE})，跳过 {adId}");
                return;
            }

            // 已有在途请求，标记 pending
            if (info.isLoading)
            {
                info.pendingPreload = true;
                Debug.Log($"[AD] 已有在途请求，pending={adId}");
                return;
            }

            DoPreload(info);
        }

        private void DoPreload(AdPreloadInfo info)
        {
            info.isLoading = true;

            var adRequestParams = new AdRequestParams()
            {
                adType = (int)info.adType,
                adId = info.adId,
                oaid = _uniqueId,
                isPreload = true
            };
            var adOptions = new AdOptions();

            Debug.Log($"[AD] 发起预加载 {info.adId}，已重试 {info.retryCount} 次");
            OHSDKKitManager.Instance.LoadAds(adRequestParams, adOptions);
        }

        /// <summary>
        /// 预加载请求结束（成功或彻底失败）统一处理
        /// </summary>
        private void OnPreloadFinished(AdPreloadInfo info, Advertisement ad, bool isNoFill)
        {
            info.isLoading = false;

            if (ad != null)
            {
                // 成功入池
                info.readyAd = ad;
                info.retryCount = 0;
                Debug.Log($"[AD] 预加载成功：{info.adId}");
                CheckPendingAndRefill(info);
                return;
            }

            // 失败
            Debug.Log($"[AD] 预加载失败：{info.adId}，noFill={isNoFill}，retry={info.retryCount}");

            // 无填充/无广告：不重试
            if (isNoFill)
            {
                info.retryCount = 0;
                CheckPendingAndRefill(info);
                return;
            }

            // 重试
            if (info.retryCount < MAX_RETRY_COUNT)
            {
                int idx = Math.Min(info.retryCount, RETRY_DELAYS.Length - 1);
                float delay = RETRY_DELAYS[idx];
                info.retryCount++;

                Debug.Log($"[AD] {delay}s 后第 {info.retryCount} 次重试：{info.adId}");

                UnityTimer.Delay(delay, () =>
                {
                    // 重试前置检查
                    if (info.readyAd != null)
                    {
                        Debug.Log($"[AD] 重试时池中已有广告，取消 {info.adId}");
                        return;
                    }
                    if (info.isLoading || IsPlaying)
                    {
                        Debug.Log($"[AD] 重试时在途/播放中，标记 pending：{info.adId}");
                        info.pendingPreload = true;
                        return;
                    }
                    DoPreload(info);
                });
            }
            else
            {
                Debug.Log($"[AD] 重试耗尽，放弃 {info.adId}");
                info.retryCount = 0;
                // 释放"在途"状态
                CheckPendingAndRefill(info);
            }
        }

        /// <summary>
        /// pending 检查补池
        /// </summary>
        private void CheckPendingAndRefill(AdPreloadInfo info)
        {
            if (info.pendingPreload &&
                GetPoolCount(info) < MAX_POOL_SIZE &&
                !info.isLoading)
            {
                info.pendingPreload = false;
                Debug.Log($"[AD] 补池：{info.adId}");
                TryPreload(info.adId, info.adType);
            }
        }

        /// <summary>
        /// 广告播放结束后统一检查：所有槽位补池
        /// </summary>
        private void OnAdPlaybackEnded()
        {
            foreach (var kv in _preloadInfos)
            {
                var info = kv.Value;
                if (info.pendingPreload)
                {
                    CheckPendingAndRefill(info);
                }
                else if (GetPoolCount(info) < MAX_POOL_SIZE && !info.isLoading)
                {
                    TryPreload(info.adId, info.adType);
                }
            }
        }

        /// <summary>
        /// 立即停止某个广告位的重试循环（用于用户点播走实时加载）
        /// </summary>
        private void CancelRetry(AdPreloadInfo info)
        {
            info.retryCount = MAX_RETRY_COUNT;
        }

        #endregion

        #region Show 接口

        public void ShowTipToolReward(Define.AdKey key, Action<bool> callback)
        {
            ShowRewardInternal(key, TIP_TOOL_REWARD_AD_ID, callback);
        }

        public void ShowReward(Define.AdKey key, Action<bool> callback)
        {
            ShowRewardInternal(key, REWARD_AD_ID, callback);
        }

        private void ShowRewardInternal(Define.AdKey key, string adId, Action<bool> callback)
        {
            CreateAdsObj();

            _currentAdKey = key;
            _completeCallback = callback;
            _adType = AdType.Reward;
            _isGetRewarded = false;
            IsPlaying = true;
            _isNeedShow_Field = true;
            _lastShowRewardAdsTime = DateTime.Now;

            var info = GetOrCreateInfo(adId, AdType.Reward);
            _currentSlot = info;

            // 1. 池中已有就绪广告 → 立即展示
            if (info.readyAd != null)
            {
                var ad = info.readyAd;
                info.readyAd = null;
                Debug.Log($"[AD] 使用预加载广告：{adId}");
                DisplayAd(ad);
                return;
            }

            // 2. 池中无就绪广告 → 立即停止重试
            CancelRetry(info);

            // 3. 若有在途请求 → 等待完成后直接展示
            if (info.isLoading)
            {
                MessageSystem.Instance.ShowLoadingAnimation();
                Debug.Log($"[AD] 预加载在途，等待完成后展示：{adId}");
                return;
            }

            // 4. 无在途请求 → 走实时加载
            info.isLoading = true;
            MessageSystem.Instance.ShowLoadingAnimation();

            var adRequestParams = new AdRequestParams()
            {
                adType = (int)AdType.Reward,
                adId = adId,
                oaid = _uniqueId,
                isPreload = true
            };
            var adOptions = new AdOptions();
            Debug.Log($"[AD] 实时加载激励视频：{adId}");
            OHSDKKitManager.Instance.LoadAds(adRequestParams, adOptions);
        }

        public void ShowInterstitial(Action<bool> callback)
        {
            CreateAdsObj();

            _completeCallback = callback;
            _adType = AdType.Interstitial;
            _isNeedShow_Field = true;
            _isGetRewarded = false;
            IsPlaying = true;

            var info = GetOrCreateInfo(INTERSTITIAL_AD_ID, AdType.Interstitial);
            _currentSlot = info;

            if (info.readyAd != null)
            {
                var ad = info.readyAd;
                info.readyAd = null;
                Debug.Log("[AD] 使用预加载插屏广告");
                DisplayAd(ad);
                return;
            }

            CancelRetry(info);

            if (info.isLoading)
            {
                MessageSystem.Instance.ShowLoadingAnimation();
                return;
            }

            info.isLoading = true;

            var adRequestParams = new AdRequestParams()
            {
                adType = (int)_adType,
                adId = INTERSTITIAL_AD_ID,
                oaid = _uniqueId,
                isPreload = true
            };
            var adOptions = new AdOptions();
            Debug.Log("[AD] 实时加载插屏广告");
            OHSDKKitManager.Instance.LoadAds(adRequestParams, adOptions);
        }

        public void LoadBannerAD() { }
        private AdRequestParams BanneradRequestParams;
        public void ShowBanner() { }
        public void HideBanner() { }

        #endregion

        #region 通用逻辑

        // 保留 _isNeedShow 字段（沿用原代码结构）
        private bool _isNeedShow_Field = false;

        private void DisplayAd(Advertisement ad)
        {
            if (!_isNeedShow_Field) return;

            Debug.Log("[AD]展示广告: " + (AdType)ad.adType);
            var adDisplayOptions = new AdDisplayOptions();
            ad.isFullScreen = true;
            OHSDKKitManager.Instance.ShowAds(ad, adDisplayOptions);

            if ((AdType)ad.adType == AdType.Reward)
            {
                string desc = "";
                switch (_currentAdKey)
                {
                    case Define.AdKey.RewardAdIdStoreGold: desc = "奖励广告-商店金币"; break;
                    case Define.AdKey.RewardAdIdItemGold: desc = "奖励广告-物品金币"; break;
                    case Define.AdKey.RewardAdIdCheckinGold1: desc = "奖励广告-签到金币1"; break;
                    case Define.AdKey.RewardAdIdCheckinGold2: desc = "奖励广告-签到金币2"; break;
                    case Define.AdKey.RewardAdIdCheckinGold3: desc = "奖励广告-签到金币3"; break;
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
            var endedType = _adType;
            var slot = _currentSlot;

            IsPlaying = false;
            _isNeedShow_Field = false;

            if (_completeCallback != null)
            {
                var tempCallback = _completeCallback;
                _completeCallback = null;

                try
                {
                    tempCallback.Invoke(success);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AD] 业务回调报错: {e.Message}");
                }
            }

            _currentSlot = null;

            // 激励视频关闭后（成功、失败、中途关闭都算），补池
            if (endedType == AdType.Reward)
            {
                UnityTimer.Delay(0.5f, () => OnAdPlaybackEnded());
            }
        }

        /// <summary>
        /// 只有"手动关闭且未获得奖励"时走此分支：不回调业务，但要补池
        /// </summary>
        private void HandleManualCloseNoReward()
        {
            var slot = _currentSlot;

            IsPlaying = false;
            _isNeedShow_Field = false;
            _completeCallback = null;
            _currentSlot = null;

            Debug.Log("[AD] 玩家手动关闭激励视频且未获得奖励，不回调 false");

            UnityTimer.Delay(0.5f, () => OnAdPlaybackEnded());
        }

        private void OnLoadAdsTrigger(SignalBase signal)
        {
            bool hasError = signal.hasError();
            string msg = signal.message;

            if (!hasError)
            {
                var targetSignal = (AdsLoadSignal)signal;
                var ad = (targetSignal.ads != null && targetSignal.ads.Count > 0) ? targetSignal.ads[0] : null;

                if (ad != null)
                {
                    var adType = (AdType)ad.adType;
                    Debug.Log($"[OnLoadAdsTrigger] type={adType}, uniqueId={ad.uniqueId}, rewarded={ad.rewarded}, clicked={ad.clicked}");

                    // 匹配到当前正在加载的槽位
                    var matched = FindLoadingSlot(adType);

                    if (matched != null)
                    {
                        matched.isLoading = false;
                        matched.retryCount = 0;

                        // 用户正在等待展示
                        if (_isNeedShow_Field && _adType == adType && _completeCallback != null)
                        {
                            Debug.Log($"[AD] 加载完成，直接展示：{matched.adId}");
                            DisplayAd(ad);
                            return;
                        }

                        // 纯预加载 → 入池
                        matched.readyAd = ad;
                        Debug.Log($"[AD] 预加载完成，入池：{matched.adId}");
                        CheckPendingAndRefill(matched);
                        return;
                    }

                    // 没有匹配槽位，兜底直接展示
                    DisplayAd(ad);
                    return;
                }
            }

            // 加载失败
            Debug.Log($"[OnLoadAdsTrigger] 加载失败，code={signal.code}, msg={msg}");
            HandleLoadFail(IsNoFillMessage(msg));
        }

        private AdPreloadInfo FindLoadingSlot(AdType adType)
        {
            foreach (var kv in _preloadInfos)
            {
                if (kv.Value.isLoading && kv.Value.adType == adType)
                    return kv.Value;
            }
            return null;
        }

        private bool IsNoFillMessage(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return false;
            var lower = msg.ToLower();
            return lower.Contains("no fill") || lower.Contains("nofill") || lower.Contains("no ad")
                || lower.Contains("无填充") || lower.Contains("无广告");
        }

        private void HandleLoadFail(bool isNoFill)
        {
            MessageSystem.Instance.HideLoadingAnimation();

            var matched = FindLoadingSlot(_adType);
            if (matched == null) matched = _currentSlot;

            // 用户点播的实时加载失败
            if (_isNeedShow_Field && _completeCallback != null && matched == _currentSlot)
            {
                if (matched != null) matched.isLoading = false;
                CallbackAd(false);
                return;
            }

            // 纯预加载失败
            if (matched != null)
            {
                OnPreloadFinished(matched, null, isNoFill);
            }
        }

        private void OnShowAdsTrigger(SignalBase signal)
        {
            if (!signal.hasError())
            {
                var targetSignal = (AdsShowSignal)signal;
                Debug.Log($"[OnShowAdsTrigger] type={(AdType)targetSignal.adType}, uniqueId={targetSignal.uniqueId}");
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

                    // 1. 释放锁定和UI
                    if (isClose || isFail)
                    {
                        Game.self.ResumeGame();
                        IsPlaying = false;
                        if (MessageSystem.Instance != null) MessageSystem.Instance.HideLoadingAnimation();
                    }

                    // 2. 视频达标标记
                    if (statusLower.Contains("reward") || statusLower.Contains("videoplayend"))
                    {
                        if (_adType == AdType.Reward)
                        {
                            _isGetRewarded = true;
                            Debug.Log("[AD] 激励视频达标，标记可发奖");
                        }
                    }

                    // 3. 关闭/失败结算
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
                                CallbackAd(false);
                            }
                            else if (isClose)
                            {
                                // 玩家手动关闭且未获得奖励 → 不回调 false
                                HandleManualCloseNoReward();
                            }
                        }
                        else if (_adType == AdType.Interstitial)
                        {
                            CallbackAd(!isFail);
                        }
                    }
                }
                else
                {
                    // Error 分支
                    Debug.LogError("[AD] 鸿蒙信号附带 Error！");
                    Game.self.ResumeGame();
                    IsPlaying = false;
                    if (MessageSystem.Instance != null) MessageSystem.Instance.HideLoadingAnimation();

                    if (_adType != AdType.Banner && _completeCallback != null)
                    {
                        CallbackAd(false);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AD] OnAdsStatusTrigger 异常: {ex.Message}");
                Game.self.ResumeGame();
                IsPlaying = false;
                if (MessageSystem.Instance != null) MessageSystem.Instance.HideLoadingAnimation();
            }
        }

        #endregion
    }
}
#endif