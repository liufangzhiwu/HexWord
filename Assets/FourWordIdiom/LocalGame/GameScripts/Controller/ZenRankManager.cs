using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ZenRankManager : MonoBehaviour
{
    public static ZenRankManager Instance { get; private set; }
    
    public List<ZenLevelState> ZenStates { get; private set; } = new List<ZenLevelState>();
    public List<ZenRewardData> RewardDatas { get; private set; } = new List<ZenRewardData>();
    
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
    
    /// <summary>
    /// 全局通用：检查是否需要结算并弹出界面
    /// </summary>
    public IEnumerator CheckAndShowSettlementRoutine()
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
            
            if (oldRank <= 0 || myRewards == null || myRewards.Count == 0)
            {
                Debug.Log($"结算拦截：玩家上赛季排名为 {oldRank}，无奖励，静默处理不弹窗。");
                yield break; // 🌟 结束协程，不往下走
            }
            // 打开弹窗
            UIWindow uiWindow = SystemManager.Instance.ShowPanel(PanelType.ZenSettlementScreen);
            ZenSettlementScreen settleUI = uiWindow.GetComponent<ZenSettlementScreen>();
            if (settleUI != null)
            {
                settleUI.ShowSettlement(oldRank, myRewards, newLevelCode, settlementType); // 传入排名、奖励、新段位名
            }

            // 🌟 死锁等待：无论是大厅还是排行榜调用此方法，都会一直等到玩家点击关闭结算界面！
            yield return new WaitUntil(() => !SystemManager.Instance.PanelIsShowing(PanelType.ZenSettlementScreen));
            
            Debug.Log("结算界面关闭，流程继续");
        }
    }
}
