using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Middleware
{
    /// <summary> 单条推送配置 </summary>
    public class PushData
    {
        public string Key;      // 文案Key，如 M1 / A3 / E17 / R2
        public string Title;    // 推送标题
        public string Text;     // 推送文案

        public PushData(string key, string title, string text)
        {
            Key   = key;
            Title = title;
            Text  = text;
        }
    }

    public class PushManager : Singleton<PushManager>
    {
        // ============================================================
        // 文案池前缀常量（对应配置表真实 Key 前缀）
        // ============================================================
        public const string POOL_MORNING = "M"; // 早晨：M1 ~ M20
        public const string POOL_NOON    = "A"; // 中午：A1 ~ A28
        public const string POOL_NIGHT   = "E"; // 夜晚：E1 ~ E28
        public const string POOL_RECALL  = "R"; // 召回：R1 ~ R5

        // Key → PushData
        private readonly Dictionary<string, PushData> pushTable = new Dictionary<string, PushData>();

        // 池前缀 → 有序列表（懒加载并缓存）
        private readonly Dictionary<string, List<PushData>> poolCache = new Dictionary<string, List<PushData>>();

        public override void Init()
        {
            LoadPushTable();
        }

        // ============================================================
        // 加载
        // ============================================================
        private void LoadPushTable()
        {
            TextAsset csvFile = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "pushConfig");
            if (csvFile == null)
            {
                Debug.LogError("[PushManaer] 加载推送配置表失败：gameinfo/pushConfig");
                return;
            }

            var lines = csvFile.text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2)
            {
                Debug.LogError("[PushManaer] 推送配置表内容为空");
                return;
            }

            pushTable.Clear();
            poolCache.Clear();

            int loaded = 0, skipped = 0;
            for (int i = 1; i < lines.Length; i++) // 跳过表头
            //for (int i = 1; i < 28; i++) // 跳过表头
            {
                var values = ParseCsvLine(lines[i]);
                if (values.Count < 3) { skipped++; continue; }

                string key   = values[0].Trim();
                string title = values[1].Trim();
                string text  = values[2].Trim();

                if (string.IsNullOrEmpty(key)) { skipped++; continue; }

                // 跳过样例行 "PushKey / PushTitleZH / PushTextZH"
                if (key.Equals("PushKey", StringComparison.OrdinalIgnoreCase)) { skipped++; continue; }

                pushTable[key] = new PushData(key, title, text);
                loaded++;
            }

            Debug.Log($"[PushManaer] 推送配置加载完成：{loaded} 条（跳过 {skipped} 行）");
        }

        /// <summary>
        /// CSV 单行解析，支持双引号包裹字段与转义引号 ""
        /// （当前配置里标题含逗号的情况不多，但为未来防御）
        /// </summary>
        private List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(sb.ToString());
                    sb.Length = 0;
                }
                else
                {
                    sb.Append(c);
                }
            }
            result.Add(sb.ToString());
            return result;
        }

        // ============================================================
        // 查询接口
        // ============================================================

        /// <summary> 取整条配置（可能为 null） </summary>
        public PushData GetPushData(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            return pushTable.TryGetValue(key, out var data) ? data : null;
        }

        /// <summary> 取标题，找不到返回 key 本身（便于定位缺失配置） </summary>
        public string GetPushTitle(string key)
        {
            var d = GetPushData(key);
            return d != null ? d.Title : key;
        }

        /// <summary> 取文案，找不到返回 key 本身 </summary>
        public string GetPushText(string key)
        {
            var d = GetPushData(key);
            return d != null ? d.Text : key;
        }

        /// <summary> 兼容旧接口：按 key 返回文案 </summary>
        public string GetPushString(string key)
        {
            return GetPushText(key);
        }

        /// <summary>
        /// 取整组文案（按池前缀），返回按数字升序排好的列表。
        /// 例：GetPool(POOL_MORNING) → M1..M20
        /// </summary>
        public List<PushData> GetPool(string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
                return new List<PushData>();

            if (poolCache.TryGetValue(prefix, out var cached))
                return cached;

            var list = new List<PushData>();
            foreach (var kv in pushTable)
            {
                if (kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    list.Add(kv.Value);
            }
            list.Sort((a, b) => CompareKeyNatural(a.Key, b.Key));

            poolCache[prefix] = list;
            return list;
        }

        /// <summary> 取某池第 index 条（自动 mod 长度，安全） </summary>
        public PushData GetPoolItem(string prefix, int index)
        {
            var pool = GetPool(prefix);
            if (pool.Count == 0) return null;
            int safe = ((index % pool.Count) + pool.Count) % pool.Count;
            return pool[safe];
        }

        /// <summary> 自然排序：M2 应排在 M10 之前 </summary>
        private static int CompareKeyNatural(string a, string b)
        {
            string prefixA = Regex.Replace(a, @"\d", "");
            string prefixB = Regex.Replace(b, @"\d", "");

            int cmp = string.Compare(prefixA, prefixB, StringComparison.OrdinalIgnoreCase);
            if (cmp != 0) return cmp;

            return ExtractNumber(a).CompareTo(ExtractNumber(b));
        }

        private static int ExtractNumber(string s)
        {
            var m = Regex.Match(s, @"\d+");
            return m.Success && int.TryParse(m.Value, out var n) ? n : 0;
        }

        // ============================================================
        // 调试辅助
        // ============================================================
        public void LogPoolSummary()
        {
            var morning = GetPool(POOL_MORNING);
            var noon    = GetPool(POOL_NOON);
            var night   = GetPool(POOL_NIGHT);
            var recall  = GetPool(POOL_RECALL);
            Debug.Log($"[PushManaer] 池子统计：M={morning.Count}, A={noon.Count}, E={night.Count}, R={recall.Count}");
        }
    }
}