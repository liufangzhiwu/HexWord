using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.HuaweiAppGallery;

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
            
            CreateAnalytic();
            InitManagers();
        }
        

        public void InitGame()
        {
            if (UIUtilities.isEditMode) return;
            
            CreateAd();
            CreateAccounts();

           StartCoroutine(WaitLoginedCreateShop());
           
        }
        
        private IEnumerator WaitLoginedCreateShop()
        {
            yield return new WaitUntil(()=>Accounts.IsLogin);
            CreateShop();
        }

        // IEnumerator  ShowLoadingScreen()
        // {
        //     yield return new WaitForSeconds(2f);
        //     LoadingScreen.gameObject.SetActive(true);
        // }

        private void InitManagers()
        {
	        GameDataManager.Instance.Init();
	        //AudioManager.Instance.Init();
	        LimitTimeManager.Instance.Init();
            
            ChessStageController.Instance.Init();
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
            if (!UIUtilities.isEditMode)
            {
                Ads.IsPlaying = false;
            }
            MessageSystem.Instance.HideLoadingAnimation();
        }

        public void Ratex2Game()
        {
            Time.timeScale = 2;
            AudioListener.pause = false;
            if (!UIUtilities.isEditMode)
            {
                Ads.IsPlaying = false;
            }
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

