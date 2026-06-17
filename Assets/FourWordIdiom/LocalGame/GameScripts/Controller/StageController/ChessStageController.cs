using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Middleware;
using Newtonsoft.Json;
using UnityEngine;

public enum LevelModes
{
    Normal,
    Hard,
    ExtraHard,
}
/// <summary>
/// 连击配置数据结构
/// </summary>
public class ComboConfig
{
    public string State;      // 加减分状态 (add)
    public int Combo;         // 连击状态
    public int Num;           // 加减分数值
    public int TimeLag;       // 时间窗口（秒）
}

public class Interval
{
    public int Mode;    // 关卡模式: 1=困难,2=极难
    public int Degree;  // 难度: 0=轻度,1=中度,2=重度
    public int Start;   // 开始关卡
    public int End;     // 结束关卡, 叹号表示后续所有关卡
}
/// <summary>
/// 冰块玩法配置
/// </summary>
public class IceConfig
{
    public bool IsOpen;     // 是否开启
    public int FirstLevel;   // 首次出现的关卡是
    public int FirstDegree;  // 首次的难度
    public Dictionary<int, int> Degree; // 难度配置 {难度级别:数量}
    public Dictionary<int, int> Fixed;  // 固定关卡配置  {关卡id,级别degree}
    public List<Interval> CycleLevels;   // 循环关卡配置
}

/// <summary>
/// 根据叶子的收集数量发放奖励
/// </summary>
public class LeafReward
{
    public int Number;   // 叶子数量
    public int Type;    // 奖励类型
    public int Value;   // 数量
}
/// <summary>
/// 叶子玩法配置
/// </summary>
public class LeafConfig
{
    public bool IsOpen;
    public int FirstLevel;   // 首次出现的
    public List<int> CycleLevels; // 循环关卡, 每个位数匹配出现
    public List<LeafReward> Rewards; // 奖励列表
}
/// <summary>
/// 花朵玩法配置
/// </summary>
public class FlowerConfig
{
    public bool IsOpen;
    public int FirstLevel;
    public int FirstDegree;
    public int InitNumber;      // 初始消除最近花朵数量
    public int MinNumber;       // 最小消除最近花朵数量
    public Dictionary<int, int> Degree; // 难度配置 {难度级别:数量}
    public Dictionary<int, int> Fixed;  // 固定关卡配置  {关卡id,级别degree}
    public List<Interval> CycleLevels;  // 循环关卡配置
}
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

    public LevelModes CurLevelMode;
    
    private ChessStageController() { }
    #endregion

    #region 数据配置
    private readonly Dictionary<int, ComboConfig> _comboConfigDict = new Dictionary<int, ComboConfig>();
    private readonly Dictionary<int, ComboConfig> _reduceConfigDict = new Dictionary<int, ComboConfig>();
    public IceConfig IceConfig { get; private set; }          // 冰块
    public LeafConfig LeafConfig { get; private set; }        // 叶子
    public FlowerConfig FlowerConfig { get; private set; }    // 花朵
    public readonly List<StimulateRuleConfig> StimulateRules = new List<StimulateRuleConfig>();   // 关卡鼓励词规则配置
    private ChessPackInfo StagePackInfo;           // 关卡配置
    public float ActiveTileSize { get; set; }      // 字块显示尺寸
    
    public List<ChessView> GoldLeafChessViews = new List<ChessView>();
    #endregion

    #region 运行时数据
    public ChessStageInfo CurrStageInfo { get; private set; }           // 当前关卡配置数据
    public ChessStageProgressData CurrStageData { get; private set; }   // 当前关卡进度数据
    public PuzzleData PuzzleData = new PuzzleData();
    public bool IsEnterPuzzle { get; set; }          //是否从目标词进入
    public bool IsEnterVocabulary { get; set; }       // 是否进入关卡内
    public bool IsFirstEnterStage { get; private set; } = true;  // 是否首次进入当前关卡

    public int _limitPuzzleCount = 0;              // 限时活动连词计数
    public int PuzzleComboCount { get;  set; } = 0;  // 连续正确次数
    public int MaxComboCount { get; private set; } = 0;
    public float LastCorrectWordTimestamp { get;  set; } // 记录上一个词找对的绝对时间点
    public int ComboErrorCount { get;  set; } = 0;   // 连续错误次数
    public int UseTipToolCount { get;  set; } = 0;   // 使用提示工具次数
    public int UseCompleteCount { get; set; } = 0;  // 使用完成工具的字数
    
    // public Chesspiece pupaLetter;     // 蝶蛹字
    
    public int CurrentTotalScore { get; set; }       // 当前获得的总分数
    public int OptimalTotalScore { get; private set; } // 当前关卡的理论最高总分
    public int EarnedPupaThisLevel { get; private set; } = 0; // 👇 🌟 专门记录这“这一关”到底获取了几个蝶蛹
    // 🌟 新增：记录树叶玩法在“这一局”内有没有因为答错被永久隐藏
    public bool IsLeafDeadThisLevel { get; set; } = false;
    // 🌟 新增：记录本关卡总共生成过几片叶子（用于皮肤循环切换）
    public int LeafGenCounter
    {
        get
        {
            // 防御性编程：若底层数据尚未完成初始化，自动返回0安全兜底
            if (GameDataManager.Instance == null || GameDataManager.Instance.ButterflyData == null) 
                return 0;
                
            return GameDataManager.Instance.ButterflyData.leafSkinCounter;
        }
        set
        {
            if (GameDataManager.Instance != null && GameDataManager.Instance.ButterflyData != null)
            {
                GameDataManager.Instance.ButterflyData.leafSkinCounter = value;
                
                // 每次发生数据变动（如通关时累加），立刻批准调用物理SaveData落盘，打扫干净战场！
                GameDataManager.Instance.ButterflyData.SaveData();
            }
        }
    } 
    public StimulateRuleConfig CurrentMatchedRule { get; private set; }
    public int CurrentBannerStyle { get; private set; }
    public float LinearZenPercent { get; private set; }
    public float DisplayZenPercent { get; private set; }  // 展示用超越百分比（0~99.98）

    // 🌟 新增：标记当前关卡是否是被跳过的
    public bool IsCurrentStageSkipped { get; set; } = false;
    #endregion

    #region 属性封装
    public ChessPackInfo PackInfos => StagePackInfo;
    public int CurrentStage
    {
        get => GameDataManager.Instance.UserData.CurrentChessStage;
        set => GameDataManager.Instance.UserData.CurrentChessStage = value;
    }
    #endregion
    #region 初始化方法

    public void Initialized()
    {
        CoroutineRunner.StartCoroutine(LoadDynamicConfig());
    }
    /// <summary>
    /// 加载当前语言的关卡配置
    /// </summary>
    private IEnumerator LoadDynamicConfig()
    {
        string levelConfigName = GameDataManager.Instance.UserData.ABName == "1" ? "ChessPackInfo_B" : "ChessPackInfo_A";   
        
        StagePackInfo = AssetBundleLoader.SharedInstance.LoadScriptableObject(ToolUtil.GetLanguageBundle(), levelConfigName) as ChessPackInfo;
        if (StagePackInfo == null)
        {
            StagePackInfo = AssetBundleLoader.SharedInstance.LoadScriptableObject("chinesesimplified", levelConfigName) as ChessPackInfo;
        }
        Debug.LogWarning("当前初始化的关卡包是 " +levelConfigName);
        // 🌟 1. 各个在线配置的异步状态与账本数据准备
        bool isComboDone = false;
        bool isMechanicsDone = false;
        bool isStimulateDone = false;

        string comboCsvData = null;
        string mechanicsCsvData = null;
        string stimulateCsvData = null;
        // 🌟 2. 并发拉取机制：每个配置独立分配 Key，同时向服务器发出请求，不互相阻塞
        // A. 拉取连击配置
        CoroutineRunner.StartCoroutine(APIGateway.Instance.GameConfigApi.GetGameConfig("ComboConfig",
            onSuccess: (response) => { comboCsvData = response.CsvString; isComboDone = true; },
            onError: (error) => { isComboDone = true; Debug.Log("⚠️ 服务器拉取 ComboConfig 失败，准备使用本地资源兜底: " + error); }
        ));
        // B. 拉取核心机制配置 (冰块、花朵、树叶)
        CoroutineRunner.StartCoroutine(APIGateway.Instance.GameConfigApi.GetGameConfig("Mechanics",
            onSuccess: (response) => { mechanicsCsvData = response.CsvString; isMechanicsDone = true; },
            onError: (error) => { isMechanicsDone = true; Debug.Log("⚠️ 服务器拉取 Mechanics 失败，准备使用本地资源兜底: " + error); }
        ));
        // C. 拉取关卡完结横幅与鼓励词配置
        CoroutineRunner.StartCoroutine(APIGateway.Instance.GameConfigApi.GetGameConfig("Stimulate",
            onSuccess: (response) => { stimulateCsvData = response.CsvString; isStimulateDone = true; },
            onError: (error) => { isStimulateDone = true; Debug.Log("⚠️ 服务器拉取 Stimulate 失败，准备使用本地资源兜底: " + error); }
        ));
        // 🌟 3. 统一收网守候：用最大时间窗口安全等待所有线上的数据结账
        float timeout = 5f;
        while ((!isComboDone || !isMechanicsDone || !isStimulateDone) && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
        // 🌟 4. 数据落地清算与安全退路（Fallbacks）
        // ======= A. 结算连击配置 =======
        if (string.IsNullOrEmpty(comboCsvData))
        {
            TextAsset comboCsvObj = AssetBundleLoader.SharedInstance.LoadTextFile("gameinfo", "cypz_ComboConfig");
            comboCsvData = comboCsvObj?.text;
        }
        if (!string.IsNullOrEmpty(comboCsvData))
        {
            LoadComboConfig(comboCsvData);
        }
        else
        {
            Debug.LogError("严重错误：连击配置表（在线/本地）全部加载失败，请核对资源名！");
        }
        // ======= B. 结算核心游戏机制（冰/花/叶） =======
        if (string.IsNullOrEmpty(mechanicsCsvData))
        {
            TextAsset mechainCsvObj = AssetBundleLoader.SharedInstance.LoadTextFile("gameinfo", "cypz_Mechanics");
            mechanicsCsvData = mechainCsvObj?.text;
        }
        if (!string.IsNullOrEmpty(mechanicsCsvData))
        {
            LoadMechainConfig(mechanicsCsvData);
        }
        else
        {
            Debug.LogError("严重错误：游戏机制配置（冰/花/叶）（在线/本地）全部加载失败！");
        }
        // ======= C. 结算结算横幅与鼓励词 =======
        if (string.IsNullOrEmpty(stimulateCsvData))
        {
            TextAsset stimulateCsvObj = AssetBundleLoader.SharedInstance.LoadTextFile("gameinfo", "cypz_Stimulate");
            stimulateCsvData = stimulateCsvObj?.text;
        }
        if (!string.IsNullOrEmpty(stimulateCsvData))
        {
            LoadStimulateRuleConfig(stimulateCsvData);
        }
        else
        {
            Debug.LogError("严重错误：关卡完成鼓励词配置（在线/本地）全部加载失败！");
        }
        yield return null;
        // 5. 组装完毕，拉起当前进度的关卡沙盒数据
        SetStageData(GameDataManager.Instance.UserData.CurrentChessStage);
    }
    #endregion

    #region 关卡管理
    /// <summary>
    /// 设置指定关卡数据
    /// </summary>
    /// <param name="StageIndex">关卡编号</param>
    public void SetStageData(int StageIndex)
    {
        _limitPuzzleCount = 0;
        IsCurrentStageSkipped = false;
        // 初始化关卡配置
        IsFirstEnterStage = GameDataManager.Instance.IsNewLevelEntry(StageIndex, true);

        CurrStageInfo = CreateStageInfo(StageIndex);
        CurrStageData = GameDataManager.Instance.RetrieveLevelProgress(CurrStageInfo); 
        
        // int ctrlInfoCount = CurrStageInfo.CurrBoardData.Count(p => p.hasIce);
        // int ctrlDataCount = CurrStageData.BoardSnapshot.Count(p => p.hasIce);
        // Debug.LogWarning($"<color=#00FFFF>[冰块追踪-步骤3-控制器沉淀]</color> 核心交付数据：静态配置层中的冰块数 = {ctrlInfoCount} | 即将提供给 UI 的进度快照中的冰块数 = {ctrlDataCount}");
        
        // 如果不是第一次进，且读出来的剩余时间 <= 0，说明玩家上次在失败界面杀了进程！
        if (!CurrStageData.IsFirstEnter && (CurrStageData.RemainingTime <= 0 || CurrStageData.IsPausedOrFailed))
        {
            Debug.LogWarning("[防逃逸系统] 检测到玩家上次在失败界面强退！视作放弃关卡，执行扣除体力惩罚！");
            
            // ① 执行失败惩罚：扣除体力
            GameDataManager.Instance.UserData.ConsumeEnergy(StageIndex, 1, "进程中断消耗");
            GameDataManager.Instance.UserData.SaveData();
            // ② 抹杀坏档：清理他上次卡死的存档文件和内存
            ClearCurrentLevelSave();
            
            // ③ 重新做人：为他生成一份干干净净的全新开局数据
            CurrStageData.InitializeFromStageInfo(CurrStageInfo);
        }
        IsFirstEnterStage = CurrStageData.IsFirstEnter;
        Debug.Log("当前是否首次进入关卡: " + IsFirstEnterStage + " " + CurrStageData.IsFirstEnter);
       
        // 👇 新增：每次进入关卡，重算本关理论最高分
        CalculateOptimalScore();

        // 记录关卡开始时间
        GameDataManager.Instance.UserData.curStageStartTime = DateTime.Now.ToString();
        AnalyticMgr.SetCommonProperties();
        
        // 首次进入关卡的特殊处理, 重置关卡内的分析数据
        if (IsFirstEnterStage)
        {
            GameDataManager.Instance.UserData.GetWordVocabulary().LevelWords.Clear();
            GameDataManager.Instance.UserData.curStageOnlineTime = 0;
            // IsLeafDeadThisLevel = false;
            MaxComboCount = 0; // 🌟 首次进入关卡，最高连击清零
            ComboErrorCount = 0;
            UseCompleteCount = 0;
            UseTipToolCount = 0;
            CurrentTotalScore = 0; // 🌟 首次进入，总分清零
            CurrStageData.CurrentTotalScore = 0; // 🌟 同步进存档
            EarnedPupaThisLevel = 0;
            PuzzleComboCount = 0;
            float energy = GameDataManager.Instance.ChessDynamicHardSave.EnergyValue;
            AnalyticMgr.LevelStart(energy);
            CurrStageData.IsFirstEnter = false;
            GameDataManager.Instance.UserData.curIsEnter = true;
            GameDataManager.Instance.UserData.ClearPuzzleVocabulary();
        }
        else
        {
            CurrentTotalScore = CurrStageData.CurrentTotalScore;
            PuzzleComboCount = CurrStageData.CurrentCombo;
            MaxComboCount = CurrStageData.MaxCombo;
            EarnedPupaThisLevel = CurrStageData.EarnedPupaCount;
        }

        // if (CurrStageData.PupaDatas != null)
        // {
        //     pupaLetter = CurrStageData.PupaDatas;
        // }

        GameDataManager.Instance.UpdateLevelProgress(CurrStageData);
        CheckRateUsConditions(StageIndex);
        
        foreach (var puzzle in CurrStageData.FoundTargetPuzzles)
        {
            GameDataManager.Instance.UserData.AddStagePuzzle(puzzle);
        }
        CurrStageData.SaveToFile();
        // Debug.Log("添加完成后: "+ JsonConvert.SerializeObject(GameDataManager.Instance.UserData.GetWordVocabulary().LevelWords));
    }

    /// <summary>
    /// 创建关卡数据
    /// </summary>
    private ChessStageInfo CreateStageInfo(int stageIndex, bool isAi = false)
    {
        int wordCount= ChessDynamicHardManager.Instance.CheckLevelHardChange(stageIndex);
      
        int actualStageId = CalculateActualStageId(stageIndex);
        
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
        PuzzleComboCount = 0;
    }

    /// <summary>
    /// 检查评分弹窗条件
    /// </summary>
    private void CheckRateUsConditions(int stageIndex)
    {
        var userData = GameDataManager.Instance.UserData;

        // 第9关首次触发
        if (stageIndex == 6 && userData.showRateusCount <= 0)
        {
            SystemManager.Instance.ShowPanel(PanelType.RateUsScreen);
            return;
        }

        // 每日通关条件
        if (userData.dayPassStageCount == 6 && 
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
    
    /// <summary>
    /// 更新连击状态
    /// </summary>
    /// <param name="isCorrect">当前回答是否正确</param>
    /// <param name="currentWordErrorCount">当前回答是否正确</param>
    /// <param name="timeSpent">当前时间</param>
    public bool OnUpdateRewardPuzzle(bool isCorrect, int currentWordErrorCount, float timeSpent = 0f)
    {
        if (isCorrect)
        {
            // 🌟 检查是否发呆超时断连击
            if (PuzzleComboCount > 0)
            {
                int timeLimit = GetComboTimeLag(PuzzleComboCount);
                if (timeSpent > timeLimit)
                {
                    // 超时了！连击无情断掉
                    PuzzleComboCount = 0;
                    Debug.Log($"[连击断裂] 找词耗时 {timeSpent:F1}秒，超过了当前连击允许的 {timeLimit}秒限制！");
                }
            }
            
            HandleCorrectAnswer();
            return false;
        }
        else
        {
            PuzzleComboCount = 0;
            CurrStageData.CurrentCombo = 0;
            int penalty = 0;
            bool isPunished = false; // 🌟 新增标记
            
            if (currentWordErrorCount > 0 && _reduceConfigDict.Count > 0)
            {
                int maxErrorKey = _reduceConfigDict.Keys.Max();
                int targetKey = Mathf.Min(currentWordErrorCount, maxErrorKey);
                if (_reduceConfigDict.TryGetValue(targetKey, out var config))
                {
                    penalty = config.Num;
                    if (penalty > 0) penalty = -penalty; // 强转负数
                }
            } 
            if (penalty != 0) 
            {
                int oldScore = CurrentTotalScore;
                CurrentTotalScore += penalty; 
                if (CurrentTotalScore < 0) CurrentTotalScore = 0;
                
                CurrStageData.CurrentTotalScore = CurrentTotalScore;
                int realDiff = CurrentTotalScore - oldScore;
                EventDispatcher.instance.TriggerChessScoreChanged(CurrentTotalScore,realDiff);
                
                Debug.Log($"[触发惩罚] 同一个词连错 {currentWordErrorCount} 次，扣除 {Mathf.Abs(penalty)} 分，当前总分降至: {CurrentTotalScore}");
                isPunished = true;
            }
            return isPunished; // 🌟 返回是否被惩罚了
        }
    }
    // --- 🌟 新增：供 UI 实时调用的接口 ---
    /// <summary>
    /// 获取当前连击剩余时间的百分比 (0.0f ~ 1.0f)
    /// 供 ChessPlayArea 在 Update 中刷新连击进度条
    /// </summary>
    public float GetComboTimeProgress()
    {
        if (PuzzleComboCount <= 0) return 0f;

        int timeLimit = GetComboTimeLag(PuzzleComboCount);
        float elapsed = Time.time - LastCorrectWordTimestamp;
    
        float progress = 1f - (elapsed / timeLimit);
        return Mathf.Clamp01(progress);
    }
    /// <summary>
    /// 检查连击是否已经由于发呆而自动失效
    /// </summary>
    public void CheckAndResetComboOnIdle()
    {
        if (PuzzleComboCount <= 0) return;

        if (GetComboTimeProgress() <= 0)
        {
            PuzzleComboCount = 0;
            Debug.Log("[连击失效] 玩家发呆过久，连击自动结束");
            // 这里可以触发一个事件通知 UI 隐藏连击特效
            EventDispatcher.instance.TriggerChessScoreChanged(CurrentTotalScore,0); 
        }
    }
    /// <summary>
    /// 处理正确答案逻辑
    /// </summary>
    private void HandleCorrectAnswer()
    {
        int baseScore = GetBaseScore();
        int comboBonus = GetComboScoreReward(PuzzleComboCount);
      
        int currentScoreEarned = baseScore + comboBonus;
        CurrentTotalScore += currentScoreEarned;
        
        PuzzleComboCount++;
        MaxComboCount = Mathf.Max(MaxComboCount, PuzzleComboCount);
        CurrStageData.CurrentTotalScore = CurrentTotalScore;
        CurrStageData.CurrentCombo = PuzzleComboCount;
        CurrStageData.MaxCombo = MaxComboCount;
        CurrStageData.SaveToFile();
        EventDispatcher.instance.TriggerChessScoreChanged(CurrentTotalScore,currentScoreEarned );
    }
    
    #endregion

    #region 关卡流程控制
    
    /// <summary>
    /// 【阶段一：结账期】在游戏刚结束、任何动画都没播之前，瞬间结算所有分数并决定横幅样式
    /// </summary>
    public void FinalizeStageData(bool isJump)
    {
        // 🚨 如果是跳过关卡，不加分，不计算超越百分比，不抽横幅
        if (isJump)
        {
          
            IsCurrentStageSkipped = true;
            CurrentTotalScore = 0;
            PuzzleComboCount = 0;
            MaxComboCount = 0;
            EarnedPupaThisLevel = 0;
            _limitPuzzleCount = 0;
            if (CurrStageData != null)
            {
                CurrStageData.CurrentTotalScore = 0;
                CurrStageData.CollectedLeaves = 0;
                CurrStageData.EarnedPupaCount = 0;
            }

            return;
        }

        // 1. 计算树叶附带的【禅意分】奖励
        int collectedLeaves = CurrStageData.CollectedLeaves;
        int leafZenReward = 0;
        if (collectedLeaves > 0 && LeafConfig != null && LeafConfig.IsOpen)
        {
            List<LeafReward> earnedRewards = GetAllLeafRewards(collectedLeaves);
            foreach (var reward in earnedRewards)
            {
                if (reward.Type == 12) leafZenReward += reward.Value; // 莲花大满贯
            }
        }

        // 2. 将树叶分正式并入总分
        CurrentTotalScore += leafZenReward;
        
        CurrStageData.CurrentTotalScore = CurrentTotalScore; // 同步内存

        // 3. 计算最终百分比与决定使用哪款横幅
        // 这一步里你原有的代码已经处理了 sMax 理论最高分和百分比的计算
        CalculateEndGameBanner(); 
    }
    /// <summary>
    /// 完成关卡主逻辑
    /// </summary>
    public void CompleteStage(int stageNumber, int wordErrorCount, bool isJump = false)
    {
        ComboErrorCount = wordErrorCount;

        if (CurrStageData.GoldLeafCount > 0 && !isJump)
        {
            GameDataManager.Instance.UserData.UpdateGoldLeaf(CurrStageData.GoldLeafCount);
        }
        CoroutineRunner.StartCoroutine(CompleteStageRoutine(stageNumber,isJump));
        GoldLeafChessViews.Clear();
    }

    /// <summary>
    /// 关卡完成协程
    /// </summary>
    /// <param name="stageNumber"></param>
    /// <param name="isJump"></param>
    private IEnumerator CompleteStageRoutine(int stageNumber, bool isJump)
    {
        ActiveTileSize = 0;
        if (stageNumber == CurrentStage)
        {
            GameDataManager.Instance.UserData.UpdateChessStage();
        }

        if (!isJump)
        {
            bool isButterflyFinished = ButterfliesManager.Instance.IsAllButterfliesCollected();
            // 1. 【优先计算树叶奖励】
            int collectedLeaves = CurrStageData.CollectedLeaves;
            int leafGoldReward = 0;
            int leafPupaReward = 0;
            if (collectedLeaves > 0)
            {
                List<LeafReward> earnedRewards = GetAllLeafRewards(collectedLeaves);
                foreach (var reward in earnedRewards)
                {
                    if (reward.Type == 0) leafGoldReward = reward.Value;
                    else if (reward.Type == 11 && !isButterflyFinished) leafPupaReward = reward.Value;
                    // else if (reward.Type == 12) leafZenReward = reward.Value; // 莲花大满贯
                }
            }
            // 蝶蛹计算
            int basePupa = 0;
            int threshold = ButterfliesManager.Instance.GetScoreThresholdForPupa();
            if (!isButterflyFinished && ButterfliesManager.Instance.CanShowPupaProgressBarThisLevel(OptimalTotalScore))
            {
                basePupa = CurrentTotalScore >= threshold ? 1 : 0;
            }
            EarnedPupaThisLevel = basePupa + leafPupaReward;
            if (EarnedPupaThisLevel > 0)
            {
                GameDataManager.Instance.ButterflyData.AddPupa(EarnedPupaThisLevel);
            }
            // if(leafGoldReward > 0)
            // {
            //     GameDataManager.Instance.UserData.UpdateGold(leafGoldReward, false, false, "树叶收集结算");
            // }
            DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedPassLevel, 1);
            if (CheckLeafMechanic(stageNumber, out _))
            {
                LeafGenCounter++;
                Debug.Log($"[树叶轮询] 成功通关一个树叶关卡！当前树叶轮询总进度为: {LeafGenCounter}");
            }
        }
        yield return PlayCompletionEffects(stageNumber, isJump);
        GameDataManager.Instance.CommitGameData();
        ClearCurrentLevelSave();
    }

    /// <summary>
    /// 播放关卡完成效果 纯粹的 UI 切换与打点，不再包含任何业务算分逻辑
    /// </summary>
    private IEnumerator PlayCompletionEffects(int stageNumber, bool isJump)
    {
        if (!isJump) AudioManager.Instance.PlaySoundEffect("success");

        //yield return new WaitForSeconds(0.4f);
        
        // ---- 修复：统一封存最后一段在线时长 ----
        GameDataManager.Instance.UserData.UpdateOnlineStageTime();
        float duration = GameDataManager.Instance.UserData.curStageOnlineTime;
        float energy = GameDataManager.Instance.ChessDynamicHardSave.EnergyValue;
        if (!isJump)
        {
            AnalyticMgr.LevelCompleted(duration,energy,CurrentTotalScore,MaxComboCount);
            GameDataManager.Instance.UserData.dayPassStageCount++;
            GameDataManager.Instance.UserData.zenCount += CurrentTotalScore;
            if(ChessDynamicHardManager.Instance.IsOpenDynamicHard())
                CheckDynamicDifficultyIntervention(stageNumber,ComboErrorCount, duration);
        }
        
        AdRuleManager.Instance.TryShowInterstitial((issuccess) =>
        {
            if (issuccess)
            {
                AnalyticMgr.InsetAdSuccess("关卡插屏");
                GameDataManager.Instance.UserData.totalSeeAds++;
            }
            else
            {
                AnalyticMgr.InsetAdFail("关卡插屏");
            }
        });
        //yield return new WaitForSeconds(0.4f);
        
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
    
    public void UpdateGoldLeafCount(int value)
    {
        //CurrStageData.UpdateGoldLeafCount(value);
    }

    public int LimitPuzzleCount
    {
        get => _limitPuzzleCount;
        set => _limitPuzzleCount = value; 
    }
    // 2. 专门提供一个获取“翻倍后奖励”的方法
    public int GetDoubleRewardedPuzzleCount()
    {
        bool canshow = LimitTimeManager.Instance.LimitTimeCanShow();
        return LimitPuzzleCount * (canshow ? 2 : 1);
    }

    /// <summary>
    /// 添加已找到的词语
    /// </summary>
    public void AddFoundPuzzle(string puzzle, float timeSpent = 0f)
    {
        CurrStageData.FoundTargetPuzzles ??= new List<string>();
        CurrStageData.FoundTargetPuzzles.Add(puzzle);
        GameDataManager.Instance.UserData.AddStagePuzzle(puzzle);
        _limitPuzzleCount += 1;

        OnUpdateRewardPuzzle(true,0, timeSpent);
        LastCorrectWordTimestamp = Time.time;
    }
    /// <summary>
    /// 修改棋盘内的值
    /// </summary>
    public void ModifyChreepiece(Chesspiece chesspiece)
    {
        // CurrStageData.BoardSnapshot.Remove(chesspiece);
        // CurrStageData.BoardSnapshot.Add(chesspiece);
        CurrStageData.BoardSnapshot.RemoveWhere(p => p.row == chesspiece.row && p.col == chesspiece.col);
        
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
    #region 连击与计分系统
    /// <summary>
    /// 加载并解析连击配置表 CSV (需要在游戏初始化时调用)
    /// </summary>
    /// <param name="csvText">CSV 文件的纯文本内容</param>
    public void LoadComboConfig(string csvText)
    {
        _comboConfigDict.Clear();
        _reduceConfigDict.Clear(); // 记得清空旧的扣分字典
        
        // 按行分割，支持不同操作系统的换行符
        string[] lines = csvText.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        
        // 从索引 2 开始遍历 (跳过第0行中文表头 和 第1行英文表头)
        for (int i = 2; i < lines.Length; i++)
        {
            string[] cols = lines[i].Split(',');
            if (cols.Length >= 3) // 至少要有状态、连击数、数值
            {
                string state = cols[0].Trim().ToLower();
                int combo = int.Parse(cols[1]);
                int num = int.Parse(cols[2]);
                
                // 解析时间窗口 (如果为空或0，默认给个极大值代表无限制)
                int timeLag = 999999;
                if (cols.Length >= 4 && !string.IsNullOrEmpty(cols[3]))
                {
                    int.TryParse(cols[3], out timeLag);
                }

                ComboConfig config = new ComboConfig
                {
                    State = state,
                    Combo = combo,
                    Num = num,
                    TimeLag = timeLag
                };
                // 👇 分门别类存入不同的字典
                if (state == "add") _comboConfigDict[combo] = config;
                else if (state is "reduce" or "sub") _reduceConfigDict[combo] = config;
            }
        }
        Debug.Log($"连击配置解析完成后: {JsonConvert.SerializeObject(_comboConfigDict)}");
    }
    
    /// <summary>
    /// 获取当前连击数对应的【额外连击加分】 (绝对不包含基础分)
    /// </summary>
    public int GetComboScoreReward(int combo)
    {
        if (combo <= 0 || _comboConfigDict.Count == 0) return 0;
        
        int maxCombo = _comboConfigDict.Keys.Max();
        int targetCombo = Mathf.Min(combo, maxCombo);

        if (_comboConfigDict.TryGetValue(targetCombo, out var config))
        {
            return config.Num;
        }
        return 0;
    }
    
    /// <summary>
    /// 获取填对一个词的【基础分】 (0连击时的分数，兜底为5分)
    /// </summary>
    public int GetBaseScore()
    {
        return _comboConfigDict.TryGetValue(0, out var config0) ? config0.Num : 5;
    }
    
    /// <summary>
    /// 获取当前连击数允许的最大间隔时间
    /// </summary>
    public int GetComboTimeLag(int combo)
    {
        if (_comboConfigDict.Count == 0) return 999999;

        int maxCombo = _comboConfigDict.Keys.Max();
        int targetCombo = Mathf.Min(combo, maxCombo);

        if (_comboConfigDict.TryGetValue(targetCombo, out var config))
        {
            // 配置表里如果没有配时间（例如0连击时），给予极大值不限制
            return config.TimeLag > 0 ? config.TimeLag : 999999; 
        }
        return 999999;
    }
    /// <summary>
    /// 计算本关的理论最高分 (即：0失误、全程连击不断的情况)
    /// </summary>
    public void CalculateOptimalScore()
    {
        // 假设 PhraseGroups 代表本关所有的目标词数量
        int totalWordsInStage = CurrStageInfo.PhraseGroups.Count; 
        OptimalTotalScore = 0;
        
        int baseScore = GetBaseScore();
        
        for (int i = 0; i < totalWordsInStage; i++)
        {
            // 模拟从 0 连击一直连到通关
            OptimalTotalScore += (baseScore + GetComboScoreReward(i));
        }

        // 兜底，防止分母为 0
        if (OptimalTotalScore <= 0) OptimalTotalScore = 1; 
    }
    
    /// <summary>
    /// 提供给 UI 层调用的方法：获取当前分数的进度比例 (0.0f ~ 1.0f)
    /// </summary>
    public float GetScoreProgressRatio()
    {
        if (OptimalTotalScore == 0) return 0f;
        return Mathf.Clamp01((float)CurrentTotalScore / OptimalTotalScore);
    }
    #endregion
    
    /// <summary>
    /// 彻底清理当前关卡的游戏状态 (包括内存数据和本地物理存档)
    /// 供玩家中途强行退出、或失败放弃时调用
    /// </summary>
    public void ClearCurrentLevelSave()
    {
        //  获取当前关卡对应的存档文件名并物理删除
        int currentStage = CurrentStage;
        string saveFileName = ChessStageProgressData.CreateLevelIdentifier(currentStage);
        string filePath = Path.Combine(Application.persistentDataPath, saveFileName);
        
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            Debug.Log($"[Controller] 已彻底物理清理关卡 {currentStage} 的游戏状态存档！");
        }
        GameDataManager.Instance.ClearChessLevelCache(currentStage);
        // 3. 同步重置内存中的存档对象标志位
        if (CurrStageData != null)
        {
            CurrStageData.IsFirstEnter = true;
            // CurrStageData.CurrentTotalScore = 0;
            CurrStageData.RemainingTime = 300f;
        }
    }
    /// <summary>
    /// 检查当前关卡是否有未完成的残余存档 (用于判断异常退出)
    /// </summary>
    public bool HasUnfinishedSave()
    {
        int currentStage = CurrentStage;
        string saveFileName = ChessStageProgressData.CreateLevelIdentifier(currentStage);
        string filePath = Path.Combine(Application.persistentDataPath, saveFileName);
        
        // 如果文件存在，说明玩家上次没打完就强退了
        return File.Exists(filePath); 
    }
    
    #region 新玩法配置获取 (冰块、花朵、树叶)

    private void LoadMechainConfig(string text)
    {
        string[] lines = text.Split(new[] { '\n', '\r'}, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrEmpty(line))
            .ToArray();
    
        string[] cols = lines.Last().Split(',',StringSplitOptions.RemoveEmptyEntries);
        // 定义一个安全的布尔值解析局部方法 (C# 7.0+)
        bool TryParseBool(string[] arr, int index, bool defaultValue = false)
        {
            if (arr == null || index < 0 || index >= arr.Length) 
                return defaultValue; // 防止 IndexOutOfRangeException
            
            string val = arr[index].Trim().ToLower();
        
            // 兼容配置表常见的 1/0 写法
            if (val == "1" || val == "true") return true;
            if (val == "0" || val == "false") return false;
        
            // 标准 TryParse 后备
            if (bool.TryParse(val, out bool result)) return result;
        
            return defaultValue; // 无法解析时返回默认值，防止崩溃
        }
        string[] opens = cols[0].Split("_");
        if (cols.Length > 3)
        {
            IceConfig = new IceConfig
            {
                IsOpen = TryParseBool(opens,0),
                Fixed = new Dictionary<int, int>(),
                CycleLevels = new List<Interval>(),
                Degree = new Dictionary<int, int>()
            };
            string[] degree = cols[1].Split('_');
            for (int i = 0; i < degree.Length; i++)
            {
                IceConfig.Degree.Add(i, int.Parse(degree[i]));
            }
            string[] levels = cols[2].Split(';');
            string[] first = levels[0].Split('_');  
            IceConfig.FirstLevel = int.Parse(first[0]);
            IceConfig.FirstDegree = int.Parse(first[1]);
            for (int i = 1; i < levels.Length; i++)
            { 
                degree = levels[i].Split('_');
                IceConfig.Fixed.Add(int.Parse(degree[0]), int.Parse(degree[1]));
            }
            string[] cycles = cols[3].Split(';');
            for (int i = 0; i < cycles.Length; i++)
            {
                string[] mode = cycles[i].Split('#');
                degree = mode[1].Split("*");
                string[] steps = degree[0].Split('_');
                Interval interval = new Interval
                {
                    Mode = int.Parse(mode[0]),
                    Degree = int.Parse(degree[1]),
                    Start = int.Parse(steps[0])
                };
                if (steps.Length >= 2 && int.TryParse(steps[1], out int end)) 
                    interval.End = end;
                else interval.End = int.MaxValue;
                IceConfig.CycleLevels.Add(interval);
            }
            
            Debug.Log("解析完成后的冰块配置: "+ JsonConvert.SerializeObject(IceConfig));
        }

        if (cols.Length >= 5)
        {
            LeafConfig = new LeafConfig
            {
                IsOpen = TryParseBool(opens,1),
                FirstLevel = int.Parse(cols[4]),
                CycleLevels = new List<int>(),
                Rewards = new List<LeafReward>()
            };
            string[] cycles = cols[5].Split('_');
            for (int i = 0; i < cycles.Length; i++)
            {
                LeafConfig.CycleLevels.Add(int.Parse(cycles[i]));
            }
            string[] rewardGroups = cols[6].Split('_');
            HashSet<(string NumberRaw, int Type)> seen = new HashSet<(string, int)>();
            foreach (string groupStr in rewardGroups)
            {
                if (string.IsNullOrWhiteSpace(groupStr)) continue;
                string[] rewards = groupStr.Split(';');

                foreach (string r in rewards)
                {
                    string[] parts = r.Split('#');
                    if (parts.Length < 2) continue;

                    string[] rewardValue = parts[1].Split('*');
                    if (rewardValue.Length < 2) continue;

                    int type = int.Parse(rewardValue[1]);
                    LeafReward leafReward = new LeafReward
                    {
                        Type = type,
                        Value = int.Parse(rewardValue[0])
                    };

                    // 解析 Number 字段（支持 n、n-1 等）
                    string numStr = parts[0];
                    if (int.TryParse(numStr, out int num))
                    {
                        leafReward.Number = num;
                    }
                    else if (numStr == "n")
                    {
                        leafReward.Number = -1;
                    }
                    else if (numStr.StartsWith("n-") && int.TryParse(numStr.Substring(2), out int delta))
                    {
                        leafReward.Number = -delta;
                    }
                    else
                    {
                        throw new FormatException($"无法解析的数量: {numStr}");
                    }
                    var key = (numStr, type);
                    if (seen.Contains(key)) continue;
                    seen.Add(key);
                    LeafConfig.Rewards.Add(leafReward);
                }
            }
            Debug.Log("解析完成后的树叶配置: "+ JsonConvert.SerializeObject(LeafConfig));
        }

        if (cols.Length >= 9)
        {
            string[] levels = cols[9].Split(';');
            string[] first = levels[0].Split('_');
            string[] removes = cols[8].Split("_");
            FlowerConfig = new FlowerConfig
            {
                IsOpen = TryParseBool(opens,2),
                FirstLevel = int.Parse(first[0]),
                FirstDegree = int.Parse(first[1]),
                InitNumber = int.Parse(removes[0]),
                MinNumber = int.Parse(removes[1]),
                Degree = new Dictionary<int, int>(),
                Fixed = new Dictionary<int, int>(),
                CycleLevels = new List<Interval>(),
            };
            for (int i = 1; i < levels.Length; i++)
            {
                string[] next = levels[i].Split('_');    
                FlowerConfig.Fixed.Add(int.Parse(next[0]), int.Parse(next[1]));
            }
            string[] degrees = cols[7].Split("_");
            for (int i = 0; i < degrees.Length; i++)
            {
                FlowerConfig.Degree.Add(i,int.Parse(degrees[i]));
            }
            string[] cycles = cols[10].Split(';');
            for (int i = 0; i < cycles.Length; i++)
            {
                string[] mode = cycles[i].Split('#');
                string[] degree = mode[1].Split("*");
                string[] steps = degree[0].Split('_');
                Interval interval = new Interval
                {
                    Mode = int.Parse(mode[0]),
                    Degree = int.Parse(degree[1]),
                    Start = int.Parse(steps[0]),
                };
                if (steps.Length >= 2 && int.TryParse(steps[1], out int end))
                    interval.End = end;
                else interval.End = int.MaxValue;
                
                FlowerConfig.CycleLevels.Add(interval);
            }
            Debug.Log("解析完成后的花朵配置: "+ JsonConvert.SerializeObject(FlowerConfig));
        }
    }
    /// <summary>
    /// 辅助方法：获取关卡难度枚举 (从 UI 抽离到底层计算)
    /// </summary>
    public LevelModes GetLevelDifficultyMode(int levelNumber) 
    {
        if (levelNumber % 5 == 0) {
            if ((levelNumber / 5) % 2 == 1) return LevelModes.Hard;
            else return LevelModes.ExtraHard;
        }
        return LevelModes.Normal;
    }
    /// <summary>
    /// 获取当前关卡的冰块配置状态
    /// </summary>
    /// <param name="stageIndex">要查询的关卡ID</param>
    /// <param name="isFirstTime">是否首次出现（用于触发新手引导）</param>
    /// <param name="degree">输出的难度层级</param>
    /// <returns>当前关卡是否生成冰块</returns>
    public bool CheckIceMechanic(int stageIndex, out bool isFirstTime, out int degree)
    {
        isFirstTime = false;
        degree = 0;
        
        if (IceConfig == null || !IceConfig.IsOpen) return false;
        if (stageIndex < IceConfig.FirstLevel) return false;
        
        bool hasMechanic = false;
        // 1. 检查是否为首次出现 (优先级最高)
        if (stageIndex == IceConfig.FirstLevel)
        {
            hasMechanic = true;
            degree = IceConfig.FirstDegree;
        }
        else
        // 2. 检查特定关卡固定难度配置
        if (IceConfig.Fixed != null && IceConfig.Fixed.TryGetValue(stageIndex, out int fixedDegree))
        {
            hasMechanic = true;
            degree = fixedDegree;
        }
        else 
        // 3. 检查循环/区间配置
        if (IceConfig.CycleLevels != null)
        {
            LevelModes curMode = GetLevelDifficultyMode(stageIndex);
            
            foreach (var interval in IceConfig.CycleLevels)
            {
                // 💡 提示：JSON 里的 "!" 如果用 int 接收会报错，建议在反序列化时将 "!" 转为 -1 或 0
                // 这里把 <=0 或 int.MaxValue 当作 "无限后续关卡"
                bool isInfinity = interval.End <= 0 || interval.End == int.MaxValue;
                // 判断是否在区间内
                if (stageIndex >= interval.Start && (isInfinity || stageIndex <= interval.End))
                {
                    // 匹配关卡模式 (0为不限, 1为困难, 2为极难)
                    if (interval.Mode == 0 || 
                       (interval.Mode == 1 && curMode == LevelModes.Hard) || 
                       (interval.Mode == 2 && curMode == LevelModes.ExtraHard))
                    {
                        hasMechanic = true;
                        degree = interval.Degree;
                        break; // 找到匹配的区间就跳出
                    }
                }
            }
        }
        if (hasMechanic)
        {
            // 🌟 核心兼容：只要当前关卡有冰块，且玩家本地未完成引导，就强制触发新手引导！
            // 注意：请在引导结束时执行 GameDataManager.Instance.UserData.HasGuidedIce = true;
            bool hasGuided = GameDataManager.Instance.UserData.ChessTutorialProgress.ContainsKey(6) && 
                             GameDataManager.Instance.UserData.ChessTutorialProgress[6];
            isFirstTime = !hasGuided;
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// 获取当前关卡的花朵配置状态 (逻辑与冰块相同)
    /// </summary>
    public bool CheckFlowerMechanic(int stageIndex, out bool isFirstTime, out int degree)
    {
        isFirstTime = false;
        degree = 0;
        
        if (FlowerConfig == null || !FlowerConfig.IsOpen) return false;
        if (stageIndex < FlowerConfig.FirstLevel) return false;
        
        bool hasMechanic = false;
        
        if (stageIndex == FlowerConfig.FirstLevel)
        {
            hasMechanic = true;
            degree = FlowerConfig.FirstDegree;
        }
        else if (FlowerConfig.Fixed != null && FlowerConfig.Fixed.TryGetValue(stageIndex, out int fixedDegree))
        {
            hasMechanic = true;
            degree = fixedDegree;
        }
        else if (FlowerConfig.CycleLevels != null)
        {
            LevelModes curMode = GetLevelDifficultyMode(stageIndex);
            foreach (var interval in FlowerConfig.CycleLevels)
            {
                bool isInfinity = interval.End <= 0 || interval.End == int.MaxValue;
                if (stageIndex >= interval.Start && (isInfinity || stageIndex <= interval.End))
                {
                    if (interval.Mode == 0 || 
                        (interval.Mode == 1 && curMode == LevelModes.Hard) || 
                        (interval.Mode == 2 && curMode == LevelModes.ExtraHard))
                    {
                        hasMechanic = true;
                        degree = interval.Degree;
                        break;
                    }
                }
            }
        }
        if (hasMechanic)
        {
            // 🌟 完美兼容：查阅花朵(7)是否已经引导过
            bool hasGuided = GameDataManager.Instance.UserData.ChessTutorialProgress.ContainsKey(7) && 
                             GameDataManager.Instance.UserData.ChessTutorialProgress[7];
            isFirstTime = !hasGuided; 
            return true;
        }
        return false;
    }
    /// <summary>
    /// 获取当前关卡的树叶配置状态
    /// </summary>
    /// <returns>当前关卡是否有树叶收集玩法</returns>
    public bool CheckLeafMechanic(int stageIndex, out bool isFirstTime)
    {
        isFirstTime = false;
        if (LeafConfig == null || !LeafConfig.IsOpen) return false;
        if (CurrStageData != null && CurrStageData.CollectedLeaves >= 10)
        {
            return false;
        }
        if (stageIndex < LeafConfig.FirstLevel) return false;
        
        bool hasMechanic = false;
        // 1. 首次出现
        if (stageIndex == LeafConfig.FirstLevel)
        {
            hasMechanic = true;
        }
        else // 2. 检查循环关卡 (判断个位数)
        if (LeafConfig.CycleLevels != null && LeafConfig.CycleLevels.Count > 0)
        {
            int unitDigit = stageIndex % 10; // 提取当前关卡的个位数
            if (LeafConfig.CycleLevels.Contains(unitDigit))
            {
                hasMechanic = true;
            }
        }
        if (hasMechanic)
        {
            // 🌟 完美兼容：查阅树叶(8)是否已经引导过
            bool hasGuided = GameDataManager.Instance.UserData.ChessTutorialProgress.ContainsKey(8) && 
                             GameDataManager.Instance.UserData.ChessTutorialProgress[8];
            isFirstTime = !hasGuided; 
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// 🌟 修复：获取玩家当前收集数量下，所有已解锁的树叶奖励（累加机制）
    /// </summary>
    public List<LeafReward> GetAllLeafRewards(int collectedCount)
    {
        if (LeafConfig == null || LeafConfig.Rewards == null || LeafConfig.Rewards.Count == 0) 
            return new List<LeafReward>();
        bool isButterflyFinished = ButterfliesManager.Instance.IsAllButterfliesCollected();
        int maxLeavesInStage = CurrStageInfo.PhraseGroups.Count; 
        Debug.Log($"[树叶奖励] 当前收集叶子数: {collectedCount}，本关成语总数 n = {maxLeavesInStage}");

        return LeafConfig.Rewards.Where(r => 
        {
            int requireNum = r.Number;
            if (r.Number == -1) requireNum = maxLeavesInStage - 1;
            if (r.Number < -1) requireNum = maxLeavesInStage + (r.Number + 1);
            Debug.Log($"[树叶奖励] 配置：NumberRaw={r.Number}, Type={r.Type}, Value={r.Value}, 解析后需要数量={requireNum}, 是否满足={requireNum <= collectedCount}");
            if (requireNum > collectedCount) return false;
            if (isButterflyFinished && r.Type == 11) return false;
            // 核心修复：只要目标需求量 <= 当前收集量，全都要！
            return true;
        }).ToList();
    }
    #endregion

    #region StimulateRule 激励词规则

    private void LoadStimulateRuleConfig(string assetText)
    {
        StimulateRules.Clear();
        List<string> lines  = ToolUtil.SplitCsvLines(assetText);
        for (int i = 2; i < lines.Count; i++)
        {
            string[] fields = ParseCsvLineAndCleanQuotes(lines[i]);
            // 健壮性防御：如果当前行是彻底的空行，直接跳过
            if (fields == null || fields.Length == 0 || string.IsNullOrEmpty(fields[0])) continue;
            try
            {
                string[] banners = fields[0].Split(';', StringSplitOptions.RemoveEmptyEntries);
                StimulateRuleConfig stimulateRule = new StimulateRuleConfig
                {
                    BannerTypes = new BannerType[banners.Length], TitleRate = 0, StimulateRate = 0,
                };
                for (int j = 0; j < banners.Length; j++)
                {
                    string[] bType = banners[j].Split('_');
                    stimulateRule.BannerTypes[j] = new BannerType
                    {
                        Number = int.Parse(bType[0]),
                        Rate =  int.Parse(bType[1])
                    };
                }

                string[] tRates = fields[1].Split(';', StringSplitOptions.RemoveEmptyEntries);
                if (tRates.Length >= 2)
                {
                    // 🌟 修复 2：优雅地切出 _ 后面的数字，替代危险的 Substring(-1)
                    stimulateRule.TitleRate = int.Parse(tRates[0].Split('_')[1]);
                    stimulateRule.StimulateRate = int.Parse(tRates[1].Split('_')[1]);
                }

                stimulateRule.ScatterFlowers = (fields[2] == "1"); // 如果填1表示开启撒花
                stimulateRule.Priority = string.IsNullOrEmpty(fields[3]) ? 0 : int.Parse(fields[3]);
                stimulateRule.Type = string.IsNullOrEmpty(fields[4]) ? 0 : int.Parse(fields[4]);
                if (!string.IsNullOrEmpty(fields[5]))
                {
                    string[] percents = fields[5].Split('_');
                    if (percents.Length >= 2)
                    {
                        stimulateRule.ZenPercent = new[] { int.Parse(percents[0]), int.Parse(percents[1]) };
                    }
                }

                stimulateRule.TitleKey = fields[6];
                stimulateRule.PhraseKey = fields[7];
                stimulateRule.EmojiKey = fields[8];
                stimulateRule.LongTextKey = fields[9];
                StimulateRules.Add(stimulateRule);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"解析第 {i} 行数据时崩溃! 内容: {lines[i]} | 错误: {ex.Message}\n{ex.StackTrace}");
            }
        }

        Debug.Log("鼓励词配置解析完成! " + JsonConvert.SerializeObject(StimulateRules));
    }
    /// <summary>
    /// 辅助方法：完美切分 CSV 列，并剥离外围双引号，同时保留单元格内容中的换行与逗号
    /// </summary>
    private string[] ParseCsvLineAndCleanQuotes(string line)
    {
        var list = new List<string>();
        var cur = new System.Text.StringBuilder();
        bool inQuote = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (i + 1 < line.Length && line[i + 1] == '"')
                {
                    cur.Append('"'); // 转义双引号
                    i++;
                }
                else
                {
                    inQuote = !inQuote; // 只切换状态，外层包裹的引号不录入内容
                }
            }
            else if (c == ',' && !inQuote)
            {
                list.Add(cur.ToString().Trim());
                cur.Clear();
            }
            else
            {
                cur.Append(c);
            }
        }
        list.Add(cur.ToString().Trim());
        return list.ToArray();
    }
    
    /// <summary>
    /// 根据概率配置，权重随机抽取横幅样式编号
    /// </summary>
    public int GetRandomBannerStyle(BannerType[] banners)
    {
        if (banners == null || banners.Length == 0) return 1; // 兜底返回样式1

        int totalWeight = banners.Sum(b => b.Rate);
        if (totalWeight <= 0) return banners[0].Number;

        int randomVal = UnityEngine.Random.Range(0, totalWeight);
        int currentWeight = 0;

        foreach (var b in banners)
        {
            currentWeight += b.Rate;
            if (randomVal < currentWeight)
            {
                return b.Number;
            }
        }

        return banners[0].Number; // 理论上不会走到这里，兜底
    }
    /// <summary>
    /// 核心算法：计算通关成绩并仲裁最合适的鼓励配置
    /// </summary>
    public void CalculateEndGameBanner()
    {
        var stageData = CurrStageData;
        var stageInfo = CurrStageInfo;
        var userData = GameDataManager.Instance.UserData;

        int n = stageInfo.PhraseGroups.Count; // 当前关卡成语总数
        int stageIndex = stageData.StageId; // 当前关卡ID
        // 🌟【核心修复】：在算百分比时，先确认并当场加上还没来得及加的树叶禅意分，确保绝对同步
        int collectedLeaves = stageData.CollectedLeaves;
        Debug.Log("收集的叶子数量" + collectedLeaves);
        int pendingLeafZenReward = 0;
        if (collectedLeaves > 0 && LeafConfig != null && LeafConfig.IsOpen)
        {
            var earnedRewards = GetAllLeafRewards(collectedLeaves);
            Debug.Log("查看所有的叶子奖励" + JsonConvert.SerializeObject(earnedRewards));
            foreach (var reward in earnedRewards)
            {
                if (reward.Type == 12) pendingLeafZenReward += reward.Value; // 莲花大满贯禅意分
            }
        }
        // 实际总分 = 游戏得分 - 树叶得分
        int pureGameScore = CurrentTotalScore - pendingLeafZenReward;
        Debug.Log($"当前计算的实际分数： {pureGameScore} , 总分{CurrentTotalScore}, 树叶分{pendingLeafZenReward}");
        float timeSpent = stageData.TotalActiveSeconds;

        // 1. 理论极值与禅意百分比计算
        int baseScore = GetBaseScore();
        int sMin = n * baseScore;
        int sMax = sMin;
        for (int i = 0; i < n; i++)
        {
            sMax += GetComboScoreReward(i);
        }
        LinearZenPercent = (sMax > sMin) ? Mathf.Max(0, (float)(pureGameScore - sMin) / (sMax - sMin) * 100f) : 100f;
        if (sMax > 0)
        {
            //计算时用的是 pureGameScore / Smax，并非 (pureGameScore - Smin) / (Smax - Smin)，完全符合规则描述。
            float ratio = Mathf.Clamp01((float)pureGameScore / sMax);
            float sqrtPercent = Mathf.Sqrt(ratio) * 100f;
            DisplayZenPercent = Mathf.Min(99.98f, sqrtPercent);
        }
        else
        {
            DisplayZenPercent = 99.98f;
        }

        float zenPercent = LinearZenPercent;   // 用于激励匹配
        // 2. 本地记录判定：破纪录与极速通关
        bool isNewRecord = pureGameScore > userData.HighestZenScore;
        if (isNewRecord)
        {
            userData.HighestZenScore = pureGameScore; // 刷新最高分记录
        }

        bool isNewBestTime = false;
        if (n >= 8 && stageIndex > 20) // 是否添加大于20关才记录
        {
            float currentBest = userData.GetBestClearTime(n);
            if (currentBest <= 0 || timeSpent < currentBest)
            {
                isNewBestTime = true;
                userData.SetBestClearTime(n, timeSpent); // 刷新极速记录
                Debug.Log($"[极速记录核对] 关卡: {stageIndex} 规格: {n}个词 | 本局耗时: {timeSpent:F2}秒 已保存");
            }
        }

        // 其他判定条件
        // 【Type 2】：叶子全部收集
        bool isAllLeavesCollected = stageData.CollectedLeaves >= n; 
        // 【Type 4】：不仅百分百禅意分(超越比达99.98或分数达max)，还需获取叶子最后一档奖励(通常为10片)
        bool isMaxZenAndLastLeaf = (LinearZenPercent >= 99.98f || pureGameScore >= sMax) && (stageData.CollectedLeaves >= 10);
        // 【Type 6】：没有使用任何提示或完成工具，且通过了困难/极难关卡
        bool isHardOrExtraLevel = (CurLevelMode == LevelModes.Hard || CurLevelMode == LevelModes.ExtraHard);
        bool noAnyToolsUsed = (UseTipToolCount == 0 && UseCompleteCount == 0);
        bool isHardLevelNoPressure = isHardOrExtraLevel && noAnyToolsUsed;
        // 3. 过滤并匹配规则表 (优先保证 StimulateRules 已经解析并赋值)
        List<StimulateRuleConfig> validRules = new List<StimulateRuleConfig>();
        foreach (var rule in StimulateRules) 
        {
            bool match = false;
            switch (rule.Type)
            {
                case 0: // 🌟 处理 1-10 关以内的禅意分
                    if (stageIndex <= 9 && rule.ZenPercent != null && rule.ZenPercent.Length >= 2)
                    {
                        // 处理 100_100 等右区间闭合的情况
                        if (rule.ZenPercent[1] >= 100)
                            match = (zenPercent >= rule.ZenPercent[0] && zenPercent <= rule.ZenPercent[1]);
                        else
                            match = (zenPercent >= rule.ZenPercent[0] && zenPercent < rule.ZenPercent[1]);
                    }
                    break;
                case 1: // 禅意百分比
                    if (stageIndex > 9 && rule.ZenPercent != null && rule.ZenPercent.Length >= 2)
                    {
                        if (rule.ZenPercent[1] >= 100) // 右边界包含100
                            match = (zenPercent >= rule.ZenPercent[0] && zenPercent <= rule.ZenPercent[1]);
                        else
                            match = (zenPercent >= rule.ZenPercent[0] && zenPercent < rule.ZenPercent[1]);
                    }
                    break;
                case 2: if (isAllLeavesCollected) match = true; break; // 2-叶子全收集
                case 3: if (isNewBestTime) match = true; break; // 3-极速通关 (8个词以上, 20关以后且刷新时间)
                case 4: if (isMaxZenAndLastLeaf) match = true; break;
                case 5: if (isNewRecord && stageIndex > 20) match = true; break;
                case 6: if (isHardLevelNoPressure) match = true; break;
            }
            if (match) validRules.Add(rule);
        }

        // 4. 优先级仲裁与表现层概率抽卡
        CurrentMatchedRule = validRules.OrderBy(r => r.Priority)
            .ThenBy(r => 
            (r.Type == 0 || r.Type == 1) && r.ZenPercent != null && r.ZenPercent.Length >= 2 
                ? r.ZenPercent[1] - r.ZenPercent[0]   // 区间越小越靠前
                : int.MaxValue).FirstOrDefault();

        if (CurrentMatchedRule != null)
        {
            CurrentBannerStyle = GetRandomBannerStyle(CurrentMatchedRule.BannerTypes);
        }
        else
        {
            CurrentBannerStyle = 1; // 兜底标准样式
        }
        // =========================================================================
        // 🌟 新增：详细日志打印区
        // =========================================================================
        System.Text.StringBuilder log = new System.Text.StringBuilder();
        log.AppendLine($"<color=#00FFFF>【激励横幅结算报告】当前关卡: {stageIndex}</color>");
        log.AppendLine($"<color=#00FF00>[关卡统计] 词组总数(N): {n} 个 | 通关实际耗时: {timeSpent:F2} 秒</color>");
        log.AppendLine($"最低分(全基础): {sMin}  最高分(全连击): {sMax}  差值: {sMax - sMin}");
        log.AppendLine($"[核心数据] 实际得分: {pureGameScore} | 理论满分: {sMax} | 原始得分比: {LinearZenPercent:F4}");
        log.AppendLine($"[禅意进度] 最终展示超越百分比 (BeyondPercent): <color=#00FF00>{DisplayZenPercent:F2}%</color>");
        log.AppendLine($"[触发状态] 叶子全收集:{isAllLeavesCollected} | 极速通关:{isNewBestTime} | 完美通关:{isMaxZenAndLastLeaf} | 新纪录:{isNewRecord} | 困难无压力:{isHardLevelNoPressure}");
        
        log.Append("[合格规则池] ");
        if (validRules.Count > 0)
        {
            foreach (var r in validRules)
            {
                log.Append($"<Type:{r.Type}, 优先级Priority:{r.Priority}> ");
            }
        }
        else
        {
            log.Append("无");
        }
        log.AppendLine();

        if (CurrentMatchedRule != null)
        {
            log.AppendLine($"<color=#FFFF00>[最终仲裁] 胜出规则 -> Type: {CurrentMatchedRule.Type} | Priority: {CurrentMatchedRule.Priority}</color>");
            log.AppendLine($"标题Key:{CurrentMatchedRule.TitleKey}  文案Key:{CurrentMatchedRule.PhraseKey}  表情Key:{CurrentMatchedRule.EmojiKey}");
            log.AppendLine($"长文案Key:{CurrentMatchedRule.LongTextKey}  撒花:{CurrentMatchedRule.ScatterFlowers}");
            log.AppendLine($"[样式抽取] 最终抽取的 BannerStyle: {CurrentBannerStyle}");
            log.AppendLine($"[配置抽取] 最终抽取的规则配置: {JsonConvert.SerializeObject(CurrentMatchedRule)}");
        }
        else
        {
            log.AppendLine("<color=#FF0000>[异常兜底] 没有匹配到任何规则！使用默认兜底样式: 1</color>");
        }
        
        Debug.Log(log.ToString());
    }
    
    #endregion
}