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
        private const int MAX_POOL_SIZE = 2;
        private const int MAX_RETRY_COUNT = 3;
        private static readonly float[] RETRY_DELAYS = { 1f, 3f, 5f };

        /// <summary>
        /// true  ：同 adType 全局串行（绝对不串台，但同类型广告位不能并行预加载）
        /// false ：允许同 adType 并行（依赖 SDK 按提交顺序返回，效率高，实践中可用）
        /// </summary>
        private const bool STRICT_SERIAL_PER_ADTYPE = false;

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

        // ================== 预加载槽位 ==================
        private class AdPreloadInfo
        {
            public string adId;
            public AdType adType;
            public Advertisement readyAd;       // 池中已就绪广告
            public bool isLoading;              // 该槽位在途
            public bool wantPreload;            // 触发标记
            public bool pendingShow;            // 用户点播等待
            public Define.AdKey pendingKey;
            public Action<bool> pendingCallback;
            public int retryCount;
        }

        private readonly Dictionary<string, AdPreloadInfo> _preloadInfos
            = new Dictionary<string, AdPreloadInfo>();

        // ★ 提交顺序队列（FIFO 匹配返回信号）
        private readonly Queue<AdPreloadInfo> _submitQueue = new Queue<AdPreloadInfo>();

        private bool _isInGameScene = false;
        private bool _isGetRewarded = false;
        private DateTime _lastShowRewardAdsTime = DateTime.MinValue;

        private AdPreloadInfo _currentSlot;
        private Action<bool> _completeCallback;
        private AdType _adType;
        private bool _isNeedShow_Field = false;

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

                Debug.Log("[AD] SDK 初始化完成，发起预加载");
                TryPreload(REWARD_AD_ID, AdType.Reward);
                TryPreload(TIP_TOOL_REWARD_AD_ID, AdType.Reward);
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

        private int GetPoolCount(AdPreloadInfo info)
        {
            int count = 0;
            if (info.readyAd != null) count++;
            if (info.isLoading) count++;
            return count;
        }

        public void OnEnterGameScene()
        {
            _isInGameScene = true;
            Debug.Log("[AD] 进入游戏场景，检查补池");
            TryPreload(REWARD_AD_ID, AdType.Reward);
            TryPreload(TIP_TOOL_REWARD_AD_ID, AdType.Reward);
        }

        private void TryPreload(string adId, AdType adType)
        {
            var info = GetOrCreateInfo(adId, adType);

            if (IsPlaying)
            {
                info.wantPreload = true;
                Debug.Log($"[AD] 播放中，标记 wantPreload：{adId}");
                return;
            }

            if (GetPoolCount(info) >= MAX_POOL_SIZE)
            {
                Debug.Log($"[AD] 池已满，跳过 {adId}");
                return;
            }

            if (info.isLoading)
            {
                // 已有在途，等它完成后再判断（PumpPreload 会在结束回调里被调）
                Debug.Log($"[AD] 已在途，跳过 {adId}");
                return;
            }

            info.wantPreload = true;
            PumpPreload();
        }

        /// <summary>
        /// ★ 核心泵：一次可发多个请求（每个广告位最多 1 个）
        /// 优先级：pendingShow > wantPreload
        /// </summary>
        private void PumpPreload()
        {
            if (IsPlaying)
            {
                Debug.Log("[AD] Pump 跳过：播放中");
                return;
            }

            // 1) 用户点播优先（点播槽位先发）
            foreach (var kv in _preloadInfos)
            {
                var info = kv.Value;
                if (info.pendingShow && !info.isLoading && info.readyAd == null)
                {
                    if (CanSubmitNow(info))
                    {
                        Debug.Log($"[AD] Pump 优先点播：{info.adId}");
                        DoPreload(info);
                    }
                }
            }

            // 2) 常规预加载
            foreach (var kv in _preloadInfos)
            {
                var info = kv.Value;
                if (info.wantPreload
                    && !info.isLoading
                    && info.readyAd == null
                    && GetPoolCount(info) < MAX_POOL_SIZE)
                {
                    if (CanSubmitNow(info))
                    {
                        Debug.Log($"[AD] Pump 预加载：{info.adId}");
                        DoPreload(info);
                    }
                }
            }
        }

        /// <summary>
        /// 是否允许现在提交（STRICT 模式下同 adType 至多 1 个在途）
        /// </summary>
        private bool CanSubmitNow(AdPreloadInfo info)
        {
            if (!STRICT_SERIAL_PER_ADTYPE) return true;

            foreach (var kv in _preloadInfos)
            {
                if (kv.Value != info && kv.Value.adType == info.adType && kv.Value.isLoading)
                    return false;
            }
            return true;
        }

        private void DoPreload(AdPreloadInfo info)
        {
            info.isLoading = true;
            info.wantPreload = false;
            _submitQueue.Enqueue(info);   // ★ 记录提交顺序

            var adRequestParams = new AdRequestParams()
            {
                adType = (int)info.adType,
                adId = info.adId,
                oaid = _uniqueId,
                isPreload = true
            };
            var adOptions = new AdOptions();

            Debug.Log($"[AD] 发起预加载 {info.adId}，在途队列长度={_submitQueue.Count}");
            OHSDKKitManager.Instance.LoadAds(adRequestParams, adOptions);
        }

        /// <summary>
        /// 请求结束统一处理
        /// </summary>
        private void OnPreloadFinished(AdPreloadInfo info, Advertisement ad, bool isNoFill)
        {
            info.isLoading = false;

            // ---------- 成功 ----------
            if (ad != null)
            {
                info.retryCount = 0;

                // 用户点播在等待 → 立即展示
                if (info.pendingShow)
                {
                    Debug.Log($"[AD] 点播请求完成，立即展示：{info.adId}");
                    ShowInternalFromSlot(info, ad);
                    return;
                }

                // 纯预加载 → 入池
                info.readyAd = ad;
                Debug.Log($"[AD] 预加载入池：{info.adId}");
                PumpPreload();
                return;
            }

            // ---------- 失败 ----------
            Debug.Log($"[AD] 预加载失败：{info.adId}，noFill={isNoFill}，retry={info.retryCount}");

            if (info.pendingShow)
            {
                var cb = info.pendingCallback;
                info.pendingShow = false;
                info.pendingCallback = null;

                MessageSystem.Instance.HideLoadingAnimation();
                Debug.Log($"[AD] 点播请求失败，回调 false：{info.adId}");
                cb?.Invoke(false);

                PumpPreload();
                return;
            }

            if (isNoFill)
            {
                info.retryCount = 0;
                PumpPreload();
                return;
            }

            if (info.retryCount < MAX_RETRY_COUNT)
            {
                int idx = Math.Min(info.retryCount, RETRY_DELAYS.Length - 1);
                float delay = RETRY_DELAYS[idx];
                info.retryCount++;

                Debug.Log($"[AD] {delay}s 后第 {info.retryCount} 次重试：{info.adId}");

                UnityTimer.Delay(delay, () =>
                {
                    if (info.readyAd != null) return;
                    if (info.isLoading) return;
                    info.wantPreload = true;
                    PumpPreload();
                });
            }
            else
            {
                Debug.Log($"[AD] 重试耗尽，放弃 {info.adId}");
                info.retryCount = 0;
                PumpPreload();
            }
        }

        private void ShowInternalFromSlot(AdPreloadInfo info, Advertisement ad)
        {
            _currentSlot = info;
            _adType = info.adType;
            _currentAdKey = info.pendingKey;
            _completeCallback = info.pendingCallback;
            _isNeedShow_Field = true;
            IsPlaying = true;

            info.pendingShow = false;
            info.pendingCallback = null;

            MessageSystem.Instance.HideLoadingAnimation();
            DisplayAd(ad);
        }

        private void CancelRetry(AdPreloadInfo info)
        {
            info.retryCount = MAX_RETRY_COUNT;
        }

        #endregion

        #region FIFO 匹配

        /// <summary>
        /// 成功返回：按 adType 从队列中匹配最早的槽位
        /// </summary>
        private AdPreloadInfo DequeueForReturnedAd(Advertisement ad)
        {
            var adType = (AdType)ad.adType;

            // 优先看队首（最符合 FIFO 场景）
            if (_submitQueue.Count > 0)
            {
                var head = _submitQueue.Peek();
                if (head.isLoading && head.adType == adType)
                {
                    var matched = _submitQueue.Dequeue();
                    Debug.Log($"[AD] FIFO 匹配队首：{matched.adId}");
                    return matched;
                }
            }

            // 队首不是（可能多类型交叉），遍历找第一个匹配 adType 的
            var tmp = new List<AdPreloadInfo>();
            AdPreloadInfo result = null;
            while (_submitQueue.Count > 0)
            {
                var item = _submitQueue.Dequeue();
                if (result == null && item.isLoading && item.adType == adType)
                {
                    result = item;
                }
                else
                {
                    tmp.Add(item);
                }
            }
            foreach (var item in tmp) _submitQueue.Enqueue(item);

            if (result != null)
                Debug.Log($"[AD] FIFO 匹配（遍历）：{result.adId}");

            return result;
        }

        /// <summary>
        /// 失败返回：信号里没有 adType 信息，只能 FIFO 出队最早的一个
        /// </summary>
        private AdPreloadInfo DequeueForFailedAd()
        {
            while (_submitQueue.Count > 0)
            {
                var item = _submitQueue.Dequeue();
                if (item.isLoading)
                {
                    Debug.Log($"[AD] 失败信号匹配队列：{item.adId}");
                    return item;
                }
            }
            return null;
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
            
            Debug.Log($"[AD] 点击视频点：{adId}");
            
            _currentAdKey = key;
            _completeCallback = callback;
            _adType = AdType.Reward;
            _isGetRewarded = false;
            _lastShowRewardAdsTime = DateTime.Now;

            var info = GetOrCreateInfo(adId, AdType.Reward);
            _currentSlot = info;

            // 1) 池中已就绪 → 立即展示
            if (info.readyAd != null)
            {
                var ad = info.readyAd;
                info.readyAd = null;
                IsPlaying = true;
                _isNeedShow_Field = true;
                Debug.Log($"[AD] 使用预加载广告（命中池）：{adId}");
                DisplayAd(ad);
                return;
            }

            // 2) 池空 → 停止重试
            CancelRetry(info);

            // 3) 标记用户点播
            info.pendingShow = true;
            info.pendingKey = key;
            info.pendingCallback = callback;
            info.wantPreload = true;

            // 播放中禁止 pump，但用户点播必须 pump（会发新请求）
            // IsPlaying 暂不置 true，等 DisplayAd 才置
            MessageSystem.Instance.ShowLoadingAnimation();

            Debug.Log($"[AD] 点播等待：{adId}，本槽在途={info.isLoading}");
            PumpPreload();
        }

        public void ShowInterstitial(Action<bool> callback)
        {
            CreateAdsObj();

            _completeCallback = callback;
            _adType = AdType.Interstitial;
            _isNeedShow_Field = true;
            _isGetRewarded = false;

            var info = GetOrCreateInfo(INTERSTITIAL_AD_ID, AdType.Interstitial);
            _currentSlot = info;

            if (info.readyAd != null)
            {
                var ad = info.readyAd;
                info.readyAd = null;
                IsPlaying = true;
                Debug.Log("[AD] 使用预加载插屏广告");
                DisplayAd(ad);
                return;
            }

            CancelRetry(info);

            info.pendingShow = true;
            info.pendingCallback = callback;
            info.wantPreload = true;

            MessageSystem.Instance.ShowLoadingAnimation();
            PumpPreload();
        }

        public void LoadBannerAD() { }
        private AdRequestParams BanneradRequestParams;
        public void ShowBanner() { }
        public void HideBanner() { }

        #endregion

        #region 通用逻辑

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
                try { tempCallback.Invoke(success); }
                catch (Exception e) { Debug.LogError($"[AD] 业务回调报错: {e.Message}"); }
            }

            _currentSlot = null;

            if (endedType == AdType.Reward)
            {
                // 播放结束后补池：所有 Reward 槽位
                UnityTimer.Delay(0.5f, () =>
                {
                    foreach (var kv in _preloadInfos)
                    {
                        var info = kv.Value;
                        if (info.adType == AdType.Reward
                            && info.readyAd == null
                            && !info.isLoading)
                        {
                            info.wantPreload = true;
                        }
                    }
                    PumpPreload();
                });
            }
        }

        private void HandleManualCloseNoReward()
        {
            IsPlaying = false;
            _isNeedShow_Field = false;
            _completeCallback = null;
            _currentSlot = null;

            Debug.Log("[AD] 玩家手动关闭激励视频且未获得奖励，不回调 false");

            UnityTimer.Delay(0.5f, () =>
            {
                foreach (var kv in _preloadInfos)
                {
                    var info = kv.Value;
                    if (info.adType == AdType.Reward
                        && info.readyAd == null
                        && !info.isLoading)
                    {
                        info.wantPreload = true;
                    }
                }
                PumpPreload();
            });
        }

        private void OnLoadAdsTrigger(SignalBase signal)
        {
            bool hasError = signal.hasError();
            string msg = signal.message;

            if (!hasError)
            {
                var targetSignal = (AdsLoadSignal)signal;
                var ad = (targetSignal.ads != null && targetSignal.ads.Count > 0)
                    ? targetSignal.ads[0] : null;

                if (ad != null)
                {
                    Debug.Log($"[AD] [OnLoadAdsTrigger] type={(AdType)ad.adType}, uniqueId={ad.uniqueId}");

                    // ★ FIFO 匹配
                    var matched = DequeueForReturnedAd(ad);

                    if (matched != null)
                    {
                        OnPreloadFinished(matched, ad, false);
                        return;
                    }

                    // 兜底：无匹配（异常情况），直接展示
                    Debug.LogWarning("[AD] 无匹配在途槽位，兜底展示");
                    _adType = (AdType)ad.adType;
                    _isNeedShow_Field = true;
                    DisplayAd(ad);
                    return;
                }
            }

            // 加载失败
            Debug.Log($"[OnLoadAdsTrigger] 加载失败，code={signal.code}, msg={msg}");

            var failSlot = DequeueForFailedAd();
            if (failSlot != null)
            {
                OnPreloadFinished(failSlot, null, IsNoFillMessage(msg));
                return;
            }

            // 无在途但有用户等待
            if (_isNeedShow_Field && _completeCallback != null)
            {
                MessageSystem.Instance.HideLoadingAnimation();
                CallbackAd(false);
            }
        }

        private bool IsNoFillMessage(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return false;
            var lower = msg.ToLower();
            return lower.Contains("no fill") || lower.Contains("nofill") || lower.Contains("no ad")
                || lower.Contains("无填充") || lower.Contains("无广告");
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

                    if (isClose || isFail)
                    {
                        Game.self.ResumeGame();
                        IsPlaying = false;
                        if (MessageSystem.Instance != null)
                            MessageSystem.Instance.HideLoadingAnimation();
                    }

                    if (statusLower.Contains("reward") || statusLower.Contains("videoplayend"))
                    {
                        if (_adType == AdType.Reward)
                        {
                            _isGetRewarded = true;
                            Debug.Log("[AD] 激励视频达标，标记可发奖");
                        }
                    }

                    if (isClose || isFail)
                    {
                        if (_adType == AdType.Reward)
                        {
                            if (_isGetRewarded) CallbackAd(true);
                            else if (isFail) CallbackAd(false);
                            else if (isClose) HandleManualCloseNoReward();
                        }
                        else if (_adType == AdType.Interstitial)
                        {
                            CallbackAd(!isFail);
                        }
                    }
                }
                else
                {
                    Debug.LogError("[AD] 鸿蒙信号附带 Error！");
                    Game.self.ResumeGame();
                    IsPlaying = false;
                    if (MessageSystem.Instance != null)
                        MessageSystem.Instance.HideLoadingAnimation();

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
                if (MessageSystem.Instance != null)
                    MessageSystem.Instance.HideLoadingAnimation();
            }
        }

        #endregion
    }
}
#endif