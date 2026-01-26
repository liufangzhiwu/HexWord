using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using Middleware;
using Newtonsoft.Json;
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
     [SerializeField] private RectTransform rollingObject;   // 滚动的方块 (Image)
     private float _objectRadius;    // 方块半径
    // [SerializeField] private Button AccountQuitBtn;   // 进度条组件
    //[SerializeField] private RectTransform indicatorIcon; // 进度指示图标

    [Header("加载配置")]
    //[SerializeField] private float minLoadingTime = 5f; // 最小加载时间(秒)
    [SerializeField] private int randomHintCount = 20;    // 随机提示数量

    private AsyncOperation sceneLoadOperation;        // 场景加载操作
    private float loadStartTime;                      // 加载开始时间
    
    
    private LoginResponse loginResponse;       // 登录响应数据
    private bool isLogined = false;
    
    private UserData serverUserData;          // 解析后的主数据
    private FishUserSaveData serverFishData;      // 解析后的鱼数据 (假设你的类名叫 FishSaveData)
    private ButterflyData serverButterflyData;// 解析后的蝴蝶数据 (假设你的类名叫 ButterflyData)

    private void Awake()
    {
        loadingHintText.text = "";
        //loadingHintText.transform.GetChild(0).GetComponent<Text>().text = "";
    }

    private void OnEnable()
    {
        UnityMainThreadDispatcher.Instance();
        StartCoroutine(InitializeLoadingProcess());
    }

    private void Start()
    {
        _objectRadius = (rollingObject.rect.width * rollingObject.lossyScale.x) / 2f;
    }


    /// <summary>
    /// 初始化加载流程
    /// </summary>
    IEnumerator InitializeLoadingProcess()
    {
        InitializeLocalization();
        SetupRandomLoadingHint();
        
#if UNITY_OPENHARMONY
        if (!UIUtilities.isEditMode)
        {
            StartCoroutine(SimulateLoadingProgress());
            //初始化商店、广告、登录
            Game.self.InitGame();
            yield return new WaitUntil(()=>Game.self.Accounts.IsLogin);
            //设置登录用户ID
            AnalyticMgr.SetLoginUser(Game.self.Accounts.UserId);
        }else{
            StartCoroutine(SimulateLoadingProgress());}
#elif UNITY_huawei || UNITY_EDITOR

        if (!UIUtilities.isEditMode)
        {
             Debug.Log($"进入初始化游戏服务流程");
            //初始化游戏服务 
            yield return InitializeGameService();
            StartCoroutine(SimulateLoadingProgress());
            // 初始化商店(需要等待游戏服务完成后)
            Game.self.InitGame();
            yield return new WaitUntil(() => _flowStatus == GameFlowStatus.LoggingIn);
            // 登录开始
            yield return LoadHuaweiGameLogin();
            yield return new WaitUntil(() => _flowStatus is GameFlowStatus.Ready);
            //设置登录用户ID（需要等待游戏数据获取后）
            AnalyticMgr.SetLoginUser(Game.self.Accounts.UserId);
        }
        else
        {
            StartCoroutine(SimulateLoadingProgress());
        }
#endif
        yield return APIGateway.Instance.LoginApi.Login((res)=> 
        {
            if (res != null)
            {
                loginResponse = res as LoginResponse;
            }
            isLogined = true;
        });
       
        LoadWordVocabulary();

        yield return new WaitUntil(() => isLogined);
        
        
        yield return APIGateway.Instance.LoginApi.GetUserData(LoadUserData);
        yield return APIGateway.Instance.LoginApi.FetchUserProfile((res) =>
        {
            if (res != null)
            {
                Debug.Log("获取用户信息成功！"+ res.uid);
                //GameDataManager.Instance.UserData.Zenlevel = res.zen_level;
            }
        });
    }
    
    
    // 加载数据
    private void LoadUserData(GameDataDto response)
    {
        
        if (response == null)
        {
            Debug.Log("获取数据接口错误！，使用默认数据");
            StartCoroutine(LoadingSequence());
            return;
        } 
        if (string.IsNullOrEmpty(response.UserData))
        {
            Debug.Log("服务端主数据为空，视为新号或异常，使用本地初始化逻辑！");
            UserLocalData(); // 没数据就直接走本地逻辑
            return;
        }

        try
        {
            serverUserData = JsonConvert.DeserializeObject<UserData>(response.UserData);
            if (response.ExtraData != null)
            {
                if (!string.IsNullOrEmpty(response.ExtraData.FishUserSave))
                    serverFishData = JsonConvert.DeserializeObject<FishUserSaveData>(response.ExtraData.FishUserSave);
                
                if (!string.IsNullOrEmpty(response.ExtraData.Butterfly))
                    serverButterflyData = JsonConvert.DeserializeObject<ButterflyData>(response.ExtraData.Butterfly);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"解析服务器数据失败: {ex.Message}，回退到本地数据");
            UserLocalData();
            return;
        }
        
        // 对比逻辑 (服务器 vs 本地)
        CompareAndSelectData();
    }
    // 抽离对比逻辑，保持代码整洁
    private void CompareAndSelectData()
    {
        // A. 优先比对关卡进度
        if (serverUserData.CurrentHexStage != GameDataManager.Instance.UserData.CurrentHexStage)
        {
            if (serverUserData.CurrentHexStage > GameDataManager.Instance.UserData.CurrentHexStage)
            {
                UserServerData();
                Debug.Log("服务器关卡进度更优，使用服务器数据, 服务器数据同步完成！");
            }
            else 
            {
                UserLocalData();
                Debug.Log("本地关卡进度更优，使用本地数据");
            }
        }
        else // B. 关卡进度相同时，比对离线时间
        {
            // 安全的时间解析，防止 Parse 报错
            DateTime.TryParse(GameDataManager.Instance.UserData.logoutTime, out DateTime localTime);
            DateTime.TryParse(serverUserData.logoutTime, out DateTime serverTime);
            Debug.Log($"本地时间: {localTime}  <--> 服务器时间: {serverTime}");
            if (localTime < serverTime)
            {
                Debug.Log("服务器存档时间更新，使用服务器数据");
                UserServerData();
            }
            else
            {
                Debug.Log("本地存档时间更新，使用本地数据");
                UserLocalData();
            }
        }
    }
    
    private void UserLocalData()
    {
        ModifyUserWithABtest();
        StartCoroutine(LoadingSequence());
    }

    private void UserServerData()
    {
        GameDataManager.Instance.UserData.InitData(serverUserData);
        if (serverFishData != null)
            GameDataManager.Instance.FishUserSave.InitData(serverFishData);
        if (serverButterflyData != null)
            GameDataManager.Instance.ButterflyData.InitData(serverButterflyData);
        GameDataManager.Instance.SetInitailized(true);
        ModifyUserWithABtest();
        StartCoroutine(LoadingSequence());
    }
    
    
    // 处理ABtest数据
    public void ModifyUserWithABtest()
    {
        UserData user = GameDataManager.Instance.UserData;
        user.PlayerId = loginResponse.uid;
        user.ABName = (string)loginResponse.abtest.GetValueOrDefault("pack_name", null);
        try
        {
            Dictionary<string, object> parameterValues = new Dictionary<string, object>();
            if (loginResponse.abtest.TryGetValue("parameter_value", out object value))
            {
                parameterValues = JsonConvert.DeserializeObject<Dictionary<string, object>>(value.ToString());
            }
            //Dictionary<string, object> parameterValues = (Dictionary<string, object>)loginResponse.abtest.GetValueOrDefault("parameter_value", new Dictionary<string, object>());
            Type userType = typeof(UserData);
            foreach (var kvp in parameterValues)
            {
                PropertyInfo prop = userType.GetProperty(kvp.Key, BindingFlags.Public | BindingFlags.Instance);
                if (prop == null)
                {
                    FieldInfo field = userType.GetField(kvp.Key, BindingFlags.Public | BindingFlags.Instance);

                    if (field != null)
                    {
                        field.SetValue(user, Convert.ChangeType(kvp.Value, field.FieldType));
                    }
                }
                else
                {
                    object convertedValue = Convert.ChangeType(kvp.Value, prop.PropertyType);
                    prop.SetValue(user, convertedValue, null);
                }
            }
        }catch(Exception ex)
        {
            Debug.LogError("ABtest参数解析失败！"+ ex.Message);
        }
        GameDataManager.Instance.SetNewUser(user);
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
        Debug.Log("开始加载词库资源");
        await WordVocabularyManager.Instance.LoadEntriesAsync();
        Debug.Log("完成加载词库资源");
    }

    /// <summary>
    /// 设置随机加载提示
    /// </summary>
    private void SetupRandomLoadingHint()
    {
        int id=Random.Range(1,12);
        string sid = id < 10 ? "0" + id : id.ToString();
        loadingHintText.text =MultilingualManager.Instance.GetString("loadText"+ sid);    
    }

    /// <summary>
    /// 主加载序列协程
    /// </summary>
    private IEnumerator LoadingSequence()
    {
        // 并行执行模拟加载和实际加载
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
            Debug.LogError($"初始化游戏服务失败,并退出游戏");
            Application.Quit();
            yield break;
        }

        _retryAttempt++;
        _flowStatus = GameFlowStatus.Initializing;
        HuaweiGameService.Init(new AntiAddictionHandler(), new InitHandler(statusSetter, () =>
        {
            _flowStatus = GameFlowStatus.InitFailed;
            StartCoroutine(RetryAfterDelay(RETRY_DELAY));
            Debug.LogError($"初始化游戏服务失败，重试次数：{_retryAttempt}");
        }));
        yield return new WaitUntil(() => _flowStatus is GameFlowStatus.CheckingUpdate);
        Debug.LogError($"进入检查更新流程");
        HuaweiGameService.CheckUpdate(new CheckUpdateListener(statusSetter));
        Debug.LogError($"检查更新流程完成");
        yield return new WaitUntil(() => _flowStatus is GameFlowStatus.LoggingIn);
        Debug.LogError($"进入登录流程");
    }
    
    // 助手协程：用于在重试前等待一段时间
    private IEnumerator RetryAfterDelay(float delay)
    {
        MessageSystem.Instance.ShowTip($"等待 {delay} 秒后重试...");
        yield return new WaitForSeconds(delay);
    
        // 🔑 关键：重新启动初始化流程
        StartCoroutine(InitializeGameService());
    }
    
    /// <summary>
    /// 模拟加载进度（确保最小加载时间）
    /// </summary>
    private IEnumerator SimulateLoadingProgress()
    {
        loadStartTime = Time.time;  // 先设置开始时间
        Loading.GetComponent<CanvasGroup>().DOFade(1, 0.1f);
       
        RectTransform sliderBackground = progressSlider.transform.GetChild(0).GetComponent<RectTransform>();
        Vector3 localStart = new Vector3(sliderBackground.rect.xMin, 0, 0);
        Vector3 localEnd = new Vector3(sliderBackground.rect.xMax, 0, 0);
        
        Vector3 worldStart = sliderBackground.TransformPoint(localStart);
        Vector3 worldEnd = sliderBackground.TransformPoint(localEnd);

        float startY = rollingObject.position.y;
        
        float elapsedTime = 0;
        float progress = 0;
        
        while (progress < 1f)
        {
            elapsedTime = Time.time - loadStartTime;
            progress = Mathf.Clamp01(elapsedTime / 4f);
            
            progressSlider.value = progress;
            Vector3 currentPos = Vector3.Lerp(worldStart, worldEnd, progress);
            currentPos.y = startY;
            rollingObject.position = currentPos;
            rollingObject.localEulerAngles = new Vector3(0, 0, -progress * 360f);
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
        
        yield return AssetBundleLoader.SharedInstance.LoadMaterialResource(
            "materials",
            "lizi01"); 
        
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
        Debug.Log("登录完成, 当前状态" + _flowStatus );
        yield return new WaitUntil(() => _flowStatus is GameFlowStatus.GetGamePlayer);
        Player _player = null;
        HuaweiGameService.GetGamePlayer(new GetGamePlayerListener(statusSetter, player=> _player = player));
        Debug.Log("获取用户信息, 当前状态" + _flowStatus );
        yield return new WaitUntil(() => _flowStatus is GameFlowStatus.GamePlayerSave);
        AppPlayerInfo appPlayerInfo = new AppPlayerInfo();
        appPlayerInfo.Rank = "test rank";
        appPlayerInfo.Area = "test area";
        appPlayerInfo.Role = GameDataManager.Instance.UserData.UserName;
        appPlayerInfo.Sociaty = "sociaty";
        appPlayerInfo.PlayerId = _player.PlayerId;
        appPlayerInfo.OpenId = _player.OpenId;
        Game.self.Accounts.UserId = _player.OpenId;
        Game.self.Accounts.IsLogin = true;
        
        Debug.LogFormat("登录华为安卓用户时的数据: {0}", JsonConvert.SerializeObject(appPlayerInfo));
        HuaweiGameService.SavePlayerInfo(appPlayerInfo.ConvertToJavaObject(), new SavePlayerInfoListener(statusSetter));
        Debug.Log("数据上报完成, 当前状态" + _flowStatus );
        yield return new WaitUntil(() => _flowStatus is GameFlowStatus.Ready);
    
    }

    /// <summary>
    /// 异步加载主场景
    /// </summary>
    private IEnumerator LoadMainSceneAsync()
    {
        sceneLoadOperation = SceneManager.LoadSceneAsync("GameLobby");
        sceneLoadOperation.allowSceneActivation = false;

        Debug.Log("开始加载主场景");
        yield return new WaitUntil(() => sceneLoadOperation.progress >= 0.9f&&progressSlider.value>=1f&&isLogined);
        Debug.Log("主场景加载完成");
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
                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                        MessageSystem.Instance.ShowTip("请检查网络");
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
                         MessageSystem.Instance.ShowTip(msg);
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
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    _onLoginCompleted?.Invoke(GameFlowStatus.SilentFailed);
                });
                return;
            }
            string msg = "get login success with signInAccountProxy info: \n";
            msg += String.Format("displayName:{0}, email:{1}, uid:{2}, openId:{3}, unionId:{4}, accessToken:{5}, serverAuthCode:{6}, idToken:{7}",
                signInAccountProxy.DisplayName, signInAccountProxy.Email, signInAccountProxy.Uid, signInAccountProxy.OpenId, signInAccountProxy.UnionId,
                signInAccountProxy.AccessToken, signInAccountProxy.ServerAuthCode, signInAccountProxy.IdToken);
            // MessageSystem.Instance.ShowTip(msg);
           _onLoginCompleted.Invoke(GameFlowStatus.GetGamePlayer);
        }

        public void OnSignOut()
        {
            string msg = "sign out success.";
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                MessageSystem.Instance.ShowTip(msg);
            });
        }

        public void OnFailure(int code, string message)
        {
            string msg = "account method failed, code:" + code + " message:" + message;
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                MessageSystem.Instance.ShowTip(msg);
                _onLoginCompleted?.Invoke(GameFlowStatus.SilentFailed);
            });
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
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    _onGetPlayerCompleted?.Invoke(GameFlowStatus.GetGamePlayerFailed);
                    MessageSystem.Instance.ShowTip("用户信息为空,请检查！");
                });
                return;
            }
            var msg = "getGamePlayer succeed. \n";
            msg += string.Format(
                "displayName:{0}, playerId:{1}, playerSign:{2}, openId:{3}, unionId:{4}, openIdSign:{5}, accessToken:{6}",
                player.DisplayName, player.PlayerId, player.PlayerSign, player.OpenId, player.UnionId, player.OpenIdSign, player.AccessToken
            );
            _owner?.Invoke(player);
            _onGetPlayerCompleted?.Invoke(GameFlowStatus.GamePlayerSave);
        }

        public void OnFailure(int code, string message)
        {
            var msg = "getCurrentPlayer failed, code:" + code + " message:" + message;
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                _onGetPlayerCompleted?.Invoke(GameFlowStatus.GetGamePlayerFailed);
                MessageSystem.Instance.ShowTip(msg);
            });
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
            _onSavePlayerInfoCompleted?.Invoke(GameFlowStatus.Ready);
        }
        
        public void OnFailure(int code, string message)
        {
            _onSavePlayerInfoCompleted?.Invoke(GameFlowStatus.Ready);
        }
    }

}
