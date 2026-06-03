using System;
using System.Collections.Generic;
using Middleware;
using UnityEngine;

public partial class AnalyticMgr
{
    #region 进度相关
    private static DateTime? _startTime;// 使用 记录开始时间
    
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
        SetLogoutProperties();
    }

    /// <summary>
    /// 登录时用户数据
    /// </summary>
    private static void SetLoginProperties()
    {
        var userData = GameDataManager.Instance.UserData;
        var now = DateTime.Now;
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

            userData.totallogin++;
            userData.lastLoginDay = today.ToString("yyyy-MM-dd");
        }

        // 记录本次登录开始时间
        _startTime = now;

        // 计算生命周期天数（基于首次登录时间）
        int lifeDays = 0;
        if (!string.IsNullOrEmpty(userData.firstLoginTime) &&
            DateTime.TryParse(userData.firstLoginTime, out var firstLoginDate))
        {
            lifeDays = (today - firstLoginDate.Date).Days+ 1; // +1 表示第1天
            Debug.Log("当前时间："+today.ToString("yyyy-MM-dd HH:mm:ss")+"周期天数 life:"+lifeDays);
        }
        
        var properties = new Dictionary<string, object>
        {
            //时间类
            { "first_login_time",userData.firstLoginTime},
            { "last_login_time", now.ToString("yyyy-MM-dd HH:mm:ss")},
            { "first_pay_time", GameDataManager.Instance.UserData.firstPayTime},
            { "last_pay_time", GameDataManager.Instance.UserData.lastPayTime},
            //累积类
            { "total_revenue", GameDataManager.Instance.UserData.TotalRevenue },
            { "total_login", GameDataManager.Instance.UserData.totallogin},
            { "total_pay_times", GameDataManager.Instance.UserData.TotalPayTimes },
            { "total_ad_times", GameDataManager.Instance.UserData.totalSeeAds},
            { "total_item_cost", GameDataManager.Instance.UserData.GetTotalToolCost()},
            { "active_day", GameDataManager.Instance.UserData.activeDayCnt},
            { "life_day", lifeDays},
        };
        Game.self?.Analytics.SetUserProperty(properties, Define.DataTarget.Think);
        
        Game.self?.Analytics.LogEvent("ta_app_start", Define.DataTarget.Think);
        
        Game.self.Attributes.ReportConversion(133054104);
    }

    /// <summary>
    /// 登出时用户数据
    /// </summary>
    private static void SetLogoutProperties()
    {
        var properties = new Dictionary<string, object>
        {
            //资源类
            { "current_coin", GameDataManager.Instance.UserData.Gold },
            { "current_tipItem", GameDataManager.Instance.UserData.toolInfo[102].count },
            { "current_resetItem", GameDataManager.Instance.UserData.toolInfo[101].count },
            { "current_flyItem", GameDataManager.Instance.UserData.toolInfo[103].count },
            { "current_level", GameDataManager.Instance.UserData.CurrentHexStage },
        };
        Game.self.Analytics?.SetUserProperty(properties, Define.DataTarget.Think);
        
        //处理异常，确保_startTime有值
        if (!_startTime.HasValue)
        {
            _startTime = DateTime.Now;
        }
        TimeSpan span = new TimeSpan(DateTime.Now.Ticks - _startTime.Value.Ticks);
        float durationSeconds = Math.Max(0, (float)span.TotalSeconds); // 确保非负
        
        var outproperties = new Dictionary<string, object>(){{"#duration", durationSeconds.ToString("0.00")}};
        Game.self.Analytics?.LogEvent("ta_app_end",outproperties, Define.DataTarget.Think);
    }
    
    /// <summary>
    /// 公共事件属性
    /// </summary>
    public static void SetCommonProperties()
    {
        
        var userData = GameDataManager.Instance.UserData;
        if(userData==null)  return;
        
        int levelId = GameDataManager.Instance.UserData.CurrentHexStage;
        
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

        // 计算生命周期天数（基于首次登录时间）
        int lifeDays = 0;
        if (!string.IsNullOrEmpty(userData.firstLoginTime) &&
            DateTime.TryParse(userData.firstLoginTime, out var firstLoginDate))
        {
            lifeDays = (DateTime.Now.Date - firstLoginDate.Date).Days+ 1; // +1 表示第1天
        }
        
        if (lifeDays == 2)
        {
            Game.self.Attributes.ReportConversion(133054105);
        }
     
        var properties = new Dictionary<string, object>
        {
            {"gold", GameDataManager.Instance.UserData.Gold },
            {"tipItem",GameDataManager.Instance.UserData.toolInfo[102].count},
            {"resetItem",GameDataManager.Instance.UserData.toolInfo[101].count},
            {"flyItem",GameDataManager.Instance.UserData.toolInfo[103].count},
            {"level_id",levelId},
            {"level_type",GameDataManager.Instance.UserData.GetLevelMode()},
            { "active_day_event", userData.activeDayCnt},
            { "life_day_event", lifeDays}
        };
            Game.self.Analytics.SetCommonProperties(properties);
    }

    public static void OnAnalyticsStart()
    {
        if (!GameDataManager.Instance.UserData.Rigister)
        {      
            Game.self.Analytics.LogEvent("ta_app_install", Define.DataTarget.Think);
            Game.self.Analytics.LogEvent("ta_app_startFirst", Define.DataTarget.Think);
            Game.self.Analytics.LogEvent("register", Define.DataTarget.Think);
            GameDataManager.Instance.UserData.Rigister = true;
        }
        
        SetCommonProperties();
        SetLoginProperties();
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
            Debug.Log("赋值中用户唯一id为："+uid);
            GameDataManager.Instance.UserData.UserId = uid;
            Game.self.Analytics.Login(GameDataManager.Instance.UserData.UserId);
        }
        
        Debug.Log("赋值后用户唯一id为："+ GameDataManager.Instance.UserData.UserId);
        OnAnalyticsStart();
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
    
    public static void LevelStart()
    {
        Game.self.Analytics.LogEvent("level_start",Define.DataTarget.Think);
    }
    
    public static void LevelProgress(int wordIndex,string word,float duration,int errorCount,int combo,int userToolCount)
    {
        if (GameDataManager.Instance.UserData.CurrentHexStage > 100) 
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
    
    public static void LevelCompleted(float duration)
    {
        var thproperties = new Dictionary<string, object>
        {
            {"lv_duration", duration}
        };
        Game.self.Analytics.LogEvent("level_completed",thproperties,Define.DataTarget.Think);
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
            {"resource_id", resName},
            {"change_type", "获得"},
            {"change_num",changeNum},
            {"change_reason",reason},
        }; 
        Game.self.Analytics.LogEvent("resource_change", properties, Define.DataTarget.Think);
    }
    
    /// <summary>
    /// 减少资源与道具
    /// </summary>
    public static void ResourceReduce(string resId,int changeNum,string reason)
    {
        var properties = new Dictionary<string, object>
        {
            {"resource_id",resId},
            {"change_type","消耗"},
            {"change_num",changeNum},
            {"change_reason",reason},
        }; 
        Game.self.Analytics.LogEvent("resource_change", properties, Define.DataTarget.Think);
    }
    #endregion
    
    
    
}