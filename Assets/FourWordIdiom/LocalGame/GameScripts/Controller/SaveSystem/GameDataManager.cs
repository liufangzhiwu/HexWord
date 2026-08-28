using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using FourWordIdiom.LocalGame.GameScripts.Controller.SaveSystem;
using Middleware;
using Newtonsoft.Json;

public class GameDataManager : SingletonMono<GameDataManager>
{
    #region 数据字段
    private UserData playerProfile = new UserData();
    private RankSaveData leaderboardCache = new RankSaveData();
    private Dictionary<string, StageProgressData> LevelProgressDict = new Dictionary<string, StageProgressData>();
    private Dictionary<string, ChessStageProgressData> ChessLevelProgressDict = new Dictionary<string, ChessStageProgressData>();
    private FishUserSaveData fishUserSave = new FishUserSaveData(); 
    private DynamicHardSave dynamicHard = new DynamicHardSave();
    private ChessDynamicHardSave chessDynamicHard = new ChessDynamicHardSave();
    private ButterflyData butterfly = new ButterflyData();
    private OverallRankData overallRankData = new OverallRankData();
    private AchieveSaveDatas achieveSaveDatas = new AchieveSaveDatas();
    
    public bool dataInitialized = false;
    public bool IsBeiData=false;
    public bool IsSetData=false;
    private bool requireFocusCheck = false;
    private DateTime lastSaveTime;
    // 加入记录时间的变量
    private float _lastSaveRealTime = 0f;
    public static bool HasSyncedThisSession = false;
    #endregion

    #region 属性
    public FishUserSaveData FishUserSave { get { return fishUserSave; } }
    public RankSaveData Leaderboard { get { return leaderboardCache; } }
    public UserData UserData { get { return playerProfile; } }
    public ButterflyData ButterflyData { get { return butterfly; } }
    public OverallRankData OverallRank { get { return overallRankData; } }
    
    public DynamicHardSave DynamicHardSave { get { return dynamicHard; } }
    public ChessDynamicHardSave ChessDynamicHardSave { get { return chessDynamicHard; } }
    public AchieveSaveDatas AchieveSaveDataList { get { return achieveSaveDatas; } }
    
    private Coroutine _trackingCoroutine;
    private float _currentSessionTime = 0f; // 当前会话时长（秒）
    private bool _isTracking = false;
    // 更新频率（秒）
    [SerializeField] private float updateInterval = 60f;
    // 状态锁：标记是否正在等待玩家处理历史记录弹窗
    [HideInInspector] public bool IsWaitingForHistoryResolution = false; 
    #endregion

    #region Unity生命周期方法
    
    void Update()
    {
        if (ChessDynamicHardSave.EnergyValue == 5.96046448e-8f || Mathf.Approximately(ChessDynamicHardSave.EnergyValue, 5.96046448e-8f))
        {
            Debug.LogError($"energy异常值 detected at frame {Time.frameCount}");
            Debug.LogError($"Time.deltaTime: {Time.deltaTime}, timeScale: {Time.timeScale}");
            //Debug.LogStackTrace();  // 显示调用栈
        }
    }

    public override void Init()
    {
        lastSaveTime = DateTime.Now;
        Game.self.Analytics.OnSdkInit += AnalyticMgr.OnAnalyticsSdkInit;
        // Application.wantsToQuit += OnWantsToQuit;
        // 游戏启动时开始追踪
        StartTracking();

        LoadPlayerProfile();
    }

    private void OnApplicationFocus(bool focusStatus)
    {
        HandleFocusChange(focusStatus);
    }

    private void OnApplicationPause(bool pauseState)
    {
        HandlePauseState(pauseState);
    }

    protected override void OnApplicationQuit()
    {
        HandleQuitEvent();
        base.OnApplicationQuit();
    }

    #endregion

    #region 初始化方法

    public void LoadPlayerProfile()
    {
        playerProfile.LoadData();
        fishUserSave.LoadData();
        dynamicHard.LoadData();
        chessDynamicHard.LoadData();
        butterfly.LoadData();
        overallRankData.LoadData();
        AchieveSaveDataList.LoadData();
        
        if (string.IsNullOrEmpty(playerProfile.ABName))
        {
            playerProfile.ABName = playerProfile.IsFirstLaunch ? (UnityEngine.Random.Range(0, 2) == 0 ? "0" : "1") : "0";
        }
        
        // dataInitialized = true;
        Debug.Log($"初始化时是： {playerProfile.ABName}" );
        //leaderboardCache.LoadData();
    }
    public void SetNewUser(UserData user)
    {
        if(user == null) return;

        playerProfile = user;
    }
    public void SetInitailized(bool init)
    {
        dataInitialized = init;
    }
    #endregion

    #region 关卡数据管理
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

    public bool IsNewLevelEntry(int StageNumber, bool isChessStage = false)
    {
        string saveFileName;
        if (isChessStage)
            saveFileName = ChessStageProgressData.CreateLevelIdentifier(StageNumber);
        else
            saveFileName= CreateLevelIdentifier(StageNumber);

        string filePath = Path.Combine(Application.persistentDataPath, saveFileName);

        if (!File.Exists(filePath))
        {
            Debug.LogWarning("未找到关卡进度文件");
            return true;
        }
        return false;
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

    public void UpdateLevelProgress(StageProgressData progressData)
    {
        string identifier = CreateLevelIdentifier(progressData.StageId);
        if (LevelProgressDict.ContainsKey(identifier))
        {
            LevelProgressDict[identifier] = progressData;
        }

        // 无用更新检查
        if (progressData.StageId % 2 == 0)
        {
            Debug.Log($"更新了偶数关卡 {progressData.StageId}");
        }
    }
    public ChessStageProgressData RetrieveLevelProgress(ChessStageInfo levelDetails)
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
    public void UpdateLevelProgress(ChessStageProgressData progressData)
    {
        string identifier = ChessStageProgressData.CreateLevelIdentifier(progressData.StageId);
        if (ChessLevelProgressDict.ContainsKey(identifier))
        {
            ChessLevelProgressDict[identifier] = progressData;
        }
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
    /// 保存当前会话时长到用户数据
    /// </summary>
    private void SaveSessionTime()
    {
        if (_currentSessionTime <= 0) return;
        
        float minutesToAdd = _currentSessionTime / 60f;
        AddOnlineMinutes(minutesToAdd);
        _currentSessionTime = 0f;
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
    
    #endregion

    #region 数据保存
    public void CommitGameData()
    {
        playerProfile.SaveData();
        fishUserSave.SaveData();
        dynamicHard.SaveData();
        chessDynamicHard.SaveData();
        butterfly.SaveData();
        overallRankData.SaveData();
        AchieveSaveDataList.SaveData();
        // Debug.LogFormat("保存用户时的数据: {0}", JsonConvert.SerializeObject(playerProfile));
        // StartCoroutine(PushServerData());
        //leaderboardCache.SaveData();
        string currentLevelId = CreateLevelIdentifier(playerProfile.CurrentHexStage);
        if (LevelProgressDict.ContainsKey(currentLevelId))
        {
            LevelProgressDict[currentLevelId].SaveToFile();
        }
        string chessCurrentLevelId = ChessStageProgressData.CreateLevelIdentifier(playerProfile.CurrentChessStage);
        if (ChessLevelProgressDict.ContainsKey(chessCurrentLevelId))
        {
            ChessLevelProgressDict[chessCurrentLevelId].SaveToFile();
        }
        
        if (!dataInitialized)
        {
            Debug.Log("<color=yellow>[GameDataManager] 游戏尚未初始化完成(Loading中)，跳过服务器上传。</color>");
            return;
        }
        
        // 如果正在等待玩家处理历史弹窗，阻断服务器上传
        if (IsWaitingForHistoryResolution)
        {
            Debug.Log("<color=yellow>[GameDataManager] 正在等待历史记录处理，暂停上传服务器数据，防止旧数据覆盖新数据。</color>");
            return;
        }
        if (!HTTPClient.Instance.IsTokenValid())
        {
            Debug.Log("<color=orange>[GameDataManager] 当前未登录(无Token)，仅保存本地，跳过服务器上传。</color>");
            return;
        }
        StartCoroutine(PushServerData());
    }
  
    private IEnumerator PushServerData()
    {
        bool saveOver = false;
        yield return APIGateway.Instance.LoginApi.UpdateUserData(new GameDataDto
        {
            UserData = JsonConvert.SerializeObject(playerProfile),
            ExtraData = new ExtraDataDto
            {
                FishUserSave = JsonConvert.SerializeObject(fishUserSave),
                Butterfly = JsonConvert.SerializeObject(butterfly),
                OverallRank = JsonConvert.SerializeObject(overallRankData),
                AchieveSaveDatas = JsonConvert.SerializeObject(achieveSaveDatas),
            }
        }, over => saveOver = over);
        yield return new WaitUntil(() => saveOver);
    }
    
    /// <summary>
    /// 🌟 玩家选择服务器数据后，强行覆盖本地数据
    /// </summary>
    public void OverwriteLocalWithServerData(UserData serverUser, ExtraDataDto serverExtraData)
    {
        // 1. 清理本地所有旧的残局进度文件 (避免新关卡读取到旧的进度)
        ClearAllLevelProgressFiles();

        // 2. 覆盖主数据并保存到本地硬盘
        if (serverUser != null)
        {
            string jsonData = JsonConvert.SerializeObject(serverUser, Formatting.Indented);
            Debug.Log($"历史用户数据: {jsonData}");
            playerProfile.InitData(serverUser);
        }

        // 3. 解析并覆盖 ExtraData (周边系统)
        if (serverExtraData != null)
        {
            if (!string.IsNullOrEmpty(serverExtraData.FishUserSave))
            {
                var serverFishData = JsonConvert.DeserializeObject<FishUserSaveData>(serverExtraData.FishUserSave);
                fishUserSave.InitData(serverFishData);
            }

            if (!string.IsNullOrEmpty(serverExtraData.Butterfly))
            {
                var serverButterflyData = JsonConvert.DeserializeObject<ButterflyData>(serverExtraData.Butterfly);
                butterfly.InitData(serverButterflyData);
            }

            if (!string.IsNullOrEmpty(serverExtraData.OverallRank))
            {
                var serverOverallRankData =JsonConvert.DeserializeObject<OverallRankData>(serverExtraData.OverallRank);
                overallRankData.InitData(serverOverallRankData);
            }
        }

        // 4. 标记当前会话已同步，避免无限弹窗
        CommitGameData();
        HasSyncedThisSession = true;
        SetInitailized(true);
        Debug.Log("<color=green>[GameDataManager] 已用服务器数据彻底覆盖本地内存与本地文件！</color>");
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
       
            requireFocusCheck = true;
            AnalyticMgr.GameEnd();
            
            if(Game.self.Ads?.IsPlaying==true) return; //播放广告中
           
            StopTracking();   
            
            Debug.Log("应用进入后台，数据已保存");
        }
        else if (requireFocusCheck)
        {
            AnalyticMgr.GameStart();
            
            if(Game.self.Ads?.IsPlaying==true) return; //播放广告中
            
            StartTracking();
            Debug.Log("应用回到前台，验证数据");
            requireFocusCheck = false;
            playerProfile?.CheckResetDailyTime();
        }
    }

    private void HandlePauseState(bool isPaused)
    {
        if (isPaused && dataInitialized)
        {
            // 防抖：1秒内不重复保存
            if (Time.realtimeSinceStartup - _lastSaveRealTime < 1f) return;
            _lastSaveRealTime = Time.realtimeSinceStartup;
            
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
            // StartCoroutine(APIGateway.Instance.LoginApi.Logout(playerProfile, null));
        }
    }

    bool logoutCompleted = false;

    #endregion

    #region 数据清理
    public void WipeAllGameData()
    {
        PurgePersistentFiles();

        playerProfile.InitData();
        fishUserSave.InitData();
        LevelProgressDict.Clear();
        ChessLevelProgressDict.Clear();
        dynamicHard.InitData();
        chessDynamicHard.InitData();
        butterfly.InitData();
        overallRankData.InitData();
        achieveSaveDatas.InitData();
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
    /// <summary>
    /// 彻底清除某个关卡在内存中的残局缓存
    /// </summary>
    public void ClearChessLevelCache(int stageNumber)
    {
        string identifier = ChessStageProgressData.CreateLevelIdentifier(stageNumber);
        if (ChessLevelProgressDict.ContainsKey(identifier))
        {
            ChessLevelProgressDict.Remove(identifier);
            Debug.Log($"[GameDataManager] 已清理关卡 {stageNumber} 的内存字典缓存！");
        }
    }
    /// <summary>
    /// 清理所有关卡进度缓存文件
    /// </summary>
    public void ClearAllLevelProgressFiles()
    {
        LevelProgressDict.Clear();
        ChessLevelProgressDict.Clear();

        string storagePath = Application.persistentDataPath;
        if (Directory.Exists(storagePath))
        {
            string[] allFiles = Directory.GetFiles(storagePath);
            foreach (string filePath in allFiles)
            {
                // 只删除关卡进度文件，保留其他正常数据文件
                string fileName = Path.GetFileName(filePath);
                if (fileName.StartsWith("StageProgress_") || fileName.StartsWith("ChessStageProgress_"))
                {
                    File.Delete(filePath);
                }
            }
        }
    }
    #endregion
}