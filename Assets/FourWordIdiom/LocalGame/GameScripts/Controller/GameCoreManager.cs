using System;
using System.Collections;
using System.Linq;
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
    }

    public void SetAutoLevelTalbe(bool isShow)
    {
        AutoLevelTalbe.gameObject.SetActive(isShow);
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

    #region 私有方法
    /// <summary>
    /// 初始化多语言字符串
    /// </summary>
    private void InitializeLanguageStrings()
    {
        //string TimeHourText = _languageManager.GetString("TimeH") + " ";
        //string TimeMinuteText = _languageManager.GetString("TimeM");
    }

    /// <summary>
    /// 游戏初始化协程
    /// </summary>
    private IEnumerator InitializeGameRoutine()
    {
        yield return new WaitForSeconds(0.2f);
        Debug.Log("主场景核心初始化完成 。。。。");
        StageHexController.Instance.CreateStageInfo(1);
        
        if (GameDataManager.Instance.UserData.IsFirstLaunch)
        {
            ShowGamePanel();
            //ShowPrivacyScreen();
            // 标记非首次进入
            GameDataManager.Instance.UserData.IsFirstLaunch = false;
        }
        else
        {
            SystemManager.Instance.ShowPanel(PanelType.PrimaryInterface);
        }
    }
    /// <summary>
    /// 显示游戏主界面
    /// </summary>
    public void ShowGamePanel()
    {
        StageHexController.Instance.SetStageData(GameDataManager.Instance.UserData.CurrentHexStage);
        SystemManager.Instance.ShowPanel(PanelType.HexGamePlayArea);
    }
    
    /// <summary>
    /// 显示隐私协议界面
    /// </summary>
    private void ShowPrivacyScreen()
    {
        SystemManager.Instance.ShowPanel(PanelType.PolicyView);
    }
    
    #endregion
}
