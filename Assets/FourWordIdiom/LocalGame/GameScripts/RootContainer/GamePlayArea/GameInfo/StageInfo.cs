using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#region 数据结构

/// <summary>
/// 棋盘数据结构
/// </summary>
/// <summary>
/// 棋盘数据结构
/// </summary>
public class BoardGame
{
    public int rows;
    public int cols;
    public int minRow;
    public int minCol;
    public List<List<List<char>>> board; // 三维列表：[行][列][字符层]
}

public class CellData
{
    public string character;
    public int row;
    public int col;
}

public class WordBlock
{
    public Vector2Int position;
    public List<string> characters; // 可能包含多个字符
    public string rawCharacters;    // 原始字符数据
}

public class IdiomData
{
    public string theme;
    public string word;
    public int difficulty;
    public List<IdiomBlock> blocks;
}

public class IdiomBlock
{
    public int index;
    public string character;
    public Vector2Int position;
}

public class LevelData
{
    public string pass; // 对应"1_pass"的内容
    public string russ; // 对应"1_russ"的内容
}

#endregion

/// <summary>
/// 关卡信息管理类 - 负责加载、解析和提供关卡数据
/// </summary>
public class StageInfo
{
    #region 私有字段

    private TextAsset _StageFile;       // 关卡文本资源
    private readonly int _StageNumber;  // 关卡编号
    private readonly int _StageInfoId;  // 关卡配置ID
    private bool _isStageFileLoaded;    // 文件加载状态
    private int _maxPuzzleLength = -1;    // 最大单词长度(延迟计算)

    private string _hint;               // 关卡提示
    private List<string> _Puzzles=new List<string>();        // 单词列表
    private BoardGame _boardData=new BoardGame();       // 棋盘数据

    public List<IdiomData> idioms = new List<IdiomData>();

    #endregion

    #region 公有属性

    /// <summary> 单词列表(已按长度排序) </summary>
    public List<string> Puzzles => _Puzzles;

    /// <summary> 棋盘数据 </summary>
    public BoardGame CurBoardData => _boardData;

    /// <summary> 关卡编号 </summary>
    public int StageNumber => _StageNumber;


    /// <summary> 最大单词长度(延迟计算) </summary>
    public int MaxPuzzleLength
    {
        get
        {
            if (_maxPuzzleLength == -1)
                CalculateMaxPuzzleLength();
            return _maxPuzzleLength;
        }
    }

    #endregion

    #region 构造函数

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="StageFile">关卡文本资源</param>
    /// <param name="StageInfoId">关卡配置ID</param>
    /// <param name="StageNumber">关卡编号</param>
    public StageInfo(TextAsset StageFile, int StageInfoId, int StageNumber)
    {
        _StageFile = StageFile;
        _StageInfoId = StageInfoId;
        _StageNumber = StageNumber;

        LoadStageData(); // 立即加载数据
    }

    #endregion

    #region 公有方法

    /// <summary>
    /// 检查单词是否存在于当前关卡
    /// </summary>
    public bool IsPuzzleInStage(string Puzzle)
    {
        return _Puzzles.Contains(Puzzle);
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 加载关卡数据
    /// </summary>
    private void LoadStageData()
    {
        if (_StageFile == null)
        {
            string fileName = GetStageFileNameByLanguage();
            _StageFile = AdvancedBundleLoader.SharedInstance.LoadTextFile(fileName, "hexlevel_"+_StageInfoId);
        }    

        string jsonContent = _StageFile.text;

        // 添加调试输出以检查原始JSON内容
        Debug.Log("Original JSON: " + jsonContent);


        // 统一提取方法
        string passContent = ExtractJsonValue(jsonContent, $"{_StageInfoId}_pass");
        if (passContent == null) return;

        string russContent = ExtractJsonValue(jsonContent, $"{_StageInfoId}_russ");
        if (russContent == null) return;

        Debug.Log("Extracted passContent: " + passContent);
        Debug.Log("Extracted russContent: " + russContent);  
   

        // 2. 计算棋盘尺寸
        var boardSize = CalculateBoardSizeFromPass(passContent);

        // 3. 初始化棋盘
        InitializeBoard(boardSize);

        // 4. 解析pass数据填充到棋盘
        ParsePassData(passContent);

        // 5. 解析russ数据（成语列表）
        ParseRussData(russContent);
    
        _isStageFileLoaded = true;
    }

    /// <summary>
    /// 安全提取JSON字符串值
    /// </summary>
    private string ExtractJsonValue(string json, string key)
    {
        // 构造带引号的键名
        string quotedKey = $"\"{key}\"";
        int keyStart = json.IndexOf(quotedKey, StringComparison.Ordinal);

        if (keyStart == -1)
        {
            Debug.LogError($"Failed to find '{key}' key in JSON");
            return null;
        }

        // 定位冒号位置
        int colonPos = json.IndexOf(':', keyStart + quotedKey.Length);
        if (colonPos == -1)
        {
            Debug.LogError($"No colon found after '{key}'");
            return null;
        }

        // 查找值起始引号
        int valueStart = colonPos + 1;
        while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
        {
            valueStart++;
        }

        if (valueStart >= json.Length || json[valueStart] != '"')
        {
            Debug.LogError($"Value start quote not found for '{key}'");
            return null;
        }

        // 查找值结束引号（处理转义）
        int contentStart = valueStart + 1;
        int contentEnd = FindUnescapedQuoteEnd(json, contentStart);
        if (contentEnd == -1)
        {
            Debug.LogError($"Closing quote not found for '{key}'");
            return null;
        }

        return json.Substring(contentStart, contentEnd - contentStart);
    }


    /// <summary>
    /// 查找非转义结束引号位置
    /// </summary>
    private int FindUnescapedQuoteEnd(string json, int startIndex)
    {
        for (int i = startIndex; i < json.Length; i++)
        {
            if (json[i] == '"')
            {
                // 检查前导反斜杠数量（处理转义）
                int slashCount = 0;
                for (int j = i - 1; j >= 0 && json[j] == '\\'; j--)
                {
                    slashCount++;
                }

                // 偶数个反斜杠表示非转义引号
                if (slashCount % 2 == 0)
                {
                    return i;
                }
            }
        }
        return -1;
    }

    /// <summary>
    /// 初始化棋盘数据结构
    /// </summary>
    private void InitializeBoard((int rows, int cols,int minRow,int minCol) size)
    {
        _boardData = new BoardGame
        {
            rows = size.rows,
            cols = size.cols,
            minRow = size.minRow,
            minCol = size.minCol,
            board = new List<List<List<char>>>()
        };

        // 初始化空棋盘（三维结构），每个单元格包含一个空字符
        for (int row = 0; row < size.rows; row++)
        {
            var rowList = new List<List<char>>();
            for (int col = 0; col < size.cols; col++)
            {
                // 每个单元格初始化为包含一个空字符('\0')的列表
                rowList.Add(new List<char> { '\0' });
            }
            _boardData.board.Add(rowList);
        }
    }

    /// <summary>
    /// 从pass数据计算棋盘的最大行和列
    /// </summary>
    private (int maxRow, int maxCol,int minRow, int minCol) CalculateBoardSizeFromPass(string passContent)
    {
        int maxRow = 0;
        int maxCol = 0;
        
        int minRow =int.MaxValue;
        int minCol =int.MaxValue;

        if (string.IsNullOrEmpty(passContent))
        {
            return (0, 0,0,0);
        }
        
        // 处理开头的"1_0="部分（如果存在）
        int startIndex = passContent.IndexOf('=');
        if (startIndex >= 0 && startIndex + 1 < passContent.Length)
        {
            passContent = passContent.Substring(startIndex + 1);
        }
        string[] cells = passContent.Split('|');

        foreach (string cell in cells)
        {
            // 分割位置部分（格式：行_列）
            int colonIndex = cell.IndexOf(':');
            if (colonIndex <= 0) continue;

            string positionPart = cell.Substring(0, colonIndex);
            string[] position = positionPart.Split('_');

            if (position.Length < 2) continue;

            // 解析行索引
            if (int.TryParse(position[1], out int row) && row > maxRow)
            {
                maxRow = row;
            }

            // 解析列索引
            if (int.TryParse(position[0], out int col) && col > maxCol)
            {
                maxCol = col;
            }
            
            // 解析行索引
            if (int.TryParse(position[1], out int mrow) && mrow < minRow)
            {
                minRow = row;
            }

            // 解析列索引
            if (int.TryParse(position[0], out int mcol) && mcol < minCol)
            {
                minCol = col;
            }
        }
        return (maxRow+1, maxCol+1, minRow, minCol);
    }


    /// <summary>
    /// 根据语言设置获取关卡文件名
    /// </summary>
    private string GetStageFileNameByLanguage()
    {
        switch (GameDataManager.instance.UserData.LanguageCode)
        {
            case "ChineseSimplified": return "chineseStage";
            case "CT": return "chinesetraStage";
            default: return "japanese";
        }
    }


    /// <summary>
    /// 解析pass数据并填充到棋盘（支持多层字符）
    /// </summary>
    private void ParsePassData(string passContent)
    {
        if (string.IsNullOrEmpty(passContent))
        {
            Debug.LogError("Pass content is empty");
            return;
        }

        string[] cells = passContent.Split('|');

        foreach (string cell in cells)
        {
            // 处理特殊格式：可能包含等号的位置定义
            string positionPart;
            string charsPart;

            if (cell.Contains("="))
            {
                // 处理 "1_0=3_6:呜_气" 这种格式
                string[] equalParts = cell.Split('=');
                if (equalParts.Length < 2) continue;

                // 获取等号后的部分 (3_6:呜_气)
                string[] colonParts = equalParts[1].Split(':');
                if (colonParts.Length < 2) continue;

                positionPart = colonParts[0];
                charsPart = colonParts[1];
            }
            else
            {
                // 处理常规格式 "3_5:满"
                string[] colonParts = cell.Split(':');
                if (colonParts.Length < 2) continue;

                positionPart = colonParts[0];
                charsPart = colonParts[1];
            }

            // 解析位置 (格式: "行_列")
            string[] position = positionPart.Split('_');
            if (position.Length < 2) continue;

            int row = int.Parse(position[1]);
            int col = int.Parse(position[0]);           
          
            // 确保位置在棋盘范围内
            if (row >= 0 && row < _boardData.rows &&
                col >= 0 && col < _boardData.cols)
            {
                // 清空初始的空字符
                _boardData.board[row][col].Clear();

                // 解析多层字符 (用_分隔)
                string[] charLayers = charsPart.Split('_');

                // 获取该位置的字符列表
                List<char> cellChars = _boardData.board[row][col];

                // 按顺序添加字符（从底层到顶层）
                foreach (string charStr in charLayers)
                {
                    if (!string.IsNullOrEmpty(charStr))
                    {
                        // 只取第一个字符（如果是多字符字符串）
                        cellChars.Add(charStr[0]);
                    }
                }

                // 反转列表使最后一个字符在顶部（可选，根据实际需求）
                cellChars.Reverse();

                // 调试输出
                Debug.Log($"Cell ({row}, {col}) layers: {string.Join(", ", cellChars)}");
            }
            
        }
    }

    void ParseRussData(string russData)
    {
        // 示例格式: "\"无主题\"_呜呼哀哉_1:#1_呜_3_6#1_呼_4_6..."
        string[] idiomEntries = russData.Split('|');     

        foreach (string entry in idiomEntries)
        {
            if (string.IsNullOrEmpty(entry)) continue;

            string[] parts = entry.Split('_');
            if (parts.Length < 3) continue;

            IdiomData idiom = new IdiomData
            {
                theme = parts[0].Trim('"'),
                word = parts[1],
                difficulty = int.Parse(parts[2].Split(':')[0]),
                blocks = new List<IdiomBlock>()
            };

            string[] wordInfos = entry.Split(':');
            // 解析成语块
            string[] blockInfos = wordInfos[1].Split('#');
            for (int i = 0; i < blockInfos.Length; i++)
            {
                string[] blockParts = blockInfos[i].Split('_');
                if (blockParts.Length < 3) continue;

                IdiomBlock idiomBlock = new IdiomBlock
                {
                    index = int.Parse(blockParts[0]),
                    character = blockParts[1],
                    position = new Vector2Int(int.Parse(blockParts[3]), int.Parse(blockParts[2]))
                };

                idiom.blocks.Add(idiomBlock);
            }

            idioms.Add(idiom);
            _Puzzles.Add(idiom.word);
        }
    }

    /// <summary>
    /// 按单词长度排序
    /// </summary>
    private void SortPuzzles()
    {
        _Puzzles.Sort((a, b) => a.Length.CompareTo(b.Length));
    }

    /// <summary>
    /// 计算最大单词长度
    /// </summary>
    private void CalculateMaxPuzzleLength()
    {
        _maxPuzzleLength = 0;
        foreach (var Puzzle in _Puzzles)
        {
            if (Puzzle.Length > _maxPuzzleLength)
                _maxPuzzleLength = Puzzle.Length;
        }
    }

    #endregion
}