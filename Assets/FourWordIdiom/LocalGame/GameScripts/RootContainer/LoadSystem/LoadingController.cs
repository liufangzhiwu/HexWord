using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using Newtonsoft.Json;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
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
    
    [Header("UI组件引用")] 
    [SerializeField] private Text loadingHintText; // 加载提示文本
    [SerializeField] private Slider progressSlider; // 进度条组件
    [SerializeField] private GameObject Loading; // 进度条组件

    [SerializeField] private RectTransform rollingObject; // 滚动的方块 (Image)

    [Header("加载配置")]
    [SerializeField]
    private int randomHintCount = 20; // 随机提示数量

    private AsyncOperation sceneLoadOperation; // 场景加载操作
    private float loadStartTime; // 加载开始时间


    private LoginResponse loginResponse; // 登录响应数据
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

    private async void Start()
    {
        await WordVocabularyManager.Instance.LoadEntriesAsync();
        Debug.Log("开始下载场景包...");
        
        Task taskScene = AssetBundleLoader.SharedInstance.PreloadSingleBundle("scene_gamelobby");
        await Task.WhenAll(taskScene);
        Debug.Log("所有资源下载完毕！");
    }

    /// <summary>
    /// 初始化加载流程
    /// </summary>
    IEnumerator InitializeLoadingProcess()
    {
        InitializeLocalization();
        SetupRandomLoadingHint();
        loadStartTime = Time.time;
        GameDataManager.Instance.LoadPlayerProfile();
        StartCoroutine(GameInit());
        
        yield return new WaitForSeconds(0.05f);
        SetupRandomLoadingHint();
        yield return new WaitForSeconds(0.05f);
        StartCoroutine(LoadingSequence());
        #if UNITY_EDITOR
        yield break;
        #endif
        Game.self.Accounts.Init(0.01f);
        yield return new WaitForSeconds(0.05f);
        Game.self.Accounts.VerifyPlayer();
        // yield return new WaitUntil(() => Game.self.Accounts.IsAuthorized);
        // 3. 开始微信登录流程
        bool isLoginProcessFinished = false; // 用于控制协程等待的标志位
        bool isLoginSuccess = false;       // 用于记录结果
        Debug.Log("开始调用微信 SDK 登录...");
        Game.self.Accounts.Login((code) =>
        {
            if (!string.IsNullOrEmpty(code))
            {
                // 2. 拿到 Code 后，调用 LoginApi 发送给服务器
                StartCoroutine( APIGateway.Instance.LoginApi.WechatLogin(code, (response) => 
                {
                    if (response != null)
                    {
                        Debug.Log("服务器验证通过，Token 已保存");
                        isLoginSuccess = true;
                    }
                    else
                    {
                        Debug.LogError("服务器验证失败");
                        isLoginSuccess = false;
                    }
                    isLoginProcessFinished = true;
                }));
            }
            else
            {
                Debug.LogError("微信 SDK 登录失败，未获取到 Code");
                isLoginSuccess = false;
                isLoginProcessFinished = true; // 即使失败也要标记完成，否则会死锁
            }
        });
        float loginTimeout = 3.0f; 
        float timer = 0f;
        while (!isLoginProcessFinished && timer < loginTimeout)
        {
            timer += Time.deltaTime;
            yield return null;
        }
        if (timer >= loginTimeout)
        {
            Debug.LogWarning("登录超时，跳过等待，进入离线模式");
            isLoginProcessFinished = true;
            isLoginSuccess = false;
        }
        if (isLoginSuccess)
        {
            Debug.Log("登录流程全部完成，开始加载用户存档...");
            yield return APIGateway.Instance.LoginApi.GetUserData(LoadUserData);
        }
        else
        {
            // 弹窗提示用户：登录失败，请重试
            Debug.LogWarning("⚠️ 登录失败/网络错误/超时：进入离线模式");
        }
        // yield return APIGateway.Instance.LoginApi.FetchUserProfile((res) =>
        // {
        //     if (res != null)
        //     {
        //         Debug.Log("获取用户信息成功！" + res.uid);
        //         //GameDataManager.Instance.UserData.Zenlevel = res.zen_level;
        //     }
        // });
    }

    private IEnumerator GameInit()
    {
        Debug.Log("打印一下" + Game.self);
        yield return new WaitForSeconds(0.05f);
        Game.self.InitGame();
    }

    #region 服务器数据处理
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
        loadingHintText.text = MultilingualManager.Instance.GetString("loadText" + sid);
        loadingHintText.transform.GetChild(0).GetComponent<Text>().text = MultilingualManager.Instance.GetString("loadText101");
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
        // yield return AssetBundleLoader.SharedInstance.LoadAtlas(
        //     "ui_universal",
        //     "UI_Universal");
        //
        // //LoadFont();
        // // 加载字体资源
        // Font mainFont = AssetBundleLoader.SharedInstance.LoadFont(
        //     "stagefonts",
        //     "FZKTK");
        // //loadingHintText.font = mainFont;
        //
        // // 并行加载其他关键资源
        // yield return AssetBundleLoader.SharedInstance.LoadAtlas(
        //     "effect_sprite",
        //     "trailAltas");
        //
        // yield return AssetBundleLoader.SharedInstance.LoadMaterialResource(
        //     "effectsitemmats",
        //     "Circle");

        //预加载关卡文件
        StageHexController.Instance.LoadPackInfos();
        // 开始场景加载
        yield return LoadMainSceneAsync();
    }
    
    /// <summary>
    /// 异步加载主场景
    /// </summary>
    private IEnumerator LoadMainSceneAsync()
    {
#if UNITY_EDITOR
        Debug.Log("[Editor] 正在编辑器模式下切换场景...");
        string[] guids = AssetDatabase.FindAssets("GameLobby t:Scene");
        if (guids.Length == 0)
        {
            Debug.LogError($"找不到场景文件: GameLobby");
            yield break;
        }

        string scenePath = AssetDatabase.GUIDToAssetPath(guids[0]);
        sceneLoadOperation = EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
        sceneLoadOperation = SceneManager.LoadSceneAsync("GameLobby");
#endif
        sceneLoadOperation!.allowSceneActivation = false;
        yield return new WaitUntil(() => sceneLoadOperation.progress >= 0.9f&&progressSlider.value>=1f&&isLogined);
        Debug.Log("主场景加载完成");

    }
}