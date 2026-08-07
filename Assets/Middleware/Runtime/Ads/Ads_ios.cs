#if UNITY_IOS
using System;
using System.Collections.Generic;
using ByteDance.Union;
using UnityEngine;

using System.Collections.Generic;
using System.Threading;
using ByteDance.Union;
using ByteDance.Union.Mediation;
using UnityEngine;
using UnityEngine.UI;

namespace Middleware
{
    public class Ads_ios : IAds
    {
        public bool IsPlaying { get; set; }
        
        public NativeAd bannerAd;                    // 自渲染banner，仅支持csj。推荐使用ExpressBannerAd
        public ExpressBannerAd mExpressBannerAd;     // 模板banner，支持csj和融合
        public BUSplashAd splashAd;                  // 开屏广告，支持csj和融合
        public ExpressAd mExpressFeedad;             // 模板feed，仅支持csj
        public FeedAd feedAd;                        // 自渲染feed，支持csj和融合。在融合里模板和自渲染都支持。
        public DrawFeedAd drawFeedAd;                // drawFeed，仅支持融合
        public FullScreenVideoAd fullScreenVideoAd;  // 插全屏和新插屏，支持csj和融合
        public RewardVideoAd rewardAd;               // 激励视频，支持csj和融合

        private bool useMediation;
        
        public Action<bool> _adCompletedCallBackI; //插屏关闭回调
        
        // Unity 主线程ID:
        public static int MainThreadId;

        public void Init(float delay)
        {
            UnityTimer.Delay(delay, () =>
            {
                MainThreadId = Thread.CurrentThread.ManagedThreadId;
                InitializeCSJMobileAds();
            });
        }

        public bool IsReady(Define.AdKey key)
        {
            return true;
        }

        public void ShowReward(Define.AdKey key, Action<bool> callback)
        {
// #if UNITY_EDITOR||Unity_ShowLog
//             callback(true);
//             return;
// #endif
            _adCompletedCallBackI = callback;
            ExampleRewardAd.ShowReward(this);
        }
        
        public void LoadReward()
        {
            ExampleRewardAd.LoadReward(this, false);
        }

        public void ShowInterstitial(Action<bool> callback)
        {
            _adCompletedCallBackI = callback;
            ExampleFullScreenVideoAd.ShowFullScreenVideoAd(this);
        }

        public void LoadInterstitial()
        {
            ExampleFullScreenVideoAd.LoadFullScreenVideoAd(this, false);
        }
        
        public void LoadBannerAD()
        {
            ExampleBannerAd.LoadNativeBannerAd(this);
        }

        public void ShowBanner()
        {
            Debug.Log("Show the banner AD view.");
            //_bannerView?.Show();
            ExampleBannerAd.ShowNativeBannerAd(this);
        }

        public void HideBanner()
        {
           
          
        }

        
        #region 通用逻辑
        
        private string GetAdId(Define.AdKey key)
        {
            var adId = "";

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

            return adId;
        }

        private void InitializeCSJMobileAds()
        {
            Debug.Log("CSJ Ads initialization");
            try
            {
                
                // sdk初始化
                SDKConfiguration sdkConfiguration = new SDKConfiguration.Builder()
                    .SetAppId(CSJMDAdPositionId.APP_ID)
                    .SetAppName("APP测试媒体")
                    .SetUseMediation(useMediation) // 是否使用融合功能，置为false，可不初始化聚合广告相关模块
                    .SetDebug(true) // debug日志开关，app发版时记得关闭
                    .SetMediationConfig(GetMediationConfig())
                    .SetPrivacyConfigurationn(GetPrivacyConfiguration())
                    .SetAgeGroup(0)
                    .SetPaid(false) // 是否是付费用户
                    .SetTitleBarTheme(AdConst.TITLE_BAR_THEME_LIGHT) // 设置落地页主题
                    .SetKeyWords("") // 设置用户画像关键词列表
                    .Build();

                Pangle.Init(sdkConfiguration); // 合规要求，初始化分为2步，第一步先调用init
                Pangle.Start(SdkInitCallback); // 第二步再调用start。注意在初始化回调成功后再请求广告
            }
            catch (Exception e)
            {
                Debug.LogError($"CSJ initialization failed: {e.Message}\n{e.StackTrace}");
            }
        }
        
        /**
        * 初始化时进行隐私合规相关配置。不设置的将使用默认值
        */
        
        private PrivacyConfiguration GetPrivacyConfiguration()
        {
            // 这里仅展示了部分设置，开发者根据自己需要进行设置，不设置的将使用默认值，默认值可能不合规。
            PrivacyConfiguration privacyConfig = new PrivacyConfiguration();
            privacyConfig.CanUsePhoneState = false;
            privacyConfig.CanUseLocation = false;
            privacyConfig.Longitude = 115.7;
            privacyConfig.Latitude = 39.4;
            privacyConfig.IsCanUseMessage = true;
            //privacyConfig.CustomIdfa = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx";
            Dictionary<string, System.Object> userPrivacyConfig = new Dictionary<string, System.Object>();
            userPrivacyConfig.Add("motion_info", "1");
            userPrivacyConfig.Add(AdConst.bum_limit_personal_cpus, "0");
            userPrivacyConfig.Add("installUninstallListen", "1"); // 是否允许gdt/baidu监听安装和卸载app
            userPrivacyConfig.Add("forbidden_idfa", "1"); 
            privacyConfig.UserPrivacyConfig = userPrivacyConfig;


            // 融合相关配置示例
            privacyConfig.MediationPrivacyConfig = new MediationPrivacyConfig();
            privacyConfig.MediationPrivacyConfig.LimitPersonalAds = false;
            privacyConfig.MediationPrivacyConfig.ProgrammaticRecommend = false;
            privacyConfig.MediationPrivacyConfig.CanUseOaid = false;

            return privacyConfig;
        }
        
        
        /**
         * 使用融合功能时，初始化时进行相关配置
         */
        private MediationConfig GetMediationConfig()
        {
            MediationConfig mediationConfig = new MediationConfig();

            // 聚合配置json字符串（从gromore平台下载），用于首次安装时作为兜底配置使用。可选
            mediationConfig.CustomLocalConfig = MediationLocalConfig.CONFIG_JSON_STR;

            // 流量分组功能，可选
            MediationConfigUserInfoForSegment segment = new MediationConfigUserInfoForSegment();
            segment.Age = 18;
            segment.Gender = AdConst.GENDER_MALE;
            segment.Channel = "mediation-unity";
            segment.SubChannel = "mediation-sub-unity";
            segment.UserId = "mediation-userId-unity";
            segment.UserValueGroup = "mediation-user-value-unity";
            segment.CustomInfos = new Dictionary<string, string>
            {
                { "customKey", "customValue" }
            };
            mediationConfig.MediationConfigUserInfoForSegment = segment;

            return mediationConfig;
        }
        
        private void SdkInitCallback(bool success, string message)
        {
            // 注意：在初始化回调成功后再请求广告
            Debug.Log("CSJM_Unity "+"Example "+"sdk初始化结束：success: " + success + ", message: " + message);
            // 也可以调用sdk的函数，判断sdk是否初始化完成
            Debug.Log("CSJM_Unity "+ "Example " + "sdk是否初始化成功, IsSdkReady: " + Pangle.IsSdkReady());
        }

 

    /* 💜💜💜💜💜💜💜💜💜💜💜💜💜💜 ↓↓↓↓↓↓↓↓↓↓ 开屏广告相关样例 ↓↓↓↓↓↓↓↓↓↓ 💜💜💜💜💜💜💜💜💜💜💜💜💜💜 */

    // load and show splash ad
    // public void LoadAndShowSplashAd()
    // {
    //     ExampleSplashAd.LoadAndShowSplashAd(this, false);
    // }
    //
    // // load and show mediation splash ad
    // public void LoadAndShowMediationSplashAd()
    // {
    //     ExampleSplashAd.LoadAndShowSplashAd(this, true);
    // }

    /* 💜💜💜💜💜💜💜💜💜💜💜💜💜💜 ↑↑↑↑↑↑↑↑↑↑ 开屏广告相关样例 ↑↑↑↑↑↑↑↑↑↑ 💜💜💜💜💜💜💜💜💜💜💜💜💜💜 */

    
    /* 🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤 ↓↓↓↓↓↓↓↓↓↓ feed广告相关样例 ↓↓↓↓↓↓↓↓↓↓ 🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤 */
    // load express feed ad
    // public void LoadExpressFeedAd()
    // {
    //     ExampleExpressFeedAd.LoadExpressFeedAd(this);
    // }
    //
    // // Show the expressFeed Ad.
    // public void ShowExpressFeedAd()
    // {
    //     ExampleExpressFeedAd.ShowExpressFeedAd(this);
    // }
    //
    // // load feed ad.
    // public void LoadFeedAd()
    // {
    //     ExampleFeedAd.LoadFeedAd(this, false);
    // }
    //
    // // Show the Feed Ad.
    // public void ShowFeedAd()
    // {
    //     ExampleFeedAd.ShowFeedAd(this);
    // }
    //
    // // load mediation feed ad.
    // public void LoadMediationFeedAd()
    // {
    //     ExampleFeedAd.LoadFeedAd(this, true);
    // }
    //
    // // Show the mediation Feed Ad.
    // public void ShowMediationFeedAd()
    // {
    //     ExampleFeedAd.ShowFeedAd(this);
    // }
    /* 🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤 ↑↑↑↑↑↑↑↑↑↑ feed广告相关样例 ↑↑↑↑↑↑↑↑↑↑ 🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤 */


    /* 🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤 ↓↓↓↓↓↓↓↓↓↓ DrawFeed广告相关样例 ↓↓↓↓↓↓↓↓↓↓ 🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤 */

    // load mediation draw feed ad
    public void LoadMediationDrawFeedAd()
    {
        //ExampleDrawFeedAd.LoadDrawFeedAd(this);
    }

    // show mediation draw feed ad
    public void ShowMediationDrawFeedAd()
    {
        //ExampleDrawFeedAd.ShowDrawFeedAd(this);
    }

    /* 🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤 ↑↑↑↑↑↑↑↑↑↑ DrawFeed广告相关样例 ↑↑↑↑↑↑↑↑↑↑ 🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤🖤 */

    // Dispose the reward Ad.
    public void DisposeAds()
    {
        // 激励
        if (this.rewardAd != null)
        {
            this.rewardAd.Dispose();
            this.rewardAd = null;
        }

        // 全屏/新插屏
        if (this.fullScreenVideoAd != null)
        {
            this.fullScreenVideoAd.Dispose();
            this.fullScreenVideoAd = null;
        }

        // banner
        if (this.bannerAd != null)
        {
            this.bannerAd.Dispose();
            this.bannerAd = null;
        }
        if (this.mExpressBannerAd != null)
        {
            this.mExpressBannerAd.Dispose();
            this.mExpressBannerAd = null;
        }

        // 信息流
        if (this.feedAd != null)
        {
            this.feedAd.Dispose();
            this.feedAd = null;
        }
        if (this.mExpressFeedad != null)
        {
            this.mExpressFeedad.Dispose();
            this.mExpressFeedad = null;
        }
        if (this.drawFeedAd != null)
        {
            this.drawFeedAd.Dispose();
            this.drawFeedAd = null;
        }

        // 开屏
        if (this.splashAd != null)
        {
            this.splashAd.Dispose();
            this.splashAd = null;
        }
    }
        
    #endregion


    }
}
#endif