using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using FourWordIdiom.LocalGame.GameScripts.Controller.SaveSystem;
using Middleware;
using Newtonsoft.Json;
using UnityEngine;
#if UNITY_HUAWEI
using UnityEngine.HuaweiAppGallery;
using UnityEngine.HuaweiAppGallery.Listener;
using UnityEngine.HuaweiAppGallery.Model;
#endif
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
    public static LoadingController self;

    [Header("UI组件引用")]
    [SerializeField] private Text loadingHintText;    // 加载提示文本
    [SerializeField] private Slider progressSlider;   // 进度条组件
    [SerializeField] private GameObject Loading;      // 进度条容器
    [SerializeField] private RectTransform rollingObject;   // 滚动的方块 (Image)
    private float _objectRadius;    // 方块半径

    private AsyncOperation sceneLoadOperation;        // 场景加载操作
    private float loadStartTime;                      // 加载开始时间
    [SerializeField] private float minLoadingTime = 1.5f;

    private UserData serverData;               // 服务器数据
    private UserData selectData;               // 选择的数据
    private LoginResponse loginResponse;       // 登录响应数据
    private bool isLogined = false;

    private UserData serverUserData;          // 解析后的主数据
    private FishUserSaveData serverFishData;      // 解析后的鱼数据
    private ButterflyData serverButterflyData;    // 解析后的蝴蝶数据
    private OverallRankData serverOverallRankData;// 解析后总榜数据
    private AchieveSaveDatas serverAchieveSaveDatas;// 解析后成就数据

    private bool IsLocalDataNull;// 本地数据是否为空

    public float loginStart;
    public float loginTimeout;

    private void Awake()
    {
        self = this;
        loadingHintText.text = "";
    }

    private void Start()
    {
        _objectRadius = (rollingObject.rect.width * rollingObject.lossyScale.x) / 2f;
    }

    private void OnEnable()
    {
        StartCoroutine(InitBg());
        UnityMainThreadDispatcher.Instance();
        StartLoading();
    }

    private IEnumerator InitBg()
    {
        IsLocalDataNull = GameDataManager.Instance.UserData.LocalDataIsNull();
        this.transform.GetComponent<Image>().color = Color.black;
        yield return AdvancedBundleLoader.SharedInstance.LoadAtlas(
            "ui_theme",
            "UI_Theme");
        yield return null;

        ThemeDataItem curDataItem = ThemeManager.Instance.GetThemeDataItem(GameDataManager.Instance.UserData.userthemeid);
        Sprite sprite = GetSprite(curDataItem.iconName);
        this.transform.GetComponent<Image>().sprite = sprite;
        this.transform.GetComponent<Image>().color = Color.white;
    }


    public void StartLoading()
    {
        StartCoroutine(InitializeLoadingProcess());
    }


    private void OnApplicationFocus(bool focusStatus)
    {
        HandleFocusChange(focusStatus);
    }

    private void HandleFocusChange(bool hasFocus)
    {
        // 应用进入后台（用户可能正在华为登录界面操作）
        if (!hasFocus)
        {
            Debug.Log("应用进入后台，暂停登录超时计时");
        }
        else
        {
            // 回到前台，重置计时窗口（超时时间不变）
            loginStart = Time.time;
            Debug.Log("应用回到前台，重置登录超时计时");
        }
    }

    /// <summary>
    /// 初始化加载流程
    /// </summary>
    IEnumerator InitializeLoadingProcess()
    {
        SetupRandomLoadingHint();


#if UNITY_HUAWEI&&!UNITY_EDITOR
            HuaweiGameService.AppInit();
#endif

        loadStartTime = Time.time;
        // 本地化
        MultilingualManager.Instance.LoadLocalization();
        LoadWordVocabulary();
        Game.self.InitGame();
        yield return new WaitForSeconds(0.5f);

        // ================= 等待登录（基于 LoginState 枚举）=================
        loginTimeout = 10f;        // ← 超时时间保持不变
        loginStart = Time.time;

        while (true)
        {
            var state = Game.self.State;

            // 1) 登录成功 → 跳出
            if (state == LoginState.Success) break;

            // 3) None / Logging / Timeout → 继续等待
            float elapsed = Time.time - loginStart;
            if (elapsed >= loginTimeout)
            {
                if (!Application.isFocused)
                {
                    // 应用在后台，说明用户正在华为登录界面输入账号 → 重置计时
                    loginStart = Time.time;
                }
                else
                {
                    // 回到游戏仍超时：不报错，只打日志，继续等用户完成登录
                    Debug.LogWarning("[Loading] 登录等待已超过10秒，但状态仍为 " + state + "，继续等待用户完成登录");
                    loginStart = Time.time; // 重置，避免日志刷屏
                }
                
                // 2) 明确失败 / 用户取消 → 跳出，走错误流程
                if (state == LoginState.Failed || state == LoginState.Canceled)
                    break;
            }

            yield return new WaitForSeconds(1f);
        }
        // =================================================================

        if (Game.self.State != LoginState.Success)
        {
            Debug.LogError("登录失败，State = " + Game.self.State);
            Game.self.ShowLoginErrorPanel();
            yield break;
        }

        yield return APIGateway.Instance.LoginApi.Login((res) =>
        {
            if (res != null)
            {
                loginResponse = res as LoginResponse;
            }
            isLogined = true;
        });

        yield return new WaitUntil(() => isLogined);

        yield return APIGateway.Instance.LoginApi.GetUserData(LoadUserData);
        yield return APIGateway.Instance.LoginApi.FetchUserProfile((res) =>
        {
            if (res != null)
            {
                Debug.Log("获取用户信息成功！" + res.uid);
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
            AnalyticMgr.Login();
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
                Debug.Log("OverallRank 开始解析" + response.ExtraData.OverallRank);
                if (!string.IsNullOrEmpty(response.ExtraData.OverallRank))
                    serverOverallRankData = JsonConvert.DeserializeObject<OverallRankData>(response.ExtraData.OverallRank);
                if (!string.IsNullOrEmpty(response.ExtraData.AchieveSaveDatas))
                    serverAchieveSaveDatas = JsonConvert.DeserializeObject<AchieveSaveDatas>(response.ExtraData.AchieveSaveDatas);
                Debug.Log("OverallRank 解析成功");
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
        if (IsLocalDataNull)
        {
            UserServerData();
            Debug.Log("本地用户数据为空，直接使用服务器数据, 服务器数据同步完成！");
            return;
        }

        // A. 优先比对关卡进度
        if (serverUserData.CurrentChessStage != GameDataManager.Instance.UserData.CurrentChessStage)
        {
            if (serverUserData.CurrentChessStage > GameDataManager.Instance.UserData.CurrentChessStage)
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
        else if (serverUserData.overallZenScore != GameDataManager.Instance.UserData.overallZenScore)
        {
            if (serverUserData.overallZenScore > GameDataManager.Instance.UserData.overallZenScore)
            {
                UserServerData();
                Debug.Log("服务器禅意分更多，使用服务器数据, 服务器数据同步完成！");
            }
            else
            {
                UserLocalData();
                Debug.Log("本地禅意分更多，使用本地数据");
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
        GameDataManager.HasSyncedThisSession = true;
        ModifyUserWithABtest();
        StartCoroutine(LoadingSequence());
        AnalyticMgr.Login();
    }

    private void UserServerData()
    {
        GameDataManager.Instance.UserData.InitData(serverUserData);
        if (serverFishData != null)
            GameDataManager.Instance.FishUserSave.InitData(serverFishData);
        if (serverButterflyData != null)
            GameDataManager.Instance.ButterflyData.InitData(serverButterflyData);
        if (serverOverallRankData != null)
            GameDataManager.Instance.OverallRank.InitData(serverOverallRankData);
        if (serverAchieveSaveDatas != null)
            GameDataManager.Instance.AchieveSaveDataList.InitData(serverAchieveSaveDatas);

        GameDataManager.Instance.ClearAllLevelProgressFiles();
        GameDataManager.HasSyncedThisSession = true;
        GameDataManager.Instance.SetInitailized(true);
        ModifyUserWithABtest();
        StartCoroutine(LoadingSequence());
        AnalyticMgr.Login();
    }


    // 处理ABtest数据
    public void ModifyUserWithABtest()
    {
        UserData user = GameDataManager.Instance.UserData;
        user.PlayerId = loginResponse.uid;
        try
        {
            user.ABName = (string)loginResponse.abtest.GetValueOrDefault("pack_name", "0");
            Dictionary<string, object> parameterValues = new Dictionary<string, object>();
            if (loginResponse.abtest.TryGetValue("parameter_value", out object value))
            {
                parameterValues = JsonConvert.DeserializeObject<Dictionary<string, object>>(value.ToString());
            }
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
        }
        catch (Exception ex)
        {
            Debug.LogError("ABtest参数解析失败！" + ex.Message);
        }

        if (loginResponse.is_version_upgraded)
        {
            GameDataManager.Instance.UserData.HasUnclaimedUpdateJoin = true;
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
        string key = LoadTextManager.Instance.GetNextText();
        string des = MultilingualManager.Instance.GetString(key, "hudie");
        if (des.Contains(" "))
        {
            des = des.Replace(" ", "\u00A0");
        }
        loadingHintText.text = des;
    }

    /// <summary>
    /// 主加载序列协程
    /// </summary>
    private IEnumerator LoadingSequence()
    {
        // 4. 资源加载与进度模拟（并行）
        Coroutine resourceLoad = StartCoroutine(LoadEssentialResources());
        Coroutine simProgress = StartCoroutine(SimulateLoadingProgress());

        yield return resourceLoad;
        yield return simProgress;

        // 保证最短加载时间
        float elapsed = Time.time - loadStartTime;
        if (elapsed < minLoadingTime)
            yield return new WaitForSeconds(minLoadingTime - elapsed);

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
        Loading.GetComponent<CanvasGroup>().DOFade(0, 0.1f);
    }

    /// <summary>
    /// 加载核心游戏资源
    /// </summary>
    private IEnumerator LoadEssentialResources()
    {
        Debug.Log("开始预加载游戏资源");

        yield return AdvancedBundleLoader.SharedInstance.LoadAtlas(
           "ui_universal",
           "UI_Universal");

        //LoadFont();
        // 加载字体资源
        Font mainFont = AdvancedBundleLoader.SharedInstance.LoadFont(
             "stagefonts",
             "FZKTK");
        //loadingHintText.font = mainFont;

        // 并行加载其他关键资源
        yield return AdvancedBundleLoader.SharedInstance.LoadAtlas(
            "effect_sprite",
            "trailAltas");

        yield return AdvancedBundleLoader.SharedInstance.LoadMaterialResource(
            "effectsitemmats",
            "Circle");

        yield return AdvancedBundleLoader.SharedInstance.LoadMaterialResource(
            "materials",
            "lizi01");

        //预加载关卡文件
        StageHexController.Instance.LoadPackInfos();
        ChessStageController.Instance.Initialized();
        // 开始场景加载
        yield return LoadMainSceneAsync();
    }

    private Sprite GetSprite(string spriteName)
    {
        return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(spriteName, "UI_Theme");
    }

    /// <summary>
    /// 异步加载主场景
    /// </summary>
    private IEnumerator LoadMainSceneAsync()
    {
        sceneLoadOperation = SceneManager.LoadSceneAsync("GameLobby");
        sceneLoadOperation.allowSceneActivation = false;
        Debug.Log("开始加载主场景");
        yield return new WaitUntil(() => sceneLoadOperation.progress >= 0.9f && progressSlider.value >= 1f && isLogined);
        Debug.Log("主场景加载完成");
    }

}