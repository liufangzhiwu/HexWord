using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public class ZenRankManager : MonoBehaviour
{
    public static ZenRankManager Instance { get; private set; }
    public event Action<int, string> OnRankTimerTick; 
    private Coroutine globalTimerCoroutine;
    
    public List<ZenLevelState> ZenStates { get; private set; } = new List<ZenLevelState>();
    public List<ZenRewardData> RewardDatas { get; private set; } = new List<ZenRewardData>();
    
    
    // ==========================================
    // 🌟 新增：排行榜核心数据缓存
    // ==========================================
    public List<ZenRankState> TopRanks { get; private set; } = new List<ZenRankState>();
    public List<ZenRankState> MiddleRanks { get; private set; } = new List<ZenRankState>();
    public List<ZenRankState> BottomRanks { get; private set; } = new List<ZenRankState>();
    public LeaderboardEntry MyCurrentRankData { get; private set; } // 玩家自己的真实排名数据
    public int RemainingSeconds { get; private set; } = -1; // 当前赛季剩余时间
    public int NextRemainingSeconds { get; private set; } = -1;    // 下一期的时间next_remaining_seconds
    // 缓存上一次的排名和分数（用于结算动画比对）
    public int CachedOldRank { get;  set; }
    public int CachedOldScore { get;  set; }
    public bool IsFetching { get;  set; } // 数据请求状态锁
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 保证切场景不销毁
            LoadZenConfigs(); // 启动时自动解析 CSV 配置
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void LoadZenConfigs()
    {
        // 🌟 将原 ZenRankScreen 里的 ConvertCSVToJSON 和 ParseZenLevelItems 逻辑移到这里
        // 确保游戏一启动，段位表和奖励表就已经加载在内存里了
        
        TextAsset csvData = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "ZenStateTable");
        if (csvData != null)
        {
            ParseZenLevelItems(csvData.text);
        }
        // 加载奖励列表
        TextAsset textAsset = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo","ZenRankingRewardTable");
        if (textAsset != null)
            ConvertRewardCSVToJSON(textAsset.text);
        
        
    }
    private void ParseZenLevelItems(string csvText)
    {
        string[] lines = csvText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 2; i < lines.Length; i++)
        {
            var fields = lines[i].Split(',');
            if (fields.Length < 1) continue;
            try
            {
                ZenStates.Add(new ZenLevelState
                {
                    Id = int.Parse(fields[0].Trim()),
                    Code = fields[1].Trim(),
                    Name = fields[2].Trim(),
                    UpProportion = fields[3].Trim(),
                    DownProportion = fields[4].Trim(),
                    MinScore = int.Parse(fields[5].Trim()),
                    MaxScore = int.Parse(fields[6].Trim())
                });
            }catch (Exception ex)
            {
                Debug.LogError("Error parsing line: " + i + " Exception: " + ex.Message);
            }

        }
    }
    void ConvertRewardCSVToJSON(string data)
    {
        // 用于构建 JSON 字符串
        string[] lines = data.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        for (int i = 2; i < lines.Length; i++) // 从第一行开始，跳过标题行
        {
            string[] fields = lines[i].Split(',');

            if (fields.Length >= 3) // 确保有足够的字段
            {
                int id = int.Parse(fields[0].Trim()); // Id
                int state = int.Parse(fields[1].Trim());  // 段位
                int rank = int.Parse(fields[2].Trim()); // 排名
                
                Dictionary<int, int> rewardData = new Dictionary<int, int>();
                // 先用 # 分隔
                string[] groups = fields[3].Split('#');
                foreach (string group in groups)
                {
                    string[] reward =  group.Split(';');
                    rewardData.Add(int.Parse(reward[0]), int.Parse(reward[1]));
                }

                ZenRewardData item = new ZenRewardData
                {
                    Id = id,
                    State = state,
                    Rank = rank,
                    rewards =  rewardData
                };
                RewardDatas.Add(item);
            }
            else
            {
                Debug.LogWarning($"Skipping line {i + 1}: Not enough fields.");
            }
        }
    }
    public void ClearRankCache()
    {
        TopRanks.Clear();
        MiddleRanks.Clear();
        BottomRanks.Clear();
        MyCurrentRankData = null; // 必须设为 null
        CachedOldRank = 0;
        CachedOldScore = 0;
    }
    // ==========================================
    // 🌟 全局统一的排行榜数据请求接口
    // ==========================================
    public IEnumerator FetchLeaderboardDataRoutine(string boardId)
    {
        IsFetching = true;

        // 在拉取新数据前，如果有旧数据，先将其缓存下来用于对比
        if (MyCurrentRankData != null && MyCurrentRankData.rank > 0)
        {
            CachedOldRank = MyCurrentRankData.rank;
            CachedOldScore = MyCurrentRankData.score;
        }

        bool isCompleted = false;
        yield return APIGateway.Instance.LeaderboardApi.GetLeaderboard(boardId, (res) =>
        {
            // 利用你说的“未登录返回空”的特性，直接根据 res.my 判断
            if (res != null)
            {
                TopRanks.Clear(); MiddleRanks.Clear(); BottomRanks.Clear();
                foreach (var entry in res.top) TopRanks.Add(ConvertEntryToState(entry));
                foreach (var entry in res.middle) MiddleRanks.Add(ConvertEntryToState(entry));
                foreach (var entry in res.bottom) BottomRanks.Add(ConvertEntryToState(entry));
                
                RemainingSeconds = res.remaining_seconds;
                NextRemainingSeconds = res.next_remaining_seconds;
                StartGlobalTimer(RemainingSeconds);
                Debug.Log("拉取榜单完成, 通知所有时间订阅" + RemainingSeconds);
                // 🌟 单独处理玩家自己的数据和缓存
                if (res.my != null)
                {
                    MyCurrentRankData = res.my;
                    
                    // 如果是本局第一次拿到有效数据，同步缓存，防播动画
                    CachedOldRank = MyCurrentRankData.rank;
                    CachedOldScore = MyCurrentRankData.score;
                }
                else
                {
                    // 确保未上榜时数据为空，UI 层才能正确显示“未上榜”状态
                    MyCurrentRankData = null; 
                }
            }
            isCompleted = true;
        });

        yield return new WaitUntil(() => isCompleted);
        IsFetching = false;
    }
    private ZenRankState ConvertEntryToState(LeaderboardEntry entry)
    {
        return new ZenRankState { Rank = entry.rank, Avatar = entry.avatar, Name = entry.nickname, Level = entry.leaderboard_name, Score = entry.score };
    }
    public void StartGlobalTimer(int seconds)
    {
        RemainingSeconds = seconds;
        if (globalTimerCoroutine != null)
        {
            StopCoroutine(globalTimerCoroutine);
        }
        globalTimerCoroutine = StartCoroutine(GlobalTimerRoutine());
    }
    private IEnumerator GlobalTimerRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(1f);
    
        // 缓存多语言字符串，避免每秒重复获取
        string day = MultilingualManager.Instance.GetString("TimeD") ?? "d ";
        string hour = MultilingualManager.Instance.GetString("TimeH") ?? "h ";
        string minute = MultilingualManager.Instance.GetString("TimeM") ?? "m";
        string second = MultilingualManager.Instance.GetString("TimeS") ?? "s";
        string settleStr = MultilingualManager.Instance.GetString("LotusRankingEnd") ?? "结算中...";

        while (RemainingSeconds > 0)
        {
            string timeStr = FormatTime(RemainingSeconds, day, hour, minute, second);
            // 广播当前时间和格式化好的文本
            OnRankTimerTick?.Invoke(RemainingSeconds, timeStr); 
        
            yield return wait;
            RemainingSeconds--; // 全局统一递减
        }
        // 倒计时结束，广播结算状态
        if(RemainingSeconds == 0)
            OnRankTimerTick?.Invoke(0, settleStr);
    }
    // 统一的时间格式化方法
    private string FormatTime(int seconds, string d, string h, string m, string s)
    {
        TimeSpan ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalDays >= 1) return $"{ts.Days}{d}{ts.Hours:D2}{h}";
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}{h}{ts.Minutes:D2}{m}";
        return $"{ts.Minutes:D2}{m}{ts.Seconds:D2}{s}";
    }
    /// <summary>
    /// 返回 NextRemainingSeconds 格式化后的时间字符串（如 "1d 02h" "15m 30s"）
    /// </summary>
    public string GetNextRemainingTimeFormatted()
    {
        // 无效值或负数则返回结算中文本
        if (NextRemainingSeconds <= 0)
        {
            return MultilingualManager.Instance.GetString("LotusRankingEnd") ?? "结算中...";
        }
        string day = MultilingualManager.Instance.GetString("TimeD") ?? "d ";
        string hour = MultilingualManager.Instance.GetString("TimeH") ?? "h ";
        string minute = MultilingualManager.Instance.GetString("TimeM") ?? "m";
        string second = MultilingualManager.Instance.GetString("TimeS") ?? "s";
        return FormatTime(NextRemainingSeconds, day, hour, minute, second);
    }
    // ==========================================
    // 🌟 核心修复：获取排行榜上下文玩家 (Vita式真实感)
    // ==========================================
    public List<ZenRankState> GetContextPlayersForAnimation(int oldRank, int newRank)
    {
        List<ZenRankState> contextPlayers = new List<ZenRankState>();
        var allRanks = new List<ZenRankState>();
        allRanks.AddRange(TopRanks);
        allRanks.AddRange(MiddleRanks);
        allRanks.AddRange(BottomRanks);

        // 1. 剔除玩家自己，避免在列表中看到两个自己
        string myName = GameDataManager.Instance.UserData.UserName;
        var otherPlayers = allRanks.Where(p => p.Name != myName && p.Rank > 0).ToList();

        // 2. 按名次从高到低 (1, 2, 3...) 排序
        var sortedOthers = otherPlayers.OrderBy(p => p.Rank).ToList();

        // 3. 智能抓取策略：我们最多在界面上展示 4 个其他玩家
        // 优先抓取名次介于 oldRank 和 newRank 之间的人 (你确确实实超越/被超越的人)
        // 如果不够，就抓紧挨着你 newRank 的人 (让你知道周围都是谁)
        foreach (var p in sortedOthers)
        {
            if (contextPlayers.Count >= 4) break; // 界面最多塞4个垫背/仰望的

            // 无论升降，只要他的名次离你新名次足够近 (比如正负5名以内)
            // 或者他刚好卡在你新旧名次之间，就把他抓进 UI 里展示
            bool isBetweenRanks = (oldRank > 0 && p.Rank >= Mathf.Min(oldRank, newRank) && p.Rank <= Mathf.Max(oldRank, newRank));
            bool isCloseToNewRank = Mathf.Abs(p.Rank - newRank) <= 5;

            if (isBetweenRanks || isCloseToNewRank)
            {
                contextPlayers.Add(p);
            }
        }

        // 4. 再次确保按名次排好序，交给 UI 层
        return contextPlayers.OrderBy(p => p.Rank).ToList();
    }
    
    /// <summary>
    /// 全局通用：检查是否需要结算并弹出界面
    /// </summary>
    public IEnumerator CheckAndShowSettlementRoutine(string sourcePanel = null,System.Action<bool> onComplete = null)
    {
        bool isFetchFinished = false;
        bool hasSettlement = false;
        string oldLevelCode = "";
        string newLevelCode = "";
        string settlementType = "";
        int oldRank = 0;

        // 1. 请求后端获取玩家最新状态和结算信息
        yield return APIGateway.Instance.LeaderboardApi.CheckZenSettlement((res) =>
        {
            if (res != null)
            {
                hasSettlement = res.has_settlement;
                oldLevelCode = res.old_level;
                newLevelCode = res.current_level;
                settlementType = res.settlement_type;
                oldRank = res.old_rank;
            }
            isFetchFinished = true;
        });

        yield return new WaitUntil(() => isFetchFinished);

        // 2. 如果发生了结算，弹出结算 UI 
        if (hasSettlement)
        {
            GameDataManager.Instance.UserData.Zenlevel = newLevelCode;
            
            // 查找旧段位的 ID 以匹配奖励
            var oldState = ZenStates.FirstOrDefault(s => s.Code == oldLevelCode) ?? ZenStates[0];
            
            // 查找该发的奖励
            Dictionary<int, int> myRewards = null;
            var rewardConfig = RewardDatas.FirstOrDefault(r => r.State == oldState.Id && r.Rank == oldRank);
            if (rewardConfig != null) myRewards = rewardConfig.rewards;
            
            // 打开弹窗
            UIWindow uiWindow = SystemManager.Instance.ShowPanel(PanelType.ZenSettlementScreen);
            ZenSettlementScreen settleUI = uiWindow.GetComponent<ZenSettlementScreen>();
            if (settleUI != null)
            {
                // 传入排名、奖励、新段位名
                settleUI.ShowSettlement(oldRank, myRewards, oldLevelCode, newLevelCode, settlementType);
            }

            // 🌟 死锁等待：无论是大厅还是排行榜调用此方法，都会一直等到玩家点击关闭结算界面！
            yield return new WaitUntil(() => !SystemManager.Instance.PanelIsShowing(PanelType.ZenSettlementScreen));
            bool isClaimCompleted = false;
            yield return APIGateway.Instance.LeaderboardApi.ClaimZenReward((res) =>
            {
                // 收到服务端确认后，本地应用新段位
                GameDataManager.Instance.UserData.Zenlevel = res.new_level;
                
                // 🌟 核心状态切换：强行把玩家踢出榜单，要求他等会儿必须点雷达匹配重新加入！
                GameDataManager.Instance.UserData.isJoinedZenRank = false; 
                
                // 保存本地，并把变更为 false 的状态立刻同步给服务端
                GameDataManager.Instance.CommitGameData();
                
                isClaimCompleted = true;
            });
            yield return new WaitUntil(() => isClaimCompleted);
            Debug.Log("在打开加入面板前，已经完成领奖了");
            UIWindow window = SystemManager.Instance.ShowPanel(PanelType.ZenRankStartScreen);
            if (!string.IsNullOrEmpty(sourcePanel) && window != null)
                window.GetComponent<ZenRankStartScreen>().SetSourcePanel(sourcePanel);
            
            onComplete?.Invoke(true);
            Debug.Log("结算界面关闭，服务器已确认领奖，流程继续");
            ClearRankCache();
        }
        else
        {
            onComplete?.Invoke(false);
        }
    }
    
    /// <summary>
    /// 遍历服务器下发的所有玩家数据，根据我最新打出的绝对分数，预测我的真实名次
    /// </summary>
    public int PredictMyRealRank(int realOldRank, int expectedNewScore)
    {
        // 默认名次：如果没上榜，先给个极大的数
        int predictedRank = realOldRank > 0 ? realOldRank : 9999;
    
        var allRanks = new List<ZenRankState>();
        allRanks.AddRange(TopRanks);
        allRanks.AddRange(MiddleRanks);
        allRanks.AddRange(BottomRanks);

        // 剔除玩家自己
        string myName = GameDataManager.Instance.UserData.UserName;
        var otherPlayers = allRanks.Where(p => p.Name != myName && p.Rank > 0).ToList();

        // 拿着我的新分数，去跟所有人硬碰硬对比
        foreach (var p in otherPlayers)
        {
            // 只要我的分数大于等于他，并且他的名次比我现在的预测名次高，我就能顶替他！
            if (expectedNewScore >= p.Score && p.Rank < predictedRank)
            {
                predictedRank = p.Rank; 
            }
        }
        if (predictedRank == 9999) 
        {
            return otherPlayers.Count + 1;
        }
        // 兜底防御：最高是第 1 名
        return Mathf.Max(1, predictedRank);
    }
}
