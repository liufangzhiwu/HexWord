using System;
using System.Collections;
using System.Collections.Generic;
using Middleware;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// 关卡管理系统（非MonoBehaviour单例）
/// 功能：
/// 1. 管理所有关卡数据加载与切换
/// 2. 处理关卡进度保存
/// 3. 协调关卡完成流程
/// </summary>

public class ChessStageController
{
    #region 单例实现
    private static readonly ChessStageController _instance = new ChessStageController();

    public static ChessStageController Instance => _instance;


    private ChessStageController() { }
    #endregion

    #region 数据配置

    private ChessPackInfo StagePackInfo;           // 关卡配置
    public float ActiveTileSize { get; set; }      // 字块显示尺寸
    #endregion

    #region 运行时数据
    public ChessStageInfo CurrStageInfo { get; private set; }           // 当前关卡配置数据
    public ChessStageProgressData CurrStageData { get; private set; }   // 当前关卡进度数据
    public PuzzleData PuzzleData = new PuzzleData();
    public bool IsEnterPuzzle { get; set; }          //是否从目标词进入
    public bool IsEnterVocabulary { get; set; }       // 是否进入关卡内
    public bool IsGMEnterStage { get; set; }          // 是否通过GM工具进入关卡
    public bool IsFirstEnterStage { get; private set; } = true;  // 是否首次进入当前关卡

    private int _limitPuzzleCount = 0;              // 限时活动连词计数
    public int PuzzleComboCount { get;  set; } = 0;  // 连续正确次数
    public int ComboErrorCount { get;  set; } = 0;   // 连续错误次数
    public int UseTipToolCount { get;  set; } = 0;   // 使用提示工具次数
    public int UseCompleteCount { get; set; } = 0;  // 使用完成工具的字数
    #endregion

    #region 属性封装
    public ChessPackInfo PackInfos => StagePackInfo;
    public int CurrentStage
    {
        get => GameDataManager.Instance.UserData.CurrentChessStage;
        private set => GameDataManager.Instance.UserData.CurrentChessStage = value;
    }
    #endregion
    #region 初始化方法
    /// <summary>
    /// 加载当前语言的关卡配置
    /// </summary>
    public void Init()
    {
        
        if (StagePackInfo == null)
        {
            StagePackInfo = AdvancedBundleLoader.SharedInstance.LoadScriptableObject(ToolUtil.GetLanguageBundle(), "ChessPackInfo") as ChessPackInfo;
        }
    }
    #endregion

    #region 关卡管理
    /// <summary>
    /// 设置指定关卡数据
    /// </summary>
    /// <param name="StageIndex">关卡编号</param>
    public void SetStageData(int StageIndex)
    {
        // 初始化关卡配置
        IsFirstEnterStage = GameDataManager.Instance.IsNewLevelEntry(StageIndex);

        CurrStageInfo = CreateStageInfo(StageIndex);
        CurrStageData = GameDataManager.Instance.RetrieveChessLevelProgress(CurrStageInfo);
        IsFirstEnterStage &= CurrStageData.IsFirstEnter;
        Debug.Log("当前是否首次进入关卡: " + IsFirstEnterStage + " " + CurrStageData.IsFirstEnter);
        // 重置关卡状态
        ResetStageState(StageIndex);

        // 记录关卡开始时间
        GameDataManager.Instance.UserData.curStageStartTime = DateTime.Now.ToString();
        AnalyticMgr.SetCommonProperties();
        
        // 首次进入关卡的特殊处理, 重置关卡内的分析数据
        if (IsFirstEnterStage)
        {
            GameDataManager.Instance.UserData.GetWordVocabulary().LevelWords.Clear();
            GameDataManager.Instance.UserData.curStageOnlineTime = 0;

            PuzzleComboCount = 0;
            UseCompleteCount = 0;
            UseTipToolCount = 0;
            ComboErrorCount = 0;
            AnalyticMgr.LevelStart();
            CurrStageData.IsFirstEnter = false;
            GameDataManager.Instance.UserData.curIsEnter = true;
            GameDataManager.Instance.UserData.ClearPuzzleVocabulary();
        }

        GameDataManager.Instance.UpdateChessLevelProgress(CurrStageData);
        CheckRateUsConditions(StageIndex);
        
        foreach (var puzzle in CurrStageData.FoundTargetPuzzles)
        {
            GameDataManager.Instance.UserData.AddStagePuzzle(puzzle);
        }
        // Debug.Log("添加完成后: "+ JsonConvert.SerializeObject(GameDataManager.Instance.UserData.GetWordVocabulary().LevelWords));
    }

    /// <summary>
    /// 创建关卡数据
    /// </summary>
    private ChessStageInfo CreateStageInfo(int stageIndex, bool isAi = false)
    {
        int wordCount= ChessDynamicHardManager.Instance.CheckLevelHardChange(stageIndex);
      
        int actualStageId = CalculateActualStageId(stageIndex);
        
        if(PackInfos.PackInfos.Count <= actualStageId)
            actualStageId = 0;
        
        var stageInfo = new ChessStageInfo(
            PackInfos.Get(actualStageId),
            actualStageId,
            stageIndex,
            wordCount
            );
 
        PackInfos.CurrentStageInfo = stageInfo;
        return stageInfo; 
    }

    /// <summary>
    /// 计算实际关卡ID(处理循环关卡逻辑)
    /// </summary>
    private int CalculateActualStageId(int stageIndex)
    {
        if (stageIndex <= StagePackInfo.PackInfos.Count)
            return stageIndex;

        int startStage = PackInfos.PackInfos.Count - AppGameSettings.LoopLevelStart;
        int overflow = stageIndex - startStage;
        return startStage + (overflow % (PackInfos.PackInfos.Count - startStage));
    }

    /// <summary>
    /// 重置关卡
    /// </summary>
    private void ResetStageState(int stageIndex)
    {
        _limitPuzzleCount = 0;
    }

    /// <summary>
    /// 检查评分弹窗条件
    /// </summary>
    private void CheckRateUsConditions(int stageIndex)
    {

    }
    #endregion

    #region 关卡流程控制
    /// <summary>
    /// 完成关卡主逻辑
    /// </summary>
    public void CompleteStage(int stageNumber, int wordErrorCount)
    {
        ComboErrorCount = wordErrorCount;
        CoroutineRunner.StartCoroutine(CompleteStageRoutine(stageNumber));
    }

    /// <summary>
    /// 关卡完成协程
    /// </summary>
    /// <param name="stageNumber"></param>
    /// <returns></returns>
    private IEnumerator CompleteStageRoutine(int stageNumber)
    {
        ActiveTileSize = 0;
        if (stageNumber == CurrentStage)
        {
            GameDataManager.Instance.UserData.UpdateChessStage();
        }

        yield return PlayCompletionEffects(stageNumber);
        GameDataManager.Instance.CommitGameData();
        // 更新任务
        DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedPassLevel, 1);
    }

    /// <summary>
    /// 播放关卡完成效果
    /// </summary>
    /// <param name="stageNumber"></param>
    /// <returns></returns>
    private IEnumerator PlayCompletionEffects(int stageNumber)
    {
        AudioManager.Instance.PlaySoundEffect("success");

        yield return new WaitForSeconds(0.7f);

        // 计算耗时
        DateTime startTime = DateTime.Parse(GameDataManager.Instance.UserData.curStageStartTime);
        float duration = (float)(DateTime.Now - startTime).TotalSeconds +
                         GameDataManager.Instance.UserData.curStageOnlineTime;
        AnalyticMgr.LevelCompleted(duration);
        
        GameDataManager.Instance.UserData.dayPassStageCount++;

        CheckDynamicDifficultyIntervention(stageNumber,ComboErrorCount, duration);

        yield return new WaitForSeconds(0.8f);
        // 播放过关视频
        try
        {
            EnhancedVideoController.Instance.PlayVideo();
        }
        catch (Exception e)
        {
            Debug.Log($"播放视频错误？ {e.Message} " + e.ToString());
        }
        
        // UI切换
        SystemManager.Instance.HidePanel(PanelType.HeaderSection);
        SystemManager.Instance.HidePanel(PanelType.ChessPlayArea);

        yield return new WaitForSeconds(0.8f);

        SystemManager.Instance.ShowPanel(PanelType.ChessFinishView);
        SystemManager.Instance.ShowPanel(PanelType.HeaderSection);
    }

    /// <summary>
    /// 动态难度关卡初值设定机制
    /// </summary>
    /// <param name="currentLevel"></param>
    /// <param name="errorCount"></param>
    /// <param name="timeSpent"></param>
    private void CheckDynamicDifficultyIntervention(int currentLevel, int errorCount, float timeSpent)
    {
        float propswordCount = GetUserToolCount();
        Debug.Log($"使用道具提示字总数 {propswordCount} 自动完成字数{UseCompleteCount}");
        bool usedprops = propswordCount > 0;
        
        ChessDynamicHardManager.Instance.CheckLevelClearConditions(
            level: currentLevel, 
            errorCount: errorCount,
            clearTime: timeSpent,
            usedProps: usedprops,
            propsWordCount: propswordCount);
    }

    private int GetUserToolCount()
    {
        return UseTipToolCount + UseCompleteCount; // 加上自动完成的
    }
    public Vector2 ScreenToLocalPosition(Vector2 screenPos, RectTransform parent)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent,
            screenPos,
            null,
            out Vector2 localPos);
        return localPos;
    }
    #endregion

    #region 游戏逻辑

    public int LimitPuzzleCount
    {
        get
        {
            bool canshow = LimitTimeManager.Instance.LimitTimeCanShow();
            return _limitPuzzleCount * (canshow ? 2 : 1);
        }
        set { _limitPuzzleCount = value; }
    }

    /// <summary>
    /// 添加已找到的词语
    /// </summary>
    public void AddFoundPuzzle(string puzzle)
    {
        CurrStageData.FoundTargetPuzzles ??= new List<string>();
        CurrStageData.FoundTargetPuzzles.Add(puzzle);
        GameDataManager.Instance.UserData.AddStagePuzzle(puzzle);
        _limitPuzzleCount += 1;
    }
    /// <summary>
    /// 修改棋盘内的值
    /// </summary>
    public void ModifyChreepiece(Chesspiece chesspiece)
    {
        CurrStageData.BoardSnapshot.Remove(chesspiece);
        CurrStageData.BoardSnapshot.Add(chesspiece);
    }
    /// <summary>
    ///  修改光标
    /// </summary>
    public void ModifyCursor(int row, int col)
    {
        var list = CurrStageData.Cousor;
        if (list.Count == 2)   // 复用容量
        {
            list[0] = row;
            list[1] = col;
        }
        else                   // 第一次或长度不对
        {
            list.Clear();
            list.Add(row);
            list.Add(col);
        }
    }
    /// <summary>
    /// 修改字堆单元， id是不变的
    /// </summary>
    public void ModifyBowl(Bowl bowl)
    {
        CurrStageData.Puzzles.RemoveWhere(b => b.id == bowl.id);
        CurrStageData.Puzzles.Add(bowl);
    }
    #endregion
}