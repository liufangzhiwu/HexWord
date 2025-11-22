#if UNITY_IOS
using System;
using System.Collections.Generic;
using UnityEngine;
using GoogleMobileAds.Api;
namespace Middleware
{
    public class Ads_ios : IAds
    {
        public bool IsPlaying { get; set; }

        public void Init(float delay)
        {
            UnityTimer.Delay(delay, () =>
            {
                SetTestDeviceIds();
                InitializeGoogleMobileAds();
            });
        }

        public bool IsReady(Define.AdKey key)
        {
            return _rewardedAds.ContainsKey(key) && _rewardedAds[key].CanShowAd();
        }

        public void ShowReward(Define.AdKey key, Action<bool> callback)
        {
#if UNITY_EDITOR
            callback(true);
            return;
#endif
            AdCompletedCallBackR = callback;
            var ad = _rewardedAds[key];
            if (ad != null)
            {
                if (ad.CanShowAd())
                {
                    ad.Show(reward =>
                    {
                        AdCompletedCallBackR?.Invoke(true);
                        //DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedSeeAds,1); 逻辑放外面
                    });
                }
                else
                {
                    Debug.LogWarning($"{key}The advertisement is not ready and is being reloaded...");
                    AdCompletedCallBackR?.Invoke(false);
                    HandleAdLoadFailureR(key, "The advertisement is not ready and is being reloaded");
                }
            }
            else
            {
                Debug.LogError($"No advertising unit found: {key}");
                AdCompletedCallBackR?.Invoke(false);
                HandleAdLoadFailureR(key, "No advertising unit found");
            }
        }

        public void ShowInterstitial(Action<bool> callback)
        {
            _adCompletedCallBackI = callback;
            if (_interstitialAd != null && _interstitialAd.CanShowAd())
            {
                Debug.Log("Interstitial ads are being displayed.");
                _interstitialAd.Show();
            }
            else
            {
                Debug.LogError("Interstitial advertisements are not ready yet.");
                HandleAdLoadFailureI();
                _adCompletedCallBackI?.Invoke(false);
            }
        }

        public void ShowBanner()
        {
            Debug.Log("Show the banner AD view.");
            _isShowAdB = true;
            _bannerView?.Show();
            AutoRefreshB();
        }

        public void HideBanner()
        {
            _isShowAdB = false;
            _bannerView?.Hide();
            UnityTimer.Kill(_autoRefreshTimer);
            _autoRefreshTimer = 0;
        }

        
        #region 通用逻辑
        private string GetAdId(Define.AdKey key)
        {
            var adId = "";
#if Unity_Release
            adId = ConfigManager.Instance.GetString(key.ToString());
#else
            switch (key)
            {
                case Define.AdKey.BannerAdUnitId:
                    adId = Define.ConfigIOS.TestBannerAdId;
                    break;
                case Define.AdKey.InterstitialAdId:
                    adId = Define.ConfigIOS.TestInterstitialAdId;
                    break;
                default:
                    adId = Define.ConfigIOS.TestRewardAdId;
                    break;
            }
#endif
            return adId;
        }

        private void SetTestDeviceIds()
        {
            var requestConfiguration = new RequestConfiguration();
            MobileAds.SetRequestConfiguration(requestConfiguration);
        }

        private void InitializeGoogleMobileAds()
        {
            Debug.Log("Google Mobile Ads initialization");
            try
            {
                MobileAds.Initialize(initStatus =>
                {
                    if (initStatus == null)
                    {
                        Debug.LogError("Google Mobile Ads initialization failed.");
                        return;
                    }

                    Debug.Log("Google Mobile Ads SDK has been completed!");
                    // 初始化完成后加载广告
                    LoadAllAds();
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"AdMob initialization failed: {e.Message}\n{e.StackTrace}");
            }
        }

        private void LoadAllAds()
        {
            LoadInterstitialAd();
            LoadRewardedAds();
            LoadBannerAd();
            HideBanner();
        }


        #endregion

        #region 插屏广告

        private InterstitialAd _interstitialAd; // 插屏广告实例
        private Action<bool> _adCompletedCallBackI; //插屏关闭回调
        private int _retryCountI;
        private long _retryTimerI;
        private const int MaxRetriesI = 3; // 最大重试次数
        private const float RetryDelayI = 2f; // 重试延迟（秒）

        private void LoadInterstitialAd()
        {
            _retryTimerI = 0;
            _interstitialAd?.Destroy();

            var adRequest = new AdRequest();
            InterstitialAd.Load(GetAdId(Define.AdKey.InterstitialAdId), adRequest, (ad, error) =>
            {
                // 如果加载失败并返回错误信息。
                if (error != null)
                {
                    HandleAdLoadFailureI();
                    return;
                }

                // 如果加载失败但未返回错误信息（未知错误）。
                if (ad == null)
                {
                    Debug.LogError(
                        "Unexpected error: The interstitial AD loading event was triggered, but both the AD and the error message were empty.");
                    return;
                }

                // 广告加载成功。
                Debug.Log("The interstitial advertisement has been loaded successfully. Response message: " +
                          ad.GetResponseInfo());
                _interstitialAd = ad;
                ListenAdEventsI(ad);
            });
        }

        private void ListenAdEventsI(InterstitialAd ad)
        {
            // 当广告产生收益时触发。
            ad.OnAdPaid += adValue => { Debug.Log($"插屏广告产生收益: {adValue.Value} {adValue.CurrencyCode}"); };
            // 当广告记录一次展示时触发。
            ad.OnAdImpressionRecorded += () => { Debug.Log("插屏广告记录了一次展示。"); };
            // 当广告被点击时触发。
            ad.OnAdClicked += () => { Debug.Log("插屏广告被点击。"); };
            // 当广告打开全屏内容时触发。
            ad.OnAdFullScreenContentOpened += () =>
            {
                Debug.Log("插屏广告全屏内容已打开。");
                Game.PauseGame();
            };
            // 当广告关闭全屏内容时触发。
            ad.OnAdFullScreenContentClosed += () =>
            {
                Debug.Log("插屏广告全屏内容已关闭。");
                Game.ResumeGame();
                _adCompletedCallBackI?.Invoke(true);
            };
            // 当广告无法打开全屏内容时触发。
            ad.OnAdFullScreenContentFailed += error =>
            {
                Debug.LogError("插屏广告无法打开全屏内容，错误信息: " + error);
                _adCompletedCallBackI?.Invoke(false);
            };
        }

        private void HandleAdLoadFailureI()
        {
            if (_retryTimerI != 0) return;
            if (_retryCountI < MaxRetriesI)
            {
                Debug.Log($"尝试重新加载插屏广告，第 {_retryCountI} 次重试。");
                _retryCountI++;
                _retryTimerI = UnityTimer.Delay(RetryDelayI, LoadInterstitialAd);
            }
            else
            {
                Debug.LogError("The maximum retry count has been reached. Stop loading the interstitial ads");
                _retryCountI = 0;
            }
        }

        #endregion

        #region 激励广告

        private Dictionary<Define.AdKey, RewardedAd> _rewardedAds;
        private event Action<bool> AdCompletedCallBackR;
        private int _retryCountR;
        private long _retryTimerR;
        private const int MaxRetriesR = 3; // 最大重试次数
        private const float RetryDelayR = 2f; // 重试延迟（秒）
        
        private void LoadRewardedAds()
        {
            var enumKeys = Enum.GetNames(typeof(Define.AdKey));
            foreach (var key in enumKeys)
            {
                if(!key.StartsWith("RewardAd"))
                    continue;
                LoadRewardAd((Define.AdKey)Enum.Parse(typeof(Define.AdKey), key));
            }
        }

        private void LoadRewardAd(Define.AdKey key)
        {
            _retryTimerR = 0;
            if (_rewardedAds.ContainsKey(key))
                _rewardedAds[key].Destroy();

            // 创建用于加载广告的请求。
            var adRequest = new AdRequest();
            var adUnitId = GetAdId(key);
            if (string.IsNullOrEmpty(adUnitId))
            {
                Debug.LogError("The reward Ads parameters are incorrect：" + adUnitId);
                return;
            }

            // 发送请求以加载广告。
            RewardedAd.Load(adUnitId, adRequest, (ad, error) =>
            {
                // 如果操作失败，输出错误信息。
                if (error != null)
                {
                    HandleAdLoadFailureR(key, error.GetResponseInfo().ToString());
                    return;
                }

                // 如果广告为空，则记录意外错误。
                if (ad == null)
                {
                    Debug.LogError(
                        "Unexpected error: The reward Ads event was triggered, but the advertisement was empty。");
                    return;
                }

                // 操作成功完成。
                Debug.Log("The reward Ads was loaded successfully. Response information: " + ad.GetResponseInfo());
                _rewardedAds[key] = ad;
                ListenAdEventsR(ad, key);
            });
        }

        private void ListenAdEventsR(RewardedAd ad, Define.AdKey key)
        {
            // 当广告估计获得收入时触发。
            ad.OnAdPaid += adValue => { Debug.Log($"奖励广告支付了 {adValue.Value} {adValue.CurrencyCode}"); };

            // 当记录到一次展示时触发。
            ad.OnAdImpressionRecorded += () => { Debug.Log("奖励广告记录到一次展示。"); };

            // 当广告被点击时触发。
            ad.OnAdClicked += () => { Debug.Log("奖励广告被点击。"); };

            // 当广告打开全屏内容时触发。//在广告开始展示并铺满设备屏幕时被调用。如需暂停应用音频输出或游戏循环，则非常适合使用此方法。
            ad.OnAdFullScreenContentOpened += () =>
            {
                Debug.Log("奖励广告打开全屏内容。");
                Game.PauseGame();
            };

            // 当广告关闭全屏内容时触发。
            ad.OnAdFullScreenContentClosed += () =>
            {
                Debug.Log("奖励广告关闭全屏内容。");
                LoadRewardAd(key);
                Game.ResumeGame();
            };

            // 当广告打开全屏内容失败时触发。
            ad.OnAdFullScreenContentFailed += error =>
            {
                Debug.LogError("奖励广告打开全屏内容失败，错误信息： " + error);
                LoadRewardAd(key);
                AdCompletedCallBackR?.Invoke(false);
            };
        }

        private void HandleAdLoadFailureR(Define.AdKey key, string error)
        {
            if (_retryTimerR != 0) return;
            if (_retryCountR < MaxRetriesR)
            {
                Debug.Log($"Try reloading the incentive advertisement，第 {_retryCountR} 次重试。" + key + " :" + error);
                _retryCountR++;
                _retryTimerR = UnityTimer.Delay(RetryDelayR, () => { LoadRewardAd(key); });
            }
            else
            {
                Debug.LogError("The maximum retry count has been reached. Stop loading the incentive advertisement.。");
                _retryCountR = 0;
            }
        }

        #endregion

        #region 横幅广告

        private BannerView _bannerView;
        private long _autoRefreshTimer;
        private bool _isShowAdB;
        private int _retryCountB;
        private long _retryTimerB;
        private const int MaxRetriesB = 3; // 最大重试次数
        private const float RetryDelayB = 5; // 重试延迟
        private const float RefreshInterval = 30; //自动刷新时间
        
        private void LoadBannerAd()
        {
            _bannerView?.Destroy();
            _retryTimerB = 0;

            _bannerView = new BannerView(GetAdId(Define.AdKey.BannerAdUnitId), AdSize.Banner, AdPosition.Bottom);
            ListenAdEventsB();
        }

        private void AutoRefreshB()
        {
            if(_autoRefreshTimer > 0) return;
            _autoRefreshTimer = UnityTimer.Loop(RefreshInterval, () =>
            {
                if(_isShowAdB)
                    _bannerView?.Show();
            });
        }

        private void ListenAdEventsB()
        {
            // 当广告加载到横幅视图时触发。
            _bannerView.OnBannerAdLoaded += () =>
            {
                Debug.Log("The banner advertisement view loads the advertisement and response information：" +
                          _bannerView.GetResponseInfo());
            };

            // 当广告加载失败时触发。
            _bannerView.OnBannerAdLoadFailed += error =>
            {
                Debug.Log("横幅广告视图加载广告失败，错误信息： " + error);
                HandleAdLoadFailureB();
            };

            // 当广告估计获得收入时触发。
            _bannerView.OnAdPaid += adValue => { Debug.Log($"横幅广告视图获得了 {adValue.Value} {adValue.CurrencyCode} 的收入"); };

            // 当广告记录到一次展示时触发。
            _bannerView.OnAdImpressionRecorded += () => { Debug.Log("横幅广告视图记录到一次展示。"); };

            // 当广告被点击时触发。
            _bannerView.OnAdClicked += () => { Debug.Log("横幅广告视图被点击。"); };

            // 当广告打开全屏内容时触发。
            _bannerView.OnAdFullScreenContentOpened += () =>
            {
                Debug.Log("横幅广告视图打开了全屏内容。");
                UnityTimer.Delay(RetryDelayB, LoadBannerAd);
            };

            // 当广告关闭全屏内容时触发。
            _bannerView.OnAdFullScreenContentClosed += () => { Debug.Log("横幅广告视图关闭了全屏内容。"); };
        }

        private void HandleAdLoadFailureB()
        {
            if (_retryTimerB != 0) return;
            if (_retryCountB < MaxRetriesB)
            {
                Debug.Log($"Try to reload the banner ad，Count {_retryCountB} Second retry。");
                _retryCountB++;
                _retryTimerB = UnityTimer.Delay(RetryDelayB, LoadBannerAd);
            }
            else
            {
                Debug.LogError("The maximum retry count has been reached. Stop loading the banner advertisement.");
                _retryCountB = 0;
            }
        }

        #endregion
        
    }
}
#endif