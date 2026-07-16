using System;
using System.Collections.Generic;
using System.Linq;
using Middleware;
using UnityEngine;
using UnityEngine.Rendering;

public class LoadTimeIndex
{
    //当前早晨(凌晨2点到中午10点)索引id;
    public int curmorningIndex;
    //当前白天(中午10点到晚上20点)索引id;
    public int curdaylightIndex;   
    //当前晚上(晚上20点到凌晨2点)索引id;
    public int curnightIndex; 
    
    public LoadTimeIndex Clone()
    {
        var clone = new LoadTimeIndex
        {
            curmorningIndex = this.curmorningIndex,
            curdaylightIndex = this.curdaylightIndex,
            curnightIndex = this.curnightIndex,
        };
        return clone;
    }
    
}

public class LoadTextConfigItem
{
    //对应多语言配置表中的key
    public string languagekey;
    //生命周期
    public int daylife;   
    //当前时间段缩写
    public string timecycle; 
    //时间段对应序号
    public int order; 
}


/// <summary>
/// 加载文案管理器 
/// 负责根据时间段、查询对应文案配置、管理文案显示逻辑
/// </summary>
public class LoadTextManager : Singleton<LoadTextManager>
{
    private LoadTimeIndex loadTimeIndexData;
    
    // 按时间段分组的配置列表（已按 Order 升序）
    private Dictionary<string, List<LoadTextConfigItem>> configGroups;

    public override void Init()
    {
        // 获取 loadTimeIndexData
        if (loadTimeIndexData == null)
        {
            if (GameDataManager.Instance != null)
                loadTimeIndexData = GameDataManager.Instance.UserData._loadTimeIndexData.Clone();
            else
                Debug.LogError("LoadTextManager: 未找到 loadTimeIndexData 实例");
        }

        // 加载并解析 CSV
        TextAsset data = AssetBundleLoader.SharedInstance.LoadTextFile("gameinfo", "WisdomRules");
        if (data != null)
        {
            ParseRewardConfig(data.text);
        }
        else
        {
            Debug.LogError("Failed to load WisdomRules CSV.");
        }

        // 若配置为空，创建空字典避免空引用
        if (configGroups == null)
            configGroups = new Dictionary<string, List<LoadTextConfigItem>>();
    }

    private void ParseRewardConfig(string csvData)
    {
        string[] lines = csvData.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) return;

        // 临时存储，按 TimeCycle 分组
        var tempGroups = new Dictionary<string, List<LoadTextConfigItem>>();

        // 从第1行开始（跳过标题行）
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] fields = line.Split(',');
            if (fields.Length < 4) continue; // key, DayLife, TimeCycle, Order

            // 解析字段
            string key = fields[0].Trim();
            if (!int.TryParse(fields[1].Trim(), out int daylife)) continue;
            string timeCycle = fields[2].Trim();
            if (!int.TryParse(fields[3].Trim(), out int order)) continue;

            var item = new LoadTextConfigItem
            {
                languagekey = key,
                daylife = daylife,
                timecycle = timeCycle,
                order = order
            };

            if (!tempGroups.ContainsKey(timeCycle))
                tempGroups[timeCycle] = new List<LoadTextConfigItem>();
            tempGroups[timeCycle].Add(item);
        }

        // 对每个分组按 Order 排序
        configGroups = new Dictionary<string, List<LoadTextConfigItem>>();
        foreach (var kv in tempGroups)
        {
            kv.Value.Sort((a, b) => a.order.CompareTo(b.order));
            configGroups[kv.Key] = kv.Value;
        }
    }

    /// <summary>
    /// 获取当前应显示的文案 key，并自动推进索引到下一个
    /// </summary>
    public string GetNextText()
    {
        // 1. 确定当前时间段
        string currentCycle = GetCurrentTimeCycle();

        // 2. 获取玩家当前生命周期（假设从 UserData 获取）
        int playerDayLife = GetPlayerDayLife();

        // 3. 获取该时间段的所有配置，筛选出 DayLife <= playerDayLife（0 视为无限制）
        List<LoadTextConfigItem> allItems = configGroups.ContainsKey(currentCycle) ? configGroups[currentCycle] : new List<LoadTextConfigItem>();
        var validItems = allItems.Where(item => item.daylife == 0 || item.daylife <= playerDayLife).ToList();

        if (validItems.Count == 0)
        {
            Debug.LogWarning($"LoadTextManager: 当前时间段 {currentCycle} 没有符合条件的文案（玩家生命周期 {playerDayLife}）");
            return string.Empty;
        }

        // 4. 获取当前索引（对应时间段）
        int currentIndex = GetCurrentIndex(currentCycle);
        // 确保索引在有效范围内
        if (currentIndex < 0 || currentIndex >= validItems.Count)
            currentIndex = 0;

        // 5. 获取当前文案 key
        string textKey = validItems[currentIndex].languagekey;

        // 6. 更新索引到下一个（回绕）
        currentIndex = (currentIndex + 1) % validItems.Count;
        SetCurrentIndex(currentCycle, currentIndex);

        // 7. 返回结果
        return textKey;
    }

    /// <summary>
    /// 根据系统时间判断当前时间段（M / D / N）
    /// </summary>
    private string GetCurrentTimeCycle()
    {
        int hour = DateTime.Now.Hour;
        // M: 2-10点，D: 10-20点，N: 20-2点（跨天）
        if (hour >= 2 && hour < 10)
            return "M";
        else if (hour >= 10 && hour < 20)
            return "D";
        else
            return "N"; // 20-23 和 0-1
    }

    /// <summary>
    /// 获取玩家当前生命周期（示例，请替换为实际获取方式）
    /// </summary>
    private int GetPlayerDayLife()
    {
        var today = DateTime.Today;
        // 计算生命周期天数
        int lifeDays = 0;
        if (!string.IsNullOrEmpty( GameDataManager.Instance.UserData.firstLoginTime) &&
            DateTime.TryParse( GameDataManager.Instance.UserData.firstLoginTime, out var firstLoginDate))
        {
            lifeDays = (today - firstLoginDate.Date).Days + 1; // +1 表示首日
            Debug.Log("当前时间：" + today.ToString("yyyy-MM-dd HH:mm:ss") + " 周期天数 life:" + lifeDays);
        }
        return lifeDays; // 默认 0
    }

    // ----- 索引读写辅助方法 -----
    private int GetCurrentIndex(string cycle)
    {
        if (loadTimeIndexData == null) return 0;
        switch (cycle)
        {
            case "M": return loadTimeIndexData.curmorningIndex;
            case "D": return loadTimeIndexData.curdaylightIndex;
            case "N": return loadTimeIndexData.curnightIndex;
            default: return 0;
        }
    }

    private void SetCurrentIndex(string cycle, int value)
    {
        if (loadTimeIndexData == null) return;
        switch (cycle)
        {
            case "M": GameDataManager.Instance.UserData._loadTimeIndexData.curmorningIndex = value; break;
            case "D":  GameDataManager.Instance.UserData._loadTimeIndexData.curdaylightIndex = value; break;
            case "N":  GameDataManager.Instance.UserData._loadTimeIndexData.curnightIndex = value; break;
        }
    }
}