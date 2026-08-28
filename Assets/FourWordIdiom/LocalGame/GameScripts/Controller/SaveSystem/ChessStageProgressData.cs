using System;
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
    
    public Chesspiece PupaDatas = null;
    
    public int CurrentTotalScore { get; set; }       // 当前获得的总分数
    public float RemainingTime = 300f;
    public float TotalActiveSeconds = 0f;
    public int CurrentCombo = 0;       // 🌟 新增：当前连击
    public int TotalCumulativeCombos = 0; // 累计有效连击总次数
    public int CurBreakIceCount = 0;       // 🌟 新增：当前破冰数量
    public int CurPickFlowerLeavesCount = 0;      // 新增：当前收集花朵数量
    public bool CurrPerfectCount;       // 🌟 新增：是否完美通关
    public int MaxCombo = 0;           // 🌟 新增：最高连击
    public int EarnedPupaCount = 0;    // 🌟 新增：已得蝶蛹数

    public bool IsPausedOrFailed = false;
    
    public int FlowerActionCount = 0; // 记录花朵被消除机制触发的次数
    public int CollectedLeaves = 0;   // 记录本局收集的树叶数量
    
    public int GoldLeafCount { get; set; } // 🌟 新增：已得金叶数
    
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
        PupaDatas = stageInfo._pupaData;
        IsFirstEnter = true;
        CurrentTotalScore = 0;
        RemainingTime = ButterfliesManager.Instance.GetStageLimitTimer(); // 🌟 首次进入关卡，重置为 300 秒
        TotalActiveSeconds = 0f;
        //stageInfo.CreatePupaData();
        IsPausedOrFailed = false; // 🌟 新关卡默认安全
        this.CollectedLeaves = 0;
        this.FlowerActionCount = 0;
        this.GoldLeafCount = 0;
        this.CurBreakIceCount = 0;
        this.CurPickFlowerLeavesCount = 0;
        this.CurrPerfectCount = false;
        TotalCumulativeCombos = 0;
    }

    public void InitializeFromExisting(ChessStageProgressData sourceData)
    {
        this.StageId = sourceData.StageId;
        Dictionary<string, int> totalNeededLetters = new Dictionary<string, int>(); // 总需求 (所有未通关坑位的答案)
        Dictionary<string, int> consumedLetters = new Dictionary<string, int>();    // 已消耗 (玩家实际填入坑位里的字)  foreach (var piece in sourceData.BoardSnapshot)
        foreach (var piece in sourceData.BoardSnapshot)
        {
            // 只要没通关 (None, Check, Fill, Error)，这个坑位的【正确答案】就应该存在于总字库中
            if (piece.state != TileState.Success && piece.state != TileState.Default)
            {
                if (!totalNeededLetters.ContainsKey(piece.letter)) totalNeededLetters[piece.letter] = 0;
                totalNeededLetters[piece.letter]++;
            }

            // 统计玩家【实际填进去】的字是什么
            if (piece.state == TileState.Fill || piece.state == TileState.Error)
            {
                // 🌟 核心修复：必须取 piece.bowl.letter (实际填的)，而不是 piece.letter (坑位答案)！
                string actualLetter = (piece.bowl != null && !string.IsNullOrEmpty(piece.bowl.letter)) 
                    ? piece.bowl.letter 
                    : piece.letter; // 极端防错兜底

                if (!consumedLetters.ContainsKey(actualLetter)) consumedLetters[actualLetter] = 0;
                consumedLetters[actualLetter]++;
            }
        }
        Dictionary<string, int> availableLetters = new Dictionary<string, int>();
        foreach (var kvp in totalNeededLetters)
        {
            string letter = kvp.Key;
            int total = kvp.Value;
            int consumed = consumedLetters.ContainsKey(letter) ? consumedLetters[letter] : 0;
            // 防御性归零，防止负数
            availableLetters[letter] = Mathf.Max(0, total - consumed); 
        }
        
        this.Puzzles = new HashSet<Bowl>();
        HashSet<string> processedLetters = new HashSet<string>();
        foreach (var oldBowl in sourceData.Puzzles)
        {
            if (oldBowl.status == 2) continue; // 已彻底通关销毁的字，跳过

            string letter = oldBowl.letter;
            if (processedLetters.Contains(letter)) continue; // 拦截幽灵分身

            int available = availableLetters.ContainsKey(letter) ? availableLetters[letter] : 0;
            int consumed = consumedLetters.ContainsKey(letter) ? consumedLetters[letter] : 0;

            if (available > 0)
            {
                oldBowl.count = available;
                oldBowl.totalcount=available;
                oldBowl.status = 0; // 正常显示
                this.Puzzles.Add(oldBowl);
                processedLetters.Add(letter);
            }
            else if (consumed > 0)
            {
                oldBowl.count = 0;
                oldBowl.status = 1; // 黑影锁定 (字在棋盘上)
                this.Puzzles.Add(oldBowl);
                processedLetters.Add(letter);
            }
        }
        foreach (var letter in totalNeededLetters.Keys)
        {
            if (!processedLetters.Contains(letter)) 
            {
                int available = availableLetters.ContainsKey(letter) ? availableLetters[letter] : 0;
                int consumed = consumedLetters.ContainsKey(letter) ? consumedLetters[letter] : 0;

                if (available > 0 || consumed > 0)
                {
                    Bowl recoveredBowl = new Bowl
                    {
                        id = "b_" + Guid.NewGuid().ToString("N")[..8],
                        letter = letter,
                        count = available,
                        totalcount = available,
                        status = available > 0 ? 0 : 1
                    };
                    this.Puzzles.Add(recoveredBowl);
                    processedLetters.Add(letter);
                    Debug.Log($"[存档自愈] 成功帮玩家找回了丢失的字：{letter}");
                }
            }
        }
        
        this.BoardSnapshot = sourceData.BoardSnapshot;
        this.Cousor = sourceData.Cousor;
        this.MaxRow = sourceData.MaxRow;
        this.MaxCol = sourceData.MaxCol;
        this.MinRow = sourceData.MinRow;
        this.MinCol = sourceData.MinCol;
        this.IsFirstEnter = sourceData.IsFirstEnter;
        this.CurrentTotalScore = sourceData.CurrentTotalScore;
        this.RemainingTime = sourceData.RemainingTime;
        TotalActiveSeconds = sourceData.TotalActiveSeconds;
        this.CurrentCombo = sourceData.CurrentCombo; // 🌟 恢复
        this.CurBreakIceCount = sourceData.CurBreakIceCount; // 🌟 恢复
        this.CurPickFlowerLeavesCount = sourceData.CurPickFlowerLeavesCount; // 🌟 恢复
        this.CurrPerfectCount = sourceData.CurrPerfectCount; // 🌟 恢复
        this.MaxCombo = sourceData.MaxCombo;         // 🌟 恢复
        this.EarnedPupaCount = sourceData.EarnedPupaCount; // 🌟 恢复
        this.PupaDatas=sourceData.PupaDatas;
        this.CollectedLeaves = sourceData.CollectedLeaves;
        this.FlowerActionCount = sourceData.FlowerActionCount;
        this.GoldLeafCount = sourceData.GoldLeafCount;
        this.TotalCumulativeCombos = sourceData.TotalCumulativeCombos; // 读档时恢复数据
        this.ChessGroup.Clear();
        this.ChessGroup=sourceData.ChessGroup;
        
        this.FoundTargetPuzzles = sourceData.FoundTargetPuzzles != null ?
            new List<string>(sourceData.FoundTargetPuzzles) : new List<string>();
        
        this.IsPausedOrFailed = sourceData.IsPausedOrFailed;
       
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
 
            // 数量不同直接返回
            bool foundword = (stageInfo.CurrBoardData.Count == loadedData.BoardSnapshot.Count);
                                       // 用 HashSet<T>.SetEquals 即可
                                       stageInfo.CurrBoardData.SetEquals(loadedData.BoardSnapshot);
            
            if (loadedData.StageId <= 0 || !foundword) 
            {
                InitializeFromStageInfo(stageInfo);
            }
            else
            {
                if (loadedData.ChessGroup.Count <= 0)
                {
                    loadedData.ChessGroup=stageInfo.ChessGroup;
                }
                InitializeFromExisting(loadedData);
            }
        }
        catch(System.Exception e)
        {
            Debug.LogError($"加载关卡数据失败: {e.Message}");
            InitializeFromStageInfo(stageInfo);
            AnalyticMgr.BugRecord("拼字关卡数据异常",e.Message);
        }
    }
    public void SaveToFile()
    {
        SaveFileName = CreateLevelIdentifier(StageId);

        string filePath = Path.Combine(Application.persistentDataPath, SaveFileName);
        try
        {
            // 转换数据
            //tempgroup = ChessGroup.ToDictionary(kv=> $"{kv.Key.row}_{kv.Key.col}", kv=>kv.Value);
            string json = JsonConvert.SerializeObject(this);
            string encryptedJson = SecurityProvider.ProtectData(json);
            File.WriteAllText(filePath, encryptedJson);

            Debug.Log($"拼字关卡进度已保存：{filePath}");
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
    
    public void UpdateGoldLeafCount(int gold)
    {
        this.GoldLeafCount += gold;
    }
    
    #endregion
}
