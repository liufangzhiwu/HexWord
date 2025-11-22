using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
// using ThinkingAnalytics;
using Unity.VisualScripting;
using UnityEngine;

#region 数据结构定义

// /// <summary>
// /// 道具类型枚举
// /// </summary>
// public enum ToolType 
// { 
//     Reset,     // 重置道具
//     Hint,      // 提示道具
//     Butterfly, // 蝴蝶道具
//     Null       // 空类型
// }

#endregion

/// <summary>
/// 用户游戏数据管理类
/// 负责处理用户数据的加载、保存、初始化及日常管理
/// 使用JSON序列化和加密存储用户数据
/// </summary>
public class UserData
{
    #region 用户基础数据
    public string PlayerId;              // 玩家ID
    public string ABName;           // AB测试包名
    public string UserName;
    public int UserHeadId;
    public string UserId;            // 用户唯一标识
    public int Gold;                // 当前金币数量
    public int CurrentHexStage;        // 当前六边形关卡进度
    
    public int CurrentChessStage;        // 当前拼字关卡进度
    public int levelMode;               // 当前游戏模式 1:普通模式 2:拼字模式 3:六边形模式
    public int dayPassStageCount;        // 每日通关数量

    #endregion

    #region 系统设置数据

    public bool IsMusicOn = true;       // 背景音乐开关
    public bool IsSoundOn = true;        // 音效开关
    //public bool IsVibrationOn ;    // 震动反馈开关
    public string LanguageCode;          // 当前语言代码

    #endregion

    #region 游戏进度数据

    public int TutorialProgress;        // 新手引导进度
    public Dictionary<int, bool> ChessTutorialProgress;   // 填字引导进度
    public int GetTutorialProgress() { return TutorialProgress; }
    public bool Rigister;   // 注册标志
    public bool IsFirstLaunch = true;   // 首次启动标志
    public bool isShowVocabulary;       // 是否显示词库标志
    
    public int TotalPayTimes; //支付次数
    public float TotalRevenue; //累计付费金额
    public int totallogin;       // 总登录次数
    public int totalSeeAds;       // 总看广告次数
    public int activeDayCnt; //活跃天数
    
    /// <summary>
    /// 词库数据
    /// </summary>   
    //public WordVocabulary<string> wordVocabularyJan  = new WordVocabulary<string>();  
    //public WordVocabulary<string> wordVocabularyChinTra  = new WordVocabulary<string>(); 
    public WordVocabulary<string> wordVocabularyChinSim  = new WordVocabulary<string>();

    #endregion

    #region 时间相关数据

    public string logoutTime;           // 退出时间
    public string curStageStartTime;    // 当前关卡开始时间
    public bool curIsEnter;    // 当前关卡是否已经进入
    public int curStageOnlineTime;      // 当前关卡在线时长(秒)
    // 关卡对应通关时长
    public Dictionary<int, int> passLevelUseTime=new Dictionary<int, int>();
    
    public string firstPayTime;//首次充值时间
    public string lastPayTime;//最后充值时间
    public long firstLoginStamp;//首次登录时间戳
    public string lastLoginDay;//最后登录时间

    #endregion

    #region 道具数据

    /// <summary>
    /// 道具信息字典
    /// Key: 道具ID (101:重置, 102:提示, 103:蝴蝶)
    /// Value: 道具信息
    /// </summary>
    public Dictionary<int, ToolInfo> toolInfo;
    
    //签到数据
    public int signid;         // 签到id
    public bool isDayEnterSign;             // 签到活动重置后是否为首次进入
    public string signOpenTime;          // 签到活动开启时间
    
    //限时活动数据
    public int timePuzzlecount;            // 限时活动中连出成语数量
    public int timerePuzzleid;            // 限时活动中奖励领取id
    public string limitOpenTime;        // 限时活动开启时间
    public int limitMinPeriod;         // 限时翻倍周期（分钟）
    public string limitEndTime;        // 限时翻倍结束时间
    public bool isNeedShowHelp;      // 是否需要主动弹窗帮助界面
    public bool isDayEnterLimint;      // 限时活动重置后是否为首次进入
    
    
    /// <summary>
    /// 每日任务数据
    /// </summary>
    /// 
    /// 完成任务id
    public List<CompleteTaskData> completeTaskList=new List<CompleteTaskData>();
    public bool butterflyTaskIsOpen;        // 每日任务无限蝴蝶任务是否开启
    public int taskButterflyUseMinutes;             // 每日任务无限蝴蝶任务使用分钟
    public bool isAllCompleteTask;      // 每日任务活动是否全部完成
    /// 任务数据
    public List<TaskSaveData> taskSaveDatas=new List<TaskSaveData>();
    
    /// 商店限时商品数据
    //public List<ShopLimitData> limitShopItems=new List<ShopLimitData>();

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
    
    
    #region PlayerPrefs 键名常量
    private const string USER_DATA_KEY = "UserData";
    private const string USER_DATA_VERSION_KEY = "UserData_Version";
    private const int CURRENT_DATA_VERSION = 2; // 每次数据结构变更时递增
    #endregion
    
    // 增量保存的键名前缀
    private const string CURRENT_STAGE_KEY = "CurrentStage";
    private const string MAX_STAGE_KEY = "MaxStage";
    private const string TOOL_KEY_PREFIX = "Tool_";

    #region 数据初始化方法
    
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
                
            if (loadedData.CurrentHexStage <=0)
            {
                Debug.LogError($"关卡数据异常: {json}");
                InitData();
                //AnalyticMgr.BugRecord("关卡存档异常",json);
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
        ABName = null;
        UserHeadId = 0;
        UserName=null;
        UserId = null;
        Gold = AppGameSettings.StartingGold;
        CurrentHexStage = AppGameSettings.FirstLevel;
        CurrentChessStage = AppGameSettings.FirstLevel;
        levelMode = 3;
        dayPassStageCount = 0;
        LanguageCode = GetLanguage();
        IsMusicOn = true;
        IsSoundOn = true;
        // 游戏进度
        TutorialProgress = 0;
        ChessTutorialProgress = new Dictionary<int,bool> {{1,false},{2,false},{3,false},{4,false},{5,false}};
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
        firstLoginStamp = DateTime.Now.Ticks;
        lastLoginDay = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        curStageStartTime = null;
        curStageOnlineTime = 0;
        curIsEnter = false;
        curIsEnter = false;
        // 初始化道具数据
        toolInfo = new Dictionary<int, ToolInfo>
        {
            { 101, new ToolInfo { cost = AppGameSettings.ShopItems.ResetCost, type = "Reset", count = AppGameSettings.ShopItems.StartingResets } },
            { 102, new ToolInfo { cost = AppGameSettings.ShopItems.HintCost, type = "Hint", count = AppGameSettings.ShopItems.StartingHints } },
            { 103, new ToolInfo { cost = AppGameSettings.ShopItems.ButterflyCost, type = "Butterfly", count = AppGameSettings.ShopItems.StartingButterflies } },
            { 104, new ToolInfo { cost = AppGameSettings.ShopItems.AutoCompleteCost, type = "AutoComplete", count = AppGameSettings.ShopItems.StartingHints } }
        };
        // 签到数据
        signOpenTime = null;
        signid = 0;
        isDayEnterSign = true;
        butterflyTaskIsOpen = false;
        taskButterflyUseMinutes = 0;
        
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
        CurrentHexStage = user.CurrentHexStage;
        CurrentChessStage = user.CurrentChessStage;
        levelMode = user.levelMode;
        dayPassStageCount = user.dayPassStageCount;
        LanguageCode = GetLanguage();
        IsMusicOn = user.IsMusicOn;
        IsSoundOn = user.IsSoundOn;
        Rigister = user.Rigister;
        firstLoginStamp = user.firstLoginStamp;
        lastLoginDay = user.lastLoginDay;
        firstPayTime = user.firstPayTime;
        lastPayTime = user.lastPayTime;
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
        ChessTutorialProgress = user.ChessTutorialProgress ?? new Dictionary<int,bool> {{1,false},{2,false},{3,false},{4,false},{5,false}};
        IsFirstLaunch = user.IsFirstLaunch;
        isShowVocabulary = user.isShowVocabulary;
        // 时间数据
        logoutTime = DateTime.Now.ToString();
        curIsEnter = user.curIsEnter;
        // 初始化道具数据
        toolInfo = user.toolInfo;
        // 签到数据
        signOpenTime = user.signOpenTime;
        signid = user.signid;
        isDayEnterSign = user.isDayEnterSign;
        curStageStartTime= user.curStageStartTime;
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
        
        // 检查是否需要重置每日数据
        CheckResetLimitTime();
       
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
        }
        return "方块消";
    }
    
    /// <summary>
    /// 检查并重置每日限时数据
    /// </summary>
    public void CheckResetLimitTime()
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
        
        butterflyTaskIsOpen=false;
        completeTaskList = new List<CompleteTaskData>();
        taskButterflyUseMinutes = 0;
        taskSaveDatas=new List<TaskSaveData>();
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
    }
    
    private void UpdatePanelUI()
    {
        if (SystemManager.Instance != null)
        {
            if(SystemManager.Instance.PanelIsShowing(PanelType.LimitTimeScreen))
                SystemManager.Instance.HidePanel(PanelType.LimitTimeScreen);
            
            if (SystemManager.Instance.PanelIsShowing(PanelType.SignWaterScreen))
            {
                SystemManager.Instance.HidePanel(PanelType.SignWaterScreen);
            }
            
            if(SystemManager.Instance.PanelIsShowing(PanelType.DailyTasksScreen))
                SystemManager.Instance.HidePanel(PanelType.DailyTasksScreen);
           
        }
        
        if (WaterManager.instance != null)
        {
            WaterManager.instance.WaterShow(false);
            WaterManager.instance.ClearWater();
        }
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
            if(CurrentHexStage<=0) return;
            
            // 更新登出时间
            if (!string.IsNullOrEmpty(logoutTime) && DateTime.Now > DateTime.Parse(logoutTime))
            {
                logoutTime = DateTime.Now.ToString();
            }
            
            // 更新在线时长
            UpdateOnlineStageTime();
            
            // 标记非首次进入
            IsFirstLaunch = false;

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
    /// 获得道具消耗总数
    /// </summary>
    /// <returns></returns>
    public int GetTotalToolCost()
    {
        int totalToolCost = 0;
        totalToolCost += toolInfo[101].reducecount+toolInfo[102].reducecount
                                                  +toolInfo[103].reducecount;
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
        timePuzzlecount+=value;
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
        TutorialProgress +=1;
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
        //AnalyticMgr.ActivityProgress("签到活动",signid,(int)ts.TotalSeconds);
        if (signid > 3)
        {
            //AnalyticMgr.ActivityComplete("签到活动",(int)ts.TotalSeconds);
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
        
        if (string.IsNullOrEmpty(limitOpenTime))
        {
            limitOpenTime = DateTime.Now.ToString();
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
    public void UpdateTool(LimitRewordType type, int value, string message = "")
    {
        int toolId = GetToolIdByType(type);
        
        if (toolInfo.ContainsKey(toolId))
        {
            toolInfo[toolId].count += value;
            
            if (value > 0)
            {
                toolInfo[toolId].addcount += value;
            }
            else
            {
                toolInfo[toolId].reducecount += Mathf.Abs(value);
            }
            
            //Debug.Log($"{type}道具{(value > 0 ? "增加" : "减少")}: {Math.Abs(value)}, 当前数量: {toolInfo[toolId].count}");
        }
    }


    /// <summary>
    /// 根据道具类型获取道具ID
    /// </summary>
    private int GetToolIdByType(LimitRewordType type)
    {
        return type switch
        {
            LimitRewordType.Resettool => 101,
            LimitRewordType.Tipstool => 102,
            LimitRewordType.Butterfly => 103,
            _ => 0
        };
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
        PlayerPrefs.DeleteKey(USER_DATA_KEY);
        PlayerPrefs.DeleteKey(USER_DATA_VERSION_KEY);
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("已清空所有用户数据");
    }

    #endregion
}