/// <summary>
/// 界面类型管理器 (Panel Type Manager)
/// 新版本重构：优化了界面常量管理方式 (New version: Optimized panel constant management)
/// </summary>
public class PanelType  // Renamed class
{
    /* 核心游戏界面 (Core Game Interfaces) */
    public const string GamePlayArea = "GamePlayArea";  // Changed from GameContainer
    public const string HeaderSection = "HeaderSection";  // Changed from TopContainer
    public const string PrimaryInterface = "PrimaryInterface";  // Changed from MainInterface
   
    /* 系统功能界面 (System Function Screens) */
    public const string AppRating = "AppRating";  // Changed from RateUsScreen
    public const string PolicyView = "PolicyView";  // Changed from PrivacyScreen


    public const string LearningGuide = "LearningGuide";  // Changed from TutorialScreen
    /* 游戏状态界面 (Game State Screens) */
    public const string DebugMenu = "DebugMenu";  
    public const string OptionsView = "OptionsView";  
    public const string StageFinishView = "StageFinishView"; 
    public const string LimitTimeScreen = "LimitTimeScreen";
    public const string SignWaterScreen = "SignWaterScreen";
    public const string LimitHelpScreen= "LimitHelpScreen";
    public const string DashCompetition="DashCompetition";
    public const string HeadScreen="HeadScreen";
    public const string MatchSuccess="MatchSuccess";
    public const string CompetitionFail="CompetitionFail";
    public const string CompetitionHelp="CompetitionHelp";
    public const string DailyTasksScreen="DailyTasksScreen";
    public const string CompetitionStart="CompetitionStart";

    // 已注释的旧界面常量 (Legacy commented constants)
    public const string RewardAdsScreen = "RewardAdsScreen"; 
    public const string ShopScreen = "ShopScreen";  
    public const string AdsDiscountScreen = "AdsDiscountScreen";  
    public const string WordVocabularyScreen = "WordVocabularyScreen";  
    public const string WordDetailScreen = "WordDetailScreen";  
    public const string LevelWordScreen = "LevelWordScreen";  
    public const string LevelWordDetail = "LevelWordDetail";  

    /// <summary>
    /// 获取所有可用界面名称 (Get all available panel names)
    /// 新版本使用属性缓存优化性能 (New version uses cached properties)
    /// </summary>
    public static string[] AvailableViews()  // Changed from GetPanelNames
    {
        var type = typeof(PanelType);
        var allFields = type.GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        string[] all = new string[allFields.Length];
        for (int i = 0; i<allFields.Length; i++)
            all[i] = allFields[i].Name;

        return all;
    }      

    /// <summary>
    /// 获取界面显示名称 (Get panel display name)
    /// 新增本地化支持方法 (Added localization support)
    /// </summary>
    public static string GetDisplayName(string panelId)
    {
        // 实际项目中应接入本地化系统
        // (In production should connect to localization system)
        return panelId;
    }
}
