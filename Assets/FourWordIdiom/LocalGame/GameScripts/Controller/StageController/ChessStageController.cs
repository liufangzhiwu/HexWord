using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FourWordIdiom.LocalGame.GameScripts.Controller;
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
public partial class ChessStageController
{
    #region 单例实现
    private static readonly ChessStageController _instance = new ChessStageController();

    public static ChessStageController Instance => _instance;

    public LevelModes CurLevelMode;
    
    private ChessStageController() { }
    #endregion
    
    public bool IsJump { get; set; }


    #region 运行时数据
    public ChessStageInfo CurrStageInfo { get; private set; }           // 当前关卡配置数据
    public ChessStageProgressData CurrStageData { get; private set; }   // 当前关卡进度数据
    public PuzzleData PuzzleData = new PuzzleData();
    public bool IsEnterPuzzle { get; set; }          //是否从目标词进入
    public bool IsEnterVocabulary { get; set; }       // 是否进入关卡内
    public bool IsFirstEnterStage { get; private set; } = true;  // 是否首次进入当前关卡

    public int _limitPuzzleCount = 0;              // 限时活动连词计数
    public int PuzzleComboCount { get;  set; } = 0;  // 当前正在维持的连击 连续正确次数
    public int TotalCumulativeCombos { get;  set; } = 0;  // 累计有效连击总次数
    public int MaxComboCount { get; private set; } = 0;   // 本关达到过的最大连续数字
    public float LastCorrectWordTimestamp { get;  set; } // 记录上一个词找对的绝对时间点
    public int ComboErrorCount { get;  set; } = 0;   // 连续错误次数
    public int UseTipToolCount { get;  set; } = 0;   // 使用提示工具次数
    public int UseCompleteCount { get; set; } = 0;  // 使用完成工具的字数
    
    public float ActiveTileSize { get; set; }      // 字块显示尺寸
    /// <summary> 皮肤金箔字 </summary>
    public readonly List<ChessView> GoldLeafChessViews = new List<ChessView>();
    
    public int CurrentTotalScore { get; set; }       // 当前获得的总分数
    public int OptimalTotalScore { get; private set; } // 当前关卡的理论最高总分
    public int EarnedPupaThisLevel { get; private set; } = 0; // 👇 🌟 专门记录这“这一关”到底获取了几个蝶蛹
    public StimulateRuleConfig CurrentMatchedRule { get; private set; }
    public int CurrentBannerStyle { get; private set; }    // 过关横幅选取
    public float LinearZenPercent { get; private set; }    // 计算连击百分比
    public float DisplayZenPercent { get; private set; }  // 展示用超越百分比（0~99.98）

    /// <summary>
    /// 记录每个 FeedbackID 上次触发的真实游戏时间 (Time.time)
    /// </summary>
    private Dictionary<int, float> _lastPraiseTriggerTimes = new Dictionary<int, float>();
    // 👇 记录全局最后一次触发任何鼓励动效的时间
    private float _lastGlobalPraiseTime = -999f; 
    // 👇 设定横幅在屏幕上停留的霸体时间 (与 UI 层的 2.0f 保持一致)
    private const float GLOBAL_PRAISE_COOLDOWN = 2.0f;
    private List<PraiseContext> _praiseQueue = new List<PraiseContext>();
    // 🌟 新增：标记当前关卡是否是被跳过的
    public bool IsCurrentStageSkipped { get; set; } = false;
    
    public List<string> CurrentLevelFourCharWords { get; private set; } = new List<string>();
    public List<string> CurrentLevelOtherWords { get; private set; } = new List<string>();
    #endregion

    #region 关卡管理
    /// <summary>
    /// 设置指定关卡数据
    /// </summary>
    /// <param name="StageIndex">关卡编号</param>
    public void SetStageData(int StageIndex)
    {
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
            CurrentLevelFourCharWords.Clear();
            CurrentLevelOtherWords.Clear();
            
            GameDataManager.Instance.UserData.GetWordVocabulary().LevelWords.Clear();
            GameDataManager.Instance.UserData.curStageOnlineTime = 0;
            ResetStageState(StageIndex);
            float energy = GameDataManager.Instance.ChessDynamicHardSave.EnergyValue;
            AnalyticMgr.LevelStart(energy);
            GameDataManager.Instance.UserData.curIsEnter = true;
            GameDataManager.Instance.UserData.ClearPuzzleVocabulary();
        }
        else
        {
            CurrentTotalScore = CurrStageData.CurrentTotalScore;
            PuzzleComboCount = CurrStageData.CurrentCombo;
            MaxComboCount = CurrStageData.MaxCombo;
            EarnedPupaThisLevel = CurrStageData.EarnedPupaCount;
            TotalCumulativeCombos = CurrStageData.TotalCumulativeCombos; 
            
            CurrentLevelFourCharWords.Clear();
            CurrentLevelOtherWords.Clear();
            
            foreach (var puzzle in CurrStageData.FoundTargetPuzzles)
            {
                if (puzzle.Length == 4) CurrentLevelFourCharWords.Add(puzzle);
                else CurrentLevelOtherWords.Add(puzzle);
            }
        }

        GameDataManager.Instance.UpdateLevelProgress(CurrStageData);
        CheckRateUsConditions(StageIndex);
        
        foreach (var puzzle in CurrStageData.FoundTargetPuzzles)
        {
            GameDataManager.Instance.UserData.AddStagePuzzle(puzzle);
        }
        CurrStageData.SaveToFile();
    }

    /// <summary>
    /// 创建关卡数据
    /// </summary>
    public ChessStageInfo CreateStageInfo(int stageIndex, bool isAi = false)
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
        MaxComboCount = 0; // 🌟 首次进入关卡，最高连击清零
        TotalCumulativeCombos = 0; 
        ComboErrorCount = 0;
        UseCompleteCount = 0;
        UseTipToolCount = 0;
        CurrentTotalScore = 0; // 🌟 首次进入，总分清零
        CurrStageData.CurrentTotalScore = 0; // 🌟 同步进存档
        EarnedPupaThisLevel = 0;
        CurrStageData.IsFirstEnter = false;
    }

    /// <summary>
    /// 检查评分弹窗条件
    /// </summary>
    private void CheckRateUsConditions(int stageIndex)
    {
        var userData = GameDataManager.Instance.UserData;

        // 第9关首次触发
        if (stageIndex == 8 && userData.showRateusCount <= 0)
        {
            SystemManager.Instance.ShowPanel(PanelType.RateUsScreen);
            return;
        }

        // 每日通关条件
        if (userData.dayPassStageCount == 8 && 
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
    private float GetComboTimeProgress()
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
        
        if (PuzzleComboCount > 0)
        {
            TotalCumulativeCombos++;
        }
        PuzzleComboCount++;
        MaxComboCount = Mathf.Max(MaxComboCount, PuzzleComboCount);
        CurrStageData.CurrentTotalScore = CurrentTotalScore;
        CurrStageData.CurrentCombo = PuzzleComboCount;
        CurrStageData.MaxCombo = MaxComboCount;
        CurrStageData.TotalCumulativeCombos = TotalCumulativeCombos;
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
        IsJump=isJump;
        // 🚨 如果是跳过关卡，不加分，不计算超越百分比，不抽横幅
        if (isJump)
        {
          
            IsCurrentStageSkipped = true;
            CurrentTotalScore = 0;
            PuzzleComboCount = 0;
            MaxComboCount = 0;
            MaxComboCount = 0;
            MaxComboCount = 0;
            EarnedPupaThisLevel = 0;
            _limitPuzzleCount = 0;
            if (CurrStageData != null)
            {
                CurrStageData.CurrentTotalScore = 0;
                CurrStageData.CollectedLeaves = 0;
                CurrStageData.CurBreakIceCount = 0;
                CurrStageData.CurPickFlowerLeavesCount = 0;
                CurrStageData.CurrPerfectCount = false;
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
            // 保存数据
            bool isButterflyFinished = ButterfliesManager.Instance.IsPupaSufficientForAllRemaining();
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
            
            GameDataManager.Instance.UserData.UpdateOnlineStageTime();
            GameDataManager.Instance.UserData.dayPassStageCount++;
            GameDataManager.Instance.UserData.chessdayPassStageCount++;
            // 👉 禅意分，在这里真正加进内存！
            GameDataManager.Instance.UserData.zenCount += CurrentTotalScore;
            GameDataManager.Instance.UserData.overallZenScore += CurrentTotalScore;
            GameDataManager.Instance.UserData.fourWordCount += CurrentLevelFourCharWords.Count;
            GameDataManager.Instance.UserData.nofourWordCount += CurrentLevelOtherWords.Count;
            GameDataManager.Instance.UserData.MaxComboCount = Mathf.Max(MaxComboCount, GameDataManager.Instance.UserData.MaxComboCount);

        }
        GameDataManager.Instance.CommitGameData();
        ClearCurrentLevelSave();
        yield return PlayCompletionEffects(stageNumber, isJump);
    }

    /// <summary>
    /// 播放关卡完成效果 纯粹的 UI 切换与打点，不再包含任何业务算分逻辑
    /// </summary>
    private IEnumerator PlayCompletionEffects(int stageNumber, bool isJump)
    {
        if (!isJump) AudioManager.Instance.PlaySoundEffect("success");

        yield return new WaitForSeconds(0.2f);
        float duration = GameDataManager.Instance.UserData.curStageOnlineTime;
        float energy = GameDataManager.Instance.ChessDynamicHardSave.EnergyValue;
        if (!isJump)
        {
            GameDataManager.Instance.UserData.MaxComboCount =
                Mathf.Max(GameDataManager.Instance.UserData.MaxComboCount, MaxComboCount);
            AnalyticMgr.LevelCompleted(duration, energy, CurrentTotalScore, MaxComboCount);
            if(ChessDynamicHardManager.Instance.IsOpenDynamicHard())
                CheckDynamicDifficultyIntervention(stageNumber, ComboErrorCount, duration);

            int perfect = CurrStageData.CurrPerfectCount ? 1 : 0;

            GameDataManager.Instance.AchieveSaveDataList.UpdateAchieveItemData(AchieveType.DoubleHit1, TotalCumulativeCombos);
            GameDataManager.Instance.AchieveSaveDataList.UpdateAchieveItemData(AchieveType.BreakIce1, CurrStageData.CurBreakIceCount);
            GameDataManager.Instance.AchieveSaveDataList.UpdateAchieveItemData(AchieveType.CollectLeaves1, CurrStageData.CollectedLeaves);
            GameDataManager.Instance.AchieveSaveDataList.UpdateAchieveItemData(AchieveType.PickFlowers1, CurrStageData.CurPickFlowerLeavesCount);
            GameDataManager.Instance.AchieveSaveDataList.UpdateAchieveItemData(AchieveType.Perfect1, perfect);
        }
        yield return new WaitForSeconds(0.1f);
        // UI切换
        SystemManager.Instance.HidePanel(PanelType.HeaderSection);
        SystemManager.Instance.HidePanel(PanelType.ChessPlayArea);

        yield return new WaitForSeconds(0.8f);

        SystemManager.Instance.ShowPanel(PanelType.ChessFinishView);
        SystemManager.Instance.ShowPanel(PanelType.HeaderSection);
        
        
        // ---- 修复：统一封存最后一段在线时长 ----
        GameDataManager.Instance.UserData.UpdateOnlineStageTime();
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
        
        if (!string.IsNullOrEmpty(puzzle))
        {
            if (puzzle.Length == 4) 
                CurrentLevelFourCharWords.Add(puzzle);
            else 
                CurrentLevelOtherWords.Add(puzzle);
        }
        
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
    #endregion
    #region 连击与计分系统
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
    
    #region 新玩法配置获取 (冰块、花朵、树叶)

   
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
    /// 🌟 获取玩家当前收集数量下，所有已解锁的树叶奖励（累加机制）
    /// </summary>
    public List<LeafReward> GetAllLeafRewards(int collectedCount)
    {
        if (LeafConfig == null || LeafConfig.Rewards == null || LeafConfig.Rewards.Count == 0) 
            return new List<LeafReward>();
        bool isButterflyFinished = ButterfliesManager.Instance.IsPupaSufficientForAllRemaining();
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

    #region StimulateRule 过关横幅激励词规则
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
            // float sqrtPercent = Mathf.Sqrt(ratio) * 100f;
            float quadPercent = Mathf.Pow(ratio, 0.25f) * 100f;
            DisplayZenPercent = Mathf.Min(99.98f, quadPercent);
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
        if (n >= 9 && stageIndex > 20) // 是否添加大于20关才记录
        {
            float currentBest = userData.GetBestClearTime(n);
            if (currentBest <= 0)
            {
                isNewBestTime = false;
                userData.SetBestClearTime(n, timeSpent); // 首次进入该规格关卡，仅作为基准时长录入，不触发破纪录表现
                Debug.Log($"[极速记录核对] 首次完成 {n} 个词关卡: {stageIndex} | 建立基准耗时: {timeSpent:F2}秒 已保存");
                
            }
            else if (timeSpent < currentBest)
            {
                // 真正打破了已有的旧记录
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
        stageData.CurrPerfectCount = LinearZenPercent >= 99.98f || pureGameScore >= sMax;
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

    #region  填词鼓励系统
    private bool _isProcessingQueue = false;
    public void EnqueuePraiseCheck(PraiseContext context)
    {
        // 将这次触发加入队列
        _praiseQueue.Add(context);
        // 如果当前没有正在运行的仲裁逻辑，开启处理
        if (!_isProcessingQueue)
        {
            CoroutineRunner.StartCoroutine(ProcessPraiseQueue());
        }
    }

    private IEnumerator ProcessPraiseQueue()
    {
        _isProcessingQueue = true;
    
        // 等待 0.2 秒，让交叉字产生的多次 AddFoundPuzzle 调用全部落入队列
        yield return new WaitForSeconds(0.2f);
        List<PraiseConfig> praiseConfigs = new List<PraiseConfig>();
        foreach (var queue in _praiseQueue)
        {
            var winner = EvaluatePraiseFeedback(queue);
            if(winner != null) praiseConfigs.Add(winner);
        }
        
        if (praiseConfigs.Count > 0)
        {
            // 从队列中选出一个优先级最高的进行仲裁
            // 这里你可以根据 context 里的信息，或者直接取优先级最高的
            var bestWinner = praiseConfigs.OrderByDescending(w=>w.Priority).First();
            // 触发 UI 播放
            ChessPlayArea.Instance.ShowPraiseUI(bestWinner);
        }
        praiseConfigs.Clear();
        _praiseQueue.Clear();
        _isProcessingQueue = false;
    }
    /// <summary>
    /// 核心方法：评估并获取当前最合适的正反馈配置
    /// </summary>
    public PraiseConfig EvaluatePraiseFeedback(PraiseContext context)
    {
        if (PraiseConfigDict == null || PraiseConfigDict.Count == 0) return null;
        if (CurrentStage == 1) return null;
        System.Text.StringBuilder logSb = new System.Text.StringBuilder();
        logSb.AppendLine($"<color=#00FFFF>【填词鼓励系统 (Praise) 仲裁报告】</color>");
        // ==========================================
        // 🌟 核心修复：全局表现层锁 (防止同帧叠加或快速连击重叠)
        // ==========================================
        // if (Time.time - _lastGlobalPraiseTime < GLOBAL_PRAISE_COOLDOWN)
        // {
        //     logSb.AppendLine($"<color=#FFA500>[表现层拦截] 屏幕上已有横幅正在展示中，直接丢弃本次请求！</color>");
        //     Debug.Log(logSb.ToString());
        //     return null; // 强行中断，不再占用 CPU 去做后面的规则计算
        // }
        
        logSb.AppendLine($"[输入上下文] 首词:{context.IsFirstWord} | 初始字:{context.InitialLettersCount} | 单词试错:{context.ErrorsOnThisWord} | 剩余词:{context.WordsRemaining} | 当前连击:{context.CurrentCombo} | 总试错数:{context.TotalErrorsInLevel}");
        
        List<PraiseConfig> validCandidates = new List<PraiseConfig>();
        
        logSb.AppendLine("[阶段一：基础条件匹配]");
        // 1. 遍历所有规则，匹配触发条件
        foreach (var kvp in PraiseConfigDict)
        {
            PraiseConfig config = kvp.Value;
            bool isMet = IsConditionMet(config.FeedbackID, context, out string reason);
            
            if (isMet)
            {
                validCandidates.Add(config);
                logSb.AppendLine($"  <color=#00FF00>✔ ID {config.FeedbackID}: 匹配成功 ({reason})</color>");
            }
            else
            {
                logSb.AppendLine($"  <color=#808080>✖ ID {config.FeedbackID}: 匹配失败 -> {reason}</color>");
            }
        }

        // 如果没有匹配到任何规则，直接返回
        if (validCandidates.Count == 0)
        {
            logSb.AppendLine("<color=#FF0000>[仲裁中断] 没有任何规则满足基础触发条件。</color>");
            Debug.Log(logSb.ToString());
            return null;
        }
        
        logSb.AppendLine("[阶段二：冷却(CD)过滤]");
        // 2. 过滤冷却时间 (TimeWindow)
        float currentTime = Time.time;
        var afterCdCandidates = new List<PraiseConfig>();
        foreach (var config in validCandidates)
        {
            if (config.TimeWindow <= 0)
            {
                afterCdCandidates.Add(config);
                logSb.AppendLine($"  <color=#00FF00>✔ ID {config.FeedbackID}: 无冷却要求</color>");
            }
            else if (_lastPraiseTriggerTimes.TryGetValue(config.FeedbackID, out float lastTime))
            {
                float passedTime = currentTime - lastTime;
                if (passedTime >= config.TimeWindow)
                {
                    afterCdCandidates.Add(config);
                    logSb.AppendLine($"  <color=#00FF00>✔ ID {config.FeedbackID}: 冷却完毕 (已过 {passedTime:F1}s / 需要 {config.TimeWindow}s)</color>");
                }
                else
                {
                    logSb.AppendLine($"  <color=#FFA500>✖ ID {config.FeedbackID}: 冷却中 (剩余 {config.TimeWindow - passedTime:F1}s)</color>");
                }
            }
            else
            {
                afterCdCandidates.Add(config);
                logSb.AppendLine($"  <color=#00FF00>✔ ID {config.FeedbackID}: 首次触发，无CD阻碍</color>");
            }
        }

        if (afterCdCandidates.Count == 0)
        {
            logSb.AppendLine("<color=#FF0000>[仲裁中断] 命中的规则全部处于冷却中。</color>");
            Debug.Log(logSb.ToString());
            return null;
        }
        
        logSb.AppendLine("[阶段三：概率(Probability)过滤]");
        // 3. 概率掷骰过滤 (Probability)
        // 注意：配置表里的 Probability 是 0.5、1.0 这种浮点数
        var afterProbCandidates = new List<PraiseConfig>();
        foreach (var config in afterCdCandidates)
        {
            if (config.Probability >= 1.0f)
            {
                afterProbCandidates.Add(config);
                logSb.AppendLine($"  <color=#00FF00>✔ ID {config.FeedbackID}: 必触发 (概率 1.0)</color>");
            }
            else
            {
                float roll = UnityEngine.Random.value;
                if (roll <= config.Probability)
                {
                    afterProbCandidates.Add(config);
                    logSb.AppendLine($"  <color=#00FF00>✔ ID {config.FeedbackID}: 概率命中 (Roll={roll:F2} <= 目标={config.Probability})</color>");
                }
                else
                {
                    logSb.AppendLine($"  <color=#FFA500>✖ ID {config.FeedbackID}: 概率未命中 (Roll={roll:F2} > 目标={config.Probability})</color>");
                }
            }
        }

        if (afterProbCandidates.Count == 0)
        {
            logSb.AppendLine("<color=#FF0000>[仲裁中断] 存活的规则全部因脸黑被概率过滤。</color>");
            Debug.Log(logSb.ToString());
            return null;
        }

        logSb.AppendLine("[阶段四：优先级(Priority)仲裁]");
        // 4. 优先级仲裁 (Priority)
        // 假设 Priority 数字越小优先级越高 (1最高)
        PraiseConfig winner = afterProbCandidates.OrderBy(config => config.Priority).First();

        string finalIds = string.Join(", ", afterProbCandidates.Select(c => $"ID:{c.FeedbackID}(优先:{c.Priority})"));
        logSb.AppendLine($"  进入最终仲裁池: [{finalIds}]");
        logSb.AppendLine($"  <color=#FFFF00>👑 胜出者: ID {winner.FeedbackID} (将播放样式: {winner.BannerStyle})</color>");

        Debug.Log(logSb.ToString()); // 统一打印出这篇长长的报告
        // 5. 记录触发时间，并返回赢家
        _lastPraiseTriggerTimes[winner.FeedbackID] = currentTime;
        // 👇 更新全局表现层锁的时间
        // _lastGlobalPraiseTime = currentTime;
        return winner;
    }
    
    /// <summary>
    /// 根据配置表硬编码判断条件。
    /// 规则表说明 (拼字玩法配置表 - Praise)：
    /// ID 1 (样式一/大拇指) - 第一次艰难答对：关卡内解开第一个词，同时满足：1.该词的初始字=2；2.答对之前，判断次数>=2次；（不论判断哪个词）
    /// ID 2 (样式一/大拇指) - 第一次轻松答对：关卡内解开第一个词，同时满足：1.该词的初始字=2；2.答对之前，判断次数<=1；（不论判断哪个词）
    /// ID 3 (样式一/大拇指) - 第一次答对难的词：关卡内解开第一个词，且该词的初始字<=1；
    /// ID 4 (样式二/两个大拇指) - 盲答对：存在某个词，这个词不是关卡内解开的第一个词，且无初始字，并且一次性填对（这个词没有判定过错误）；
    /// ID 5 (样式三/鼓掌) - 反复试错答对：存在某个词，这个词不是关卡内解开的第一个词，连续答错次数>=X(X=3)，且之后答对的也是这个词；
    /// ID 6 (样式四/金色光效) - 临界过关：玩家答对一个词之后，只剩下最后一个词；
    /// ID 7 (长横幅颜色一) - 2连击：连击达2次；
    /// ID 8 (长横幅颜色二) - 5连击：连击达5次；
    /// ID 9 (长横幅颜色三) - 8连击：连击达8次；
    /// ID 10 (长横幅颜色四) - >=11连击：连击达11次及以上。
    /// </summary>
    private bool IsConditionMet(int feedbackID, PraiseContext context, out string reason)
    {
        reason = "";
        switch (feedbackID)
        {
            case 1: // 第一次艰难答对
                if (!context.IsFirstWord) { reason = "必须是本关解开的首个词"; return false; }
                if (context.InitialLettersCount != 2) { reason = $"要求初始字=2 (当前:{context.InitialLettersCount})"; return false; }
                // if (context.ErrorsOnThisWord < 2)
                if (context.TotalErrorsInLevel < 2) { reason = $"要求全局累计错误>=2 (当前:{context.TotalErrorsInLevel})"; return false; }
                reason = "满足: 首词 且 初始字=2 且 错误>=2";
                return true;
            
            case 2: // 第一次轻松答对
                if (!context.IsFirstWord) { reason = "必须是本关解开的首个词"; return false; }
                if (context.InitialLettersCount != 2) { reason = $"要求初始字=2 (当前:{context.InitialLettersCount})"; return false; }
                if (context.TotalErrorsInLevel > 1) { reason = $"要求全局累计错误<=1 (当前:{context.TotalErrorsInLevel})"; return false; }
                reason = "满足: 首词 且 初始字=2 且 几乎没错";
                return true;
            
            case 3: // 第一次答对难的词
                if (!context.IsFirstWord) { reason = "必须是本关解开的首个词"; return false; }
                if (context.InitialLettersCount > 1) { reason = $"要求初始字<=1 (当前:{context.InitialLettersCount})"; return false; }
                reason = "满足: 首词 且 初始字<=1 的难词";
                return true;
            
            case 4: // 盲答对
                if (context.IsFirstWord) { reason = "不能是本关首个词"; return false; }
                if (context.InitialLettersCount != 0) { reason = $"要求无初始字 (当前:{context.InitialLettersCount})"; return false; }
                if (context.ErrorsOnThisWord != 0) { reason = $"要求一次答对零失误 (当前错误:{context.ErrorsOnThisWord})"; return false; }
                reason = "满足: 非首词 且 全空盲答对 且 零失误";
                return true;
            
            case 5: // 反复试错答对 (X=3)
                if (context.IsFirstWord) { reason = "不能是本关首个词"; return false; }
                if (context.ErrorsOnThisWord < 3) { reason = $"要求死磕连续试错>=3次 (当前:{context.ErrorsOnThisWord})"; return false; }
                reason = "满足: 非首词 且 反复试错后答对";
                return true;
            
            case 6: // 临界过关
                if (context.WordsRemaining != 1) { reason = $"要求只剩最后1个词 (当前剩余:{context.WordsRemaining})"; return false; }
                reason = "满足: 临界过关 (仅剩最后1词)";
                return true;
            
            // 连击类判定
            case 7: 
                if (context.CurrentCombo != 2) { reason = $"要求恰好2连击 (当前:{context.CurrentCombo})"; return false; }
                reason = "满足: 2连击";
                return true;
            case 8: 
                if (context.CurrentCombo != 5) { reason = $"要求恰好5连击 (当前:{context.CurrentCombo})"; return false; }
                reason = "满足: 5连击";
                return true;
            case 9: 
                if (context.CurrentCombo != 8) { reason = $"要求恰好8连击 (当前:{context.CurrentCombo})"; return false; }
                reason = "满足: 8连击";
                return true;
            case 10: 
                if (context.CurrentCombo < 11) { reason = $"要求>=11连击 (当前:{context.CurrentCombo})"; return false; }
                reason = "满足: >=11连击";
                return true;

            default:
                reason = "未知的反馈ID";
                return false;
        }
    }
    
    /// <summary>
    /// 供鼓励系统使用：根据刚刚完成的成语字符串，反查它在关卡初始状态下有几个固定字
    /// </summary>
    public int GetPhraseInitialCountByWord(string phrase)
    {
        if (CurrStageInfo == null || CurrStageInfo.PhraseGroups == null) 
            return 0;

        foreach (var group in CurrStageInfo.PhraseGroups)
        {
            // 将组内字块的 letter 拼接成完整词组
            string groupPhrase = string.Join("", group.chesspieces.Select(p => p.letter));
            
            // 如果匹配到了玩家刚刚填对的词
            if (groupPhrase == phrase)
            {
                // 统计该组在初始配置中，状态为 Default (初始固定显示) 的字数
                return group.chesspieces.Count(p => p.isInitialFixed);
            }
        }
        
        return 0; // 兜底
    }
    
    
    #endregion
}