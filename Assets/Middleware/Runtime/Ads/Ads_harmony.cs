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
        private const float AD_CACHE_EXPIRE_MINUTES = 60f;   // 预加载广告有效期
        private const float SHOW_TIMEOUT_SECONDS = 12f;      // 展示超时兜底

        //保持广告串行加载值为false
        private const bool STRICT_SERIAL_PER_ADTYPE = false;

        private const string REWARD_AD_ID = "s1emwq0ad9";
        private const string TIP_TOOL_REWARD_AD_ID = "e5dojfoi6c";
        private const string INTERSTITIAL_AD_ID = "i0tgl4g0bw";

        /// <summary>
        /// 横幅广告位：AGP 后台申请的鸿蒙正式横幅位 n56633mwpp。
        /// 统一取 Define.ConfigHarmony.TestBannerAdId，避免两处各写一份对不上。
        /// </summary>
        private const string BANNER_AD_ID = Define.ConfigHarmony.TestBannerAdId;

        /// <summary>横幅期望尺寸（vp）：鸿蒙横幅要求宽高必填，360x57 是华为推荐比例</summary>
        private const int BANNER_WIDTH_VP = 360;
        private const int BANNER_HEIGHT_VP = 57;

        /// <summary>横幅轮播间隔（ms），取值范围 [30000, 120000]</summary>
        private const int BANNER_REFRESH_TIME = 30000;

        /// <summary>
        /// 鸿蒙 AdsKit 的横幅广告类型值（advertising.AdRequestParams.adType）：
        /// 1=开屏、3=原生、7=激励、8=横幅、12=插屏、60=贴片。
        /// 注意不能用 C# 内部 AdType.Banner（=3），那是项目自定义枚举，
        /// 传给 ArkTS 会被鸿蒙 SDK 当成"原生广告"导致横幅拉不出来。
        /// </summary>
        private const int HARMONY_AD_TYPE_BANNER = 8;

        // ================== 基础字段 ==================
        public bool isLoadReady;
        public bool IsPlaying { get; set; }
        private string _uniqueId;
        Define.AdKey _currentAdKey;

        SignalHandler SignalHandlerObj;
        AdsStatusSignalHandle SignalReceiveObj;

        // ================== 预加载槽位 ==================
        private class CachedAd
        {
            public Advertisement ad;
            public DateTime loadTime;
        }

        private class AdPreloadInfo
        {
            public string adId;
            public AdType adType;
            public List<CachedAd> readyAds = new List<CachedAd>();
            public bool isLoading;
            public bool wantPreload;
            public bool pendingShow;
            public Define.AdKey pendingKey;
            public Action<bool> pendingCallback;
            public int retryCount;
        }

        private readonly Dictionary<string, AdPreloadInfo> _preloadInfos
            = new Dictionary<string, AdPreloadInfo>();

        private readonly Queue<AdPreloadInfo> _submitQueue = new Queue<AdPreloadInfo>();

        private bool _isInGameScene = false;
        private bool _isGetRewarded = false;
        private DateTime _lastShowRewardAdsTime = DateTime.MinValue;

        private AdPreloadInfo _currentSlot;
        private Action<bool> _completeCallback;
        private AdType _adType;
        private bool _isNeedShow_Field = false;

        // ================== 展示状态跟踪 ==================
        private DateTime _adShowStartTime;
        private bool _adOpenReceived;
        private bool _adVideoStartReceived;
        private bool _adShowTimeoutTriggered;

        // ================== Banner 状态 ==================
        /// <summary>参数里带过的 OAID，授权状态变化后用它判断要不要重新同步</summary>
        private string _bannerPreparedOaid;
        /// <summary>横幅当前是否处于展示状态（C# 侧记账，ArkTS 侧也会幂等保护）</summary>
        private bool _bannerShowing;
        /// <summary>因为要播全屏广告而临时关掉的横幅，全屏广告结束后要恢复</summary>
        private bool _bannerHiddenByFullScreenAd;
        /// <summary>闸门关闭期间被挡下的横幅参数同步</summary>
        private bool _bannerPending;
        /// <summary>闸门关闭期间被挡下的横幅展示请求</summary>
        private bool _bannerShowPending;
        /// <summary>横幅拉取失败后的冷却截止时间（避免每次进关都重开一次原生对话框）</summary>
        private DateTime _bannerRetryAt = DateTime.MinValue;
        /// <summary>ArkTS 桥接回调（必须持有引用，否则会被 GC 回收导致回调丢失）</summary>
        private static OpenHarmonyJSCallback _adsBridgeCallback;

        // ================== 广告请求闸门 ==================
        /// <summary>
        /// 广告请求闸门：广告追踪授权（APP_TRACKING_CONSENT）流程结束前，禁止发起任何广告请求。
        ///
        /// 原因：华为 AdsKit 在 loadAd 时会自己去取 OAID，应用还没拿到授权时系统会直接弹
        /// "广告标识符 / 跟踪"授权框。之前 Init 里 0.2s 就预加载两个激励位、加载页还同步了
        /// Banner 参数，结果系统弹窗抢在"广告追踪引导页点继续"之前就弹了出来。
        /// 现在统一的节奏是：引导页 → 点继续 → 系统弹窗 → 结果回来后 Game.AllowAdRequests() 放行广告。
        /// </summary>
        public static bool AdRequestsAllowed { get; private set; }

        /// <summary>当前实例，供静态放行入口回调泵起被挡住的请求</summary>
        private static Ads_harmony _current;

        /// <summary>放行广告请求（由 AdTrackingManager 在广告追踪引导流程结束时调用）</summary>
        public static void AllowAdRequests()
        {
            if (AdRequestsAllowed) return;

            AdRequestsAllowed = true;
            Debug.Log("[AD] 广告追踪流程已结束，放行广告请求");
            _current?.PumpAfterTrackingGate();
        }

        /// <summary>闸门打开后，把之前挡下的横幅与预加载补起来</summary>
        private void PumpAfterTrackingGate()
        {
            // 只补"玩家/玩法已经要求展示"的横幅；没要求展示就不要自己弹出来
            if (_bannerShowPending)
            {
                _bannerShowPending = false;
                ShowBanner();
            }

            foreach (var kv in _preloadInfos) kv.Value.wantPreload = true;
            PumpPreload();
        }


        // ================== 生命周期 ==================
        public void Init(float delay)
        {
            _current = this;
            CreateAdsObj();

            UnityTimer.Delay(delay, () =>
            {
                SignalHandler.Instance.RegisterSignalDelegate<AdsLoadSignal>(OnLoadAdsTrigger);
                SignalHandler.Instance.RegisterSignalDelegate<AdsShowSignal>(OnShowAdsTrigger);
                SignalHandler.Instance.RegisterSignalDelegate<AdsStatusSignal>(OnAdsStatusTrigger);
                _uniqueId = Game.self.GetOAID();

                // 注意：这里只是"登记预加载"，真正发请求要等 AdRequestsAllowed 打开，
                // 否则 AdsKit 会在加载页就去取 OAID，把系统授权弹窗抢在引导页之前弹出来。
                Debug.Log($"[AD] SDK 初始化完成，两个激励位登记预加载（闸门={(AdRequestsAllowed ? "开" : "关")}）");
                TryPreload(REWARD_AD_ID, AdType.Reward);
                TryPreload(TIP_TOOL_REWARD_AD_ID, AdType.Reward);
            });
        }

        public bool IsReady(Define.AdKey key) => true;

        public void DestoryAdsObj()
        {
            // SignalHandler / AdsStatusSignalHandle 是 SDKKit 的进程级单例（登录流程先建好），
            // 销毁会连带干掉登录、支付等模块的回调通道，这里只断开引用。
            SignalHandlerObj = null;
            SignalReceiveObj = null;
        }

        public void CreateAdsObj()
        {
            // SignalHandler 是单例：Awake 里发现已有实例会直接抛
            // "Already has a SignalHandler instance!!"。
            // Account_harmony 登录时已通过 SignalHandler.Instance 建好它（并顺带建了
            // AdsStatusSignalHandle），所以这里只能复用，不能再 new 一个挂上去。
            if (SignalHandlerObj == null) SignalHandlerObj = SignalHandler.Instance;

            if (SignalReceiveObj == null)
                SignalReceiveObj = Object.FindObjectOfType<AdsStatusSignalHandle>();
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
            int count = info.readyAds.Count;
            if (info.isLoading) count++;
            return count;
        }

        public void OnEnterGameScene()
        {
            _isInGameScene = true;
            Debug.Log("[AD] 进入游戏场景，检查补池");
            TryPreload(REWARD_AD_ID, AdType.Reward);
            TryPreload(TIP_TOOL_REWARD_AD_ID, AdType.Reward);

            // 注意：不要在这里调 LoadBannerAD()。
            // 鸿蒙侧 LoadBanner 现在会直接拉起横幅弹窗（加载即展示），
            // 进场景就调会让横幅在每个场景切换时自己弹出来；展示时机交给玩法（ShowBanner）。
        }

        private void TryPreload(string adId, AdType adType)
        {
            var info = GetOrCreateInfo(adId, adType);

            // 广告追踪授权流程没走完之前不发任何广告请求（见 AdRequestsAllowed 注释）
            if (!AdRequestsAllowed)
            {
                info.wantPreload = true;
                Debug.Log($"[AD] 等待广告追踪授权，暂缓预加载：{adId}");
                return;
            }

            if (IsPlaying)
            {
                info.wantPreload = true;
                Debug.Log($"[AD] 播放中，标记 wantPreload：{adId}");
                return;
            }

            int maxpool = MAX_POOL_SIZE;
            if (info.adId == REWARD_AD_ID)
            {
                maxpool = 1;
            }

            if (GetPoolCount(info) >= maxpool)
            {
                Debug.Log($"[AD] 池已满({GetPoolCount(info)}/{maxpool})，跳过 {adId}");
                return;
            }

            if (info.isLoading)
            {
                Debug.Log($"[AD] 已在途，跳过 {adId}");
                return;
            }

            info.wantPreload = true;
            PumpPreload();
        }

        private void PumpPreload()
        {
            if (!AdRequestsAllowed)
            {
                Debug.Log("[AD] Pump 跳过：等待广告追踪授权");
                return;
            }

            if (IsPlaying)
            {
                Debug.Log("[AD] Pump 跳过：播放中");
                return;
            }

            foreach (var kv in _preloadInfos)
            {
                var info = kv.Value;
                if (info.pendingShow && !info.isLoading && info.readyAds.Count == 0)
                {
                    if (CanSubmitNow(info))
                    {
                        Debug.Log($"[AD] Pump 优先点播：{info.adId}");
                        DoPreload(info);
                    }
                }
            }

            foreach (var kv in _preloadInfos)
            {
                var info = kv.Value;
                int maxpool = MAX_POOL_SIZE;
                if (info.adId == REWARD_AD_ID)
                {
                    maxpool = 1;
                }

                if (info.wantPreload
                    && !info.isLoading
                    && GetPoolCount(info) < maxpool)
                {
                    if (CanSubmitNow(info))
                    {
                        Debug.Log($"[AD] Pump 预加载：{info.adId}，当前池={GetPoolCount(info)}/{maxpool}");
                        DoPreload(info);
                    }
                }
            }
        }

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
            _submitQueue.Enqueue(info);

            int maxpool = MAX_POOL_SIZE;
            if (info.adId == REWARD_AD_ID)
            {
                maxpool = 1;
            }

            int adWidth = 720;
            int adHeight = 1280;

            if (info.adType == AdType.Reward || info.adType == AdType.Interstitial)
            {
                adWidth = 720;
                adHeight = 1280;
            }

            var adRequestParams = new AdRequestParams()
            {
                adType = (int)info.adType,
                adId = info.adId,
                oaid = _uniqueId,
                isPreload = true,
                adWidth = adWidth,
                adHeight = adHeight
            };
            var adOptions = new AdOptions();

            Debug.Log($"[AD] 发起预加载 {info.adId}，尺寸={adWidth}x{adHeight}，队列长度={_submitQueue.Count}，当前池={GetPoolCount(info)}/{maxpool}");
            OHSDKKitManager.Instance.LoadAds(adRequestParams, adOptions);
        }

        private void OnPreloadFinished(AdPreloadInfo info, Advertisement ad, bool isNoFill)
        {
            info.isLoading = false;

            int maxpool = MAX_POOL_SIZE;
            if (info.adId == REWARD_AD_ID)
            {
                maxpool = 1;
            }

            if (ad != null)
            {
                info.retryCount = 0;

                if (info.pendingShow)
                {
                    Debug.Log($"[AD] 点播请求完成，立即展示：{info.adId}");
                    ShowInternalFromSlot(info, ad);
                    return;
                }

                info.readyAds.Add(new CachedAd { ad = ad, loadTime = DateTime.Now });

                Debug.Log($"[AD] 预加载入池：{info.adId}，池大小={info.readyAds.Count}/{maxpool}");

                if (GetPoolCount(info) < maxpool)
                {
                    info.wantPreload = true;
                }
                PumpPreload();
                return;
            }

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
                    if (info.isLoading) return;
                    if (GetPoolCount(info) >= maxpool) return;
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

        private AdPreloadInfo DequeueForReturnedAd(Advertisement ad)
        {
            var adType = (AdType)ad.adType;

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
            if (UIUtilities.isEditMode)
            {
                callback?.Invoke(true);
                return;
            }
         
            ShowRewardInternal(key, TIP_TOOL_REWARD_AD_ID, callback);
        }

        public void ShowReward(Define.AdKey key, Action<bool> callback)
        {
            ShowRewardInternal(key, REWARD_AD_ID, callback);
        }

        private void ShowRewardInternal(Define.AdKey key, string adId, Action<bool> callback)
        {
            CreateAdsObj();

            // 闸门关着（广告追踪授权还没走完）时直接回调失败，避免转圈卡死
            if (!AdRequestsAllowed)
            {
                Debug.LogWarning("[AD] 广告追踪授权未完成，激励视频暂不可用");
                MessageSystem.Instance?.ShowTip("广告资源准备中，请稍后再试");
                callback?.Invoke(false);
                return;
            }

            Debug.Log($"[AD] 点击视频点：{adId}");

            _currentAdKey = key;
            _completeCallback = callback;
            _adType = AdType.Reward;
            _isGetRewarded = false;
            _lastShowRewardAdsTime = DateTime.Now;

            var info = GetOrCreateInfo(adId, AdType.Reward);
            _currentSlot = info;

            if (info.readyAds.Count > 0)
            {
                var cached = info.readyAds[0];
                // 检查是否过期
                if ((DateTime.Now - cached.loadTime).TotalMinutes > AD_CACHE_EXPIRE_MINUTES)
                {
                    Debug.LogWarning($"[AD] 预加载广告已过期，丢弃并重新加载：{adId}");
                    info.readyAds.RemoveAt(0);
                    info.wantPreload = true;
                    // 走点播等待流程
                    CancelRetry(info);
                    info.pendingShow = true;
                    info.pendingKey = key;
                    info.pendingCallback = callback;
                    info.wantPreload = true;
                    MessageSystem.Instance.ShowLoadingAnimation();
                    PumpPreload();
                    return;
                }

                info.readyAds.RemoveAt(0);

                IsPlaying = true;
                _isNeedShow_Field = true;
                Debug.Log($"[AD] 使用预加载广告（命中池）：{adId}，剩余池={info.readyAds.Count}");

                DisplayAd(cached.ad);
                return;
            }

            CancelRetry(info);

            info.pendingShow = true;
            info.pendingKey = key;
            info.pendingCallback = callback;
            info.wantPreload = true;

            MessageSystem.Instance.ShowLoadingAnimation();

            Debug.Log($"[AD] 点播等待：{adId}，本槽在途={info.isLoading}");
            PumpPreload();
        }

        public void ShowInterstitial(Action<bool> callback)
        {
            
            if (UIUtilities.isEditMode)
            {
                callback?.Invoke(true);
                return;
            }
            
            
            CreateAdsObj();

            if (!AdRequestsAllowed)
            {
                Debug.LogWarning("[AD] 广告追踪授权未完成，插屏暂不可用");
                callback?.Invoke(false);
                return;
            }

            _completeCallback = callback;
            _adType = AdType.Interstitial;
            _isNeedShow_Field = true;
            _isGetRewarded = false;

            var info = GetOrCreateInfo(INTERSTITIAL_AD_ID, AdType.Interstitial);
            _currentSlot = info;

            if (info.readyAds.Count > 0)
            {
                var cached = info.readyAds[0];
                if ((DateTime.Now - cached.loadTime).TotalMinutes > AD_CACHE_EXPIRE_MINUTES)
                {
                    Debug.LogWarning($"[AD] 插屏预加载广告已过期，丢弃：{INTERSTITIAL_AD_ID}");
                    info.readyAds.RemoveAt(0);
                    info.wantPreload = true;
                    PumpPreload();
                    // 继续走点播等待
                }
                else
                {
                    info.readyAds.RemoveAt(0);
                    IsPlaying = true;
                    Debug.Log($"[AD] 使用预加载插屏广告，剩余池={info.readyAds.Count}");
                    DisplayAd(cached.ad);
                    return;
                }
            }

            CancelRetry(info);

            info.pendingShow = true;
            info.pendingCallback = callback;
            info.wantPreload = true;

            PumpPreload();
        }

        /// <summary>
        /// 鸿蒙横幅：ArkTS 侧 SDKManager.LoadBanner 现在会「取 OAID → 拉起横幅弹窗 → 请求广告」，
        /// 也就是调用 LoadBannerAD 就等于展示横幅（不再有独立的 ShowBanner 桥接）。
        /// </summary>
        public void LoadBannerAD()
        {
            CreateAdsObj();

            // 授权流程没走完之前不请求广告：AdsKit 取 OAID 时未授权会触发系统跟踪授权框，
            // 会把弹窗抢在"广告追踪引导页点继续"之前弹出来。
            if (!AdRequestsAllowed)
            {
                _bannerPending = true;
                Debug.Log("[AD][Banner] 等待广告追踪授权，暂缓横幅");
                return;
            }

            // 授权通过后 OAID 才会写进 oaid.txt，这里每次都取最新的，
            // 保证"先拒绝 → 后来在设置里开启"之后横幅能带上 OAID 出价
            string latestOaid = Game.self != null ? Game.self.GetOAID() : null;
            if (!string.IsNullOrEmpty(latestOaid)) _uniqueId = latestOaid;
           
            var adRequestParams = new AdRequestParams()
            {
                // 必须传鸿蒙原生横幅类型 8（不是 C# 内部 AdType.Banner=3）
                adType = HARMONY_AD_TYPE_BANNER,
                adId = BANNER_AD_ID,
                oaid = _uniqueId ?? string.Empty,
                adWidth = BANNER_WIDTH_VP,
                adHeight = BANNER_HEIGHT_VP
            };
            var adOptions = new AdOptions();
            var adDisplayOptions = new AdDisplayOptions() { refreshTime = BANNER_REFRESH_TIME };

            Debug.Log($"[AD][Banner] 准备横幅参数 adId={BANNER_AD_ID}, " +
                      $"oaid={(string.IsNullOrEmpty(_uniqueId) ? "<空>" : "已就绪")}, 尺寸={BANNER_WIDTH_VP}x{BANNER_HEIGHT_VP}");

            // 加载即展示：ArkTS 侧 SDKManager.LoadBanner 会先取 OAID，
            // 再 ShowAdsWindow 拉起横幅弹窗，并把参数同步给 BannerAd 组件发起请求
            OHSDKKitManager.Instance.LoadBanner(adRequestParams, adOptions, adDisplayOptions);
           
            _bannerPreparedOaid = _uniqueId ?? string.Empty;
        }

        public void ShowBanner()
        {
            // 广告追踪流程没走完：先记账，等 Game.AllowAdRequests() 后由闸门补展示
            if (!AdRequestsAllowed)
            {
                _bannerShowPending = true;
                Debug.Log("[AD][Banner] 等待广告追踪授权，暂缓展示横幅");
                return;
            }

            if (_bannerShowing) return;

            // 拉取失败后的冷却：横幅失败会由 ArkTS 侧自动关掉对话框，
            // 这里不挡住的话每进一关都会重开一次原生对话框（真机上就是反复 open/close + 报错）
            if (DateTime.Now < _bannerRetryAt)
            {
                Debug.Log($"[AD][Banner] 失败冷却中，跳过展示（剩余 {(int)(_bannerRetryAt - DateTime.Now).TotalSeconds}s）");
                return;
            }

            // 加载即展示：ArkTS 的 LoadBanner 内部会 ShowAdsWindow + 让 BannerAd 发起请求
            LoadBannerAD();

            _bannerShowing = true;
            _bannerShowPending = false;
            _bannerPending = false;
            Debug.Log($"[AD][Banner] 展示横幅：{BANNER_AD_ID}");
        }

        public void HideBanner()
        {
            _bannerShowPending = false;

            if (!_bannerShowing)
            {
                // 没有展示就不必关，避免对未打开的对话框调 close（ArkTS 侧会报错日志）
                return;
            }

            _bannerShowing = false;
            CallAdsBridge("HideBannerOnUI");
            Debug.Log("[AD][Banner] 关闭横幅");
        }

        /// <summary>
        /// 调 ArkTS 侧桥接方法（HideBannerOnUI）。
        /// 该方法由 AppBuilder_HARMONY.PatchOHKitAdsManager 在导出时注入 OHKitAdsManager.ets，
        /// 若没重新导出工程，真机会打"没有该方法"的警告。
        /// </summary>
        private static void CallAdsBridge(string method)
        {
            try
            {
                OpenHarmonyJSObject adsObj = OHSDKKitManager.OHKitAdsManagerObj;
                if (adsObj == null)
                {
                    var _ = OHSDKKitManager.Instance;   // 触发一次初始化拿实例
                    adsObj = OHSDKKitManager.OHKitAdsManagerObj;
                }

                if (adsObj == null)
                {
                    Debug.LogWarning($"[AD][Banner] OHKitAdsManager 未就绪，跳过 {method}");
                    return;
                }

                if (_adsBridgeCallback == null)
                    _adsBridgeCallback = new OpenHarmonyJSCallback(OnAdsBridgeCallback);

                adsObj.Call(method, _adsBridgeCallback);
                Debug.Log($"[AD][Banner] 已调用 ArkTS 桥接：{method}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AD][Banner] 调用 {method} 失败：{e.Message}" +
                                 "（ArkTS 侧桥接方法未注入？需要重新导出鸿蒙工程）");
            }
        }

        private static object OnAdsBridgeCallback(params OpenHarmonyJSObject[] args)
        {
            return true;
        }
    
        /// <summary>播全屏广告前先收起横幅（横幅是原生浮层，会盖在全屏广告上面）</summary>
        private void HideBannerForFullScreenAd()
        {
            if (!_bannerShowing) return;

            _bannerHiddenByFullScreenAd = true;
            HideBanner();
        }

        /// <summary>全屏广告结束后恢复之前被收起的横幅</summary>
        private void RestoreBannerAfterFullScreenAd()
        {
            if (!_bannerHiddenByFullScreenAd) return;

            _bannerHiddenByFullScreenAd = false;
            ShowBanner();
        }

        #endregion

        #region 通用逻辑

        private void DisplayAd(Advertisement ad)
        {
            if (!_isNeedShow_Field) return;

            Debug.Log("[AD]展示广告: " + (AdType)ad.adType);

            // 重置展示状态跟踪
            _adShowStartTime = DateTime.Now;
            _adOpenReceived = false;
            _adVideoStartReceived = false;
            _adShowTimeoutTriggered = false;

            // 横幅是原生浮层，会盖在全屏广告上方：播全屏广告前先收起，结束后恢复
            HideBannerForFullScreenAd();

            var adDisplayOptions = new AdDisplayOptions();

            OHSDKKitManager.Instance.ShowAds(ad, adDisplayOptions);

            // 启动展示超时兜底
            UnityTimer.Delay(SHOW_TIMEOUT_SECONDS, () =>
            {
                if (IsPlaying && !_adShowTimeoutTriggered)
                {
                    _adShowTimeoutTriggered = true;
                    Debug.LogError($"[AD] 展示超时 {SHOW_TIMEOUT_SECONDS}s，强制回调 false");
                    CallbackAd(false);
                }
            });

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
            _adShowTimeoutTriggered = false;

            if (_completeCallback != null)
            {
                var tempCallback = _completeCallback;
                _completeCallback = null;
                try { tempCallback.Invoke(success); }
                catch (Exception e) { Debug.LogError($"[AD] 业务回调报错: {e.Message}"); }
            }

            _currentSlot = null;

            // 全屏广告结束，恢复之前被收起的横幅
            RestoreBannerAfterFullScreenAd();

            if (endedType == AdType.Reward)
            {
                UnityTimer.Delay(0.5f, () =>
                {
                    foreach (var kv in _preloadInfos)
                    {
                        var info = kv.Value;
                        if (info.adType == AdType.Reward && !info.isLoading)
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
            Debug.Log("[AD] 玩家手动关闭激励视频且未获得奖励，回调 false");
            //CallbackAd(false);
        }

        private void OnLoadAdsTrigger(SignalBase signal)
        {
            bool hasError = signal.hasError();
            string msg = signal.message;

            // 横幅参数同步的回执：ArkTS LoadBanner 返回的是 {"code":0,"message":"load Banner advertising success"}，
            // 会以 AdsLoadSignal 的形式回到这里。它不是真正的广告加载结果，
            // 不挡掉会被当成"加载失败"，进而误回调正在播放的全屏广告（CallbackAd(false)）。
            if (!string.IsNullOrEmpty(msg) && msg.Contains("Banner advertising success"))
            {
                Debug.Log("[AD][Banner] 收到横幅参数同步回执，忽略");
                return;
            }

            if (!hasError)
            {
                var targetSignal = (AdsLoadSignal)signal;
                var ad = (targetSignal.ads != null && targetSignal.ads.Count > 0)
                    ? targetSignal.ads[0] : null;

                if (ad != null)
                {
                    Debug.Log($"[AD] [OnLoadAdsTrigger] type={(AdType)ad.adType}, uniqueId={ad.uniqueId}");

                    var matched = DequeueForReturnedAd(ad);

                    if (matched != null)
                    {
                        OnPreloadFinished(matched, ad, false);
                        return;
                    }

                    bool userWaiting = _isNeedShow_Field
                                       && _completeCallback != null
                                       && _currentSlot != null
                                       && _currentSlot.pendingShow
                                       && (AdType)ad.adType == _currentSlot.adType;

                    if (userWaiting)
                    {
                        Debug.LogWarning($"[AD] 无匹配但有点播等待，兜底展示：{_currentSlot.adId}");
                        ShowInternalFromSlot(_currentSlot, ad);
                        return;
                    }

                    Debug.LogWarning("[AD] 无匹配在途槽位，忽略该加载信号（不播放）");
                    return;
                }
            }

            Debug.Log($"[OnLoadAdsTrigger] 加载失败，code={signal.code}, msg={msg}");

            var failSlot = DequeueForFailedAd();
            if (failSlot != null)
            {
                OnPreloadFinished(failSlot, null, IsNoFillMessage(msg));
                return;
            }

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

                bool isOpen = statusLower.Contains("open");
                bool isClose = statusLower.Contains("close");
                bool isFail = statusLower.Contains("fail");
                bool isReward = statusLower.Contains("reward") || statusLower.Contains("videoplayend");
                bool isVideoStart = statusLower.Contains("videoplaybegin") || statusLower.Contains("videostart");

                // 横幅等"非全屏"广告也会走同一个状态通道（onAdOpen / onAdClose / onAdFail）。
                // 没有全屏广告在播时，这些状态只同步横幅记账，不参与全屏广告的回调/发奖逻辑，
                // 否则横幅关闭会被误判成激励视频关闭，导致错误发奖或错误回调。
                if (!IsPlaying && !_isNeedShow_Field)
                {
                    if (isClose || isFail)
                    {
                        // 横幅自己关掉了（用户点关闭 / 拉取失败）：C# 侧记账同步复位，
                        // 否则后续 ShowBanner 会一直以为"已经在展示"而不再拉起
                        _bannerShowing = false;

                        if (isFail)
                        {
                            // 无填充/参数不匹配时不要每关重试，冷却 3s 再试
                            _bannerRetryAt = DateTime.Now.AddSeconds(3);
                            Debug.LogWarning($"[AD][Banner] 横幅拉取失败，3s 内不再尝试（{currentStatus}）");
                        }
                        else
                        {
                            Debug.Log("[AD][Banner] 横幅已关闭（底层状态同步）");
                        }
                    }
                    else
                    {
                        Debug.Log($"[AD] 忽略非全屏广告状态（横幅）: {currentStatus}");
                    }
                    return;
                }

                if (!signal.hasError())
                {
                    if (isOpen)
                    {
                        _adOpenReceived = true;
                        Debug.Log("[AD] 广告已打开 onAdOpen");
                    }

                    if (isVideoStart)
                    {
                        _adVideoStartReceived = true;
                        Debug.Log("[AD] 广告视频开始播放 onVideoPlayBegin");
                    }

                    if (isReward)
                    {
                        if (_adType == AdType.Reward)
                        {
                            _isGetRewarded = true;
                            Debug.Log("[AD] 激励视频达标，标记可发奖");
                        }
                    }

                    if (isClose || isFail)
                    {
                        Game.self.ResumeGame();
                        IsPlaying = false;
                        if (MessageSystem.Instance != null)
                            MessageSystem.Instance.HideLoadingAnimation();
                    }

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
                                // 判断是否为异常关闭
                                var duration = (DateTime.Now - _adShowStartTime).TotalSeconds;
                                bool isAbnormal = !_adOpenReceived || !_adVideoStartReceived || duration < 2.0;
                                if (isAbnormal)
                                {
                                    Debug.LogWarning($"[AD] 检测到异常关闭（未打开/未播放/时长过短 duration={duration:F2}s），回调 false");
                                    CallbackAd(false);
                                }
                                else
                                {
                                    HandleManualCloseNoReward();
                                }
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
                CallbackAd(false);
            }
        }

        #endregion
    }
}
#endif