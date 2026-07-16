using System;
using System.Collections.Generic;
using System.Linq;
using Middleware;
using UnityEngine;
using UnityEngine.Rendering;


/// <summary>
/// 奖励项结构
/// </summary>
public struct RewardItem
{
    public int type;   // 0:金币,1:蝴蝶,2:提示,3:重置,4:5min双倍,5:15min双倍,11:蝶蛹,12:禅意值,13:金箔,14:红心,15:特殊头像
    public int amount;
}

public class VictoryRewardConfig
{
    public int streak;
    public List<RewardItem> normalRewards;      // # 前的配置（蝴蝶未完成时使用）
    public List<RewardItem> alternativeRewards; // # 后的配置（蝴蝶已完成时使用）
}

/// <summary>
/// SevenWin 7日连胜签到，StreakWin 连胜签到
/// </summary>
public enum WinType{ Null, SevenWin,StreakWin }

/// <summary>
/// 签到管理器 - 依赖 UserData 中的 SignSaveData
/// 负责执行签到、查询签到状态、管理连胜逻辑
/// </summary>
public class StreakManager : Singleton<StreakManager>
{
    // 引用全局用户数据
    private UserData userData;

    // 事件：签到成功时触发，参数为当前连胜天数
    public event Action<int> OnSignSuccess;
    public bool IswinStreakBreak = false;
    public WinType winType =WinType.Null;
    
    // 奖励配置字典：key=连胜天数，value=奖励配置
    private Dictionary<int, VictoryRewardConfig> rewardConfigs = new Dictionary<int, VictoryRewardConfig>();

    public override void Init()
    {
        // 获取 UserData 实例
        if (userData == null)
        {
            if (GameDataManager.Instance != null)
                userData = GameDataManager.Instance.UserData;
            else
                Debug.LogError("StreakManager: 未找到 UserData 实例，请手动赋值");
        }
        
        TextAsset data = AssetBundleLoader.SharedInstance.LoadTextFile("gameinfo", "DailyVictory");
        if (data != null)
        {
            ParseRewardConfig(data.text);
        }
        else
        {
            Debug.LogError("Failed to load CSV data.");
        }
        
        // 加载数据后立即检查断签，确保连胜数据准确
        CheckAndResetStreakIfBroken();
    }

    /// <summary>
    /// 更新连胜天数
    /// </summary>
    public void UpdateWinStreak()
    {
        DateTime today = DateTime.Today;
        // 2. 检查昨天是否签到（断签检测）
        GameDataManager.Instance.UserData._signSaveData.currentStreak = userData._signSaveData.GetCurrentStreak(today);
    }
    
    
   private void ParseRewardConfig(string csvData)
    {
        string[] lines = csvData.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) return;

        // 从第2行开始（跳过标题行）
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] fields = line.Split(',');
            if (fields.Length < 2) continue;

            // 解析连胜天数
            if (!int.TryParse(fields[0].Trim(), out int streak)) continue;

            // 解析奖励配置字符串
            string rewardStr = fields[1].Trim();
            if (string.IsNullOrEmpty(rewardStr)) continue;

            var config = new VictoryRewardConfig();
            config.streak = streak;

            // 分割 # 前后（正常/替代）
            string[] parts = rewardStr.Split('#');
            string normalPart = parts[0];
            string alternativePart = parts.Length > 1 ? parts[1] : null;

            config.normalRewards = ParseRewardItems(normalPart);
            config.alternativeRewards = alternativePart != null ? ParseRewardItems(alternativePart) : null;

            rewardConfigs[streak] = config;
        }
    }

    private List<RewardItem> ParseRewardItems(string rewardString)
    {
        var items = new List<RewardItem>();
        if (string.IsNullOrEmpty(rewardString)) return items;

        string[] entries = rewardString.Split(';');
        foreach (string entry in entries)
        {
            string trimmed = entry.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // 支持 '_' 或 ' ' 作为分隔
            string[] pair = trimmed.Split(new[] { '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (pair.Length != 2) continue;

            if (int.TryParse(pair[0], out int type) && int.TryParse(pair[1], out int amount))
            {
                items.Add(new RewardItem { type = type, amount = amount });
            }
        }
        return items;
    }
    
    /// <summary>
    /// 判断这个功能是否解锁（基于首次登录天数）
    /// </summary>
    public bool UnlockStreak()
    {
        int lifeDays = 0;
        if (!string.IsNullOrEmpty(userData.firstLoginTime) &&
            DateTime.TryParse(userData.firstLoginTime, out var firstLoginDate))
        {
            lifeDays = (DateTime.Now.Date - firstLoginDate.Date).Days + 1;
        }
        return lifeDays >= 2;
    }
    
    /// <summary>
    /// 检查连胜奖励是否存在 7
    /// </summary>
    /// <returns></returns>
    public bool CheckSevenRewardsExist()
    {
        int currentStreak= GameDataManager.Instance.UserData._signSaveData.currentStreak;

        if (currentStreak % 7 == 0)
        {
            currentStreak = 7;
        }
        else
        {
            return false;
        }
        
        return rewardConfigs.Keys.Contains(currentStreak);
    }

    /// <summary>
    /// 检查连胜宝箱奖励是否存在
    /// </summary>
    /// <returns></returns>
    public bool CheckBoxRewardsExist()
    {
        int curAwardid= GameDataManager.Instance.UserData._signSaveData.curAwardid;
       
        if (curAwardid <= 1)
        {
            return false;
        }
        return rewardConfigs.Keys.Contains(curAwardid);
    }
    
    /// <summary>
    /// 获取连胜签到奖励
    /// </summary>
    /// <returns></returns>
    public VictoryRewardConfig GetSevenSignRewards()
    {
        int currentStreak = 7;
        return rewardConfigs[currentStreak];
    }
    
    /// <summary>
    /// 获取连胜签到奖励
    /// </summary>
    /// <returns></returns>
    public VictoryRewardConfig GetBoxSignRewards()
    {
        int curAwardid= GameDataManager.Instance.UserData._signSaveData.curAwardid;
        return rewardConfigs[curAwardid];
    }
    
    /// <summary>
    /// 获取回归首胜签到奖励
    /// </summary>
    /// <returns></returns>
    public VictoryRewardConfig GetFirstWinSignRewards()
    {
        return rewardConfigs[1];
    }


    // ==================== 断签检测与重置 ====================

    /// <summary>
    /// 检查当前签到连续性，如果发现断签则将 currentStreak 置为 0
    /// 应在游戏启动、数据加载后调用，或在打开签到界面时调用
    /// </summary>
    public void CheckAndResetStreakIfBroken()
    {
        if (userData == null) return;

        var signData = userData._signSaveData;
        if (signData.lastSignDay == 0) return;

        long today = UIUtilities.GetCurrentDayIndex();
        long yesterday = today - 1;

        // 今天已签到
        if (signData.lastSignDay == today)
        {
            // 如果昨天没签，但连胜大于1（异常），修正为1
            if (!IsDaySigned(yesterday) && signData.currentStreak > 1)
            {
                signData.currentStreak = 1;
                userData.SaveData();
            }
            return;
        }
        else
        {
            //今天未签到，检查昨天
            if (!IsDaySigned(yesterday)||!IsDaySigned(today))
            {
                IswinStreakBreak = true;
                //signData.currentStreak = 0;
                userData.SaveData();
            }
            else
            {
                IswinStreakBreak = false;
            }
        }

       
    }

    // ==================== 公共方法 ====================
    
    /// <summary>
    /// 当天是否签到（基于当前时间）
    /// </summary>
    public bool IsCanShowWinSign()
    {
        bool isSigned= IsDailySign();
        bool isLocked= UnlockStreak();
        
        return !isSigned && isLocked;
    }
    
    /// <summary>
    /// 当天是否签到（基于当前时间）
    /// </summary>
    private bool IsDailySign()
    {
        if (userData == null)
        {
            Debug.LogError("StreakManager: UserData 未设置");
            return false;
        }
      
        long today = UIUtilities.GetCurrentDayIndex();
        return IsDaySigned(today);
    }
    
    /// <summary>
    /// 执行每日签到（基于当前时间）
    /// </summary>
    public bool ClaimDailyReward()
    {
        if (userData == null)
        {
            Debug.LogError("StreakManager: UserData 未设置");
            return false;
        }

        var signData = userData._signSaveData;
        long today = UIUtilities.GetCurrentDayIndex();
        DateTime todayDate = UIUtilities.DayIndexToDateTime(today);
        long monthKey = UIUtilities.GetMonthKey(todayDate.Year, todayDate.Month);

        // 1. 检查今日是否已签到
        if (IsDaySigned(today))
        {
            Debug.Log("今日已签到，不可重复领取");
            return false;
        }

        // 2. 检查昨天是否签到（断签检测）
        long yesterday = today - 1;
        bool isConsecutive = IsDaySigned(yesterday);
        IswinStreakBreak = false;

        // 3. 更新连胜
        if (isConsecutive)
        {
            signData.AddCurrentStreak();
        }
        else
        {
            // 昨天未签到，断签（或首次签到），重置为1
            signData.currentStreak = 1;
            signData.curAwardid = 1;
            signData.firstWinStreakDay = today;
        }
        
        // 5. 记录首次签到日期
        if (today>signData.lastSignDay)
        {
            signData.lastSignDay = today;
        }
      
        signData.totalSignDays++;

        // 4. 更新该月签到日期列表
        if (!signData.signMonthDatas.ContainsKey(monthKey))
        {
            signData.signMonthDatas[monthKey] = new StreakData();
        }
        var monthData = signData.signMonthDatas[monthKey];
        if (!monthData.signedDays.Contains(todayDate.Day))
        {
            monthData.signedDays.Add(todayDate.Day);
            monthData.signedDays.Sort();
        }

        // 5. 记录首次签到日期
        if (signData.firstSignDay == 0||today<signData.firstSignDay)
        {
            signData.firstSignDay = today;
        }

        // 6. 保存用户数据
        userData.SaveData();

        // 7. 触发事件
        OnSignSuccess?.Invoke(signData.currentStreak);

        Debug.Log($"签到成功！当前连胜 {signData.currentStreak} 天，累计签到 {signData.totalSignDays} 天");
        return true;
    }

    /// <summary>
    /// 检查指定天序号是否已签到
    /// </summary>
    public bool IsDaySigned(long dayIndex)
    {
        if (userData == null) return false;

        var signData = userData._signSaveData;
        DateTime date = UIUtilities.DayIndexToDateTime(dayIndex);
        long monthKey = UIUtilities.GetMonthKey(date.Year, date.Month);

        if (!signData.signMonthDatas.TryGetValue(monthKey, out var monthData))
            return false;

        return monthData.signedDays.Contains(date.Day);
    }

    /// <summary>
    /// 获取当前连胜天数
    /// </summary>
    public int GetCurrentStreak()
    {
        return userData?._signSaveData.currentStreak ?? 0;
    }
    
    /// <summary>
    /// 获取奖励id
    /// </summary>
    public int GetCurAwardid()
    {
        return userData?._signSaveData.curAwardid ?? 0;
    }
    
    /// <summary>
    /// 获取当前连胜天数
    /// </summary>
    public bool CheckWinStreakBreak()
    {
        
        // 加载数据后立即检查断签，确保连胜数据准确
        CheckAndResetStreakIfBroken();
        
        return userData?._signSaveData.currentStreak<=0||IswinStreakBreak;
    }
    
    /// <summary>
    /// 获取当前连胜奖励id
    /// </summary>
    public int GetcurAwardid()
    {
        return userData?._signSaveData.curAwardid ?? 0;
    }

    /// <summary>
    /// 获取累计签到总天数
    /// </summary>
    public int GetTotalSignDays()
    {
        return userData?._signSaveData.totalSignDays ?? 0;
    }

    /// <summary>
    /// 获取首次签到天序号
    /// </summary>
    public long GetFirstSignDay()
    {
        return userData?._signSaveData.firstWinStreakDay ?? 0;
    }

    /// <summary>
    /// 获取最近签到天序号
    /// </summary>
    public long GetLastSignDay()
    {
        return userData?._signSaveData.lastSignDay ?? 0;
    }

    /// <summary>
    /// 获取指定年月的签到日期列表（日号列表）
    /// </summary>
    public List<int> GetSignedDaysInMonth(int year, int month)
    {
        if (userData == null) return new List<int>();

        long monthKey = UIUtilities.GetMonthKey(year, month);
        var signData = userData._signSaveData;
        if (signData.signMonthDatas.TryGetValue(monthKey, out var monthData))
        {
            return monthData.signedDays;
        }
        return new List<int>();
    }

    /// <summary>
    /// 获取从首次签到到当前月份的所有月份列表（用于日历滑动范围）
    /// </summary>
    public List<(int year, int month)> GetAllMonthsWithData()
    {
        var result = new List<(int, int)>();
        if (userData == null) return result;

        long first = userData._signSaveData.firstSignDay;
        if (first == 0) return result;

        DateTime start = UIUtilities.DayIndexToDateTime(first);
        DateTime end = DateTime.UtcNow;
        DateTime current = new DateTime(start.Year, start.Month, 1);
        DateTime endMonth = new DateTime(end.Year, end.Month, 1);

        while (current <= endMonth)
        {
            result.Add((current.Year, current.Month));
            current = current.AddMonths(1);
        }
        return result;
    }

    /// <summary>
    /// 重置签到数据（仅用于测试）
    /// </summary>
    public void ResetSignData()
    {
        if (userData == null) return;

        userData._signSaveData = new SignSaveData();
        userData.SaveData();
        Debug.Log("签到数据已重置");
    }

}