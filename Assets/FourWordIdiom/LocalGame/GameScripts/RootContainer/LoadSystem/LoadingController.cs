using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
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
    [SerializeField] private Image bgImage;
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

    // private void Awake()
    // {
        // loadingHintText.text = "";
        //loadingHintText.transform.GetChild(0).GetComponent<Text>().text = "";
    // }

    private void OnEnable()
    {
        // UnityMainThreadDispatcher.Instance();
        StartCoroutine(InitializeLoadingProcess());
        // Sprite bgSprite = AssetBundleLoader.SharedInstance.GetSpriteFromBundle("background_bg", "background");
        //
        // if (bgSprite != null)
        // {
        //     // 4. 【核心一步】把图塞回 Image 组件
        //     bgImage.sprite = bgSprite;
        //     
        //     // 可选：为了视觉效果，可以让颜色从黑渐变到白，或者由透明变不透明
        //     bgImage.color = Color.white; 
        // }
        // else
        // {
        //     Debug.LogError("背景图加载失败！");
        // }
    }
    
    /// <summary>
    /// 初始化加载流程
    /// </summary>
    IEnumerator InitializeLoadingProcess()
    {
        loadStartTime = Time.time;
        StartCoroutine(GameInit());
        yield return new WaitForSeconds(0.05f);
        StartCoroutine( SetupRandomLoadingHint());
        GameDataManager.Instance.LoadPlayerProfile();
        yield return new WaitForSeconds(0.05f);
        StartCoroutine(LoadGameProcess());
        #if UNITY_EDITOR
        isLogined = true;
        yield break;
        #endif
        Game.self.Accounts.Init(0.01f);
        yield return null;
        bool isLoginProcessFinished = false; // 用于控制协程等待的标志位
        bool isLoginSuccess = false;       // 用于记录结果
        string wxCode = null;
        Debug.Log("等待用户授权隐私协议并登录微信...");
        Game.self.Accounts.Login(code =>
        {
            wxCode = code;
            isLoginProcessFinished = true;
        });
        yield return new WaitUntil(() => isLoginProcessFinished);
        isLoginProcessFinished = false;
        // 3. 开始微信登录流程
        if (!string.IsNullOrEmpty(wxCode))
        {
                // 2. 拿到 Code 后，调用 LoginApi 发送给服务器
                StartCoroutine( APIGateway.Instance.LoginApi.WechatLogin(wxCode, (response) => 
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
        }
        else
        {
                Debug.LogError("微信 SDK 登录失败，未获取到 Code");
                isLoginSuccess = false;
                isLoginProcessFinished = true; // 即使失败也要标记完成，否则会死锁
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
        
        isLogined = true;
        Debug.Log("登录数据状态完成！ " + isLogined);
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
    #endregion

    /// <summary>
    /// 设置随机加载提示
    /// </summary>
    private IEnumerator SetupRandomLoadingHint()
    {
        int id=Random.Range(1,12);
        string sid = id < 10 ? "0" + id : id.ToString();
        string tipTxt = MultilingualManager.Instance.GetString("loadText" + sid);
        loadingHintText.text = tipTxt;
        loadingHintText.transform.GetChild(0).GetComponent<Text>().text = MultilingualManager.Instance.GetString("loadText101");

        yield return new WaitUntil(() => Launch.FontTask.IsCompleted);
        Font font = AssetBundleLoader.SharedInstance.LoadFont("stagefonts", "FZKTK");
        loadingHintText.font = font;
        loadingHintText.transform.GetChild(0).GetComponent<Text>().font = font;
    }
    
    /// <summary>
    /// 模拟加载进度（确保最小加载时间）
    /// </summary>
    private IEnumerator LoadGameProcess()
    {
        loadStartTime = Time.time;  // 先设置开始时间
        Loading.GetComponent<CanvasGroup>().DOFade(1, 0.1f);
        RectTransform sliderBackground = progressSlider.transform.GetChild(0).GetComponent<RectTransform>();
        Vector3 localStart = new Vector3(sliderBackground.rect.xMin, 0, 0);
        Vector3 localEnd = new Vector3(sliderBackground.rect.xMax, 0, 0);
        Vector3 worldStart = sliderBackground.TransformPoint(localStart);
        Vector3 worldEnd = sliderBackground.TransformPoint(localEnd);
        float startY = rollingObject.position.y;
        
        while (Launch.ResourceLoadingTask != null && !Launch.ResourceLoadingTask.IsCompleted)
        {
            // 如果下载失败，报错退出
            if (Launch.ResourceLoadingTask.IsFaulted) { /* 报错处理... */ Debug.LogError("资源下载失败"); yield break; }

            // 🔥 这里就是你要的“让进度条先跑”
            // 我们让进度条在 0% ~ 30% 之间反复横跳，或者缓慢增长
            // 告诉玩家：“别急，正在从云端拉取数据...”
            float fakeProgress = Mathf.PingPong(Time.time * 0.3f, 0.3f); 
            UpdateLoadingUI(fakeProgress, worldStart, worldEnd, startY);
            yield return null; // 等待下一帧，继续检查
        }
        
        Debug.Log("资源就绪，开始加载场景...");
        AsyncOperation sceneOp = null;
#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("GameLobby t:Scene");
        if (guids.Length > 0) {
            string scenePath = AssetDatabase.GUIDToAssetPath(guids[0]);
            sceneOp = EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));
        } else {
            Debug.LogError("找不到场景: GameLobby");
            yield break;
        }
#else
        sceneOp = SceneManager.LoadSceneAsync("GameLobby");
#endif
        
        sceneOp.allowSceneActivation = false; // 禁止自动跳转
        StageHexController.Instance.LoadPackInfos();
        float minDuration = 2f;
        while (true)
        {
            // 1. 计算【时间进度】：从 loadStartTime 开始算了多久了？ 比如过了 2秒，progress 就是 0.5；过了 4秒，progress 就是 1.0
            float timeProgress = Mathf.Clamp01((Time.time - loadStartTime) / minDuration);
            // 2. 计算【场景进度】：Unity 真实加载到哪了？ sceneOp.progress 最大 0.9，所以除以 0.9 归一化到 0~1
            float sceneProgress = Mathf.Clamp01(sceneOp.progress / 0.9f);
            // 3. 🔥【核心逻辑】取两者的最小值！
            float finalProgress = Mathf.Min(timeProgress, sceneProgress);
            // 4. 登录卡点 (如果没登录，死活不让到 100%)
            if (!isLogined && finalProgress >= 1f)
            {
                finalProgress = 0.99f;
            }
            UpdateLoadingUI(finalProgress, worldStart, worldEnd, startY);
            if (timeProgress >= 1f && sceneOp.progress >= 0.9f && isLogined)
            {
                UpdateLoadingUI(1f, worldStart, worldEnd, startY);
                break; // 所有条件满足，跳出循环
            }
            yield return null;
        }
        Debug.Log("所有加载项完成，进入游戏...");
        sceneOp.allowSceneActivation = true;
        yield return null;

        Loading.GetComponent<CanvasGroup>().DOFade(0, 0.5f)
            .OnComplete(() => { gameObject.SetActive(false); });
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
        yield return new WaitUntil(() => sceneLoadOperation.progress >= 0.9f && progressSlider.value >= 1f && CheckResourceLoadingTask());
        Debug.Log("主场景加载完成");

    }

    private bool CheckResourceLoadingTask()
    {
        return (Launch.ResourceLoadingTask != null && Launch.ResourceLoadingTask.IsCompleted) && isLogined;
    }
    
    private void UpdateLoadingUI(float progress, Vector3 start, Vector3 end, float y)
    {
        progressSlider.value = progress;
        Vector3 currentPos = Vector3.Lerp(start, end, progress);
        currentPos.y = y;
        rollingObject.position = currentPos;
        rollingObject.localEulerAngles = new Vector3(0, 0, -progress * 360f);
    }
}