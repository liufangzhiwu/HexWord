using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.HuaweiAppGallery;
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
        
        public Transform _uiRoot;
        public CommonErrorType CurrentErrorType { private set; get; }

        public static bool IsNetworkActive { private set; get; }

        private void Awake()
        {
            self = this;
            DontDestroyOnLoad(gameObject);
            gameObject.AddComponent<UnityTimer>();
         
            
#if UNITY_huawei&&!UNITY_EDITOR
            HuaweiGameService.AppInit();
#endif
            // StartCoroutine(ShowLoadingScreen());
            StartCoroutine(CheckNetworkConnection());
            
            InitManagers();
        }

        public void InitGame()
        { 
            CreateAccounts();
            StartCoroutine(WaitLoginedCreateShop());
        }
        
        private IEnumerator WaitLoginedCreateShop()
        {
            CreateAnalytic();
            yield return new WaitUntil(()=>Accounts.IsLogin);
// #if UNITY_EDITOR 
//             CreateAd();
#if UNITY_OPENHARMONY||UNITY_huawei
            CreateAd();
            CreateShop();
            CreateAttribute();
#endif
        }


        private void InitManagers()
        {
	        GameDataManager.Instance.Init();
            LoadTextManager.Instance.Init();
	        //AudioManager.Instance.Init();
            LimitTimeManager.Instance.Init();
            StreakManager.Instance.Init();
            ThemeManager.Instance.Init();
            
            #if UNITY_EDITOR
            CreateAnalytic();
            #endif
        }
        
        private void CreateAccounts()
        {
#if UNITY_EDITOR
            Accounts = new Account_android();
#elif UNITY_ANDROID&&!UNITY_huawei
            Accounts = new Account_android();
#elif UNITY_huawei&&!UNITY_EDITOR
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
#elif UNITY_huawei
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
#if UNITY_ANDROID&&!UNITY_huawei
            Attributes = new AndoridAttribution();
#elif UNITY_huawei
            Attributes = new HuaWeiAttribution();
#elif UNITY_IOS
            Attributes = new AndoridAttribution();
#elif UNITY_OPENHARMONY
            Attributes = new HuaweiHarAttribution();
#endif
            Attributes.Init(0.1f);
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
            Analytics.Init(1f);
        }
        
        private void CreateShop()
        {
#if UNITY_huawei
            Shop = new Shop_huawei();
#elif UNITY_IOS
            Shop = new Shop_ios();
#elif UNITY_OPENHARMONY
            Shop = new Shop_harmony();
#endif
            Shop.Init(0.2f);
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
            GameObject ps = Instantiate(pg, _uiRoot.transform);
            ps.SetActive(true);
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

