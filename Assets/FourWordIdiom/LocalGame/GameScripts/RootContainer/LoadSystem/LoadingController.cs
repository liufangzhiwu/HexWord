using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.HuaweiAppGallery;
using UnityEngine.HuaweiAppGallery.Listener;
using UnityEngine.HuaweiAppGallery.Model;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Game = Middleware.Game;
using Random = UnityEngine.Random;


/// <summary>
/// 游戏加载控制器
/// 主要功能：
/// 1. 管理游戏初始化加载流程
/// 2. 显示加载进度和提示信息
/// 3. 预加载关键游戏资源
/// 与原LoadPanel的主要差异：
/// - 完全重构的加载流程管理
/// - 新增资源依赖系统
/// - 改进进度反馈机制
/// </summary>
public class LoadingController : MonoBehaviour
{
    private enum GameFlowStatus 
    {
        NotStarted,
    
        // --- 1. 初始化阶段 ---
        Initializing,
        InitFailed,
    
        // --- 2. 更新检查阶段 ---
        CheckingUpdate,
        UpdateRequired, // 需要更新，等待用户操作
    
        // --- 3. 登录阶段 ---
        LoggingIn,
        SilentFailed,
        LoginFailed,
        
        // 获取用户信息
        GetGamePlayer,
        GetGamePlayerFailed,
        // 上传角色信息
        GamePlayerSave,
        GamePlayerSaveFailed,
        // --- 4. 完成状态 ---
        Ready // 所有流程完成，游戏可以启动
    }
    private GameFlowStatus _flowStatus = GameFlowStatus.NotStarted;
    private int _retryAttempt = 0;
    private const int MAX_RETRIES = 3; // 设置最大重试次数
    private const float RETRY_DELAY = 1.0f; // 重试间隔（秒）
    
    [Header("UI组件引用")]
    [SerializeField] private Text loadingHintText;    // 加载提示文本
    [SerializeField] private Slider progressSlider;   // 进度条组件
     [SerializeField] private GameObject Loading;   // 进度条组件
    // [SerializeField] private Button AccountQuitBtn;   // 进度条组件
    //[SerializeField] private RectTransform indicatorIcon; // 进度指示图标

    [Header("加载配置")]
    //[SerializeField] private float minLoadingTime = 5f; // 最小加载时间(秒)
    [SerializeField] private int randomHintCount = 20;    // 随机提示数量

    private AsyncOperation sceneLoadOperation;        // 场景加载操作
    private float loadStartTime;                      // 加载开始时间

    private void OnEnable()
    {
        StartCoroutine(InitializeLoadingProcess());
    }


    /// <summary>
    /// 初始化加载流程
    /// </summary>
    IEnumerator InitializeLoadingProcess()
    {
        yield return new WaitForSeconds(0.05f);
        loadStartTime = Time.time;
        // InitializeLocalization();
        //SetupRandomLoadingHint();
        LoadWordVocabulary();
        StartCoroutine(LoadingSequence());
    }

    /// <summary>
    /// 初始化本地化系统
    /// </summary>
    private void InitializeLocalization()
    {
        MultilingualManager.Instance.LoadLocalization();
        // MultilingualManager.Instance.LoadLocalizationNameTable();
        // MultilingualManager.Instance.InitbiddenWords();
    }
    
    public async void LoadWordVocabulary()
    {
        // Debug.Log("开始加载词库资源");
        await WordVocabularyManager.Instance.LoadEntriesAsync();
        Debug.Log("完成加载词库资源");
    }

    /// <summary>
    /// 设置随机加载提示
    /// </summary>
    private void SetupRandomLoadingHint()
    {
        int id=Random.Range(1,21);
        string sid = id < 10 ? "0" + id : id.ToString();
        loadingHintText.text =MultilingualManager.Instance.GetString("Haiku"+ sid);    
    }

    /// <summary>
    /// 主加载序列协程
    /// </summary>
    private IEnumerator LoadingSequence()
    {
        yield return InitializeGameService();
        yield return new WaitUntil(() => _flowStatus == GameFlowStatus.LoggingIn);
       
        // 并行执行模拟加载和实际加载
        yield return StartCoroutine(SimulateLoadingProgress());
        yield return StartCoroutine(LoadEssentialResources());
        //AudioManager.Instance.Initialize();
        // GameDataManager.Instance.LoadPlayerProfile();
        sceneLoadOperation.allowSceneActivation = true;
    }

    private IEnumerator InitializeGameService()
    {
        Action<GameFlowStatus> statusSetter = (status) => { _flowStatus = status; };
        if (_retryAttempt >= MAX_RETRIES)
        {
            _flowStatus = GameFlowStatus.InitFailed;
            Application.Quit();
            yield break;
        }

        _retryAttempt++;
        _flowStatus = GameFlowStatus.Initializing;
        HuaweiGameService.Init(new AntiAddictionHandler(), new InitHandler(statusSetter, () =>
        {
            _flowStatus = GameFlowStatus.InitFailed;
            StartCoroutine(RetryAfterDelay(RETRY_DELAY));
        }));
        yield return new WaitUntil(() => _flowStatus is GameFlowStatus.CheckingUpdate);
        HuaweiGameService.CheckUpdate(new CheckUpdateListener(statusSetter));
        yield return new WaitUntil(() => _flowStatus is GameFlowStatus.LoggingIn);
    }
    
    // 助手协程：用于在重试前等待一段时间
    private IEnumerator RetryAfterDelay(float delay)
    {
        Debug.Log($"等待 {delay} 秒后重试...");
        yield return new WaitForSeconds(delay);
    
        // 🔑 关键：重新启动初始化流程
        StartCoroutine(InitializeGameService());
    }
    /// <summary>
    /// 模拟加载进度（确保最小加载时间）
    /// </summary>
    private IEnumerator SimulateLoadingProgress()
    {
        Loading.GetComponent<CanvasGroup>().DOFade(1, 0.1f);
        
        float elapsedTime = 0;
        float progress = 0;

        while (progress < 1f)
        {
            elapsedTime = Time.time - loadStartTime;
            progress = Mathf.Clamp01(elapsedTime / 2f);
            UpdateProgressDisplay(progress);
            yield return null;
        }
        Loading.GetComponent<CanvasGroup>().DOFade(0, 0.1f);
    }

    /// <summary>
    /// 加载核心游戏资源
    /// </summary>
    private IEnumerator LoadEssentialResources()
    {
        Debug.Log("开始预加载游戏资源");
        // 标记非首次进入
        GameDataManager.Instance.UserData.IsFirstLaunch = false;
        
        yield return AssetBundleLoader.SharedInstance.LoadAtlas(
           "ui_universal",
           "UI_Universal");

        //LoadFont();
        // 加载字体资源
        Font mainFont = AssetBundleLoader.SharedInstance.LoadFont(
             "stagefonts",
             "FZKTK");
        //loadingHintText.font = mainFont;

        // 并行加载其他关键资源
        yield return AssetBundleLoader.SharedInstance.LoadAtlas(
            "effect_sprite",
            "trailAltas");

        yield return AssetBundleLoader.SharedInstance.LoadMaterialResource(
            "effectsitemmats",
            "Circle");       
        
        // 登录开始
        yield return LoadHuaweiGameLogin();
        yield return new WaitUntil(() => _flowStatus is GameFlowStatus.Ready);
        
        //预加载关卡文件
         StageHexController.Instance.LoadPackInfos();
   
        // 开始场景加载
        yield return LoadMainSceneAsync();
    }

    private IEnumerator LoadHuaweiGameLogin()
    {
        Action<GameFlowStatus> statusSetter = (status) => { _flowStatus = status; };
        
        HuaweiGameService.SilentSignIn(new SilentLoginListener(statusSetter));
        if (_flowStatus == GameFlowStatus.SilentFailed)
        {
            HuaweiGameService.Login(new SilentLoginListener(statusSetter));
        }
        yield return new WaitUntil(() => _flowStatus is GameFlowStatus.GetGamePlayer);
        Player _player = null;
        HuaweiGameService.GetGamePlayer(new GetGamePlayerListener(statusSetter, player=> _player = player));
        yield return new WaitUntil(() => _flowStatus is GameFlowStatus.GamePlayerSave);
        AppPlayerInfo appPlayerInfo = new AppPlayerInfo();
        appPlayerInfo.Rank = "test rank";
        appPlayerInfo.Area = "test area";
        appPlayerInfo.Role = GameDataManager.Instance.UserData.UserName;
        appPlayerInfo.Sociaty = "sociaty";
        appPlayerInfo.PlayerId = _player.PlayerId;
        appPlayerInfo.OpenId = _player.OpenId;
        HuaweiGameService.SavePlayerInfo(appPlayerInfo.ConvertToJavaObject(), new SavePlayerInfoListener(statusSetter));
        
        yield return new WaitUntil(() => _flowStatus is GameFlowStatus.Ready);
    }
    private void LoadFont()
    {
        // 加载TMP字体资源
        // TMP_FontAsset tmpFont = AdvancedBundleLoader.SharedInstance.LoadTMPFont(
        //     "stagefonts",
        //     "FZKTKSDF"); // 注意资源名称包含"SDF"后缀
        //
        // TMP_FontAsset selecttmpFont =  AdvancedBundleLoader.SharedInstance.LoadTMPFont(
        //     "stagefonts",
        //     "FZKTKSDF_select"); // 注意资源名称包含"SDF"后缀

        // if (tmpFont != null)
        // {
        //    
        //     Shader shaderLoad = Resources.Load<Shader>("TMP_SDF Overlay");
        //
        //     if (shaderLoad == null)
        //     {
        //         Debug.LogError("Shader加载失败");
        //     }
        //     tmpFont.material.shader = shaderLoad;
        //     selecttmpFont.material.shader = shaderLoad;
        // }
        // else
        // {
        //     Debug.LogError("TMP字体资源加载失败");
        // }
    }

    /// <summary>
    /// 异步加载主场景
    /// </summary>
    private IEnumerator LoadMainSceneAsync()
    {
        sceneLoadOperation = SceneManager.LoadSceneAsync("GameLobby");
        sceneLoadOperation.allowSceneActivation = false;

        Debug.Log("开始加载主场景");
        yield return new WaitUntil(() => sceneLoadOperation.progress >= 0.9f);
        Debug.Log("主场景加载完成");
    }

    /// <summary>
    /// 更新进度显示
    /// </summary>
    private void UpdateProgressDisplay(float progress)
    {
        // 平滑更新进度条
        progressSlider.DOValue(progress, 0.1f);

        // 更新进度指示器位置
        // Vector2 sliderSize = progressSlider.GetComponent<RectTransform>().sizeDelta;
        // float xPos = progress * sliderSize.x - (sliderSize.x / 2);
        // indicatorIcon.anchoredPosition = new Vector2(xPos, 0);

        // 更新百分比文本
        // loadingHintText.text = $"{Mathf.FloorToInt(progress * 100)}%";
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
            switch (code)
            {
                case 7002:
                    MessageSystem.Instance.ShowTip("请检查网络");
                    _onCompletedCallback?.Invoke(GameFlowStatus.InitFailed);
                    break;
                case 7401:
                    _onCompletedCallback?.Invoke(GameFlowStatus.InitFailed);
                    Application.Quit();
                    break;
                case 907135003:
                    _onRetryAction?.Invoke();
                    break;
                default:
                    MessageSystem.Instance.ShowTip(msg);
                    _onCompletedCallback?.Invoke(GameFlowStatus.InitFailed);
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
                    _onCompletedCallback?.Invoke(GameFlowStatus.LoggingIn);
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
                    _onCompletedCallback?.Invoke(GameFlowStatus.LoggingIn);
                }
            }
            else
            {
                _onCompletedCallback?.Invoke(GameFlowStatus.LoggingIn);
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
    
    private class SilentLoginListener : ILoginListener
    {
        private readonly Action<GameFlowStatus> _onLoginCompleted;

        public SilentLoginListener(Action<GameFlowStatus> onLoginCompleted)
        {
            _onLoginCompleted = onLoginCompleted;
        }
        public void OnSuccess(SignInAccountProxy signInAccountProxy)
        {
            if (signInAccountProxy == null)
            {
                MessageSystem.Instance.ShowTip("signInAccountProxy == null");
                _onLoginCompleted?.Invoke(GameFlowStatus.SilentFailed);
                return;
            }
            string msg = "get login success with signInAccountProxy info: \n";
            msg += String.Format("displayName:{0}, email:{1}, uid:{2}, openId:{3}, unionId:{4}, accessToken:{5}, serverAuthCode:{6}, idToken:{7}",
                signInAccountProxy.DisplayName, signInAccountProxy.Email, signInAccountProxy.Uid, signInAccountProxy.OpenId, signInAccountProxy.UnionId,
                signInAccountProxy.AccessToken, signInAccountProxy.ServerAuthCode, signInAccountProxy.IdToken);
            MessageSystem.Instance.ShowTip(msg);
           _onLoginCompleted.Invoke(GameFlowStatus.GetGamePlayer);
        }

        public void OnSignOut()
        {
            string msg = "sign out success.";
           MessageSystem.Instance.ShowTip(msg);
        }

        public void OnFailure(int code, string message)
        {
            string msg = "account method failed, code:" + code + " message:" + message;
            MessageSystem.Instance.ShowTip(msg);
            _onLoginCompleted?.Invoke(GameFlowStatus.SilentFailed);
        }
    }
    private class GetGamePlayerListener : IGetPlayerListener
    {
        private readonly Action<GameFlowStatus> _onGetPlayerCompleted;
        private readonly Action<Player> _owner;

        public GetGamePlayerListener(Action<GameFlowStatus> onGetPlayerCompleted, Action<Player> owner)
        {
            _onGetPlayerCompleted = onGetPlayerCompleted;
            _owner = owner;
        }
        public void OnSuccess(Player player)
        {
            if (player == null)
            {
                MessageSystem.Instance.ShowTip("player == null");
                _onGetPlayerCompleted?.Invoke(GameFlowStatus.GetGamePlayerFailed);
                return;
            }
            var msg = "getGamePlayer succeed. \n";
            msg += string.Format(
                "displayName:{0}, playerId:{1}, playerSign:{2}, openId:{3}, unionId:{4}, openIdSign:{5}, accessToken:{6}",
                player.DisplayName, player.PlayerId, player.PlayerSign, player.OpenId, player.UnionId, player.OpenIdSign, player.AccessToken
            );
            MessageSystem.Instance.ShowTip(msg);
            _owner?.Invoke(player);
            _onGetPlayerCompleted?.Invoke(GameFlowStatus.GamePlayerSave);
        }

        public void OnFailure(int code, string message)
        {
            var msg = "getCurrentPlayer failed, code:" + code + " message:" + message;
            MessageSystem.Instance.ShowTip(msg);
            _onGetPlayerCompleted?.Invoke(GameFlowStatus.GetGamePlayerFailed);
        }
    }
    
    private class SavePlayerInfoListener : ISavePlayerInfoListener
    {
        private readonly Action<GameFlowStatus> _onSavePlayerInfoCompleted;

        public SavePlayerInfoListener(Action<GameFlowStatus> onSavePlayerInfoCompleted)
        {
            _onSavePlayerInfoCompleted = onSavePlayerInfoCompleted;
        }
        public void OnSuccess()
        {
            var msg = "SavePlayerInfo succeed.";
            MessageSystem.Instance.ShowTip(msg);
            _onSavePlayerInfoCompleted?.Invoke(GameFlowStatus.Ready);
        }
        
        public void OnFailure(int code, string message)
        {
            var msg = "SavePlayerInfo failed, code:" + code + " message:" + message;
            MessageSystem.Instance.ShowTip(msg);
            _onSavePlayerInfoCompleted?.Invoke(GameFlowStatus.GamePlayerSaveFailed);
        }
    }

}
