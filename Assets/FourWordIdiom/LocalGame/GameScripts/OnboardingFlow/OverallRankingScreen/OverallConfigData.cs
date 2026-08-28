using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==========================================
// 数据实体类定义
// ==========================================
[System.Serializable]
public class SettlementData
{
    public string period;
    public int myRank;                       // <=0 表示未上榜
    public int myScore;
    public int myAvatar;
    public List<LeaderboardEntry> topList;   // 旧月名次快照（前 N）
    public LeaderboardEntry myEntry;         // 自己的快照（用于"上榜但 list 没把自己带回来"时补位）
    public Dictionary<int, int> rewards;  
    public bool HasReward => rewards != null && rewards.Count > 0;
    public bool SelfInTop6 => myRank > 0 && myRank <= OverallSettlementScreen.MAX_ROWS;
}

/// <summary>
/// 境界禅意配置 (对应 image_259f03.png)
/// </summary>
public class RealmLevelData
{
    public int Level;          // 等级
    public string NameKey;     // 境界名称多语言Key (如 ZL01)
    public string FeelKey;     // 境界禅意多语言Key (如 AC01)
    public int UpScore;        // 升下一级需要的禅意分
}

/// <summary>
/// 排行榜全局控制配置 (对应 image_259ee3.png 的 RankControl 字段)
/// </summary>
public class RankControlConfig
{
    public bool IsOpen;                // 排行榜入口控制 (1开0关)
    public int TotalRankUnlockScore;   // 总榜的禅意值解锁条件
    public int MonthlyRankUnlockScore; // 月榜的禅意值解锁条件
}

/// <summary>
/// 月榜宝箱奖励数据
/// </summary>
public class MonthlyRewardItem
{
    public int ItemId;
    public int ItemType;
    public int ItemCount;
}

public class MonthlyRewardConfig
{
    public int Rank; // 排名 (1, 2, 3)
    public List<MonthlyRewardItem> Rewards = new List<MonthlyRewardItem>();
}