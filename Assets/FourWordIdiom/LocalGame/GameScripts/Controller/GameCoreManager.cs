using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Middleware;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
#if UNITY_IOS
using UnityEngine.iOS;
#endif

public enum PanelState
{
   Null,MainMenuPanel,FinishPanel,GamePanel,
}
public enum GameState
{
    None,
    Lobby,      // 大厅状态
    Gameplay,    // 游戏状态
    Butterfly,
    Fish,
    Shop,
}
/// <summary>
/// 游戏核心管理器（单例模式）
/// 功能：
/// 1. 游戏全局初始化
/// 2. 隐私协议处理
/// 3. 设备信息检测
/// 4. 游戏流程控制
/// </summary>
public sealed class GameCoreManager: MonoBehaviour
{
    #region 单例实现
    public static GameCoreManager Instance;
    
    #endregion
    
    public bool IsTrueAuto;
    public GameObject AutoLevelTalbe;
    public PanelState PanelState=PanelState.Null;
    
    private readonly string[] _lobbyBundles = {  "RootCanvas" };
    private readonly string[] _gameplayBundles  = { "gameplayarea",  "effectsitemmats", "useritems" };
    private readonly string[] _butterflyBundles = new string[] {"Scenes_hudie","Huoqu_hudie", "butterflyhome", "ButterflyBg" };
    private readonly string[] _fishBundles = { "Effect_Fish", "Effect_FishBox", "fishhomescreen", }; // 假设鱼的包
    private readonly string[] _shopBundles  = { "shophomescreen" }; // 假设商店的包
    
    public GameState CurrentState { get; private set; } = GameState.None;
    private bool _isSwitching = false; // 🔒 状态锁，防止并发调用
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 保持广告管理器在场景切换时不销毁
        }
    }
 
    private void Start()
    {
        Application.targetFrameRate = 60; // 平台设置为60帧
        Game.self.InitManagers();
        // Game.self._uiRoot=SystemManager.Instance._uiRoot;
        // 🔥 修复 UI 相机丢失问题
        SetupCanvasCamera();
        
        StartCoroutine(InitializeGameRoutine());
        //StartCoroutine(CheckNetworkConnection());
        AutoLevelTalbe.GetComponent<Toggle>().onValueChanged.AddListener(OnAutoLevelTalbeValueChanged);
        
#if Unity_ShowLog || UNITY_EDITOR
        IsTrueAuto = false;
        AutoLevelTalbe.gameObject.SetActive(false);
        Debug.unityLogger.logEnabled = true;
#else 
        IsTrueAuto = false;
        AutoLevelTalbe.gameObject.SetActive(false);
        Debug.unityLogger.logEnabled = false;
#endif

        AssetBundleLoader.SharedInstance.LoadAtlas("UI_Universal","UI_Universal");
    }

    public async void StartGameFlow()
    {
        await EnterLobbyRoutine(fromGameState: GameState.None);
    }

    public async Task SwitchToState(GameState targetState)
    {
        if (CurrentState == targetState || _isSwitching) return;
        _isSwitching = true; // 上锁
        MessageSystem.Instance.ShowLoadingAnimation();
        Debug.Log($"[State] 正在从 {CurrentState} 切换到 {targetState} ...");

        await ExitCurrentStateRoutine();
        var unloadOp = Resources.UnloadUnusedAssets();
        while (!unloadOp.isDone) await Task.Yield();
        System.GC.Collect();

        Debug.Log("开始进入"+targetState);
        CurrentState = targetState;
        await EnterNewStateRoutine(targetState);
        _isSwitching = false;
        MessageSystem.Instance.HideLoadingAnimation();
    }

    private async Task ExitCurrentStateRoutine()
    {
        switch (CurrentState)
        {
            case GameState.Lobby:
                SystemManager.Instance.HidePanel(PanelType.HeaderSection);
                await Task.Yield();
                SystemManager.Instance.CloseAndDestroyPanel(PanelType.PrimaryInterface);
                SystemManager.Instance.CloseAndDestroyPanel(PanelType.OptionsView);
                SystemManager.Instance.CloseAndDestroyPanel(PanelType.HeadScreen);
                SystemManager.Instance.CloseAndDestroyPanel(PanelType.DebugMenu);
                await Task.Yield();
                UnloadBundles(_lobbyBundles);
                break;

            case GameState.Gameplay:
                SystemManager.Instance.HidePanel(PanelType.HeaderSection);
                await Task.Yield();
                SystemManager.Instance.CloseAndDestroyPanel(PanelType.HexGamePlayArea);
                SystemManager.Instance.CloseAndDestroyPanel(PanelType.StageFinishView);
                SystemManager.Instance.CloseAndDestroyPanel(PanelType.HardView);
                await Task.Yield();
                UnloadBundles(_gameplayBundles);
                break;

            case GameState.Butterfly:
                // 关掉蝴蝶所有子面板
                SystemManager.Instance.CloseAndDestroyPanel(PanelType.ButterflyHome);
                SystemManager.Instance.CloseAndDestroyPanel(PanelType.ButterflyGardenHelp);
                SystemManager.Instance.CloseAndDestroyPanel(PanelType.ButterflyManual);
                await Task.Yield();
                UnloadBundles(_butterflyBundles);
                break;

            case GameState.Fish:
                // 关掉鱼所有子面板
                SystemManager.Instance.CloseAndDestroyPanel(PanelType.DashCompetition);
                // 假设鱼也有帮助页之类的
                SystemManager.Instance.CloseAndDestroyPanel(PanelType.CompetitionHelp); 
                SystemManager.Instance.CloseAndDestroyPanel(PanelType.CompetitionFail); 
                SystemManager.Instance.CloseAndDestroyPanel(PanelType.MatchSuccess);
                await Task.Yield();
                UnloadBundles(_fishBundles);
                break;

            case GameState.Shop:
                SystemManager.Instance.CloseAndDestroyPanel(PanelType.ShopScreen);
                await Task.Yield();
                UnloadBundles(_shopBundles);
                break;
                
            case GameState.None:
                // 刚启动，啥都不用卸载
                break;
        }
        await Task.Yield(); // 确保逻辑帧结束
    }
    
    // ==========================================
    // 私有：进入逻辑 (根据目标状态决定装谁)
    // ==========================================
    private async Task EnterNewStateRoutine(GameState newState)
    {
        switch (newState)
        {
            case GameState.Lobby:
                await LoadBundles(_lobbyBundles);
                SystemManager.Instance.ShowPanel(PanelType.PrimaryInterface);
                break;

            case GameState.Gameplay:
                await LoadBundles(_gameplayBundles);
                SystemManager.Instance.ShowPanel(PanelType.HexGamePlayArea);
                break;

            case GameState.Butterfly:
                await LoadBundles(_butterflyBundles);
                SystemManager.Instance.ShowPanel(PanelType.ButterflyHome);
                break;

            case GameState.Fish:
                await LoadBundles(_fishBundles);
                SystemManager.Instance.ShowPanel(PanelType.DashCompetition); // 你的鱼主面板
                break;

            case GameState.Shop:
                await LoadBundles(_shopBundles);
                SystemManager.Instance.ShowPanel(PanelType.ShopScreen); // 你的商店主面板
                break;
        }
    }
    
    // --- 辅助方法 ---
    private async Task LoadBundles(string[] bundles)
    {
        List<Task> tasks = new List<Task>();
        foreach (var b in bundles) tasks.Add(AssetBundleLoader.SharedInstance.PreloadSingleBundle(b));
        await Task.WhenAll(tasks);
    }

    private void UnloadBundles(string[] bundles)
    {
        foreach (var b in bundles) AssetBundleLoader.SharedInstance.ReleaseBundle(b, true);
    }
    
    private async Task EnterLobbyRoutine(GameState fromGameState)
    {
        MessageSystem.Instance.ShowLoadingAnimation();
        
        SystemManager.Instance.CloseAndDestroyPanel(PanelType.HexGamePlayArea);
        SystemManager.Instance.CloseAndDestroyPanel(PanelType.StageFinishView);
        
        foreach (var bundle in _gameplayBundles)
        {
            AssetBundleLoader.SharedInstance.ReleaseBundle(bundle, true);
        }
        // 强制 GC
        Resources.UnloadUnusedAssets();
        System.GC.Collect();
        
        List<Task> loadTasks = new List<Task>();
        foreach (var bundle in _lobbyBundles)
        {
            loadTasks.Add(AssetBundleLoader.SharedInstance.PreloadSingleBundle(bundle));
        }
        await Task.WhenAll(loadTasks);
        
        SystemManager.Instance.ShowPanel(PanelType.PrimaryInterface);

        CurrentState = GameState.Lobby;
        MessageSystem.Instance.HideLoadingAnimation();
    }

    public async void GoToGameplay()
    {
        if (CurrentState == GameState.Gameplay) return;
        
        MessageSystem.Instance.ShowLoadingAnimation();
        
        SystemManager.Instance.CloseAndDestroyPanel(PanelType.PrimaryInterface);
        
        foreach (var bundle in _lobbyBundles)
        {
            AssetBundleLoader.SharedInstance.ReleaseBundle(bundle, true);
        }
        var unloadOp = Resources.UnloadUnusedAssets();
        while (!unloadOp.isDone) await Task.Yield();
        System.GC.Collect();
        
        List<Task> loadTasks = new List<Task>();
        foreach (var bundle in _gameplayBundles)
        {
            loadTasks.Add(AssetBundleLoader.SharedInstance.PreloadSingleBundle(bundle));
        }
        await Task.WhenAll(loadTasks);
        SystemManager.Instance.ShowPanel(PanelType.HexGamePlayArea);
        
        CurrentState = GameState.Gameplay;
        MessageSystem.Instance.HideLoadingAnimation();
    }
    
    // ==========================================
    // 2. 进蝴蝶 (从大厅 -> 蝴蝶)
    // ==========================================
    public async Task EnterButterflyRoutine()
    {
        
        if (CurrentState == GameState.Butterfly) return;
        SystemManager.Instance.HidePanel(PanelType.HeaderSection);
        MessageSystem.Instance.ShowLoadingAnimation();
        
        // 情况1：从大厅进来
        if (CurrentState == GameState.Lobby)
        {
            SystemManager.Instance.CloseAndDestroyPanel(PanelType.PrimaryInterface);
            foreach (var bundle in _lobbyBundles)
            {
                AssetBundleLoader.SharedInstance.ReleaseBundle(bundle, true);
            }
        }// 情况2：从游戏(胜利页)进来
        else if (CurrentState == GameState.Gameplay)
        {
            // 关掉胜利页和游戏页
            SystemManager.Instance.CloseAndDestroyPanel(PanelType.StageFinishView);
            SystemManager.Instance.CloseAndDestroyPanel(PanelType.HexGamePlayArea);
        
            // 卸载游戏包
            foreach (var bundle in _gameplayBundles)
            {
                AssetBundleLoader.SharedInstance.ReleaseBundle(bundle, true);
            }
        }
        var unloadOp = Resources.UnloadUnusedAssets();
        while (!unloadOp.isDone) await Task.Yield();
        System.GC.Collect();

        // C. 【加载蝴蝶】
        List<Task> loadTasks = new List<Task>();
        foreach (var bundle in _butterflyBundles)
        {
            loadTasks.Add(AssetBundleLoader.SharedInstance.PreloadSingleBundle(bundle));
        }
        // 必须 await！确保资源全部进内存
        await Task.WhenAll(loadTasks);

        // D. 【显示蝴蝶】
        SystemManager.Instance.ShowPanel(PanelType.ButterflyHome);
        CurrentState = GameState.Butterfly;
        MessageSystem.Instance.HideLoadingAnimation();
    }
    
    public async void OpenFeature(string bundleName, string panelName)
    {
        // 叠加模式不需要卸载当前的主状态 (Lobby/Game)，直接加载新的
        MessageSystem.Instance.ShowLoadingAnimation();

        await AssetBundleLoader.SharedInstance.PreloadSingleBundle(bundleName);
        
        var panel = SystemManager.Instance.ShowPanel(panelName);
        
        // 监听面板关闭，自动卸载资源
        // 假设你的 UIWindow 基类有一个 onWindowClose 回调，或者你手动处理
        // 这里演示手动处理：
        // 你需要在 FeaturePanel 脚本里调用 CloseFeature 方法
        
        MessageSystem.Instance.HideLoadingAnimation();
    }
    
    public void CloseFeature(string bundleName, string panelName)
    {
        SystemManager.Instance.CloseAndDestroyPanel(panelName);
        AssetBundleLoader.SharedInstance.ReleaseBundle(bundleName, true);
        Debug.Log($"功能模块 {panelName} 已关闭并释放资源");
    }
    
    public void SetAutoLevelTalbe(bool isShow)
    {
        AutoLevelTalbe.gameObject.SetActive(isShow);
    }
    #region 私有方法
    /// <summary>
    /// 游戏初始化协程
    /// </summary>
    private IEnumerator InitializeGameRoutine()
    {
        yield return null;
        WordVocabularyManager.Instance.LoadEntriesAsync();
        StageHexController.Instance.CreateStageInfo(1);
        
        if (GameDataManager.Instance.UserData.IsFirstLaunch)
        {
            CurrentState = GameState.Gameplay;
            StageHexController.Instance.SetStageData(GameDataManager.Instance.UserData.CurrentHexStage);
            SystemManager.Instance.ShowPanel(PanelType.HexGamePlayArea);
            //ShowPrivacyScreen();
            // 标记非首次进入
            GameDataManager.Instance.UserData.IsFirstLaunch = false;
        }
        else
        {
            CurrentState = GameState.Lobby;
            SystemManager.Instance.ShowPanel(PanelType.PrimaryInterface);
        }
    }
    
    /// <summary>
    /// 显示隐私协议界面
    /// </summary>
    private void ShowPrivacyScreen()
    {
        SystemManager.Instance.ShowPanel(PanelType.PolicyView);
    }
    private void SetupCanvasCamera()
    {
        // 1. 找到场景里的 Canvas (假设你的 Canvas 名字叫 "Canvas")
        Canvas myCanvas = GameObject.Find("Canvas")?.GetComponent<Canvas>();
    
        if (myCanvas != null && myCanvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            // 2. 如果 Render Camera 是空的，把主相机赋给它
            if (myCanvas.worldCamera == null)
            {
                myCanvas.worldCamera = Camera.main;
                Debug.Log("[UI Fix] 已重新绑定 Canvas 的相机");
            }
        
            // 3. 确保 Plane Distance 没问题 (有时候会变成负数导致被裁剪)
            if(myCanvas.planeDistance < 1) myCanvas.planeDistance = 100;
        
            // 4. 强制刷新一下 Sorting Layer (有时层级会错乱)
            myCanvas.sortingLayerName = "UI"; // 确保你项目里有这个 Layer
            myCanvas.sortingOrder = 0;
        }
    }
    private void OnAutoLevelTalbeValueChanged(bool ison)
    {
        IsTrueAuto = ison;
        if (ison)
        {
            Game.self.Ratex2Game();
        }
        else
        {
            Game.self.ResumeGame();
        }
        
        EventDispatcher.instance.TriggerAutoPassLevel();
    }
    #endregion
}
