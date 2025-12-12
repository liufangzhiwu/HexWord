using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Middleware
{
    public class Game : MonoBehaviour
    {
        public static IAds Ads { private set; get; }
        public static IAccounts Accounts { private set; get; }
        public static IAnalytics Analytics { private set; get; }
        public static IShop Shop { private set; get; }
        
        public GameObject LoadingScreen;
        
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            gameObject.AddComponent<UnityTimer>();
            
         
            // StartCoroutine(ShowLoadingScreen());
        }

        public static void InitGame()
        {
#if UNITY_UNITY_OPENHARMONY
            CreateAd();
            CreateAccounts();
            
#endif
            CreateAnalytic();
            // CreateShop();
            InitManagers();
        }

        IEnumerator  ShowLoadingScreen()
        {
            yield return new WaitForSeconds(2f);
            LoadingScreen.gameObject.SetActive(true);
        }

        private static void InitManagers()
        {
	        GameDataManager.Instance.Init();
	        //AudioManager.Instance.Init();
	        LimitTimeManager.Instance.Init();
            
            ChessStageController.Instance.Init();
        }
        
        private static void CreateAccounts()
        {
#if UNITY_OPENHARMONY
            Accounts = new Account_harmony();
            Accounts.Init(0.2f);
#endif
        }
    
        private static void CreateAd()
        {
#if UNITY_ANDROID
            // Ads = new Ads_android();
            Ads = new Ads_huawei();
#elif UNITY_IOS
            Ads = new Ads_ios();
#elif UNITY_OPENHARMONY
            Ads = new Ads_harmony();
#endif
            Ads.Init(0.2f);
        }
    
        private static void CreateAnalytic()
        {
#if UNITY_ANDROID
            Analytics = new Analytics_android();
#elif UNITY_IOS
            Analytics = new Analytics_ios();
#elif UNITY_OPENHARMONY
            Analytics = new Analytics_harmony();
#endif
            Analytics.Init(1.5f);
        }
        
        private static void CreateShop()
        {
#if UNITY_ANDROID
            Shop = new Shop_huawei();
#elif UNITY_IOS
            Shop = new Shop_ios();
#elif UNITY_OPENHARMONY
            Shop = new Shop_harmony();
#endif
            Shop.Init(1.5f);
        }
        
        public static void PauseGame()
        {
            Time.timeScale = 0;
            AudioListener.pause = true;
            Ads.IsPlaying = true;
        }
    
        public static void ResumeGame()
        {
            Time.timeScale = 1;
            AudioListener.pause = false; 
#if UNITY_OPENHARMONY
            Ads.IsPlaying = false;
#endif
        }
        
        public static void Ratex2Game()
        {
            Time.timeScale = 2;
            AudioListener.pause = false; 
#if UNITY_OPENHARMONY
            Ads.IsPlaying = false;
#endif
            
        }
        
        public static string GetUniqueId()
        {
#if UNITY_OPENHARMONY
            var filePath = Path.Combine(Application.persistentDataPath, "files", "oaid.txt");
            if (!File.Exists(filePath)) return null;
            return File.ReadAllText(filePath).Trim();
#else
            return SystemInfo.deviceUniqueIdentifier;
#endif
        }
    }

}

