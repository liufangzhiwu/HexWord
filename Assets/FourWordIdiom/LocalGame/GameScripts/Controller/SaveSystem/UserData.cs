using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Passport;
using Unity.Passport.Sample.Scripts;
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
    public int Gold;                // 当前金币数量
    public int CurrentStage;        // 当前关卡进度

    #endregion

    #region 系统设置数据

    public bool IsMusicOn = true;       // 背景音乐开关
    public bool IsSoundOn = true;        // 音效开关
    //public bool IsVibrationOn ;    // 震动反馈开关
    public string LanguageCode;          // 当前语言代码

    #endregion

    #region 游戏进度数据

    private int TutorialProgress;        // 新手引导进度
    public int GetTutorialProgress() { return TutorialProgress; }
    
    public bool IsFirstLaunch = true;   // 首次启动标志
    public bool isShowVocabulary;       // 是否显示词库标志
    
    /// <summary>
    /// 词库数据
    /// </summary>   
    //public WordVocabulary<string> wordVocabularyJan  = new WordVocabulary<string>();  
    //public WordVocabulary<string> wordVocabularyChinTra  = new WordVocabulary<string>(); 
    public WordVocabulary<string> wordVocabularyChinSim  = new WordVocabulary<string>();

    #endregion

    #region 时间相关数据

    public string logoutTime;           // 退出时间
    //public string curStageStartTime;    // 当前关卡开始时间
    public bool curIsEnter;    // 当前关卡是否已经进入
    //public int curStageOnlineTime;      // 当前关卡在线时长(秒)
    // 关卡对应通关时长
    //public Dictionary<int, int> passLevelUseTime=new Dictionary<int, int>();

    #endregion

    #region 统计计数数据

    // public int hudiecount;              // 蝴蝶道具数量
    // public int showRateusCount;         // 好评界面显示次数
    // public int dayPassStageCount;       // 当日通关次数
    // public bool isChangeUserName;       // 是否更改过用户名称
    // public int totallogin;       // 总登录次数
    // public int totalSeeAds;       // 总看广告次数

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
    //public bool butterflyTaskIsOpen;        // 每日任务无限蝴蝶任务是否开启
    //public int taskButterflyUseMinutes;             // 每日任务无限蝴蝶任务使用分钟
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
    public void LoadData(Persona persona)
    {
        // 检查数据版本
        int savedVersion = PlayerPrefs.GetInt(USER_DATA_VERSION_KEY, 0);
        
        if (savedVersion < CURRENT_DATA_VERSION)
        {
            Debug.Log($"数据版本更新: {savedVersion} -> {CURRENT_DATA_VERSION}");
          
        }
        
        # region 初始数据
        
        // 基础数据
        Gold = AppGameSettings.StartingGold;
        CurrentStage = AppGameSettings.FirstLevel;
        LanguageCode = GetLanguage();
        IsMusicOn = true;
        IsSoundOn = true;
        // 游戏进度
        TutorialProgress = 0;
        IsFirstLaunch = true;
        isShowVocabulary = false;
        // 时间数据
        logoutTime = DateTime.Now.ToString();
        curIsEnter = false;
        // 初始化道具数据
        toolInfo = new Dictionary<int, ToolInfo>
        {
            { 101, new ToolInfo { cost = AppGameSettings.ShopItems.ResetCost, type = "Reset", count = AppGameSettings.ShopItems.StartingResets } },
            { 102, new ToolInfo { cost = AppGameSettings.ShopItems.HintCost, type = "Hint", count = AppGameSettings.ShopItems.StartingHints } },
            { 103, new ToolInfo { cost = AppGameSettings.ShopItems.ButterflyCost, type = "Butterfly", count = AppGameSettings.ShopItems.StartingButterflies } }
        };
        // 签到数据
        signOpenTime = null;
        signid = 0;
        isDayEnterSign = true;
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

        if (persona != null)
        {
            // 基础数据
            if (persona.Properties.ContainsKey("Gold"))
                Gold = int.Parse(persona.Properties["Gold"].ToString());
            if (persona.Properties.ContainsKey(CURRENT_STAGE_KEY))
                CurrentStage = int.Parse(persona.Properties[CURRENT_STAGE_KEY].ToString());
            // 系统设置
            if (persona.Properties.ContainsKey("LanguageCode"))
                LanguageCode = persona.Properties["LanguageCode"].ToString();
            if (persona.Properties.ContainsKey("IsMusicOn"))
                IsMusicOn = int.Parse(persona.Properties["IsMusicOn"].ToString()) == 1;
            if (persona.Properties.ContainsKey("IsSoundOn"))
                IsSoundOn = int.Parse(persona.Properties["IsSoundOn"].ToString()) == 1;
            // 游戏进度
            if (persona.Properties.ContainsKey("TutorialProgress"))
                TutorialProgress = int.Parse(persona.Properties["TutorialProgress"].ToString());
            if (persona.Properties.ContainsKey("IsFirstLaunch"))
                IsFirstLaunch = int.Parse(persona.Properties["IsFirstLaunch"].ToString()) == 1;
            if (persona.Properties.ContainsKey("isShowVocabulary"))
                isShowVocabulary = int.Parse(persona.Properties["isShowVocabulary"].ToString()) == 1;
            // 时间数据
            if (persona.Properties.ContainsKey("logoutTime"))
                logoutTime = persona.Properties["logoutTime"].ToString();
            if (persona.Properties.ContainsKey("curIsEnter"))
                curIsEnter = int.Parse(persona.Properties["curIsEnter"].ToString()) == 1;
            // 道具数据 - 需要特殊处理字典类型
            if (persona.Properties.ContainsKey("ToolInfo"))
            {
                string toolInfoJson = persona.Properties["ToolInfo"].ToString();
                if(!string.IsNullOrEmpty(toolInfoJson)&&toolInfoJson!="{}")
                    toolInfo = JsonConvert.DeserializeObject<Dictionary<int, ToolInfo>>(toolInfoJson);
            }
            
            // 签到数据
            if (persona.Properties.ContainsKey("signOpenTime"))
                signOpenTime = persona.Properties["signOpenTime"].ToString();
            if (persona.Properties.ContainsKey("signid"))
                signid = int.Parse(persona.Properties["signid"].ToString());
            if (persona.Properties.ContainsKey("isDayEnterSign"))
                isDayEnterSign = int.Parse(persona.Properties["isDayEnterSign"].ToString()) == 1;
            // 限时活动数据
            if (persona.Properties.ContainsKey("timerePuzzleid"))
                timerePuzzleid = int.Parse(persona.Properties["timerePuzzleid"].ToString());
            if (persona.Properties.ContainsKey("limitOpenTime"))
                limitOpenTime = persona.Properties["limitOpenTime"].ToString();
            if (persona.Properties.ContainsKey("limitMinPeriod"))
                limitMinPeriod = int.Parse(persona.Properties["limitMinPeriod"].ToString());
            if (persona.Properties.ContainsKey("limitEndTime"))
                limitEndTime = persona.Properties["limitEndTime"].ToString();
            if (persona.Properties.ContainsKey("isDayEnterLimint"))
                isDayEnterLimint = int.Parse(persona.Properties["isDayEnterLimint"].ToString()) == 1;
            if (persona.Properties.ContainsKey("timePuzzlecount"))
                timePuzzlecount = int.Parse(persona.Properties["timePuzzlecount"].ToString());
            if (persona.Properties.ContainsKey("isNeedShowHelp"))
                isNeedShowHelp = int.Parse(persona.Properties["isNeedShowHelp"].ToString()) == 1;
            // 每日任务数据
            if (persona.Properties.ContainsKey("completeTaskList"))
            {
                string completeTaskJson = persona.Properties["completeTaskList"].ToString();
                if(!string.IsNullOrEmpty(completeTaskJson)&&completeTaskJson!="{}")
                    completeTaskList = JsonConvert.DeserializeObject<List<CompleteTaskData>>(completeTaskJson);
            }
            if (persona.Properties.ContainsKey("taskSaveDatas"))
            {
                // 修改反序列化代码
                string taskSaveJson = persona.Properties["taskSaveDatas"].ToString();
                if(!string.IsNullOrEmpty(taskSaveJson)&&taskSaveJson!="{}")
                    taskSaveDatas = JsonConvert.DeserializeObject<List<TaskSaveData>>(taskSaveJson);
            }
            if (persona.Properties.ContainsKey("isAllCompleteTask"))
                isAllCompleteTask = int.Parse(persona.Properties["isAllCompleteTask"].ToString()) == 1;
            
            // 词汇数据
            if (persona.Properties.ContainsKey("wordVocabularyChinSim"))
            {
                string vocabularyJson = persona.Properties["wordVocabularyChinSim"].ToString();
                if(!string.IsNullOrEmpty(vocabularyJson)&&vocabularyJson!="{}")
                    wordVocabularyChinSim = JsonConvert.DeserializeObject<WordVocabulary<string>>(vocabularyJson);
            }
        }
        
        // // 加载道具数据
        // LoadToolData();
        //
        // // 加载列表数据
        // LoadListData();
        //
        // // 加载词库数据
        // LoadVocabulary();
        // 检查是否需要重置每日数据
        CheckResetLimitTime();

        SubmitInitialUserData();
    }
    
     // 首次初始化数据提交方法
    private void SubmitInitialUserData()
    {
        // 创建字典并添加键值对（所有值转换为字符串）
        Dictionary<string, string> userData = new Dictionary<string, string>();
        
        // 基础数据
        userData["Gold"] = AppGameSettings.StartingGold.ToString();
        userData["CurrentStage"] = AppGameSettings.FirstLevel.ToString();
        userData["LanguageCode"] = GetLanguage();
        userData["IsMusicOn"] = "1"; // true转换为1
        userData["IsSoundOn"] = "1"; // true转换为1
        
        // 游戏进度
        userData["TutorialProgress"] = "0";
        userData["IsFirstLaunch"] = "1"; // true转换为1
        userData["isShowVocabulary"] = "0"; // false转换为0
        
        // 时间数据
        userData["logoutTime"] = DateTime.Now.ToString();
        userData["curIsEnter"] = "0"; // false转换为0
        
        // 道具数据（JSON序列化）
        Dictionary<int, ToolInfo> initialTools = new Dictionary<int, ToolInfo>
        {
            { 101, new ToolInfo { cost = AppGameSettings.ShopItems.ResetCost, type = "Reset", count = AppGameSettings.ShopItems.StartingResets } },
            { 102, new ToolInfo { cost = AppGameSettings.ShopItems.HintCost, type = "Hint", count = AppGameSettings.ShopItems.StartingHints } },
            { 103, new ToolInfo { cost = AppGameSettings.ShopItems.ButterflyCost, type = "Butterfly", count = AppGameSettings.ShopItems.StartingButterflies } }
        };
        userData["ToolInfo"] = JsonUtility.ToJson(initialTools);
        
        // 签到数据
        userData["signOpenTime"] = ""; // null转换为空字符串
        userData["signid"] = "0";
        userData["isDayEnterSign"] = "1"; // true转换为1
        
        // 限时活动数据
        userData["timerePuzzleid"] = "0";
        userData["limitOpenTime"] = ""; // null转换为空字符串
        userData["limitMinPeriod"] = "0";
        userData["limitEndTime"] = ""; // null转换为空字符串
        userData["isDayEnterLimint"] = "1"; // true转换为1
        userData["timePuzzlecount"] = "0";
        userData["isNeedShowHelp"] = "1"; // true转换为1
        
        // 每日任务数据（JSON序列化）
        userData["completeTaskList"] = JsonUtility.ToJson(new List<CompleteTaskData>());
        userData["taskSaveDatas"] = JsonUtility.ToJson(new List<TaskSaveData>());
        userData["isAllCompleteTask"] = "0"; // false转换为0
        
        // 词汇数据（JSON序列化）
        userData["wordVocabularyChinSim"] = JsonUtility.ToJson(new WordVocabulary<string>());
        
        // 调用更新接口
        UIController.Instance.UpdateUserInfo(userData);
    }

    #endregion

    #region 数据维护方法
    
    // /// <summary>
    // /// 加载道具数据
    // /// </summary>
    // private void LoadToolData()
    // {
    //     toolInfo = new Dictionary<int, ToolInfo>
    //     {
    //         { 101, LoadSingleTool(101,20,"Reset") },
    //         { 102, LoadSingleTool(102,50,"Hint") },
    //         { 103, LoadSingleTool(103,60,"Butterfly") }
    //     };
    // }
    
    // /// <summary>
    // /// 加载单个道具数据
    // /// </summary>
    // private ToolInfo LoadSingleTool(int toolId,int cost,string type)
    // {
    //     string keyPrefix = $"{TOOL_KEY_PREFIX}{toolId}_";
    //     return new ToolInfo
    //     {
    //         cost = PlayerPrefs.GetInt(keyPrefix + "cost", cost),
    //         type = PlayerPrefs.GetString(keyPrefix + "type", type),
    //         count = PlayerPrefs.GetInt(keyPrefix + "count", 1),
    //         addcount = PlayerPrefs.GetInt(keyPrefix + "addcount", 0),
    //         reducecount = PlayerPrefs.GetInt(keyPrefix + "reducecount", 0)
    //     };
    // }
    
    
    // /// <summary>
    // /// 加载列表数据
    // /// </summary>
    // private void LoadListData()
    // {
    //     // 加载完成任务列表
    //     completeTaskList = LoadList<CompleteTaskData>("CompleteTask");
    //     
    //     // 加载任务数据
    //     taskSaveDatas = LoadList<TaskSaveData>("TaskSaveData");
    //     
    //     // 加载限时商店数据
    //     //limitShopItems = LoadList<ShopLimitData>("LimitShopItems");
    //     
    //     // 加载通关时间数据
    //     //passLevelUseTime = LoadDictionary<int, int>("PassLevelUseTime");
    // }
    
    // /// <summary>
    // /// 加载词库数据
    // /// </summary>
    // private void LoadVocabulary()
    // {
    //     // wordVocabularyJan = LoadVocabulary("JanVocabulary");
    //     // wordVocabularyChinTra = LoadVocabulary("ChinTraVocabulary");
    //     wordVocabularyChinSim = LoadVocabulary("ChinSimVocabulary");
    // }
    
    // /// <summary>
    // /// 加载词库
    // /// </summary>
    // private WordVocabulary<string> LoadVocabulary(string key)
    // {
    //     string json = PlayerPrefs.GetString(key, null);
    //     if (!string.IsNullOrEmpty(json))
    //     {
    //         try
    //         {
    //             return JsonConvert.DeserializeObject<WordVocabulary<string>>(json);
    //         }
    //         catch
    //         {
    //             // 解析失败时返回新对象
    //             return new WordVocabulary<string>();
    //         }
    //     }
    //     return new WordVocabulary<string>();
    // }
    
    // /// <summary>
    // /// 加载列表
    // /// </summary>
    // private List<T> LoadList<T>(string key)
    // {
    //     string json = PlayerPrefs.GetString(key, null);
    //     if (!string.IsNullOrEmpty(json))
    //     {
    //         try
    //         {
    //             return JsonConvert.DeserializeObject<List<T>>(json);
    //         }
    //         catch
    //         {
    //             // 解析失败时返回新列表
    //             return new List<T>();
    //         }
    //     }
    //     return new List<T>();
    // }
    
    // /// <summary>
    // /// 加载字典
    // /// </summary>
    // private Dictionary<K, V> LoadDictionary<K, V>(string key)
    // {
    //     string json = PlayerPrefs.GetString(key, null);
    //     if (!string.IsNullOrEmpty(json))
    //     {
    //         try
    //         {
    //             return JsonConvert.DeserializeObject<Dictionary<K, V>>(json);
    //         }
    //         catch
    //         {
    //             // 解析失败时返回新字典
    //             return new Dictionary<K, V>();
    //         }
    //     }
    //     return new Dictionary<K, V>();
    // }

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
        
        //butterflyTaskIsOpen=false;
        completeTaskList = new List<CompleteTaskData>();
        //taskButterflyUseMinutes = 0;
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
        //dayPassStageCount = 0;
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
    /// 保存用户数据 - 现在只保存变化的部分
    /// </summary>
    public void SaveData()
    {
        try
        {
            // 更新登出时间
            if (!string.IsNullOrEmpty(logoutTime) && DateTime.Now > DateTime.Parse(logoutTime))
            {
                logoutTime = DateTime.Now.ToString();
                SaveString("logoutTime", logoutTime);
            }
            
            // 更新在线时长
            //UpdateOnlineStageTime();
            
            // 标记非首次进入
            if (IsFirstLaunch)
            {
                IsFirstLaunch = false;
                SaveBool("IsFirstLaunch", IsFirstLaunch);
            }

            // 保存版本号
            PlayerPrefs.SetInt(USER_DATA_VERSION_KEY, CURRENT_DATA_VERSION);
            PlayerPrefs.Save();
        }
        catch (Exception ex)
        {
            Debug.LogError($"保存用户数据失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 保存字符串
    /// </summary>
    private void SaveString(string key, string value)
    {
        PlayerPrefs.SetString(key, value);
        PlayerPrefs.Save();
        
        // 创建字典并添加键值对（整数需转换为字符串）
        Dictionary<string, string> userData = new Dictionary<string, string>();
        userData[key] = value.ToString();  // 关键转换

        // 调用更新接口
        UIController.Instance.UpdateUserInfo(userData);
    }
    
    /// <summary>
    /// 保存整数
    /// </summary>
    private void SaveInt(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
        PlayerPrefs.Save();
        
        // 创建字典并添加键值对（整数需转换为字符串）
        Dictionary<string, string> userData = new Dictionary<string, string>();
        userData[key] = value.ToString();  // 关键转换

        // 调用更新接口
        UIController.Instance.UpdateUserInfo(userData);
    }
    
    /// <summary>
    /// 保存布尔值
    /// </summary>
    private void SaveBool(string key, bool value)
    {
        int tvalue = value ? 1 : 0;
        
        PlayerPrefs.SetInt(key, tvalue);
        PlayerPrefs.Save();
        
        // 创建字典并添加键值对（整数需转换为字符串）
        Dictionary<string, string> userData = new Dictionary<string, string>();
        userData[key] = tvalue.ToString();  // 关键转换

        // 调用更新接口
        UIController.Instance.UpdateUserInfo(userData);
    }
    
    /// <summary>
    /// 保存列表
    /// </summary>
    private void SaveList<T>(string key, List<T> list)
    {
        string json = JsonConvert.SerializeObject(list);
        SaveString(key, json);
        
        // 创建字典并添加键值对（整数需转换为字符串）
        Dictionary<string, string> userData = new Dictionary<string, string>();
        userData[key] = json;  // 关键转换
        
        // 调用更新接口
        UIController.Instance.UpdateUserInfo(userData);
    }
    
    
    /// <summary>
    /// 保存字典
    /// </summary>
    private void SaveDictionary<K, V>(string key, Dictionary<K, V> dict)
    {
        string json = JsonConvert.SerializeObject(dict);
        SaveString(key, json);
    }
    
    /// <summary>
    /// 保存词库
    /// </summary>
    private void SaveVocabulary(string key, WordVocabulary<string> vocabulary)
    {
        string json = JsonConvert.SerializeObject(vocabulary);
        SaveString(key, json);
    }
    
    /// <summary>
    /// 保存单个道具
    /// </summary>
    private void SaveTool()
    {
        SaveList("toolList", toolInfo.ToList());
    }


    #endregion

    #region 游戏数据操作方法
    
   
    public void UdpateTimePuzzleCount(int value)
    {
        timePuzzlecount+=value;
        SaveInt("timePuzzlecount", timePuzzlecount);
    }

    /// <summary>
    /// 新增任务数据
    /// </summary>
    public void UpdateDailyTaskData(TaskSaveData taskSave)
    {
        taskSaveDatas.Add(taskSave);
        SaveTaskData();
    }

    public void SaveTaskData()
    {
        SaveList("taskSaveDatas", taskSaveDatas);
    }

    /// <summary>
    /// 更新关卡进度
    /// </summary>
    /// <param name="value">变化值</param>
    /// <param name="isSet">是否直接设置值</param>
    /// <summary>
    /// 更新关卡进度
    /// </summary>
    public void UpdateStage(int value = 1, bool isSet = false)
    {
        CurrentStage = isSet ? value : CurrentStage + value;
        SaveInt(CURRENT_STAGE_KEY, CurrentStage);
        
        Debug.Log($"关卡更新: {(isSet ? "设置为" : "增加")}{value}, 当前关卡: {CurrentStage}");
    }

    public void UpdateTutorialProgress()
    {
        TutorialProgress +=1;
        SaveInt("TutorialProgress", TutorialProgress);
    }

    /// <summary>
    /// 更新金币数量
    /// </summary>
    public void UpdateGold(int value, bool isanim = false, bool updateui = true, string message = "")
    {
        //int oldGold = Gold;
        Gold += value;
        
        SaveInt("Gold", Gold);
        
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
        
        SaveString("signOpenTime", signOpenTime);
        SaveBool("isDayEnterSign", isDayEnterSign);
    }
    
    /// <summary>
    /// 更新限时活动进度id
    /// </summary>
    public void UpdateSignid()
    {
        signid++;
        SaveInt("signid", signid);
        
        if (string.IsNullOrEmpty(signOpenTime))
        {
            signOpenTime = DateTime.Now.ToString();
            SaveString("signOpenTime", signOpenTime);
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
        
        SaveList("CompleteTask", completeTaskList);
    }
    
    /// <summary>
    /// 更新所有任务完成数据
    /// </summary>
    public void UpdateAllCompleteTask()
    {
        isAllCompleteTask = true;
        SaveBool("isAllCompleteTask", isAllCompleteTask);
    }
    
    /// <summary>
    /// 更新限时活动进度id
    /// </summary>
    public void UpdateLImitid()
    {
        timerePuzzleid++;
        SaveInt("timerePuzzleid", timerePuzzleid);
        
        if (string.IsNullOrEmpty(limitOpenTime))
        {
            limitOpenTime = DateTime.Now.ToString();
            SaveString("limitOpenTime", limitOpenTime);
        }
    }
    
    /// <summary>
    /// 每日首次开启限时活动
    /// </summary>
    public void EveryDayOpenLimit()
    {
        limitOpenTime = DateTime.Now.ToString();
        isDayEnterLimint = false;
        
        SaveString("limitOpenTime", limitOpenTime);
        SaveBool("isDayEnterLimint", isDayEnterLimint);
    }
    
    /// <summary>
    /// 更新限时翻译结束时间
    /// </summary>
    public void UpdateLimitEndTime(int minutes)
    {
        limitEndTime = DateTime.Now.AddMinutes(minutes).ToString();
        UpdatelimitMinPeriod(minutes);
        
        SaveString("limitEndTime", limitEndTime);
    }
    
    /// <summary>
    /// 更新限时翻倍周期
    /// </summary>
    public void UpdatelimitMinPeriod(int minutes)
    {
        limitMinPeriod = minutes;
        SaveInt("limitMinPeriod", limitMinPeriod);
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
            
            // 保存单个道具
            SaveTool();
            
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
            SaveVocabulary(GetVocabularyKey(), vocabulary);
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
            SaveVocabulary(GetVocabularyKey(), vocabulary);
            
            if (!isShowVocabulary)
            {
                isShowVocabulary = true;
                SaveBool("isShowVocabulary", isShowVocabulary);
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
            SaveVocabulary(GetVocabularyKey(), vocabulary);
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
        SaveVocabulary(GetVocabularyKey(), vocabulary);
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