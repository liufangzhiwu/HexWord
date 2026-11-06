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
/// resource_change 的原因两级口径：change_reason（一级，仅 5 个封闭大类）+ reason_index（二级，具体场景）。
///
/// 背景：原 change_reason 枚举膨胀到 20+ 项，看板无法筛选；20261106 需求把原因收敛为 5 类，
/// 细节统一丢给 reason_index，取值对照如下（来源：飞书《通用数数埋点方案 / #事件数据 / resource_change》）：
///
///   change_reason   reason_index
///   内购            商品表「商品名」列的内容
///   系统活动        任务 / 竞速 / 莲心榜 / 月榜 / 限时奖励(灯笼) / 连胜(7、10、20、30 日)
///   激励视频        对应的视频点名称
///   关卡内行为      叶子收集 / 金币关 / 金币购买道具灯泡 / 火箭 / 复活 / 通关结算
///   其他            分享奖励 / 商店免费金币 / 抽取皮肤 / 自然恢复体力 / 金币购买体力 / 改名奖励 /
///                   广告追踪激励 / 插屏赠礼；都不符合时用 XXX，并找策划补 reason_index 枚举
/// </summary>
public static class ResReason
{
    #region 一级：change_reason（只允许这 5 个取值）
    public const string Iap = "内购";
    public const string SystemActivity = "系统活动";
    public const string RewardVideo = "激励视频";
    public const string InLevel = "关卡内行为";
    public const string Other = "其他";
    #endregion

    #region 二级：reason_index 常用取值
    // 系统活动
    public const string IdxTask = "任务";
    public const string IdxRace = "竞速";
    public const string IdxZenRank = "莲心榜";
    public const string IdxMonthRank = "月榜";
    public const string IdxLimitReward = "限时奖励";
    public const string IdxCombo = "连胜";

    // 关卡内行为
    public const string IdxLeafCollect = "叶子收集";
    public const string IdxGoldLevel = "金币关";
    public const string IdxBuyBulb = "金币购买道具灯泡";
    public const string IdxBuyRocket = "金币购买道具火箭";
    public const string IdxBuyRevive = "金币购买道具复活";
    public const string IdxLevelSettle = "通关结算";
    /// <summary>关卡内直接用掉库存道具（非金币购买）。需求表未列，待策划确认是否并入“金币购买道具XXX”。</summary>
    public const string IdxLevelUse = "关卡内使用道具";

    // 其他
    public const string IdxShare = "分享奖励";
    public const string IdxShopFreeGold = "商店免费金币";
    public const string IdxSkinDraw = "抽取皮肤";
    public const string IdxEnergyRecover = "自然恢复体力";
    public const string IdxEnergyBuy = "金币购买体力";
    public const string IdxRename = "改名奖励";
    public const string IdxAdTracking = "广告追踪激励";
    public const string IdxInterstitialGift = "插屏赠礼";
    /// <summary>需求表未列，待策划确认。</summary>
    public const string IdxIceRefund = "冰块拦截退还";
    /// <summary>需求表未列，待策划确认。</summary>
    public const string IdxReturnWin = "回归首胜奖励";
    /// <summary>需求表未列，待策划确认。</summary>
    public const string IdxSign = "签到";
    /// <summary>调试菜单直接改资源，仅测试环境出现。</summary>
    public const string IdxDebug = "调试加资源";

    /// <summary>上表场景都无法覆盖时的兜底值；一旦出现在正式数据里，必须找策划补枚举。</summary>
    public const string IdxUnknown = "XXX";
    #endregion

    /// <summary>一级 + 二级的成对取值。</summary>
    public struct Ctx
    {
        public readonly string Category;
        public readonly string Index;

        public Ctx(string category, string index)
        {
            Category = string.IsNullOrEmpty(category) ? Other : category;
            Index = string.IsNullOrEmpty(index) ? IdxUnknown : index;
        }
    }

    /// <summary>内购：reason_index = 商品表「商品名」列的内容。</summary>
    public static Ctx IapItem(string productName) => new Ctx(Iap, productName);

    /// <summary>激励视频：reason_index = 对应的视频点名称（建议与 videoAd_show.adName 保持一致）。</summary>
    public static Ctx VideoPoint(string pointName) => new Ctx(RewardVideo, pointName);

    /// <summary>“其他”类固定场景，例如 ResReason.OtherOf(ResReason.IdxShare)。</summary>
    public static Ctx OtherOf(string index) => new Ctx(Other, index);

    /// <summary>
    /// 把历史自由文本原因收敛成 (change_reason, reason_index)。
    /// 保留给既有调用点（UpdateGold/UpdateTool 的 message 参数）；新代码建议直接传 Ctx 的 Category/Index。
    /// </summary>
    public static Ctx Resolve(string reason)
    {
        string raw = reason == null ? string.Empty : reason.Trim();
        if (raw.Length == 0) return new Ctx(Other, IdxUnknown);

        // ---------- 内购：reason_index 取商品名 ----------
        if (raw == Iap) return new Ctx(Iap, IdxUnknown);
        if (raw.StartsWith("补单发货")) return new Ctx(Iap, Strip(raw, "补单发货"));
        if (raw.Contains("商店购买") || raw.Contains("购买金币"))
            return new Ctx(Iap, ExtractProductName(raw));

        // ---------- 这几条必须先于通用“广告”判断 ----------
        if (raw.Contains("广告追踪")) return new Ctx(Other, IdxAdTracking);
        // “复活广告消耗金币 / 金币复活赠送蝴蝶”属于关卡内行为，不能落到激励视频
        if (raw.Contains("复活")) return new Ctx(InLevel, IdxBuyRevive);

        // ---------- 激励视频：reason_index 取视频点名称 ----------
        if (raw.Contains("看广告") || raw.Contains("激励视频") || raw.Contains("商店金币视频"))
            return new Ctx(RewardVideo, raw);

        if (raw.Contains("插屏")) return new Ctx(Other, IdxInterstitialGift);

        // ---------- 关卡内行为 ----------
        if (raw.Contains("树叶") || raw.Contains("叶子")) return new Ctx(InLevel, IdxLeafCollect);
        if (raw.Contains("金币关") || raw.Contains("金币词")) return new Ctx(InLevel, IdxGoldLevel);
        if (raw.Contains("通关结算")) return new Ctx(InLevel, IdxLevelSettle);
        // 金币购买道具：调用点应按道具类型细分成 灯泡/火箭/复活，这里只兜底
        if (raw.Contains("金币购买道具") || raw.Contains("道具购买") || raw.Contains("购买道具"))
            return new Ctx(InLevel, IdxUnknown);
        if (raw.Contains("关卡内使用") || raw.Contains("退出关卡") || raw.Contains("进程中断"))
            return new Ctx(InLevel, IdxLevelUse);

        // ---------- 系统活动 ----------
        if (raw.Contains("月榜")) return new Ctx(SystemActivity, IdxMonthRank);
        if (raw.Contains("赛季") || raw.Contains("排行榜")) return new Ctx(SystemActivity, IdxZenRank);
        if (raw.Contains("限时奖励") || raw.Contains("限时宝箱") || raw.Contains("灯笼"))
            return new Ctx(SystemActivity, IdxLimitReward);
        if (raw.Contains("竞速")) return new Ctx(SystemActivity, IdxRace);
        if (raw.Contains("任务")) return new Ctx(SystemActivity, IdxTask);
        if (raw.Contains("连胜")) return new Ctx(SystemActivity, IdxCombo);
        if (raw.Contains("签到")) return new Ctx(SystemActivity, IdxSign);

        // ---------- 其他 ----------
        if (raw.Contains("分享")) return new Ctx(Other, IdxShare);
        if (raw.Contains("皮肤")) return new Ctx(Other, IdxSkinDraw);
        if (raw.Contains("自然恢复") || raw.Contains("时间恢复")) return new Ctx(Other, IdxEnergyRecover);
        if (raw.Contains("购买体力")) return new Ctx(Other, IdxEnergyBuy);
        if (raw.Contains("改名")) return new Ctx(Other, IdxRename);
        if (raw.Contains("冰块")) return new Ctx(Other, IdxIceRefund);
        if (raw.Contains("回归首胜")) return new Ctx(Other, IdxReturnWin);
        if (raw.Contains("DebugMenu") || raw.Contains("调试")) return new Ctx(Other, IdxDebug);

        // 兜底：通关类结算（如“树叶收集结算”已在上面拦掉，这里只剩无来源的“结算”）
        if (raw.Contains("结算")) return new Ctx(InLevel, IdxLevelSettle);

        Debug.LogWarning($"[Analytics] reason_index 未登记，按 {IdxUnknown} 上报：{raw}（请补枚举或调用点显式传值）");
        return new Ctx(Other, IdxUnknown);
    }

    /// <summary>从“商品名 + 商店购买XXX / 商品名 + 购买金币”这类拼接串里切出商品名。</summary>
    private static string ExtractProductName(string raw)
    {
        string s = raw;

        int buy = s.IndexOf("商店购买");
        if (buy > 0) s = s.Substring(0, buy);          // “金币礼包商店购买蝴蝶” → 金币礼包
        else if (buy == 0) s = s.Substring(4);          // “商店购买” + 商品名 → 商品名

        int gold = s.IndexOf("购买金币");
        if (gold >= 0) s = s.Substring(0, gold);        // “金币礼包购买金币” → 金币礼包

        s = s.Trim();
        return s.Length == 0 ? IdxUnknown : s;
    }

    private static string Strip(string raw, string prefix)
    {
        string s = raw.Substring(prefix.Length).Trim();
        return s.Length == 0 ? IdxUnknown : s;
    }
}


public partial class AnalyticMgr
{
    #region 进度相关
    private static DateTime? _startTime;// 使用 记录开始时间
    private static DateTime firstLoginTime = DateTime.MinValue;
    
    /// <summary>
    /// 游戏开始（登录）。
    /// </summary>
    /// <param name="countAsLogin">
    /// true：真正的登录（启动、换号），会累加 total_login / active_day 并上报 ta_app_start；
    /// false：从后台切回前台，只刷新会话时间与用户属性，**不计一次登录**。
    /// </param>
    public static void GameStart(bool countAsLogin = true)
    {
        SetLoginProperties(countAsLogin);

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
    /// <param name="countAsLogin">
    /// true 才累加 total_login / active_day 并上报启动事件；
    /// 从后台回前台时传 false，只刷新会话时间与用户属性。
    /// </param>
    private static void SetLoginProperties(bool countAsLogin = true)
    {
        // 记录本次会话开始时间（回前台也算一次新会话，用于时长统计）
        var now = DateTime.Now;
        _startTime = now;
        var userData = GameDataManager.Instance.UserData;
        var today = now.Date;

        if (countAsLogin)
        {
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

                if (userData.activeDayCnt <= 0) userData.activeDayCnt = 1;

                userData.totallogin++;
                userData.lastLoginDay = today.ToString("yyyy-MM-dd");
            }
        }
        else if (string.IsNullOrEmpty(userData.firstLoginTime))
        {
            // 兜底：首次登录一定先于回前台，这里只防止下面 Parse 抛异常
            userData.firstLoginTime = now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        firstLoginTime = DateTime.Parse(userData.firstLoginTime);

        // 计算生命周期天数（基于首次登录时间）
        int lifeDays = (today - firstLoginTime.Date).Days+ 1; // +1 表示第1天
        Debug.Log("当前时间："+today.ToString("yyyy-MM-dd HH:mm:ss")+"周期天数 life:"+lifeDays);

        // 留存回传只在真正的登录时机上报一次（回前台不算登录，不重复上报）
        if (countAsLogin && lifeDays == 2)
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

        // ★ ta_app_start 回前台也要发：用于会话/启动事件的配对统计。
        //   注意它只代表「应用回到前台」，不等于一次登录 —— 登录计数与启动转化在下面按 countAsLogin 处理。
        Game.self?.Analytics.LogEvent("ta_app_start", Define.DataTarget.Think);

        if (!countAsLogin)
        {
            Debug.Log("[Analytics] 回到前台：已上报 ta_app_start，但不累加登录次数、不报启动转化");
            return;
        }

        UnityTimer.Delay(3, () =>
        {
            if (!string.IsNullOrEmpty(GameDataManager.Instance.UserData.adTrackingEnabled))
            {
                //PushMessageReceiver._instance.OnOAIDClosedSignal(GameDataManager.Instance.UserData.adTrackingEnabled);
            }
            else
            {
#if UNITY_EDITOR 
            
                string enRange=Random.Range(0, 2).ToString();
                PushMessageReceiver._instance.OnOAIDClosedSignal(enRange);
#endif
            }
        });
        // 标准转化事件编码：0 安装 / 1 激活 / 2 启动 / 3 次日留存 / 4 付费。
        // 登录即视为一次「启动应用」(2)；「激活应用」(1) 已在 HarmonyAttribution.Init
        // 的启动阶段上报，不依赖登录，这里不再重复。
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
         
        var outproperties = new Dictionary<string, object>(){{"#duration", durationSeconds.ToString("0.00")}};
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
        => ResourceChange(resName, changeNum, reason, string.Empty);

    /// <summary>
    /// 资源变化（获得或消耗）。
    /// change_reason 只上报 5 个大类，细节走 reason_index。
    /// </summary>
    /// <param name="resId">资源名（中文），内部统一归一成规范编码 0~15</param>
    /// <param name="changeNum">变化数量（正数为获得，负数为消耗）</param>
    /// <param name="reason">变化原因（历史自由文本，内部会收敛成 5 大类 + reason_index）</param>
    /// <param name="word">关联的词语，无则传空</param>
    /// <param name="reasonIndex">reason_index 显式值；传了就不再做关键字推断（内购传商品名、激励视频传视频点名称）</param>
    /// <param name="reasonCategory">change_reason 显式值；与 reasonIndex 同时传时完全跳过推断</param>
    public static void ResourceChange(string resId, int changeNum, string reason, string word,
        string reasonIndex = null, string reasonCategory = null)
    {
        string category = reasonCategory;
        string index = reasonIndex;

        // 两个都没显式给，才走历史文本推断；只给一个时另一个用推断结果补齐
        if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(index))
        {
            ResReason.Ctx ctx = ResReason.Resolve(reason);
            if (string.IsNullOrEmpty(category)) category = ctx.Category;
            if (string.IsNullOrEmpty(index)) index = ctx.Index;
        }

        string changeType = changeNum >= 0 ? "获得" : "消耗";
        var properties = new Dictionary<string, object>
        {
            { "resource_id", ResId.Normalize(resId) },
            { "change_type", changeType },
            { "change_num", Math.Abs(changeNum) },
            { "change_reason", category },
            { "reason_index", index },
            { "tip_word", word ?? string.Empty }
        };
        Game.self.Analytics.LogEvent("resource_change", properties, Define.DataTarget.Think);
    }

    // 保留原有方法便于调用，内部调用统一方法
    public static void ResourceGet(string resName, int changeNum, string reason, string word,
        string reasonIndex = null, string reasonCategory = null)
        => ResourceChange(resName, changeNum, reason, word, reasonIndex, reasonCategory);

    public static void ResourceReduce(string resId, int changeNum, string reason, string word,
        string reasonIndex = null, string reasonCategory = null)
        => ResourceChange(resId, -changeNum, reason, word, reasonIndex, reasonCategory);
    
    #endregion
    
    
    
}