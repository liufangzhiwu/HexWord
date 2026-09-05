using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class OverallRankingManager : MonoBehaviour
{
    public static OverallRankingManager Instance { get; private set; }
    
    // ==========================================
    // 🌟 配置数据缓存
    // ==========================================
    public List<RealmLevelData> RealmLevelList { get; private set; } = new List<RealmLevelData>();
    public RankControlConfig RankConfig { get; private set; } = new RankControlConfig();
    public List<MonthlyRewardConfig> MonthlyRewards { get; private set; } = new List<MonthlyRewardConfig>();

    // ==========================================
    // 🌟 榜单动态数据缓存
    // ==========================================
    // 假设 LeaderboardEntry 是底层通用的榜单数据结构
    public List<LeaderboardEntry> TotalRanks { get; private set; } = new List<LeaderboardEntry>();
    public List<LeaderboardEntry> MonthlyRanks { get; private set; } = new List<LeaderboardEntry>();
    public List<LeaderboardEntry> HallOfFameRanks { get; private set; } = new List<LeaderboardEntry>();
    
    public LeaderboardEntry MyTotalRankData { get; private set; }
    public LeaderboardEntry MyMonthlyRankData { get; private set; }
    // 缓存月榜倒计时秒数
    private int _cachedMonthlyRemainingSeconds;
    // 新增这两个变量用于月榜差值计算
    private int _originalMonthlyScoreFromServer = 0; 
    private int _monthlySnapshotTotalScore = -1;
    public bool IsFetching { get; private set; }

    // 用于记录上次成功请求的时间戳 (Time.realtimeSinceStartup)
    private float _lastTotalFetchTime = -9999f;
    private float _lastMonthlyFetchTime = -9999f;
    private bool _hasFetchedHallOfFame = false; // 名人堂单次 App 生命周期只请求一次
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            //DontDestroyOnLoad(gameObject);
            LoadConfigs(); // 启动时自动解析 CSV 配置
        }
        else
        {
            Destroy(gameObject);
        }
    }

    #region 配置解析
    // ==========================================
    // 🌟 配置解析模块
    // ==========================================
    private void LoadConfigs()
    {
        // 1. 加载境界等级表 (参考 image_259f03.png)
        TextAsset realmLevelAsset = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "OverallZenLevel");
        if (realmLevelAsset != null)
        {
            ParseRealmLevelConfig(realmLevelAsset.text);
        }

        // 2. 加载排行榜控制及奖励表 (参考 image_259ee3.png)
        TextAsset rankControlAsset = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "OverallZenContrl");
        if (rankControlAsset != null)
        {
            ParseRankControlConfig(rankControlAsset.text);
        }
    }

    private void ParseRealmLevelConfig(string csvText)
    {
        // string[] lines = csvText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        List<string> lines = Middleware.ToolUtil.SplitCsvLines(csvText);
        // 从索引 2 开始（跳过中文注释行和英文变量名行）
        for (int i = 2; i < lines.Count; i++)
        {
            string[] rawFields = Middleware.ToolUtil.ParseCsvLineKeepQuotes(lines[i]);
            if (rawFields.Length < 4) continue;
            string[] fields = new string[rawFields.Length];
            for (int j = 0; j < rawFields.Length; j++)
            {
                fields[j] = rawFields[j].Trim('\"');
            }
            try
            {
                RealmLevelList.Add(new RealmLevelData
                {
                    Level = int.Parse(fields[0].Trim()),
                    NameKey = fields[1].Trim(),
                    FeelKey = fields[2].Trim(),
                    UpScore = int.Parse(fields[3].Trim())
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OverallRanking] Error parsing RealmLevelTable line {i}: {ex.Message}");
            }
        }
    }

    private void ParseRankControlConfig(string csvText)
    {
        List<string> lines = Middleware.ToolUtil.SplitCsvLines(csvText);
        if (lines.Count <= 2) return;

        // 读取第三行数据 (索引为2)
        string[] rawFields = Middleware.ToolUtil.ParseCsvLineKeepQuotes(lines[2]);
        if (rawFields.Length < 2) return;
        string[] fields = new string[rawFields.Length];
        for (int j = 0; j < rawFields.Length; j++)
        {
            fields[j] = rawFields[j].Trim('\"');
        }
        try
        {
            // 解析 RankControl (如 "1_10000_2000")
            string[] controlParams = fields[0].Split('_');
            if (controlParams.Length >= 3)
            {
                RankConfig.IsOpen = controlParams[0] == "1";
                RankConfig.TotalRankUnlockScore = int.Parse(controlParams[1]);
                RankConfig.MonthlyRankUnlockScore = int.Parse(controlParams[2]);
            }

            // 解析 RankBox 月榜奖励 (如 "16_1;0_200;1_3#17_1;0_150;1_2#18_1;0_100;1_2")
            string[] rankGroups = fields[1].Split('#');
            for (int i = 0; i < rankGroups.Length; i++)
            {
                MonthlyRewardConfig rewardConfig = new MonthlyRewardConfig { Rank = i + 1 };
                string[] itemStrings = rankGroups[i].Split(';');
                
                foreach (var itemStr in itemStrings)
                {
                    if (string.IsNullOrWhiteSpace(itemStr)) continue;
                    string[] itemParams = itemStr.Split('_');
                    if (itemParams.Length >= 2)
                    {
                        // 这里具体怎么解析取决于你的底层定义，假设格式是 "ItemID_Count" 或 "ItemID_Type_Count"
                        // 为了兼容示例，假设为 ItemID_Count
                        rewardConfig.Rewards.Add(new MonthlyRewardItem
                        {
                            ItemId = int.Parse(itemParams[0]),
                            ItemCount = int.Parse(itemParams[1])
                            // ItemType = ... 如果有第三个参数
                        });
                    }
                }
                MonthlyRewards.Add(rewardConfig);
            }
            Debug.Log($"[OverallRanking] 配置解析成功！总榜解锁分: {RankConfig.TotalRankUnlockScore}, 月榜解锁分: {RankConfig.MonthlyRankUnlockScore}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[OverallRanking] Error parsing RankControlTable: {ex.Message}");
        }
    }
    
    #endregion
    
    // ==========================================
    // 🌟 API 网络请求模块
    // ==========================================
    
    //  每次获取时，动态扣除已经流逝的时间
    public int GetActualMonthlyRemainingSeconds()
    {
        if (_lastMonthlyFetchTime < 0) return 0;
        
        // 计算从上次请求成功到现在，一共过去了多少秒
        float elapsedSeconds = Time.realtimeSinceStartup - _lastMonthlyFetchTime;
        
        // 用最初下发的倒计时 - 流逝的时间
        int actualRemaining = _cachedMonthlyRemainingSeconds - (int)elapsedSeconds;
        
        return Mathf.Max(0, actualRemaining); // 确保不出现负数
    }
    
    // ==========================================
    // 暴露缓存检查方法 
    // ==========================================
    public bool IsTotalRankCached() => Time.realtimeSinceStartup - _lastTotalFetchTime < 1200f;
    public bool IsMonthlyRankCached() => Time.realtimeSinceStartup - _lastMonthlyFetchTime < 1200f;
    public bool IsHallOfFameCached() => _hasFetchedHallOfFame;
    /// <summary>
    /// 获取总榜数据
    /// </summary>
    public IEnumerator FetchTotalRankRoutine()
    {
        if (IsTotalRankCached())
        {
            Debug.Log("[Ranking] 总榜触发20分钟缓存，直接使用内存数据");
            yield break; // 直接退出协程，外部继续往下执行
        }
        
        IsFetching = true;
        bool isCompleted = false;

        yield return APIGateway.Instance.LeaderboardApi.GetTotalRank((res) =>
        {
            if (res != null)
            {
                TotalRanks.Clear();
                TotalRanks.AddRange(res.list); // 假设后端返回 list 数组
                MyTotalRankData = res.my;
                _lastTotalFetchTime = Time.realtimeSinceStartup;
            }
            isCompleted = true;
        });

        yield return new WaitUntil(() => isCompleted);
        IsFetching = false;
    }

    /// <summary>
    /// 获取月榜数据
    /// </summary>
    public IEnumerator FetchMonthlyRankRoutine()
    {
        if (IsMonthlyRankCached())
        {
            Debug.Log("[Ranking] 月榜触发20分钟缓存，直接使用内存数据");
            yield break; 
        }
        
        IsFetching = true;
        bool isCompleted = false;

        yield return APIGateway.Instance.LeaderboardApi.GetMonthlyRank((res) =>
        {
            if (res != null)
            {
                MonthlyRanks.Clear();
                MonthlyRanks.AddRange(res.list);
                MyMonthlyRankData = res.my;
                // 👇 核心修改：记录服务器返回时的原始月分，以及当时的本地总分快照
                _originalMonthlyScoreFromServer =  res?.my.score ?? 0;
                _monthlySnapshotTotalScore = GameDataManager.Instance.UserData.overallZenScore;
                
                _cachedMonthlyRemainingSeconds = res.remaining_seconds;
                _lastMonthlyFetchTime = Time.realtimeSinceStartup;
            }
            isCompleted = true;
        });

        yield return new WaitUntil(() => isCompleted);
        IsFetching = false;
    }

    /// <summary>
    /// 获取名人堂数据
    /// </summary>
    public IEnumerator FetchHallOfFameRoutine()
    {
        // 第一次进入主动请求，后续不再重复请求
        if (IsHallOfFameCached())
        {
            Debug.Log("[Ranking] 名人堂数据已在内存中，跳过网络请求");
            yield break;
        }
        
        IsFetching = true;
        bool isCompleted = false;

        yield return APIGateway.Instance.LeaderboardApi.GetHallOfFame((res) =>
        {
            if (res != null)
            {
                HallOfFameRanks.Clear();
                HallOfFameRanks.AddRange(res.list);
                _hasFetchedHallOfFame = true;
            }
            isCompleted = true;
        });

        yield return new WaitUntil(() => isCompleted);
        IsFetching = false;
    }
    /// <summary>
    /// 检查月度榜单是否有结算奖励（测试服按小时结算）
    /// </summary>
    public IEnumerator CheckMonthlySettlementRoutine(Action<bool> onComplete = null)
    {
        MonthlySettlementResponse resp = null; // 局部变量，用完即毁
        bool done = false;
        yield return APIGateway.Instance.LeaderboardApi.CheckMonthlySettlement((res) =>
        {
            resp = res;
            done = true;
        });
        yield return new WaitUntil(() => done);
        if (resp == null || !resp.has_settlement)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        SettlementData data = new SettlementData
        {
            period   = resp.period,
            myRank   = resp.my_rank,
            myScore  = resp.my_score,
            myAvatar = resp.my_avatar,
            topList  = resp.list,
            myEntry  = new LeaderboardEntry
            {
                rank = resp.my_rank, score = resp.my_score,
                avatar = resp.my_avatar, 
                nickname = string.IsNullOrEmpty(resp.my_nickname)
                    ? GameDataManager.Instance.UserData.UserName
                    : resp.my_nickname,
                
                user_id = int.Parse(GameDataManager.Instance.UserData.PlayerId)
            },
        };
        // 2) 奖励按名次查本地配置 —— 必须放在初始化器外面
        var rewardCfg = MonthlyRewards.FirstOrDefault(c => c.Rank == resp.my_rank);
        data.rewards = rewardCfg?.Rewards?
                           .ToDictionary(r => r.ItemId, r => r.ItemCount)
                       ?? new Dictionary<int, int>();
        
        // 打开弹窗
        UIWindow uiWindow = SystemManager.Instance.ShowPanel(PanelType.OverallSettlementScreen);
        OverallSettlementScreen settleUI = uiWindow.GetComponent<OverallSettlementScreen>();
        if (settleUI != null)
        {
            // 传入排名、奖励、新段位名
            settleUI.ShowSettlement(data);
        }
        yield return new WaitUntil(() => !SystemManager.Instance.PanelIsShowing(PanelType.OverallSettlementScreen));
        // 直接将数据通过回调丢给UI，不做全局缓存
        onComplete?.Invoke(data.HasReward);
    }
    
    // ==========================================
    // 🌟 辅助工具方法
    // ==========================================
    
    /// <summary>
    /// 检查总榜是否已对玩家解锁
    /// </summary>
    public bool IsTotalRankUnlocked(int playerZenScore)
    {
        if (!RankConfig.IsOpen) return false;
        return playerZenScore >= RankConfig.TotalRankUnlockScore;
    }

    /// <summary>
    /// 检查月榜是否已对玩家解锁
    /// </summary>
    public bool IsMonthlyRankUnlocked(int playerZenScore)
    {
        if (!RankConfig.IsOpen) return false;
        return playerZenScore >= RankConfig.MonthlyRankUnlockScore;
    }
    
    public void InvalidateMonthlyCache()
    {
        _lastMonthlyFetchTime = -9999f;
        MonthlyRanks.Clear();
        MyMonthlyRankData = null;
        
        // 清理月榜快照
        _originalMonthlyScoreFromServer = 0;
        _monthlySnapshotTotalScore = -1;
    }
    // 清理名人堂缓存的方法
    public void InvalidateHallOfFameCache()
    {
        _hasFetchedHallOfFame = false; // 标记为未请求，下次打开强制重拉
        HallOfFameRanks.Clear();
    }
    // 强制使所有榜单缓存失效
    public void InvalidateAllRanksCache()
    {
        // 1. 清理总榜缓存
        _lastTotalFetchTime = -9999f;
        TotalRanks.Clear();
        MyTotalRankData = null;
        
        // 2. 复用已有的清理月榜和名人堂方法
        InvalidateMonthlyCache();
        InvalidateHallOfFameCache();
    }
    
    public static string Format(int rank)
    {
        if (rank <= 0) return null;
    
        if (rank > 10000)
        {
            return $"{rank / 10000}万+";
        }
        else if (rank > 1000)
        {
            return $"{rank / 1000}千+";
        }
        else if (rank > 100)
        {
            return $"{rank / 100}00+";
        }
    
        // 排名 < 100，正常显示数字
        return rank.ToString(); 
    }
    
    /// <summary>
    /// 根据传入的禅意总分，计算并返回当前的禅意等级 (例如: 1, 2, 3...)
    /// </summary>
    public int GetZenLevelByScore(int score)
    {
        if (RealmLevelList == null || RealmLevelList.Count == 0) 
        {
            return 1; // 配置未加载时，兜底返回1级
        }

        int cumulativeScore = 0; // 累计所需总分
        foreach (var realm in RealmLevelList)
        {
            cumulativeScore += realm.UpScore;
            
            // 如果玩家的总分小于累加到当前的阈值，说明他就停留在这个等级
            if (score < cumulativeScore)
            {
                return realm.Level;
            }
        }
        // 如果循环结束还没 return，说明分数已经超过了配置表的上限，满级了
        return RealmLevelList.LastOrDefault() != null ? RealmLevelList.Last().Level : 1;
    }
    
    /// <summary>
    /// 获取当前等级的进度，用于UI进度条展示 (分子 / 分母)
    /// </summary>
    /// <param name="score">玩家总分</param>
    /// <param name="currentLevelScore">当前等级已积累的分数 (分子)</param>
    /// <param name="currentLevelNeed">当前等级升级需要的分数 (分母)</param>
    public void GetZenProgress(int score, out int currentLevelScore, out int currentLevelNeed)
    {
        if (RealmLevelList == null || RealmLevelList.Count == 0) 
        {
            currentLevelScore = 0;
            currentLevelNeed = 1;
            return;
        }

        int cumulativeScore = 0;
        foreach (var realm in RealmLevelList)
        {
            if (score < cumulativeScore + realm.UpScore)
            {
                // 分子 = 玩家总分 - 升到当前等级之前抠掉的所有历史总分
                currentLevelScore = score - cumulativeScore;
                // 分母 = 这一级升下一级纯粹需要的经验
                currentLevelNeed = realm.UpScore;
                return;
            }
            cumulativeScore += realm.UpScore;
        }

        // 满级状态兜底
        var last = RealmLevelList.Last();
        currentLevelScore = last.UpScore;
        currentLevelNeed = last.UpScore;
    }
    
    // ==========================================
    // 🌟 核心体验优化：本地分数插榜刷新
    // ==========================================
    
    /// <summary>
    /// 将本地最新的禅意分同步到内存缓存的排行榜中，并重新排序
    /// </summary>
    public void SyncLocalScoreToCache()
    {
        // 获取本地最新分数和玩家ID
        int latestTotalScore = GameDataManager.Instance.UserData.overallZenScore;
        if (!int.TryParse(GameDataManager.Instance.UserData.PlayerId, out int myUserId)) return;
        
        // 1. 世界榜：直接用最新总分进行本地插榜
        UpdateSingleListCache(TotalRanks, MyTotalRankData, latestTotalScore, myUserId);
        // 2. 月榜：利用快照差值(Delta)计算最新月榜分，进行本地插榜
        if (MyMonthlyRankData != null && _monthlySnapshotTotalScore >= 0)
        {
            // 计算自上次从服务器拉取数据后，本地又涨了多少分
            int scoreDelta = latestTotalScore - _monthlySnapshotTotalScore;
            
            // 只要分涨了，就把涨的部分加到服务器下发的原始月分上
            int latestMonthlyScore = _originalMonthlyScoreFromServer + scoreDelta;
            UpdateSingleListCache(MonthlyRanks, MyMonthlyRankData, latestMonthlyScore, myUserId);
        }
    }

    private void UpdateSingleListCache(List<LeaderboardEntry> list, LeaderboardEntry myData, int latestScore, int myUserId)
    {
        if (list == null || myData == null) return;

        // 1. 取服务端返回分数和本地最新分数的最大值
        int realScore = Mathf.Max(myData.score, latestScore);
        // 更新我自己的独立数据
        myData.score = realScore;

        // 2. 查找我是否在当前显示的列表(如前100名)中
        var meInList = list.FirstOrDefault(x => x.user_id == myUserId);
        
        if (meInList != null)
        {
            if (meInList.score == realScore) return;
            // 在榜单中，直接更新分数
            meInList.score = realScore;
        }
        else
        {
            // 我没在榜单中，判断我的新分数是否击败了榜单最后一名（俗称：挤进榜单）
            if (realScore > 0)
            {
                // 创建一个分身塞进榜单参与排序
                list.Add(new LeaderboardEntry 
                {
                    user_id = myUserId,
                    avatar = myData.avatar,
                    nickname = myData.nickname,
                    avatar_frame = myData.avatar_frame,
                    score = latestScore
                });
            }
            else
            {
                // 分数依然没够着上榜门槛，不操作 list，直接退出
                return;
            }
        }

        // 3. 重新按分数降序排序
        list.Sort((a, b) => b.score.CompareTo(a.score));

        // 如果挤进了新人，把超出原本长度（如第101名）的人剔除
        if (list.Count > 100) 
        {
            // 注：如果没有 GroupSize 限制，你可以固定写 100，或者省略这步
            list.RemoveAt(list.Count - 1); 
        }

        // 4. 重新赋予以 1 开始的名次
        for (int i = 0; i < list.Count; i++)
        {
            list[i].rank = i + 1;
        }

        // 5. 将我在大列表里的最新名次，同步回我的底部常驻数据
        var updatedMe = list.FirstOrDefault(x => x.user_id == myUserId);
        if (updatedMe != null)
        {
            myData.rank = updatedMe.rank;
        }
    }
}