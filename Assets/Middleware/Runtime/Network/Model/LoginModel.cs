
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;


[Serializable]
public class LoginRequest
{
    public string factory;
    public string openId;
    public string deviceId;
    public string platform;
    public string push_token;
    public string idfa;
    public string version;
    public string language;
}

[Serializable]
public class LoginResponse
{
    public string uid;
    public string token;
    public int expiresIn; // 过期时间，单位秒
    public int offline_Seconds;
    public bool is_version_upgraded;
    public Dictionary<string, Object> abtest; // A/B测试参数
}

[Serializable]
public class LogoutRequest
{
    public string gameData;
}


[Serializable]
public class GameDataDto
{
    // 1. 主存档
    // PHP端: $request->input('data')
    // C#端: 这里的 PropertyName 必须写 "data"
    [JsonProperty("data")] 
    public string UserData { get; set; }
    // 2. 额外数据
    // PHP端: $request->input('extra_data')
    // C#端: 这里的 PropertyName 必须写 "extra_data"
    [JsonProperty("extra_data")] 
    public ExtraDataDto ExtraData { get; set; }
    // 模型自带的时间字段
    [JsonProperty("updated_at")]
    public string UpdatedAt { get; set; }
    // 模型自带的时间字段
    [JsonProperty("created_at")]
    public string CreatedAt { get; set; }
}
/// <summary>
/// 额外数据的内部结构
/// </summary>
[Serializable]
public class ExtraDataDto
{
    // 对应你的 fishUserSave 对象
    [JsonProperty("fishUserSave")]
    public string FishUserSave { get; set; }

    // 对应你的 dynamicHard 对象
    [JsonProperty("butterfly")]
    public string Butterfly { get; set; }
    
    [JsonProperty("overallRank")]
    public string OverallRank { get; set; }
    
    [JsonProperty("achieveSaveDatas")]
    public string AchieveSaveDatas { get; set; }
}

[Serializable]
public class UserProfile
{
    // ================= 基础属性 =================
    public int uid;
    public string nickname;
    public string avatar;
    
    // ================= 游戏进度与数据 =================
    public int zen_count;
    public int highest_zen_score;    // 历史最高分
    public int max_win_streak;       // 最大连胜
    public int max_chess_combo;      // 最大连击
    
    // ================= 社交与主页展示 =================
    public int likes_count;          // 获赞数
    public string join_date_text;    // 加入时间文本 (如"2026年8月开始")
    public HofAwardsDto hof_awards;  // 名人堂奖牌数据
    
    // ================= 榜单与结算 =================
    public string highest_zen_level; // 历史最高段位
    public string zen_level;
    public bool has_settlement;
    public string settlement_type;
    public string old_zen_level;
    public int old_rank;
}