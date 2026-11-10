using System.Collections.Generic;

/// <summary>
/// 游戏核心配置（修改版，避免被识别为重复应用）
/// </summary>
public static class AppGameSettings
{
    // ===== 经济系统配置 =====
    public static int StartingGold { get; } = 0;
    public static int FirstLevel { get; } = 1;
    public static string SystemLanguage { get; } = "ChineseSimplified";  

    // ===== 关卡奖励 =====
    public static int LevelCompleteBonus { get; } = 50;  

    // ===== 道具商店 =====
    public static class ShopItems 
    {
        public static int SingleHintCost { get; } = 80;     
        public static int SingleHintCount { get; } = 1;   
        
        public static int WordHintCount { get; } = 1;   
        public static int WordHintCost { get; } = 150;       
       
        
        public static int ButterflyCost { get; } = 50;   
        public static int StartingButterflies { get; } = 0; 
        
        public static int AutoCompleteCost { get; } = 150;   
        public static int ReStartCost { get; } = 60;   
        
        public static int EnergyCost { get; } = 150;
    }

    // ===== 游戏机制开关 =====
    public static bool EnableComboButterflies { get; } = false;
    public static int MaxButterfliesPerLevel { get; } = 2;
    
    //每日任务中无限使用蝴蝶道具时长（分钟）
    public static int TaskButterflyUseTime { get; } = 60;     
    //竞速目标词语数量
    public static int FishTargetWordCount { get; set; } = 100;   

    // ===== 关卡循环设置 =====
    public static int LoopLevelStart { get; } = 180;     

    // ===== 功能解锁关卡 =====
    public static class UnlockRequirements 
    {
        public static int TimeLimitMode { get; } = 1;   
        public static int SignInRewards { get; } = 11;   
        public static int DailyMissions { get; } = 16;   
        //30关卡进入结算界面时开启(鲤鱼跃龙门活动)
        public static int FishOpenLevel { get; } = 21;
        //10关卡进入结算界面时开启（命名界面）
        public static int HeadOpenLevel { get; } = 1;
        public static int ZenOpenLevel { get; } = 11; // 11关卡开启禅模式

        // ===== 同步自 WordTuanjie 1.9.8：仅补充结构，数值沿用 HexWord 现有节奏 =====
        // HexWord 当前未对蝴蝶花园做关卡限制，暂取 1（等价于不限制），如需限制请运营给值
        public static int ButterflyGardenOpenLevel { get; } = 1;
        // HexWord 当前未对主题界面做关卡限制，暂取 1（等价于不限制），如需限制请运营给值
        public static int ThemeOpenLevel { get; } = 1;
    }

    // ===== 功能显示关卡（同步自 WordTuanjie 1.9.8，数值对齐 HexWord 现有解锁节奏）=====
    public static class ShowRequirements 
    {
        public static int TimeLimitShow { get; } = 1;
        public static int SignInRewards { get; } = 11;
        // 提前 5 关显示入口（锁定态显示"第16关"），与 WT 的引导节奏一致；如需取消提前显示，改成与 UnlockRequirements 相同即可
        public static int DailyMissionsShow { get; } = 11;
        // 鲤鱼跃龙门活动显示门槛（解锁 21 关，提前 5 关露出锁定态）
        public static int FishShowLevel { get; } = 16;
        public static int HeadOpenLevel { get; } = 1;
        public static int ZenShowLevel { get; } = 11; // 11关卡显示禅模式
        // HexWord 当前未对蝴蝶花园做显示限制，暂取 1
        public static int ButterflyGardenShowLevel { get; } = 1;
        // HexWord 当前未对主题界面做显示限制，暂取 1
        public static int ThemeShowLevel { get; } = 1;
    }


    // ===== 任务系统 =====
    //每日任务中无限使用蝴蝶道具时长（分钟）
    public static int UnlimitedButterflyDuration { get; } = 60;  // 分钟
    /// <summary>
    /// 是否开启保留剩余任务进度
    /// </summary>
    public static bool SaveMissionProgress { get; } = false;    
    /// <summary>
    /// 是否开启保留在线时长剩余任务进度
    /// </summary>
    public static bool SaveOnlineTimeProgress { get; } = true;   

    // ===== 结算界面关卡进度数值 =====
    // public static List<int> ProgressMilestones { get; } = new List<int>() 
    // {
    //     5, 5, 10, 10, 10, 10, 20
    // };

    // ===== 设备白名单 =====
    public static HashSet<string> AuthorizedDevices { get; } = new HashSet<string>() 
    {
        // 示例设备ID（实际使用时建议加密存储）
        "",
        // ...（其余ID保持不变）
    };
}