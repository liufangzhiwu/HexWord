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
        public static IAds Ads { private set; get; }
        public static IAccounts Accounts { private set; get; }
        public static IAnalytics Analytics { private set; get; }
        public static IShop Shop { private set; get; }
        
        public Transform _uiRoot;
        public CommonErrorType CurrentErrorType { private set; get; }

        public static bool IsNetworkActive { private set; get; }

        private void Awake()
        {
            self = this;
            DontDestroyOnLoad(gameObject);
            gameObject.AddComponent<UnityTimer>();
         
            // StartCoroutine(ShowLoadingScreen());
            StartCoroutine(CheckNetworkConnection());
        }

        public static void InitGame()
        {
#if UNITY_OPENHARMONY
            CreateAd();
            CreateAccounts();
            
#endif
            CreateAnalytic();
            // CreateShop();
            InitManagers();
        }

        // IEnumerator  ShowLoadingScreen()
        // {
        //     yield return new WaitForSeconds(2f);
        //     LoadingScreen.gameObject.SetActive(true);
        // }

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
#if UNITY_huawei
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
            Shop = new Shop_android();
#elif UNITY_huawei
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
        
        private IEnumerator CheckNetworkConnection()
        {
            while (true)
            {
                bool isSuccess = false;
                Ping ping = new Ping("8.8.8.8");
                float timeout = 3.0f;
                float startTime = Time.time;

                // 等待Ping完成或超时
                while (!ping.isDone && Time.time - startTime < timeout)
                {
                    yield return null;
                }

                // 关键修改：明确超时和成功的条件
                if (ping.isDone && ping.time > 0 && ping.time < 2000)
                {
                    isSuccess = true;
                }
                else
                {
                    isSuccess = false;
                }

                // 释放Ping资源（Unity需手动销毁）
                ping.DestroyPing();
                ping = null;

                IsNetworkActive = isSuccess;
                Debug.Log("网络状态: " + (IsNetworkActive ? "已连接" : "未连接"));

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

