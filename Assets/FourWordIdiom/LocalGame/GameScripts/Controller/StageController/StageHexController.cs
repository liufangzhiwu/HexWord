using System;
using System.Collections;
using System.Collections.Generic;
using Middleware;
using UnityEngine;

public enum LevelType
{
    BlockWord=1,//词语消除
    ChessWord=2,//拼字游戏
    HexWord=3,//六边形游戏
}

public enum HexType
{
    PingHexagon=0,//平顶六边形
    JianHexagon=1,//尖顶六边形
}

/// <summary>
/// 词语数据类 - 存储当前选中的词语信息
/// </summary>
public class PuzzleData
{
    public int PageIndex;                     // 当前页码索引
    public string CurPuzzle = null;             // 当前选中的词语
    public bool IsVocabularyPuzzle = false;     // 是否来自词库的词语
}

/// <summary>
/// 关卡管理系统（非MonoBehaviour单例）
/// 功能：
/// 1. 管理所有关卡数据加载与切换
/// 2. 处理关卡进度保存
/// 3. 协调关卡完成流程
/// </summary>
public class StageHexController
{
    #region 单例实现
    private static readonly StageHexController _instance = new StageHexController();
    
    public static StageHexController Instance => _instance;
    
    private StageHexController() 
    {
        // 私有构造函数防止外部实例化
        //LoadPackInfos();
    }
    #endregion

    #region 数据配置
    // 多语言关卡配置
    private PackInfo PackInfo_Stage;      // 关卡配置  

    // 游戏配置参数
    public float ActiveTileSize { get; set; }  // 字块显示尺寸
    #endregion

    #region 运行时数据
    public StageInfo CurStageInfo { get; private set; } // 当前关卡配置数据
    public StageProgressData CurStageData { get; private set; } // 当前关卡进度数据   
    public BoardGame BoardData { get; set; } = new BoardGame(); // 棋盘数据
    
    public PuzzleData PuzzleData=new PuzzleData();

    // 状态标记
    public bool IsEnterPuzzle { get; set; }       // 是否从目标词进入熟语
    public bool IsEnterVocabulary { get; set; } // 是否进入关卡内熟语
    public bool IsGMEnterStage { get; set; }    // 是否通过GM工具进入关卡
    public bool IsFirstEnterStage { get; set; } // 是否首次进入当前关卡

    // 引用组件（通过方法注入）
    public PuzzleTile UpPuzzleGrid { get; set; }    // 字块矩阵中最上方字块
    private int _limitPuzzleCount = 0;            // 限时活动连词计数
    public int PuzzleComboCount = 0;            // 连击次数
    public int PuzzleZenCount = 0;            // 连击次数
    
    public float lastActivityTime;
    
    public string tipPuzzle = "";
    
    public LevelModes CurLevelMode;
    
    #endregion

    #region 属性封装
    /// <summary>
    /// 获取当前语言对应的关卡配置
    /// </summary>
    public PackInfo PackInfos
    {
        get
        {
            return PackInfo_Stage;
           
        }
    }

    /// <summary>
    /// 当前关卡编号（代理保存系统的当前关卡）
    /// </summary>
    public int CurrentStage
    {
        get => GameDataManager.Instance.UserData.CurrentHexStage;
        private set => GameDataManager.Instance.UserData.CurrentHexStage = value;
    }
    #endregion

    #region 初始化方法
    /// <summary>
    /// 加载当前语言的关卡配置
    /// </summary>
    public void LoadPackInfos()
    {
        //string languageCode = GameDataManager.Instance.UserData.LanguageCode;
        
        // if (languageCode == "ChineseSimplified" && PackInfo_Stage == null)
        // {
            PackInfo_Stage = LoadPackInfoAsset("CS_Content");
        // }
        // else if (languageCode == "ChineseTraditional" && PackInfo_Stage == null)
        // {
        //     PackInfo_Stage = LoadPackInfoAsset("CT_Content");
        // }
        // else if (PackInfo_Stage == null)
        // {
        //     PackInfo_Stage = LoadPackInfoAsset("JP_Content");
        // }
    }

    /// <summary>
    /// 从AssetBundle加载关卡配置
    /// </summary>
    private PackInfo LoadPackInfoAsset(string assetName)
    {
        return AdvancedBundleLoader.SharedInstance.LoadScriptableObject(
            "objects", 
            assetName
        ) as PackInfo;
    }
    #endregion

    #region 关卡管理
    /// <summary>
    /// 设置当前关卡数据
    /// </summary>
    /// <param name="StageIndex">关卡编号</param>
    public void SetStageData(int StageIndex)
    {
        // 初始化关卡配置
        //IsFirstEnterStage = GetIsFirstEnterStage();
        
        CurStageInfo = CreateStageInfo(StageIndex);
        CurStageData = GameDataManager.Instance.RetrieveLevelProgress(CurStageInfo);

        // 重置关卡状态
        ResetStageState(StageIndex);
        
        // 记录关卡开始时间
        GameDataManager.Instance.UserData.curStageStartTime = DateTime.Now.ToString();
        Debug.Log("关卡id:"+StageIndex+"关卡开始时间"+GameDataManager.Instance.UserData.curStageStartTime);
        AnalyticMgr.SetCommonProperties();

        // 首次进入关卡的特殊处理
        if (!GameDataManager.Instance.UserData.curIsEnter)
        {
            IsFirstEnterStage = true;
            GameDataManager.Instance.UserData.GetWordVocabulary().LevelWords.Clear();
            GameDataManager.Instance.UserData.curStageOnlineTime = 0;
            PuzzleComboCount = 0;
            PuzzleZenCount = 0;
            // 可在此处添加分析事件...
            float energy = GameDataManager.Instance.ChessDynamicHardSave.EnergyValue;
            AnalyticMgr.LevelStart(energy);
            GameDataManager.Instance.UserData.curIsEnter = true;
            GameDataManager.Instance.UserData.ClearPuzzleVocabulary();
        }
        
        CheckRateUsConditions(StageIndex);
    }
    
    public bool GetIsFirstEnterStage()
    {
        return GameDataManager.Instance.IsNewLevelEntry(CurrentStage);
    }

    /// <summary>
    /// 创建关卡配置信息
    /// </summary>
    public StageInfo CreateStageInfo(int StageId,bool isAi=false)
    {
        int actualStageId = CalculateActualStageId(StageId);
        var stageInfo = new StageInfo(
            PackInfos.StageFiles[actualStageId - 1],
            actualStageId,
            StageId
        );
        PackInfos.CurrentStageInfo = stageInfo;
        return stageInfo;
    }

    /// <summary>
    /// 计算实际关卡ID（处理循环关卡逻辑）
    /// </summary>
    private int CalculateActualStageId(int StageId)
    {
        // 未超过总关卡数直接返回
        if (StageId <= PackInfos.StageFiles.Count)
            return StageId;

        // 计算循环关卡ID
        int startStage = PackInfos.StageFiles.Count - AppGameSettings.LoopLevelStart;
        int overflow = StageId - startStage;
        return startStage + (overflow % (PackInfos.StageFiles.Count - startStage));
    }

    /// <summary>
    /// 重置关卡状态
    /// </summary>
    private void ResetStageState(int StageIndex)
    {
        EventDispatcher.instance.TriggerUpdateRewardPuzzle(false);
        _limitPuzzleCount = 0;
    }
    #endregion

    #region 关卡流程控制
    /// <summary>
    /// 完成关卡主逻辑
    /// </summary>
    public void CompleteStage(int StageNumber)
    {
        CoroutineRunner.StartCoroutine(CompleteStageRoutine(StageNumber));
    }

    /// <summary>
    /// 关卡完成协程
    /// </summary>
    private IEnumerator CompleteStageRoutine(int StageNumber)
    {
        // 更新进度
        if (StageNumber == CurrentStage)
        {
            GameDataManager.Instance.UserData.UpdateHexStage();
        }
        
        UpdateLimitPuzzleCount();
        
        // 播放效果
        yield return PlayCompletionEffects(StageNumber);

        // 更新任务
        DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedPassLevel, 1);
        
        //提交游戏数据
        GameDataManager.Instance.CommitGameData();
    }

    /// <summary>
    /// 播放关卡完成效果
    /// </summary>
    private IEnumerator PlayCompletionEffects(int StageNumber)
    {
        //EventDispatcher.instance.TriggerChoicePuzzleSetStatus(false);
        AudioManager.Instance.PlaySoundEffect("PassStage");

        yield return new WaitForSeconds(0.7f);

        if (string.IsNullOrEmpty(GameDataManager.Instance.UserData.curStageStartTime))
        {
            Debug.LogError("关卡开始时间异常"+StageNumber);
            GameDataManager.Instance.UserData.curStageStartTime= DateTime.Now.ToString();
        }
        
        // 计算耗时
        DateTime startTime = DateTime.Parse(GameDataManager.Instance.UserData.curStageStartTime);
        float duration = (float)(DateTime.Now - startTime).TotalSeconds + 
                         GameDataManager.Instance.UserData.curStageOnlineTime;
        
        Debug.LogError("关卡id:"+StageNumber+"关卡进行时间(秒)"+duration);
        // ---- 修复：统一封存最后一段在线时长 ----
        GameDataManager.Instance.UserData.UpdateOnlineStageTime();
        float energy = GameDataManager.Instance.ChessDynamicHardSave.EnergyValue;
        AnalyticMgr.LevelCompleted(duration,energy,0,0);
        
        GameDataManager.Instance.UserData.UpdateLevelUseTimes(StageNumber,(int)duration);
        
        if (StageNumber >= 1)
        {

#if UNITY_OPENHARMONY || UNITY_huawei

            AnalyticMgr.InsetAdStart("关卡插屏");
            
            AdRuleManager.Instance.TryShowInterstitial((issuccess) =>
            {
                if (issuccess)
                {
                    AnalyticMgr.InsetAdSuccess("关卡插屏");
                    GameDataManager.Instance.UserData.totalInsetSeeAds++;
                }
                else
                {
                    AnalyticMgr.InsetAdFail("关卡插屏");
                }
            });
            
            // // 显示插屏广告
            // Game.self.Ads.ShowInterstitial((bool issuccess) => 
            // {
            //     if (issuccess)
            //     {
            //         AnalyticMgr.InsetAdSuccess("关卡插屏");
            //         GameDataManager.Instance.UserData.totalSeeAds++;
            //     }
            //     else
            //     {
            //         AnalyticMgr.InsetAdFail("关卡插屏");
            //     }
            // });
#endif
        }

        // 更新每日计数
        GameDataManager.Instance.UserData.dayPassStageCount++;

        yield return new WaitForSeconds(0.5f);

        // 播放过关视频
        //EnhancedVideoController.Instance.PlayVideo();

        // UI切换
        SystemManager.Instance.HidePanel(PanelType.HeaderSection);
        SystemManager.Instance.HidePanel(PanelType.HexGamePlayArea);

        yield return new WaitForSeconds(0.8f);

        SystemManager.Instance.ShowPanel(PanelType.HeaderSection);
        SystemManager.Instance.ShowPanel(PanelType.StageFinishView);
        
      
    }
    
    /// <summary>
    /// 检查评分弹窗条件
    /// </summary>
    private void CheckRateUsConditions(int StageIndex)
    {
        var userData = GameDataManager.Instance.UserData;

        // 第5关首次触发
        if (StageIndex == 6 && userData.showRateusCount <= 0)
        {
            SystemManager.Instance.ShowPanel(PanelType.RateUsScreen);
            return;
        }

        // 每日通关条件
        if (userData.dayPassStageCount == 5 && 
            userData.showRateusCount < 3 &&
            !string.IsNullOrEmpty(userData.showRateusTime))
        {
            DateTime lastTime = DateTime.Parse(userData.showRateusTime).Date;
            if ((DateTime.Now.Date - lastTime).TotalDays >= 1)
            {
                SystemManager.Instance.ShowPanel(PanelType.RateUsScreen);
            }
        }
    }
    
    #endregion

    #region 游戏逻辑
    
    // 重置无操作计时器
    public void ResetInactivityTimer()
    {
        lastActivityTime = Time.time;
    }
    
    /// <summary>
    /// 添加已找到的词语
    /// </summary>
    public void AddFoundPuzzle(string Puzzle)
    {
        // 初始化集合
        CurStageData.FoundTargetPuzzles ??= new List<string>();

        // 添加词语
        CurStageData.UpdateFoundTargetPuzzle(Puzzle);
        GameDataManager.Instance.UserData.AddStagePuzzle(Puzzle);

        // 限时活动计数
        // if (CurStageInfo.StageNumber >= CurrentStage)
        // {
        //     UpdateLimitPuzzleCount(1);
        // }
    }
    
    /// <summary>
    /// 限时奖励活动中记录连词的数量
    /// </summary>
    public int LimitPuzzlecount { 
        get 
        {
            bool canshow = LimitTimeManager.Instance.LimitTimeCanShow();
            return _limitPuzzleCount * (canshow ? 2 : 1);
        }
        set 
        {
            _limitPuzzleCount = value;
        } 
    }

    /// <summary>
    /// 更新限时活动计数
    /// </summary>
    public void UpdateLimitPuzzleCount(int increment=0)
    {
        _limitPuzzleCount = CurStageInfo.Puzzles.Count;
    }

    /// <summary>
    /// 屏幕坐标转UI本地坐标
    /// </summary>
    public Vector2 ScreenToLocalPosition(Vector2 screenPos, RectTransform parent)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent,
            screenPos,
            SystemManager.Instance.MainCamera,
            out Vector2 localPos
        );
        return localPos;
    }
    #endregion
}

/// <summary>
/// 协程运行辅助类（替代MonoBehaviour协程支持）
/// </summary>
public static class CoroutineRunner
{
    private class Runner : MonoBehaviour { }
    
    private static Runner _instance;
    
    public static Coroutine StartCoroutine(IEnumerator routine)
    {
        if (_instance == null)
        {
            _instance = new GameObject("CoroutineRunner")
                .AddComponent<Runner>();
            UnityEngine.Object.DontDestroyOnLoad(_instance.gameObject);
        }
        return _instance.StartCoroutine(routine);
    }
}