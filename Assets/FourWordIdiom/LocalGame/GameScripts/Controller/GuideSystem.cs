using UnityEngine;
using System.Collections.Generic;


/// <summary>
/// 教程管理系统（单例模式）
/// 版本：1.1
/// 功能说明：
/// 1. 管理游戏内新手引导流程
/// 2. 控制教程界面的显示/隐藏
/// 3. 维护教程相关资源池
/// 最后修改：2023-08-20
/// </summary>
public class GuideSystem : MonoBehaviour
{
    #region 单例实现
    // 线程安全的单例实例
    public static GuideSystem Instance { get; private set; }

    /// <summary>
    /// 初始化单例实例
    /// </summary>
    private void Awake()
    {
        // 单例冲突处理
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            Debug.LogWarning($"检测到重复的教程管理器实例，已自动销毁：{gameObject.name}");
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // 跨场景持久化
        Debug.Log($"教程管理器初始化完成：{GetInstanceID()}");
    }
    #endregion

    #region 资源配置
    [Tooltip("对象池管理器（用于教程元素复用）")]
    private ObjectPool _objectPool;

    [Space(10)]
    [Header("网格配置")]
    [Tooltip("当前关卡的字词网格列表")]
    public List<PuzzleTile> PuzzleGrids = new List<PuzzleTile>();

    [HideInInspector]
    [Tooltip("当前教学目标的单词")]
    public string targetPuzzle;

    [HideInInspector]
    [Tooltip("当前使用的教学工具对象")]
    public GameObject activeToolObject;
    #endregion

    #region 核心功能
    /// <summary>
    /// 显示教程界面
    /// </summary>
    /// <remarks>
    /// 调用UI管理器显示指定的教程面板
    /// </remarks>
    public void DisplayGuide()
    {
        if (SystemManager.Instance != null)
        {
            SystemManager.Instance.ShowPanel(PanelType.LearningGuide);
            //AnalyticsManager.TrackTutorialStart(); // 埋点：教程开始
            //ThinkManager.instance.Event_Guide();
        }
        else
        {
            Debug.LogError("UI管理器未初始化！");
        }
    }

    /// <summary>
    /// 隐藏教程界面
    /// </summary>
    /// <remarks>
    /// 调用UI管理器隐藏教程面板，并执行清理操作
    /// </remarks>
    public void CloseGuide()
    {
        if (SystemManager.Instance != null)
        {
            SystemManager.Instance.HidePanel(PanelType.LearningGuide); 
            CleanCurrentTutorial();
            //AnalyticsManager.TrackTutorialEnd(); // 埋点：教程结束
        }
        else
        {
            Debug.LogError("UI管理器未初始化！");
        }
    }
    #endregion

    #region 辅助方法
    /// <summary>
    /// 清理当前教程状态
    /// </summary>
    private void CleanCurrentTutorial()
    {
        // 回收教学工具对象
        if (activeToolObject != null)
        {
            //_objectPool?.Release(activeToolObject);
            activeToolObject = null;
        }
    }
    #endregion

    #region 生命周期
    private void OnDestroy()
    {
        // 释放对象池资源
        if (_objectPool != null)
        {
            _objectPool.ReturnAllObjectsToPool();
            _objectPool = null;
        }

        // 单例实例清理
        if (Instance == this)
        {
            Instance = null;
        }
    }
    #endregion
}
