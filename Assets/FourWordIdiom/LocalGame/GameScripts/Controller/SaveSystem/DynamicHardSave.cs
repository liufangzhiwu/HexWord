using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class DynamicHardSave
{
    /// <summary>
    /// 动态难度E值
    /// </summary>
    public float EnergyValue; // 改为float类型以支持小数变化
    
    /// <summary>
    /// 动态难度最大等级
    /// </summary>
    public int MaxEnergyLevel;
    
    /// <summary>
    /// 是否使用了道具
    /// </summary>
    public bool UsedProps;
    
    /// <summary>
    /// 使用的道具提示的词数
    /// </summary>
    public float PropsWordCount;
    
    /// <summary>
    /// 新增：记录所有关卡的通关时长
    /// </summary>
    public Dictionary<int, float> LevelClearTimes = new Dictionary<int, float>();
    
    public string Getfilepath
    {
        get
        {
            return Path.Combine(Application.persistentDataPath, "DynamicHardSave.json");
        }
    }
    
    /// <summary>
    /// 首次启动游戏数据初始化
    /// </summary>
    public void InitData()
    {
        EnergyValue = 0f;
        MaxEnergyLevel = 0;
        UsedProps = false;
        PropsWordCount = 0f;
        LevelClearTimes.Clear();
    }
       
    /// <summary>
    /// 初始化保存的数据
    /// </summary>
    /// <param name="dynamicHardSave"></param>
    public void InitData(DynamicHardSave dynamicHardSave)
    {
        EnergyValue = dynamicHardSave.EnergyValue;
        MaxEnergyLevel= dynamicHardSave.MaxEnergyLevel;
        UsedProps = dynamicHardSave.UsedProps;
        PropsWordCount = dynamicHardSave.PropsWordCount;
        
        // 复制关卡通关时长字典
        LevelClearTimes.Clear();
        foreach (var kvp in dynamicHardSave.LevelClearTimes)
        {
            LevelClearTimes.Add(kvp.Key, kvp.Value);
        }
    }
    
    /// <summary>
    /// 更新E值
    /// </summary>
    /// <param name="value"></param>
    public void UpdateEnergy(float value)
    {
        EnergyValue += value;
        
        Debug.Log($" 关卡动态难度 更新E值后: {EnergyValue} 增加的E值为{value}");
    }
    
    /// <summary>
    /// 更新最大等级
    /// </summary>
    /// <param name="value"></param>
    public void UpdateMaxEnergyLevel(int levelValue)
    {
        if(levelValue > MaxEnergyLevel)
            MaxEnergyLevel = levelValue;
        
        Debug.Log($" 关卡动态难度 更新最大等级后: {MaxEnergyLevel}");
    }
    
    /// <summary>
    /// 记录关卡通关时长
    /// </summary>
    /// <param name="level">关卡编号</param>
    /// <param name="clearTime">通关时长</param>
    public void RecordLevelClearTime(int level, float clearTime)
    {
        if (LevelClearTimes.ContainsKey(level))
        {
            LevelClearTimes[level] = clearTime;
        }
        else
        {
            LevelClearTimes.Add(level, clearTime);
        }
    }
    
    /// <summary>
    /// 获取关卡通关时长
    /// </summary>
    /// <param name="level">关卡编号</param>
    /// <returns>通关时长，如果未通关则返回0</returns>
    public float GetLevelClearTime(int level)
    {
        if (LevelClearTimes.ContainsKey(level))
        {
            return LevelClearTimes[level];
        }
        
        return 0f;
    }

    /// <summary>
    /// 获取指定关卡之前所有关卡的总时长
    /// </summary>
    /// <param name="level"></param>
    /// <returns></returns>
    public float GetLevelTotalTime(int level)
    {
        float totalTime = 0f;
        
        // 计算1到8关的总时长
        for (int i = 1; i <= level; i++)
        {
            // 使用字典中的值（如果存在）
            if (LevelClearTimes.ContainsKey(i))
            {
                totalTime += LevelClearTimes[i];
            }
        }
        
        return totalTime;
    }
   
    /// <summary>
    /// 加载数据 
    /// </summary>
    public void LoadData()
    {           
        string filePath = Getfilepath;

        try
        {
            if (File.Exists(filePath))
            {
                string Dejson = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
           
                string json = SecurityProvider.RestoreData(Dejson); //解密   
                // Debug.Log("关卡动态数据加载路径: " + filePath + "读取json数据" + Dejson + "解密后数据" + json);
                // 验证 JSON 数据格式
                if (!IsValidJson(json))
                { 
                    Debug.LogError("关卡动态JSON 格式错误: " + json);
                    InitData();
                }
                else
                {
                    DynamicHardSave dynamicHardSave = JsonConvert.DeserializeObject<DynamicHardSave>(json);               
                   
                    if (dynamicHardSave == null)
                    { 
                        Debug.Log("关卡动态数据加载异常: " + json);
                        InitData();
                    }
                    else
                    {
                        InitData(dynamicHardSave);
                    }
                }
            }
            else
            {
                Debug.LogWarning("关卡动态没有找到数据文件, 返回默认数据.");
                InitData();
            }      
        }
        catch (Exception e)
        {
            Debug.LogError("关卡动态数据加载失败: " + e);
            InitData();
        }
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
    

    // 保存数据
    public void SaveData()
    {     
        string filePath = Getfilepath;
        string oldjson = JsonConvert.SerializeObject(this, Formatting.Indented); // 转换为 JSON 格式          
        string json = SecurityProvider.ProtectData(oldjson); //加密
        File.WriteAllText(filePath, json); // 写入文件
        Debug.Log("关卡动态数据已保存: " + json);
    }
   
}