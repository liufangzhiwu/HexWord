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
    public void BuildFromJson(string jsonText)
    {
        list.Clear();
        var root = JObject.Parse (jsonText);
        Dictionary<string, ChessLevelConf> temp = new();
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
                case "cursor": conf.cursor = kv.Value.ToString(); break;
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
