using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Middleware;
using UnityEngine;

/// <summary>
/// 词组数据结构
/// </summary>
[Serializable]
public class PhraseGroup // phrase group
{
    public string id;   // 词组ID
    public int direction; // 方向 1横向，0纵向
    public List<Chesspiece> chesspieces; // 字块列表 
}
[Serializable]  
public class Chesspiece: IEquatable<Chesspiece> //Chess piece
{
    public string id;   // 格子ID
    public int row;     // 格子行
    public int col;     // 格子列
    public int direction; // 格子方向
    public string letter; // 格子的词
    public TileState state;  // 格子当前的状态
    public bool tip = false; // 格子是否提示词
    public Bowl bowl; // 填入字
    public bool isUsed;  
    public bool Equals(Chesspiece other) => other != null && row == other.row && col == other.col;
    public override bool Equals(object obj) => Equals(obj as Chesspiece);
    public override int GetHashCode() => row * 1000 + col;
}
[Serializable]
public class Bowl
{
    public string id;
    public string letter;
    public int status;  
    public bool isUsed;  
}
/// <summary>
/// 关卡信息类 - 负责加载、解析和提供关卡数据
/// </summary>
public class ChessStageInfo
{
    #region 私有字段
    private ChessLevelConf _StageConf;      // 关卡文本资源
    private readonly int _StageNumber; // 关卡编号
    private readonly int _StageInfoId; // 关卡配置ID
    //private bool _IsStageFileLoaded;   // 文件加载状态
    private int _MaxRow = -1; // 最大row（延迟计算）
    private int _MaxCol = -1;   // 最大col（延迟计算）
    private int _MinRow = 0;
    private int _MinCol = 0;
    
    private HashSet<Bowl> _puzzles;         // 字堆单词列表
    private HashSet<Chesspiece> _chesspiece;  // 棋盘配置字
    private Dictionary<(int row, int col), HashSet<PhraseGroup>> _chessGroup;  // 字和组关联
    private List<int> _cursor;             // 初始光标位置
    
    private List<PhraseGroup> _phraseGroups;   // 组列表

    #endregion

    #region 公有属性

    public HashSet<Bowl> Puzzles => _puzzles;
    public HashSet<Chesspiece> CurrBoardData => _chesspiece;
    public int StageNumber => _StageNumber;
    public List<int> Currsor => _cursor;
    public Dictionary<(int row, int col), HashSet<PhraseGroup>> ChessGroup => _chessGroup;
    public int MaxRow => _MaxRow;
    public int MaxCol => _MaxCol;
    public int MinRow => _MinRow;
    public int MinCol => _MinCol;
    
    public List<PhraseGroup> PhraseGroups => _phraseGroups;
    #endregion

    #region 构造函数
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="stageConf">关卡文本资源</param>
    /// <param name="stageinfoid">关卡配置ID</param>
    /// <param name="stagenumber">关卡编号</param>
    /// <param name="wordCount">动态字</param>
    public ChessStageInfo(ChessLevelConf stageConf, int stageinfoid, int stagenumber, int wordCount)
    {
        _StageConf = stageConf;
        _StageNumber = stagenumber;
        _StageInfoId = stageinfoid;
        _puzzles = new HashSet<Bowl>();
        _chesspiece = new HashSet<Chesspiece>();
        _cursor = new List<int>();
        _chessGroup = new Dictionary<(int row, int col), HashSet<PhraseGroup>>();
        _phraseGroups = new();
        
        LoadStageData();
        
        if (wordCount != 0)
            DynamicHardLevelChange(wordCount);
    }

    #endregion


    #region 私有方法
    private void DynamicHardLevelChange(int wordCount)
    {
        if (_puzzles.Count >= 32) return;

        if (wordCount > 0)   // 变简单，增加可见字
        {
            Debug.Log($"开始增加 {wordCount} 个可见字");
            if (ChessDynamicHardManager.Instance.GetHardMode(_StageNumber) == 1)
            {
                Debug.Log("进入小幅度简单---------------");
                IncreaseShowWord1(wordCount);
            }
            else
            {
                Debug.Log("进入大幅度简单---------------");
                IncreaseShowWord2(wordCount);
            }
        }
        else // 变难, 减少可见字
        {
            DecreaseShowWord(wordCount);
        }
    }

    /// <summary>
    /// 增加可见字，小幅度简单
    /// </summary>
    /// <param name="number">数量</param>
    private void IncreaseShowWord1(int number)
    {
        var usedPos = new HashSet<(int row, int col)>();
        var changedLetters = new List<string>();
        do
        {
            number--;
            var candidates = new List<(Chesspiece piece, PhraseGroup group, int index)>();
            foreach (var g in _phraseGroups)
            {
                if (g.chesspieces.Count(cp => cp.state == TileState.None) < 3) continue;
                for (int i = 0; i < g.chesspieces.Count; i++)
                {
                    var cp = g.chesspieces[i];
                    if(cp.state == TileState.None && !usedPos.Contains((cp.row, cp.col)))
                        candidates.Add((cp, g,i));
                }
            }

            var filtered = candidates.Where(c =>
            {
                int row = c.piece.row, col = c.piece.col;
                bool selfHeadTail = c.index == 0 || c.index == c.group.chesspieces.Count - 1;
                bool crossHeadTail = IsHeadOrTailOfAnyGroup(row, col, _chessGroup);
                return !(selfHeadTail && crossHeadTail);
            }).ToList();
            
            if(filtered.Count == 0) break;
            
            filtered.Sort((a, b) =>
            {
                int hiddenA = a.group.chesspieces.Count(p => p.state == TileState.None);
                int hiddenB = b.group.chesspieces.Count(p => p.state == TileState.None);
                return hiddenB.CompareTo(hiddenA);
            });
            
            filtered.Reverse();   // ← 新增：倒序
            var targetPiece =  filtered[0].piece;
            Debug.Log($"准备写回：{targetPiece?.letter} ({targetPiece?.row},{targetPiece?.col})  原state={targetPiece?.state}");
            targetPiece!.state = TileState.Default;
            var origConfig = _chesspiece.First(cp => cp.row == targetPiece.row && cp.col == targetPiece.col);
            origConfig.state  = TileState.Default;
            
            changedLetters.Add(targetPiece.letter);
            usedPos.Add((targetPiece.row, targetPiece.col));
            Debug.Log($"已点亮：{targetPiece.letter}  state={targetPiece.state}");
        }while (number > 0);

        foreach (var key in usedPos)
        {
            var origConfig = _chesspiece.First(cp => cp.row == key.row && cp.col == key.col);
            Debug.Log($"检查棋盘显示： {origConfig.letter} state={origConfig.state}");
        }
        
        // 批量从 HashSet 移除
        foreach (var letter in changedLetters)
        {
            var bowl = _puzzles.FirstOrDefault(b => b.letter == letter);
            if (bowl != null) _puzzles.Remove(bowl);
        }
        
        Debug.Log($"_cursor长度={_cursor.Count} 内容 " + JsonConvert.SerializeObject(_cursor) );
        
        if (_cursor.Count < 2 || usedPos.Contains((_cursor[0], _cursor[1])))
        {
            Chesspiece nextCross  = GetFirstCrossNoneChess();
            if (nextCross  != null)
            {
                _cursor.Clear();
                _cursor.Add(nextCross .row);
                _cursor.Add(nextCross .col);
            }
        }

        Debug.Log("增加可见字完成—— " + string.Join(", ", changedLetters));
    }

    /// <summary>
    /// 增加可见字，大幅度简单
    /// </summary>
    /// <param name="number">数量</param>
    private void IncreaseShowWord2(int number)
    {
        // 物理去重：记录已处理的坐标
        var usedPos = new HashSet<(int row, int col)>();
        var changedLetters = new List<string>();
        do
        {
            number--;
            var qualifiedGroups = _phraseGroups
                .Where(g => g.chesspieces.Count(cp => cp.state == TileState.None) >= 2)
                .OrderByDescending(g => g.chesspieces.Count(cp => cp.state == TileState.None))
                .ThenBy(g => g.direction)
                .ToList();
            
            if(!qualifiedGroups.Any()) break;

            Chesspiece selected = null;
            PhraseGroup fromGroup = null;

            foreach (var g in qualifiedGroups)
            {
                bool isHorz = g.direction == 1;
                var sequence = isHorz ? g.chesspieces.OrderBy(cp=>cp.row) : g.chesspieces.OrderBy(cp=>cp.col);

                foreach (var cp in sequence)
                {
                    if(cp.state != TileState.None || usedPos.Contains((cp.row, cp.col)))
                        continue;
                    
                    bool isCross = _chessGroup.TryGetValue((cp.row, cp.col), out var c) && c.Count >= 2;
                    if (isCross)
                    {
                        selected = cp;
                        fromGroup = g;
                        break;
                    }
                }
                if(selected != null) break;
                foreach (var cp in sequence)
                {
                    if(cp.state != TileState.None || usedPos.Contains((cp.row, cp.col)))
                        continue;
                    selected = cp;
                    fromGroup = g;
                    break;
                }

                if (selected != null) break;
            }
            
            if(selected == null) break;
            Debug.Log($"准备写回：{selected?.letter} ({selected?.row},{selected?.col})  原state={selected?.state}");
            selected.state = TileState.Default;
            usedPos.Add((selected.row, selected.col));
            changedLetters.Add(selected.letter);
            var origConfig = _chesspiece.First(cp => cp.row == selected.row && cp.col == selected.col);
            origConfig.state = TileState.Default;
            Debug.Log($"已点亮：{selected.letter}  state={selected.state}");
        }while (number > 0);

        Debug.Log("增加可见字完成, 关卡内容" + JsonConvert.SerializeObject(_chesspiece));
        // ---------- 5. 从池子里移除已揭示字母 ----------
        foreach (var letter in changedLetters)
        {
            var bowl = _puzzles.FirstOrDefault(b => b.letter == letter);
            if (bowl != null) _puzzles.Remove(bowl);
            
        }
        // ---------- 6. 光标移到下一个交叉未填字 ----------
        
        if (usedPos.Contains((_cursor[0], _cursor[1])))
        {
            Chesspiece nextCross  = GetFirstCrossNoneChess();
            Debug.Log("初始字为: " + JsonConvert.SerializeObject(nextCross));
            if (nextCross  != null)
            {
                _cursor.Clear();
                _cursor.Add(nextCross .row);
                _cursor.Add(nextCross .col);
            }
        }
       
        Debug.Log("增加可见字完成== " + string.Join(", ", changedLetters));
    }
    
    /// <summary>
    /// 减少可见字
    /// </summary>
    /// <param name="number">数量</param>
    private void DecreaseShowWord(int number)
    {
        int minShow = 3; // 最少留 3 个显示字
        var changedPieces = new List<Chesspiece>();
        do
        {
            number++;
            // 1. 收集所有未用且显示的位置
            var candidates = new List<(Chesspiece piece, PhraseGroup group, int index)>();
            foreach (var g in _phraseGroups)
            {
                for (int i = 0; i < g.chesspieces.Count; i++)
                {
                    var cp = g.chesspieces[i];
                    if (cp.state == TileState.Default)
                    {
                        candidates.Add((cp, g, i));
                    }
                }
            }
       
            // 2. 排序：① 多组交叉放最后 ② 0 最少 → 最多 ③ 1 最多 → 最少
            candidates.Sort((a, b) =>
            {
                // ① 非交叉优先（非交叉在前）
                bool multiA = IsMultiGroup(a.piece.row, a.piece.col);
                bool multiB = IsMultiGroup(b.piece.row, b.piece.col);
                int crossCmp = multiA.CompareTo(multiB);   // false < true
                if (crossCmp != 0) return crossCmp;
                
                // ② 交叉字且存在另一个交叉显示字 → 提升优先级（放前面）
                bool hasA = multiA && HasCrossSibling(a.piece.row, a.piece.col, _phraseGroups);
                bool hasB = multiB && HasCrossSibling(b.piece.row, b.piece.col, _phraseGroups);
                int siblingCmp = hasB.CompareTo(hasA);   // 有 sibling 的放前面
                if (siblingCmp != 0) return siblingCmp;
                
                // ③ 展示字数多优先（降序）
                int onesA = a.group.chesspieces.Count(p => p.state == TileState.Default);
                int onesB = b.group.chesspieces.Count(p => p.state == TileState.Default);
                int onesCmp = onesB.CompareTo(onesA);      // 降序
                if (onesCmp != 0) return onesCmp;
                
                // ④ 组内倒序（index 降序）
                return b.index.CompareTo(a.index);             // 尾部在前
            });
            
            // 3. 正序取第一个未用
            var target = candidates.FirstOrDefault();
            if (target == default) break; // 无候选
            var targetPiece = target.piece;
            targetPiece.state = TileState.None;
           
 
            // // 4. 首/尾均显示 → 可去尾；若尾关联其他组 → 去首；**若首尾皆不关联其他组，则任去一端**
            // bool isHead = target.index == 0;
            // bool isTail = target.index == target.group.chesspieces.Count - 1;
            // if (isHead || isTail)                       // 本身是首或尾
            // {
            //     bool headShow = target.group.chesspieces[0].state == TileState.Default;
            //     bool tailShow = target.group.chesspieces[^1].state == TileState.Default;
            //
            //     if (headShow && tailShow)               // 首尾均显示
            //     {
            //         bool headMulti = IsMultiGroup(target.group.chesspieces[0].row,
            //             target.group.chesspieces[0].col);
            //         bool tailMulti = IsMultiGroup(target.group.chesspieces[^1].row,
            //             target.group.chesspieces[^1].col);
            //
            //         // 先挑无关联端
            //         if (!headMulti || !tailMulti)
            //         {
            //             // 无关联端直接隐藏
            //             targetPiece.isUsed = true;
            //             changedPieces.Add(targetPiece);
            //
            //             // 同字母全部标记为已用
            //             foreach (var same in candidates.Where(c => c.piece.letter == targetPiece.letter))
            //                 same.piece.isUsed = true;
            //
            //             number++;
            //             continue;   // 本轮结束
            //         }
            //
            //         // 两端都关联 → 强制去首（可改成随机）
            //         targetPiece.isUsed = true;
            //         changedPieces.Add(targetPiece);
            //
            //         foreach (var same in candidates.Where(c => c.piece.letter == targetPiece.letter))
            //             same.piece.isUsed = true;
            //
            //         number++;
            //         continue;   // 本轮结束
            //     }
            // }
          
            // 5. 保证棋盘 ≥ 3 个显示字
            if (ShowCount() <= minShow) break;
            
            // 6. 隐藏并标记
            changedPieces.Add(targetPiece);
  
        } while (number < 0);

        foreach (var piece in changedPieces)
        {
            if (_puzzles.Count >= 32) break;
            
            Chesspiece findpize = _chesspiece.FirstOrDefault(p => p.row == piece.row && p.col == piece.col);
            if (findpize.state != TileState.None)
                findpize.state = TileState.None;

            Debug.Log("再看字是否隐藏成功-->" + JsonConvert.SerializeObject(findpize));
            _puzzles.Add(new Bowl
            {
                id = "b_" + Guid.NewGuid().ToString("N")[..8],
                letter = piece.letter,
                status = 0,
            });
        }
        // Chesspiece newCp = GetFirstCrossNoneChess();
        // if (newCp != null)
        // {
        //     _cursor.Clear();
        //     _cursor.Add(newCp.row);
        //     _cursor.Add(newCp.col);
        // }
        Debug.Log("钱少可见字完成--> " + JsonConvert.SerializeObject(changedPieces));
    }
    
    /// <summary>
    /// 判断 (r,c) 是否是任意词组的首或尾格子
    /// </summary>
    private bool IsHeadOrTailOfAnyGroup(int r, int c,Dictionary<(int r, int c), HashSet<PhraseGroup>> chessGroup)
    {
        if (!chessGroup.TryGetValue((r, c), out var set)) return false;
        foreach (var g in set)
        {
            int idx = g.chesspieces.FindIndex(p => p.row == r && p.col == c);
            if (idx == 0 || idx == g.chesspieces.Count - 1)
                return true;
        }
        return false;
    }
    /// <summary>
    /// 该格子所在的所有词组中，任意一个的 1 的数量 ≠ 3 即可通过
    /// </summary>
    private bool AnyGroupOnesNotEqual3(int r, int c,
        Dictionary<(int r, int c), HashSet<PhraseGroup>> chessGroup)
    {
        if (!chessGroup.TryGetValue((r, c), out var set)) return false;
        foreach (var g in set)
        {
            int ones = g.chesspieces.Count(p => p.state == TileState.Default);
            if (ones != 3) return true;   // 只要有一个词组 1≠3 就放行
        }
        return false;                     // 所有词组 1 都=3，淘汰
    }
    
    /// <summary> 该格子是否被多个词组共享 </summary>
    public bool IsMultiGroup(int r, int c) =>
        _chessGroup.TryGetValue((r, c), out var set) && set.Count > 1;
    
    /// <summary>
    /// 该交叉字所在的所有组中，是否存在另一个**也关联其他组**的显示字
    /// </summary>
    private bool HasCrossSibling(int r, int c, List<PhraseGroup> groups)
    {
        if (!groups.Any(g => g.chesspieces.Any(p => p.row == r && p.col == c)))
            return false;

        foreach (var g in groups)
        {
            // 跳过当前字所在组
            if (g.chesspieces.Any(p => p.row == r && p.col == c))
                continue;

            // 找该组内**显示且也关联其他组**的字
            foreach (var p in g.chesspieces)
            {
                if (p.state == TileState.Default && IsMultiGroup(p.row, p.col))
                    return true;   // 存在另一个交叉显示字
            }
        }
        return false;
    }
    /// <summary> 整个棋盘显示字数量 </summary>
    private int ShowCount() =>
        _chesspiece.Count(p => p.state == TileState.Default);
    
    private void LoadStageData()
    {
        if (_StageConf == null)
        {
            _StageConf = ChessStageController.Instance.PackInfos.Get(_StageInfoId);
        }

        ParseStageContent(_StageConf);
    }
    /// <summary>
    /// 返回棋盘内从上到下、从左到右第一个未填写（None）且属于≥2个组（交叉字）的字。
    /// 没有满足条件的字时返回 null。
    /// </summary>
    private Chesspiece GetFirstCrossNoneChess()
    {
        return _chesspiece
            .OrderBy(cp => cp.row)      // 从上到下
            .ThenBy(cp => cp.col)       // 从左到右
            .FirstOrDefault(cp =>
                _chessGroup.TryGetValue((cp.row, cp.col), out var groups) &&
                groups.Count >= 2 &&                              // 交叉字
                cp.state == TileState.None);                // 未填写
    }
    
    /// <summary>
    /// 解析关卡文件内容
    /// </summary>
    /// <param name="stageConf"></param>
    private void ParseStageContent(ChessLevelConf stageConf)
    {
        Debug.Log($"关卡文本内容 : 对应的目标词处理："+ JsonConvert.SerializeObject(stageConf));
        List<PhraseGroup> tempGroup = new List<PhraseGroup>();
        int maxRow = 0;
        int maxCol = 0;
        int minRow = int.MaxValue;
        int minCol = int.MaxValue;

        string[] chunks = stageConf.pass.Split('#');

        int chunkIndex = 0; // chunk 序号
        foreach (string chunk in chunks)
        {
            string[] block = chunk.Split(',');
            int tens = int.Parse(block[0][0].ToString()) ;
            int units = int.Parse(block[0][1].ToString());
            int direction = int.Parse(block[1]);
            string id = $"pg_{chunkIndex}_{block[0]}_{block[1]}";
            PhraseGroup boardGame = new PhraseGroup
            {
                id = id,
                direction = direction,
                chesspieces = new List<Chesspiece>(),
            };

            for (int i = 0; i < block[2].Length; i++)
            {
                int r = direction == 0 ? tens: tens + i;
                int c = direction == 0 ? units -i : units;
                maxRow = Mathf.Max(maxRow, r);
                maxCol = Mathf.Max(maxCol, c);
                minRow = Mathf.Min(minRow, r);
                minCol = Mathf.Min(minCol, c);

                string word = block[2][i].ToString();
                int show = int.Parse(block[3][i].ToString());

                Chesspiece puzzle = new Chesspiece
                {
                    id = $"cp_{chunkIndex}_{i}",
                    row = r,
                    col = c,
                    direction = direction,
                    letter = word,
                    state = show == 1 ? TileState.Default : TileState.None,
                };
                boardGame.chesspieces.Add(puzzle);
            }
            tempGroup.Add(boardGame);
            _phraseGroups.Add(boardGame);
            chunkIndex++;
        }
 
        // 分组对应的词
        foreach (var group in _phraseGroups)
        {
            foreach(var piece in group.chesspieces)
            {
                _chesspiece.Add(piece);
                if (!_chessGroup.ContainsKey((piece.row, piece.col)))
                    _chessGroup[(piece.row, piece.col)] = new HashSet<PhraseGroup>();
                _chessGroup[(piece.row, piece.col)].Add(group);
            }
        }
        // 添加词堆字
        int idCounter = 0;
        string[] chessBowls = stageConf.russ.Split('#');
        if (_StageNumber != 1)
        {
            chessBowls.Shuffle();
        }
        
        foreach(var chessbowl in chessBowls)
        {
            _puzzles.Add(new Bowl
            {
                id = "b_" + Guid.NewGuid().ToString("N")[..8],
                letter = chessbowl ,
                status = 0,
            });
        }
        
        // 处理光标
        if(stageConf.cursor != null)
        {
            string[] cursor = stageConf.cursor.Split(",",2, StringSplitOptions.RemoveEmptyEntries);
            foreach (string s in cursor)
            {
                int v = int.Parse(s);
                this._cursor.Add(v);
            }
        }
        _MaxRow = maxRow;
        _MaxCol = maxCol;
        _MinRow = minRow;
        _MinCol = minCol;
        
        Debug.Log($"关卡 {_StageNumber} 解析完成： 最大行 {_MaxRow}， 最大列 {_MaxCol}， 最小行 {_MinRow}， 最小列 {_MinCol} ");
        // _phraseGroups = tempGroup;
    }
    #endregion
}
