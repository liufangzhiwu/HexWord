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
    int DynamicHardIsOpen = 0;
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
    public void Initialized()
    {
         StartCoroutine(LoadDynamicConfig());
    }

    public bool IsOpenDynamicHard()
    {
        return DynamicHardIsOpen == 1;
    }
    /// <summary>
    /// 加载动态配置表
    /// </summary>
    private IEnumerator LoadDynamicConfig()
    {
        string dynamicCsvData = null;
        string levelCsvData = null;
        // 🌟 1. 定义两把锁
        bool isDynamicDone = false;
        bool isLevelDone = false;
        // ==========================================
        // 1. 获取主配置 (dynamicConfig)
        // ==========================================
        StartCoroutine( APIGateway.Instance.GameConfigApi.GetGameConfig("cypz_dynamicConfig",
            onSuccess: (response) => { dynamicCsvData = response.CsvString; isDynamicDone = true;},
            onError:   (error) => {isDynamicDone = true; Debug.Log("服务器拉取 cypz_dynamicConfig 配置失败，准备兜底"); }
        ));
        // ==========================================
        // 2. 获取关卡难度配置 (cypz_levelDifficultyChange)
        // ==========================================
        StartCoroutine(  APIGateway.Instance.GameConfigApi.GetGameConfig("cypz_levelDifficultyChange",
            onSuccess: (response) => { levelCsvData = response.CsvString; isLevelDone = true;},
            onError:   (error) => {isLevelDone = true; Debug.Log("服务器拉取 cypz_levelDifficultyChange 难度配置失败，准备兜底"); }
        ));
        
        float timeout = 5f;
        while (!(isDynamicDone && isLevelDone) && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
        
        // ==========================================
        // 3. 执行断网兜底逻辑 (读取本地 Bundle)
        // ==========================================
        if (string.IsNullOrEmpty(dynamicCsvData))
        {
            TextAsset csvFile = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "cypz_dynamicConfig");
            dynamicCsvData = csvFile?.text;
        }
        if (string.IsNullOrEmpty(levelCsvData))
        {
            TextAsset levelCsvFile = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "cypz_levelDifficultyChange");
            levelCsvData = levelCsvFile?.text;
        }
        
        // ==========================================
        // 4. 解析生效
        // ==========================================
        if (!string.IsNullOrEmpty(dynamicCsvData))
            ConverCSVToJSON(dynamicCsvData);
        else
            Debug.LogError("Failed to load cypz_dynamicConfig csv data.");
        
        if (!string.IsNullOrEmpty(levelCsvData))
            ConvertLevelCSVToJSON(levelCsvData);
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
                    // Debug.Log("是否进行了初始化？ " + GameDataManager.Instance.ChessDynamicHardSave.ReduceWord);
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
        
        float playerEValue = GameDataManager.Instance.ChessDynamicHardSave.EnergyValue;
        Debug.Log($"<color=#00FFFF>[动态难度-开局判定]</color> 正在进入关卡 <b>{level}</b> | 玩家当前E值: <color=#FFD700>{playerEValue}</color>");
        LevelDifficultyData levelDiff = GetLevelDifficultyDataByEValue(playerEValue, level);
        if (levelDiff == null)
        {
            Debug.Log($"<color=#FF4500>[动态难度-异常]</color> 关卡 {level} 未能匹配到难度配置，E值: {playerEValue}");
            return change;
        }
        Debug.Log($"<color=#00FFFF>[动态难度-配置匹配]</color> 命中难度组 Group: <b>{JsonConvert.SerializeObject(levelDiff)}</b> | 预设目标增减字数: <color=#FFD700>{levelDiff.wordChange}</color>");
        // 更新最高/当前的E值分组（如果发生了变化）
        if (levelDiff.groupId != GameDataManager.Instance.ChessDynamicHardSave.MaxEnergyLevel)
        {
            GameDataManager.Instance.ChessDynamicHardSave.UpdateMaxEnergyLevel(levelDiff.groupId);
        }
        // 初始赋予正常的配置字数增减量
        change = levelDiff.wordChange;
        //  如果是新进入一个关卡（非重玩当前关卡），才进行每日减负特权判断
        if (level != GameDataManager.Instance.ChessDynamicHardSave.StageIndex)
        {
            if (CanReduce(change, playerEValue))
            {
                change += 1;
                Debug.Log($"<color=#00FF00>[动态难度-每日减负]</color> 判定生效！玩家E值(<color=#FFD700>{playerEValue}</color>) >= 门槛({dayEValue})。特权抵消1个难度字。最终实际变动字数: <color=#FFD700>{change}</color>");
            }
            else
            {
                Debug.Log($"<color=#00FFFF>[动态难度-正常执行]</color> 减负特权未生效或次数已尽，按配置正常执行。最终实际变动字数: <color=#FFD700>{change}</color>");
            }
            GameDataManager.Instance.ChessDynamicHardSave.StageIndex = level;
            GameDataManager.Instance.ChessDynamicHardSave.SaveData();
        }
        return change;
    }

    /// <summary>
    /// 判断是否满足每日降低一次难度的条件（恢复1个初始字）
    /// </summary>
    /// <param name="wordChange">当前原本应当改变的字数配置</param>
    /// <param name="playerEValue">玩家当前的E值</param>
    private bool CanReduce(int wordChange, float playerEValue)
    {
        if (wordChange > 0) return false;
        
        // Debug.Log($"检查是否达到 {dayEValue} E值要求: 当前E值 {playerEValue}"+" 前三关剩余减负特权次数："+GameDataManager.Instance.ChessDynamicHardSave.ReduceWord);
        // 如果每日的“前3关”特权次数还有剩余
        if (GameDataManager.Instance.ChessDynamicHardSave.ReduceWord > 0)
        {
            GameDataManager.Instance.ChessDynamicHardSave.ReduceWord--;
            // 核心需求判断：若当前 E值 >= 设定的门槛值(如32)，则特权生效，恢复1个字
            if (playerEValue >= dayEValue)
            {
                // 🌟 核心修改：只有特权真正生效时，才扣除特权次数！
                // GameDataManager.Instance.ChessDynamicHardSave.ReduceWord--;
                Debug.Log($"<color=#00FF00>[动态难度-特权触发]</color> E值{playerEValue} 达标 {dayEValue}，消耗1次特权。剩余特权次数：{GameDataManager.Instance.ChessDynamicHardSave.ReduceWord}");
                return true;
            }
            else
            {
                // 未达到门槛：不减字，也不扣除特权次数
                Debug.Log($"<color=#FFA500>[动态难度-门槛未达]</color> 玩家E值({playerEValue})不足({dayEValue})，特权保留，按正常难度正常扣字。");
                return false;
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
        if(handle)
            Debug.Log($"<color=#00FFFF>[动态难度-系统介入]</color> 第 <b>{level}</b> 关达到特定要求，强制激活并重置E值为: <color=#FFD700>{eV}</color>");
     
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
        float oldEnergy = GameDataManager.Instance.ChessDynamicHardSave.EnergyValue;
        GameDataManager.Instance.ChessDynamicHardSave.UpdateEnergy(energyChange, false);
        var ability = GetLevelDifficultyDataByEValue(GameDataManager.Instance.ChessDynamicHardSave.EnergyValue, level);
        
        // 结构化打印结算信息
        string propStatus = usedProps ? $"<color=#DDA0DD>已使用 (字数:{propsWordCount})</color>" : "未使用";
        string energyColor = energyChange >= 0 ? "#00FF00" : "#FF8C00";
        string sign = energyChange >= 0 ? "+" : "";
        Debug.Log($"<color=#00FFFF>[动态难度-关卡结算]</color> 关卡 <b>{level}</b> 结束。耗时: <b>{clearTime:F1}s</b> | 道具: {propStatus} | E值变动: <color={energyColor}>{sign}{energyChange}</color>");
        
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
        
        float finalEnergy = GameDataManager.Instance.ChessDynamicHardSave.EnergyValue;
        float netChange = finalEnergy - oldEnergy;
        string changeDirLog = netChange >= 0 ? $"<color=#00FF00>增加 (+{netChange})</color>" : $"<color=#FF8C00>减少 ({netChange})</color>";
        // 动态构建减A状态的彩色日志文本
        string descALog = DescA != 0 ? $"<color=#FF8C00>已触发 (-{DescA})</color>" : "<color=#A9A9A9>未触发</color>";
        // 将【变动方向】和【净差值】明确加入结账打印中
        Debug.Log($"<color=#00FFFF>[动态难度-数据结账]</color> E值轨迹: <color=#FFD700>{oldEnergy}</color> -> <color=#FFD700>{finalEnergy}</color> | 净结果: {changeDirLog} | 所属Group: <b>{ability?.groupId}</b> | 减A机制: {descALog}");
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
