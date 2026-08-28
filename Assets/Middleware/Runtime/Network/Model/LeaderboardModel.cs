using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LeaderboardEntry
{
    public int user_id;
    public bool is_joined;
    public int rank;
    public int avatar;
    public string avatar_frame;
    public string nickname;
    public int score;
    public string leaderboard_name;
    public string period_date;
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
    public int next_remaining_seconds; // 下一期的时间
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

// 对应的响应数据结构
public class ZenSettlementResponse
{
    public bool has_settlement;
    public string current_level;
    public string old_level;
    public string settlement_type; // up, down, keep
    public int old_rank;
}
// 加入响应
public class JoinZenRankResponse
{
    public string status;           
    public string level;            // 当前段位
    public string grouping;         // 分组（我们修改后为空）
    public int base_zen_count;      // 服务端锁定的底分
}

// ==========================================
// 🌟 总榜、月榜、名人堂 通用的响应数据结构
// ==========================================
public class OverallRankResponse
{
    public LeaderboardEntry my;            // 玩家自己的排名数据
    public List<LeaderboardEntry> list;    // 榜单主体列表（前100名等）
    public int remaining_seconds;
}

// ==========================================
// 🌟 月榜领奖响应数据结构
// ==========================================
public class ClaimMonthlyRewardResponse
{
    public string status;           // 状态: "success" 或 "error"
    public string message;          // 提示信息（例如：“奖励已发放”、“不在发奖期”等）
    // 如果后端直接下发掉落物列表，可以在这里拓展一个 List<RewardItem> rewards
}
// 🌟 月榜结算响应（用于给玩家展示自己和前5名的数据）
public class MonthlySettlementResponse
{
    public bool has_settlement;          // 是否弹结算（独立于有没有奖励）
    public bool has_reward;              // 是否进前3有奖
    public string period;        // 期数 (例如 "2026-07-30 14")
    public int my_rank;                  // 0 = 未上榜
    public int my_score;
    public int my_avatar;                // 与 LeaderboardEntry.avatar 同为 int
    public string my_nickname;
    public List<LeaderboardEntry> list; // 前6名快照
}