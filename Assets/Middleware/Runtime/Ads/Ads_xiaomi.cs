#if UNITY_ANDROID
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Middleware
{
    public enum AdType
    {
        Reward, Interstitial, Banner
    }

    public class Ads_xiaomi : IAds
    {
        public bool IsPlaying { get; set; }
        public bool isLoadReady;

        // ==================== 广告位配置（替换为米盟后台真实值） ====================
        private const string AppId = "2882303761520479149";
        private const string AppName = "成语消：禅意之境";
        private const string RewardVideoUpId = "37451a3154da27d35AAF306F45725d6b";
        private const string InterstitialUpId = "65f00239e05fab8ae4d4c00df2662dff";
        private const string BannerUpId = "F51D5BD0217B3A23883AFBB19D4E91F7";
        // ======================================================================

        private string _uniqueId;
        private Define.AdKey _currentAdKey;
        private Action<bool> _completeCallback;
        private AdType _adType;

        // 等待展示的请求
        private bool _isNeedShow = false;
        // 正在展示中，用于防重入
        private bool _isShowing = false;
        private bool _isRewarded = false;

        private readonly float _preloadInterval = 30f;
        private DateTime _lastPreloadTime = DateTime.MinValue;

        // ★ 按广告类型区分"已预加载成功"
        private readonly Dictionary<AdType, bool> _preloadedAds = new Dictionary<AdType, bool>();
        // ★ 按广告类型区分"正在加载中"（替代原来的全局 _isPreloading）
        private readonly HashSet<AdType> _loadingTypes = new HashSet<AdType>();

        private AndroidJavaObject _bridge;
        private MimoCallbackReceiver _receiver;

        public void Init(float delay)
        {
            var go = new GameObject("MimoAdManager");
            _receiver = go.AddComponent<MimoCallbackReceiver>();
            _receiver.owner = this;
            UnityEngine.Object.DontDestroyOnLoad(go);

            try
            {
                AndroidJavaClass cls = new AndroidJavaClass(
                    "com.liufangzhiwu.chengyuxiao.mimo.MimoBridge");
                _bridge = cls.CallStatic<AndroidJavaObject>("getInstance");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AD]获取 MimoBridge 失败：{e}");
                _bridge = null;
            }

            UnityTimer.Delay(delay, () =>
            {
                _uniqueId = Game.self.GetOAID();
                if (_bridge != null)
                    _bridge.Call("init", AppId, AppName);
            });
        }

        public bool IsReady(Define.AdKey key)
        {
            return isLoadReady;
        }

        // ==================== 激励视频 ====================
        public void ShowReward(Define.AdKey key, Action<bool> callback)
        {
            if (_isShowing)
            {
                Debug.LogWarning("[AD]已有广告正在展示中，忽略本次激励视频请求");
                return;
            }

            _currentAdKey = key;
            _completeCallback = callback;
            _adType = AdType.Reward;
            _isNeedShow = true;
            _isRewarded = false;

            // 优先用预加载
            if (HasPreloadedAd(AdType.Reward))
            {
                Debug.Log("[AD]使用预加载的激励视频广告");
                _preloadedAds[AdType.Reward] = false;
                ShowRewardInternal();
                return;
            }

            // 已在加载中，等待回调即可
            if (_loadingTypes.Contains(AdType.Reward)) return;

            if (_bridge == null)
            {
                _isNeedShow = false;
                _completeCallback = null;
                callback?.Invoke(false);
                return;
            }

            MessageSystem.Instance.ShowLoadingAnimation();
            _loadingTypes.Add(AdType.Reward);
            _bridge.Call("loadRewardVideo", RewardVideoUpId);
        }

        private void ShowRewardInternal()
        {
            // ★ 关键：展示后立刻清空待展示标记，防止 Loaded 回调二次触发
            _isNeedShow = false;
            _isShowing = true;

            string desc = "";
            switch (_currentAdKey)
            {
                case Define.AdKey.RewardAdIdStoreGold:   desc = "奖励广告-商店金币";  break;
                case Define.AdKey.RewardAdIdItemGold:    desc = "奖励广告-物品金币";  break;
                case Define.AdKey.RewardAdIdCheckinGold1:desc = "奖励广告-签到金币1"; break;
                case Define.AdKey.RewardAdIdCheckinGold2:desc = "奖励广告-签到金币2"; break;
                case Define.AdKey.RewardAdIdCheckinGold3:desc = "奖励广告-签到金币3"; break;
            }
            AnalyticMgr.VideoStart(desc);

            if (_bridge != null)
                _bridge.Call("showRewardVideo");

            // 展示后异步补一次预加载，不影响当前流程
            UnityTimer.Delay(1f, PreloadRewardVideo);
        }

        // ==================== 插屏 ====================
        public void ShowInterstitial(Action<bool> callback)
        {
            if (_isShowing)
            {
                Debug.LogWarning("[AD]已有广告正在展示中，忽略本次插屏请求");
                return;
            }

            _completeCallback = callback;
            _adType = AdType.Interstitial;
            _isNeedShow = true;

            if (HasPreloadedAd(AdType.Interstitial))
            {
                Debug.Log("[AD]使用预加载的插屏广告");
                _preloadedAds[AdType.Interstitial] = false;
                ShowInterstitialInternal();
                return;
            }

            if (_loadingTypes.Contains(AdType.Interstitial)) return;

            if (_bridge == null)
            {
                _isNeedShow = false;
                _completeCallback = null;
                callback?.Invoke(false);
                return;
            }

            _loadingTypes.Add(AdType.Interstitial);
            _bridge.Call("loadInterstitial", InterstitialUpId);
        }

        private void ShowInterstitialInternal()
        {
            _isNeedShow = false;
            _isShowing = true;

            if (_bridge != null)
                _bridge.Call("showInterstitial");

            UnityTimer.Delay(1f, PreloadInterstitial);
        }

        // ==================== Banner ====================
        public void LoadBannerAD()
        {
            if (_bridge != null) _bridge.Call("loadBanner", BannerUpId);
        }

        public void ShowBanner()
        {
            _adType = AdType.Banner;
            _isNeedShow = true;
            if (_bridge != null) _bridge.Call("showBanner");
        }

        public void HideBanner()
        {
            if (_bridge != null) _bridge.Call("destroyBanner");
        }

        #region 预加载逻辑
        public void PreloadAds()
        {
            // ★ 不再清 _isNeedShow
            if ((DateTime.Now - _lastPreloadTime).TotalSeconds < _preloadInterval) return;
            _lastPreloadTime = DateTime.Now;

            Debug.Log("[AD]开始预加载广告");
            PreloadRewardVideo();
            PreloadInterstitial();
        }

        private void PreloadRewardVideo()
        {
            if (_bridge == null) return;
            if (HasPreloadedAd(AdType.Reward)) return;
            if (_loadingTypes.Contains(AdType.Reward)) return;
            // 用户正在等激励视频展示，不要重复加载
            if (_isNeedShow && _adType == AdType.Reward) return;

            Debug.Log("[AD]预加载激励视频广告");
            _loadingTypes.Add(AdType.Reward);
            _bridge.Call("loadRewardVideo", RewardVideoUpId);
        }

        private void PreloadInterstitial()
        {
            if (_bridge == null) return;
            if (HasPreloadedAd(AdType.Interstitial)) return;
            if (_loadingTypes.Contains(AdType.Interstitial)) return;
            if (_isNeedShow && _adType == AdType.Interstitial) return;

            Debug.Log("[AD]预加载插屏广告");
            _loadingTypes.Add(AdType.Interstitial);
            _bridge.Call("loadInterstitial", InterstitialUpId);
        }

        public bool HasPreloadedAd(AdType adType)
        {
            return _preloadedAds.TryGetValue(adType, out var v) && v;
        }

        public void ForcePreloadAds()
        {
            _lastPreloadTime = DateTime.MinValue;
            PreloadAds();
        }
        #endregion

        #region 通用逻辑
        /// <summary>
        /// 展示流程统一收口：重置所有状态 + 回调 + 成功时补预加载
        /// </summary>
        private void CallbackAd(bool success)
        {
            _isShowing = false;
            _isNeedShow = false;
            _isRewarded = false;

            var cb = _completeCallback;
            _completeCallback = null;
            cb?.Invoke(success);

            if (success)
            {
                UnityTimer.Delay(2f, PreloadAds);
            }
        }
        #endregion

        // ==================== 供 MimoCallbackReceiver 调用的内部方法 ====================
        internal void HandleSdkInitSuccess()
        {
            Debug.Log("[AD]米盟 SDK 初始化成功");
        }

        internal void HandleSdkInitFailed(int code)
        {
            Debug.LogError($"[AD]米盟 SDK 初始化失败：{code}");
        }

        // ---------- 激励视频 ----------
        internal void HandleRewardVideoLoaded()
        {
            Debug.Log("[AD]激励视频加载成功");
            _loadingTypes.Remove(AdType.Reward);

            // ★ 有等待展示的请求，展示；否则缓存为预加载成功
            if (_isNeedShow && _adType == AdType.Reward && !_isShowing)
            {
                MessageSystem.Instance.HideLoadingAnimation();
                ShowRewardInternal();
                return;
            }

            _preloadedAds[AdType.Reward] = true;
        }

        internal void HandleRewardVideoLoadFailed(string error)
        {
            Debug.LogError($"[AD]激励视频加载失败：{error}");
            _loadingTypes.Remove(AdType.Reward);
            _preloadedAds[AdType.Reward] = false;

            if (_isNeedShow && _adType == AdType.Reward)
            {
                MessageSystem.Instance.HideLoadingAnimation();
                CallbackAd(false);
            }
        }

        internal void HandleReward()
        {
            _isRewarded = true;
        }

        internal void HandleRewardVideoClosed()
        {
            if (_adType == AdType.Reward)
            {
                CallbackAd(_isRewarded);
                Game.self.ResumeGame();
            }
        }

        internal void HandleRewardVideoError(string error)
        {
            Debug.LogError($"[AD]激励视频错误：{error}");
            if (_adType == AdType.Reward)
            {
                CallbackAd(false);
                Game.self.ResumeGame();
            }
        }

        internal void HandleRewardVideoShown()
        {
            Debug.Log("[AD]激励视频展示");
            Game.self.PauseGame();
        }

        // ---------- 插屏 ----------
        internal void HandleInterstitialLoaded()
        {
            Debug.Log("[AD]插屏加载成功");
            _loadingTypes.Remove(AdType.Interstitial);

            if (_isNeedShow && _adType == AdType.Interstitial && !_isShowing)
            {
                ShowInterstitialInternal();
                return;
            }

            _preloadedAds[AdType.Interstitial] = true;
        }

        internal void HandleInterstitialLoadFailed(string error)
        {
            Debug.LogError($"[AD]插屏加载失败：{error}");
            _loadingTypes.Remove(AdType.Interstitial);
            _preloadedAds[AdType.Interstitial] = false;

            if (_isNeedShow && _adType == AdType.Interstitial)
            {
                CallbackAd(false);
            }
        }

        internal void HandleInterstitialShown()
        {
            Debug.Log("[AD]插屏展示");
            Game.self.PauseGame();
        }

        internal void HandleInterstitialClosed()
        {
            if (_adType == AdType.Interstitial)
            {
                CallbackAd(true);
                Game.self.ResumeGame();
            }
        }

        internal void HandleInterstitialRenderFail(string error)
        {
            Debug.LogError($"[AD]插屏渲染失败：{error}");
            if (_adType == AdType.Interstitial)
            {
                CallbackAd(false);
                Game.self.ResumeGame();
            }
        }

        // ---------- Banner ----------
        internal void HandleBannerLoaded()
        {
            Debug.Log("[AD]Banner 加载成功");
            if (_isNeedShow && _adType == AdType.Banner)
            {
                if (_bridge != null) _bridge.Call("showBanner");
            }
        }

        internal void HandleBannerLoadFailed(string error)
        {
            Debug.LogError($"[AD]Banner 加载失败：{error}");
        }

        internal void HandleBannerShown()     { Debug.Log("[AD]Banner 展示"); }
        internal void HandleBannerClicked()   { Debug.Log("[AD]Banner 点击"); }
        internal void HandleBannerDismissed() { Debug.Log("[AD]Banner 消失"); }
        internal void HandleBannerRenderFail(string error) { Debug.LogError($"[AD]Banner 渲染失败：{error}"); }

        // ==================== UnitySendMessage 回调接收器 ====================
        public class MimoCallbackReceiver : MonoBehaviour
        {
            public Ads_xiaomi owner;

            // ---------- 初始化 ----------
            public void OnSdkInitSuccess(string msg) { owner?.HandleSdkInitSuccess(); }
            public void OnSdkInitFailed(string code) { owner?.HandleSdkInitFailed(int.Parse(code)); }

            // ---------- Banner ----------
            public void OnBannerLoaded(string msg) { owner?.HandleBannerLoaded(); }
            public void OnBannerLoadFailed(string error) { owner?.HandleBannerLoadFailed(error); }
            public void OnBannerShow(string msg) { owner?.HandleBannerShown(); }
            public void OnBannerClicked(string msg) { owner?.HandleBannerClicked(); }
            public void OnBannerDismissed(string msg) { owner?.HandleBannerDismissed(); }
            public void OnBannerRenderFail(string error) { owner?.HandleBannerRenderFail(error); }
            public void OnBannerDestroyed(string msg) { Debug.Log("[AD]Banner 销毁"); }

            // ---------- 激励视频 ----------
            public void OnRewardVideoLoaded(string msg) { owner?.HandleRewardVideoLoaded(); }
            public void OnRewardVideoLoadFailed(string error) { owner?.HandleRewardVideoLoadFailed(error); }
            public void OnRewardVideoShown(string msg) { owner?.HandleRewardVideoShown(); }
            public void OnRewardVideoClicked(string msg) { Debug.Log("[AD]激励视频点击"); }
            public void OnRewardVideoError(string error) { owner?.HandleRewardVideoError(error); }
            public void OnRewardVideoComplete(string msg) { Debug.Log("[AD]激励视频播放完成"); }
            public void OnRewardVideoClosed(string msg) { owner?.HandleRewardVideoClosed(); }
            public void OnReward(string msg) { owner?.HandleReward(); }
            public void OnRewardVideoSkipped(string msg) { Debug.Log("[AD]激励视频跳过"); }

            // ---------- 插屏 ----------
            public void OnInterstitialLoaded(string msg) { owner?.HandleInterstitialLoaded(); }
            public void OnInterstitialLoadFailed(string error) { owner?.HandleInterstitialLoadFailed(error); }
            public void OnInterstitialShowFailed(string error) { owner?.HandleInterstitialLoadFailed(error); }
            public void OnInterstitialShown(string msg) { owner?.HandleInterstitialShown(); }
            public void OnInterstitialClicked(string msg) { Debug.Log("[AD]插屏点击"); }
            public void OnInterstitialVideoComplete(string msg) { Debug.Log("[AD]插屏视频完成"); }
            public void OnInterstitialClosed(string msg) { owner?.HandleInterstitialClosed(); }
            public void OnInterstitialVideoSkipped(string msg) { Debug.Log("[AD]插屏视频跳过"); }
            public void OnInterstitialRenderFail(string error) { owner?.HandleInterstitialRenderFail(error); }
            public void OnInterstitialDestroyed(string msg) { Debug.Log("[AD]插屏销毁"); }
        }
    }
}
#endif