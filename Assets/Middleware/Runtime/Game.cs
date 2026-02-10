using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

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
        public IAccounts Accounts { private set; get; }
        public IAnalytics Analytics { private set; get; }
        public IShop Shop { private set; get; }
        
        public Transform _uiRoot;
        public CommonErrorType CurrentErrorType { private set; get; }

        public static bool IsNetworkActive { private set; get; } = true;

        private void Awake()
        {
            self = this;
            DontDestroyOnLoad(gameObject);
            gameObject.AddComponent<UnityTimer>();
        }
        

        public void InitGame()
        {
            CreateAnalytic();
            if (UIUtilities.isEditMode) return;
            CreateAccounts();
        }
        public void InitManagers()
        {
            AudioManager.Instance.Init();
	        LimitTimeManager.Instance.Init();
            ConfigManager.Instance.LoadAdjustTable();
            DailyTaskManager.Instance.Init();
            // ShopManager.shopManager.Initialize();
            MultilingualManager.Instance.LoadLocalizationNameTable();
            // ChessStageController.Instance.Init();
            CreateAd();
            // CreateShop();
        }
        
        private void CreateAccounts()
        {
            
#if UNITY_ANDROID
            Accounts = new Account_android();
#elif UNITY_huawei
            Accounts = new Account_huaweiandroid();
            Accounts.Init(0.2f);
#elif UNITY_OPENHARMONY
            Accounts = new Account_harmony();
           
#endif
            Accounts = new Account_wechat();
            Accounts.Init(0.2f);
        }
    
        private void CreateAd()
        {
#if UNITY_huawei
            // Ads = new Ads_android();
            Ads = new Ads_huawei();
#elif UNITY_IOS
            Ads = new Ads_ios();
#elif UNITY_OPENHARMONY
            Ads = new Ads_harmony();
#endif
            Ads = new Ads_wecaht();
            Ads.Init(0.2f);
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
            Analytics = new Analytics_wechat();
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
            Shop = new Shop_wechat();
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
            MessageSystem.Instance.HideLoadingAnimation();
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

