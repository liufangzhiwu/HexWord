using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 教程管理系统 (单例模式)
/// 
/// </summary>
public class ChessGuideSystem : MonoBehaviour
{
    #region 单例实现
    public static ChessGuideSystem Instance { get; private set; }

    private void Awake()
    {
        if(Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this);
    }
    #endregion

    #region 资源配置
    private ChessLearningGuide _learningGuidePanel;
    [Tooltip("对象池管理器 (用于教程元素复用)")]
    private ObjectPool _objectPool;

    [Space(10)]
    
    [Header("网格配置")]
    [HideInInspector]
    [Tooltip("当前关卡需要展示字词列表")]
    public List<ChessView> ChesspieceList = new List<ChessView>();

    [HideInInspector]
    [Tooltip("当前教学目标的单词")]
    public List<BowlView> TargetPuzzle = new List<BowlView>();

    [HideInInspector]
    [Tooltip("当前使用的教学工具对象")]
    public GameObject activeToolObject;
    [HideInInspector]
    [Tooltip("当前使用的教学工具来源名称")]
    public string toolSourceName;

    public int currentTutorial  = 1;
    #endregion
    

    #region 核心功能
    /// <summary>
    /// 显示教程界面
    /// </summary>
    public void DisplayGuide()
    {
        if (SystemManager.Instance != null)
        {
            UIWindow panel = SystemManager.Instance.ShowPanel(PanelType.ChessLearningGuide);
            if(panel != null)
            {
                _learningGuidePanel = panel.GetComponent<ChessLearningGuide>();
            }
            // 添加分析;
            AnalyticMgr.GuideBegin();
        }
        else
        {
            Debug.LogError("UI管理器未初始化！ 调整先初始化UI管理器");
        }
    }
    /// <summary>
    /// 隐藏教程界面
    /// </summary>
    public void CloseGuide()
    {
        if (SystemManager.Instance != null)
        {

            if (SystemManager.Instance.PanelIsShowing(PanelType.ChessLearningGuide))
            {
                SystemManager.Instance.HidePanel(PanelType.ChessLearningGuide);
            }
        }
        else
        {
            Debug.LogError("UI管理器未初始化！");
        }
    }
    public void OnClickCallback()
    {
        _learningGuidePanel?.SetClickCallback();
    }
    #endregion
    #region 辅助方法
    public void CleanCurrentTutorial()
    {
        if (activeToolObject != null)
        {
            activeToolObject = null;
        }
        toolSourceName = null;
        ChesspieceList.Clear();
        TargetPuzzle.Clear();
    }
    #endregion
    #region 生命周期
    private void OnDestroy()
    {
        // 释放对象池资源
        if(_objectPool != null)
        {
            _objectPool.ReturnAllObjectsToPool();
            _objectPool = null;
        }

        // 单例销毁
        if(Instance == this)
        {
            Instance = null;
        }
    }
    #endregion
}
