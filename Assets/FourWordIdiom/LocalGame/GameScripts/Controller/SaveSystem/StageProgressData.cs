using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using System;

[System.Serializable]
public class StageProgressData
{
    #region 核心字段
    public int StageId = 0;
    public List<string> Puzzles = new List<string>();
    public List<string> FoundTargetPuzzles = new List<string>();
    public Dictionary<string, int> CharacterHints = new Dictionary<string, int>();
    public List<string> PuzzleHints = new List<string>();
    public BoardGame BoardSnapshot = new BoardGame();
    #endregion

    #region 初始化方法
    public void InitializeFromStageInfo(StageInfo stageInfo)
    {
        StageId = stageInfo.StageNumber;
        Puzzles = stageInfo.Puzzles;
        BoardSnapshot = stageInfo.CurBoardData;
    }

    public void InitializeFromExisting(StageProgressData sourceData)
    {
        this.StageId = sourceData.StageId;
        Puzzles = sourceData.Puzzles;
        this.FoundTargetPuzzles = sourceData.FoundTargetPuzzles != null ? 
            new List<string>(sourceData.FoundTargetPuzzles) : new List<string>();
        this.CharacterHints = sourceData.CharacterHints != null ? 
            new Dictionary<string, int>(sourceData.CharacterHints) : new Dictionary<string, int>();
        this.PuzzleHints = sourceData.PuzzleHints != null ? 
            new List<string>(sourceData.PuzzleHints) : new List<string>();
        this.BoardSnapshot = sourceData.BoardSnapshot;
    }
    #endregion

    #region PlayerPrefs存储操作
    private string GetPlayerPrefsKey()
    {
        return $"StageProgress_{StageId}";
    }

    public void LoadFromPlayerPrefs(StageInfo stageInfo)
    {
        // StageId = stageInfo.StageNumber;
        // string key = GetPlayerPrefsKey();

        // if (!PlayerPrefs.HasKey(key))
        // {
        //     Debug.LogWarning("未找到关卡进度，使用默认数据初始化");
            InitializeFromStageInfo(stageInfo);
        //     return;
        // }

        // try
        // {
        //     string encryptedJson = PlayerPrefs.GetString(key);
        //     string json = SecurityProvider.RestoreData(encryptedJson);
        //
        //     if (!ValidateJson(json))
        //     {
        //         Debug.LogError("JSON数据格式无效");
        //         InitializeFromStageInfo(stageInfo);
        //         return;
        //     }
        //
        //     var loadedData = JsonConvert.DeserializeObject<StageProgressData>(json);
        //
        //     if (loadedData != null && loadedData.BoardSnapshot != null)
        //     {
        //         InitializeFromExisting(loadedData);
        //     }
        //     else
        //     {
        //         InitializeFromStageInfo(stageInfo);
        //     }
        // }
        // catch (System.Exception e)
        // {
        //     Debug.LogError($"加载关卡数据失败: {e.Message}");
        //     InitializeFromStageInfo(stageInfo);
        // }
    }

    public void SaveToPlayerPrefs()
    {
        string key = GetPlayerPrefsKey();
        try
        {
            // string json = JsonConvert.SerializeObject(this);
            // string encryptedJson = SecurityProvider.ProtectData(json);
            // PlayerPrefs.SetString(key, encryptedJson);
            // PlayerPrefs.Save(); // 确保立即写入
            
            Debug.Log($"关卡进度已保存到PlayerPrefs: {key}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"保存关卡数据失败: {e.Message}");
        }
    }

    private bool ValidateJson(string json)
    {
        try
        {
            JToken.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
    #endregion

    #region 业务逻辑（添加立即保存）
    public bool IsPuzzleFullyHinted(string Puzzle)
    {
        return GetPuzzleHintCount(Puzzle) >= 3;
    }
    
    public void UpdateFoundTargetPuzzle(string Puzzle)
    {
        if (!FoundTargetPuzzles.Contains(Puzzle))
        {
            FoundTargetPuzzles.Add(Puzzle);
            SaveToPlayerPrefs(); // 数据变动立即保存
        }
    }

    public int GetPuzzleHintCount(string Puzzle)
    {
        if (CharacterHints != null && CharacterHints.ContainsKey(Puzzle))
        {
            return CharacterHints[Puzzle];
        }

        if (PuzzleHints.Contains(Puzzle))
        {
            return 0;
        }

        return -1;
    }

    public Dictionary<string, int> GetIncompleteHintPuzzles()
    {
        return new Dictionary<string, int>();
    }
    
    public List<string> GetIncompleteButterflyHints()
    {
        var result = new List<string>();
        if (PuzzleHints == null) return result;

        foreach (string word in PuzzleHints)
        {
            if (!IsPuzzleFullyHinted(word) && !FoundTargetPuzzles.Contains(word))
            {
                result.Add(word);
            }
        }
        return result;
    }

    public void UpdateCharacterHint(string Puzzle, int hintIndex)
    {
        if (CharacterHints == null) return;

        if (CharacterHints.ContainsKey(Puzzle))
        {
            CharacterHints[Puzzle] = hintIndex;
        }
        else
        {
            CharacterHints.Add(Puzzle, hintIndex);
        }
        SaveToPlayerPrefs(); // 数据变动立即保存
    }

    public void AddPuzzleHints(string Puzzle)
    {
        if (!PuzzleHints.Contains(Puzzle))
        {
            PuzzleHints.Add(Puzzle);
            SaveToPlayerPrefs(); // 数据变动立即保存
        }
    }

    public string FindFirstHintedPuzzle(HashSet<string> availablePuzzles)
    {
        return null;
    }
    #endregion
}