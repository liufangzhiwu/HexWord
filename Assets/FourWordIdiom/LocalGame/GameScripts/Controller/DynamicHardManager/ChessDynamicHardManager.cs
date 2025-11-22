using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Middleware;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// 检查关卡配置
/// </summary>
[Serializable]
public readonly struct CheckOpen
{
    public readonly int   Level;  // 第几关检查
    public readonly int   Time;   // 检查小于时间
    public readonly float Value;   // 初始e值
    
    public CheckOpen(int level, int time, float value)
    {
        Level = level;
        Time  = time;
        Value = value;
    }
}

/// <summary>
/// 关卡区间e值配置
/// </summary>
[System.Serializable]
public  class LevelDifficultyData
{
    public int groupId;        // e值id
    public  int wordChange;   // 改变的展示字
    public  float intervalE01;  // 关卡一区间的值
    public  float intervalE02;  // 关卡二区间的值
    public  float intervalE03;  // 关卡三区间的值
    public  float intervalE04;  // 关卡四区间的值
    public  float intervalE05;  // 关卡五区间的值

    public LevelDifficultyData(int groupId,int wordChange, float intervalE01, float intervalE02, float intervalE03,
        float intervalE04, float intervalE05)
    {
        this.groupId = groupId;
        this.wordChange = wordChange;
        this.intervalE01 = intervalE01;
        this.intervalE02 = intervalE02;
        this.intervalE03 = intervalE03;
        this.intervalE04 = intervalE04;
        this.intervalE05 = intervalE05;
    }
}
/// <summary>
/// 拼字动态难度
/// </summary>
public class ChessDynamicHardManager : MonoBehaviour
{
    public static ChessDynamicHardManager Instance;
    readonly string[] intervalFields = { "intervalE01", "intervalE02", "intervalE03", "intervalE04", "intervalE05" };
    
    [Tooltip("动态难度开关")]
    public int DynamicHardIsOpen = 1;
    [Tooltip("初始层关卡号")]
    public int StartLevel = 11;
    
    [Tooltip("关卡开启配置")]
    public Dictionary<int, CheckOpen>  CheckLevelOpen = new Dictionary<int, CheckOpen>();
    [Tooltip("每日大于该E值减少难度")]
    public float dayEValue = 0;
    [Tooltip("每日几关前")]
    public int dayDecrLevel = 0;
    
    [Header("能力变动参数")]
    
    [Tooltip("关卡区间边界")]
    public List<int> LevelBounds = new List<int>();

    [Tooltip("关卡区间的a值变化")]
    public List<float> aValues = new List<float>();
    [Tooltip("关卡区间的难度变化")]
    public List<int> difficultyModes = new List<int>();
    
    [Space(10)]
    [Header("不同关卡区间的E值变化参数配置")]
    [Tooltip("道具使用E值减少参数")]
    public List<float> PropsReduction = new List<float>();
    [Tooltip("初段时间阈值 前10关")]
    public List<float> TimeThresholds0 = new List<float>();
    public List<float> EChanges0 = new List<float>();
    [Tooltip("一阶段时间阈值 11-20")]
    public List<float> TimeThresholds1 = new List<float>();
    public List<float> EChanges1 = new List<float>();
    [Tooltip("二阶段时间阈值 21-60")]
    public List<float> TimeThresholds2 = new List<float>();
    public List<float> EChanges2 = new List<float>();
    [Tooltip("三阶段时间阈值 61-200")]
    public List<float> TimeThresholds3 = new List<float>();
    public List<float> EChanges3 = new List<float>();
    [Tooltip("四阶段时间阈值 201-500")]
    public List<float> TimeThresholds4 = new List<float>();
    public List<float> EChanges4 = new List<float>();
    [Tooltip("五阶段时间阈值 500以上")]
    public List<float> TimeThresholds5 = new List<float>();
    public List<float> EChanges5 = new List<float>();
    
    [Tooltip("关卡区间的操作配置")]
    public List<LevelDifficultyData> LevelDifficultyDatas = new List<LevelDifficultyData>();
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Start is called before the first frame update
    private void Start()
    {
        if (IsOpenDynamicHard())
        {
            LoadDynamicConfig();
        }    
    }

    private bool IsOpenDynamicHard()
    {
        return DynamicHardIsOpen == 1;
    }
    /// <summary>
    /// 加载动态配置表
    /// </summary>
    private void LoadDynamicConfig()
    {
        TextAsset csvFile = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "cypz_dynamicConfig");
        TextAsset levelCsvFile = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "cypz_levelDifficultyChange");
        
        if(csvFile != null)
            ConverCSVToJSON(csvFile.text);
        else
            Debug.LogError("Failed to load cypz_dynamicConfig csv data.");
        
        if (levelCsvFile != null)
            ConvertLevelCSVToJSON(levelCsvFile.text);
        else
            Debug.LogError("Failed to load cypz_levelDifficultyChange csv data.");
    }
    #region  解析能力参数
    /// <summary>
    /// 读取关卡难度配置
    /// </summary>
    /// <param name="data"></param>
    private void ConvertLevelCSVToJSON(string data)
    {
        LevelDifficultyDatas.Clear();
        
        string[] lines = data.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        for (int i = 2; i < lines.Length; i++)
        {
            string[]  fields = lines[i].Split(',', System.StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length >= 6)
            {
                if (int.TryParse(fields[1], out int word)
                    && float.TryParse(fields[2], out float e1)
                    && float.TryParse(fields[3], out float e2)
                    && float.TryParse(fields[4], out float e3)
                    && float.TryParse(fields[5], out float e4)
                    && float.TryParse(fields[6], out float e5))
                {
                    LevelDifficultyDatas.Add(new LevelDifficultyData(i - 2, word, e1, e2, e3, e4, e5));
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
        LevelDifficultyDatas.Sort((a, b) => a.intervalE01.CompareTo(b.intervalE01));

    }

    
    private void ConverCSVToJSON(string data)
    {
        string[] lines =  data.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        for (int i = 2; i < lines.Length; i++)
        {
            string[] fields = ToolUtil.ParseCsvLineKeepQuotes(lines[i]);
            if (fields.Length >= 3)
            {
                string paramId = fields[0].Trim();
                string description = fields[1].Trim();
                string valueStr = TrimOuterQuotes(fields[2].Trim());
                if (paramId == "cypz_dynamicLevel3")
                {
                    string[] values = valueStr.Split('_');
                    if (values.Length >= 2)
                    {
                        int.TryParse(values[0], out DynamicHardIsOpen);
                        string[] level2 = values[1].Split(',');
                        string[] level3 = values[2].Split(',');
                        CheckOpen checkOpen2 = new CheckOpen(2, int.Parse(level2[0].Trim()), float.Parse(level2[1].Trim()));
                        CheckOpen checkOpen5 = new CheckOpen(5, int.Parse(level3[0].Trim()), float.Parse(level3[1].Trim()));
                        CheckLevelOpen.Add(1, checkOpen2);
                        CheckLevelOpen.Add(4, checkOpen5);
                    }
                }else if (paramId == "cypz_dynamicLevel3E")
                {
                    // Debug.Log("处理能力变动： " + valueStr);
                    ParseDynamicLevelConfig(valueStr);
                }else if (paramId == "cypz_dynamicLevelDayword")
                {
                    string[] values = valueStr.Split('_');
                    float.TryParse(values[0], out dayEValue);
                    int.TryParse(values[1], out dayDecrLevel);
                    GameDataManager.Instance.ChessDynamicHardSave.ReduceWord = dayDecrLevel;
                    Debug.Log("是否进行了初始化？ " + GameDataManager.Instance.ChessDynamicHardSave.ReduceWord);
                }else if (paramId == "cypz_cv_a")
                {
                    string[] values = valueStr.Split('_');
                    values.ToList().ForEach(v =>
                    { 
                        if(float.TryParse(v, out float num))
                            aValues.Add(num);
                    });
                }else if (paramId == "cypz_difficultChange")
                {
                    string[] values = valueStr.Split('_');
                    values.ToList().ForEach(v =>
                    {
                        if(int.TryParse(v, out int num))
                            difficultyModes.Add(num);
                    });
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
    /// <param name="valueStr">JSON格式的配置字符串</param>
    private void ParseDynamicLevelConfig(string valueStr)
    {
        string cleanedJson = valueStr.Trim();
        if (cleanedJson.StartsWith("\"") && cleanedJson.EndsWith("\""))
        {
            cleanedJson = cleanedJson.Substring(1, cleanedJson.Length - 2);
        }
        // 替换全角字符为半角字符
        cleanedJson = cleanedJson.Replace("_", ",")
            .Replace("：", ":")
            .Replace("\"\"", "\"");
        
        // Debug.Log($"清理后的JSON: {cleanedJson}");
        JObject configObj = JObject.Parse(cleanedJson);
        
        // 解析关卡区间边界
        if (configObj["n"] != null)
        {
            string[] bounds = configObj["n"].ToString().Split(',');
            for (int i = 0; i < bounds.Length; i++)
            {
                if (int.TryParse(bounds[i], out var tmp))
                    LevelBounds.Add(tmp);
            }
        }
        // 即系道具使用E值减少参数
        if (configObj["c"] != null)
        {
            string[] reductions = configObj["c"].ToString().Split(',');
            for (int i = 0; i < reductions.Length; i++)
            {
                if(float.TryParse(reductions[i], out var tmp))
                    PropsReduction.Add(tmp);
            }
        }
        // 解析初始阶段参数
        ParseLevelConfig(configObj, "time0", "e0", TimeThresholds0, EChanges0);
        // 解析一阶段参数
        ParseLevelConfig(configObj, "time1", "e1", TimeThresholds1, EChanges1);
        // 解析二阶段参数
        ParseLevelConfig(configObj, "time2", "e2", TimeThresholds2, EChanges2);
        // 解析三阶段参数
        ParseLevelConfig(configObj, "time3", "e3", TimeThresholds3, EChanges3);
        // 解析四阶段参数
        ParseLevelConfig(configObj, "time4", "e4", TimeThresholds4, EChanges4);
        // 解析五阶段参数
        ParseLevelConfig(configObj, "time5", "e5", TimeThresholds5, EChanges5);
    }

    private void ParseLevelConfig(JObject configObj, string timeKey, string eKey, List<float> timeThresholds,
        List<float> eChanges)
    {
        if (configObj[timeKey] != null)
        {
            string[] times = configObj[timeKey].ToString().Split(',');
            for (int i = 0; i < times.Length; i++)
            {
                if(float.TryParse(times[i], out var tmp))
                    timeThresholds.Add(tmp);
            }
        }

        if (configObj[eKey] != null)
        {
            string[] changes = configObj[eKey].ToString().Split(',');
            for (int i = 0; i < changes.Length; i++)
            {
                if (float.TryParse(changes[i], out var tmp))
                    eChanges.Add(tmp);
            }
        }
    }
    
    /// <summary>
    /// 移除字符串首尾的双引号（如果存在）。
    /// 不存在或长度不足2时返回原字符串。
    /// </summary>
    /// <param name="input">原始字符串</param>
    /// <returns>去壳后的字符串</returns>
    private string TrimOuterQuotes(string input)
    {
        if (input.Length >= 2 && input[0] == '"' && input[^1] == '"')
            return input[1..^1];        // .NET 6/Unity 2021.2+ 可用
        // 旧版本兼容写法：
        // return input.Substring(1, input.Length - 2);
        return input;
    }
    #endregion
 
    /// <summary>
    /// 给定e值获取难度值
    /// </summary>
    /// <param name="currentEValue">当前E值</param>
    /// <param name="level">当前关卡</param>
    /// <returns>LevelDifficultyData</returns>
    private LevelDifficultyData GetLevelDifficultyDataByEValue(float currentEValue, int level)
    {
        if (LevelDifficultyDatas == null || LevelDifficultyDatas.Count == 0)
        {
            Debug.LogWarning("难度能力值数据未初始化");
            return null;
        }
        int idx = 0;
        // 找到 level 所在的区间序号
        for (; idx < LevelBounds.Count && level > LevelBounds[idx]; idx++) { }
        // 取对应字段值（反射只做一次，可缓存）
        var field = intervalFields[Mathf.Min(idx, intervalFields.Length - 1)];
        // 一次遍历即可
        foreach (var data in LevelDifficultyDatas)
        {
            var limit = (float)typeof(LevelDifficultyData).GetField(field).GetValue(data);
            if (currentEValue <= limit) return data;
        }
         // 兜底用最后一档
        return LevelDifficultyDatas[^1];
        
        // for (int i = 0; i < LevelDifficultyDatas.Count; i++)
        // {
        //     if (level <= LevelBounds[0])   // 0-10关
        //     {
        //         if (currentEValue <= LevelDifficultyDatas[i].intervalE01)
        //         {
        //             return LevelDifficultyDatas[i];
        //         }
        //     }else if (level <= LevelBounds[1])
        //     {
        //         if (currentEValue <= LevelDifficultyDatas[i].intervalE02)
        //         {
        //             return LevelDifficultyDatas[i];
        //         }
        //     }else if (level <= LevelBounds[2])
        //     {
        //         if (currentEValue <= LevelDifficultyDatas[i].intervalE03)
        //         {
        //             return LevelDifficultyDatas[i];
        //         }
        //     }else if(level <= LevelBounds[3])
        //     {
        //         if (currentEValue <= LevelDifficultyDatas[i].intervalE04)
        //         {
        //             return LevelDifficultyDatas[i];
        //         }
        //     }
        //     else
        //     {
        //         if (currentEValue <= LevelDifficultyDatas[i].intervalE05)
        //         {
        //             return LevelDifficultyDatas[i];
        //         }
        //     }
        // }
        //
        // return LevelDifficultyDatas[LevelDifficultyDatas.Count - 1];
    }
    
    /// <summary>
    /// 检查是否需要改变难度
    /// </summary>
    /// <param name="level">关卡</param>
    /// <returns></returns>
    public int CheckLevelHardChange(int level)
    {
        int change = 0;
        if (!IsOpenDynamicHard()) return change;

        if (!GameDataManager.Instance.ChessDynamicHardSave.IsEnergy) return change;
        
        Debug.Log($"在 {level} 关前的位置时E值： "+ GameDataManager.Instance.ChessDynamicHardSave.EnergyValue);
        float playerEValue = GameDataManager.Instance.ChessDynamicHardSave.EnergyValue;
     
        LevelDifficultyData levelDiff = GetLevelDifficultyDataByEValue(playerEValue, level);
        Debug.Log($" 在关卡 {level} 找到的匹配难度： " + JsonConvert.SerializeObject(levelDiff));
        if (levelDiff == null)
        {
            return change;
        }

        if (levelDiff.groupId != GameDataManager.Instance.ChessDynamicHardSave.MaxEnergyLevel)
        {
            GameDataManager.Instance.ChessDynamicHardSave.UpdateMaxEnergyLevel(levelDiff.groupId);
            change = levelDiff.wordChange;
        }
        else
        {
            bool canR = CanReduce(levelDiff.wordChange);
            Debug.Log($"在是否减少之前--- {change}   {levelDiff.wordChange}");
            Debug.Log($" 当前检查等级{level} 存储的等级" + GameDataManager.Instance.ChessDynamicHardSave.StageIndex + $" 是否可以减少 {canR}" );
            if (level != GameDataManager.Instance.ChessDynamicHardSave.StageIndex && canR)
            {
                change = levelDiff.wordChange + 1;
            }
            else
            {
                change = levelDiff.wordChange;
            }
        }
        if (GameDataManager.Instance.ChessDynamicHardSave.StageIndex != level)
        {
            GameDataManager.Instance.ChessDynamicHardSave.StageIndex = level;
        }
        Debug.Log($"拼字关卡动态难度 玩家E值 {playerEValue} 对应的目标词处理: {change}");
        return change;
    }

    /// <summary>
    /// 能否减少一个
    /// </summary>
    /// <returns></returns>
    private bool CanReduce(int count)
    {
        if (count > 0) return false;
        Debug.Log($"检查是否达到 {dayEValue} E值要求:  "+ GameDataManager.Instance.ChessDynamicHardSave.EnergyValue +" 还剩次数："+GameDataManager.Instance.ChessDynamicHardSave.ReduceWord);
        if (dayEValue <= GameDataManager.Instance.ChessDynamicHardSave.EnergyValue)
        {
            if (GameDataManager.Instance.ChessDynamicHardSave.ReduceWord > 0)
            {
                GameDataManager.Instance.ChessDynamicHardSave.ReduceWord--;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 获取变简单的模式
    /// </summary>
    /// <param name="level"></param>
    public int GetHardMode(int level)
    {
        for (int i = 0; i < LevelBounds.Count; i++)
        {
            if (level <= LevelBounds[i])
            {
                return difficultyModes[i];
            }
        }
        return 1;
    }
    /// <summary>
    /// 记录关卡通关时间、道具使用情况和道具提示词数
    /// </summary>
    /// <param name="level">关卡编号</param>
    /// <param name="clearTime">通关时长(秒)</param>
    /// <param name="usedProps">是否使用道具</param>
    /// <param name="propsWordCount">道具提示的词数</param>
    private void RecordLevelClearData(int level, float clearTime, bool usedProps, float propsWordCount = 0f)
    {
        if (!IsOpenDynamicHard()) return;
        
        GameDataManager.Instance.ChessDynamicHardSave.RecordLevelClearTime(level, clearTime);
        
        GameDataManager.Instance.ChessDynamicHardSave.UsedProps = usedProps;
        GameDataManager.Instance.ChessDynamicHardSave.PropsWordCount = propsWordCount;
        
        GameDataManager.Instance.ChessDynamicHardSave.SaveData();
    }

    /// <summary>
    /// 检查关卡通过条件，并执行难度介入
    /// </summary>
    /// <param name="level">当前关卡</param>
    /// <param name="errorCount">连错次数</param>
    /// <param name="clearTime">通关时长(秒)</param>
    /// <param name="usedProps">是否使用道具</param>
    /// <param name="propsWordCount">道具提示的词数</param>
    public void CheckLevelClearConditions(int level, int errorCount, float clearTime, bool usedProps, float propsWordCount = 0f)
    {
        if(!IsOpenDynamicHard()) return;

        if (level <= StartLevel)
        {
            RecordLevelClearData(level, clearTime, usedProps, propsWordCount);
        }

        bool handle = false;
        float eV = 0;
        if (level == 1)
        {
            // CheckLevelOpen.TryGetValue(1, out var checkLevelOpen1);
            if (CheckLevelOpen != null && CheckLevelOpen.TryGetValue(level, out var checkLevelOpen1))
            {
                if (errorCount == 0 && usedProps == false &&  clearTime <= checkLevelOpen1.Time)
                {
                    GameDataManager.Instance.ChessDynamicHardSave.IsEnergy = true;
                    GameDataManager.Instance.ChessDynamicHardSave.SetEnergy(checkLevelOpen1.Value);
                    GameDataManager.Instance.ChessDynamicHardSave.SaveData();
                    eV = checkLevelOpen1.Value;
                    handle = true;
                }
            } 
           
        }
        else if (level == 4 && GameDataManager.Instance.ChessDynamicHardSave.EnergyValue == 0)
        {
            if (CheckLevelOpen != null && CheckLevelOpen.TryGetValue(level, out var checkLevelOpen2))
            {
                if (errorCount == 0 && usedProps == false && GetCumulativeClearTime(level) <= checkLevelOpen2.Time)
                {
                    GameDataManager.Instance.ChessDynamicHardSave.IsEnergy = true;
                    GameDataManager.Instance.ChessDynamicHardSave.SetEnergy(checkLevelOpen2.Value);
                    GameDataManager.Instance.ChessDynamicHardSave.SaveData();
                    eV = checkLevelOpen2.Value;
                    handle = true;
                }
            }
        }
        else if (level >= 10 &&  GameDataManager.Instance.ChessDynamicHardSave.IsEnergy  == false)
        {
            GameDataManager.Instance.ChessDynamicHardSave.IsEnergy = true;
            GameDataManager.Instance.ChessDynamicHardSave.SetEnergy(0);
            GameDataManager.Instance.ChessDynamicHardSave.SaveData();
            handle = true;
        }
        // if(handle)
        //     MessageSystem.Instance.ShowTip($"关卡动态难度 第{level}关 触发E值介入，E值设置为 {eV}");
        // Debug.LogError($"是否进入修改： {handle} " + GameDataManager.Instance.ChessDynamicHardSave.IsEnergy);
        if (!handle && GameDataManager.Instance.ChessDynamicHardSave.IsEnergy)
        {
            AdjustEnergyValue(level, clearTime, usedProps, propsWordCount);
        }
 
    }
    private float GetCumulativeClearTime(int level)
    {
        float avgTime = GameDataManager.Instance.ChessDynamicHardSave.GetLevelTotalTime(level);
        return avgTime;
    }

    /// <summary>
    /// 根据通关时间和是否使用道具调整E值
    /// </summary>
    private void AdjustEnergyValue(int level, float clearTime, bool usedProps, float propsWordCount)
    {
        float energyChange = 0f;

        if (usedProps)
        {
            energyChange = CalculateEnergyDecrease(propsWordCount);
        }
        else
        {
            energyChange = CalculateEnergyIncrease(level, clearTime);
        }
        Debug.Log($"拼字是否使用道具 {usedProps} 值：{energyChange} 道具字数{propsWordCount}");
        float oldEnergy = GameDataManager.Instance.ChessDynamicHardSave.EnergyValue;
        GameDataManager.Instance.ChessDynamicHardSave.UpdateEnergy(energyChange, false);
        var ability = GetLevelDifficultyDataByEValue(GameDataManager.Instance.ChessDynamicHardSave.EnergyValue, level);
        Debug.Log($"在关卡 {level} 找到的难度： " +JsonConvert.SerializeObject(ability));
        float DescA = 0;
        if (usedProps && GameDataManager.Instance.ChessDynamicHardSave.IsDecrA && ability.groupId is >= 5 and <= 9) 
        {
            // 减去A值 默认给最后一档
            float aValue = aValues.Count > 0 ? aValues[^1] : 0f;

            for (int i = 0; i < LevelBounds.Count && i < aValues.Count; ++i)
            {
                if (level <= LevelBounds[i])
                {
                    aValue = aValues[i];
                    break;
                }
            }
    
            GameDataManager.Instance.ChessDynamicHardSave.UpdateEnergy(-aValue,true);
            DescA = aValue;
        }
        GameDataManager.Instance.ChessDynamicHardSave.SaveData();
        
        // string changeType = usedProps ? "减少" : "增加";
        // if(DescA != 0)
        //     MessageSystem.Instance.ShowTip($"在关卡{level} 结束时发生减A {DescA} 当前E值:" + GameDataManager.Instance.ChessDynamicHardSave.EnergyValue);
        // else
        // {
        //     MessageSystem.Instance.ShowTip($"第{level}关通关，时长{clearTime}秒，E值{changeType}: {oldEnergy} -> {GameDataManager.Instance.ChessDynamicHardSave.EnergyValue}");
        // }
    }
    
    /// <summary>
    /// 计算使用道具时的E值减少量
    /// </summary>
    private float CalculateEnergyDecrease(float propsWordCount)
    {
        float energyDecrease = 0f;

        if (propsWordCount <= 1f)
            energyDecrease = PropsReduction[0];
        else if (propsWordCount <= 2f)
            energyDecrease = PropsReduction[1];
        else
            energyDecrease = PropsReduction[2];
        
        return energyDecrease;
    }

    /// <summary>
    /// 计算未使用道具时的E值增加量
    /// </summary>
    private float CalculateEnergyIncrease(int level, float clearTime)
    {
        float energyChange = 0f;

        if (level <= 10)   // 0-10关
        {
            energyChange = CalculateIncreaseLevel(clearTime, TimeThresholds0, EChanges0);
        }else if (level <= LevelBounds[0])
        {
            energyChange = CalculateIncreaseLevel(clearTime, TimeThresholds1, EChanges1);
        }else if (level > LevelBounds[0] && level <= LevelBounds[1])
        {
            energyChange = CalculateIncreaseLevel(clearTime, TimeThresholds2, EChanges2);
        }else if (level > LevelBounds[1] && level <= LevelBounds[2])
        {
            energyChange = CalculateIncreaseLevel(clearTime, TimeThresholds3, EChanges3);
        }else if (level > LevelBounds[2] && level <= LevelBounds[3])
        {
            energyChange = CalculateIncreaseLevel(clearTime, TimeThresholds4, EChanges4);
        }
        else
        {
            energyChange = CalculateIncreaseLevel(clearTime, TimeThresholds5, EChanges5);
        }
        return energyChange;
    }

    /// <summary>
    /// 计算增加的阈值
    /// </summary>
    private float CalculateIncreaseLevel(float clearTime,  List<float> timeThresholds, List<float> eChanges)
    {
        // 1. 先默认给最后一档（就是原来的 else 分支）
       float energyChange = eChanges.Count > 0 ? eChanges[^1] : 0f;

        // 2. 只要没命中任何 threshold，就保持最后一档
        for (int i = 0; i < timeThresholds.Count && i < eChanges.Count - 1; ++i)
        {
            if (clearTime <= timeThresholds[i])
            {
                energyChange = eChanges[i];   // 命中就覆盖
                break;                        // 跳出循环
            }
        }
        // 如果循环走完都没 break，energyChange 仍是最后一档 → 等价于原 else
        return energyChange;
    }
    
    /// <summary>
    /// 清空存储的关卡通关时间
    /// </summary>
    public void ClearStoredLevelTimes()
    {
        // 清空关卡时间
        GameDataManager.Instance.ChessDynamicHardSave.LevelClearTimes.Clear();
        GameDataManager.Instance.ChessDynamicHardSave.UsedProps = false;
        GameDataManager.Instance.ChessDynamicHardSave.PropsWordCount = 0f;
        
        // 保存更新后的数据到JSON文件
        GameDataManager.Instance.ChessDynamicHardSave.SaveData();
    }
    
}
