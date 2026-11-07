using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 商店管理器 - 特殊节日&周末配置部分类。
/// 职责：加载"HolidayWeekend特殊节日&周末匹配表"，判断当天是否为节日/周末，
///      计算活动礼包限时时间（连续节日/周末日期取截至时间）。
/// 注意：预设时间表最晚日期距打包不足2个月时，需提醒更新周末/节日表。
/// </summary>
public partial class ShopManager : MonoBehaviour
{
    /// <summary>节日/周末表：日期(yyyy-MM-dd) → 节日名/周末</summary>
    private Dictionary<string, string> _holidayWeekendTable = new Dictionary<string, string>();

    /// <summary>
    /// 加载节日/周末表（HolidayWeekend特殊节日&周末匹配表 202×20）。
    /// 列：日期 | 节日/周末。配置放本地（Bundle），表内最晚日期距当前不足 2 个月时告警提醒更新。
    /// </summary>
    private void LoadHolidayWeekendTable()
    {
        TextAsset hw = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "holiday_weekend");
        if (hw != null)
            ParseHolidayWeekendTable(hw.text);

        // 需求：预设时间表最晚日期距打包不足2个月要代码备注提醒更新周末/节日表
        DateTime latest = DateTime.MinValue;
        foreach (var kv in _holidayWeekendTable)
        {
            DateTime d;
            if (DateTime.TryParse(kv.Key, out d) && d > latest)
                latest = d;
        }
        if (latest != DateTime.MinValue && latest.Subtract(DateTime.Now).TotalDays < 60)
        {
            Debug.LogWarning($"[ShopManager] 节日/周末预设表最晚日期 {latest:yyyy-MM-dd} 距当前不足2个月，请更新HolidayWeekend特殊节日&周末匹配表（当前 {DateTime.Now:yyyy-MM-dd}）");
        }
    }

    private void ParseHolidayWeekendTable(string data)
    {
        _holidayWeekendTable.Clear();
        var records = SplitCsvLines(data);
        for (int i = 0; i < records.Count; i++)
        {
            var f = records[i];
            if (f.Count < 2) continue;
            string dateStr = GetField(f, 0);
            string label = GetField(f, 1);
            if (dateStr == "日期" || string.IsNullOrEmpty(dateStr)) continue;
            DateTime date;
            if (DateTime.TryParse(dateStr, out date) && !string.IsNullOrEmpty(label))
            {
                _holidayWeekendTable[date.ToString("yyyy-MM-dd")] = label;
            }
        }
    }

    /// <summary>
    /// 今天是否为节日或周末（需求：节日/周末权重、商店入口角标、破冰/复购判断均命中）
    /// 优先级：预设表（节日）＞自然周末。
    /// </summary>
    private bool IsHolidayOrWeekend()
    {
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        if (_holidayWeekendTable.ContainsKey(today))
            return true;
        // 自然周末（周六/周日）
        return DateTime.Now.DayOfWeek == DayOfWeek.Saturday || DateTime.Now.DayOfWeek == DayOfWeek.Sunday;
    }

    /// <summary>
    /// 二十四节气名。表里会把节气也写进「活动标签」（如 2026-11-07 立冬），
    /// 但节气不是购物节日，落在周末时角标应显示「周末 X 折」而不是「节日 X 折」（bug45）。
    /// </summary>
    private static readonly HashSet<string> SolarTerms = new HashSet<string>
    {
        "立春", "雨水", "惊蛰", "春分", "清明", "谷雨",
        "立夏", "小满", "芒种", "夏至", "小暑", "大暑",
        "立秋", "处暑", "白露", "秋分", "寒露", "霜降",
        "立冬", "小雪", "大雪", "冬至", "小寒", "大寒",
    };

    private static bool IsSolarTerm(string label)
    {
        return !string.IsNullOrEmpty(label) && SolarTerms.Contains(label);
    }

    /// <summary>
    /// 获取今天的节日/周末标签："周末"、"中秋节"、"国庆节"等；非节日周末返回空串。
    /// 优先级：法定/传统节日 ＞ 自然周末 ＞ 节气（节气落在周末时按周末算）。
    /// </summary>
    private string GetTodayHolidayLabel()
    {
        DateTime now = DateTime.Now;
        string today = now.ToString("yyyy-MM-dd");
        bool isWeekend = now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday;

        string label;
        if (_holidayWeekendTable.TryGetValue(today, out label))
        {
            // ★ bug45：2026-11-07 是周六，表里写着「立冬」→ 角标原本显示「节日 5 折」。
            //   节气不是节日，周末当天应按自然周末走标签。
            if (!(isWeekend && IsSolarTerm(label)))
                return label;
        }

        if (isWeekend)
            return "周末";

        return "";
    }

    /// <summary>
    /// 计算当前节日/周末的截至时间（活动礼包限时 = 本次节日/周末结束时间 - 当前时间）。
    /// 基于预设表连续日期段计算：从今天起连续命中节日/周末的最后一天 23:59:59。
    /// </summary>
    private DateTime GetCurrentHolidayEndTime()
    {
        DateTime now = DateTime.Now;
        DateTime end = now.Date.AddDays(1).AddSeconds(-1); // 兜底：今天结束

        // 从明天起连续检查（今天已命中）
        DateTime cursor = now.Date.AddDays(1);
        int guard = 0;
        while (guard++ < 30)
        {
            string key = cursor.ToString("yyyy-MM-dd");
            bool inTable = _holidayWeekendTable.ContainsKey(key);
            bool isWeekend = cursor.DayOfWeek == DayOfWeek.Saturday || cursor.DayOfWeek == DayOfWeek.Sunday;
            if (!inTable && !isWeekend) break;
            end = cursor.AddDays(1).AddSeconds(-1);
            cursor = cursor.AddDays(1);
        }
        return end;
    }
}
//（注：内容由AI生成）
