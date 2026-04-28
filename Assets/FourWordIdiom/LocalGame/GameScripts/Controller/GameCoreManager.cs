using System;
using System.Collections;
using Middleware;
using UnityEngine;
using UnityEngine.HuaweiAppGallery;
using UnityEngine.Rendering;
using UnityEngine.UI;
#if UNITY_IOS
using UnityEngine.iOS;
#endif

public enum PanelState
{
   Null,MainMenuPanel,FinishHexPanel,GameHexPanel,GamePingPanel,FinishPingPanel
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
#if UNITY_huawei && !UNITY_EDITOR
        HuaweiGameService.ShowFloatWindow();
        StartCoroutine(CheckOrderShipmentCompleted());
#endif
        Game.self._uiRoot=SystemManager.Instance._uiRoot;
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
        
        if (GameDataManager.Instance.UserData.IsFirstLaunch)
        {
            ChessStageController.Instance.SetStageData(GameDataManager.Instance.UserData.CurrentChessStage);
            yield return new WaitForSeconds(0.2f);
            SystemManager.Instance.ShowPanel(PanelType.ChessPlayArea);
            // 标记非首次进入
            GameDataManager.Instance.UserData.IsFirstLaunch = false;
            
            AnalyticMgr.ActivityBegin("蝶园活动");
        }
        else
        {
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

    // 检查发货是否完成
    private IEnumerator CheckOrderShipmentCompleted()
    {
        yield return new WaitForSeconds(2f);
        Game.self.Shop.Restore((ok, items) =>
        {
            foreach (ProductItem item in items)
            {
                ShopManager.shopManager.OnPurchaseSuccess(item);
            }
        });
    }
    #endregion

    private void OnDisable()
    {
        #if UNITY_huawei && !UNITY_EDITOR
        HuaweiGameService.HideFloatWindow();
        #endif
    }
}
