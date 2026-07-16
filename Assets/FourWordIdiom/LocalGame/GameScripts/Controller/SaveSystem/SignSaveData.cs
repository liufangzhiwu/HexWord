using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SignSaveData
{
    public long firstSignDay = 0;                 // 首次签到日的 天序号
    public long firstWinStreakDay = 0;                 // 首次连胜签到日的 天序号
    public long lastSignDay = 0;                  // 最近一次签到的 天序号（新增）
    public int currentStreak = 0;                 // 当前连续签到天数（新增缓存）
    public int curAwardid = 0;                 // 当前连胜奖励id
    public int totalSignDays = 0;                 // 累计签到总次数
    public int historyWinDayTimes = 0;                 // 历史最长连胜天数
    
    /// <summary>
    /// 当前年月对应的签到数据
    /// </summary>
    public Dictionary<long, StreakData> signMonthDatas = new Dictionary<long, StreakData>();
    /// <summary>
    /// 连胜奖励是否已经获得
    /// </summary>
    public Dictionary<int, bool> winAwardClaims = new Dictionary <int, bool>();
    
    
    public SignSaveData Clone()
    {
        var clone = new SignSaveData
        {
            firstSignDay = this.firstSignDay,
            firstWinStreakDay = this.firstWinStreakDay,
            lastSignDay = this.lastSignDay,
            currentStreak = this.currentStreak,
            curAwardid = this.curAwardid,
            totalSignDays = this.totalSignDays,
            winAwardClaims = this.winAwardClaims
        };
        // 深拷贝字典
        foreach (var kv in this.signMonthDatas)
        {
            var monthData = new StreakData();
            monthData.signedDays = new List<int>(kv.Value.signedDays); // 复制列表
            clone.signMonthDatas[kv.Key] = monthData;
        }
        return clone;
    }

    public void AddCurrentStreak()
    {
        this.currentStreak++;
        this.curAwardid++;
        
        if (this.curAwardid > 30)
        {
            this.curAwardid = 1;
            winAwardClaims.Clear();
        }
    }


    public void AddWinClaim()
    {
        if (!winAwardClaims.Keys.Contains(curAwardid))
        {
            winAwardClaims[curAwardid] = true;
        }
    }
    
    
    /// <summary>
    /// 检查连胜奖励是否获得10,20,30
    /// </summary>
    /// <returns></returns>
    public bool CheckWinClaim()
    {
        if(curAwardid<10) return false;

        if (curAwardid % 10 != 0)
        {
            int awardindex = curAwardid - curAwardid % 10;
            return winAwardClaims.Keys.Contains(awardindex);
        }
        
        
        return winAwardClaims.Keys.Contains(curAwardid);
    }
    
    
    // 辅助函数：判断某天是否已签到
    bool IsSigned(DateTime date)
    {
        // 月份 Key 的生成规则需与存储时一致，此处假设为 "年份 * 100 + 月份"
        long monthKey = date.Year * 100 + date.Month;
        if (signMonthDatas.TryGetValue(monthKey, out StreakData monthData))
        {
            return monthData.signedDays.Contains(date.Day);
        }
        return false;
    }
    
    /// <summary>
    /// 指定当前日期往前daycount天视为已经签到
    /// </summary>
    /// <param name="currentDate">当前日期（通常为 DateTime.Now）</param>
    /// <returns>连续签到天数</returns>
    public void AddDodaySigneDays(int daycount)
    {
        // 确定起始检查日期：若今天已签到，则从今天开始；否则从昨天开始
        DateTime currentDate = DateTime.Today;
        DateTime checkDate = currentDate.AddDays(-1);
        long yestodayCheckDataTicks=UIUtilities.GetSomeDayIndex(checkDate);
        if (lastSignDay < yestodayCheckDataTicks)
        {
            lastSignDay = yestodayCheckDataTicks;
        }
        
        int index = daycount;
        // 向前逐日回溯，遇到未签到日则停止
        while (index>0)
        {
            index--;
          
            // 未签到时进行签到
            if (!IsSigned(checkDate))
            {
                long monthKey = UIUtilities.GetMonthKey(checkDate.Year, checkDate.Month);
                
                if (!signMonthDatas.ContainsKey(monthKey))
                {
                    signMonthDatas[monthKey] = new StreakData();
                }
                var monthData = signMonthDatas[monthKey];
                if (!monthData.signedDays.Contains(checkDate.Day))
                {
                    monthData.signedDays.Add(checkDate.Day);
                    monthData.signedDays.Sort();
                }
            }
            
            checkDate = checkDate.AddDays(-1);
        }
    }
    
    /// <summary>
    /// 获取截至指定日期的连续签到天数（实时计算）
    /// </summary>
    /// <param name="currentDate">当前日期（通常为 DateTime.Now）</param>
    /// <returns>连续签到天数</returns>
    public int GetCurrentStreak(DateTime currentDate)
    {
        // 确定起始检查日期：若今天已签到，则从今天开始；否则从昨天开始
        DateTime checkDate = currentDate;
        if (!IsSigned(currentDate))
        {
            checkDate = currentDate.AddDays(-1);
        }

        int streak = 0;
        // 向前逐日回溯，遇到未签到日则停止
        while (IsSigned(checkDate))
        {
            streak++;
            checkDate = checkDate.AddDays(-1);
        }
        
        checkDate = checkDate.AddDays(1);

        firstWinStreakDay = UIUtilities.GetSomeDayIndex(checkDate);

        if (firstSignDay > firstWinStreakDay||firstSignDay==0)
        {
            firstSignDay=firstWinStreakDay;
        }
        

        if (streak > historyWinDayTimes)
        {
            historyWinDayTimes = streak;
        }

        if (streak>0&&streak%30==0)
        {
            curAwardid =30;
        }
        else
        {
            curAwardid =streak%30;
        }

        if (curAwardid <10&&winAwardClaims.Count>0)
        {
            winAwardClaims.Clear();
        }
       
        
        return streak;
    }
    
   
}

// ============================================================
// 1. 数据模型（可序列化）
// ============================================================
[Serializable]
public class StreakData
{
    public List<int> signedDays = new List<int>(); // 存储该月已签到的日号（1-31）
   
}


