using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LeaderboardEntry
{
    public int user_id;
    public int rank;
    public int avatar;
    public string nickname;
    public int score;
    public string leaderboard_name;
    public string grouping;
    
}
public class LeaderboardRequest
{
    public string boardId;
}

public class LeaderboardResponse
{
    public LeaderboardEntry my;
    public string updated_at;
    public int remaining_seconds; // 🌟 接收后端传来的秒数
    public List<LeaderboardEntry> top;
    public List<LeaderboardEntry> middle;
    public List<LeaderboardEntry> bottom;
}

// 👇=== 领奖接口的返回数据结构 ===👇
public class ClaimZenRewardResponse
{
    public string status;           // 状态: "success" 或 "error"
    public string message;          // 错误信息 (如果有)
    public string new_level;        // 结算后的新段位 (如 "ZenState02")
    public string settlement_type;  // 结算类型 ("up", "down", "keep")
}