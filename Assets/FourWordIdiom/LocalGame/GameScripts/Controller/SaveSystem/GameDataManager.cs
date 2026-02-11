using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using Middleware;
using Newtonsoft.Json;

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
    private ButterflyData butterfly = new ButterflyData();
    
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
    public ButterflyData ButterflyData { get { return butterfly; } }
    #endregion
    
    
    private Coroutine _trackingCoroutine;
    private float _currentSessionTime = 0f; // 当前会话时长（秒）
    private bool _isTracking = false;
    
    // 更新频率（秒）
    [SerializeField] private float updateInterval = 60f;
    

    #region Unity生命周期方法
    public override void Init()
    {
        lastSaveTime = DateTime.Now;
        Application.wantsToQuit += OnWantsToQuit;
        // needLogout = false;
        // 游戏启动时开始追踪
        StartTracking();
    }
    
    
    /// <summary>
    /// 开始追踪玩家在线时长
    /// </summary>
    public void StartTracking()
    {
        if (_isTracking) return;
        
        _isTracking = true;
        _trackingCoroutine = StartCoroutine(TrackPlayTime());
        
        Debug.Log("玩家生命周期追踪已启动");
    }
    
    /// <summary>
    /// 停止追踪玩家在线时长
    /// </summary>
    public void StopTracking()
    {
        if (!_isTracking) return;
        
        _isTracking = false;
        
        if (_trackingCoroutine != null)
        {
            StopCoroutine(_trackingCoroutine);
            _trackingCoroutine = null;
        }
        
        // 保存当前的会话时长
        SaveSessionTime();
        
        Debug.Log("玩家生命周期追踪已停止");
    }
    
    private IEnumerator TrackPlayTime()
    {
        var waitTime = new WaitForSecondsRealtime(updateInterval);
        
        while (_isTracking)
        {
            yield return waitTime;
            
            // 更新当前会话时长
            _currentSessionTime += updateInterval;
            
            // 转换为分钟并添加到总在线时长
            float minutesToAdd = updateInterval / 60f;
            
            // 调用UserData添加在线时长
            AddOnlineMinutes(minutesToAdd);
            
            Debug.Log($"更新在线时长: {minutesToAdd:F2}分钟, 当前会话: {_currentSessionTime / 60f:F1}分钟");
        }
    }
    
    /// <summary>
    /// 添加在线时长到用户数据
    /// </summary>
    private void AddOnlineMinutes(float minutes)
    {
        if (Instance == null || UserData == null)
        {
            Debug.LogWarning("无法添加在线时长：用户数据未初始化");
            return;
        }
        UserData.AddOnlineMinutes(minutes);
    }
    
    /// <summary>
    /// 保存当前会话时长到用户数据
    /// </summary>
    private void SaveSessionTime()
    {
        if (_currentSessionTime <= 0) return;
        
        float minutesToAdd = _currentSessionTime / 60f;
        AddOnlineMinutes(minutesToAdd);
        _currentSessionTime = 0f;
    }
    

    private void OnApplicationFocus(bool focusStatus)
    {
        HandleFocusChange(focusStatus);
    }

    private void OnApplicationPause(bool pauseState)
    {
        HandlePauseState(pauseState);
    }

    private new void OnApplicationQuit()
    {
        HandleQuitEvent();
    }
    #endregion

    #region 初始化方法
    
    public bool PushServerCompleted { get; private set; } = false;
    private bool OnWantsToQuit()
    {
        if (dataInitialized)
        {
            Debug.Log("应用请求关闭，保存数据中...");
            CommitGameData();
            AnalyticMgr.GameEnd();
        }
        return true;
    }


    public void LoadPlayerProfile()
    {
        playerProfile.LoadData();
        fishUserSave.LoadData();
        butterfly.LoadData();
        dynamicHard.LoadData();
        // chessDynamicHard.LoadData();
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
    
    public void SetInitailized(bool init)
    {
        dataInitialized = init;
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

    public int SaveNumber { get; private set; } = 0;
    public void CommitGameData()
    {
        SaveNumber = 0;
        playerProfile.SaveData();
        butterfly.SaveData();
        fishUserSave.SaveData();
      
        //leaderboardCache.SaveData();
         string currentLevelId = CreateLevelIdentifier(playerProfile.CurrentHexStage);
         if (LevelProgressDict.ContainsKey(currentLevelId))
         {
             LevelProgressDict[currentLevelId].SaveToPlayerPrefs();
         }
         
         // string chessCurrentLevelId = ChessStageProgressData.CreateLevelIdentifier(playerProfile.CurrentChessStage);
         // if (ChessLevelProgressDict.ContainsKey(chessCurrentLevelId))
         // {
         //     ChessLevelProgressDict[chessCurrentLevelId].SaveToFile();
         // }
    }

    public void CommitPushServerData(bool needLogout = false)
    {
        try
        {
            StartCoroutine(PushServerData(needLogout));
        }
        catch (Exception e)
        {
            Debug.LogWarning("提交到服务器错误？ " + e);   
        }
    }
    private IEnumerator PushServerData(bool needLogout = false)
    {
        bool saveOver = false;
        yield return APIGateway.Instance.LoginApi.UpdateUserData(new GameDataDto
        {
            UserData = JsonConvert.SerializeObject(playerProfile),
            ExtraData = new ExtraDataDto
            {
                FishUserSave = JsonConvert.SerializeObject(fishUserSave),
                Butterfly = JsonConvert.SerializeObject(butterfly),
            }
        }, over=> saveOver = over);
        yield return new WaitUntil(() => saveOver);
        // if (needLogout)
        // {
        //     yield return APIGateway.Instance.LoginApi.Logout(playerProfile, (res) =>
        //     {
        //         PushServerCompleted = res;
        //         Application.Quit();
        //     });
        // }
        SaveNumber++;
    }
    
    public void SetNewUser(UserData user)
    {
        if(user == null) return;

        playerProfile = user;
    }
    
    #endregion

    #region 应用程序状态处理
    private void HandleFocusChange(bool hasFocus)
    {
        // 应用进入后台
        if (!hasFocus)
        {
            //初始化完成后才可以保存，不然保存的数据都为默认数值
            if (dataInitialized)
                CommitGameData();
       
            if(Game.self?.Ads?.IsPlaying == true) return; //播放广告中
            AnalyticMgr.GameEnd();
                
            StopTracking();
            requireFocusCheck = true;
            Debug.Log("应用进入后台，数据已保存");
        }
        else if (requireFocusCheck)
        {
            AnalyticMgr.GameStart();
            StartTracking();
            Debug.Log("应用回到前台，验证数据");
            requireFocusCheck = false;
            playerProfile.CheckResetDailyTime();
        }
    }

    private void HandlePauseState(bool isPaused)
    {
        if (isPaused && dataInitialized)
        {
            CommitGameData();
            //StopTracking();
            Debug.Log("应用暂停，数据已保存");
        }
    }

    private void HandleQuitEvent()
    {
        if (dataInitialized)
        {
            CommitGameData();
            StopTracking();
            AnalyticMgr.GameEnd();
            Debug.Log("应用关闭，数据已保存");
        }
    }
    #endregion

    #region 数据清理
    public void WipeAllGameData()
    {
        PurgePersistentFiles();
        
        dynamicHard.InitData();
        chessDynamicHard.InitData();
        playerProfile.InitData();
        fishUserSave.InitData();
        LevelProgressDict.Clear();
        ChessLevelProgressDict.Clear();
        butterfly.InitData();
    }

    public void PurgePersistentFiles()
    {
        string storagePath = Application.persistentDataPath;

        if (Directory.Exists(storagePath))
        {
            try
            {
                string[] allFiles = Directory.GetFiles(storagePath);
                foreach (string filePath in allFiles)
                {
                    File.Delete(filePath);
                    Debug.Log($"已移除文件: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"清除存储数据时出错: {ex.Message}");
            }
        }
    }
    #endregion
}