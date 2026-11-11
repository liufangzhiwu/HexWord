using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace FourWordIdiom.LocalGame.GameScripts.Controller.SaveSystem
{
    /**
     * 总榜养成数据
     */
    [Serializable]
    public class OverallRankData
    {
        public int ZenScore;               // 禅意分数量
        public bool HasClickedZenEntrance; // 是否未点击过,第一次点击
        public bool HasShowZenHelp;        // 是否第一次弹过
        public bool HasViewedTotalRankUnlock;  // 记录是否已经点掉过“总榜解锁”的新角标
        public bool HasViewedMonthlyRankUnlock; // 记录是否已经点掉过“月榜解锁”的新角标
        public bool HasViewedEmojiTip;        // 记录是否展示过emoji气泡
        
        public bool HasShowName;           // 是否弹过名称设置
        public int applauseBgCounter;       // 鼓掌底板轮询 (1~5)
        public int longBannerCounter;      // 10号横幅轮询 (4~7)

        // ===== 同步自 WT 1.9.8（破冰激励 / 入场激励 / 新手奖励 / 特殊金币关）=====
        public int BeginnerTitleLoopIndex;  // 短标题轮询
        public long LastSkipLevelTime = 0; // 上次跳关的绝对时间戳（秒）
        
        public float SpecialGoldForceC = 0f; // 特殊金币关逼迫系数C
        public bool HasTriggeredFirstExtraHardGold = false; // 是否已经触发过极难关的必出金币
        public bool hasUsedIceBreakFree; // 是否首次破冰广告
        
        // 用于记录每个横幅类型(BannerType)或激励ID上一次触发的真实时间，处理 TimeWindow 冷却
        public Dictionary<int, float> LastEnterStimulateTimes = new Dictionary<int, float>();
        // 记录对应 StimulateID 的文案和图标当前轮播到了第几个索引
        public Dictionary<int, int> TextLoopIndices = new Dictionary<int, int>();
        public Dictionary<int, int> EmojiLoopIndices = new Dictionary<int, int>();
        // 记录已经展示过开局横幅的关卡 ID
        public List<int> ShownEnterBannerStageIds = new List<int>();
        public List<int> ClaimedGoldStageIds = new List<int>();

        public bool CanIsLaunch = false;

        public string Getfilepath => Path.Combine(Application.persistentDataPath, "OverallRankData.json");

        #region 数据加载

        public void InitData()
        {
            this.ZenScore = 0;
            this.HasClickedZenEntrance = false;
            this.HasShowZenHelp = false;
            this.HasShowName = false;
            this.applauseBgCounter = 1;
            this.longBannerCounter = 4;
            this.HasViewedTotalRankUnlock = false;
            this.HasViewedMonthlyRankUnlock = false;
            // ===== 同步自 WT 1.9.8 =====
            this.LastSkipLevelTime = 0;
            this.LastEnterStimulateTimes = new Dictionary<int, float>();
            this.TextLoopIndices = new Dictionary<int, int>();
            this.EmojiLoopIndices = new Dictionary<int, int>();
            this.SpecialGoldForceC = 0;
            this.HasTriggeredFirstExtraHardGold = false;
            this.hasUsedIceBreakFree = false;
            this.BeginnerTitleLoopIndex = 1;
            this.HasViewedEmojiTip = false;
            this.ClaimedGoldStageIds = new List<int>();
            this.ShownEnterBannerStageIds = new List<int>();
            this.CanIsLaunch = false;
        }

        public void InitData(OverallRankData data)
        {
            ZenScore = data.ZenScore;
            HasClickedZenEntrance = data.HasClickedZenEntrance;
            HasShowZenHelp = data.HasShowZenHelp;
            HasShowName = data.HasShowName;
            applauseBgCounter = data.applauseBgCounter == 0 ? 1 : data.applauseBgCounter;
            longBannerCounter = data.longBannerCounter == 0 ? 4 : data.longBannerCounter;
            // 跳关冷却时间戳必须随存档回读，否则重启后冷却被清零（同步自 WT 1.9.8）
            LastSkipLevelTime = data.LastSkipLevelTime;
            
            HasViewedTotalRankUnlock = data.HasViewedTotalRankUnlock;
            HasViewedMonthlyRankUnlock = data.HasViewedMonthlyRankUnlock;
            LastEnterStimulateTimes = data.LastEnterStimulateTimes ?? new Dictionary<int, float>();
            TextLoopIndices = data.TextLoopIndices ?? new Dictionary<int, int>();
            EmojiLoopIndices = data.EmojiLoopIndices ?? new Dictionary<int, int>();
            
            SpecialGoldForceC = data.SpecialGoldForceC;
            HasTriggeredFirstExtraHardGold = data.HasTriggeredFirstExtraHardGold;
            hasUsedIceBreakFree = data.hasUsedIceBreakFree;
            BeginnerTitleLoopIndex = data.BeginnerTitleLoopIndex;
            HasViewedEmojiTip = data.HasViewedEmojiTip;
            ClaimedGoldStageIds = data.ClaimedGoldStageIds ?? new List<int>();
            ShownEnterBannerStageIds = data.ShownEnterBannerStageIds ?? new List<int>();
            CanIsLaunch = data.CanIsLaunch;
        }

        // 添加一个辅助方法
        public void MarkStageGoldGenerated(int stageId)
        {
            if (!ClaimedGoldStageIds.Contains(stageId))
            {
                ClaimedGoldStageIds.Add(stageId);
            }
        }

        public bool HasClaimedGoldInStage(int stageId)
        {
            return ClaimedGoldStageIds.Contains(stageId);
        }
        // 开局横幅的辅助检查与写入方法
        public void MarkEnterBannerShown(int stageId)
        {
            if (!ShownEnterBannerStageIds.Contains(stageId))
            {
                ShownEnterBannerStageIds.Add(stageId);
            }
        }
        public bool HasShownEnterBanner(int stageId)
        {
            return ShownEnterBannerStageIds.Contains(stageId);
        }
        public void LoadData()
        {
            try
            {
                if (File.Exists(Getfilepath))
                {
                    string decrypted = File.ReadAllText(Getfilepath, System.Text.Encoding.UTF8);
                    string json = SecurityProvider.RestoreData(decrypted);
                    if (!IsValidJson(json))
                    {
                        Debug.LogError("总榜养成文件 JSON格式错误：" + json);
                        InitData();
                    }
                    else
                    {
                        OverallRankData butterflyData = JsonConvert.DeserializeObject<OverallRankData>(json);
                        if (butterflyData == null)
                        {
                            Debug.Log("总榜养成数据加载异常: " + json);
                            InitData();
                        }
                        else
                        {
                            InitData(butterflyData);
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("总榜养成没有找到数据文件, 返回默认数据.");
                    InitData();
                }
            }
            catch (Exception e)
            {
                Debug.LogError("总榜养成数据加载失败: " + e);
                InitData();
            }
        }

        public void SaveData()
        {
            string oldJson = JsonConvert.SerializeObject(this, Formatting.Indented);
            string json = SecurityProvider.ProtectData(oldJson);
            File.WriteAllText(Getfilepath, json);
        }

        public bool IsValidJson(string json)
        {
            try
            {
                // 尝试解析 JSON 数据，若格式错误会抛出异常
                JToken.Parse(json);
                return true; // JSON 格式正确
            }
            catch (JsonException)
            {
                return false; // JSON 格式错误
            }
        }

        #endregion
    }
}