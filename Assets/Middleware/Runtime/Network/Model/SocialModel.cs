using System;
using System.Collections;
using System.Collections.Generic;

[Serializable]
public class PublicProfileDto
{
    public string user_id;
    public string nickname;
    public string avatar;
    public string avatar_frame;
    public string zen_level;
    public int zen_count;
    public int overallZenScore;

    // 格式化好的加入时间 (例如: "2026年5月开始")
    public string join_date_text;

    public int likes_count;
    public int max_win_streak;
    public int max_chess_combo;
    public int four_char_count;
    public int other_char_count;
    public string chess_stage;
    public string highest_zen_level;

    // 蝶园漫游依赖数据
    public int current_garden_id;

    // 如果 PHP 返回的是 JSON 数组，这里用 List<int> 或 List<string> 接收
    public List<string> collected_butterflies;

    // 名人堂奖牌统计
    public HofAwardsDto hof_awards;

    // 是否已经点赞过 (用于初始化点赞按钮的高亮状态)
    public bool has_liked;
}

[Serializable]
public class HofAwardsDto
{
    public int gold; // 冠军次数
    public int silver; // 亚军次数
    public int bronze; // 季军次数
}

[Serializable]
public class LikeResponseDto
{
    // 返回 "like" 或 "unlike"，方便前端切换大拇指 UI
    public string action;
    public int likes_count;
}