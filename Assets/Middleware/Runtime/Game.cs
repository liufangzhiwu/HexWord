using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
#if UNITY_OPENHARMONY
using OpenHarmonyKits.Param;
#endif
using UnityEngine;
#if UNITY_HUAWEI
using UnityEngine.HuaweiAppGallery;
#endif
using UnityEngine.Networking;

namespace Middleware
{
    public enum CommonErrorType
    {
        LoginFail,
        ExitPopup,
    }
    
    public class Game : MonoBehaviour
    {
        public static Game self;
        public IAds Ads { private set; get; }
        public IAttribute Attributes { private set; get; }
        public IAccounts Accounts { private set; get; }
        public IAnalytics Analytics { private set; get; }
        public IShop Shop { private set; get; }
        public IPushs Pushs { private set; get; }
        
        public Transform _uiRoot;
        public CommonErrorType CurrentErrorType { private set; get; }

        public static bool IsNetworkActive { private set; get; }

        private GameObject NetErrorView;
        
        public bool isRigistered = false;
        
        public LoginState State { get; set; }
        
        
        private void Awake()
        {
            if (self == null)
            {
                self = this;
                DontDestroyOnLoad(gameObject);
            }
            //gameObject.AddComponent<UnityTimer>();

            StartCoroutine(CheckNetworkConnection());
            CreateAnalytic();
            // 归因在启动阶段初始化：激活事件(1)必须不依赖登录，
            // 也不能被广告/商店等后续模块的异常连带影响
            CreateAttribute();
            InitManagers();
        }

        public void InitGame()
        { 
            CreateAccounts();
            StartCoroutine(WaitLoginedCreateShop());
        }
        
        private IEnumerator WaitLoginedCreateShop()
        {
            yield return new WaitUntil(()=>Accounts.IsLogin);

            CreateAd();
            CreateShop();
            CreatePush();

        }


        /// <summary>
        /// 鸿蒙：请求 APP_TRACKING_CONSENT（广告追踪 / OAID）系统授权框。
        /// 注意：
        ///   1) 只负责"弹权限框 + 拉起 ArkTS 侧授权请求"，不加载 Banner；
        ///      Banner 的加载/展示/关闭见 Ads_harmony 的 LoadBannerAD / ShowBanner / HideBanner。
        ///   2) 授权框的"弹出时机"由 AdTrackingManager 控制（引导页点"继续"之后才调用），
        ///      不要一进加载页就调，否则会抢在广告追踪引导页之前弹系统权限框。
        ///   3) 允许/拒绝都会通过 TuanjieSendMessage → PushMessageReceiver.OnOAIDClosedSignal 回到 Unity，
        ///      埋点（Pop_up / Pop_accept / Pop_refuse）统一在 AdTrackingManager 里上报。
        /// </summary>
        public void RequestTrackingConsent()
        {
#if UNITY_OPENHARMONY
            try
            {
                OpenHarmonyJSObject adsObj = OHSDKKitManager.OHKitAdsManagerObj;
                if (adsObj == null)
                {
                    // 触发一次初始化，拿到 ArkTS 侧 OHKitAdsManager 的实例
                    var _ = OHSDKKitManager.Instance;
                    adsObj = OHSDKKitManager.OHKitAdsManagerObj;
                }

                if (adsObj == null)
                {
                    Debug.LogWarning("[OAID] ArkTS 侧 OHKitAdsManager 未就绪，无法请求广告追踪授权");
                    return;
                }

                if (_trackingConsentCallback == null)
                    _trackingConsentCallback = new OpenHarmonyJSCallback(OnTrackingConsentCallback);

                adsObj.Call("RequestTrackingConsentOnUI", _trackingConsentCallback);
                Debug.Log("[OAID] 已请求广告追踪授权（APP_TRACKING_CONSENT）");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OAID] 请求广告追踪授权失败：" + e.Message +
                                 "（ArkTS 侧桥接方法未更新？需要重新导出鸿蒙工程）");
            }
#endif
        }

        /// <summary>兼容旧调用点：等同于 RequestTrackingConsent（早期语义是"加载 Banner 顺带拉授权框"）</summary>
        public void LoadAds_OAID()
        {
            RequestTrackingConsent();
        }

        /// <summary>
        /// 鸿蒙：静默查询 APP_TRACKING_CONSENT 当前是否已授权（不弹任何系统框）。
        /// 用于启动 / 回前台时核对授权状态（bug34：玩家去系统设置把"广告关联"改成禁止后，
        /// 存档里的状态不会变，导致 IAA 分层不刷新）。
        ///
        /// 实现：ArkTS 侧只调 abilityAccessCtrl.requestPermissionsFromUser ——
        ///   已同意 → 直接返回 granted；已拒绝 → 静默返回 -1（系统不会再弹框）；
        ///   从不去拉 requestPermissionOnSetting 兜底弹窗。
        /// 结果经 TuanjieSendMessage('PushMessageReceiver','OnOAIDQueryResult','1'/'0') 回传。
        /// ★ 需要重新导出鸿蒙工程（ArkTS 侧是新增的桥接方法），未更新时只会打一条 warning。
        /// </summary>
        public void QueryTrackingConsent()
        {
#if UNITY_OPENHARMONY
            try
            {
                OpenHarmonyJSObject adsObj = OHSDKKitManager.OHKitAdsManagerObj;
                if (adsObj == null)
                {
                    var _ = OHSDKKitManager.Instance;
                    adsObj = OHSDKKitManager.OHKitAdsManagerObj;
                }

                if (adsObj == null)
                {
                    Debug.LogWarning("[OAID] ArkTS 侧 OHKitAdsManager 未就绪，无法静默查询广告追踪授权状态");
                    return;
                }

                if (_trackingConsentCallback == null)
                    _trackingConsentCallback = new OpenHarmonyJSCallback(OnTrackingConsentCallback);

                adsObj.Call("QueryTrackingConsentOnUI", _trackingConsentCallback);
                Debug.Log("[OAID] 已发起广告追踪授权状态静默查询");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OAID] 静默查询广告追踪授权状态失败：" + e.Message +
                                 "（ArkTS 侧桥接方法未更新？需要重新导出鸿蒙工程）");
            }
#endif
        }

        /// <summary>
        /// 放行广告请求（鸿蒙专用）。
        /// 广告追踪授权流程结束前，Ads_harmony 会把激励预加载 / 横幅全部挡下，
        /// 避免 AdsKit 在加载页就去取 OAID、把系统授权框抢在引导页之前弹出来。
        /// 由 AdTrackingManager 在引导流程结束（弹出过/无需弹/超时）时调用；其它平台空实现。
        /// </summary>
        public void AllowAdRequests()
        {
#if UNITY_OPENHARMONY && !UNITY_EDITOR
            Ads_harmony.AllowAdRequests();
#endif
        }

#if UNITY_OPENHARMONY 
        private static OpenHarmonyJSCallback _trackingConsentCallback;

        private static object OnTrackingConsentCallback(params OpenHarmonyJSObject[] args)
        {
            // 授权结果不走这里，统一由 ArkTS 的 TuanjieSendMessage → PushMessageReceiver.OnOAIDClosedSignal 回传
            return true;
        }
#endif
        
        private void InitManagers()
        {
	        GameDataManager.Instance.Init();
            LoadTextManager.Instance.Init();
	        //AudioManager.Instance.Init();
            LimitTimeManager.Instance.Init();
            StreakManager.Instance.Init();
            ThemeManager.Instance.Init();
            AchievementManager.Instance.Init();
            PushManager.Instance.Init();
        }
        
        private void CreateAccounts()
        {
#if UNITY_EDITOR
            Accounts = new Account_android();
#elif UNITY_ANDROID&&!UNITY_HUAWEI
            Accounts = new Account_android();
#elif UNITY_HUAWEI&&!UNITY_EDITOR
            Accounts = new Account_huaweiandroid();
#elif UNITY_OPENHARMONY
            Accounts = new Account_harmony();
#endif
            Accounts.Init(0.001f);
            Debug.LogWarning("CreateAccounts 创建完成");
        }
    
        private void CreateAd()
        {
#if UNITY_EDITOR
            Ads = new Ads_android();
#elif UNITY_HUAWEI
            Ads = new Ads_huawei();
#elif UNITY_IOS
            Ads = new Ads_ios();
#elif UNITY_OPENHARMONY
            Ads = new Ads_harmony();
#endif
            Ads.Init(0.2f);
        }
    
        private void CreateAttribute()
        {
            // 归因初始化失败不能影响游戏主流程，单独兜底
            try
            {
#if UNITY_HUAWEI
                // 华为安卓：客户端查智能分包(channel/taskid/callback) + AGC 服务端事件回传
                Attributes = new HuaWeiAttribution();
#elif UNITY_OPENHARMONY
                // 鸿蒙：客户端无渠道查询接口，走「应用归因服务」登记转化事件，
                // 归因结果由华为回传服务端
                Attributes = new HarmonyAttribution();
#else
                // iOS / 非华为安卓 / 编辑器：暂无归因实现
                Attributes = new EmptyAttribution();
#endif
                Attributes.Init(0.1f);
                // 便于在 hilog 里确认实际走的归因实现（排除"包没更新"的干扰）。
                // 用 Warning 级：部分日志导出工具会过滤 Info 级。
                Debug.LogWarning($"[Attr] 归因实现已就绪: {Attributes.GetType().Name}, delay=0.1s");
            }
            catch (Exception e)
            {
                Debug.LogError("[Attr] 归因初始化失败: " + e.Message);
            }
        }
        
        private void CreateAnalytic()
        {
#if UNITY_ANDROID
            Analytics = new Analytics_android();
#elif UNITY_IOS
            Analytics = new Analytics_ios();
#elif UNITY_OPENHARMONY
            Analytics = new Analytics_harmony();
#endif
            Analytics.Init(0.5f);
        }
        
        private void CreateShop()
        {
#if UNITY_EDITOR
            Shop = new Shop_android();
#elif UNITY_HUAWEI
            Shop = new Shop_huawei();
#elif UNITY_IOS
            Shop = new Shop_ios();
#elif UNITY_OPENHARMONY
            Shop = new Shop_harmony();
#endif
            Shop.Init(0.2f);
        }
        
        private void CreatePush()
        {
#if UNITY_HUAWEI
             //Pushs = new Push_harmony();
#elif UNITY_IOS
            
#elif UNITY_OPENHARMONY
            Pushs = new Push_harmony();
            Pushs.Init(0.5f);
#endif
           
        }
        
        public void PauseGame()
        {
            Time.timeScale = 0;
            AudioListener.pause = true;
            Ads.IsPlaying = true;
        }
    
        public void ResumeGame()
        {
            Time.timeScale = 1;
            AudioListener.pause = false; 
#if UNITY_OPENHARMONY&&!UNITY_EDITOR
            Ads.IsPlaying = false;
#endif
        }
        
        public void Ratex2Game()
        {
            Time.timeScale = 2;
            AudioListener.pause = false; 
#if UNITY_OPENHARMONY&&!UNITY_EDITOR
            Ads.IsPlaying = false;
#endif
            
        }
        
        public string GetOAID()
        {
#if UNITY_OPENHARMONY
            var filePath = Path.Combine(Application.persistentDataPath, "files", "oaid.txt");
            if (!File.Exists(filePath)) return null;
            return File.ReadAllText(filePath).Trim();
#else
            return SystemInfo.deviceUniqueIdentifier;
#endif
        }
        
        public string GetUniqueId()
        {
            return SystemInfo.deviceUniqueIdentifier;
        }
        
        private IEnumerator CheckNetworkConnection()
        {
            while (true)
            {
                bool isSuccess = false;
                using (UnityWebRequest request = UnityWebRequest.Head("https://www.apple.com/library/test/success.html"))
                {
                    // 设置超时 5 秒
                    request.timeout = 5;
                    request.SendWebRequest();

                    float startTime = Time.time;
                    while (!request.isDone && Time.time - startTime < 5.5f)
                    {
                        yield return null;
                    }

                    // 成功条件：没有网络错误且 HTTP 状态码为 2xx 或 3xx
                    if (!request.isNetworkError && !request.isHttpError)
                    {
                        isSuccess = true;
                    }
                    else
                    {
                        // 可选：记录错误码便于调试
                        Debug.Log($"网络检测失败: {request.error}");
                    }
                }

                IsNetworkActive = isSuccess;
                yield return new WaitForSeconds(5);
            }
        }

        public void ShowLoginErrorPanel()
        {
            if(_uiRoot == null) return;
            
            CurrentErrorType = CommonErrorType.LoginFail;
            GameObject pg = Resources.Load<GameObject>("Privacy/NetErrorView");
            if (NetErrorView == null)
            {
                NetErrorView = Instantiate(pg, _uiRoot.transform);
            }
            
            NetErrorView.SetActive(true);
        }
        
        public void HideLoginErrorPanel()
        {
            if(_uiRoot == null) return;
           
            if (NetErrorView != null)
            {
                NetErrorView.SetActive(false);
            }
        }
        
        public void ShowQuitGamePanel()
        {
            if(_uiRoot == null) return;
            
            CurrentErrorType = CommonErrorType.ExitPopup;
            GameObject pg = Resources.Load<GameObject>("Privacy/NetErrorView");
            GameObject ps = Instantiate(pg, _uiRoot.transform);
            ps.SetActive(true);
        }
    }

}

