using System;
using System.Collections;
using System.Collections.Generic;
using Middleware;
using UnityEngine;
using UnityEngine.HuaweiAppGallery;
using UnityEngine.HuaweiAppGallery.Listener;
using UnityEngine.UI;

public class Launch : MonoBehaviour
{
    public static Launch Instance;
    [SerializeField] private Button _ageTip;

    private float timer = 0f;
    public bool isTiming = false;
    
    public GameFlowStatus flowStatus = GameFlowStatus.NotStarted;
    private int _retryAttempt = 0;
    private const int MAX_RETRIES = 3; // 设置最大重试次数
    private const float RETRY_DELAY = 1.0f; // 重试间隔（秒）
    private void Awake()
    {
        if(Instance == null)
            Instance = this;
    }
    
    // Start is called before the first frame update
    private IEnumerator Start()
    {
        yield return null;
        UnityMainThreadDispatcher.Instance();
        MultilingualManager.Instance.LoadLocalization();
        GameDataManager.Instance.LoadPlayerProfile();
        transform.parent.GetChild(transform.GetSiblingIndex() + 1).gameObject.SetActive(false);
        _ageTip.AddClickAction(OnAgeTipClick);
        yield return new WaitForSeconds(0.3f);
        
        if (!GameDataManager.Instance.UserData.IsAgreePrivacy)
        {
#if UNITY_OPENHARMONY
             GameDataManager.Instance.UserData.IsAgreePrivacy = true;
             isTiming = true;
#elif UNITY_huawei
            GameObject pg = Resources.Load<GameObject>("Privacy/PrivacyGuidance");
            GameObject ps = Instantiate(pg, transform);
            ps.SetActive(true);
#endif
        }
        else
        {
            isTiming = true;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!isTiming) return;
        timer += Time.deltaTime;
        if (timer >= 2f)
        {
            isTiming = false;
            OpenNextPage();
        }
    }

    public void OpenNextPage()
    {
       StartCoroutine(WaitLogin());
    }

    public IEnumerator WaitLogin()
    {
        
        if (!UIUtilities.isEditMode)
        {
            Debug.Log($"进入初始化游戏服务流程");
            //初始化游戏服务 
            yield return InitializeGameService();
            // 初始化商店(需要等待游戏服务完成后)
            Game.self.InitGame();
            yield return new WaitUntil(() => flowStatus == GameFlowStatus.LoginReady);
            HuaweiGameService.ShowFloatWindow();
            yield return new WaitForSeconds(0.02f);
        }
        else
        {
            Game.self.InitGame();
        }
        Debug.Log("完成初始化游戏服务流程");
        flowStatus = GameFlowStatus.LoggingIn;
        yield return new WaitUntil(() => flowStatus == GameFlowStatus.LoggingIn);
        gameObject.SetActive(false);
        transform.parent.GetChild(transform.GetSiblingIndex() + 1).gameObject.SetActive(true);
    }
    
    private void OnAgeTipClick()
    {
        GameObject go = Resources.Load<GameObject>("Privacy/AgeWindow");
        GameObject aw = Instantiate(go, transform);
        aw.SetActive(true);
    }
    
    private IEnumerator InitializeGameService()
    {
        Action<GameFlowStatus> statusSetter = (status) => { flowStatus = status; };
        if (_retryAttempt >= MAX_RETRIES)
        {
            flowStatus = GameFlowStatus.InitFailed;
            Debug.LogError($"初始化游戏服务失败,并退出游戏");
            Application.Quit();
            yield break;
        }

        _retryAttempt++;
        flowStatus = GameFlowStatus.Initializing;
        HuaweiGameService.Init(new AntiAddictionHandler(), new InitHandler(statusSetter, () =>
        {
            flowStatus = GameFlowStatus.InitFailed;
            StartCoroutine(RetryAfterDelay(RETRY_DELAY));
            Debug.LogError($"初始化游戏服务失败，重试次数：{_retryAttempt}");
        }));
        yield return new WaitUntil(() => flowStatus is GameFlowStatus.CheckingUpdate);
        Debug.Log($"进入检查更新流程");
        HuaweiGameService.CheckUpdate(new CheckUpdateListener(statusSetter));
        yield return new WaitUntil(() => flowStatus is GameFlowStatus.LoginReady);
        Debug.Log($"检查更新流程完成");
     
    }
    
    // 助手协程：用于在重试前等待一段时间
    private IEnumerator RetryAfterDelay(float delay)
    {
        MessageSystem.Instance.ShowTip($"等待 {delay} 秒后重试...");
        yield return new WaitForSeconds(delay);
    
        // 🔑 关键：重新启动初始化流程
        StartCoroutine(InitializeGameService());
    }
    
    // 🔑 1. 定义局部实现类 IAntiAddictionListener
    private class AntiAddictionHandler : IAntiAddictionListener
    {
        public void OnExit()
        {
            Debug.Log("防沉迷退出回调：退出应用。");
            GameDataManager.Instance.CommitGameData();
            Application.Quit();
        }
    }
    
        // 🔑 2. 定义局部实现类 IInitListener
    private class InitHandler : IInitListener
    {
        private readonly Action<GameFlowStatus> _onCompletedCallback;
        private readonly Action  _onRetryAction;

        public InitHandler(Action<GameFlowStatus> onCompletedCallback, Action onRetryAction = null)
        {
            _onCompletedCallback = onCompletedCallback;
            _onRetryAction = onRetryAction;
        }
        public void OnSuccess()
        {
            HuaweiGameService.ShowFloatWindow();
            _onCompletedCallback?.Invoke(GameFlowStatus.CheckingUpdate);
        }

        public void OnFailure(int code, string message)
        {
            string msg = $"JosAppsClient init failed, code:{code} message:{message}";
            Debug.Log(msg);
            switch (code)
            {
                case 7002:
                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                        // MessageSystem.Instance.ShowTip("请检查网络");
                        _onCompletedCallback?.Invoke(GameFlowStatus.InitFailed);
                    });
                    break;
                case 7401:
                    _onCompletedCallback?.Invoke(GameFlowStatus.InitFailed);
                    Application.Quit();
                    break;
                case 907135003:
                    _onRetryAction?.Invoke();
                    break;
                default:
                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                         // MessageSystem.Instance.ShowTip(msg);
                        _onCompletedCallback?.Invoke(GameFlowStatus.InitFailed);
                    });
                    break;
            }
        }
    }
    
    private class CheckUpdateListener : ICheckUpdateListener
    {
        private readonly Action<GameFlowStatus> _onCompletedCallback;

        public CheckUpdateListener(Action<GameFlowStatus> onCompletedCallback)
        {
            _onCompletedCallback = onCompletedCallback;
        }
        public  void OnUpdateInfo(AndroidJavaObject intent)
        {
            if (intent !=null)
            {
                int status = intent.Call<int>("getIntExtra", "status", 0);
                if (status==0)
                {
                    // 无需更新，直接进入下一阶段：登录
                    _onCompletedCallback?.Invoke(GameFlowStatus.LoginReady);
                }
                else if (status == 7)
                {
                    // 发现更新，等待用户操作或退出
                    _onCompletedCallback.Invoke(GameFlowStatus.UpdateRequired);
                    AndroidJavaObject apkUpgradeInfo = intent.Call<AndroidJavaObject>("getSerializableExtra", "updatesdk_update_info");
                    HuaweiGameService.ShowUpdateDialog(apkUpgradeInfo, true);
                    // bool isExit = intent.Call<bool>("getBooleanExtra", ",", false);
                    // TODO
                }
                else
                {
                    _onCompletedCallback?.Invoke(GameFlowStatus.LoginReady);
                }
            }
            else
            {
                _onCompletedCallback?.Invoke(GameFlowStatus.LoginReady);
            }
        }

        public void OnMarketInstallInfo(AndroidJavaObject intent)
        {
           
        }

        public void OnMarketStoreError(int responseCode)
        {
           
        }

        public void OnUpdateStoreError(int responseCode)
        {
           
        }
    }
    
}