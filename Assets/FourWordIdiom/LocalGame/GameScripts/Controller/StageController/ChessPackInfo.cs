using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


[Serializable]
public class ChessLevelConf
{
    public string levelDiff;
    public string pass;
    public string russ;
    public string elem;
    public string emoji;
    public string cursor;
}
[Serializable]
public class ChessDiffPack
{
    public List<ChessLevelConf> levels = new List<ChessLevelConf> ();
}
/// <summary>
/// 关卡包配置数据（ScriptableObject）
/// 功能：
/// 1. 存储关卡文本资源引用
/// 2. 管理当前关卡状态
/// 3. 提供关卡数据访问接口
/// </summary>
[CreateAssetMenu(fileName = "ChessPackInfo", menuName = "拼字关卡配置", order = 1)]
public class ChessPackInfo : ScriptableObject
{
    [Header("关卡资源")]
    //[Tooltip("所有关卡的文本资源文件(按顺序)")]  测试文件不一样
    //[SerializeField] private List<TextAsset> _StageFiles = new List<TextAsset>();
    [Tooltip("测试文件所有的数据")]
    [SerializeField] private List<ChessLevelConf> list = new();

    [Header("调试信息")]
    [Tooltip("当前选中的关卡信息")]
    private ChessStageInfo _currentStageInfo;

    /// <summary>
    /// 所有关卡文件（只读）
    /// </summary>
    //public IReadOnlyList<TextAsset> StageFiles => _StageFiles;

    /// <summary>
    /// 当前关卡信息
    /// </summary>
    public ChessStageInfo CurrentStageInfo
    {
        get => _currentStageInfo;
        set
        {
            _currentStageInfo = value;
            Debug.Log($"当前关卡更新为：{value?.StageNumber ?? -1}");
        }
    }
    public List<ChessLevelConf> PackInfos => list;

    /// <summary>
    /// 对外唯一接口,获取关卡配置
    /// </summary>
    /// <param name="level">关卡ID</param>
    /// <param name="difficulty"></param>
    public ChessLevelConf Get(int level, int difficulty = 1)
    {
        var key = $"{level}_{difficulty}";
        return list.Find(x => x.levelDiff == key);
    }

    // 仅在编辑器下烘培数据使用
    public void BuildFromJson(string jsonText, string csvText = "")
    {
        list.Clear();
        var root = JObject.Parse (jsonText);
        Dictionary<string, ChessLevelConf> temp = new();
        
        // 1. 原有 JSON 解析逻辑保持不变
        foreach (var kv in root)
        {
            string[] seg = kv.Key.Split('_');
            int lv = int.Parse(seg[0]);
            int dif = int.Parse(seg[1]);
            string field = seg[2];
            var key = $"{lv}_{dif}";

            if(!temp.TryGetValue(key, out var conf))
            {
                conf = new ChessLevelConf { levelDiff = key, };
                temp[key] = conf;
            }
            switch (field)
            {
                case "pass": conf.pass  = kv.Value.ToString(); break;
                case "russ": conf.russ  = kv.Value.ToString(); break;
                case "elem": conf.elem  = kv.Value.ToString(); break;
                case "emoji": conf.emoji  = kv.Value.ToString(); break;
                case "cursor": conf.cursor = kv.Value.ToString(); break;
            }
        }
        // 2. CSV 增量解析逻辑
        if (!string.IsNullOrEmpty(csvText))
        {
            // 临时字典，用于聚合同一个关卡的多个 Emoji 字符
            Dictionary<string, List<string>> levelEmojis = new();
        
            // 逐行读取 CSV（兼容 Windows \r\n 和 Mac \n）
            string[] lines = csvText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        
            // 假设第0行是表头，从第1行开始遍历
            for (int i = 1; i < lines.Length; i++)
            {
                // 假设 CSV 格式为： 关卡ID, 汉字, 图片名 (例: 1_1, 龙, img_dragon)
                string[] cols = lines[i].Split(',');
                if (cols.Length < 3) continue; // 至少得有 字、图标、和一个关卡ID
                string emojiChar = cols[0].Trim(); // 第 0 列：字
                // string iconName = cols[1].Trim(); // 第 1 列：图标名 (如果你后续需要图标名，可以在这里取)
                
                // 从第 2 列开始，向后遍历所有关卡 ID
                for (int j = 2; j < cols.Length; j++)
                {
                    string levelStr = cols[j].Trim();
                    if (string.IsNullOrEmpty(levelStr)) continue; // 忽略空白单元格

                    // 尝试解析纯数字的关卡ID
                    if (float.TryParse(levelStr, out float lvNum))
                    {
                        // 将关卡ID拼成与 JSON 一致的 key (默认拼接 _1 难度，如 "3" -> "3_1")
                        string levelKey = $"{(int)lvNum}_1"; 

                        if (!levelEmojis.ContainsKey(levelKey))
                        {
                            levelEmojis[levelKey] = new List<string>();
                        }
                    
                        levelEmojis[levelKey].Add(emojiChar);
                    }
                }
            }

            // 3. 将聚合后的列表转换为 "A#B#C" 格式，合入 temp 字典
            foreach (var kvp in levelEmojis)
            {
                string key = kvp.Key;
                string joinedEmojiStr = string.Join("#", kvp.Value);

                if (temp.TryGetValue(key, out var conf))
                {
                    // 如果 JSON 已经提供了 emoji 字段，可以选择覆盖或追加
                    // 此处选择直接覆盖
                    conf.emoji = joinedEmojiStr; 
                }
                else
                {
                    // 若 JSON 中完全没有该关卡，则创建新配置（容错）
                    temp[key] = new ChessLevelConf { levelDiff = key, emoji = joinedEmojiStr };
                }
            }
        }
        foreach(var kv in temp)
        {
            list.Add(kv.Value);
        }
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty (this);
#endif
    }
}
