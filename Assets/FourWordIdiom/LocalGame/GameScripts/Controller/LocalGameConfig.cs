using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 本地配置表（gameinfo/LoclalGameConfig.csv）。
///
/// 表结构（前 4 列固定，其后每个玩法占一列）：
///   功能 | 参数说明 | 格式 | 参数名称 | 禅意之境 | 成语消消闯关 | …
/// 例如：
///   &lt;填字模式&gt;用户每次发生付费时… | "X：开关…D：保护天数" | X_D | cypz_payProtect | 1_3 | …
///
/// 解析口径：
///   1. 第 4 列（index 3）是参数名称（如 cypz_payProtect），用于代码里取值；
///   2. 参数名称之后**第一个非空单元格**就是本玩法（拼字模式）的取值，
///      这样表里增删玩法列时不用改代码；
///   3. 说明列里带逗号、换行、双引号（CSV 标准转义），因此不用按行 split，
///      走一个带引号状态机的解析器。
///
/// 值缺失（断网 / 表没打进包 / 参数没配）时全部走调用方给的兜底默认值，
/// 不会抛异常，也不会把空值当成 0。
/// </summary>
public static class LocalGameConfig
{
    public const string BundleName = "gameinfo";
    public const string AssetName = "LoclalGameConfig";   // 注意：资源名本身拼写就是 Loclal（少一个 a）

    /// <summary>付费保护参数名（X_D：X 开关 0/1，D 保护天数）</summary>
    public const string KeyPayProtect = "cypz_payProtect";

    // 参数名 -> 本玩法取值
    private static readonly Dictionary<string, string> Values =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static bool _loaded;

    /// <summary>已解析到的参数个数（排查用）</summary>
    public static int Count => Values.Count;

    /// <summary>重新读取（表更新 / 热重载后调用）</summary>
    public static void Reload()
    {
        _loaded = false;
        Values.Clear();
        EnsureLoaded();
    }

    /// <summary>取原始字符串，缺失返回 fallback</summary>
    public static string GetString(string paramName, string fallback = "")
    {
        if (string.IsNullOrEmpty(paramName)) return fallback;
        EnsureLoaded();
        if (Values.TryGetValue(paramName.Trim(), out string value) && !string.IsNullOrEmpty(value))
            return value;
        return fallback;
    }

    /// <summary>
    /// 按 "_" 拆分取值（如 "1_3" → ["1","3"]），缺失返回空数组。
    /// </summary>
    public static string[] GetParts(string paramName)
    {
        string raw = GetString(paramName);
        if (string.IsNullOrEmpty(raw)) return Array.Empty<string>();
        return raw.Split('_');
    }

    /// <summary>取 "_" 分隔的第 index 段并转 int，缺失 / 解析失败返回 fallback</summary>
    public static int GetIntPart(string paramName, int index, int fallback)
    {
        string[] parts = GetParts(paramName);
        if (index >= 0 && index < parts.Length && int.TryParse(parts[index].Trim(), out int value))
            return value;
        return fallback;
    }

    /// <summary>取 "_" 分隔的第 index 段并转 float，缺失 / 解析失败返回 fallback</summary>
    public static float GetFloatPart(string paramName, int index, float fallback)
    {
        string[] parts = GetParts(paramName);
        if (index >= 0 && index < parts.Length &&
            float.TryParse(parts[index].Trim(), out float value))
            return value;
        return fallback;
    }

    // ======================= 具体参数（用到哪个加哪个） =======================

    /// <summary>
    /// cypz_payProtect 的 X：付费保护开关（0 关 / 1 开），默认 1。
    /// </summary>
    public static bool PayProtectSwitch => GetIntPart(KeyPayProtect, 0, 1) == 1;

    /// <summary>
    /// cypz_payProtect 的 D：付费保护天数，默认 3。
    /// </summary>
    public static int PayProtectDays => GetIntPart(KeyPayProtect, 1, 3);

    // ======================= 解析 =======================

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        TextAsset asset = null;
        try
        {
            asset = AdvancedBundleLoader.SharedInstance.LoadTextFile(BundleName, AssetName);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LocalGameConfig] 读取 {AssetName} 异常，全部使用默认值：{e.Message}");
        }

        if (asset == null || string.IsNullOrEmpty(asset.text))
        {
            Debug.LogWarning($"[LocalGameConfig] 未加载到 {BundleName}/{AssetName}，全部使用默认值");
            return;
        }

        List<List<string>> rows = ParseCsv(asset.text);
        int parsed = 0;
        foreach (List<string> row in rows)
        {
            if (row == null || row.Count < 5) continue;

            string paramName = row[3]?.Trim();
            // 表头两行（参数名称 / name）不是配置项
            if (string.IsNullOrEmpty(paramName) || paramName == "name" || paramName == "参数名称")
                continue;

            // 参数名之后第一个非空单元格 = 本玩法的取值
            string value = null;
            for (int i = 4; i < row.Count; i++)
            {
                string cell = row[i]?.Trim();
                if (!string.IsNullOrEmpty(cell))
                {
                    value = cell;
                    break;
                }
            }
            if (string.IsNullOrEmpty(value)) continue;

            Values[paramName] = value;
            parsed++;
        }

        Debug.Log($"[LocalGameConfig] 解析完成，共 {parsed} 个参数" +
                  $"（{KeyPayProtect}={GetString(KeyPayProtect, "<缺失，用默认 1_3>")}）");
    }

    /// <summary>
    /// 标准 CSV 解析：支持双引号包裹、引号内逗号/换行、"" 转义、CRLF。
    /// </summary>
    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')   // "" 转义
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    cell.Append(c);
                }
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    row.Add(cell.ToString());
                    cell.Clear();
                    break;
                case '\r':
                    break;                       // CRLF：吃掉 \r，等 \n 收行
                case '\n':
                    row.Add(cell.ToString());
                    cell.Clear();
                    rows.Add(row);
                    row = new List<string>();
                    break;
                default:
                    cell.Append(c);
                    break;
            }
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }
        return rows;
    }
}
