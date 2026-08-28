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
        
        public bool HasShowName;           // 是否弹过名称设置
        public int applauseBgCounter;       // 鼓掌底板轮询 (1~5)
        public int longBannerCounter;      // 10号横幅轮询 (4~7)
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
        }

        public void InitData(OverallRankData data)
        {
            ZenScore = data.ZenScore;
            HasClickedZenEntrance = data.HasClickedZenEntrance;
            HasShowZenHelp = data.HasShowZenHelp;
            HasShowName = data.HasShowName;
            applauseBgCounter = data.applauseBgCounter == 0 ? 1 : data.applauseBgCounter;
            longBannerCounter = data.longBannerCounter == 0 ? 4 : data.longBannerCounter;
            
            HasViewedTotalRankUnlock = data.HasViewedTotalRankUnlock;
            HasViewedMonthlyRankUnlock = data.HasViewedMonthlyRankUnlock;
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