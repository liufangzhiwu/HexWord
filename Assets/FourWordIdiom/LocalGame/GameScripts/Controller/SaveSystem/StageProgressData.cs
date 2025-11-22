using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using System;
using System.IO;

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
    
    public string SaveFileName;
    
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

    public void LoadFromFile(StageInfo stageInfo)
    {

        SaveFileName = $"StageProgress_{stageInfo.StageNumber}.json";

        string filePath = Path.Combine(Application.persistentDataPath, SaveFileName);

        if (!File.Exists(filePath))
        {
            Debug.LogWarning("未找到关卡进度文件，使用默认数据初始化");
            InitializeFromStageInfo(stageInfo);           
            return;
        }

        try
        {
            string encryptedJson = File.ReadAllText(filePath, Encoding.UTF8);
            //解密
            string json = SecurityProvider.RestoreData(encryptedJson);

            if (!ValidateJson(json))
            {
                Debug.LogError("JSON数据格式无效");
                InitializeFromStageInfo(stageInfo);
                return;
            }

            var loadedData = JsonConvert.DeserializeObject<StageProgressData>(json);
            
            bool foundword=true;
            for (int i = 0; i < loadedData.Puzzles.Count; i++)
            {
                string targetWord = loadedData.Puzzles[i];
                if (!stageInfo.Puzzles.Contains(targetWord))
                {
                    foundword = false;
                    break;
                }
            }
        
            if (loadedData.StageId <= 0||!foundword)
            {
                InitializeFromStageInfo(stageInfo);
            }
            else
            {
                Debug.Log("关卡数据数据已加载: " + json+" 关卡数据 "+loadedData.StageId);
                InitializeFromExisting(loadedData);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"加载关卡数据失败: {e.Message}");
            InitializeFromStageInfo(stageInfo);
        }
    }

    public void SaveToPlayerPrefs()
    {
        SaveFileName = $"StageProgress_{StageId}.json";
        string filePath = Path.Combine(Application.persistentDataPath, SaveFileName);

        try
        {
            // 虚拟加密前处理
            string json = JsonConvert.SerializeObject(this);
            //加密
            string encryptedJson = SecurityProvider.ProtectData(json);
            File.WriteAllText(filePath, encryptedJson);
          
            Debug.Log($"关卡进度已保存: {filePath}");
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
    }

    public void AddPuzzleHints(string Puzzle)
    {
        if (!PuzzleHints.Contains(Puzzle))
        {
            PuzzleHints.Add(Puzzle);
        }
    }

    public string FindFirstHintedPuzzle(HashSet<string> availablePuzzles)
    {
        return null;
    }
    #endregion
}