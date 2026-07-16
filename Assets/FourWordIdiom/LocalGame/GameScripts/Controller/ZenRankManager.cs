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
    public int RemainingSeconds { get; private set; } // 当前赛季剩余时间

    // 缓存上一次的排名和分数（用于结算动画比对）
    public int CachedOldRank { get; private set; }
    public int CachedOldScore { get; private set; }
    public bool IsFetching { get; private set; } // 数据请求状态锁
    
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
        
        TextAsset csvData = AssetBundleLoader.SharedInstance.LoadTextFile("gameinfo", "ZenStateTable");
        if (csvData != null)
        {
            ParseZenLevelItems(csvData.text);
        }
        // 加载奖励列表
        TextAsset textAsset = AssetBundleLoader.SharedInstance.LoadTextFile("gameinfo","ZenRankingRewardTable");
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
                StartGlobalTimer(RemainingSeconds);
                // 🌟 单独处理玩家自己的数据和缓存
                if (res.my != null)
                {
                    MyCurrentRankData = res.my;
                    
                    // 如果是本局第一次拿到有效数据，同步缓存，防播动画
                    if (CachedOldRank == 0)
                    {
                        CachedOldRank = MyCurrentRankData.rank;
                        CachedOldScore = MyCurrentRankData.score;
                    }
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
        string settleStr = MultilingualManager.Instance.GetString("Settling") ?? "结算中...";

        while (RemainingSeconds > 0)
        {
            string timeStr = FormatTime(RemainingSeconds, day, hour, minute, second);
            // 广播当前时间和格式化好的文本
            OnRankTimerTick?.Invoke(RemainingSeconds, timeStr); 
        
            yield return wait;
            RemainingSeconds--; // 全局统一递减
        }

        // 倒计时结束，广播结算状态
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
        
        // if (contextPlayers.Count < 4)
        // {
        //     int botsNeeded = 4 - contextPlayers.Count;
        //     
        //     // 1. 寻找当前环境里的【最大名次】和【最低分数】
        //     int maxExistingRank = newRank > 0 ? newRank : 1;
        //     int minExistingScore =  MyCurrentRankData?.score ?? 100;
        //
        //     foreach (var p in contextPlayers)
        //     {
        //         if (p.Rank > maxExistingRank) maxExistingRank = p.Rank;
        //         if (p.Score < minExistingScore) minExistingScore = p.Score;
        //     }
        //     
        //     // 2. 假人的名次严格从已有最大名次往后顺延
        //     int startBotRank = maxExistingRank + 1; 
        //     
        //     for (int i = 0; i < botsNeeded; i++)
        //     {
        //         contextPlayers.Add(new ZenRankState
        //         {
        //             Rank = startBotRank + i, // 保证绝不出现并列名次
        //             Score = Mathf.Max(10, minExistingScore - ((i + 1) * 35) + UnityEngine.Random.Range(-5, 5)), // 分数严格低于已有最低分
        //             Name = "神秘修士_" + UnityEngine.Random.Range(1000, 9999),
        //             Avatar = UnityEngine.Random.Range(0, 15) 
        //         });
        //     }
        // }

        // 4. 再次确保按名次排好序，交给 UI 层
        return contextPlayers.OrderBy(p => p.Rank).ToList();
    }
    // 动画播完后调用，对齐数据
    public void SyncCachedRank()
    {
        if (MyCurrentRankData != null)
        {
            CachedOldRank = MyCurrentRankData.rank;
            CachedOldScore = MyCurrentRankData.score;
        }
    }
    /// <summary>
    /// 全局通用：检查是否需要结算并弹出界面
    /// </summary>
    public IEnumerator CheckAndShowSettlementRoutine(System.Action<bool> onComplete = null)
    {
        bool isFetchFinished = false;
        bool hasSettlement = false;
        string oldLevelCode = "";
        string newLevelCode = "";
        string settlementType = "";
        int oldRank = 0;

        // 1. 请求后端获取玩家最新状态和结算信息
        yield return APIGateway.Instance.LoginApi.FetchUserProfile((res) =>
        {
            if (res != null)
            {
                GameDataManager.Instance.UserData.Zenlevel = res.zen_level;
                GameDataManager.Instance.UserData.zenCount = res.zen_count;
                
                hasSettlement = res.has_settlement;
                oldLevelCode = res.old_zen_level;
                newLevelCode = res.zen_level;
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
                GameDataManager.Instance.UserData.Zenlevel = newLevelCode;
                
                // 🌟 核心状态切换：强行把玩家踢出榜单，要求他等会儿必须点雷达匹配重新加入！
                GameDataManager.Instance.UserData.isJoinedZenRank = false; 
                
                // 保存本地，并把变更为 false 的状态立刻同步给服务端
                GameDataManager.Instance.CommitGameData();
                
                isClaimCompleted = true;
            });
            yield return new WaitUntil(() => isClaimCompleted);
            SystemManager.Instance.ShowPanel(PanelType.ZenRankStartScreen);
            onComplete?.Invoke(true);
            Debug.Log("结算界面关闭，服务器已确认领奖，流程继续");
        }
        else
        {
            onComplete?.Invoke(false);
        }
    }
}
