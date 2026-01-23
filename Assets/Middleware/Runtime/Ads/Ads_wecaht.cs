using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WeChatWASM;

namespace Middleware
{
    public class Ads_wecaht : IAds
    {
        private const string REWARD_AD_ID = "adunit-xxxxxxxxx";       // 激励视频ID
        private const string INTERSTITIAL_AD_ID = "adunit-yyyyyyyy";  // 插屏广告ID
        private const string BANNER_AD_ID = "adunit-zzzzzzzzz";       // Banner广告ID
        
        // 微信广告实例对象
        private WXRewardedVideoAd videoAd;
        private WXInterstitialAd interstitialAd;
        private WXBannerAd bannerAd;
        
        private IAds _adsImplementation;
        public bool IsPlaying { get; set; }
        private bool isVideoReady = false;
        
        // 当前正在等待的回调
        private Action<bool> currentRewardCallback;
        private Action<bool> currentInterstitialCallback;
        
        public void Init(float delay)
        {
            UnityTimer.Delay(delay,() =>
            {
#if UNITY_WEBGL && !UNITY_EDITOR
            // 1. 初始化激励视频广告 (建议初始化一次，后续重复调用 Show)
            InitVideoAd();
            // 2. 初始化插屏广告 (插屏通常也是复用)
            InitInterstitialAd();
#else
            Debug.Log("【模拟】广告系统初始化完成");
#endif
            });
        }

        public bool IsReady(Define.AdKey key)
        {
            return isVideoReady;
        }

        public void ShowReward(Define.AdKey key, Action<bool> callback)
        {
            if (videoAd == null)
            {
                Debug.LogWarning("广告实例未初始化");
                callback?.Invoke(false);
                return;
            }

            IsPlaying = true;
            currentRewardCallback = callback; // 存下回调

            // 尝试播放
            videoAd.Show(
                // 成功回调 (注意：这里只是Show成功，不是看完)
                (res) => { Debug.Log("广告展示成功"); }, 
                // 失败回调
                (res) => 
                {
                    Debug.Log("广告展示失败，尝试重新加载...");
                    // 只有在 Show 失败时才尝试 Load，这是微信推荐的写法
                    videoAd.Load((loadRes)=> 
                        {
                            // Load 成功后再次 Show
                            videoAd.Show(null, (showErr)=>
                            {
                                Debug.LogWarning("重试展示失败: " + showErr.errMsg);
                                IsPlaying = false;
                                callback?.Invoke(false);
                            });
                        }, 
                        (loadErr)=>
                        {
                            Debug.LogWarning("重新加载失败: " + loadErr.errMsg);
                            IsPlaying = false;
                            callback?.Invoke(false);
                        });
                }
            );
        }

        public void ShowInterstitial(Action<bool> callback)
        {
            if(interstitialAd == null) 
            {
                // 如果没初始化，尝试重新创建
                InitInterstitialAd();
                if(interstitialAd == null)
                {
                    callback?.Invoke(false);
                    return;
                }
            }
            currentInterstitialCallback = callback;
            interstitialAd.Show(null, (res) =>
            {
                Debug.Log("插屏展示失败: " + res.errMsg);
                callback?.Invoke(false);
            });
        }

        public void LoadBannerAD()
        {
            
        }


        public void ShowBanner()
        {
            if (bannerAd != null)
            {
                bannerAd.Show();
                return;
            }

            // 获取系统信息用于布局
            var sysInfo = WX.GetSystemInfoSync();
            
            // 设定 Banner 宽度（一般占屏幕宽度的 80%~100%，最小300）
            // 注意：微信小游戏 Banner 样式 style 使用的是逻辑像素
            double bannerWidth = 300; 
            double screenWidth = sysInfo.windowWidth;
            double screenHeight = sysInfo.windowHeight;

            // 居中底部布局
            double left = (screenWidth - bannerWidth) / 2;
            double top = screenHeight - 100; // 初始预估，加载完后可以根据实际高度调整

            bannerAd = WX.CreateBannerAd(new WXCreateBannerAdParam()
            {
                adUnitId = BANNER_AD_ID,
                style = new Style()
                {
                    left = (int)left,
                    top = (int)top,
                    width = (int)bannerWidth
                }
            });

            bannerAd.OnError((res) =>
            {
                Debug.LogError("Banner加载失败: " + res.errMsg);
            });

            // 监听 Resize 事件，确保广告紧贴底部
            bannerAd.OnResize((res) =>
            {
                if(bannerAd != null)
                {
                    // 重新计算 Top 值：屏幕高度 - 广告实际高度
                    bannerAd.style.top = (int)(sysInfo.windowHeight - res.height);
                    bannerAd.style.left = (int)((sysInfo.windowWidth - res.width) / 2);
                }
            });

            bannerAd.Show();
        }

        public void HideBanner()
        {
            if (bannerAd != null)
            {
                bannerAd.Hide();
                // 如果为了省内存，也可以直接 Destroy
                // bannerAd.Destroy(); 
                // bannerAd = null;
            }
        }
        
        // =========================================================
        // 激励视频逻辑
        // =========================================================
        private void InitVideoAd()
        {
            // 创建实例
            videoAd = WX.CreateRewardedVideoAd(new WXCreateRewardedVideoAdParam()
            {
                adUnitId = REWARD_AD_ID
            });

            // 监听错误
            videoAd.OnError((res) =>
            {
                Debug.LogError("激励视频加载失败: " + res.errMsg);
                isVideoReady = false;
                IsPlaying = false;
                currentRewardCallback?.Invoke(false);
                currentRewardCallback = null;
            });

            // 监听加载成功
            videoAd.OnLoad((res) =>
            {
                Debug.Log("激励视频加载成功");
                isVideoReady = true;
            });

            // 监听关闭
            videoAd.OnClose((res) =>
            {
                IsPlaying = false;
                // res.isEnded: true=完整观看, false=中途退出
                // 注意：旧版本微信可能返回 null 或 undefined，需要兼容
                bool isCompleted = (res != null && res.isEnded); 
                
                Debug.Log($"激励视频关闭，是否完整观看: {isCompleted}");
                
                currentRewardCallback?.Invoke(isCompleted);
                currentRewardCallback = null;
            });
        }
        
        // =========================================================
        // 插屏广告逻辑
        // =========================================================
        private void InitInterstitialAd()
        {
            // 插屏建议每次展示时判断或创建，但单例模式也可以
            // 如果你的插屏ID是固定的，可以在这里创建
            if (string.IsNullOrEmpty(INTERSTITIAL_AD_ID)) return;
             
            interstitialAd = WX.CreateInterstitialAd(new WXCreateInterstitialAdParam
            {
                adUnitId = INTERSTITIAL_AD_ID
            });
             
            interstitialAd.OnClose(() =>
            {
                Debug.Log("插屏关闭");
                currentInterstitialCallback?.Invoke(true);
            });
             
            interstitialAd.OnError((err) =>
            {
                Debug.LogWarning("插屏错误: " + err.errMsg);
                currentInterstitialCallback?.Invoke(false);
            });
        }
    }
}
