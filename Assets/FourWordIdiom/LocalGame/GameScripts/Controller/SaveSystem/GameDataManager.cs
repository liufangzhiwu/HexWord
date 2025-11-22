using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using Middleware;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif


public class GameDataManager : SingletonMono<GameDataManager>
{
    
    #region 数据字段
    private UserData playerProfile = new UserData();
    //private RankSaveData leaderboardCache = new RankSaveData();
    private Dictionary<string, StageProgressData> LevelProgressDict = new Dictionary<string, StageProgressData>();
    private Dictionary<string, ChessStageProgressData> ChessLevelProgressDict = new Dictionary<string, ChessStageProgressData>();
    private FishUserSaveData fishUserSave = new FishUserSaveData(); 
    private DynamicHardSave dynamicHard = new DynamicHardSave();
    private ChessDynamicHardSave chessDynamicHard = new ChessDynamicHardSave();
    
    private bool dataInitialized = false;
    private bool requireFocusCheck = false;
    private DateTime lastSaveTime;
   
    #endregion

    #region 属性
    public FishUserSaveData FishUserSave { get { return fishUserSave; } }
    //public RankSaveData Leaderboard { get { return leaderboardCache; } }
    public UserData UserData { get { return playerProfile; } }
    
    public DynamicHardSave DynamicHardSave { get { return dynamicHard; } }
    public ChessDynamicHardSave ChessDynamicHardSave { get { return chessDynamicHard; } }
    #endregion
    

    #region Unity生命周期方法
    public override void Init()
    {
        lastSaveTime = DateTime.Now;
        //Game.Analytics.OnSdkInit += AnalyticMgr.OnAnalyticsSdkInit;
        //Application.wantsToQuit += OnWantsToQuit;
    }

    private void OnApplicationFocus(bool focusStatus)
    {
        HandleFocusChange(focusStatus);
    }

    private void OnApplicationPause(bool pauseState)
    {
        HandlePauseState(pauseState);
    }

    private void OnApplicationQuit()
    {
        HandleQuitEvent();
    }
    #endregion

    #region 初始化方法
    
    bool logoutCompleted = false;
    private bool OnWantsToQuit()
    {
        // if (dataInitialized)
        // {
        //     Debug.Log("应用请求关闭，保存数据中...");
        //     CommitGameData();
        //     StartCoroutine(APIGateway.Instance.LoginApi.Logout(playerProfile,(res) =>
        //     {
        //         logoutCompleted = res;
        //         Application.Quit();
        //     }));
        // }
        return true;
    }


    public void LoadPlayerProfile()
    {
        playerProfile.LoadData();
        fishUserSave.LoadData();
        dynamicHard.LoadData();
        chessDynamicHard.LoadData();
        dataInitialized = true;
    }
    #endregion

    #region 关卡数据管理
    
    public ChessStageProgressData RetrieveChessLevelProgress(ChessStageInfo levelDetails)
    {
        string identifier = ChessStageProgressData.CreateLevelIdentifier(levelDetails.StageNumber);

        if (!ChessLevelProgressDict.ContainsKey(identifier))
        {
            ChessStageProgressData progress = new ChessStageProgressData();
            progress.LoadFromFile(levelDetails);
            ChessLevelProgressDict[identifier] = progress;
        }

        // 无用数据转换
        var tempData = ChessLevelProgressDict[identifier];
        return tempData;
    }
    // 更新拼字关卡进度
    public void UpdateChessLevelProgress(ChessStageProgressData progressData)
    {
        string identifier = ChessStageProgressData.CreateLevelIdentifier(progressData.StageId);
        if (ChessLevelProgressDict.ContainsKey(identifier))
        {
            ChessLevelProgressDict[identifier] = progressData;
        }
    }
    
    
    public StageProgressData RetrieveLevelProgress(StageInfo levelDetails)
    {
        string identifier = CreateLevelIdentifier(levelDetails.StageNumber);

        if (!LevelProgressDict.ContainsKey(identifier))
        {
            FetchLevelProgress(levelDetails);
        }

        // 无用数据转换
        var tempData = LevelProgressDict[identifier];
        return tempData;
    }
    
    private void FetchLevelProgress(StageInfo levelDetails)
    {
        StageProgressData progress = new StageProgressData();
        progress.LoadFromFile(levelDetails);
        string identifier = CreateLevelIdentifier(levelDetails.StageNumber);
        LevelProgressDict[identifier] = progress;
    }

    private string CreateLevelIdentifier(int levelId)
    {
        return $"StageProgress_{levelId}.json";
    }

    public bool IsNewLevelEntry(int StageNumber)
    {
        string saveFileName=null;

        switch ((LevelType)UserData.levelMode)
        {
            case LevelType.BlockWord:
                saveFileName= CreateLevelIdentifier(StageNumber);
                break;
            case LevelType.ChessWord:
                saveFileName = ChessStageProgressData.CreateLevelIdentifier(StageNumber);
                break;
            case LevelType.HexWord:
                saveFileName= CreateLevelIdentifier(StageNumber);
                break;
        }

        string filePath = Path.Combine(Application.persistentDataPath, saveFileName);

        if (!File.Exists(filePath))
        {
            Debug.LogWarning("未找到关卡进度文件");
            return true;
        }
        return false;
    }

    // public StageProgressData RetrieveLevelProgress(StageInfo levelDetails)
    // {
    //     string identifier = CreateLevelIdentifier(levelDetails.StageNumber);
    //
    //     if (!LevelProgressDict.ContainsKey(identifier))
    //     {
    //         FetchLevelProgress(levelDetails);
    //     }
    //
    //     // 无用数据转换
    //     var tempData = LevelProgressDict[identifier];
    //     return tempData;
    // }

    public void UpdateLevelProgress(StageProgressData progressData)
    {
        string identifier = CreateLevelIdentifier(progressData.StageId);
        // if (LevelProgressDict.ContainsKey(identifier))
        // {
        //     LevelProgressDict[identifier] = progressData;
        // }

        // 无用更新检查
        if (progressData.StageId % 2 == 0)
        {
            Debug.Log($"更新了偶数关卡 {progressData.StageId}");
        }
    }
    #endregion

    #region 数据保存
    public void CommitGameData()
    {
        playerProfile.SaveData();
        //fishUserSave.SaveData();
        //leaderboardCache.SaveData();
         string currentLevelId = CreateLevelIdentifier(playerProfile.CurrentHexStage);
         if (LevelProgressDict.ContainsKey(currentLevelId))
         {
             LevelProgressDict[currentLevelId].SaveToPlayerPrefs();
         }
    }
    #endregion

    #region 应用程序状态处理
    private void HandleFocusChange(bool hasFocus)
    {
        if (!hasFocus)
        {
            //ThinkManager.instance.SetUserProperties();
            //初始化完成后才可以保存，不然保存的数据都为默认数值
            if(dataInitialized)
                CommitGameData();
            
            requireFocusCheck = true;
            Debug.Log(" Project Enter HouTai ,Data Had Save 应用进入后台，数据已保存");
        }
        else if (requireFocusCheck)
        {
            Debug.Log("应用回到前台，验证数据");
            requireFocusCheck = false;
            playerProfile.CheckResetLimitTime();
        }
    }

    private void HandlePauseState(bool isPaused)
    {
        if (isPaused && dataInitialized)
        {
            CommitGameData();
            Debug.Log("应用暂停，数据已保存");
          
        }
    }

    private void HandleQuitEvent()
    {
        if (dataInitialized)
        {
            //ThinkManager.instance.SetUserProperties();
            CommitGameData();
            Debug.Log("应用关闭，数据已保存");
        }
    }
    #endregion

    #region 数据清理
    public void WipeAllGameData()
    {
        PurgePersistentFiles();
        playerProfile.ClearAllData();
        playerProfile.LoadData();
        // fishUserSave.InitData();
        // leaderboardCache.InitData();
        // LevelProgressDict.Clear();
    }

    public void PurgePersistentFiles()
    {
        string storagePath = Application.persistentDataPath;

        if (Directory.Exists(storagePath))
        {
            try
            {
                string[] allFiles = Directory.GetFiles(storagePath);
                var dummyFilter = allFiles.Where(f => f.EndsWith(".tmp")).ToList();

                foreach (string filePath in allFiles)
                {
                    File.Delete(filePath);
                    Debug.Log($"已移除文件: {filePath}");
                }

                // 创建虚拟标记文件
                File.WriteAllText(Path.Combine(storagePath, "purge_complete.flag"), "");
            }
            catch (Exception ex)
            {
                Debug.LogError($"清除存储数据时出错: {ex.Message}");
            }
        }
    }
    #endregion
}