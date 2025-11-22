using System;
using System.Collections;
using System.Collections.Generic;
using Middleware;
using Newtonsoft.Json.Linq;
using UnityEngine;

[System.Serializable]
public class DifficultyAbilityData
{
    public int id;
    public float eValueInterval;
    public int wordChange;
}


public class DynamicHardManager : MonoBehaviour
{
    public static DynamicHardManager Instance;
    
    
    // 动态难度开关
    public int DynamicHardIsOpen = 1;
    
    // 初分层关卡号
    public int StartLevel = 9;
    
    // 平均判断时长
    public float AvgTimeA1 = 10f;
    public float AvgTimeB1 = 30f;
    public float AvgTimeD1 = 55f;
    
    // 第三关初始E值配置
    public float E1 = 1.8f;      // 第一层E值
    public float E2 = 1.5f;       // 第二层E值
    public float E3 = 0.8f;      // 第三层E值
    public float E4 = -1f;     // 第四层E值
    
    // 关卡通关判定时长
    public float firstLvTime = 1f;
    public float secondLvTime = 1f;
    public float a_value = 2f;
    
    public float firstLvValue = 0f;
    public float secondLvValue = 0f;
    
    // 能力变动参数 - 新增字段
    public int[] LevelBounds = new int[4]; // 关卡区间边界
    public float[] PropsReduction = new float[3]; // 道具使用E值减少参数
    
    // 不同关卡区间的E值变化参数配置
    public float[] TimeThresholds0 = new float[2]; // 8-10关时间阈值
    public float[] EChanges0 = new float[3];       // 8-10关E值变化量
    
    public float[] TimeThresholds1 = new float[4]; // 10-25关时间阈值
    public float[] EChanges1 = new float[5];       // 10-25关E值变化量
    
    public float[] TimeThresholds2 = new float[4]; // 25-60关时间阈值
    public float[] EChanges2 = new float[5];       // 25-60关E值变化量
    
    public float[] TimeThresholds3 = new float[4]; // 60-200关时间阈值
    public float[] EChanges3 = new float[5];       // 60-200关E值变化量
    
    public float[] TimeThresholds4 = new float[4]; // 200关之后时间阈值
    public float[] EChanges4 = new float[5];       // 200关之后E值变化量
    
    // 新增：难度能力值数据列表
    public List<DifficultyAbilityData> difficultyAbilityDataList = new List<DifficultyAbilityData>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 保持在场景切换时不销毁
        }
        else
        {
            Destroy(gameObject);
        }       
    }

    private void Start()
    {
        if (IsOpenDynamicHard())
        {
            LoadDynamicConfig();
        }
    }
    
    /// <summary>
    /// 加载动态配置表 
    /// </summary>
    public void LoadDynamicConfig()
    {
        // 从AssetBundle中加载CSV文件
        TextAsset csvFile = AdvancedBundleLoader.SharedInstance.LoadTextFile(ToolUtil.GetLanguageBundle(), "config_dynamicConfig");
        TextAsset levelcsvFile = AdvancedBundleLoader.SharedInstance.LoadTextFile(ToolUtil.GetLanguageBundle(), "config_levelDifficultyChange");
        if (csvFile != null)
        {
            // 将 CSV 数据转换为 JSON 格式
            ConvertCSVToJSON(csvFile.text);
            //Debug.Log(csvFile.text);
        }
        else
        {
            Debug.LogError("Failed to load config_dynamicConfig CSV data.");
        }
        
        if (levelcsvFile != null)
        {
            // 将 CSV 数据转换为 JSON 格式
            ConvertLevelCSVToJSON(levelcsvFile.text);
        }
        else
        {
            Debug.LogError("Failed to load config_levelDifficultyChange CSV data.");
        }
    }
    
     void ConvertLevelCSVToJSON(string data)
    {
        
        difficultyAbilityDataList.Clear();
        
        string[] lines = data.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        
        for (int i = 2; i < lines.Length; i++) // 从第三行开始，跳过标题行
        {
            string[] fields = lines[i].Split(',');
            if (fields.Length >= 3)
            {
                DifficultyAbilityData abilityData = new DifficultyAbilityData();
                
                if (int.TryParse(fields[0], out abilityData.id) &&
                    float.TryParse(fields[1], out abilityData.eValueInterval) &&
                    int.TryParse(fields[2], out abilityData.wordChange))
                {
                    difficultyAbilityDataList.Add(abilityData);
                }
                else
                {
                    Debug.LogWarning($"解析难度能力值数据失败: {lines[i]}");
                }
            }
            else
            {
                Debug.LogWarning($"跳过行 {i + 1}: 字段不足.");
            }
        }
        
        // 按E值区间排序，确保后续查找正确
        difficultyAbilityDataList.Sort((a, b) => a.eValueInterval.CompareTo(b.eValueInterval));
        
    }

    void ConvertCSVToJSON(string data)
    {
        // 用于构建 JSON 字符串
        string[] lines = data.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        for (int i = 2; i < lines.Length; i++) // 从第三行开始，跳过标题行
        {
            string[] fields = lines[i].Split(',');
            if (fields.Length >= 3)
            {
                string paramId = fields[0].Trim();
                string description = fields[1].Trim();
                string valueStr = fields[2].Trim();

                if (paramId == "firstLvTime")
                {
                    float.TryParse(valueStr, out firstLvTime);
                }
                else if (paramId == "secondLvTime")
                {
                    float.TryParse(valueStr, out secondLvTime);
                }
                else if (paramId == "cysy_dynamic")
                {
                    // 拆分参数值
                    string[] values = valueStr.Split('_');
                    if (values.Length >= 9)
                    {
                        int.TryParse(values[0], out DynamicHardIsOpen);
                        int.TryParse(values[1], out StartLevel);
                        float.TryParse(values[2], out AvgTimeA1);
                        float.TryParse(values[3], out AvgTimeB1);
                        float.TryParse(values[4], out AvgTimeD1);
                        float.TryParse(values[5], out E1);
                        float.TryParse(values[6], out E2);
                        float.TryParse(values[7], out E3);
                        float.TryParse(values[8], out E4);
                    }
                    else
                    {
                        Debug.LogError("cysy_dynamic values count mismatch.");
                    }

                    StartLevel += 1;
                }
                else if (paramId == "cysy_dynamicLevel")
                {
                    // 处理能力变动参数
                    ParseDynamicLevelConfig(valueStr);
                }
                else if (paramId == "cysy_cd_a")
                {
                    float.TryParse(valueStr, out a_value);
                }
            }
            else
            {
                Debug.LogWarning($"Skipping line {i + 1}: Not enough fields.");
            }
        }
    }
    
    /// <summary>
    /// 解析能力变动参数配置
    /// </summary>
    /// <param name="jsonStr">JSON格式的配置字符串</param>
    private void ParseDynamicLevelConfig(string jsonStr)
    {
        // try
        // {
        
            // 修复JSON字符串格式问题
            string cleanedJson = jsonStr.Trim();
            
            // 移除可能的多余引号
            if (cleanedJson.StartsWith("\"") && cleanedJson.EndsWith("\""))
            {
                cleanedJson = cleanedJson.Substring(1, cleanedJson.Length - 2);
            }
            
            // 替换全角字符为半角字符
            cleanedJson = cleanedJson
                .Replace("_", ",")  // 全角逗号 -> 半角逗号
                .Replace("：", ":")  // 全角冒号 -> 半角冒号
                .Replace("\"\"", "\""); // 处理转义引号
            
            // Debug.Log($"清理后的JSON: {cleanedJson}");
            
            // 解析JSON
            JObject jsonObj = JObject.Parse(cleanedJson);
            JArray keyArray = (JArray)jsonObj["key"];
            
            if (keyArray != null && keyArray.Count > 0)
            {
                JObject configObj = (JObject)keyArray[0];
                
                // 解析关卡区间边界
                if (configObj["n"] != null)
                {
                    string[] bounds = configObj["n"].ToString().Split(',');
                    if (bounds.Length == 4)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            int.TryParse(bounds[i], out LevelBounds[i]);
                        }
                    }
                }
                
                // 解析道具使用E值减少参数
                if (configObj["c"] != null)
                {
                    string[] reductions = configObj["c"].ToString().Split(',');
                    if (reductions.Length == 3)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            float.TryParse(reductions[i], out PropsReduction[i]);
                        }
                    }
                }
                
                // 解析8-10关参数
                ParseLevelConfig(configObj, "time0", "e0", TimeThresholds0, EChanges0);
                
                // 解析10-25关参数
                ParseLevelConfig(configObj, "time1", "e1", TimeThresholds1, EChanges1);
                
                // 解析25-60关参数
                ParseLevelConfig(configObj, "time2", "e2", TimeThresholds2, EChanges2);
                
                // 解析60-200关参数
                ParseLevelConfig(configObj, "time3", "e3", TimeThresholds3, EChanges3);
                
                // 解析200关之后参数
                ParseLevelConfig(configObj, "time4", "e4", TimeThresholds4, EChanges4);
            }
        // }
        // catch (Exception ex)
        // {
        //     Debug.LogError($"解析cysy_dynamicLevel配置失败: {ex.Message}");
        // }
    }
    
    /// <summary>
    /// 解析单个关卡区间的配置
    /// </summary>
    private void ParseLevelConfig(JObject configObj, string timeKey, string eKey, float[] timeThresholds, float[] eChanges)
    {
        if (configObj[timeKey] != null)
        {
            string[] times = configObj[timeKey].ToString().Split(',');
            for (int i = 0; i < Mathf.Min(times.Length, timeThresholds.Length); i++)
            {
                float.TryParse(times[i], out timeThresholds[i]);
            }
        }
        
        if (configObj[eKey] != null)
        {
            string[] changes = configObj[eKey].ToString().Split(',');
            for (int i = 0; i < Mathf.Min(changes.Length, eChanges.Length); i++)
            {
                float.TryParse(changes[i], out eChanges[i]);
            }
        }
    }
    
    
    /// <summary>
    /// 根据玩家当前E值获取目标词处理数据
    /// </summary>
    /// <param name="currentEValue">玩家当前E值</param>
    /// <returns>目标词处理值，如果未找到则返回0</returns>
    private int GetWordChangeByEValue(float currentEValue)
    {
        if (difficultyAbilityDataList == null || difficultyAbilityDataList.Count == 0)
        {
            Debug.LogWarning("难度能力值数据未初始化");
            return 0;
        }
        
        // 查找第一个E值区间大于等于当前E值的配置
        for (int i = 0; i < difficultyAbilityDataList.Count; i++)
        {
            if (currentEValue <= difficultyAbilityDataList[i].eValueInterval)
            {
                return difficultyAbilityDataList[i].wordChange;
            }
        }
        
        // 如果当前E值大于所有区间，返回最后一个配置的值
        return difficultyAbilityDataList[difficultyAbilityDataList.Count - 1].wordChange;
    }
    
    /// <summary>
    /// 根据玩家当前E值获取完整的难度能力值数据
    /// </summary>
    /// <param name="currentEValue">玩家当前E值</param>
    /// <returns>难度能力值数据，如果未找到则返回null</returns>
    private DifficultyAbilityData GetDifficultyAbilityDataByEValue(float currentEValue)
    {
        if (difficultyAbilityDataList == null || difficultyAbilityDataList.Count == 0)
        {
            Debug.LogWarning("难度能力值数据未初始化");
            return null;
        }
        
        // 查找第一个E值区间大于等于当前E值的配置
        for (int i = 0; i < difficultyAbilityDataList.Count; i++)
        {
            if (currentEValue <= difficultyAbilityDataList[i].eValueInterval)
            {
                return difficultyAbilityDataList[i];
            }
        }
        
        // 如果当前E值大于所有区间，返回最后一个配置
        return difficultyAbilityDataList[difficultyAbilityDataList.Count - 1];
    }

    /// <summary>
    /// 检查是否需要改变难度
    /// </summary>
    /// <returns>目标词处理值，如果未找到则返回0</returns>
    public int CheckLevelHardChange(int level)
    {
        //是否开启动态难度调整
        if (!IsOpenDynamicHard()) return 0;
        
        // 检查是否到达动态难度起始关卡
        if (level <= StartLevel)  return 0;
        
        // 根据玩家当前E值获取目标词处理
        float playerEValue = GameDataManager.Instance.DynamicHardSave.EnergyValue;
        
        // 获取完整的难度能力值数据
        DifficultyAbilityData abilityData = GetDifficultyAbilityDataByEValue(playerEValue);
        if (abilityData != null)
        {
            Debug.Log($" 关卡动态难度 关卡：{level} ID: {abilityData.id}, E值区间: {abilityData.eValueInterval}, 目标词处理: {abilityData.wordChange}");
            
            // if (abilityData.id >= 6&&abilityData.id>GameDataManager.Instance.DynamicHardSave.MaxEnergyLevel
            //     &&StageHexController.Instance.GetUserToolCount()>0)
            // {
            //     GameDataManager.Instance.DynamicHardSave.UpdateEnergy(-a_value);
            //     GameDataManager.Instance.DynamicHardSave.UpdateMaxEnergyLevel(abilityData.id);
            //     
            //     Debug.Log($" 关卡动态难度 E值动态修正 原来E值为: {playerEValue+a_value} , 当前E值为: {playerEValue}, 动态难度等级为: {abilityData.id}");
            // }
        }
        
        int wordChange = GetWordChangeByEValue(playerEValue);
        Debug.Log($"关卡动态难度 玩家E值 {playerEValue} 对应的目标词处理: {wordChange}");

       

        return wordChange;
    }

    private bool IsOpenDynamicHard()
    {
        return DynamicHardIsOpen == 1;
        //return false;
    }
  
    /// <summary>
    /// 记录关卡通关时间、道具使用情况和道具提示词数
    /// </summary>
    /// <param name="level">关卡编号</param>
    /// <param name="clearTime">通关时长(秒)</param>
    /// <param name="usedProps">是否使用道具</param>
    /// <param name="propsWordCount">道具提示的词数</param>
    public void RecordLevelClearData(int level, float clearTime, bool usedProps, float propsWordCount = 0f)
    {
        if (!IsOpenDynamicHard()) return;
        
        // 记录第通关时长
        GameDataManager.Instance.DynamicHardSave.RecordLevelClearTime(level, clearTime);
        
        GameDataManager.Instance.DynamicHardSave.UsedProps = usedProps;
        GameDataManager.Instance.DynamicHardSave.PropsWordCount = propsWordCount;
        
        // 保存更新后的数据到JSON文件
        GameDataManager.Instance.DynamicHardSave.SaveData();
    }

    /// <summary>
    /// 检查关卡通关条件并执行难度介入
    /// </summary>
    /// <param name="level">当前关卡</param>
    /// <param name="errorCount">连错次数</param>
    /// <param name="targetWordCount">关卡目标词数</param>
    /// <param name="clearTime">通关时长(秒)</param>
    /// <param name="usedProps">是否使用道具</param>
    /// <param name="propsWordCount">道具提示的词数</param>
    public void CheckLevelClearConditions(int level, int errorCount, int targetWordCount, float clearTime, bool usedProps, float propsWordCount = 0f)
    {
        if (!IsOpenDynamicHard()) return;

        if (level <= StartLevel)
        {
            // 记录通关数据
            RecordLevelClearData(level, clearTime, usedProps, propsWordCount);
        }
        
        //1、2关卡通过时长为1，表示玩家不可能通过，不触发E值介入逻辑
        switch (level)
        {
            case 1:
                if (CheckInterventionConditions(errorCount, targetWordCount, clearTime, firstLvTime))
                {
                    GameDataManager.Instance.DynamicHardSave.EnergyValue = firstLvValue;
                    GameDataManager.Instance.DynamicHardSave.SaveData();
                    Debug.Log($"关卡动态难度 第{level}关 触发E值介入，E值设置为 {firstLvValue}");
                }
                break;
            case 2:
                if (CheckInterventionConditions(errorCount, targetWordCount, clearTime, secondLvTime))
                {
                    GameDataManager.Instance.DynamicHardSave.EnergyValue = secondLvValue;
                    GameDataManager.Instance.DynamicHardSave.SaveData();
                    Debug.Log($"关卡动态难度 第{level}关 触发E值介入，E值设置为 {secondLvValue}");
                }
                break;
        }

        if (level >= StartLevel)
        {
            if (level == StartLevel)
            {
                // 计算前StartLevel关平均通关时长
                float avgTime = GetCumulativeClearTime();
                
                // 根据累积时长进行分层并设置初始E值
                SetInitialEnergyValue(avgTime);
            }
            // 多区间关卡通用E值调整逻辑
            AdjustEnergyValue(level, clearTime, usedProps, propsWordCount);
        }
        
    }

    /// <summary>
    /// 获取前三关累积通关时长
    /// </summary>
    private float GetCumulativeClearTime()
    {
        float avgTime = GameDataManager.Instance.DynamicHardSave.GetLevelTotalTime(StartLevel)/StartLevel;
        
        return avgTime;
    }

    /// <summary>
    /// 根据累积通关时长设置初始E值
    /// </summary>
    private void SetInitialEnergyValue(float avgTime)
    {
        float energyValue = 0f;
        
        if (avgTime <= AvgTimeA1)
        {
            energyValue = E1;
        }
        else if (avgTime > AvgTimeA1 && avgTime <= AvgTimeB1)
        {
            energyValue = E2;
        }
        else if (avgTime > AvgTimeB1 && avgTime <= AvgTimeD1)
        {
            energyValue = E3;
        }
        else // totalTime > D1
        {
            energyValue = E4;
        }
        
        GameDataManager.Instance.DynamicHardSave.EnergyValue = energyValue;
        GameDataManager.Instance.DynamicHardSave.SaveData();
        Debug.Log($"关卡动态难度 第{StartLevel}关 触发E值介入，E值设置为 {energyValue}");
    }

    /// <summary>
    /// 根据通关时间和是否使用道具调整E值
    /// </summary>
    private void AdjustEnergyValue(int level, float clearTime, bool usedProps, float propsWordCount)
    {
        float energyChange = 0f;
        string levelDescription = "";
        
        if (usedProps)
        {
            // 使用了道具，减少E值
            energyChange = CalculateEnergyDecrease(propsWordCount);
            levelDescription = $"使用道具({propsWordCount}词)";
        }
        else
        {
            // 未使用道具，根据通关时间增加E值
            energyChange = CalculateEnergyIncrease(level, clearTime);
        }
        
        // 应用E值变化
        float oldEnergy = GameDataManager.Instance.DynamicHardSave.EnergyValue;
        GameDataManager.Instance.DynamicHardSave.UpdateEnergy(energyChange);
        
        // 保存更新后的数据
        GameDataManager.Instance.DynamicHardSave.SaveData();
        
        string changeType = usedProps ? "减少" : "增加";
        Debug.Log($" 关卡动态难度 第{level}关通关，时长{clearTime}秒，E值{changeType}: {oldEnergy} -> {GameDataManager.Instance.DynamicHardSave.EnergyValue}");
    }

    /// <summary>
    /// 计算未使用道具时的E值增加量
    /// </summary>
    private float CalculateEnergyIncrease(int level, float clearTime)
    {
        float energyChange = 0f;
        
        // 根据关卡区间选择不同的参数
        if (level >= StartLevel && level <= LevelBounds[0]) // 8-10关
        {
            // 获取8-10关参数
            float[] timeThresholds = TimeThresholds0;
            float[] eChanges = EChanges0;
            
            if (timeThresholds.Length >= 2 && eChanges.Length >= 3)
            {
                if (clearTime <= timeThresholds[0])
                {
                    energyChange = eChanges[0];
                }
                else if (clearTime <= timeThresholds[1])
                {
                    energyChange = eChanges[1];
                }
                else
                {
                    energyChange = eChanges[2];
                }
            }
            else
            {
                Debug.LogError("8-10关参数配置错误");
            }
        }
        else if (level > LevelBounds[0] && level <= LevelBounds[1]) // 10-25关
        {
            // 获取10-25关参数
            float[] timeThresholds = TimeThresholds1;
            float[] eChanges = EChanges1;
            
            if (timeThresholds.Length >= 4 && eChanges.Length >= 5)
            {
                if (clearTime <= timeThresholds[0])
                {
                    energyChange = eChanges[0];
                }
                else if (clearTime <= timeThresholds[1])
                {
                    energyChange = eChanges[1];
                }
                else if (clearTime <= timeThresholds[2])
                {
                    energyChange = eChanges[2];
                }
                else if (clearTime <= timeThresholds[3])
                {
                    energyChange = eChanges[3];
                }
                else
                {
                    energyChange = eChanges[4];
                }
            }
            else
            {
                Debug.LogError("10-25关参数配置错误");
            }
        }
        else if (level > LevelBounds[1] && level <= LevelBounds[2]) // 25-60关
        {
            // 获取25-60关参数
            float[] timeThresholds = TimeThresholds2;
            float[] eChanges = EChanges2;
            
            if (timeThresholds.Length >= 4 && eChanges.Length >= 5)
            {
                if (clearTime <= timeThresholds[0])
                {
                    energyChange = eChanges[0];
                }
                else if (clearTime <= timeThresholds[1])
                {
                    energyChange = eChanges[1];
                }
                else if (clearTime <= timeThresholds[2])
                {
                    energyChange = eChanges[2];
                }
                else if (clearTime <= timeThresholds[3])
                {
                    energyChange = eChanges[3];
                }
                else
                {
                    energyChange = eChanges[4];
                }
            }
            else
            {
                Debug.LogError("25-60关参数配置错误");
            }
        }
        else if (level > LevelBounds[2] && level <= LevelBounds[3]) // 60-200关
        {
            // 获取60-200关参数
            float[] timeThresholds = TimeThresholds3;
            float[] eChanges = EChanges3;
            
            if (timeThresholds.Length >= 4 && eChanges.Length >= 5)
            {
                if (clearTime <= timeThresholds[0])
                {
                    energyChange = eChanges[0];
                }
                else if (clearTime <= timeThresholds[1])
                {
                    energyChange = eChanges[1];
                }
                else if (clearTime <= timeThresholds[2])
                {
                    energyChange = eChanges[2];
                }
                else if (clearTime <= timeThresholds[3])
                {
                    energyChange = eChanges[3];
                }
                else
                {
                    energyChange = eChanges[4];
                }
            }
            else
            {
                Debug.LogError("60-200关参数配置错误");
            }
        }
        else if (level > LevelBounds[3]) // 200关之后
        {
            // 获取200关之后参数
            float[] timeThresholds = TimeThresholds4;
            float[] eChanges = EChanges4;
            
            if (timeThresholds.Length >= 4 && eChanges.Length >= 5)
            {
                if (clearTime <= timeThresholds[0])
                {
                    energyChange = eChanges[0];
                }
                else if (clearTime <= timeThresholds[1])
                {
                    energyChange = eChanges[1];
                }
                else if (clearTime <= timeThresholds[2])
                {
                    energyChange = eChanges[2];
                }
                else if (clearTime <= timeThresholds[3])
                {
                    energyChange = eChanges[3];
                }
                else
                {
                    energyChange = eChanges[4];
                }
            }
            else
            {
                Debug.LogError("200关之后参数配置错误");
            }
        }
        
        return energyChange;
    }

    /// <summary>
    /// 计算使用道具时的E值减少量
    /// </summary>
    private float CalculateEnergyDecrease(float propsWordCount)
    {
        float energyDecrease = 0f;
        
        if (propsWordCount <= 1f)
        {
            energyDecrease = PropsReduction[0];
        }
        else if (propsWordCount <= 2f)
        {
            energyDecrease = PropsReduction[1];
        }
        else // propsWordCount >= 3
        {
            energyDecrease = PropsReduction[2];
        }
        
        return energyDecrease;
    }

    /// <summary>
    /// 计算道具提示的词数（根据道具类型和次数）
    /// </summary>
    /// <param name="refreshCount">刷新道具使用次数</param>
    /// <param name="magnifierCount">放大镜道具使用次数</param>
    /// <returns>总道具提示词数</returns>
    public float CalculatePropsWordCount(int refreshCount, int magnifierCount)
    {
        // 刷新道具：每次使用相当于0.5个词
        // 放大镜道具：每次使用相当于1个词
        return (refreshCount * 0.5f) + (magnifierCount * 1f);
    }

    /// <summary>
    /// 验证是否满足介入条件
    /// </summary>
    private bool CheckInterventionConditions(int errorCount, int targetCount, float time, float threshold)
    {
        // 计算失误率（连错次数/目标词数）
        float errorRate = (targetCount > 0) ? (float)errorCount / targetCount : 0f;
        
        // 条件1: 失误率必须为0 
        // 条件2: 通关时长小于等于阈值
        return Mathf.Approximately(errorRate, 0f) && time <= threshold;
    }
    
    /// <summary>
    /// 清空存储的关卡通关时间
    /// </summary>
    public void ClearStoredLevelTimes()
    {
        // 清空关卡时间
        GameDataManager.Instance.DynamicHardSave.LevelClearTimes.Clear();
        GameDataManager.Instance.DynamicHardSave.UsedProps = false;
        GameDataManager.Instance.DynamicHardSave.PropsWordCount = 0f;
        
        // 保存更新后的数据到JSON文件
        GameDataManager.Instance.DynamicHardSave.SaveData();
    }
}