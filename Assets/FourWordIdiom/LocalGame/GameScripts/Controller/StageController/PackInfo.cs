using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 关卡包配置数据（ScriptableObject）
/// 功能：
/// 1. 存储关卡文本资源引用
/// 2. 管理当前关卡状态
/// 3. 提供关卡数据访问接口
/// </summary>
[CreateAssetMenu(fileName = "PackInfo", menuName = "Stage Pack Info", order = 1)]
public class PackInfo : ScriptableObject
{
    [Header("关卡资源")]
    [Tooltip("所有关卡的文本资源文件（按顺序）")]
    [SerializeField] private List<TextAsset> _StageFiles = new List<TextAsset>();

    [Header("调试信息")]
    [Tooltip("当前选中的关卡信息")]
    private StageInfo _currentStageInfo;

    /// <summary>
    /// 所有关卡文件（只读）
    /// </summary>
    public IReadOnlyList<TextAsset> StageFiles => _StageFiles;

    /// <summary>
    /// 当前关卡信息
    /// </summary>
    public StageInfo CurrentStageInfo
    {
        get => _currentStageInfo;
        set
        {
            _currentStageInfo = value;
            Debug.Log($"当前关卡更新为：{value?.StageNumber ?? -1}");
        }
    }

    /// <summary>
    /// 获取指定关卡的文本资源
    /// </summary>
    /// <param name="StageIndex">关卡索引（从0开始）</param>
    /// <returns>文本资源，索引无效时返回null</returns>
    public TextAsset GetStageFile(int StageIndex)
    {
        if (StageIndex >= 0 && StageIndex < _StageFiles.Count)
        {
            return _StageFiles[StageIndex];
        }

        Debug.LogError($"无效的关卡索引：{StageIndex}（最大{_StageFiles.Count - 1}）");
        return null;
    }

    /// <summary>
    /// 添加新关卡资源（编辑器使用）
    /// </summary>
    public void AddStageFile(TextAsset file)
    {
        if (!_StageFiles.Contains(file))
        {
            _StageFiles.Add(file);
        }
    }

    /// <summary>
    /// 清除所有关卡引用（编辑器使用）
    /// </summary>
    public void ClearAllStages()
    {
        _StageFiles.Clear();
    }

    /// <summary>
    /// 验证关卡数据完整性
    /// </summary>
    public void ValidateData()
    {
        // 移除空引用
        _StageFiles.RemoveAll(file => file == null);

        // 检查重复引用
        var uniqueFiles = new HashSet<TextAsset>();
        var duplicates = new List<TextAsset>();

        foreach (var file in _StageFiles)
        {
            if (!uniqueFiles.Add(file))
            {
                duplicates.Add(file);
            }
        }

        if (duplicates.Count > 0)
        {
            Debug.LogWarning($"发现重复关卡文件：{string.Join(", ", duplicates)}");
        }
    }
}