using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using Random = System.Random;

/// <summary>
/// 用户游戏数据管理类
/// 负责处理用户数据的加载、保存、初始化及日常管理
/// 使用JSON序列化和加密存储用户数据
/// </summary>
public class UserData
{
    #region 用户基础数据

    public string PlayerId; // 玩家ID

    /// <summary>
    /// PlayerId 转 int（排行榜用）。
    /// ★ 未登录 / 离线时 PlayerId 是 null（InitData 里就是 PlayerId = null），
    ///   直接 int.Parse 会抛 ArgumentException: Value cannot be null. Parameter name: s（ZenRankChangePanel 崩过）。
    /// </summary>
    public int GetPlayerIdInt()
    {
        int id = 0;
        int.TryParse(PlayerId, out id);
        return id;
    }
    public string ABName; // AB测试包名
    public string PushToken; // 推送Token
    public string UserName;
    public int UserHeadId;
    public int UserHeadBorderId;
    public string UserId; // 用户唯一标识
    public int Gold; // 当前金币数量
    public int TotalConsumedGold; // 历史累计消耗金币
    public int TotalEarnedGold; // 历史累计获得金币
    public int CurrentHexStage; // 当前六边形关卡进度
    public int MaxComboCount;                       // 最长连击数
    public int CurrentChessStage; // 当前拼字关卡进度
    public int levelMode; // 当前游戏模式 1:普通模式 2:拼字模式 3:六边形模式
    public string Zenlevel; // 禅修榜等级

    // 👇 新增：体力系统基础字段
    public int Energy; // 当前体力值
    public string LastEnergyUpdateTime; // 上次体力恢复的结算时间
    public bool hasUsedFreeRevive = false;
    public bool hasUsedGoldRevive = false; // 是否已使用过金币复活（首次金币复活赠蝴蝶）
    public int charInfoPopupCount; // 已弹出次数
    public string charInfoPopupLastTime; // 上次弹出时间
    /// <summary>无限体力结束时间（"yyyy-MM-dd HH:mm:ss"；空字符串表示从未购买）</summary>
    public string unlimitedEnergyEndTime = "";

    public int GoldLeaf; // 金箔数量
    public List<ThemeSaveItem> ThemeSaveItems = new List<ThemeSaveItem>();
    public Dictionary<int, int> ThemeItemUses; // 单个主题累计使用次数
    public int userthemeid = 0;
    public bool ischangetheme = false;
    public bool isGetNewHeadBorderIcon = false;
    public bool isGetNewHeadIcon = false;

    // 👇 新增：禅修榜是否主动加入标识（旧版本读取不到会默认为 false，但这没关系，旧版后端会放行）
    public bool isJoinedZenRank = false;

    // ===== 功能解锁持久标记（bug111~114）=====
    // ★ 只写一次，永不因"每日重置 / 赛季结算 / 资产清空"回退。
    //   解锁判定统一为：等级达标 OR 这里的持久标记。
    //   禁止再用 timerePuzzleid / isJoinedZenRank / currPupa 这类可变字段当解锁条件。
    /// <summary>解锁迁移版本号，0 = 还没跑过迁移</summary>
    public int unlockSchemaVersion = 0;
    /// <summary>蝶园（养成）是否已解锁（老用户升级继承）</summary>
    public bool butterflyGardenUnlocked = false;
    /// <summary>限时活动（灯笼）是否已解锁（老用户升级继承）</summary>
    public bool timeLimitUnlocked = false;
    /// <summary>莲心榜是否已解锁（与"本季是否参赛 isJoinedZenRank"解耦）</summary>
    public bool zenRankUnlocked = false;
    /// <summary>每日任务是否已解锁（老用户升级继承）</summary>
    public bool dailyTaskUnlocked = false;
    /// <summary>锦鲤竞速（捕鱼）是否已解锁（与"本回合是否在赛 roundstarttime"解耦）</summary>
    public bool fishRaceUnlocked = false;

    #endregion

    #region 系统设置数据
    public bool IsMusicOn = true; // 背景音乐开关
    public bool IsSoundOn = true; // 音效开关
    public bool IsVibrationOn =true;    // 震动反馈开关
    public bool IsPushRequested =false;    // 推送权限是否已经请求
    public bool IsAutoPush =false;    // 自动提醒是否开启
    // 同步自 WT 1.9.8（emoji 开关）
    public bool IsEmojiOn = true;      // emoji开关
    public bool HasUnclaimedUpdateJoin;       // 是否升级版本
    public string LanguageCode; // 当前语言代码
    public bool IsAgreePrivacy; // 同意用户隐私协议

    #endregion

    #region 游戏进度数据

    public int TutorialProgress; // 新手引导进度
    public Dictionary<int, bool> ChessTutorialProgress; // 填字引导进度

    public int GetTutorialProgress()
    {
        return TutorialProgress;
    }

    public bool Rigister; // 注册标志
    public string first_version; // 注册标志
    public bool IsFirstLaunch = true; // 首次启动标志
    public bool isShowVocabulary; // 是否显示词库标志

    public int TotalPayTimes; //支付次数
    public float TotalRevenue; //累计付费金额

    // 生命周期事件相关数据
    public float TotalOnlineMinutes; // 累计在线总时长（分钟）
    public Dictionary<string, bool> ReportedLifecycleEvents; // 已上报的生命周期事件


    // --- 广告策略存档数据 ---
    public int AdFatigueScore; // 疲劳分数
    public float TotalPlayTimeSeconds; // 累计游戏时长 (G2)
    public long LastPayTimeTicks; // 上次付费时间 (Ticks) (G3)
    public long LastRewardAdTimeTicks; // 上次看激励视频的时间 (G1)
    public string adTrackingEnabled;//广告权限是否开启

    /// <summary>广告追踪（关联应用）引导页是否已经展示过——只弹一次</summary>
    public bool adTrackingGuideShown;

    /// <summary>广告追踪激励金币是否已领取——只能领一次，领过不再出现</summary>
    public bool adTrackingRewardClaimed;
    
    /// <summary>
    /// 单次付费最大金额 A（用于 IAP 层细分）。
    /// </summary>
    public float MaxSinglePayAmount = 0f;

    public long LastInterstitialTimeTicks; // 上次看插屏的时间 (G5)

    /// <summary>
    /// 已成功播放的插屏广告累计次数（跨启动累加，重启不清零）。
    /// 破冰礼包条件之一：插屏 &gt; 3（需求 C-STRAT-08）；也作为结算礼包调度的次数入参。
    /// 由 AdRuleManager.AddOneInterAdsCount 累加、AdRuleManager.SetInterAdsCount（GM）直接赋值。
    /// </summary>
    public int interAdsTotalCount;

    public int HighestZenScore = 0; // 历史最高禅意分
    public Dictionary<int, float> BestClearTimes = new Dictionary<int, float>(); // 极速通关记录字典

    // --- 促销角标存档数据（随 UserData 同步服务器，卸载重装可恢复） ---

    /// <summary>
    /// 商店促销角标消费截止时间（"yyyy-MM-dd HH:mm:ss"）。
    /// 语义：
    ///   - 空字符串：本次节日/周末未进店消费，角标正常显示；
    ///   - 非空且当前时间 &lt; 该时间：本次节日/周末已消费，角标隐藏（重启游戏也保持隐藏）；
    ///   - 当前时间 &gt;= 该时间：本次节日/周末已结束，读取时自动清空并允许下一个节日/周末重新显示。
    /// 由 ShopManager.OnShopEntered() 在节日/周末进店时写入，值为 GetCurrentHolidayEndTime()。
    /// </summary>
    public string shopEntryBadgeConsumedUntil = "";

    /// <summary>
    /// 词库数据
    /// </summary>   
    public WordVocabulary<string> wordVocabularyChinSim = new WordVocabulary<string>();

    #endregion
    
    
    #region 统计计数数据
    
    public string showRateusTime;                       // 好评界面显示时间
    public int showRateusCount;                       // 好评界面显示次数
    public int dayPassStageCount;                      // 当日通关次数
    public int chessdayPassStageCount;                      // 拼字当日通关次数
    public bool isChangeUserName;                       // 是否更改过用户名称
    public int totallogin;                               // 总登录次数
    public int totalSeeAds;                               // 总看广告次数
    public int totalInsetSeeAds;                               // 总看广告次数
    public int activeDayCnt;                               // 活跃天数
    public int zenCount;                                    // 禅意值数量
    public int overallZenScore;                            // 总榜分
    public int likeCount;                                  // 点赞数量
    public int fourWordCount;                                  // 掌握的四字成语
    public int nofourWordCount;                                  // 掌握的非四字成语
    // 👇 新增：D规则（每日首关插屏概率）状态记录
    public bool isDayFirstLevelAdChecked;              // 今日首关是否已进行过插屏概率判定
    public bool isDayFirstLevelAdAllowed;              // 今日首关插屏概率判定的结果
    // 生命周期事件配置
    private readonly int[] LIFE_CYCLE_MINUTES = { 1, 5, 10, 15, 20, 30, 40, 60, 120, 300, 600 };
    private readonly string LIFE_CYCLE_EVENT_PREFIX = "time_level_";
    
    #endregion

    #region 时间相关数据

    public string logoutTime; // 退出时间
    public string curStageStartTime; // 当前关卡开始时间
    public bool curIsEnter; // 当前关卡是否已经进入

    public int curStageOnlineTime; // 当前关卡在线时长(秒)，兼容旧字段，值 = curStageElapsedMs/1000

    // ---- 关卡时长计时（毫秒 + 可暂停窗口）----
    public long curStageElapsedMs;        // 已封存的累计时长(毫秒)
    public long curStageWindowStartTicks; // 计时窗口起点(本地时间 Ticks)，0 表示窗口关闭（暂停中）
    public bool curStageTimerActive;      // 计时窗口当前是否打开
    public bool curStageSessionOpen;      // 本局计时会话是否已开始且未结束

    // 关卡对应通关时长
    public Dictionary<int, int> passLevelUseTime = new Dictionary<int, int>();

    public string firstPayTime; //首次充值时间
    public string lastPayTime; //最后充值时间
    public string firstLoginTime; //首次登录时间
    public string lastLoginDay; //最后登录时间

    #endregion
    

    #region 道具数据

    /// <summary>
    /// 道具信息字典
    /// Key: 道具ID
    ///   101 = SignleHint（单字提示，旧版已合并到 102）
    ///   102 = WordHint（词语提示/放大镜）
    ///   103 = Butterfly（蝴蝶）
    ///   104 = AutoComplete（火箭/自动完成）
    ///   105 = Restart（复活道具 ⏰）
    /// Value: 道具信息
    /// </summary>
    public Dictionary<int, ToolInfo> toolInfo;

    //限时活动数据
    public int timePuzzlecount; // 限时活动中连出成语数量
    public int timerePuzzleid; // 限时活动中奖励领取id
    public string limitOpenTime; // 限时活动开启时间
    public int limitMinPeriod; // 限时翻倍周期（分钟）
    public string limitEndTime; // 限时翻倍结束时间
    public bool isNeedShowHelp; // 是否需要主动弹窗帮助界面
    public bool isDayEnterLimint; // 限时活动重置后是否为首次进入

    /// <summary>
    /// 限时活动「幸运循环」标记：最后一个灯笼（最后一段）已至少完成过一次。
    /// 置位后 timerePuzzleid 固定在最后一段、timePuzzlecount 每完成一次就回到该段起点，
    /// 玩家可以反复领取最后一个灯笼的奖励，直到 0 点/活动刷新（ResetDailyData）时清零。
    /// </summary>
    public bool luckyLoopEntered;

    /// <summary>
    /// 每日任务数据
    /// </summary>
    /// 
    /// 完成任务id
    public List<CompleteTaskData> completeTaskList = new List<CompleteTaskData>();

    public bool butterflyTaskIsOpen; // 每日任务无限蝴蝶任务是否开启
    public string butterflyTaskOpenTime; // 每日任务无限蝴蝶任务开启时间
    public int taskButterflyUseMinutes; // 每日任务无限蝴蝶任务使用分钟
    public bool isAllCompleteTask; // 每日任务活动是否全部完成

    /// 任务数据
    public List<TaskSaveData> taskSaveDatas = new List<TaskSaveData>();

    /// <summary>
    /// 商店限时商品数据
    /// </summary>
    public List<ShopLimitData> limitShopItems = new List<ShopLimitData>();

    /// <summary>
    /// 购买记录（最近一次在最前，最多 MAX_PURCHASE_RECORDS 条）。
    /// 用于"购买记录"弹窗展示：礼包名称、价格、礼包内容、购买时间。
    /// 由 UserData.AddPurchaseRecord 写入，由 PurchaseRecordScreen 读取展示。
    /// </summary>
    public List<PurchaseRecord> purchaseRecords = new List<PurchaseRecord>();

    public bool isHideShopRedPoint; // 商店每日免费商品是否获得
    public bool isDayFreeGet; // 商店每日免费商品是否获得
    public bool isDayGoldBuy; // 商店每日金币购买商品是否买过
    public bool isDayMoneyBuy; // 商店每日现金购买商品是否买过
    public bool isShowDiscountGift; // 折扣礼包是否显示
    //public bool isShowChessDiscountPanel; // 折扣礼包是否显示


    public SignSaveData _signSaveData = new SignSaveData();
    public LoadTimeIndex _loadTimeIndexData = new LoadTimeIndex();
    /// <summary>
    /// 特殊头像
    /// </summary>
    public List<int> _getAnimalsHeadIcons=new List<int>();
    /// <summary>
    /// 头像框
    /// </summary>
    public List<int> _getHeadBorderIcons=new List<int>();
    private static readonly Random _random = new Random();
    
    // ==== 广告赠礼相关 ====
    public int DayRewardVideoGiftCount;   // 当日灯泡激励视频赠礼已发次数
    public int DayInterstitialGiftCount;  // 当日插屏赠礼已发次数
    public bool HasShownFirstAdGift;      // 是否已发过"首个广告"50金币一次性奖励

    // ==== 广告获取失败策略限次（S_F_T_A_B_C）持久化 ====
    /// <summary>
    /// 商店金币视频（按日）、分享（按日）、提示/复活视频（按关）的已用次数。
    /// 随 UserData 一起落盘，保证关闭游戏后计数可继承。
    /// </summary>
    public AdLimitSaveData adLimit = new AdLimitSaveData();

    #endregion

    #region 购买记录上限

    /// <summary>购买记录最多保留条数（超出自动截断最旧的）</summary>
    [JsonIgnore] public const int MAX_PURCHASE_RECORDS = 100;

    #endregion
    #region 新手奖励活动数据
    public bool isBeginnerBonusTriggered = false; // 终身是否已触发过（确保仅开一次）
    public bool isBeginnerBonusCompleted = false; // 是否已领奖/完成
    public string beginnerBonusStartTime = "";    // 开启时间（ISO 8601 或 ToString）
    #endregion

    #region 文件路径管理

    /// <summary>
    /// 获取用户数据保存路径
    /// </summary>
    public string Getfilepath
    {
        get => Path.Combine(Application.persistentDataPath, "userData.json");
    }

    #endregion


    #region 数据初始化方法

    public bool LocalDataIsNull()
    {
        bool IsNull = false;
        string filePath = Getfilepath;


        if (File.Exists(filePath))
        {
            IsNull = false;
        }
        else
        {
            IsNull = true;
        }

        return IsNull;

    }

    /// <summary>
    /// 加载用户数据
    /// </summary>
    public void LoadData()
    {
        string filePath = Getfilepath;

        if (!File.Exists(filePath))
        {
            Debug.LogWarning("未找到用户数据文件，使用默认数据初始化");
            InitData();
            return;
        }

        try
        {
            string encryptedJson = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            //解密
            string json = SecurityProvider.RestoreData(encryptedJson);

            Debug.Log($"加载用户数据: {json}");
            UserData loadedData = JsonConvert.DeserializeObject<UserData>(json);

            if (loadedData.CurrentHexStage <= 0)
            {
                Debug.LogError($"关卡数据异常: {json}");
                InitData();
                AnalyticMgr.BugRecord("关卡存档异常", json);
                return;
            }

            InitData(loadedData);
        }
        catch (Exception ex)
        {
            Debug.LogError($"加载用户数据异常: {ex.Message}");
            InitData();
        }
    }


    /// <summary>
    /// 初始化默认用户数据
    /// </summary>
    public void InitData()
    {
        # region 初始数据

        // 基础数据
        // 用户基础数据
        PlayerId = null;
        ABName = "0";
        PushToken = "";
        UserHeadId = 0;
        UserHeadBorderId=0;
        UserName = "";
        UserId = null;
        Rigister = false;
        Gold = AppGameSettings.StartingGold;
        MaxComboCount = 0;
        TotalConsumedGold = 0;
        TotalEarnedGold = 0;
        CurrentHexStage = AppGameSettings.FirstLevel;
        CurrentChessStage = AppGameSettings.FirstLevel;
        levelMode = 2;
        dayPassStageCount = 0;
        chessdayPassStageCount = 0;
        LanguageCode = GetLanguage();
        IsMusicOn = true;
        IsSoundOn = true;
        IsVibrationOn = true;
        IsPushRequested = false;
        IsAutoPush = false;
        IsAgreePrivacy = false;
        Zenlevel = "ZenState01";
        first_version = "";
        HasUnclaimedUpdateJoin = false;
        Energy = 5;
        LastEnergyUpdateTime = DateTime.Now.ToString();
        hasUsedFreeRevive = false;
        hasUsedGoldRevive = false;
        // 评价界面显示次数
        showRateusCount = 0;
        // 评价界面显示时间
        showRateusTime = null;
        isChangeUserName = false;
        isGetNewHeadBorderIcon = false;
        isGetNewHeadIcon = false;
        isJoinedZenRank = false;
        // 同步自 WT 1.9.8
        IsEmojiOn = true;

        // 功能解锁持久标记：新号一律从"未解锁 + 未迁移"开始
        unlockSchemaVersion = 0;
        butterflyGardenUnlocked = false;
        timeLimitUnlocked = false;
        zenRankUnlocked = false;
        dailyTaskUnlocked = false;
        fishRaceUnlocked = false;

        // 游戏进度
        TutorialProgress = 0;
        ChessTutorialProgress = new Dictionary<int, bool>
            { { 1, false }, { 2, false }, { 3, false }, { 4, false }, { 5, false } };
        IsFirstLaunch = true;
        isShowVocabulary = false;
        //支付次数
        TotalPayTimes = 0;
        //累计付费金额
        TotalRevenue = 0;
        //总登录次数
        totallogin = 0;
        //总看广告次数
        totalSeeAds = 0;
        totalInsetSeeAds = 0;
        //活跃天数
        activeDayCnt = 0;
        // 时间数据
        logoutTime = DateTime.Now.ToString();
        firstPayTime = DateTime.MinValue.ToString("yyyy-MM-dd HH:mm:ss");
        lastPayTime = DateTime.MinValue.ToString("yyyy-MM-dd HH:mm:ss");
        firstLoginTime = DateTime.Now.ToString();
        lastLoginDay = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        curStageStartTime = null;
        curStageOnlineTime = 0;
        curStageElapsedMs = 0;
        curStageWindowStartTicks = 0;
        curStageTimerActive = false;
        curStageSessionOpen = false;
        curIsEnter = false;
        zenCount = 0;
        overallZenScore = 0;
        likeCount = 0;
        fourWordCount = 0;
        nofourWordCount = 0;
        passLevelUseTime = new Dictionary<int, int>();
        //限时商店数据
        limitShopItems = new List<ShopLimitData>();
        // ★ 购买记录
        purchaseRecords = new List<PurchaseRecord>();
        isDayFreeGet = false;
        isDayGoldBuy = false;
        isDayMoneyBuy = false;
        isShowDiscountGift = false;
        isHideShopRedPoint = false;
        unlimitedEnergyEndTime = "";

        ThemeSaveItems = new List<ThemeSaveItem>
        {
            new() { id = 0, isGet = true },
            new() { id = 1, isGet = true },
        };
        ThemeItemUses = new Dictionary<int, int>() { { 0, 1 } };
        GoldLeaf = 0;
        userthemeid = 0;
        ischangetheme = false;

        // 生命周期事件相关数据初始化
        TotalOnlineMinutes = 0f;
        ReportedLifecycleEvents = new Dictionary<string, bool>();
        foreach (var minutes in LIFE_CYCLE_MINUTES)
        {
            string eventKey = $"{LIFE_CYCLE_EVENT_PREFIX}{minutes}";
            ReportedLifecycleEvents[eventKey] = false;
        }

        // 初始化道具数据（101~104 原有，105 复活道具新增）
        toolInfo = new Dictionary<int, ToolInfo>
        {
            { 
                101, new ToolInfo {
                    cost = AppGameSettings.ShopItems.SingleHintCost, type = "SignleHint",
                    count = 0 } },
            {
                102, new ToolInfo {
                    cost = AppGameSettings.ShopItems.SingleHintCost, type = "WordHint",
                    count = AppGameSettings.ShopItems.WordHintCount } },
            { 
                103, new ToolInfo {
                    cost = AppGameSettings.ShopItems.ButterflyCost, type = "Butterfly",
                    count = AppGameSettings.ShopItems.StartingButterflies } },
            {
                104, new ToolInfo{
                    cost = AppGameSettings.ShopItems.AutoCompleteCost, type = "AutoComplete",
                    count = AppGameSettings.ShopItems.WordHintCount } },
            // ★ 105 复活道具（Restart）
            {
                105, new ToolInfo{
                    // TODO: 接入配置表后改为 AppGameSettings.ShopItems.RestartCost
                    cost = AppGameSettings.ShopItems.AutoCompleteCost, type = "Restart",
                    count = 0 } }
        };

        butterflyTaskIsOpen = false;
        taskButterflyUseMinutes = 0;
        butterflyTaskOpenTime = null;

        //显示奖励数据
        timerePuzzleid = 0;
        limitOpenTime = null;
        limitMinPeriod = 0;
        limitEndTime = null;
        isDayEnterLimint = true;
        timePuzzlecount = 0;
        luckyLoopEntered = false;
        isNeedShowHelp = true;
        //每日任务数据
        completeTaskList = new List<CompleteTaskData>();
        taskSaveDatas = new List<TaskSaveData>();
        isAllCompleteTask = false;
        wordVocabularyChinSim = new WordVocabulary<string>();

        AdFatigueScore = 0;
        TotalPlayTimeSeconds = 0f;
        LastPayTimeTicks = 0;
        LastRewardAdTimeTicks = 0;
        LastInterstitialTimeTicks = 0;
        interAdsTotalCount = 0;
        isDayFirstLevelAdChecked = false;
        isDayFirstLevelAdAllowed = false;

        HighestZenScore = 0;
        BestClearTimes = new Dictionary<int, float>();
        
        _signSaveData=new SignSaveData();
        _loadTimeIndexData=new LoadTimeIndex();
        _getAnimalsHeadIcons=new List<int>();
        _getHeadBorderIcons = new List<int>() { 0, 1, 2, 3 };
        DayRewardVideoGiftCount = 0;
        DayInterstitialGiftCount = 0;
        HasShownFirstAdGift = false;
        adLimit = new AdLimitSaveData();
        adTrackingEnabled = "";
        adTrackingGuideShown = false;
        adTrackingRewardClaimed = false;
        MaxSinglePayAmount = 0f;

        // ★ 促销角标消费截止时间（新号默认空字符串 = 未消费）
        shopEntryBadgeConsumedUntil = "";

        // 同步自 WT 1.9.8：新手奖励
        isBeginnerBonusTriggered = false;
        isBeginnerBonusCompleted = false;
        beginnerBonusStartTime = "";

        #endregion

        // ★ 重置数据（GM 重置 / 新号 / 清档）同样换了 limitShopItems 整张表，
        //   ShopManager 的 shoplimitDatas 缓存必须跟着重建，否则缓存里全是"表外孤儿"：
        //   后续 AddBulbExposureFail / 购买计数都写到孤儿上，存档里看不到，重进游戏就归零。
        if (GameDataManager.Instance != null && GameDataManager.Instance.UserData != null
            && ShopManager.shopManager != null)
            ShopManager.shopManager.RefreshLimitData();
    }

    /// <summary>
    /// 从现有用户数据初始化
    /// </summary>
    /// <param name="user">源用户数据</param>
    public void InitData(UserData user)
    {
        if (user == null) return;

        // 基础数据
        PlayerId = user.PlayerId;
        ABName = user.ABName;
        PushToken=user.PushToken;
        UserHeadId = user.UserHeadId;
        UserHeadBorderId=user.UserHeadBorderId;
        UserName = user.UserName;
        UserId = user.UserId;
        Gold = user.Gold;
        TotalConsumedGold = user.TotalConsumedGold;
        TotalEarnedGold = user.TotalEarnedGold;
        CurrentHexStage = user.CurrentHexStage;
        CurrentChessStage = user.CurrentChessStage;
        levelMode = user.levelMode;
        zenCount = user.zenCount;
        overallZenScore = user.overallZenScore;
        likeCount = user.likeCount;
        MaxComboCount = user.MaxComboCount;
        fourWordCount = user.fourWordCount;
        nofourWordCount=user.nofourWordCount;
        Zenlevel = user.Zenlevel ?? "ZenState01";
        dayPassStageCount = user.dayPassStageCount;
        chessdayPassStageCount = user.chessdayPassStageCount;
        LanguageCode = GetLanguage();
        first_version=user.first_version;
        IsMusicOn = user.IsMusicOn;
        IsSoundOn = user.IsSoundOn;
        IsVibrationOn= user.IsVibrationOn;
        IsPushRequested= user.IsPushRequested;
        IsAutoPush= user.IsAutoPush;
        HasUnclaimedUpdateJoin=user.HasUnclaimedUpdateJoin;
        IsAgreePrivacy = user.IsAgreePrivacy;
        Rigister = user.Rigister;
        firstLoginTime = user.firstLoginTime ?? DateTime.Now.ToString();
        lastLoginDay = user.lastLoginDay;
        firstPayTime = user.firstPayTime;
        lastPayTime = user.lastPayTime;
        isShowDiscountGift = user.isShowDiscountGift;
        unlimitedEnergyEndTime = user.unlimitedEnergyEndTime;
        charInfoPopupLastTime = user.charInfoPopupLastTime;
        charInfoPopupCount = user.charInfoPopupCount;   // 同步自 WT 1.9.8：补齐弹窗次数拷贝

        // 👇 🌟 核心修复：通过时间戳是否存在，来精准判断是不是老玩家首次更新
        if (string.IsNullOrEmpty(user.LastEnergyUpdateTime))
        {
            // 老玩家首次更新到新版本，作为福利直接送满体力，并初始化时间！
            Energy = 5;
            LastEnergyUpdateTime = DateTime.Now.ToString();
        }
        else
        {
            // 正常玩家（新玩家或已经有体力系统的玩家），直接继承存档里的值
            Energy = user.Energy;
            LastEnergyUpdateTime = user.LastEnergyUpdateTime;
        }

        hasUsedFreeRevive = user.hasUsedFreeRevive;
        hasUsedGoldRevive = user.hasUsedGoldRevive;
        // 同步自 WT 1.9.8（其余设置项 HexWord 侧已在上方拷贝）
        IsEmojiOn = user.IsEmojiOn;
        GoldLeaf=user.GoldLeaf;
        
        ThemeSaveItems=user.ThemeSaveItems.Count<=0?new List<ThemeSaveItem>{
            new() {id = 0,isGet = true},
            new() {id = 1,isGet = true},
        }:user.ThemeSaveItems;
        ThemeItemUses=user.ThemeItemUses??new Dictionary<int,int>(){{0,1}};
        userthemeid = user.userthemeid;
        ischangetheme=user.ischangetheme;
        isGetNewHeadBorderIcon = user.isGetNewHeadBorderIcon;
        isGetNewHeadIcon = user.isGetNewHeadIcon;
        isJoinedZenRank = user.isJoinedZenRank;
        isChangeUserName = user.isChangeUserName;
        // 评价界面显示次数
        showRateusCount = user.showRateusCount;
        // 评价界面显示时间
        showRateusTime = user.showRateusTime;
        //限时商店数据
        // ★ 不能整表替换：服务器 / 读档那份不携带决策运行时数据（F / 付费倾向 / 探价 / 购买次数），
        //   直接赋值会把本地刚写入的记录连同 ShopManager 的缓存一起变成"孤儿"——写进去的失败分
        //   存不下来、下次进店读回 0。改为按 id 合并，运行时字段以本地为准。
        limitShopItems = MergeLimitShopItems(limitShopItems, user.limitShopItems);
        // ★ 购买记录（旧存档没有该字段时为 null，兜底成空列表）
        purchaseRecords = user.purchaseRecords ?? new List<PurchaseRecord>();
        isDayFreeGet = user.isDayFreeGet;
        isDayGoldBuy = user.isDayGoldBuy;
        isDayMoneyBuy = user.isDayMoneyBuy;
        isHideShopRedPoint = user.isHideShopRedPoint;
        //支付次数
        TotalPayTimes = user.TotalPayTimes;
        //累计付费金额
        TotalRevenue = user.TotalRevenue;
        //总登录次数
        totallogin = user.totallogin;
        //总看广告次数
        totalSeeAds = user.totalSeeAds;
        totalInsetSeeAds = user.totalInsetSeeAds;
        //活跃天数
        activeDayCnt = user.activeDayCnt;
        // 游戏进度
        TutorialProgress = user.TutorialProgress;
        butterflyTaskIsOpen = user.butterflyTaskIsOpen;
        taskButterflyUseMinutes = user.taskButterflyUseMinutes;
        butterflyTaskOpenTime = user.butterflyTaskOpenTime;
        passLevelUseTime = user.passLevelUseTime;
        ChessTutorialProgress = user.ChessTutorialProgress ?? new Dictionary<int, bool>
            { { 1, false }, { 2, false }, { 3, false }, { 4, false }, { 5, false } };
        IsFirstLaunch = user.IsFirstLaunch;
        isShowVocabulary = user.isShowVocabulary;

        ThemeSaveItems = user.ThemeSaveItems.Count <= 0
            ? new List<ThemeSaveItem>
            {
                new() { id = 0, isGet = true },
                new() { id = 1, isGet = true },
            }
            : user.ThemeSaveItems;
        ThemeItemUses = user.ThemeItemUses ?? new Dictionary<int, int>() { { 0, 1 } };
        userthemeid = user.userthemeid;
        ischangetheme = user.ischangetheme;

        // 时间数据
        logoutTime = user.logoutTime;
        curIsEnter = user.curIsEnter;
        // 初始化道具数据
        toolInfo = user.toolInfo??new Dictionary<int, ToolInfo>
        {
            { 
                101, new ToolInfo {
                    cost = AppGameSettings.ShopItems.SingleHintCost, type = "SignleHint",
                    count = 0 } },
            {
                102, new ToolInfo {
                    cost = AppGameSettings.ShopItems.SingleHintCost, type = "WordHint",
                    count = AppGameSettings.ShopItems.WordHintCount } },
            { 
                103, new ToolInfo {
                    cost = AppGameSettings.ShopItems.ButterflyCost, type = "Butterfly",
                    count = AppGameSettings.ShopItems.StartingButterflies } },
            {
                104, new ToolInfo{
                    cost = AppGameSettings.ShopItems.AutoCompleteCost, type = "AutoComplete",
                    count = AppGameSettings.ShopItems.WordHintCount } },
            // ★ 105 复活道具（Restart）
            {
                105, new ToolInfo{
                    cost = AppGameSettings.ShopItems.AutoCompleteCost, type = "Restart",
                    count = 0 } }
        };

        // ★ 兼容旧存档：确保 105 复活道具存在（旧版本没有该字段）
        if (!toolInfo.ContainsKey(105))
        {
            toolInfo[105] = new ToolInfo
            {
                cost = AppGameSettings.ShopItems.AutoCompleteCost, // TODO: 接入配置表后改为 RestartCost
                type = "Restart",
                count = 0
            };
            Debug.Log("[UserData] 旧存档兼容：初始化 105 复活道具 (Restart)");
        }
        
        if (toolInfo.TryGetValue(101, out var tool101))
        {
            if (tool101.count>0)
            {
                // 确保目标道具 104 存在，若不存在则创建
                if (!toolInfo.TryGetValue(104, out var tool104))
                {
                    tool104 = new ToolInfo
                    {
                        cost = AppGameSettings.ShopItems.AutoCompleteCost, type = "AutoComplete",
                        count = AppGameSettings.ShopItems.WordHintCount
                    }; // 根据实际 Tool 类的构造函数调整
                    toolInfo[104] = tool104;
                }

                // 合并数量
                toolInfo[104].count += toolInfo[102].count;
                // 移除源道具
                toolInfo[102].count = toolInfo[101].count;
                toolInfo[101].count = 0;
            }

            if (toolInfo[102].cost > AppGameSettings.ShopItems.SingleHintCost)
            {
                toolInfo[102].cost = AppGameSettings.ShopItems.SingleHintCost;
            }
        }
       
        curStageStartTime = user.curStageStartTime;
        curStageOnlineTime = user.curStageOnlineTime;
        // 老存档迁移：只有旧的整型秒数时折算成毫秒累计
        curStageElapsedMs = user.curStageElapsedMs;
        if (curStageElapsedMs <= 0 && user.curStageOnlineTime > 0)
            curStageElapsedMs = user.curStageOnlineTime * 1000L;
        // 冷启动后窗口一律从关闭态开始，由玩法层在恢复关卡时重新开窗
        curStageWindowStartTicks = 0;
        curStageTimerActive = false;
        curStageSessionOpen = false;
        //显示奖励数据
        timerePuzzleid = user.timerePuzzleid;
        limitOpenTime = user.limitOpenTime;
        limitMinPeriod = user.limitMinPeriod;
        limitEndTime = user.limitEndTime;
        isDayEnterLimint = user.isDayEnterLimint;
        timePuzzlecount = user.timePuzzlecount;
        luckyLoopEntered = user.luckyLoopEntered;   // 幸运循环标记（旧存档无此字段 → false）
        isNeedShowHelp = user.isNeedShowHelp;
        //每日任务数据
        completeTaskList = user.completeTaskList;
        taskSaveDatas = user.taskSaveDatas;
        isAllCompleteTask = user.isAllCompleteTask;
        wordVocabularyChinSim = user.wordVocabularyChinSim;

        // 生命周期事件相关数据
        TotalOnlineMinutes = user.TotalOnlineMinutes;
        ReportedLifecycleEvents = user.ReportedLifecycleEvents ?? new Dictionary<string, bool>();

        AdFatigueScore = user.AdFatigueScore;
        TotalPlayTimeSeconds = user.TotalPlayTimeSeconds;
        LastPayTimeTicks = user.LastPayTimeTicks;
        LastRewardAdTimeTicks = user.LastRewardAdTimeTicks;
        LastInterstitialTimeTicks = user.LastInterstitialTimeTicks;
        // ★ 插屏累计次数：旧存档没有该字段时默认 0（Json 反序列化即 0），负值兜底成 0
        interAdsTotalCount = user.interAdsTotalCount > 0 ? user.interAdsTotalCount : 0;
        isDayFirstLevelAdChecked = user.isDayFirstLevelAdChecked;
        isDayFirstLevelAdAllowed = user.isDayFirstLevelAdAllowed;

        HighestZenScore = user.HighestZenScore;
        BestClearTimes = user.BestClearTimes ?? new Dictionary<int, float>();

        _signSaveData = user._signSaveData?.Clone() ?? new SignSaveData();
        _loadTimeIndexData = user._loadTimeIndexData?.Clone() ?? new LoadTimeIndex();
        _getAnimalsHeadIcons = user._getAnimalsHeadIcons;
        _getHeadBorderIcons = user._getHeadBorderIcons ?? new List<int>() { 0, 1, 2, 3 };
        DayRewardVideoGiftCount=user.DayRewardVideoGiftCount;
        DayInterstitialGiftCount=user.DayInterstitialGiftCount;
        HasShownFirstAdGift=user.HasShownFirstAdGift;
        // ★ 广告限次（S_F_T_A_B_C）：旧存档没有该字段时兜底为新对象
        adLimit = user.adLimit != null ? user.adLimit.Clone() : new AdLimitSaveData();
        adTrackingEnabled=user.adTrackingEnabled;
        adTrackingGuideShown = user.adTrackingGuideShown;
        adTrackingRewardClaimed = user.adTrackingRewardClaimed;
        // ★ 功能解锁持久标记（旧存档没有这些字段时保持默认 false / 0，交给 FeatureUnlockMigration 一次性迁移）
        unlockSchemaVersion = user.unlockSchemaVersion;
        butterflyGardenUnlocked = user.butterflyGardenUnlocked;
        timeLimitUnlocked = user.timeLimitUnlocked;
        zenRankUnlocked = user.zenRankUnlocked;
        dailyTaskUnlocked = user.dailyTaskUnlocked;
        fishRaceUnlocked = user.fishRaceUnlocked;
        MaxSinglePayAmount=user.MaxSinglePayAmount;

        // ★ 促销角标消费截止时间（旧存档没有这个字段时为 null，统一兜底成空字符串）
        shopEntryBadgeConsumedUntil = user.shopEntryBadgeConsumedUntil ?? "";

        // 同步自 WT 1.9.8：新手奖励状态
        isBeginnerBonusTriggered = user.isBeginnerBonusTriggered;
        isBeginnerBonusCompleted = user.isBeginnerBonusCompleted;
        beginnerBonusStartTime = user.beginnerBonusStartTime;
        
        // 检查并初始化缺失的生命周期事件
        InitializeLifecycleEvents();

        // 检查是否需要重置每日数据
        CheckResetDailyTime();

        // 检查是否需要上报生命周期事件
        CheckLifecycleEvents();

        CheckShopBuyData();

        // ★ 表被合并/替换后，ShopManager 的 shoplimitDatas 缓存必须跟着重建，
        //   否则它仍然指向旧对象，后续 AddBulbExposureFail 等写入会落到表外（存档看不到）。
        if (GameDataManager.Instance != null && GameDataManager.Instance.UserData != null
            && ShopManager.shopManager != null)
            ShopManager.shopManager.RefreshLimitData();
    }

    /// <summary>
    /// 合并限购跟踪表：服务器 / 读档数据只带持久字段，决策运行时字段以本地为准。
    ///
    /// ★ 以**本地为基底**（这是关键）：限购跟踪表本质是运行时数据，服务器表经常是空的
    ///   （旧包没上报 / 服务端不存这个字段）。如果反过来以服务器为结果集，
    ///   「服务器 = []、本地有 3 条」时会把本地记录整表丢掉，
    ///   直接表现就是：写了失败分 F → 一次同步后 F 归零、重开界面权重也不带惩罚。
    /// </summary>
    private static List<ShopLimitData> MergeLimitShopItems(List<ShopLimitData> local, List<ShopLimitData> remote)
    {
        if (local == null) local = new List<ShopLimitData>();

        // 服务器没有数据（最常见）→ 本地整表原样保留
        if (remote == null || remote.Count == 0)
        {
            Debug.Log($"[ShopLimit] 合并限购跟踪表: 本地={local.Count}, 服务器=空 → 保留本地");
            return local;
        }

        if (local.Count == 0)
        {
            Debug.Log($"[ShopLimit] 合并限购跟踪表: 本地=空, 服务器={remote.Count} → 采用服务器");
            return remote;
        }

        int merged = 0, added = 0;
        foreach (ShopLimitData r in remote)
        {
            if (r == null) continue;

            ShopLimitData l = local.FirstOrDefault(x => x != null && x.id == r.id
                                                        && !string.IsNullOrEmpty(x.nameid)
                                                        && x.nameid == r.nameid);
            // id 相同但 nameid 不同（历史脏数据）时退回按 nameid 找
            if (l == null && !string.IsNullOrEmpty(r.nameid))
                l = local.FirstOrDefault(x => x != null && x.nameid == r.nameid);
            if (l == null)
                l = local.FirstOrDefault(x => x != null && x.id == r.id);

            if (l == null)
            {
                // 本地没有、服务器有 → 追加（保留服务端新增记录）
                local.Add(r);
                added++;
                continue;
            }

            // ★ 运行时字段（conversionFailScore / lastFailScoreTime / payTendencyScore /
            //   exploreScore / pushStateIndex / isActivityPack）一律以本地为准，不做覆盖。
            //   只补本地空缺的持久字段。
            if (string.IsNullOrEmpty(l.limitEndTime)) l.limitEndTime = r.limitEndTime;
            if (l.buyCount < r.buyCount) l.buyCount = r.buyCount;
            if (!l.isget && r.isget) { l.isget = r.isget; l.gettime = r.gettime; }
            if (l.id <= 0) l.id = r.id;
            if (string.IsNullOrEmpty(l.nameid)) l.nameid = r.nameid;
            merged++;
        }

        Debug.Log($"[ShopLimit] 合并限购跟踪表: 本地={local.Count - added}, 服务器={remote.Count}, 命中保留={merged}, 新增={added}");
        return local;
    }

    #endregion

    #region 数据维护方法

    /// <summary>
    /// 获得关卡模式中文描述
    /// </summary>
    /// <returns></returns>
    public string GetLevelMode()
    {
        switch (levelMode)
        {
            case 1:
                return "方块消";
            case 2:
                return "禅意拼字";
            case 3:
                return "六边形";
        }

        return "六边形";
    }

    /// <summary>
    /// 添加一条购买记录（最近的在前，超出上限自动截断）。
    /// 由 GiftTable.OnPurchaseSuccess 在购买成功后调用。
    /// </summary>
    /// <param name="record">购买记录快照</param>
    public void AddPurchaseRecord(PurchaseRecord record)
    {
        if (record == null)
        {
            Debug.LogWarning("[UserData] AddPurchaseRecord: record 为空，跳过");
            return;
        }

        if (purchaseRecords == null)
            purchaseRecords = new List<PurchaseRecord>();

        // 最近的在前
        purchaseRecords.Insert(0, record);

        // 超出上限截断最旧的
        if (purchaseRecords.Count > MAX_PURCHASE_RECORDS)
        {
            int removeCount = purchaseRecords.Count - MAX_PURCHASE_RECORDS;
            purchaseRecords.RemoveRange(MAX_PURCHASE_RECORDS, removeCount);
            Debug.Log($"[UserData] 购买记录超出上限 {MAX_PURCHASE_RECORDS}，已移除 {removeCount} 条最旧记录");
        }

        Debug.Log($"[UserData] 新增购买记录: {record.produceNameId} / {record.nameText} / price={record.price} / time={record.buyTime}, 当前共 {purchaseRecords.Count} 条");

        // 立即落盘，避免进程被杀导致记录丢失
        SaveData();
    }

    /// <summary>
    /// 修正历史购买记录里被写成「分」的金额（补单写入 LocalizedPrice 造成的 ×100 遗留数据）。
    /// 参照物用记录里自带的 originalPrice（配置原价，元）：金额达到原价的 10 倍以上就判为「分」，除以 100。
    /// 幂等：本来就正确的记录（正价或折扣价，price ≤ originalPrice）不会被改动。
    /// </summary>
    /// <returns>被修正的记录条数</returns>
    public int NormalizePurchaseRecordPrices()
    {
        if (purchaseRecords == null || purchaseRecords.Count == 0) return 0;

        int fixedCount = 0;
        for (int i = 0; i < purchaseRecords.Count; i++)
        {
            PurchaseRecord record = purchaseRecords[i];
            if (record == null || record.price <= 0f || record.originalPrice <= 0f) continue;

            // 正常记录：price ≤ originalPrice（正价或折扣）；异常记录：price ≈ originalPrice × 100
            if (record.price < record.originalPrice * 10f) continue;

            float wrong = record.price;
            record.price = record.price / 100f;
            fixedCount++;
            Debug.Log($"[UserData] 购买记录金额修正(分→元): {record.produceNameId} {wrong} → {record.price}");
        }

        if (fixedCount > 0)
        {
            Debug.Log($"[UserData] 共修正 {fixedCount} 条购买记录金额");
            SaveData();
        }
        return fixedCount;
    }

    /// <summary>
    /// 清空所有购买记录（调试或用户主动清理时使用）。
    /// </summary>
    public void ClearPurchaseRecords()
    {
        if (purchaseRecords == null)
        {
            purchaseRecords = new List<PurchaseRecord>();
            return;
        }

        purchaseRecords.Clear();
        Debug.Log("[UserData] 购买记录已清空");
        SaveData();
    }

    /// <summary>
    /// 按条件删除购买记录（调试用：例如"清除永久去广告"时把对应的订单记录一起删掉，
    /// 让玩家彻底回到没买过该商品的状态）。
    /// 注意：删掉记录后，平台已购查询就失去了去重依据，
    /// 所以必须配合 `ShopManager` 的补单拦截一起用，否则下次启动会被补发回来。
    /// </summary>
    /// <returns>被删除的记录条数</returns>
    public int RemovePurchaseRecords(Predicate<PurchaseRecord> match)
    {
        if (match == null) return 0;

        if (purchaseRecords == null)
        {
            purchaseRecords = new List<PurchaseRecord>();
            return 0;
        }

        int removed = purchaseRecords.RemoveAll(match);
        if (removed <= 0) return 0;

        Debug.Log($"[UserData] 已删除 {removed} 条购买记录，剩余 {purchaseRecords.Count} 条");
        SaveData();
        return removed;
    }

    /// <summary>
    /// 检查并重置每日限时数据
    /// </summary>
    public void CheckResetDailyTime()
    {
        if (string.IsNullOrEmpty(logoutTime)) return;

        DateTime desTime = DateTime.Parse(logoutTime);
        DateTime offTime = new DateTime(desTime.Year, desTime.Month, desTime.Day, 0, 0, 0);
        DateTime nowTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0);

        if ((nowTime - offTime).TotalDays >= 1)
        {
            // 超过一天的逻辑
            ResetDailyData();

            ResetDailyTaskDate();

            UpdatePanelUI();
        }
    }

    private async Task ResetDailyTaskDate()
    {
        await Task.Delay(10); // 等待2秒

        butterflyTaskIsOpen = false;
        completeTaskList = new List<CompleteTaskData>();
        taskButterflyUseMinutes = 0;
        butterflyTaskOpenTime = null;
        taskSaveDatas = new List<TaskSaveData>();
        isAllCompleteTask = false;
        //每日任务重置
        DailyTaskManager.Instance.GetTaskSaveData();
        DailyTaskManager.Instance.isResetDailyTask = true;
    }

    /// 重置每日数据
    /// </summary>
    private void ResetDailyData()
    {
        //限时数据
        timerePuzzleid = 0;
        limitMinPeriod = 0;
        limitEndTime = null;
        timePuzzlecount = 0;
        isDayEnterLimint = true;
        luckyLoopEntered = false;   // 活动刷新（跨天）后幸运循环一并重置
        //每日通过数据
        dayPassStageCount = 0;
        chessdayPassStageCount = 0;
        // 可在此添加其他需要每日重置的数据
        // 👇 新增：每日重置首关插屏判定状态
        isDayFirstLevelAdChecked = false;
        isDayFirstLevelAdAllowed = false;
        
        isDayFreeGet = false;
        isDayGoldBuy = false;
        isDayMoneyBuy = false;
        isHideShopRedPoint = false;
        DayRewardVideoGiftCount = 0;
        DayInterstitialGiftCount = 0;

        // ★ 广告限次：按自然日刷新的两项（商店金币视频 C / 分享 F）清零；
        //   提示/复活（A/B）按关卡刷新，不在这里清。
        if (adLimit == null) adLimit = new AdLimitSaveData();
        adLimit.shopGoldDate = "";
        adLimit.shopGoldUsed = 0;
        adLimit.shareDate = "";
        adLimit.shareUsed = 0;

        // ★ 注意：shopEntryBadgeConsumedUntil 不在每日重置里清空。
        //   它的清空时机是"本次节日/周末结束"，由 ShopManager.EnsureBadgeConsumedResetIfHolidayEnded() 判断。
        //   这样可以保证：周末进店消费后，跨天到周日仍保持隐藏，直到周一才自动清空。
        // ★ 注意：purchaseRecords 不在每日重置里清空，它是"历史购买记录"，长期保留。
    }

    private void UpdatePanelUI()
    {
        if (SystemManager.Instance != null)
        {
            if (SystemManager.Instance.PanelIsShowing(PanelType.LimitTimeScreen))
                SystemManager.Instance.HidePanel(PanelType.LimitTimeScreen);

            if (SystemManager.Instance.PanelIsShowing(PanelType.SevenSignScreen))
            {
                SystemManager.Instance.HidePanel(PanelType.SevenSignScreen);
            }

            if (SystemManager.Instance.PanelIsShowing(PanelType.DailyTasksScreen))
                SystemManager.Instance.HidePanel(PanelType.DailyTasksScreen);

        }
    }


    public void CheckShopBuyData()
    {
        foreach (ShopLimitData shopdata in limitShopItems)
        {
            if (shopdata.isopen)
            {
                DateTime getendtime = DateTime.Parse(shopdata.endtime);
                TimeSpan timeSpan = getendtime.Subtract(DateTime.Now);

                if (timeSpan.TotalMinutes <= 0)
                {
                    shopdata.isopen = false;
                    shopdata.endtime = null;
                }
            }

            if (shopdata.isget && shopdata.adstype == (int)LimitRewordType.RemoveDayAds)
            {
                // ★ 一次性修复：购买时把设备时间调到未来 → gettime / endtime 都落在未来，
                //   设备时间调回真实时间后剩余时长会凭空变长（线上反馈的「7 天周卡变成 286时55分」）。
                //   这里把整条记录按「gettime 超出当前时间的量」整体平移回真实时间轴，总时长不变。
                RepairFuturePurchasedRemoveAds(shopdata);

                // ★ bug64 修复：以前固定用「gettime + 7 天」判过期，只要玩家买到的不是 7 天（或中途续过期），
                //   就会被误标 isoverdate；重启补单时补单逻辑看到「已过期」就按 Now + 时长重新计时，
                //   玩家剩余的去广告时长被直接覆盖。
                //   现在一律以权益真实结束时间 endtime 为准；只有 endtime 缺失的老存档，才退回 gettime + 7 天（历史默认时长）。
                DateTime adEndTime = shopdata.GetEndTime();
                if (adEndTime == DateTime.MinValue && !string.IsNullOrEmpty(shopdata.gettime))
                {
                    DateTime getTime = shopdata.GetGetTime();
                    if (getTime != DateTime.MinValue)
                        adEndTime = getTime.AddHours(24 * 7);
                }

                if (adEndTime != DateTime.MinValue)
                {
                    // ★ 用 GameTime.Now：设备时间被回拨后不能让权益"续命"
                    bool expired = (adEndTime - GameTime.Now).TotalMinutes <= 0;
                    if (expired)
                    {
                        shopdata.isoverdate = true;
                    }
                    else if (shopdata.isoverdate)
                    {
                        // 未到期却带着过期标记（旧版本误标）→ 纠正回来，否则权益永远失效
                        shopdata.isoverdate = false;
                        Debug.Log($"[ShopLimit] 限时去广告过期标记纠正: 结束={adEndTime:yyyy-MM-dd HH:mm:ss}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// 小于该容差的「未来时间」视为正常校时抖动，不做修复。
    /// 权益时间已改为以服务器时间为准（<see cref="GameTime"/>），且国内单时区、不做时区换算，
    /// 正常记录不可能出现 gettime 比当前时间还晚 2 小时以上 —— 只看"超前量是不是天级"即可。
    /// </summary>
    private const int FuturePurchaseRepairToleranceMinutes = 2 * 60;

    /// <summary>
    /// 修复「用未来时间购买」的限时去广告记录。
    ///
    /// 判定：记录的 gettime 比当前时间还晚（超过容差）—— 正规购买不可能出现。
    /// 处理：把 gettime / endtime 同时减去这段超前量（总时长 endtime-gettime 不变），
    ///       平移后 gettime == now，不会重复触发。
    ///
    /// 注：这里必须用 <see cref="GameTime.Now"/> 而不是 DateTime.Now —— 已进入"修正后时间轴"的记录
    ///     （gettime 本身已经带 offset）用原始时间比较会被判成"未来购买"，从而被误缩短。
    /// </summary>
    private void RepairFuturePurchasedRemoveAds(ShopLimitData shopdata)
    {
        DateTime getTime = shopdata.GetGetTime();
        if (getTime == DateTime.MinValue) return;

        TimeSpan ahead = getTime - GameTime.Now;
        if (ahead.TotalMinutes <= FuturePurchaseRepairToleranceMinutes) return;

        DateTime oldEnd = shopdata.GetEndTime();
        if (oldEnd != DateTime.MinValue)
            shopdata.SetEndTime(oldEnd - ahead);
        shopdata.SetGetTime(getTime - ahead);

        // 修完立刻按新时间轴重算过期标记
        DateTime fixedEnd = shopdata.GetEndTime();
        if (fixedEnd != DateTime.MinValue && (fixedEnd - GameTime.Now).TotalMinutes <= 0)
            shopdata.isoverdate = true;

        Debug.LogWarning($"[ShopLimit] 检测到用未来时间购买的去广告记录，已修正 {ahead.TotalHours:F2} 小时：" +
                         $"购买时间 {getTime:yyyy-MM-dd HH:mm:ss} → {shopdata.GetGetTime():yyyy-MM-dd HH:mm:ss}，" +
                         $"结束时间 {oldEnd:yyyy-MM-dd HH:mm:ss} → {fixedEnd:yyyy-MM-dd HH:mm:ss}");
    }

    public float GetBestClearTime(int wordCount)
    {
        if (BestClearTimes != null && BestClearTimes.TryGetValue(wordCount, out float time))
            return time;
        return 0f;
    }

    public void SetBestClearTime(int wordCount, float time)
    {
        if (BestClearTimes == null) BestClearTimes = new Dictionary<int, float>();
        BestClearTimes[wordCount] = time;
    }

    #endregion

    #region 数据持久化方法


    /// <summary>
    /// 保存用户数据
    /// </summary>
    public void SaveData()
    {
        try
        {
            if (CurrentHexStage <= 0) return;

            // 更新登出时间
            if (!string.IsNullOrEmpty(logoutTime) && DateTime.Now > DateTime.Parse(logoutTime))
            {
                logoutTime = DateTime.Now.ToString();
            }

            // 更新在线时长
            UpdateOnlineStageTime();

            // 序列化并加密数据
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            string encryptedJson = SecurityProvider.ProtectData(json);

            // 写入文件
            File.WriteAllText(Getfilepath, encryptedJson);
            Debug.Log("用户数据保存成功");


        }
        catch (Exception ex)
        {
            Debug.LogError($"保存用户数据失败: {ex.Message}");
        }
    }

    #endregion

    #region 游戏数据操作方法
    
    
    /// <summary>
    /// 直接新增指定的头像ID（带有效性校验）
    /// </summary>
    public bool AddHeadIcon(int animalId)
    {
        // 校验1：ID必须在 25~39 范围内
        if (animalId < 25 || animalId > 39)
        {
            Debug.Log($"错误：头像ID {animalId} 超出有效范围(25~{39})");
            return false;
        }

        // 校验2：不能重复拥有（避免列表出现重复数据）
        if (_getAnimalsHeadIcons.Contains(animalId))
        {
            Debug.Log($"提示：头像ID {animalId} 已经拥有，无需重复添加");
            return false;
        }
        

        // 执行添加
        _getAnimalsHeadIcons.Add(animalId);
        
        GameDataManager.Instance.UserData.isGetNewHeadIcon=true;
        return true;
    }
    
    /// <summary>
    /// 直接新增指定的头像框ID（带有效性校验）
    /// </summary>
    public bool AddHeadBorderIcon(int borderId)
    {
        // 不能重复拥有（避免列表出现重复数据）
        if (_getHeadBorderIcons.Contains(borderId))
        {
            Debug.Log($"提示：头像框ID {borderId} 已经拥有，无需重复添加");
            return false;
        }

        // 执行添加
        _getHeadBorderIcons.Add(borderId);
        GameDataManager.Instance.UserData.isGetNewHeadBorderIcon = false;
        return true;
    }

    public bool TryGetRandomUnlockedAnimal(out int animalId)
    {
        //25表示起始id，15表示数量
        var allIds = Enumerable.Range(25, 15);
        var ownedSet = new HashSet<int>(_getAnimalsHeadIcons);
        var available = allIds.Where(id => !ownedSet.Contains(id)).ToList();

        if (available.Count == 0)
        {
            animalId = 0;   // 无效ID
            return false;
        }

        int index = _random.Next(available.Count);
        animalId = available[index];
        return true;
    }


    /// <summary>
    /// 更新指定主题使用次数
    /// </summary>
    public void UpdateThemeUseTimes(int themeid)
    {
        if (ThemeItemUses.Keys.Contains(themeid))
        {
            ThemeItemUses[themeid]++;
        }
        else
        {
            ThemeItemUses.Add(themeid, 1);
        }
    }


    /// <summary>
    /// 更新金箔数量
    /// </summary>
    /// <param name="reasonIndex">resource_change.reason_index（不传则按 message 关键字推断）</param>
    /// <param name="reasonCategory">resource_change.change_reason（不传则按 message 关键字推断）</param>
    public void UpdateGoldLeaf(int value, string message = "", string reasonIndex = "", string reasonCategory = "")
    {
        GoldLeaf += value;

        if (value <= 0)
        {
            SendCurrencyEvent(value, "金箔", message, string.Empty, reasonIndex, reasonCategory);
        }
        else
        {
            SendCurrencyEvent(value, "金箔", message, string.Empty, reasonIndex, reasonCategory);
        }

        Debug.Log($"金箔{(value > 0 ? "增加" : "减少")}: {Math.Abs(value)}, 当前金箔: {GoldLeaf}");

        // 金箔数量变化后，重新检查皮肤入口红点状态
        ThemeManager.Instance.CheckAndUpdateSkinRedPoint();
    }

    /// <summary>
    /// 获得道具消耗总数
    /// </summary>
    public int GetTotalToolCost()
    {
        int totalToolCost = 0;
        totalToolCost += toolInfo[102].reducecount + toolInfo[104].reducecount
                                                   + toolInfo[103].reducecount;
        return totalToolCost;
    }

    // 单段计时上限：防止用户篡改系统时间导致关卡时长出现天文数字
    private const long MaxSingleSegmentMs = 30 * 60 * 1000;

    /// <summary>
    /// 校验计时窗口起点是否可信。
    /// 无效情形：起点在未来（系统时间被回拨）、起点距今超过单段上限（旧存档里存的是 UTC ticks、
    /// 或长时间挂机未封存）。无效窗口直接丢弃，不参与累计。
    /// </summary>
    private bool IsStageWindowValid(long nowTicks)
    {
        if (curStageWindowStartTicks <= 0) return false;

        long deltaMs = (nowTicks - curStageWindowStartTicks) / TimeSpan.TicksPerMillisecond;
        return deltaMs >= 0 && deltaMs <= MaxSingleSegmentMs;
    }

    /// <summary>
    /// 确保计时窗口可用：起点异常时丢弃该段、以此刻重开窗口，不把异常时长计入本局。
    /// </summary>
    private void EnsureStageWindow(long nowTicks)
    {
        if (IsStageWindowValid(nowTicks)) return;

        long badStartTicks = curStageWindowStartTicks;
        curStageWindowStartTicks = nowTicks;

        // badStartTicks > 0 说明确实是"有窗口但不可信"，此时打日志便于排查（重开后下一帧即合法，不会刷屏）
        if (badStartTicks > 0)
        {
            long deltaMs = (nowTicks - badStartTicks) / TimeSpan.TicksPerMillisecond;
            string startText = new DateTime(badStartTicks).ToString("yyyy-MM-dd HH:mm:ss");
            Debug.Log($"[关卡计时] 窗口起点异常，已重置且不累加：起点={startText}，偏差={deltaMs / 1000f:F1}s（上限{MaxSingleSegmentMs / 1000}s）");
        }
    }

    /// <summary>
    /// 新开一局：累计清零并开窗
    /// </summary>
    public void BeginStageTime()
    {
        curStageElapsedMs = 0;
        curStageOnlineTime = 0;
        curStageStartTime = DateTime.Now.ToString();
        curStageWindowStartTicks = DateTime.Now.Ticks;
        curStageTimerActive = true;
        curStageSessionOpen = true;
    }

    /// <summary>
    /// 回到关卡（从主页/商店/蝴蝶页重新进本关）：保留已累计时长，重新开启计时会话。
    /// </summary>
    public void ReopenStageTime()
    {
        curStageSessionOpen = true;
        long nowTicks = DateTime.Now.Ticks;
        // 窗口起点异常（旧存档 UTC ticks / 系统时间回拨 / 超单段上限）→ 视为无效窗口，直接以此刻重开
        EnsureStageWindow(nowTicks);
        if (string.IsNullOrEmpty(curStageStartTime))
            curStageStartTime = DateTime.Now.ToString();
        curStageTimerActive = true;
    }

    /// <summary>
    /// 续局 / 从暂停恢复：只开窗，保留已累计时长。
    /// 未处于本局计时会话时不做事，避免在非关卡界面误开计时。
    /// </summary>
    public void ResumeStageTime()
    {
        if (!curStageSessionOpen) return;
        long nowTicks = DateTime.Now.Ticks;
        // 同上：异常窗口丢弃，不累加，避免旧存档 UTC ticks 造成 +时区差 的假时长
        EnsureStageWindow(nowTicks);
        if (string.IsNullOrEmpty(curStageStartTime))
            curStageStartTime = DateTime.Now.ToString();
        curStageTimerActive = true;
    }

    /// <summary>
    /// 封存当前段，并把窗口滚动到此刻。可反复调用、幂等。
    /// 注意：这里必须"滚动"而不是置空，否则计时窗口会被永久关闭。
    /// </summary>
    public void SealStageTime()
    {
        if (curStageWindowStartTicks > 0)
        {
            long nowTicks = DateTime.Now.Ticks;
            long deltaMs = (nowTicks - curStageWindowStartTicks) / TimeSpan.TicksPerMillisecond;
            // 只累加合法区间：负数（时间回拨）与超单段上限（旧存档 UTC ticks / 长时间挂机）都不计入
            if (deltaMs > 0 && deltaMs <= MaxSingleSegmentMs)
                curStageElapsedMs += deltaMs;
            curStageWindowStartTicks = nowTicks;
        }
        curStageOnlineTime = (int)(curStageElapsedMs / 1000);
    }

    /// <summary>
    /// 暂停计时（暂停页 / 商店 / 广告 / 切后台）：封存当前段并关窗，这段时长不计入关卡。
    /// </summary>
    public void PauseStageTime()
    {
        if (!curStageTimerActive) return;
        SealStageTime();
        curStageWindowStartTicks = 0;
        curStageTimerActive = false;
    }

    /// <summary>
    /// 退出 / 结算：封存后彻底结束本次计时会话
    /// </summary>
    public void EndStageTime()
    {
        if (curStageSessionOpen)
            SealStageTime();
        curStageWindowStartTicks = 0;
        curStageTimerActive = false;
        curStageSessionOpen = false;
    }

    /// <summary>
    /// 按"当前是否被弹窗/广告暂停"同步计时窗口状态。
    /// 由玩法层每帧调用，暂停期间不计时，恢复后从当前时刻继续累计。
    /// </summary>
    public void SyncStageTime(bool paused)
    {
        if (!curStageSessionOpen) return;
        if (paused) PauseStageTime();
        else ResumeStageTime();
    }

    /// <summary>
    /// 实时读取本局累计毫秒，不改变状态
    /// </summary>
    public long GetStageElapsedMs()
    {
        long ms = curStageElapsedMs;
        if (curStageWindowStartTicks > 0)
        {
            long deltaMs = (DateTime.Now.Ticks - curStageWindowStartTicks) / TimeSpan.TicksPerMillisecond;
            // 与 SealStageTime 口径一致：异常窗口不计入，避免读到"假"时长
            if (deltaMs > 0 && deltaMs <= MaxSingleSegmentMs)
                ms += deltaMs;
        }
        return ms;
    }

    /// <summary>
    /// 结算专用：先取值再封存，避免同一段被重复累加
    /// </summary>
    public float SealAndGetStageSeconds()
    {
        float seconds = GetStageElapsedMs() / 1000f;
        SealStageTime();
        return seconds;
    }

    /// <summary>
    /// 兼容旧调用点：语义统一为"滚动封存"
    /// </summary>
    public void UpdateOnlineStageTime()
    {
        SealStageTime();
    }

    /// <summary>
    /// 更新关卡用时
    /// </summary>
    public void UpdateLevelUseTimes(int level, int secondtimes)
    {
        if (!passLevelUseTime.ContainsKey(level))
        {
            passLevelUseTime.Add(level, secondtimes);
        }

        if (passLevelUseTime.Count > 9)
        {
            var recentEntries = passLevelUseTime.OrderByDescending(x => x.Key).Take(9).ToList();

            passLevelUseTime.Clear();

            foreach (var entry in recentEntries)
            {
                passLevelUseTime[entry.Key] = entry.Value;
            }
        }
    }


    /// <summary>
    /// 更新关卡进度
    /// </summary>
    public void UpdateChessStage(int value = 1, bool isSet = false)
    {
        CurrentChessStage = isSet ? value : CurrentChessStage + value;
        Debug.Log($"关卡更新: {(isSet ? "设置为" : "增加")}{value}, 当前关卡: {CurrentChessStage}");
    }

    public void UdpateTimePuzzleCount(int value)
    {
        timePuzzlecount += value;
    }

    /// <summary>
    /// 新增任务数据
    /// </summary>
    public void UpdateDailyTaskData(TaskSaveData taskSave)
    {
        taskSaveDatas.Add(taskSave);
    }


    /// <summary>
    /// 更新六边形关卡进度
    /// </summary>
    public void UpdateHexStage(int value = 1, bool isSet = false)
    {
        CurrentHexStage = isSet ? value : CurrentHexStage + value;
    }

    public void UpdateTutorialProgress()
    {
        TutorialProgress += 1;
    }

    /// <summary>
    /// 更新金币数量
    /// </summary>
    public void UpdateGold(int value, bool isanim = false, bool updateui = true, string message = "",
        string reasonIndex = "", string reasonCategory = "")
    {
        Gold += value;

        if (updateui)
        {
            EventDispatcher.instance.TriggerChangeGoldUI(value, isanim);
        }


        if (value <= 0)
        {
            TotalConsumedGold += Math.Abs(value);
            SendCurrencyEvent(value, "金币", message, string.Empty, reasonIndex, reasonCategory);
        }
        else
        {
            TotalEarnedGold += value;
            SendCurrencyEvent(value, "金币", message, string.Empty, reasonIndex, reasonCategory);
        }

        GameDataManager.Instance.CommitGameData();
    }

    /// <summary>
    /// 发送货币事件（用于统计）
    /// </summary>
    public void SendCurrencyEvent(int value, string currencyName, string message = "", string word = "",
        string reasonIndex = "", string reasonCategory = "")
    {
        AnalyticMgr.SetCommonProperties();
        if (value <= 0)
        {
            AnalyticMgr.ResourceReduce(currencyName, Mathf.Abs(value), message, word, reasonIndex, reasonCategory);
        }
        else
        {
            AnalyticMgr.ResourceGet(currencyName, value, message, word, reasonIndex, reasonCategory);
        }
    }

    /// <summary>
    /// 更新完成任务列表
    /// </summary>
    public void UpdateCompleteTask(int taskid, int typeid)
    {
        completeTaskList.Add(new CompleteTaskData()
        {
            taskid = taskid,
            typeid = typeid
        });
    }

    /// <summary>
    /// 更新所有任务完成数据
    /// </summary>
    public void UpdateAllCompleteTask()
    {
        isAllCompleteTask = true;
    }

    /// <summary>
    /// 更新限时活动进度id
    /// </summary>
    public void UpdateLImitid()
    {
        timerePuzzleid++;
        LimitTimeManager.Instance.GetCurWordCount();
        if (string.IsNullOrEmpty(limitOpenTime)) limitOpenTime = DateTime.Now.ToString();
        TimeSpan ts = DateTime.Now.Subtract(DateTime.Parse(limitOpenTime));
        AnalyticMgr.ActivityProgress("限时活动", timerePuzzleid, (int)ts.TotalSeconds);
        if (timerePuzzleid > 10)
        {
            AnalyticMgr.ActivityComplete("限时活动", (int)ts.TotalSeconds);
        }
    }

    /// <summary>
    /// 每日首次开启限时活动
    /// </summary>
    public void EveryDayOpenLimit()
    {
        limitOpenTime = DateTime.Now.ToString();
        isDayEnterLimint = false;
    }

    /// <summary>
    /// 更新限时翻译结束时间
    /// </summary>
    public void UpdateLimitEndTime(int minutes)
    {
        limitEndTime = DateTime.Now.AddMinutes(minutes).ToString();
        UpdatelimitMinPeriod(minutes);
    }

    /// <summary>
    /// 更新限时翻倍周期
    /// </summary>
    public void UpdatelimitMinPeriod(int minutes)
    {
        limitMinPeriod = minutes;
    }

    /// <summary>
    /// 更新道具数量
    /// </summary>
    public void UpdateTool(LimitRewordType type, int value, string message = "", string word = "",
        string reasonIndex = "", string reasonCategory = "")
    {
        int toolId = GetToolIdByType(type);

        if (toolInfo.ContainsKey(toolId))
        {
            toolInfo[toolId].count += value;
            Debug.Log($"{type}道具{(value > 0 ? "增加" : "减少")}: {Math.Abs(value)}, 当前数量: {toolInfo[toolId].count}");
            if (value > 0)
            {
                toolInfo[toolId].addcount += value;
            }
            else
            {
                toolInfo[toolId].reducecount += Mathf.Abs(value);
            }

            string toolName = null;

            switch (type)
            {
                case LimitRewordType.ReStartTool:
                    toolName = "复活道具";
                    break;
                case LimitRewordType.Tipstool:
                    toolName = "提示道具";
                    break;
                case LimitRewordType.Butterfly:
                    toolName = "蝴蝶道具";
                    break;
                case LimitRewordType.AutoComplete:
                    toolName = "火箭道具";
                    break;
            }

            // 发送道具统计事件
            SendCurrencyEvent(value, toolName, message, word, reasonIndex, reasonCategory);
        }
    }


    /// <summary>
    /// 根据道具类型获取道具ID
    ///   101 = SignleHint（单字提示，旧版已合并到 102）
    ///   102 = WordHint（词语提示/放大镜）
    ///   103 = Butterfly（蝴蝶）
    ///   104 = AutoComplete（火箭/自动完成）
    ///   105 = Restart（复活道具 ⏰）
    /// </summary>
    private int GetToolIdByType(LimitRewordType type)
    {
        return type switch
        {
            LimitRewordType.Tipstool => 102,
            LimitRewordType.Butterfly => 103,
            LimitRewordType.AutoComplete => 104,
            LimitRewordType.ReStartTool => 105,     // ★ 新增：复活道具 105
            _ => 0
        };
    }

    #endregion

    #region 生命周期事件管理

    /// <summary>
    /// 初始化生命周期事件数据结构
    /// </summary>
    private void InitializeLifecycleEvents()
    {
        if (ReportedLifecycleEvents == null)
        {
            ReportedLifecycleEvents = new Dictionary<string, bool>();
        }

        // 确保所有生命周期事件都在字典中
        foreach (var minutes in LIFE_CYCLE_MINUTES)
        {
            string eventKey = $"{LIFE_CYCLE_EVENT_PREFIX}{minutes}";
            if (!ReportedLifecycleEvents.ContainsKey(eventKey))
            {
                ReportedLifecycleEvents[eventKey] = false;
            }
        }
    }

    /// <summary>
    /// 增加在线时长并检查生命周期事件
    /// </summary>
    public void AddOnlineMinutes(float minutes)
    {
        TotalOnlineMinutes += minutes;
        CheckLifecycleEvents();

        Debug.Log($"累计在线时长增加: {minutes}分钟, 总时长: {TotalOnlineMinutes:F1}分钟");
    }

    /// <summary>
    /// 检查并上报生命周期事件
    /// </summary>
    private void CheckLifecycleEvents()
    {
        for (int i = 0; i < LIFE_CYCLE_MINUTES.Length; i++)
        {
            int targetMinutes = LIFE_CYCLE_MINUTES[i];
            string eventKey = $"{LIFE_CYCLE_EVENT_PREFIX}{targetMinutes}";

            // 如果达到目标时长且未上报过
            if (TotalOnlineMinutes >= targetMinutes &&
                (!ReportedLifecycleEvents.ContainsKey(eventKey) || !ReportedLifecycleEvents[eventKey]))
            {
                AnalyticMgr.ReportLifecycleEvent(i + 1, targetMinutes);
                ReportedLifecycleEvents[eventKey] = true;
            }
        }
    }

    public (int minutes, string eventName) GetNextLifecycleEvent()
    {
        foreach (var minutes in LIFE_CYCLE_MINUTES)
        {
            string eventKey = $"{LIFE_CYCLE_EVENT_PREFIX}{minutes}";
            if (!ReportedLifecycleEvents.ContainsKey(eventKey) || !ReportedLifecycleEvents[eventKey])
            {
                int index = Array.IndexOf(LIFE_CYCLE_MINUTES, minutes) + 1;
                return (minutes, $"{LIFE_CYCLE_EVENT_PREFIX}{index}");
            }
        }

        return (0, "已完成所有生命周期事件");
    }

    public int GetCompletedLifecycleEventCount()
    {
        int count = 0;
        foreach (var eventKey in ReportedLifecycleEvents.Keys)
        {
            if (ReportedLifecycleEvents[eventKey])
            {
                count++;
            }
        }

        return count;
    }

    public float GetLifecycleEventProgress()
    {
        int completed = GetCompletedLifecycleEventCount();
        return (float)completed / LIFE_CYCLE_MINUTES.Length;
    }

    public void ResetLifecycleEvents()
    {
        foreach (var minutes in LIFE_CYCLE_MINUTES)
        {
            string eventKey = $"{LIFE_CYCLE_EVENT_PREFIX}{minutes}";
            ReportedLifecycleEvents[eventKey] = false;
        }

        TotalOnlineMinutes = 0f;
        SaveData();

        Debug.Log("已重置所有生命周期事件");
    }

    #endregion

    #region 词库相关方法

    public void AddStagePuzzle(string Puzzle)
    {
        WordVocabulary<string> vocabulary = GetWordVocabulary();
        if (!vocabulary.LevelWords.Contains(Puzzle))
        {
            vocabulary.LevelWords.Insert(0, Puzzle);
        }
    }

    public void AddNoteBook(string Puzzle)
    {
        WordVocabulary<string> vocabulary = GetWordVocabulary();
        if (!vocabulary.UserNotes.Contains(Puzzle))
        {
            vocabulary.UserNotes.Insert(0, Puzzle);

            if (!isShowVocabulary)
            {
                isShowVocabulary = true;
            }

            // 只在“新增收藏”时上报，避免详情页重复赋值 Toggle 触发重复事件
            AnalyticMgr.CollectionWord(Puzzle);
        }
    }

    public void RemoveNoteBook(string Puzzle)
    {
        WordVocabulary<string> vocabulary = GetWordVocabulary();
        if (vocabulary.UserNotes.Contains(Puzzle))
        {
            vocabulary.UserNotes.Remove(Puzzle);
        }
    }

    public WordVocabulary<string> GetWordVocabulary()
    {
        return wordVocabularyChinSim;
    }

    private string GetVocabularyKey()
    {
        return LanguageCode switch
        {
            "English" => "JanVocabulary",
            "ChineseTraditional" => "ChinTraVocabulary",
            "ChineseSimplified" => "ChinSimVocabulary",
            _ => "JanVocabulary"
        };
    }


    public void ClearPuzzleVocabulary()
    {
        WordVocabulary<string> vocabulary = GetWordVocabulary();
        vocabulary.LevelWords.Clear();
    }

    #endregion
    

    [JsonIgnore] public const int MAX_NATURAL_ENERGY = 5;
    [JsonIgnore] public const int ENERGY_REGEN_MINUTES = 30;

    public void CalculateEnergyRegen()
    {
        if (Energy >= MAX_NATURAL_ENERGY)
        {
            LastEnergyUpdateTime = DateTime.Now.ToString();
            return;
        }

        if (string.IsNullOrEmpty(LastEnergyUpdateTime))
        {
            LastEnergyUpdateTime = DateTime.Now.ToString();
            return;
        }

        DateTime lastTime = DateTime.Parse(LastEnergyUpdateTime);
        TimeSpan passedTime = DateTime.Now - lastTime;

        int recoveredPoints = (int)(passedTime.TotalMinutes / ENERGY_REGEN_MINUTES);
        if (recoveredPoints > 0)
        {
            Energy += recoveredPoints;

            if (Energy >= MAX_NATURAL_ENERGY)
            {
                Energy = MAX_NATURAL_ENERGY;
                LastEnergyUpdateTime = DateTime.Now.ToString();
            }
            else
            {
                LastEnergyUpdateTime = lastTime.AddMinutes(recoveredPoints * ENERGY_REGEN_MINUTES).ToString();
            }

            SendCurrencyEvent(recoveredPoints, "体力", "时间自然恢复");
        }
    }
    
    public bool HasUnlimitedEnergy()
    {
        if (string.IsNullOrEmpty(unlimitedEnergyEndTime)) return false;
        DateTime end;
        if (!DateTime.TryParse(unlimitedEnergyEndTime, out end)) return false;
        return end > DateTime.Now;
    }

    public double GetUnlimitedEnergyRemainSeconds()
    {
        if (string.IsNullOrEmpty(unlimitedEnergyEndTime)) return 0;
        DateTime end;
        if (!DateTime.TryParse(unlimitedEnergyEndTime, out end)) return 0;
        double remain = (end - DateTime.Now).TotalSeconds;
        return remain > 0 ? remain : 0;
    }

    public DateTime AddUnlimitedEnergy(int hours)
    {
        if (hours <= 0) return GetUnlimitedEnergyEndTime();

        DateTime baseTime = DateTime.Now;
        DateTime oldEnd;
        if (!string.IsNullOrEmpty(unlimitedEnergyEndTime)
            && DateTime.TryParse(unlimitedEnergyEndTime, out oldEnd)
            && oldEnd > DateTime.Now)
        {
            baseTime = oldEnd;
        }

        DateTime newEnd = baseTime.AddHours(hours);
        unlimitedEnergyEndTime = newEnd.ToString("yyyy-MM-dd HH:mm:ss");
        return newEnd;
    }

    public DateTime GetUnlimitedEnergyEndTime()
    {
        if (string.IsNullOrEmpty(unlimitedEnergyEndTime)) return DateTime.MinValue;
        DateTime end;
        return DateTime.TryParse(unlimitedEnergyEndTime, out end) ? end : DateTime.MinValue;
    }

    public bool ConsumeEnergy(int stageId, int amount = 1, string message = "进入关卡消耗")
    {
        if (stageId == 1) return true;

        // ★ 无限体力期间不扣体力（进入 / 退出 / 进程中断惩罚都不扣）
        if (HasUnlimitedEnergy())
        {
            Debug.Log($"[Energy] 无限体力期间，跳过体力扣除：{message} (-{amount})");
            return true;
        }

        if (Energy >= amount)
        {
            bool wasFull = Energy >= MAX_NATURAL_ENERGY;
            Energy -= amount;

            if (wasFull && Energy < MAX_NATURAL_ENERGY)
            {
                LastEnergyUpdateTime = DateTime.Now.ToString();
            }

            SendCurrencyEvent(-amount, "体力", message);
            return true;
        }

        return false;
    }

    public void AddBonusEnergy(int amount, string message = "奖励获取")
    {
        Energy += amount;
        SendCurrencyEvent(amount, "体力", message,"",message);
    }

    public string GetEnergyDisplayString()
    {
        if (CurrentChessStage == 1) return "充足";
        return Energy.ToString();
    }

    public int GetNextEnergyRegenSeconds()
    {
        if (Energy >= MAX_NATURAL_ENERGY) return 0;

        DateTime lastTime = DateTime.Parse(LastEnergyUpdateTime);
        DateTime nextRegenTime = lastTime.AddMinutes(ENERGY_REGEN_MINUTES);
        return Mathf.Max(0, (int)(nextRegenTime - DateTime.Now).TotalSeconds);
    }

    #region 修改名称

    public bool CanShowCharInfoPopup()
    {
        if (!string.IsNullOrEmpty(UserName))
            return false;

        if (charInfoPopupCount >= 3)
            return false;

        if (string.IsNullOrEmpty(charInfoPopupLastTime))
            return true;

        if (DateTime.TryParse(charInfoPopupLastTime, out var lastTime))
        {
            return (DateTime.Now - lastTime).TotalHours >= 72;
        }

        return true;
    }

    public void MarkCharInfoPopupShown()
    {
        charInfoPopupCount++;
        charInfoPopupLastTime = DateTime.Now.ToString("o");
        SaveData();
    }


    #endregion

    #region 辅助方法

    private string GetLanguage()
    {
        string defaultLanguage = AppGameSettings.SystemLanguage;
        Debug.Log($"当前语言设置: {defaultLanguage}");
        return defaultLanguage;
    }

    public void ClearAllData()
    {
        InitData();
    }

    #endregion
}