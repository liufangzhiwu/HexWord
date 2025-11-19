using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
#if UNITY_IOS
using UnityEngine.iOS;
#endif

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
     
    [HideInInspector] public bool IsNetworkActive;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 保持广告管理器在场景切换时不销毁
        }
    }

    #region 公共API


    /// <summary>
    /// 显示游戏主界面
    /// </summary>
    public void ShowGamePanel()
    {
        StageController.Instance.SetStageData(GameDataManager.instance.UserData.CurrentStage);
        SystemManager.Instance.ShowPanel(PanelType.GamePlayArea);
    }

    private void Start()
    {
        StartCoroutine(InitializeGameRoutine());
        //StartCoroutine(CheckNetworkConnection());
    }
   
    #endregion

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
        
        StageController.Instance.CreateStageInfo(1);
        
        // if (GameDataManager.instance.UserData.IsFirstLaunch)
        // {
        //     ShowGamePanel();
        //     //ShowPrivacyScreen();
        // }
        // else
        // {
            SystemManager.Instance.ShowPanel(PanelType.PrimaryInterface);
        //}
    }

    /// <summary>
    /// 显示隐私协议界面
    /// </summary>
    private void ShowPrivacyScreen()
    {
        SystemManager.Instance.ShowPanel(PanelType.PolicyView);
    }
    
    private IEnumerator CheckNetworkConnection()
    {
        while (true)
        {
            bool isSuccess = false;
            Ping ping = new Ping("8.8.8.8");
            float timeout = 3.0f;
            float startTime = Time.time;

            // 等待Ping完成或超时
            while (!ping.isDone && Time.time - startTime < timeout)
            {
                yield return null;
            }

            // 关键修改：明确超时和成功的条件
            if (ping.isDone && ping.time > 0 && ping.time < 2000)
            {
                isSuccess = true;
            }
            else
            {
                isSuccess = false;
            }

            // 释放Ping资源（Unity需手动销毁）
            ping.DestroyPing();
            ping = null;

            IsNetworkActive = isSuccess;
            Debug.Log("网络状态: " + (IsNetworkActive ? "已连接" : "未连接"));

            yield return new WaitForSeconds(5);
        }
    }
    
    #endregion
}
