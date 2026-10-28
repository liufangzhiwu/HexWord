using System;
using System.Collections.Generic;
using System.Globalization;
using Middleware;
using UnityEngine;
using Random = UnityEngine.Random;

public static class ItemConstants
{
    public const int Audo = 104;   // 重置道具
    public const int Tip = 102;     // 提示道具
    public const int Fly = 103;     // 飞行道具
}

/// <summary>
/// resource_change.resource_id 规范编码（来源：飞书《通用数数埋点方案 / #事件数据 / resource_change》）
/// 规范内取值 0~13；14 起为规范未定义、本工程在用，需回填规范表后再启用。
/// </summary>
public static class ResId
{
    public const string Gold = "0";        // 金币
    public const string Fly = "1";         // 蝴蝶
    public const string Tip = "2";         // 提示道具
    public const string Reset = "3";       // 重置道具
    public const string Double5Min = "4";  // 5min 双倍
    public const string Double15Min = "5"; // 15min 双倍
    public const string NoAd = "6";        // 去广告
    public const string NoAd7Day = "7";    // 7 天去广告
    public const string Rocket = "8";      // 放大镜/火箭道具
    public const string MagicWand = "9";   // 魔法棒（六边形玩法）
    public const string Pupa = "11";       // 蝶蛹
    public const string GoldLeaf = "12";   // 金箔
    public const string Revive = "13";     // 复活道具

    // 规范未定义，待回填（回填前先按临时编码上报，便于先观察）
    public const string Energy = "14";     // 体力
    public const string SpecialHead = "15";// 特殊头像

    private static readonly Dictionary<string, string> Map = new Dictionary<string, string>
    {
        { "金币", Gold },
        { "蝴蝶道具", Fly },
        { "提示道具", Tip },
        { "重置道具", Reset },
        { "限时奖励5分钟翻倍", Double5Min },
        { "限时奖励15分钟翻倍", Double15Min },
        { "火箭道具", Rocket },
        { "魔法棒", MagicWand },
        { "蝶蛹", Pupa },
        { "蚕蛹", Pupa },            // 与蝶蛹为同一资源，统一归到 11
        { "限时奖励蚕蛹", Pupa },
        { "签到奖励蚕蛹", Pupa },
        { "金箔", GoldLeaf },
        { "复活道具", Revive },
        { "体力", Energy },
        { "特殊头像", SpecialHead },
    };

    /// <summary>把资源中文名归一成规范编码；未登记的原样返回并打日志，避免静默丢数。</summary>
    public static string Normalize(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        if (Map.TryGetValue(name, out var code)) return code;
        Debug.LogWarning($"[Analytics] resource_id 未登记，原样上报：{name}");
        return name;
    }
}

/// <summary>
/// resource_change.change_reason 规范枚举（来源同上）。规范外原因统一映射到最接近的规范值，
/// 无法映射的走 Other 前缀保留原文，便于后续回填规范。
/// </summary>
public static class ResReason
{
    public const string InLevel = "关卡内使用";
    public const string BuyItem = "购买道具";
    public const string ShopBuy = "商店购买";
    public const string SignFree = "签到免费获得";
    public const string SignAd = "签到广告获得";
    public const string GoldAdPop = "金币广告弹窗获得";
    public const string LevelSettle = "结算获得";
    public const string LimitReward = "限时奖励获得";
    public const string RaceReward = "竞速获得";
    public const string TaskReward = "任务获得";
    public const string RankReward = "排行榜获得";
    public const string GoldWord = "金币词获得";
    public const string GoldLeafGet = "金箔获得";
    public const string SkinDraw = "抽取皮肤";
    public const string AdTrackingReward = "广告追踪激励"; // 开启广告追踪（关联应用）的一次性金币奖励

    /// <summary>规范外原因前缀：&lt;Other&gt;原文，回填规范后即可去掉。</summary>
    public const string OtherPrefix = "其他_";

    /// <summary>把自由文本原因收敛到规范枚举。</summary>
    public static string Normalize(string reason)
    {
        if (string.IsNullOrEmpty(reason)) return OtherPrefix + "空";

        // 前缀/关键字匹配，覆盖 "商店购买XXX"、"限时宝箱商店购买XXX" 这类拼接串
        if (reason.Contains("商店购买")) return ShopBuy;
        if (reason.Contains("购买道具")) return BuyItem;
        if (reason.Contains("关卡内使用") || reason.Contains("退出关卡") || reason.Contains("进程中断")) return InLevel;
        if (reason.Contains("排行榜") || reason.Contains("月榜") || reason.Contains("赛季结算")) return RankReward;
        if (reason.Contains("签到") && reason.Contains("广告")) return SignAd;
        if (reason.Contains("签到")) return SignFree;
        if (reason.Contains("限时奖励") || reason.Contains("限时宝箱")) return LimitReward;
        if (reason.Contains("竞速")) return RaceReward;
        if (reason.Contains("任务")) return TaskReward;
        if (reason.Contains("金币词")) return GoldWord;
        if (reason.Contains("金箔")) return GoldLeafGet;
        if (reason.Contains("皮肤")) return SkinDraw;
        if (reason.Contains("结算")) return LevelSettle;
        // 必须排在通用"广告"之前，否则会被收敛成"金币广告弹窗获得"
        if (reason.Contains("广告追踪")) return AdTrackingReward;
        if (reason.Contains("广告")) return GoldAdPop;

        Debug.LogWarning($"[Analytics] change_reason 未登记，按规范外上报：{reason}");
        return OtherPrefix + reason;
    }
}


public partial class AnalyticMgr
{
    #region 进度相关
    private static DateTime? _startTime;// 使用 记录开始时间
    private static DateTime firstLoginTime = DateTime.MinValue;
    
    public static void GameStart()
    {
        SetLoginProperties();

        if (SystemManager.Instance != null)
        {
            if (SystemManager.Instance.PanelIsShowing(PanelType.HexGamePlayArea))
            {
                // 记录关卡开始时间
                GameDataManager.Instance.UserData.curStageStartTime = DateTime.Now.ToString();
            }
        }
    }

    public static void GameEnd()
    {
        if(GameDataManager.Instance.UserData == null||Game.self == null) return;
        SetCommonProperties();
        SetLogoutProperties();
    }

    /// <summary>
    /// 登录时用户数据。
    /// 注意：这是 total_login / active_day / first_login_time 的【唯一】写入口，
    /// 其它地方（含 SetCommonProperties）只读，避免累计值被埋点调用次数刷高。
    /// </summary>
    private static void SetLoginProperties()
    {
        // 记录本次登录开始时间
        var now = DateTime.Now;
        _startTime = now;
        var userData = GameDataManager.Instance.UserData;
        var today = now.Date;

        // 处理首次登录（仅在首次时设置，避免 OnAnalyticsSdkInit 中重复覆盖）
        if (string.IsNullOrEmpty(userData.firstLoginTime))
        {
            userData.firstLoginTime = now.ToString("yyyy-MM-dd HH:mm:ss");
            userData.activeDayCnt = 1;
            userData.lastLoginDay = today.ToString("yyyy-MM-dd");
            userData.totallogin = 1;
        }
        else
        {
            // 非首次登录：更新活跃天数（基于上次登录日期）
            if (DateTime.TryParse(userData.lastLoginDay, out var lastLoginDate))
            {
                if (lastLoginDate.Date != today)
                {
                    userData.activeDayCnt++;
                }
            }
            else
            {
                // 解析失败，保守增加
                userData.activeDayCnt++;
            }
            
            if(userData.activeDayCnt<=0) userData.activeDayCnt = 1;

            userData.totallogin++;
            userData.lastLoginDay = today.ToString("yyyy-MM-dd");
        }
      
        firstLoginTime = DateTime.Parse(userData.firstLoginTime);

        // 计算生命周期天数（基于首次登录时间）
        int lifeDays = (today - firstLoginTime.Date).Days+ 1; // +1 表示第1天
        Debug.Log("当前时间："+today.ToString("yyyy-MM-dd HH:mm:ss")+"周期天数 life:"+lifeDays);

        // 留存回传只在登录时机上报一次（原先放在 SetCommonProperties 里会随调用次数重复上报）
        if (lifeDays == 2)
        {
#if UNITY_HUAWEI || UNITY_OPENHARMONY
            long nowTimeMilliseconds = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            Game.self.Attributes?.ReportRetention(nowTimeMilliseconds);
#endif
        }
        
        var properties = new Dictionary<string, object>
        {
            //时间类
            { "last_login_time", now.ToString("yyyy-MM-dd HH:mm:ss")},
            { "first_pay_time", GameDataManager.Instance.UserData.firstPayTime},
            { "last_pay_time", GameDataManager.Instance.UserData.lastPayTime},
            //累积类
            { "total_revenue", GameDataManager.Instance.UserData.TotalRevenue },
            { "total_login", GameDataManager.Instance.UserData.totallogin},
            { "total_pay_times", GameDataManager.Instance.UserData.TotalPayTimes },
            { "total_ad_times", GameDataManager.Instance.UserData.totalSeeAds},
            { "total_item_cost", GameDataManager.Instance.UserData.GetTotalToolCost()},
            { "active_day", userData.activeDayCnt},
            { "life_day", lifeDays},
        };
        Game.self?.Analytics.SetUserProperty(properties, Define.DataTarget.Think);

        // user_setOnce：首登时间与首装版本只写一次
        var onceProperties = new Dictionary<string, object>
        {
            { "first_login_time", firstLoginTime.ToString("yyyy-MM-dd HH:mm:ss") },
            { "first_version", string.IsNullOrEmpty(userData.first_version) ? Application.version : userData.first_version },
        };
        Game.self?.Analytics.SetUserPropertyOnce(onceProperties, Define.DataTarget.Think);

        // 榜单类用户属性（规范 #用户数据 current_weekly_rank / current_total_rank 为 user_set）
        RefreshRankUserProperties();

        // 建立 #account_id，避免全量按设备ID统计
        var uid = userData.UserId;
        if (!string.IsNullOrEmpty(uid))
        {
            Game.self?.Analytics.Login(uid);
        }
        else
        {
            Debug.LogWarning("[Analytics] 登录时 UserId 为空，#account_id 未设置");
        }

        SetCommonProperties();
        Game.self?.Analytics.LogEvent("ta_app_start", Define.DataTarget.Think);

        UnityTimer.Delay(3, () =>
        {
            if (!string.IsNullOrEmpty(GameDataManager.Instance.UserData.adTrackingEnabled))
            {
                PushMessageReceiver._instance.OnOAIDClosedSignal(GameDataManager.Instance.UserData.adTrackingEnabled);
            }
            else
            {
#if UNITY_EDITOR 
            
                string enRange=Random.Range(0, 2).ToString();
                PushMessageReceiver._instance.OnOAIDClosedSignal(enRange);
#endif
            }
        });
        Game.self?.Attributes?.ReportConversion("2");
    }

    /// <summary>
    /// 登出时用户数据
    /// </summary>
    private static void SetLogoutProperties()
    {
        var userData = GameDataManager.Instance.UserData;
        if (userData == null) return;
        int levelId = GameDataManager.Instance.UserData.CurrentChessStage;
        
        switch ((LevelType)GameDataManager.Instance.UserData.levelMode)
        {
            case LevelType.BlockWord:
                levelId = GameDataManager.Instance.UserData.CurrentHexStage;
                break;
            case LevelType.ChessWord:
                levelId = GameDataManager.Instance.UserData.CurrentChessStage;
                break;
            case LevelType.HexWord:
                levelId = GameDataManager.Instance.UserData.CurrentHexStage;
                break;
        }
        
        int tipCount = userData.toolInfo.TryGetValue(ItemConstants.Tip, out var tip) ? tip.count : 0;
        int resetCount = userData.toolInfo.TryGetValue(ItemConstants.Audo, out var reset) ? reset.count : 0;
        int flyCount = userData.toolInfo.TryGetValue(ItemConstants.Fly, out var fly) ? fly.count : 0;

        bool lifeAdFree = false;
        bool limitedAdFree = false;
        if (ShopManager.shopManager != null)
        {
            lifeAdFree = ShopManager.shopManager.IsRemoveAdsOwned();
            limitedAdFree = ShopManager.shopManager.IsRemoveAdsActive();
        }

        var properties = new Dictionary<string, object>
        {
            { "current_coin", userData.Gold },
            { "current_tipItem", tipCount },
            { "current_resetItem", resetCount },
            { "current_flyItem", flyCount },
            { "current_level", levelId },
            { "current_pupa", GameDataManager.Instance.ButterflyData.pupa},
            { "current_goldFoil", userData.GoldLeaf},
            // 去广告状态：1 有 0 无（规范 #用户数据）
            { "Life_AdFree", lifeAdFree ? 1 : 0 },
            { "LimitedTime_AdFree", limitedAdFree ? 1 : 0 },
        };
       
        Game.self.Analytics?.SetUserProperty(properties, Define.DataTarget.Think);
        
        //处理异常，确保_startTime有值
        if (!_startTime.HasValue)
        {
            _startTime = DateTime.Now;
        }
        var durationSeconds = (DateTime.Now - _startTime.Value).TotalSeconds;
        durationSeconds = Math.Max(0, durationSeconds);
        
        // #duration 规范为数值，直接传 double，避免被锁成字符串类型
        var outproperties = new Dictionary<string, object>(){{"#duration", Math.Round(durationSeconds, 2)}};
        Game.self.Analytics?.LogEvent("ta_app_end",outproperties, Define.DataTarget.Think);
    }
    
    /// <summary>
    /// 刷新公共事件属性。
    /// 【只读】：这里只计算、不修改 UserData，累计类字段（total_login/active_day 等）只在 SetLoginProperties 里写。
    /// </summary>
    public static void SetCommonProperties()
    {
        var userData = GameDataManager.Instance?.UserData;
        if (userData == null) return;

        int levelId = userData.CurrentChessStage;
        
        switch ((LevelType)userData.levelMode)
        {
            case LevelType.BlockWord:
                levelId = userData.CurrentHexStage;
                break;
            case LevelType.ChessWord:
                levelId = userData.CurrentChessStage;
                break;
            case LevelType.HexWord:
                levelId = userData.CurrentHexStage;
                break;
        }

        int tipCount = userData.toolInfo.TryGetValue(ItemConstants.Tip, out var tip) ? tip.count : 0;
        int resetCount = userData.toolInfo.TryGetValue(ItemConstants.Audo, out var reset) ? reset.count : 0;
        int flyCount = userData.toolInfo.TryGetValue(ItemConstants.Fly, out var fly) ? fly.count : 0;

        // 生命周期天数：基于已记录的首登时间只读计算，首日 = 1
        int lifeDays = 1;
        var first = firstLoginTime;
        if (first == DateTime.MinValue && !string.IsNullOrEmpty(userData.firstLoginTime))
        {
            DateTime.TryParse(userData.firstLoginTime, out first);
        }
        if (first != DateTime.MinValue)
        {
            lifeDays = Math.Max(1, (DateTime.Now.Date - first.Date).Days + 1);
        }

        var properties = new Dictionary<string, object>
        {
            { "gold", userData.Gold },
            { "tipItem", tipCount },
            { "resetItem", resetCount },
            { "flyItem", flyCount },
            { "level_id", levelId },
            { "level_type", userData.GetLevelMode() },
            { "game_package", userData.ABName ?? string.Empty },
            { "active_day_event", userData.activeDayCnt },
            { "life_day_event", lifeDays },
        };

        Game.self.Analytics.SetCommonProperties(properties);
    }

    /// <summary>
    /// 刷新榜单类用户属性（规范 #用户数据：current_weekly_rank / current_total_rank，更新方式 user_set）
    /// </summary>
    public static void RefreshRankUserProperties()
    {
        if (Game.self == null || GameDataManager.Instance == null) return;

        var props = new Dictionary<string, object>
        {
            { "current_weekly_rank", ZenRankManager.Instance != null ? (object)ZenRankManager.Instance.CachedOldScore : 0 },
            { "current_total_rank", GameDataManager.Instance.OverallRank != null ? (object)GameDataManager.Instance.OverallRank.ZenScore : 0 },
        };
        Game.self.Analytics?.SetUserProperty(props, Define.DataTarget.Think);
    }

    /// <summary>
    /// 登出埋点：清账号ID与公共事件属性，避免换号后属性串到下一个账号
    /// </summary>
    public static void Logout()
    {
        Game.self?.Analytics?.Logout();
    }

    
    /// <summary>
    /// 安装事件的本地标记键。放 PlayerPrefs 而不是 UserData：
    /// 卸载会清除、覆盖安装会保留，语义与"安装"完全一致，且与游戏存档解耦。
    /// </summary>
    private const string InstallReportedKey = "ta_install_reported";

    /// <summary>
    /// 首次启动上报安装事件 ta_app_install（替代 SDK 的 AppInstall 自动埋点）。
    /// 原生 SDK 的判定只认 firstInstallTime == lastUpdateTime 的"纯净首装"，
    /// 且首次启动即无条件落盘 firstInstallTime，一旦没报出去该设备就永久丢失，
    /// 因此覆盖安装（内测→正式 / adb install -r / 渠道换包）的机器永远不会有该事件。
    /// 这里改成自建标记，口径可解释、可重试。
    /// </summary>
    public static void ReportInstallOnce()
    {
        try
        {
            if (PlayerPrefs.GetInt(InstallReportedKey, 0) == 1) return;

            Game.self?.Analytics?.LogEvent("ta_app_install", Define.DataTarget.Think);

            PlayerPrefs.SetInt(InstallReportedKey, 1);
            PlayerPrefs.Save();
            Debug.Log("[Analytics] 首次启动，已上报 ta_app_install");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Analytics] ta_app_install 上报异常：{e.Message}");
        }
    }

    public static void OnAnalyticsSdkInit(object sender, EventArgs e)
    {
        var uid = Game.self.GetUniqueId();
        SetCommonProperties();

        // 安装事件：SDK 初始化完成后立刻判定，只报一次
        ReportInstallOnce();
        var cacheUid = GameDataManager.Instance.UserData.UserId;
        if (string.IsNullOrEmpty(cacheUid) || cacheUid != uid)
        {
            GameDataManager.Instance.UserData.UserId = uid;
            //Game.self.Analytics.Login(GameDataManager.Instance.UserData.UserId);
        }

        if (!GameDataManager.Instance.UserData.Rigister)
        {
            Game.self.Analytics.LogEvent("ta_app_startFirst", Define.DataTarget.Think);
            Game.self.Analytics.LogEvent("register", Define.DataTarget.Think);
            GameDataManager.Instance.UserData.Rigister = true;
            GameDataManager.Instance.UserData.first_version = Application.version;
        }
        GameStart();
    }

    public static void Login()
    {
        SetCommonProperties();
        Game.self.Analytics.LogEvent("login", Define.DataTarget.Think);
    }
    
    public static void SetLoginUser(string tuid)
    {
        var uid = tuid;
        if (string.IsNullOrEmpty(uid))
        {
#if UNITY_EDITOR
            uid =Game.self.GetUniqueId();
#else
            Debug.LogError("uid is empty");
#endif
            //uid = Game.GetUniqueId();
        }
        
        var cacheUid = GameDataManager.Instance.UserData.UserId;

        Debug.Log("用户唯一id为："+uid+"当前用户id"+cacheUid);
        if (string.IsNullOrEmpty(cacheUid) || cacheUid != uid)
        {
            // 切号：先清掉上一个账号的 #account_id 与公共事件属性，再写入新账号
            if (!string.IsNullOrEmpty(cacheUid))
            {
                Game.self?.Analytics?.Logout();
            }

            Debug.Log("赋值中用户唯一id为："+uid);
            GameDataManager.Instance.UserData.UserId = uid;

            if (!string.IsNullOrEmpty(GameDataManager.Instance.UserData.UserId))
            {
                Game.self?.Analytics?.Login(GameDataManager.Instance.UserData.UserId);
            }
        }

        // 用户身份变更后再刷新公共属性，避免把新账号的数值挂在旧身份下
        SetCommonProperties();
        
        Debug.Log("赋值后用户唯一id为："+ GameDataManager.Instance.UserData.UserId);
    }
    
    public static void GuideBegin()
    {
        int mode = GameDataManager.Instance.UserData.levelMode;
        int id = GameDataManager.Instance.UserData.TutorialProgress+1;
        
        switch ((LevelType)mode)
        {
            case LevelType.BlockWord:
                id = GameDataManager.Instance.UserData.TutorialProgress+1;
                break;
            case LevelType.ChessWord:
                id = ChessGuideSystem.Instance.currentTutorial;
                break;
            case LevelType.HexWord:
                id = GameDataManager.Instance.UserData.TutorialProgress+1;
                break;
        }
     
        var properties = new Dictionary<string, object>(){{"guide_step", id}};
        Game.self.Analytics.LogEvent("guide_begin", properties, Define.DataTarget.Think);
    }
    
    public static void GuideComplete()
    {
        int mode = GameDataManager.Instance.UserData.levelMode;
        
        int id = GameDataManager.Instance.UserData.TutorialProgress+1;
        
        switch ((LevelType)mode)
        {
            case LevelType.BlockWord:
                id = GameDataManager.Instance.UserData.TutorialProgress+1;
                break;
            case LevelType.ChessWord:
                id = ChessGuideSystem.Instance.currentTutorial;
                break;
            case LevelType.HexWord:
                id = GameDataManager.Instance.UserData.TutorialProgress+1;
                break;
        }
        var properties = new Dictionary<string, object>{{"guide_step", id}};
        Game.self.Analytics.LogEvent("guide_complete", properties, Define.DataTarget.Think);
    }
    
    /// <summary>level_progress 上报关卡上限（规范 20261012：前 1000 关）</summary>
    public const int LevelProgressMaxStage = 1000;

    /// <summary>按当前玩法取关卡进度，避免两个重载各判一个字段导致口径不一致</summary>
    private static int CurrentStageForProgressGate()
    {
        var userData = GameDataManager.Instance?.UserData;
        if (userData == null) return 0;
        switch ((LevelType)userData.levelMode)
        {
            case LevelType.BlockWord:
            case LevelType.HexWord:
                return userData.CurrentHexStage;
            case LevelType.ChessWord:
                return userData.CurrentChessStage;
            default:
                return userData.CurrentChessStage;
        }
    }

    public static void LevelStart(float energy)
    {
        var properties = new Dictionary<string, object>
        {
            // 规范为数值类型，直接传 float，禁止 ToString，避免被锁成字符串字段
            { "level_difficulty_e", Math.Round((double)energy, 2) }
        };
        Game.self.Analytics.LogEvent("level_start", properties, Define.DataTarget.Think);
    }
    
    public static void LevelProgress(int wordIndex,string word,float duration,int errorCount,int combo,int userToolCount)
    {
        if (CurrentStageForProgressGate() > LevelProgressMaxStage) 
            return;
        
        var contentArray = new List<Dictionary<string, object>>();
        var contentItem = new Dictionary<string, object>
        {
            {"WordIndex", wordIndex},
            {"WordContent", word},
            {"WordDuration", duration},
            {"WordErrorNum", errorCount},
            {"WordComboLv", combo},
            {"WordItemNum", userToolCount},
        };
        contentArray.Add(contentItem);

        var properties = new Dictionary<string, object>
        {
            {"lv_content", contentArray}
        };
        Game.self.Analytics.LogEvent("level_progress", properties, Define.DataTarget.Think);
    }
    
    public static void LevelProgress(int wordIndex, string word, float duration, int errorCount, int combo, int userToolCount, float energy)
    {
        if (CurrentStageForProgressGate() > LevelProgressMaxStage) return;

        var contentItem = new Dictionary<string, object>
        {
            { "WordIndex", wordIndex },
            { "WordContent", word },
            { "WordDuration", duration },
            { "WordErrorNum", errorCount },
            { "WordComboLv", combo },
            { "WordItemNum", userToolCount },
            { "level_e", Math.Round((double)energy, 2) }
        };

        var properties = new Dictionary<string, object>
        {
            { "lv_content", new List<Dictionary<string, object>> { contentItem } }
        };
        Game.self.Analytics.LogEvent("level_progress", properties, Define.DataTarget.Think);
    }
    
    public static void LevelCompleted(float duration, float energy, float zenScore = 0f, int maxCombo = 0)
    {
        var thinkProps = new Dictionary<string, object>
        {
            { "lv_duration", Math.Round((double)duration, 2) },
            { "level_difficulty_e", Math.Round((double)energy, 2) },
            { "zen_score", zenScore },
            { "max_combo", maxCombo}
        };
        Game.self.Analytics.LogEvent("level_completed", thinkProps, Define.DataTarget.Think);
    }
    
    public static void LevelFailed()
    {
        //var properties = new Dictionary<string, object> { };
        Game.self.Analytics.LogEvent("level_fail", Define.DataTarget.Think);
    }
    public static void LevelExit()
    {
        //var properties = new Dictionary<string, object> { };
        Game.self.Analytics.LogEvent("level_quit", Define.DataTarget.Think);
    }
    
    
    /// <summary>
    /// 上报生命周期事件
    /// </summary>
    /// <param name="levelIndex">事件等级索引</param>
    /// <param name="minutes">目标分钟数</param>
    public static void ReportLifecycleEvent(int levelIndex, int minutes)
    {
        string eventName = $"time_level_{levelIndex}";
        
        // 上报埋点
        if (Game.self != null && Game.self.Analytics != null)
        {
            Game.self.Analytics.LogEvent(eventName, Define.DataTarget.Think);
        }
        
        Debug.Log($"上报生命周期事件: {eventName}, 累计时长: {GameDataManager.Instance.UserData.TotalOnlineMinutes:F1}分钟, 目标: {minutes}分钟");
       
    }
    
    #endregion


    #region 属性相关
   
    public static void NameChange(string name)
    {
        
    }
    
    /// <summary>
    /// 设置头像
    /// </summary>
    public static void HeadChange()
    {
        var properties = new Dictionary<string, object>{{"after_id", GameDataManager.Instance.UserData.UserHeadId.ToString()}};
        Game.self.Analytics.LogEvent("change_role_head", properties, Define.DataTarget.Think);
    }
    
    /// <summary>
    /// 获得资源与道具
    /// </summary>
    public static void ResourceGet(string resName,int changeNum,string reason)
    {
        var properties = new Dictionary<string, object>
        {
            {"resource_id", ResId.Normalize(resName)},
            {"change_type", "获得"},
            {"change_num",changeNum},
            {"change_reason",ResReason.Normalize(reason)},
        }; 
        Game.self.Analytics.LogEvent("resource_change", properties, Define.DataTarget.Think);
    }
  
    
    /// <summary>
    /// 资源变化（获得或消耗）
    /// </summary>
    /// <param name="resId">资源名（中文），内部统一归一成规范编码 0~13</param>
    /// <param name="changeNum">变化数量（正数为获得，负数为消耗）</param>
    /// <param name="reason">变化原因，内部统一收敛到规范枚举</param>
    public static void ResourceChange(string resId, int changeNum, string reason, string word)
    {
        string changeType = changeNum >= 0 ? "获得" : "消耗";
        var properties = new Dictionary<string, object>
        {
            { "resource_id", ResId.Normalize(resId) },
            { "change_type", changeType },
            { "change_num", Math.Abs(changeNum) },
            { "change_reason", ResReason.Normalize(reason) },
            { "tip_word", word ?? string.Empty }
        };
        Game.self.Analytics.LogEvent("resource_change", properties, Define.DataTarget.Think);
    }

    // 保留原有方法便于调用，内部调用统一方法
    public static void ResourceGet(string resName, int changeNum, string reason, string word)
        => ResourceChange(resName, changeNum, reason, word);

    public static void ResourceReduce(string resId, int changeNum, string reason, string word)
        => ResourceChange(resId, -changeNum, reason, word);
    
    #endregion
    
    
    
}