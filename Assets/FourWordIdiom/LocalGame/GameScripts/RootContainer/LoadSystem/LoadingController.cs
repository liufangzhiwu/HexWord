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
using UnityEngine.Scripting;
using UnityEngine.UI;
using Game = Middleware.Game;
using Random = UnityEngine.Random;

public enum GameFlowStatus 
{
    NotStarted,
    
    // --- 1. 初始化阶段 ---
    Initializing,
    InitFailed,
    
    // --- 2. 更新检查阶段 ---
    CheckingUpdate,
    UpdateRequired, // 需要更新，等待用户操作
    
    // --- 3. 登录阶段 ---
    LoginReady,
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
    public static LoadingController Instance;
    
    
    [Header("UI组件引用")]
    [SerializeField] private Text loadingHintText;    // 加载提示文本
    [SerializeField] private Slider progressSlider;   // 进度条组件
    [SerializeField] private GameObject Loading;   // 进度条组件
    [SerializeField] private RectTransform rollingObject;   // 滚动的方块 (Image)
    
    private AsyncOperation sceneLoadOperation;        // 场景加载操作
    private float loadStartTime;                      // 加载开始时间
    
    
    private LoginResponse loginResponse;       // 登录响应数据
    private bool isLogined = false;
    
    private UserData serverUserData;          // 解析后的主数据
    private FishUserSaveData serverFishData;      // 解析后的鱼数据 (假设你的类名叫 FishSaveData)
    private ButterflyData serverButterflyData;// 解析后的蝴蝶数据 (假设你的类名叫 ButterflyData)

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void OnEnable()
    {
        StartCoroutine(InitializeLoadingProcess());
    }

    /// <summary>
    /// 初始化加载流程
    /// </summary>
    private IEnumerator InitializeLoadingProcess()
    {
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
#endif
        StartCoroutine(SimulateLoadingProgress());
        yield return new WaitUntil(() => Launch.Instance.flowStatus is GameFlowStatus.LoggingIn);
        Debug.Log($"进入登录流程 " + Launch.Instance.flowStatus);
        // 登录开始
        StartCoroutine( LoadHuaweiGameLogin());
        yield return new WaitUntil(() => Launch.Instance.flowStatus is GameFlowStatus.Ready);
        //设置登录用户ID（需要等待游戏数据获取后）
        AnalyticMgr.SetLoginUser(Game.self.Accounts.UserId);
        LoadWordVocabulary();
        yield return APIGateway.Instance.LoginApi.Login((res)=> 
        {
            if (res != null)
            {
                loginResponse = res as LoginResponse;
            }
            isLogined = true;
        });
        yield return new WaitUntil(() => isLogined);
        yield return APIGateway.Instance.LoginApi.GetUserData(LoadUserData);
        // yield return APIGateway.Instance.LoginApi.FetchUserProfile((res) =>
        // {
        //     if (res != null)
        //     {
        //         Debug.Log("获取用户信息成功！"+ res.uid);
        //         //GameDataManager.Instance.UserData.Zenlevel = res.zen_level;
        //     }
        // });
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
            Debug.Log("UserData 开始解析");
            serverUserData = JsonConvert.DeserializeObject<UserData>(response.UserData);
            Debug.Log("UserData 解析成功");
            if (response.ExtraData != null)
            {
                Debug.Log("FishUserSave 开始解析");
                if (!string.IsNullOrEmpty(response.ExtraData.FishUserSave))
                    serverFishData = JsonConvert.DeserializeObject<FishUserSaveData>(response.ExtraData.FishUserSave);
                Debug.Log("FishUserSave 解析成功");
                Debug.Log("Butterfly 开始解析" + response.ExtraData.Butterfly);
                if (!string.IsNullOrEmpty(response.ExtraData.Butterfly))
                    serverButterflyData = JsonConvert.DeserializeObject<ButterflyData>(response.ExtraData.Butterfly);
                Debug.Log("Butterfly 解析成功");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"解析服务器数据失败: {ex.Message} {ex.ToString()} ，回退到本地数据");
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
            if (localTime <= serverTime)
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
        GameDataManager.Instance.SetInitailized(true);
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

        yield return new WaitUntil(() => isLogined);
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

    public void TryAgainClick()
    {
        StartCoroutine(LoadHuaweiGameLogin());
    }

    private IEnumerator LoadHuaweiLogin()
    {
        bool isLoginSuccess = false;
        Debug.Log("开始华为登录流程...");
        var hwAccount = Game.self.Accounts as Middleware.Account_huaweiandroid;
     
        bool callbackReceived = false;
      
        hwAccount.OnLoginComplete = (success, authAccount) =>
        {
            isLoginSuccess = success;
            callbackReceived = true;
        };
        yield return new WaitUntil(() => callbackReceived);
    }
    private IEnumerator LoadHuaweiGameLogin()
    {
        Action<GameFlowStatus> statusSetter = (status) => { Launch.Instance.flowStatus = status; };
        // HuaweiGameService.SilentSignIn(new SilentLoginListener(statusSetter,1));
        // if (Launch.Instance.flowStatus == GameFlowStatus.SilentFailed)
        // {
        //     HuaweiGameService.Login(new SilentLoginListener(statusSetter,2));
        // }
        var hwAccount = Game.self.Accounts as Middleware.Account_huaweiandroid;
        hwAccount!.Login();
        hwAccount.OnLoginComplete = (success, authAccount) =>
        {
            if(success)
                Launch.Instance.flowStatus = GameFlowStatus.GetGamePlayer;
            
            // HuaweiGameService.
        };
        yield return new WaitUntil(() => Launch.Instance.flowStatus is GameFlowStatus.GetGamePlayer);
        Debug.Log("登录完成, 当前状态" + Launch.Instance.flowStatus );
        Player player = null;
        HuaweiGameService.GetGamePlayer(new GetGamePlayerListener(statusSetter, p=> player = p));
        yield return new WaitUntil(() => Launch.Instance.flowStatus is GameFlowStatus.GamePlayerSave && player != null);
        Debug.Log("获取用户信息, 当前状态" + Launch.Instance.flowStatus);
            AppPlayerInfo appPlayerInfo = new AppPlayerInfo();
            appPlayerInfo.Rank = "test rank";
            appPlayerInfo.Area = "test area";
            appPlayerInfo.Role = (GameDataManager.Instance?.UserData != null) 
                ? GameDataManager.Instance.UserData.UserName 
                : "UnknownRole";
            appPlayerInfo.Sociaty = "sociaty";
            appPlayerInfo.PlayerId = player.PlayerId;
            appPlayerInfo.OpenId = player.OpenId;
            Debug.LogFormat("登录华为安卓用户时的数据: {0}", JsonConvert.SerializeObject(appPlayerInfo));
            if (Game.self?.Accounts != null)
            {
                // Game.self.Accounts.UserId = player.OpenId;
                Game.self.Accounts.IsLogin = true;
            }
            else
            {
                Debug.LogWarning("本地 Account 为空");
            }
        HuaweiGameService.SavePlayerInfo(appPlayerInfo.ConvertToJavaObject(), new SavePlayerInfoListener(statusSetter));
        yield return new WaitUntil(() => Launch.Instance.flowStatus is GameFlowStatus.Ready);
        Debug.Log("数据上报完成, 当前状态" + Launch.Instance.flowStatus);
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

    private void OnDestroy()
    {
        HuaweiGameService.HideFloatWindow();
    }

    
    private class SilentLoginListener : ILoginListener
    {
        private readonly Action<GameFlowStatus> _onLoginCompleted;
        private int _step;

        public SilentLoginListener(Action<GameFlowStatus> onLoginCompleted, int step)
        {
            _onLoginCompleted = onLoginCompleted;
            _step = step;
        }
        public void OnSuccess(SignInAccountProxy signInAccountProxy)
        {
            Debug.LogWarning("登录成功？" + signInAccountProxy);
            if (signInAccountProxy == null)
            {
                _onLoginCompleted?.Invoke(GameFlowStatus.SilentFailed);
                return;
            }
            string msg = "get login success with signInAccountProxy info: \n";
            msg += String.Format("displayName:{0}, email:{1}, uid:{2}, openId:{3}, unionId:{4}, accessToken:{5}, serverAuthCode:{6}, idToken:{7}",
                signInAccountProxy.DisplayName, signInAccountProxy.Email, signInAccountProxy.Uid, signInAccountProxy.OpenId, signInAccountProxy.UnionId,
                signInAccountProxy.AccessToken, signInAccountProxy.ServerAuthCode, signInAccountProxy.IdToken);
            // MessageSystem.Instance.ShowTip(msg);
            // Debug.Log(msg);
            _onLoginCompleted.Invoke(GameFlowStatus.GetGamePlayer);
        }

        public void OnSignOut()
        {
            Debug.LogWarning("OnSignOut？" );
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                // MessageSystem.Instance.ShowTip(msg);
                Game.self.ShowLoginErrorPanel();
            });
        }

        public void OnFailure(int code, string message)
        {
            string msg = "account method failed, code:" + code + " message:" + message;
            Debug.LogWarning(msg);
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                // MessageSystem.Instance.ShowTip(msg);
                _onLoginCompleted?.Invoke(GameFlowStatus.SilentFailed);
                if (_step == 2) 
                    Game.self.ShowLoginErrorPanel();
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
                    // MessageSystem.Instance.ShowTip("用户信息为空,请检查！");
                });
                return;
            }
            var msg = "getGamePlayer succeed. \n";
            msg += string.Format(
                "displayName:{0}, playerId:{1}, playerSign:{2}, openId:{3}, unionId:{4}, openIdSign:{5}, accessToken:{6}",
                player.DisplayName, player.PlayerId, player.PlayerSign, player.OpenId, player.UnionId, player.OpenIdSign, player.AccessToken
            );
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                _owner?.Invoke(player);
                Debug.Log(msg);
                _onGetPlayerCompleted?.Invoke(GameFlowStatus.GamePlayerSave);
            });
        }

        public void OnFailure(int code, string message)
        {
            var msg = "getCurrentPlayer failed, code:" + code + " message:" + message;
        
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                Debug.LogWarning(msg);
                _onGetPlayerCompleted?.Invoke(GameFlowStatus.GetGamePlayerFailed);
                // MessageSystem.Instance.ShowTip(msg);
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
            Debug.LogWarning($"数据上报失败： {code} - {message}");
            _onSavePlayerInfoCompleted?.Invoke(GameFlowStatus.Ready);
        }
    }
}
