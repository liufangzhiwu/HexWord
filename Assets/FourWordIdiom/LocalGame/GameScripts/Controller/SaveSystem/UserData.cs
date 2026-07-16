using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// 用户游戏数据管理类
/// 负责处理用户数据的加载、保存、初始化及日常管理
/// 使用JSON序列化和加密存储用户数据
/// </summary>
public class UserData
{
    #region 用户基础数据

    public string PlayerId; // 玩家ID
    public string ABName; // AB测试包名
    public string UserName;
    public int UserHeadId;
    public string UserId; // 用户唯一标识
    public int Gold; // 当前金币数量
    public int TotalConsumedGold; // 历史累计消耗金币
    public int TotalEarnedGold; // 历史累计获得金币
    public int CurrentHexStage; // 当前六边形关卡进度

    public int CurrentChessStage; // 当前拼字关卡进度
    public int levelMode; // 当前游戏模式 1:普通模式 2:拼字模式 3:六边形模式
    public int dayPassStageCount; // 每日通关数量
    public int showRateusCount; // 好评界面显示次数
    public string showRateusTime; // 好评界面显示时间
    public bool isChangeUserName; // 是否更改过用户名称
    public string Zenlevel; // 禅修榜等级

    // 👇 新增：体力系统基础字段
    public int Energy; // 当前体力值
    public string LastEnergyUpdateTime; // 上次体力恢复的结算时间
    public bool hasUsedFreeRevive = false;
    public int charInfoPopupCount; // 已弹出次数
    public string charInfoPopupLastTime; // 上次弹出时间

    public int GoldLeaf; // 金箔数量
    public List<ThemeSaveItem> ThemeSaveItems = new List<ThemeSaveItem>();
    public Dictionary<int, int> ThemeItemUses; // 单个主题累计使用次数
    public int userthemeid = 0;
    public bool ischangetheme = false;

    // 👇 新增：禅修榜是否主动加入标识（旧版本读取不到会默认为 false，但这没关系，旧版后端会放行）
    public bool isJoinedZenRank = false;

    #endregion

    #region 系统设置数据

    public bool IsMusicOn = true; // 背景音乐开关

    public bool IsSoundOn = true; // 音效开关

    //public bool IsVibrationOn ;    // 震动反馈开关
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
    public bool IsFirstLaunch = true; // 首次启动标志
    public bool isShowVocabulary; // 是否显示词库标志

    public int TotalPayTimes; //支付次数
    public float TotalRevenue; //累计付费金额
    public int totallogin; // 总登录次数
    public int totalSeeAds; // 总看广告次数
    public int activeDayCnt; //活跃天数

    // 生命周期事件相关数据
    public float TotalOnlineMinutes; // 累计在线总时长（分钟）
    public Dictionary<string, bool> ReportedLifecycleEvents; // 已上报的生命周期事件


    // --- 广告策略存档数据 ---
    public int AdFatigueScore; // 疲劳分数
    public float TotalPlayTimeSeconds; // 累计游戏时长 (G2)
    public long LastPayTimeTicks; // 上次付费时间 (Ticks) (G3)
    public long LastRewardAdTimeTicks; // 上次看激励视频的时间 (G1)

    public long LastInterstitialTimeTicks; // 上次看插屏的时间 (G5)

    // 👇 新增：D规则（每日首关插屏概率）状态记录
    public bool isDayFirstLevelAdChecked; // 今日首关是否已进行过插屏概率判定
    public bool isDayFirstLevelAdAllowed; // 今日首关插屏概率判定的结果

    public int HighestZenScore = 0; // 历史最高禅意分
    public Dictionary<int, float> BestClearTimes = new Dictionary<int, float>(); // 极速通关记录字典

    /// <summary>
    /// 词库数据
    /// </summary>   
    //public WordVocabulary<string> wordVocabularyJan  = new WordVocabulary<string>();  
    //public WordVocabulary<string> wordVocabularyChinTra  = new WordVocabulary<string>(); 
    public WordVocabulary<string> wordVocabularyChinSim = new WordVocabulary<string>();

    #endregion

    #region 时间相关数据

    public string logoutTime; // 退出时间
    public string curStageStartTime; // 当前关卡开始时间
    public bool curIsEnter; // 当前关卡是否已经进入

    public int curStageOnlineTime; // 当前关卡在线时长(秒)

    // 关卡对应通关时长
    public Dictionary<int, int> passLevelUseTime = new Dictionary<int, int>();

    public string firstPayTime; //首次充值时间
    public string lastPayTime; //最后充值时间
    public string firstLoginTime; //首次登录时间
    public string lastLoginDay; //最后登录时间
    public int zenCount; // 禅意值数量

    #endregion

    #region 道具数据

    /// <summary>
    /// 道具信息字典
    /// Key: 道具ID (101:重置, 102:提示, 103:蝴蝶)
    /// Value: 道具信息
    /// </summary>
    public Dictionary<int, ToolInfo> toolInfo;

    //签到数据
    public int signid; // 签到id
    public bool isDayEnterSign; // 签到活动重置后是否为首次进入
    public string signOpenTime; // 签到活动开启时间

    //限时活动数据
    public int timePuzzlecount; // 限时活动中连出成语数量
    public int timerePuzzleid; // 限时活动中奖励领取id
    public string limitOpenTime; // 限时活动开启时间
    public int limitMinPeriod; // 限时翻倍周期（分钟）
    public string limitEndTime; // 限时翻倍结束时间
    public bool isNeedShowHelp; // 是否需要主动弹窗帮助界面
    public bool isDayEnterLimint; // 限时活动重置后是否为首次进入

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

    public bool isHideShopRedPoint; // 商店每日免费商品是否获得
    public bool isDayFreeGet; // 商店每日免费商品是否获得
    public bool isDayGoldBuy; // 商店每日金币购买商品是否买过
    public bool isDayMoneyBuy; // 商店每日现金购买商品是否买过


    public SignSaveData _signSaveData = new SignSaveData();
    public LoadTimeIndex _loadTimeIndexData = new LoadTimeIndex();
    public List<int> _getAnimalsHeadIcons = new List<int>();

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


    // 生命周期事件配置
    private readonly int[] LIFE_CYCLE_MINUTES = { 1, 5, 10, 15, 20, 30, 40, 60, 120, 300, 600 };
    private readonly string LIFE_CYCLE_EVENT_PREFIX = "time_level_";



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
    /// 加载用户数据
    /// </summary>
    public void InitData()
    {
        # region 初始数据

        // 基础数据
        // 用户基础数据
        PlayerId = null;
        ABName = "0";
        UserHeadId = 0;
        UserName = null;
        UserId = null;
        Gold = AppGameSettings.StartingGold;
        TotalConsumedGold = 0;
        TotalEarnedGold = 0;
        CurrentHexStage = AppGameSettings.FirstLevel;
        CurrentChessStage = AppGameSettings.FirstLevel;
        levelMode = 2;
        dayPassStageCount = 0;
        LanguageCode = GetLanguage();
        IsMusicOn = true;
        IsSoundOn = true;
        IsAgreePrivacy = false;
        Zenlevel = "ZenState01";
        Energy = 5;
        LastEnergyUpdateTime = DateTime.Now.ToString();
        hasUsedFreeRevive = false;
        // 评价界面显示次数
        showRateusCount = 0;
        // 评价界面显示时间
        showRateusTime = null;
        isChangeUserName = false;
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
        curIsEnter = false;
        zenCount = 0;
        passLevelUseTime = new Dictionary<int, int>();
        //限时商店数据
        limitShopItems = new List<ShopLimitData>();
        isDayFreeGet = false;
        isDayGoldBuy = false;
        isDayMoneyBuy = false;
        isHideShopRedPoint = false;

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

        // 初始化道具数据
        toolInfo = new Dictionary<int, ToolInfo>
        {
            {
                101,
                new ToolInfo
                {
                    cost = AppGameSettings.ShopItems.SingleHintCost, type = "SignleHint",
                    count = AppGameSettings.ShopItems.SingleHintCount
                }
            },
            {
                102,
                new ToolInfo
                {
                    cost = AppGameSettings.ShopItems.WordHintCost, type = "WordHint",
                    count = AppGameSettings.ShopItems.WordHintCount
                }
            },
            {
                103,
                new ToolInfo
                {
                    cost = AppGameSettings.ShopItems.ButterflyCost, type = "Butterfly",
                    count = AppGameSettings.ShopItems.StartingButterflies
                }
            },
            {
                104,
                new ToolInfo
                {
                    cost = AppGameSettings.ShopItems.AutoCompleteCost, type = "AutoComplete",
                    count = AppGameSettings.ShopItems.WordHintCount
                }
            }
        };

        // 签到数据
        signOpenTime = null;
        signid = 0;
        isDayEnterSign = true;
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
        isDayFirstLevelAdChecked = false;
        isDayFirstLevelAdAllowed = false;

        HighestZenScore = 0;
        BestClearTimes = new Dictionary<int, float>();

        #endregion
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
        UserHeadId = user.UserHeadId;
        UserName = user.UserName;
        UserId = user.UserId;
        Gold = user.Gold;
        TotalConsumedGold = user.TotalConsumedGold;
        TotalEarnedGold = user.TotalEarnedGold;
        CurrentHexStage = user.CurrentHexStage;
        CurrentChessStage = user.CurrentChessStage;
        levelMode = user.levelMode;
        zenCount = user.zenCount;
        Zenlevel = user.Zenlevel ?? "ZenState01";
        dayPassStageCount = user.dayPassStageCount;
        LanguageCode = GetLanguage();
        IsMusicOn = user.IsMusicOn;
        IsSoundOn = user.IsSoundOn;
        IsAgreePrivacy = user.IsAgreePrivacy;
        Rigister = user.Rigister;
        firstLoginTime = user.firstLoginTime ?? DateTime.Now.ToString();
        lastLoginDay = user.lastLoginDay;
        firstPayTime = user.firstPayTime;
        lastPayTime = user.lastPayTime;

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

        GoldLeaf = user.GoldLeaf;

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

        isChangeUserName = user.isChangeUserName;
        // 评价界面显示次数
        showRateusCount = user.showRateusCount;
        // 评价界面显示时间
        showRateusTime = user.showRateusTime;
        //限时商店数据
        limitShopItems = user.limitShopItems ?? new List<ShopLimitData>();
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
        toolInfo = user.toolInfo;
        // 签到数据
        signOpenTime = user.signOpenTime;
        signid = user.signid;
        isDayEnterSign = user.isDayEnterSign;
        curStageStartTime = user.curStageStartTime;
        curStageOnlineTime = user.curStageOnlineTime;
        //显示奖励数据
        timerePuzzleid = user.timerePuzzleid;
        limitOpenTime = user.limitOpenTime;
        limitMinPeriod = user.limitMinPeriod;
        limitEndTime = user.limitEndTime;
        isDayEnterLimint = user.isDayEnterLimint;
        timePuzzlecount = user.timePuzzlecount;
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
        isDayFirstLevelAdChecked = user.isDayFirstLevelAdChecked;
        isDayFirstLevelAdAllowed = user.isDayFirstLevelAdAllowed;

        HighestZenScore = user.HighestZenScore;
        BestClearTimes = user.BestClearTimes ?? new Dictionary<int, float>();

        // 检查并初始化缺失的生命周期事件
        InitializeLifecycleEvents();

        // 检查是否需要重置每日数据
        CheckResetDailyTime();

        // 检查是否需要上报生命周期事件
        CheckLifecycleEvents();

        CheckShopBuyData();
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
            isDayFreeGet = false;
            isDayGoldBuy = false;
            isDayMoneyBuy = false;
            isHideShopRedPoint = false;
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
        //签到数据
        signid = 0;
        isDayEnterSign = true;
        //每日通过数据
        dayPassStageCount = 0;
        // 可在此添加其他需要每日重置的数据
        // 👇 新增：每日重置首关插屏判定状态
        isDayFirstLevelAdChecked = false;
        isDayFirstLevelAdAllowed = false;
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

            if (shopdata.isget && shopdata.adstype == (int)LimitRewordType.Remove7DayAds)
            {
                int hour = 24 * 7;
                DateTime buyendTime = DateTime.Parse(shopdata.gettime).AddHours(hour);
                TimeSpan timeSpan = buyendTime.Subtract(DateTime.Now);

                if (timeSpan.TotalMinutes <= 0)
                {
                    shopdata.isoverdate = true;
                }
            }
        }
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
            GameDataManager.Instance.CommitPushServerData();

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
    /// 更新指定主题使用次数
    /// </summary>
    /// <param name="themeid"></param>
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
    /// <param name="value">变化值</param>
    /// <param name="isanim">是否显示动画</param>
    /// <param name="updateui">是否更新UI</param>
    public void UpdateGoldLeaf(int value, string message = "")
    {
        GoldLeaf += value;

        if (value <= 0)
        {
            SendCurrencyEvent(value, "金箔", message); // 消耗金币事件
        }
        else
        {
            SendCurrencyEvent(value, "金箔", message); // 获得金币事件
        }

        Debug.Log($"金箔{(value > 0 ? "增加" : "减少")}: {Math.Abs(value)}, 当前金箔: {GoldLeaf}");

        // 金箔数量变化后，重新检查皮肤入口红点状态
        ThemeManager.Instance.CheckAndUpdateSkinRedPoint();
    }

    /// <summary>
    /// 获得道具消耗总数
    /// </summary>
    /// <returns></returns>
    public int GetTotalToolCost()
    {
        int totalToolCost = 0;
        totalToolCost += toolInfo[101].reducecount + toolInfo[102].reducecount
                                                   + toolInfo[103].reducecount;
        return totalToolCost;
    }

    /// <summary>
    /// 更新当前关卡在线时长
    /// </summary>
    public void UpdateOnlineStageTime()
    {
        if (!string.IsNullOrEmpty(curStageStartTime))
        {
            DateTime startTime = DateTime.Parse(curStageStartTime);
            TimeSpan duration = DateTime.Now - startTime;

            if (duration.TotalSeconds >= 0)
            {
                curStageOnlineTime += (int)duration.TotalSeconds;
            }

            //保存完当前在线时长后，需要清空开始时间，防止进入后台时重复计算时长；
            curStageStartTime = null;
        }
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
            // 假设字典的键或值中包含时间信息，我们可以根据时间排序后保留最近的9个
            // 例如，如果键是时间戳或包含时间信息：
            var recentEntries = passLevelUseTime.OrderByDescending(x => x.Key).Take(9).ToList();

            // 清空原字典
            passLevelUseTime.Clear();

            // 将最近的9个条目添加回字典
            foreach (var entry in recentEntries)
            {
                passLevelUseTime[entry.Key] = entry.Value;
            }
        }
    }


    /// <summary>
    /// 更新关卡进度
    /// </summary>
    /// <param name="value">变化值</param>
    /// <param name="isSet">是否直接设置值</param>
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
    /// 更新关卡进度
    /// </summary>
    /// <param name="value">变化值</param>
    /// <param name="isSet">是否直接设置值</param>
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
    public void UpdateGold(int value, bool isanim = false, bool updateui = true, string message = "")
    {
        //int oldGold = Gold;
        Gold += value;

        if (updateui)
        {
            EventDispatcher.instance.TriggerChangeGoldUI(value, isanim);
        }


        if (value <= 0)
        {
            TotalConsumedGold += Math.Abs(value);
            SendCurrencyEvent(value, "金币", message); // 消耗金币事件
        }
        else
        {
            TotalEarnedGold += value;
            SendCurrencyEvent(value, "金币", message); // 获得金币事件
        }

        GameDataManager.Instance.CommitGameData();
    }

    /// <summary>
    /// 每日首次开启签到活动
    /// </summary>
    public void EveryDayOpenSign()
    {
        signOpenTime = DateTime.Now.ToString();
        isDayEnterSign = false;
    }

    /// <summary>
    /// 更新限时活动进度id
    /// </summary>
    public void UpdateSignid()
    {
        signid++;
        if (string.IsNullOrEmpty(signOpenTime)) signOpenTime = DateTime.Now.ToString();
        TimeSpan ts = DateTime.Now.Subtract(DateTime.Parse(signOpenTime));
        AnalyticMgr.ActivityProgress("签到活动", signid, (int)ts.TotalSeconds);
        if (signid > 3)
        {
            AnalyticMgr.ActivityComplete("签到活动", (int)ts.TotalSeconds);
        }
    }

    /// <summary>
    /// 发送货币事件（用于统计）
    /// </summary>
    public void SendCurrencyEvent(int value, string currencyName, string message = "", string word = "")
    {
        AnalyticMgr.SetCommonProperties();
        if (value <= 0)
        {
            AnalyticMgr.ResourceReduce(currencyName, Mathf.Abs(value), message, word);
        }
        else
        {
            AnalyticMgr.ResourceGet(currencyName, value, message, word);
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

    // /// <summary>
    // /// 更新道具数量
    // /// </summary>
    // /// <param name="type">道具类型</param>
    // /// <param name="value">变化值</param>
    // public void UpdateTool(LimitRewordType type, int value,string message = "")
    // {
    //     int toolId = GetToolIdByType(type);
    //     
    //     if (toolInfo.ContainsKey(toolId))
    //     {
    //         toolInfo[toolId].count += value;
    //         Debug.Log($"{type}道具{(value > 0 ? "增加" : "减少")}: {Math.Abs(value)}, 当前数量: {toolInfo[toolId].count}");
    //         if (value > 0)
    //         {
    //             toolInfo[toolId].addcount += value;
    //         }
    //         else
    //         {
    //             toolInfo[toolId].reducecount += Mathf.Abs(value);
    //         }
    //
    //         string toolName = null;
    //
    //         switch (type)
    //         {
    //             case LimitRewordType.SingleTipsttool:
    //                 toolName = "重置(提示灯)道具";
    //                 break;
    //             case LimitRewordType.Tipstool:
    //                 toolName = "提示（放大镜）道具";
    //                 break;
    //             case LimitRewordType.Butterfly:
    //                 toolName = "蝴蝶道具";
    //                 break;
    //             case LimitRewordType.AutoComplete:
    //                 toolName = "自动拼字";
    //                 break;
    //         }
    //         
    //         // 发送道具统计事件
    //         SendCurrencyEvent(value, toolName,message); // 假设货币类型从1开始
    //         
    //         //刷新道具
    //         EventDispatcher.instance.TriggerChangeGoldUI(0, false);
    //         
    //         GameDataManager.Instance.CommitGameData();
    //     }
    // }


    /// <summary>
    /// 更新道具数量
    /// </summary>
    /// <param name="type">道具类型</param>
    /// <param name="value">变化值</param>
    public void UpdateTool(LimitRewordType type, int value, string message = "", string word = "")
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
                case LimitRewordType.SingleWordTipsttool:
                    toolName = "单字词语提示道具";
                    break;
                case LimitRewordType.Tipstool:
                    toolName = "提示道具";
                    break;
                case LimitRewordType.Butterfly:
                    toolName = "蝴蝶道具";
                    break;
                case LimitRewordType.AutoComplete:
                    toolName = "自动拼字";
                    break;
            }

            // 发送道具统计事件
            SendCurrencyEvent(value, toolName, message, word);
        }
    }


    /// <summary>
    /// 根据道具类型获取道具ID
    /// </summary>
    private int GetToolIdByType(LimitRewordType type)
    {
        return type switch
        {
            LimitRewordType.SingleWordTipsttool => 101,
            LimitRewordType.Tipstool => 102,
            LimitRewordType.Butterfly => 103,
            LimitRewordType.AutoComplete => 104,
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
    /// <param name="minutes">增加的分钟数</param>
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

    /// <summary>
    /// 获取下一个生命周期事件信息
    /// </summary>
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

        // 所有事件都已完成
        return (0, "已完成所有生命周期事件");
    }

    /// <summary>
    /// 获取已完成的生命周期事件数量
    /// </summary>
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

    /// <summary>
    /// 获取生命周期事件完成进度
    /// </summary>
    public float GetLifecycleEventProgress()
    {
        int completed = GetCompletedLifecycleEventCount();
        return (float)completed / LIFE_CYCLE_MINUTES.Length;
    }

    /// <summary>
    /// 重置所有生命周期事件（用于测试或账号重置）
    /// </summary>
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

    /// <summary>
    /// 添加单词到关卡词库
    /// </summary>
    public void AddStagePuzzle(string Puzzle)
    {
        WordVocabulary<string> vocabulary = GetWordVocabulary();
        if (!vocabulary.LevelWords.Contains(Puzzle))
        {
            vocabulary.LevelWords.Insert(0, Puzzle);
        }
    }


    /// <summary>
    /// 添加单词到生词本
    /// </summary>
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
        }
    }

    /// <summary>
    /// 从生词本移除单词
    /// </summary>
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
        // WordVocabulary<string> wordVocabulary = wordVocabularyJan;
        // switch (LanguageCode)
        // {
        //     case "English":
        //         wordVocabulary = wordVocabularyJan;
        //         break;
        //     case "ChineseTraditional":
        //         wordVocabulary = wordVocabularyChinTra;
        //         break;
        //     case "ChineseSimplified":
        //         wordVocabulary = wordVocabularyChinSim;
        //         break;
        // }
        return wordVocabularyChinSim;
    }

    /// <summary>
    /// 获取词库存储键
    /// </summary>
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
    

    [JsonIgnore] public const int MAX_NATURAL_ENERGY = 5; // 自然恢复上限
    [JsonIgnore] public const int ENERGY_REGEN_MINUTES = 30; // 恢复1点所需分钟数

    /// <summary>
    /// 计算并执行体力自然恢复 (支持离线、切后台)
    /// </summary>
    public void CalculateEnergyRegen()
    {
        // 如果体力超出或等于上限，不自然恢复，且把计时器锚点重置到当前，防止一跌下5点就瞬间恢复
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

        // 计算过去的时间里，够恢复几个30分钟？
        int recoveredPoints = (int)(passedTime.TotalMinutes / ENERGY_REGEN_MINUTES);
        if (recoveredPoints > 0)
        {
            Energy += recoveredPoints;

            if (Energy >= MAX_NATURAL_ENERGY)
            {
                Energy = MAX_NATURAL_ENERGY;
                LastEnergyUpdateTime = DateTime.Now.ToString(); // 满了，重置计时器
            }
            else
            {
                // 没满，把用掉的时间加到上次计算时间上，保留剩余的零头（例如过去35分钟，加回30分钟，剩余5分钟进度保留）
                LastEnergyUpdateTime = lastTime.AddMinutes(recoveredPoints * ENERGY_REGEN_MINUTES).ToString();
            }

            SendCurrencyEvent(recoveredPoints, "体力", "时间自然恢复");
            // 通知UI刷新 (需要在EventDispatcher里加上这行，如果你有的话)
            // EventDispatcher.instance?.TriggerEnergyUIChange(); 
        }
    }

    /// <summary>
    /// 消耗体力
    /// </summary>
    /// <param name="stageId">当前关卡ID，用于判断第1关免体力</param>
    /// <param name="amount">消耗数量，默认1</param>
    /// <returns>是否消耗成功（体力是否足够）</returns>
    public bool ConsumeEnergy(int stageId, int amount = 1, string message = "进入关卡消耗")
    {
        // 规则1：第一关体力无限
        if (stageId == 1) return true;

        if (Energy >= amount)
        {
            bool wasFull = Energy >= MAX_NATURAL_ENERGY;
            Energy -= amount;

            // 如果原本是满的(>=5)，现在扣到5以下了，马上启动自然恢复计时器！
            if (wasFull && Energy < MAX_NATURAL_ENERGY)
            {
                LastEnergyUpdateTime = DateTime.Now.ToString();
            }

            SendCurrencyEvent(-amount, "体力", message);
            return true;
        }

        return false; // 体力不足
    }

    /// <summary>
    /// 活动奖励增加体力 (无上限叠加)
    /// </summary>
    public void AddBonusEnergy(int amount, string message = "奖励获取")
    {
        Energy += amount;
        SendCurrencyEvent(amount, "体力", message);
        // EventDispatcher.instance?.TriggerEnergyUIChange();
    }

    /// <summary>
    /// UI显示辅助：获取体力展示文本
    /// </summary>
    public string GetEnergyDisplayString()
    {
        if (CurrentChessStage == 1) return "充足"; // 第一关特权
        return Energy.ToString();
    }

    /// <summary>
    /// UI显示辅助：获取距离恢复下1点体力还剩多少秒 (用于UI倒计时)
    /// </summary>
    public int GetNextEnergyRegenSeconds()
    {
        if (Energy >= MAX_NATURAL_ENERGY) return 0;

        DateTime lastTime = DateTime.Parse(LastEnergyUpdateTime);
        DateTime nextRegenTime = lastTime.AddMinutes(ENERGY_REGEN_MINUTES);
        return Mathf.Max(0, (int)(nextRegenTime - DateTime.Now).TotalSeconds);
    }

    #region 修改名称

    /// <summary>
    /// 检查是否可以弹出角色信息窗口
    /// </summary>
    public bool CanShowCharInfoPopup()
    {
        // 已经起名则不再弹出
        if (!string.IsNullOrEmpty(UserName))
            return false;

        // 超过3次
        if (charInfoPopupCount >= 3)
            return false;

        // 第一次弹出
        if (string.IsNullOrEmpty(charInfoPopupLastTime))
            return true;

        // 检查间隔
        if (DateTime.TryParse(charInfoPopupLastTime, out var lastTime))
        {
            return (DateTime.Now - lastTime).TotalHours >= 72;
        }

        return true; // 解析失败当作可以弹出
    }


    /// <summary>
    /// 记录一次弹出
    /// </summary>
    public void MarkCharInfoPopupShown()
    {
        charInfoPopupCount++;
        charInfoPopupLastTime = DateTime.Now.ToString("o"); // ISO 8601 格式，避免区域问题
        SaveData(); // 立即保存
    }


    #endregion

    #region 辅助方法

    /// <summary>
    /// 获取当前语言设置
    /// </summary>
    private string GetLanguage()
    {
        string defaultLanguage = AppGameSettings.SystemLanguage;
        Debug.Log($"当前语言设置: {defaultLanguage}");
        return defaultLanguage;
    }

    /// <summary>
    /// 清空所有用户数据
    /// </summary>
    public void ClearAllData()
    {
        InitData();
    }

    #endregion
}