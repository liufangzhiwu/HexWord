using System;
using System.Collections.Generic;
using UnityEngine;

public enum BeginnerBonusState
{
    NotStarted, // 未开启
    Start,      // 刚解锁，首次开启展示
    InProgress, // 进行中
    Completed,  // 达到30关，已完成
    Claimed,    // 🌟 新增：已领奖，活动终身结束
    Expired     // 超过24小时，已超时失效
}

public class BeginnerBonusManager
{
    private static readonly BeginnerBonusManager _instance = new BeginnerBonusManager();
    public static BeginnerBonusManager Instance => _instance;

    public const int TARGET_STAGE = 30;
    public const int UNLOCK_STAGE = 5;
    public const float DURATION_HOURS = 24f;
    public const int REWARD_GOLD = 500;

    // 通关时需主动弹窗的关卡节点
    private static readonly HashSet<int> PopupStages = new HashSet<int> { 7, 10, 13, 17, 20, 23, 27, 30 };

    // 结算页激励文案节点
    private static readonly HashSet<int> IncentiveStages = new HashSet<int> { 12, 16, 21, 25, 29 };

    private BeginnerBonusManager() { }

    /// <summary>
    /// 获取当前活动综合状态
    /// </summary>
    public BeginnerBonusState GetCurrentState()
    {
        var user = GameDataManager.Instance.UserData;
        if (user.isBeginnerBonusCompleted) return BeginnerBonusState.Claimed;
        // 错兜底：如果关卡已经到了第5关以上，但由于时序问题未标记触发，自动补齐开启
        if (!user.isBeginnerBonusTriggered && user.CurrentChessStage > UNLOCK_STAGE+1)
        {
            // 【核心修复】识别新玩家刚通关第5关进入结算页的瞬间：
            // 此时 CurrentChessStage 已经变为 6，但正常开启逻辑在随后的 CheckStageCleared(5) 才会正式触发。
            // 只有当玩家刚好处于结算页面且关卡正好是6时，暂时放行 (NotStarted)，等待正常逻辑接管。
            bool isJustClearedLevel5 = GameCoreManager.Instance != null && 
                                       GameCoreManager.Instance.PanelState == PanelState.FinishPingPanel && 
                                       user.CurrentChessStage == UNLOCK_STAGE + 1;
            if (!isJustClearedLevel5)
            {
                user.isBeginnerBonusTriggered = true;
                user.isBeginnerBonusCompleted = true; // 标记为已完成/已结束
                GameDataManager.Instance.CommitGameData();
                return BeginnerBonusState.Expired; 
            }
            // 只有处于第 5、6 关的新玩家（可能因为闪退等没正常触发），才执行容错开启
            // StartActivity();
        }
        
        if (!user.isBeginnerBonusTriggered) return BeginnerBonusState.NotStarted;
        if (user.isBeginnerBonusCompleted) return BeginnerBonusState.Completed;

        if (IsExpired())
        {
            return BeginnerBonusState.Expired;
        }

        int currentProgress = GetCurrentProgress();
        if (currentProgress >= TARGET_STAGE)
        {
            return BeginnerBonusState.Completed;
        }

        return BeginnerBonusState.InProgress;
    }

    /// <summary>
    /// 是否在活动有效期内（用于入口与文案显隐判定）
    /// </summary>
    public bool IsActivityAlive()
    {
        var state = GetCurrentState();
        return state is BeginnerBonusState.InProgress 
            or BeginnerBonusState.Start 
            or BeginnerBonusState.Completed;
    }
    /// <summary>
    /// 获取当前进度的上一个阶段节点，作为动画的起点
    /// </summary>
    public int GetPreviousMilestone(int currentProgress)
    {
        int maxPrevious = 0; // 兜底起点为 0

        // 1. 如果进度超过了解锁关卡 (5)，那么上一节点至少是 5
        if (currentProgress > UNLOCK_STAGE)
        {
            maxPrevious = UNLOCK_STAGE;
        }

        // 2. 遍历 HashSet 里的所有弹窗节点，找出小于当前进度的最大值
        foreach (int stage in PopupStages)
        {
            if (stage < currentProgress && stage > maxPrevious)
            {
                maxPrevious = stage;
            }
        }

        return maxPrevious;
    }
    
    /// <summary>
    /// 活动是否超时
    /// </summary>
    public bool IsExpired()
    {
        var user = GameDataManager.Instance.UserData;
        if (string.IsNullOrEmpty(user.beginnerBonusStartTime)) return false;
        if (DateTime.TryParse(user.beginnerBonusStartTime, out var startTime))
        {
            return (DateTime.Now - startTime).TotalHours >= DURATION_HOURS;
        }
        return false;
    }

    /// <summary>
    /// 获取剩余倒计时时间跨度
    /// </summary>
    public TimeSpan GetRemainingTime()
    {
        var user = GameDataManager.Instance.UserData;
        if (string.IsNullOrEmpty(user.beginnerBonusStartTime)) return TimeSpan.Zero;
        if (DateTime.TryParse(user.beginnerBonusStartTime, out var startTime))
        {
            DateTime endTime = startTime.AddHours(DURATION_HOURS);
            TimeSpan remain = endTime - DateTime.Now;
            return remain > TimeSpan.Zero ? remain : TimeSpan.Zero;
        }
        return TimeSpan.Zero;
    }

    /// <summary>
    /// 获取当前关卡进度（解锁时即拥有5关进度）
    /// </summary>
    public int GetCurrentProgress()
    {
        var user = GameDataManager.Instance.UserData;
        // 如果未触发，返回 0
        if (!user.isBeginnerBonusTriggered) return 0;

        // 如果已经领奖标记完成，直接拉满返回 30
        if (user.isBeginnerBonusCompleted) return TARGET_STAGE;
        
        // 拼字关卡取 CurrentChessStage，若为常规关卡则根据实际字段调整
        int currentStage = user.CurrentChessStage -1;
        return Mathf.Clamp(currentStage, 0, TARGET_STAGE);
    }

    /// <summary>
    /// 开启活动（仅终身首次）
    /// </summary>
    public void StartActivity()
    {
        var user = GameDataManager.Instance.UserData;
        if (user.isBeginnerBonusTriggered) return;

        user.isBeginnerBonusTriggered = true;
        user.beginnerBonusStartTime = DateTime.Now.ToString("o");
        GameDataManager.Instance.CommitGameData();
    }

    /// <summary>
    /// 在关卡结算（CompleteStageRoutine）调用：检查是否开启活动或触发阶段弹窗
    /// </summary>
    /// <param name="completedStage">刚刚通关的关卡号</param>
    /// <param name="isJump">是否跳关</param>
    /// <returns>是否拦截并弹窗展示</returns>
    public bool CheckStageCleared(int completedStage, bool isJump)
    {
        if (!WillPopupOnStageCleared(completedStage)) return false;

        // 1. 第5关通关（无论正常过关或跳关），触发终身首次开启
        if (completedStage == UNLOCK_STAGE)
        {
            StartActivity();
            SystemManager.Instance.ShowPanel(PanelType.BeginnerBonusPanel);
            return true;
        }

        // 2/3. 目标关（30）达标领奖 / 活动期内的进度节点
        SystemManager.Instance.ShowPanel(PanelType.BeginnerBonusPanel);
        return true;
    }

    /// <summary>
    /// 【纯判定】通关 completedStage 这关后，结算页是否<b>会</b>弹新手奖励进度弹窗。
    /// 与 CheckStageCleared 同口径，但<b>不弹窗、不开活动、不改存档</b>，
    /// 供结算页礼包调度提前预判（礼包不与该弹窗同时出现）。
    /// </summary>
    public bool WillPopupOnStageCleared(int completedStage)
    {
        if (GameDataManager.Instance == null || GameDataManager.Instance.UserData == null) return false;

        var user = GameDataManager.Instance.UserData;

        // 1. 第5关通关（无论正常过关或跳关），触发终身首次开启
        if (completedStage == UNLOCK_STAGE) return true;

        // 2. 通关第30关（达成目标，且未领奖），必定弹窗
        if (completedStage >= TARGET_STAGE && !user.isBeginnerBonusCompleted && !IsExpired()) return true;

        // 3. 活动期间内的特定进度节点弹窗
        return IsActivityAlive() && PopupStages.Contains(completedStage);
    }
    
    /// <summary>
    /// 领取最终奖励（500金币）
    /// </summary>
    public void ClaimReward()
    {
        var user = GameDataManager.Instance.UserData;
        if (user.isBeginnerBonusCompleted) return;

        user.isBeginnerBonusCompleted = true;
        user.UpdateGold(REWARD_GOLD, true, true, "新手奖励活动完成");
        GameDataManager.Instance.CommitGameData();
    }
}