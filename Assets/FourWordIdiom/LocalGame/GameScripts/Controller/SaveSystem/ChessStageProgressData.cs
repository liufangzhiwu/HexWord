using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// 关卡数据
/// </summary>
[System.Serializable]
public class ChessStageProgressData
{
    #region 核心字段
    public bool IsFirstEnter = true;    // 是否首次进入关卡
    public int StageId = 0;                                  // 关卡ID
    public HashSet<Bowl> Puzzles = new HashSet<Bowl>();  // 词堆
    public HashSet<Chesspiece> BoardSnapshot = new();        // 棋盘
    //public Dictionary<string, int> CharacterHints = new Dictionary<string, int>(); // 提示词，未用上
    public List<int> Cousor = new List<int>();
    public List<string> FoundTargetPuzzles = new List<string>(); // 词本
    [JsonIgnore]
    public Dictionary<(int row, int col), HashSet<PhraseGroup>> ChessGroup = new();   // 单词组
    public int MaxRow = -1;                                 // 最大行
    public int MaxCol = -1;
    public int MinRow = -1;
    public int MinCol = -1;

    [JsonIgnore]  // 尺寸
    public Vector2 ActiveSize = new Vector2(126,125);

    public Dictionary<string, HashSet<PhraseGroup>> tempgroup = new();
    #endregion

    public string SaveFileName;

    #region 初始化方法
    public void InitializeFromStageInfo(ChessStageInfo stageInfo)
    {
        StageId = stageInfo.StageNumber;
        Puzzles = stageInfo.Puzzles;
        BoardSnapshot = stageInfo.CurrBoardData;
        Cousor = stageInfo.Currsor;
        ChessGroup = stageInfo.ChessGroup;
        MaxRow = stageInfo.MaxRow;
        MaxCol = stageInfo.MaxCol;
        MinRow = stageInfo.MinRow;
        MinCol = stageInfo.MinCol;
        IsFirstEnter = true;
    }

    public void InitializeFromExisting(ChessStageProgressData sourceData)
    {
        this.StageId = sourceData.StageId;
        this.Puzzles = sourceData.Puzzles;

        //this.CharacterHints = sourceData.CharacterHints != null ? 
        //    new Dictionary<string, int>(sourceData.CharacterHints) : new Dictionary<string, int>();

        this.BoardSnapshot = sourceData.BoardSnapshot;
        this.Cousor = sourceData.Cousor;
        this.MaxRow = sourceData.MaxRow;
        this.MaxCol = sourceData.MaxCol;
        this.MinRow = sourceData.MinRow;
        this.MinCol = sourceData.MinCol;
        this.IsFirstEnter = sourceData.IsFirstEnter;

        ChessGroup.Clear();
        foreach (var item in sourceData.tempgroup)
        {
            var parts = item.Key.Split('_');
            int r = int.Parse(parts[0]);
            int c = int.Parse(parts[1]);
            ChessGroup[(r, c)] = item.Value;
        }
        
        this.FoundTargetPuzzles = sourceData.FoundTargetPuzzles != null ?
            new List<string>(sourceData.FoundTargetPuzzles) : new List<string>();
        
    }
    #endregion

    #region 文件操作
    public void LoadFromFile(ChessStageInfo stageInfo)
    {
        SaveFileName = CreateLevelIdentifier(stageInfo.StageNumber);

        string filePath = Path.Combine(Application.persistentDataPath, SaveFileName);

        if (!File.Exists(filePath))
        {
            Debug.LogWarning("未找到关卡进度文件，使用默认数据初始化： "+ filePath);
            InitializeFromStageInfo(stageInfo);
            return;
        }

        try
        {
            string encryptedJson = File.ReadAllText(filePath, Encoding.UTF8);

            string json = SecurityProvider.RestoreData(encryptedJson);

            if (!ValidateJson(json))
            {
                Debug.LogError("JSON数据格式无效");
                InitializeFromStageInfo(stageInfo);
                return;
            }

            var loadedData = JsonConvert.DeserializeObject<ChessStageProgressData>(json);

            if (loadedData.StageId <= 0) 
            {
                InitializeFromStageInfo(stageInfo);
            }
            else
            {
                InitializeFromExisting(loadedData);
            }
        }
        catch(System.Exception e)
        {
            Debug.LogError($"加载关卡数据失败: {e.Message}");
            InitializeFromStageInfo(stageInfo);
        }
    }
    public void SaveToFile()
    {
        SaveFileName = CreateLevelIdentifier(StageId);

        string filePath = Path.Combine(Application.persistentDataPath, SaveFileName);
        try
        {
            // 转换数据
            tempgroup = ChessGroup.ToDictionary(kv=> $"{kv.Key.row}_{kv.Key.col}", kv=>kv.Value);
            string json = JsonConvert.SerializeObject(this);
            string encryptedJson = SecurityProvider.ProtectData(json);
            File.WriteAllText(filePath, encryptedJson);

            //Debug.Log($"关卡进度已保存：{filePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"保存关卡数据失败：{e.Message}");
        }
    }

    /// <summary>
    /// 获取进度文件名称
    /// </summary>
    /// <param name="levelId"></param>
    /// <returns></returns>
    public static string CreateLevelIdentifier(int levelId)
    {
        return $"ChessStageProgress_{levelId}.json";
    }

    /// <summary>
    /// 验证JSON字符串是否有效
    /// </summary>
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

    #region 业务逻辑
    
    #endregion
}
