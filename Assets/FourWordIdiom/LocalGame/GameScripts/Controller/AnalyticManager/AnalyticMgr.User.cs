using System;
using System.Collections.Generic;
using Middleware;
using UnityEngine;

public partial class AnalyticMgr
{
    #region 进度相关
    private static DateTime _startTime;
    public static void GameStart()
    {
#if UNITY_ANDROID
        _startTime = DateTime.Now;
        var key = "start";
        var timeFormat = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        if (!PlayerPrefs.HasKey("GameFirstStart"))
        {
            PlayerPrefs.SetInt("GameFirstStart", 1);
            key = "start_first";
            Game.Analytics.SetUserProperty("player_id",GameDataManager.Instance.UserData.UserId,Define.DataTarget.Firebase);
            Game.Analytics.SetUserProperty("first_start_time",timeFormat,Define.DataTarget.Firebase);
        }
        Game.Analytics.SetUserProperty("last_start_time",timeFormat,Define.DataTarget.Firebase);
        Game.Analytics.LogEvent(key, Define.DataTarget.Firebase);
#endif
        SetLoginProperties();
    }

    public static void GameEnd()
    {
        if(GameDataManager.Instance == null) return;
        SetLogoutProperties();
#if UNITY_ANDROID
       var duration = (DateTime.Now - _startTime).TotalSeconds;
        Game.Analytics.LogEvent("end", "duration", duration, Define.DataTarget.Firebase);
#endif
    }

    private static void SetLoginProperties()
    {
        var span = new TimeSpan(DateTime.Now.Ticks - GameDataManager.Instance.UserData.firstLoginStamp);
        var firstLoginTime = new DateTime(GameDataManager.Instance.UserData.firstLoginStamp);
        var properties = new Dictionary<string, object>
        {
            //时间类
            { "first_login_time", firstLoginTime.ToString("yyyy-MM-dd HH:mm:ss")},
            { "last_login_time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
            { "first_pay_time", GameDataManager.Instance.UserData.firstPayTime},
            { "last_pay_time", GameDataManager.Instance.UserData.lastPayTime},
            //累积类
            { "total_revenue", GameDataManager.Instance.UserData.TotalRevenue },
            { "total_login", GameDataManager.Instance.UserData.totallogin},
            { "total_pay_times", GameDataManager.Instance.UserData.TotalPayTimes },
            { "total_ad_times", GameDataManager.Instance.UserData.totalSeeAds},
            { "total_item_cost", GameDataManager.Instance.UserData.GetTotalToolCost()},
            { "active_day", GameDataManager.Instance.UserData.activeDayCnt},
            { "life_day", span.Days + 1},
        };
        Game.Analytics.SetUserProperty(properties, Define.DataTarget.Think);
        
        //Game.Analytics.LogEvent("ta_app_start", Define.DataTarget.Think);
    }

    private static void SetLogoutProperties()
    {
        if(GameDataManager.Instance.UserData == null) return;
        
        var properties = new Dictionary<string, object>
        {
            //资源类
            { "current_coin", GameDataManager.Instance.UserData.Gold },
            { "current_tipItem", GameDataManager.Instance.UserData.toolInfo[102].count },
            { "current_resetItem", GameDataManager.Instance.UserData.toolInfo[101].count },
            { "current_flyItem", GameDataManager.Instance.UserData.toolInfo[103].count },
            { "current_level", GameDataManager.Instance.UserData.CurrentHexStage },
        };
        Game.Analytics.SetUserProperty(properties, Define.DataTarget.Think);
        
        //Game.Analytics.LogEvent("ta_app_end", Define.DataTarget.Think);
    }
    
    public static void SetCommonProperties()
    {
        int levelId = 0;
        if (GameDataManager.Instance.UserData.levelMode == 1)
        {
            levelId = GameDataManager.Instance.UserData.CurrentHexStage;
        }else if (GameDataManager.Instance.UserData.levelMode == 2)
        {
            levelId = GameDataManager.Instance.UserData.CurrentChessStage;
        }
            var properties = new Dictionary<string, object>
        {
            {"gold", GameDataManager.Instance.UserData.Gold },
            {"tipItem",GameDataManager.Instance.UserData.toolInfo[102].count},
            {"resetItem",GameDataManager.Instance.UserData.toolInfo[101].count},
            {"flyItem",GameDataManager.Instance.UserData.toolInfo[103].count},
            {"level_id",levelId},
            {"level_type",GameDataManager.Instance.UserData.GetLevelMode()}
        };
            Game.Analytics.SetCommonProperties(properties);
    }

    public static void OnAnalyticsSdkInit(object sender, EventArgs e)
    {
        var uid = Game.GetUniqueId();
        var cacheUid = GameDataManager.Instance.UserData.UserId;
        if (string.IsNullOrEmpty(cacheUid) || cacheUid != uid)
        {
            GameDataManager.Instance.UserData.UserId = uid;
            Game.Analytics.Login(GameDataManager.Instance.UserData.UserId);
        }
        
        if (!GameDataManager.Instance.UserData.Rigister)
        {      
            Game.Analytics.LogEvent("ta_app_startFirst", Define.DataTarget.Think);
            Game.Analytics.LogEvent("register", Define.DataTarget.Think);
            GameDataManager.Instance.UserData.Rigister = true;
            GameDataManager.Instance.UserData.firstLoginStamp = DateTime.Now.Ticks;
        }
        
        if (GameDataManager.Instance.UserData.lastLoginDay != DateTime.Now.ToString("yyyy-MM-dd"))
            GameDataManager.Instance.UserData.activeDayCnt ++;
        GameDataManager.Instance.UserData.lastLoginDay = DateTime.Now.ToString("yyyy-MM-dd");
        
        SetCommonProperties();
        SetLoginProperties();
        Game.Analytics.LogEvent("login", Define.DataTarget.Think);
    }
    
    public static void GuideBegin()
    {
        int mode = GameDataManager.Instance.UserData.levelMode;
        int id = 0;
        if (mode == 1)
            id = GameDataManager.Instance.UserData.TutorialProgress + 1;
        else if (mode == 2)
            id = ChessGuideSystem.Instance.currentTutorial;
        var properties = new Dictionary<string, object>(){{"guide_step", id}};
        Game.Analytics.LogEvent("guide_begin", properties, Define.DataTarget.Think);
    }
    
    public static void GuideComplete()
    {
        int mode = GameDataManager.Instance.UserData.levelMode;
        int id = 0;
        if (mode == 1)
            id = GameDataManager.Instance.UserData.TutorialProgress + 1;
        else if(mode == 2)
            id = ChessGuideSystem.Instance.currentTutorial;
        var properties = new Dictionary<string, object>{{"guide_step", id}};
        Game.Analytics.LogEvent("guide_complete", properties, Define.DataTarget.Think);
    }
    
    public static void LevelStart()
    {
        Game.Analytics.LogEvent("level_start",Define.DataTarget.Think);
        
#if UNITY_ANDROID
        
        var properties = new Dictionary<string, object>
        {
            {"lv_type",GameDataManager.Instance.UserData.levelMode},
            {"level_name",GameDataManager.Instance.UserData.CurrentHexStage}
        };
        Game.Analytics.LogEvent("level_start", properties,Define.DataTarget.Firebase);
#endif
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
        Game.Analytics.LogEvent("level_progress", properties, Define.DataTarget.Think);
    }
    
    public static void LevelCompleted(float duration)
    {
        var thproperties = new Dictionary<string, object>
        {
            {"lv_duration", duration}
        };
        Game.Analytics.LogEvent("level_completed",thproperties,Define.DataTarget.Think);
        
#if UNITY_ANDROID
        
        var properties = new Dictionary<string, object>()
        {
            {"lv_type",GameDataManager.Instance.UserData.levelMode},
            {"level_name",GameDataManager.Instance.UserData.CurrentHexStage}
        };
        Game.Analytics.LogEvent("level_completed", properties,Define.DataTarget.Firebase);
#endif
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
        Game.Analytics.LogEvent("change_role_head", properties, Define.DataTarget.Think);
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
        Game.Analytics.LogEvent("resource_change", properties, Define.DataTarget.Think);
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
        Game.Analytics.LogEvent("resource_change", properties, Define.DataTarget.Think);
    }
    #endregion
    
    
    
}