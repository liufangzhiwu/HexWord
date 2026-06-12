using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Middleware;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 词组数据结构
/// </summary>
[Serializable]
public class PhraseGroup // phrase group
{
    public string id;   // 词组ID
    public int direction; // 方向 1横向，0纵向
    public int weight;   // 权重
    public int quadrant; // 象限位置
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
    public bool isGoldLeaf;
    public bool hasIce = false; // 是否被冰块覆盖
    public bool hasFlower = false; // 是否被花朵覆盖 (仅限 Default 初始字)
    public bool hasLeaf = false;   // 是否有树叶标记 (跟随光标移动)
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
    public bool isGoldLeaf; 
    public int count = 1; // 👇 新增：记录该字块的数量，默认为1
    public int totalcount = 1; // 👇 新增：记录该字块的数量，默认为1
    public string pinyin;
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
    public Chesspiece _pupaData=null;       // 蚕蛹数据
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
        else if(_cursor.Count < 2)
        {
            GroupWeightSort();
            Chesspiece cpp = FindMinRowNonePiece();
            if (cpp != null)
            {
                this._cursor.Add(cpp.row);
                this._cursor.Add(cpp.col);
            }
        }

        InitMechanics();
    }

    #endregion


    #region 私有方法
    private void DynamicHardLevelChange(int wordCount)
    {
        if (_puzzles.Count >= 32) return;
        // 👇 核心拦截：检测当前关卡是否包含特殊玩法（算法生成或固定 elem 配置）
        bool hasIce = ChessStageController.Instance.CheckIceMechanic(_StageNumber, out _, out _);
        bool hasFlower = ChessStageController.Instance.CheckFlowerMechanic(_StageNumber, out _, out _);
        bool hasLeaf = ChessStageController.Instance.CheckLeafMechanic(_StageNumber, out _);
        bool hasFixedIceOrFlower = _StageConf != null && !string.IsNullOrEmpty(_StageConf.elem) && 
                                   (_StageConf.elem.Contains(",8") || _StageConf.elem.Contains(",3"));

        if (hasIce || hasFlower || hasLeaf|| hasFixedIceOrFlower)
        {
            Debug.Log($"[动态难度机制] 关卡 {_StageNumber} 为冰块/花朵特殊关卡，彻底不触发动态难度调整（字数保持原设）。");
            // 即使不做难度调整，依然建议执行一次基础数据终态校准，防止配置源文件本身存在全显词组或数量不一致
            VerifyAndSyncPuzzles();
            return; // 💥 直接中断，不往下走任何加减字的逻辑
        }
        
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

        VerifyAndSyncPuzzles();
    }
    /// <summary>
    /// 校验关卡数据终态：
    /// 1. 检查是否有词组全部为 Default (全显示)，如果有，强制隐藏其中一个。
    /// 2. 严格校验棋盘上的隐藏字(None)与字盘(_puzzles)的数量是否一致，多退少补。
    /// </summary>
    public void VerifyAndSyncPuzzles()
    {
        // ==========================================
        // 1. 检查每组内是否全部为 Default
        // ==========================================
        foreach (var group in _phraseGroups)
        {
            if (group.chesspieces.Count == 0) continue;

            // 检查这组是否全是 Default
            bool isAllDefault = group.chesspieces.All(p => p.state == TileState.Default);
            
            if (isAllDefault)
            {
                // 找一个合适的字隐藏：优先找不与其他组交叉的字，如果没有，就取第一个字
                Chesspiece targetToHide = group.chesspieces.FirstOrDefault(p => !IsMultiGroup(p.row, p.col)) 
                                       ?? group.chesspieces.First();
                
                targetToHide.state = TileState.None;
                
                // 同步更新 HashSet 中引用的对象状态 (安全起见)
                var origConfig = _chesspiece.FirstOrDefault(cp => cp.row == targetToHide.row && cp.col == targetToHide.col);
                if (origConfig != null) origConfig.state = TileState.None;

                Debug.Log($"[数据校验] 发现全显词组 {group.id}，强制隐藏字: '{targetToHide.letter}' ({targetToHide.row}, {targetToHide.col})");
                Debug.Log($"[数据校验] 发现全显词组 " + JsonConvert.SerializeObject(group.chesspieces));
            }
        }

        // ==========================================
        // 2. 统计棋盘上真实需要的隐藏字 (state == None)
        // ==========================================
        Dictionary<string, int> requiredCounts = new Dictionary<string, int>();
        foreach (var piece in _chesspiece)
        {
            if (piece.state is TileState.None or TileState.Check)
            {
                if (!requiredCounts.ContainsKey(piece.letter))
                    requiredCounts[piece.letter] = 0;
                
                requiredCounts[piece.letter]++;
            }
        }

        // ==========================================
        // 3. 统计当前字盘(_puzzles)里拥有的字
        // ==========================================
        Dictionary<string, int> currentCounts = new Dictionary<string, int>();
        foreach (var bowl in _puzzles)
        {
            if (!currentCounts.ContainsKey(bowl.letter))
                currentCounts[bowl.letter] = 0;
            
            currentCounts[bowl.letter]++;
        }

        // ==========================================
        // 4. 对比并进行“多退少补”
        // ==========================================
        
        // 4.1 补齐缺少的字
        foreach (var kvp in requiredCounts)
        {
            string letter = kvp.Key;
            int reqCount = kvp.Value;
            Bowl existingBowl = _puzzles.FirstOrDefault(b => b.letter == letter);
            // int currCount = currentCounts.ContainsKey(letter) ? currentCounts[letter] : 0;
            int currCount = existingBowl?.count ?? 0;
            if (currCount < reqCount)
            {
                if (existingBowl != null)
                {
                    existingBowl.count = reqCount; // 更新数量
                }
                else
                {
                    _puzzles.Add(new Bowl
                    {
                        id = "b_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                        letter = letter,
                        status = 0,
                        count = reqCount, // 初始数量
                        totalcount = reqCount // 初始数量
                    });
                }
            }
        }

        // 4.2 移除多余的字
        List<Bowl> bowlsToRemove = new List<Bowl>();
        foreach (var bowl in _puzzles)
        {
            int reqCount = requiredCounts.ContainsKey(bowl.letter) ? requiredCounts[bowl.letter] : 0;

            if (bowl.count > reqCount)
            {
                bowl.count = reqCount;
                bowl.totalcount = reqCount;
                if (bowl.count <= 0)
                {
                    bowlsToRemove.Add(bowl); // 记录需要彻底删除的 Bowl
                }
            }
        }
        foreach(var b in bowlsToRemove) _puzzles.Remove(b);
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
            Debug.Log($"已点亮：{origConfig.letter}  state={origConfig.state}");
        }while (number > 0);

        foreach (var key in usedPos)
        {
            var origConfig = _chesspiece.First(cp => cp.row == key.row && cp.col == key.col);
            Debug.Log($"检查棋盘显示： {origConfig.letter} state={origConfig.state}");
        }
        
        // 批量从 HashSet 移除
        // foreach (var letter in changedLetters)
        // {
        //     var bowl = _puzzles.FirstOrDefault(b => b.letter == letter);
        //     if (bowl != null) _puzzles.Remove(bowl);
        // }
        
        RandomlySetOneDefaultToNone();
        if (_cursor.Count < 2 || usedPos.Contains((_cursor[0], _cursor[1])))
        {
            GroupWeightSort();
            Chesspiece nextCross  = FindMinRowNonePiece();
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
                        break;
                    }
                }
                if(selected != null) break;
                foreach (var cp in sequence)
                {
                    if(cp.state != TileState.None || usedPos.Contains((cp.row, cp.col)))
                        continue;
                    selected = cp;
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
            Debug.Log($"已点亮：{origConfig.letter}  state={origConfig.state}");
        }while (number > 0);

        Debug.Log("增加可见字完成, 关卡内容" + JsonConvert.SerializeObject(_chesspiece));
        // ---------- 5. 从池子里移除已揭示字母 ----------
        // foreach (var letter in changedLetters)
        // {
        //     var bowl = _puzzles.FirstOrDefault(b => b.letter == letter);
        //     if (bowl != null) _puzzles.Remove(bowl);
        // }
        // ---------- 6. 光标移到下一个交叉未填字 ----------
        RandomlySetOneDefaultToNone();
        if (_cursor.Count < 2 || usedPos.Contains((_cursor[0], _cursor[1])))
        {
            GroupWeightSort();
            Chesspiece nextCross  = FindMinRowNonePiece();
            _cursor.Clear();
            Debug.Log("初始字为: " + JsonConvert.SerializeObject(nextCross));
            if (nextCross  != null)
            {
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
                bool isIsolatedGroup = !g.chesspieces.Any(p => IsMultiGroup(p.row, p.col));
                if (isIsolatedGroup) continue;
                
                for (int i = 0; i < g.chesspieces.Count; i++)
                {
                    var cp = g.chesspieces[i];
                    if (cp.state == TileState.Default)
                    {
                        candidates.Add((cp, g, i));
                    }
                }
            }
            if (candidates.Count == 0)
            {
                Debug.Log("没有符合条件的关联组可供减少显示字，提前结束。");
                break;
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
            changedPieces.Add(targetPiece);
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
     
  
        } while (number < 0);
        
        foreach (var piece in changedPieces)
        {
            if (_puzzles.Count >= 32) break;
            
            Chesspiece findpize = _chesspiece.FirstOrDefault(p => p.row == piece.row && p.col == piece.col);
            if (findpize != null && findpize.state != TileState.None)
                findpize.state = TileState.None;
            
            // _puzzles.Add(new Bowl
            // {
            //     id = "b_" + Guid.NewGuid().ToString("N")[..8],
            //     letter = piece.letter,
            //     status = 0,
            // });
        }
        GroupWeightSort();
        Chesspiece newCp = FindMinRowNonePiece();
        if (newCp != null)
        {
            _cursor.Clear();
            _cursor.Add(newCp.row);
            _cursor.Add(newCp.col);
        }
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
    /// 1. 先收集所有 state==Default 的字块；
    /// 2. 只保留“只属于 1 个组”的那些字块；
    /// 3. 若集合为空直接返回；
    /// 4. 否则随机选 1 个，把它的 state 改成 None。
    /// </summary>
    public void RandomlySetOneDefaultToNone()
    {
        // 1. 统计每个字块隶属的组数
        var groupCount = new Dictionary<Chesspiece, int>();
        foreach (var g in _phraseGroups)
        foreach (var p in g.chesspieces)
            groupCount[p] = groupCount.TryGetValue(p, out int c) ? c + 1 : 1;

        // 2. 筛选：state==Default 且 只属于 1 个组
        List<Chesspiece> candidates = _phraseGroups
            .SelectMany(g => g.chesspieces)
            .Where(p => p.state == TileState.Default && groupCount[p] == 1)
            .ToList();
        
        if (candidates.Count == 0) return;   // 没有符合要求的，安全退出

        // 3. 随机选 1 个
        Chesspiece picked = candidates[Random.Range(0, candidates.Count)];

        // 4. 修改状态
        picked.state = TileState.None;
        Chesspiece findpize = _chesspiece.FirstOrDefault(p => p.row == picked.row && p.col == picked.col);
        if (findpize.state != TileState.None)
            findpize.state = TileState.None;
        
        _puzzles.Add(new Bowl{
            id = "b_" + Guid.NewGuid().ToString("N")[..8],
            letter = picked.letter ,
            status = 0,
        });
    }
    /// <summary>
    /// 在所有组中按 weight 降序查找，返回第一个拥有
    /// state==None 且 row 最小的那个。
    /// 找不到返回 null。
    /// </summary>
    public Chesspiece FindMinRowNonePiece()
    {
            // ① 组级排序：weight 高→低，同 weight 时 direction 高→低
        var sortedGroups = _phraseGroups
                .OrderByDescending(g => g.weight) // 组按 weight 大→小排序
                .ThenByDescending(g=>g.quadrant) // 按四象限位置排序
                .ThenBy(g => g.chesspieces          // 首空：row 小→大，col 大→小
                    .Where(p => p.state is TileState.None)
                    .Select(p => (p.row, -p.col))
                    .DefaultIfEmpty((int.MaxValue, 0))
                    .First())        // 按首次出现空格的最小row和最大col排序
                .ThenByDescending(g => g.direction)
                .ToList(); // 同权重时方向值大优先 高→低
            
        Debug.Log("看看所有组的分数: " + JsonConvert.SerializeObject(sortedGroups));
  
        var representative = sortedGroups[0].chesspieces
                    .Where(p => p.state == TileState.None)
                    .OrderBy(p => p.row)   // 组内 row 最小
                    .ThenByDescending(p=>p.col)    // 组内col最大
                    .FirstOrDefault();     // 本组代表
       
        return representative;
    }
    /// <summary>
    /// 解析关卡文件内容
    /// </summary>
    /// <param name="stageConf"></param>
    private void ParseStageContent(ChessLevelConf stageConf)
    {
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
        // if (_StageNumber != 1)
        // {
        //     chessBowls.Shuffle();
        // }
        
        foreach(var chessbowl in chessBowls)
        {
            Bowl existingBowl = _puzzles.FirstOrDefault(b => b.letter == chessbowl);
            if (existingBowl != null)
            {
                existingBowl.count++; // 存在则数量+1
            }
            else
            {
                _puzzles.Add(new Bowl
                {
                    id = "b_" + Guid.NewGuid().ToString("N")[..8],
                    letter = chessbowl ,
                    status = 0,
                    count = 1,
                    totalcount = 1,
                    pinyin = WordVocabularyManager.Instance.GetCharPinyin(chessbowl)
                });
            }
         
        }
        
        // 处理光标
        if(!string.IsNullOrEmpty(stageConf.cursor))
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
    
    // public void CreatePupaData()
    // {
    //     bool canCreate = ButterfliesManager.Instance.CanObtainedPupa();
    //     if (canCreate&&_pupaData==null)
    //     {
    //         PhraseGroup curPhraseGroup = _phraseGroups[Random.Range(0,_phraseGroups.Count)];
    //         if (curPhraseGroup.chesspieces.Count>0)
    //         {
    //             List<Chesspiece> currentPositions = curPhraseGroup.chesspieces.FindAll((x)=>x.state==TileState.None);
    //             Chesspiece charPosition = currentPositions[Random.Range(0,currentPositions.Count)];
    //             _pupaData = new PupaData()
    //             {
    //                 position = new Vector2Int(charPosition.row, charPosition.col),
    //                 breakProgress = 0,
    //             };
    //         }
    //     }
    // }

    private void GroupWeightSort()
    {
        int vertical = Mathf.FloorToInt((_MaxCol + 1) / 2f);
        int horizontal = Mathf.FloorToInt((_MaxRow + 1) / 2f);
        Debug.Log($"水平 {horizontal} 和 锤子 {vertical}");
        // 分组对应的词
        foreach (var group in _phraseGroups)
        {
            int vcount = 0;
            int hcount = 0;
            foreach(var piece in group.chesspieces)
            {
                if (piece.col >= vertical)
                    vcount++;
                if(piece.row < horizontal)
                    hcount++;
                if (piece.state == TileState.Default)
                    group.weight += 1;
            }
            group.weight += (vcount >= 3 ? 4 : 0);
            group.quadrant = (vcount >= 3 ? 2 : 0) +  (hcount >= 3 ? 1 : 0) + 1;
        }
    }

    #endregion
    
    /// <summary>
    /// 初始化特殊玩法（冰块、花朵）
    /// 必须在棋盘基础结构和动态难度调整完毕后调用
    /// </summary>
    private void InitMechanics()
    {
        int stageId = _StageNumber;
        // ==========================================
        // 🌟 核心拦截：如果 elem 有数据，优先解析固定配置，并绝对跳过算法生成
        // ==========================================
        bool hasTargetFixedMechanics = _StageConf != null && !string.IsNullOrEmpty(_StageConf.elem) && 
                                       (_StageConf.elem.Contains(",3") || _StageConf.elem.Contains(",8") || _StageConf.elem.Contains(",9"));
        if (hasTargetFixedMechanics)
        {
            Debug.Log($"[玩法配置] 关卡 {stageId} 检测到固定玩法数据 (elem): {_StageConf.elem}，切换为固定配置模式。");
            ParseAndApplyElemData(_StageConf.elem);
            RemoveFlowerFromCursorGroup();
            return; // 💥 只有真正配了 3, 8, 9 时，才绝对不再执行下方的算法生成
        }
        if (_StageConf != null && !string.IsNullOrEmpty(_StageConf.elem))
        {
            // 如果有 elem 数据（比如 67,4），但不是冰花机制，先解析它（让其他系统处理类型4），但不 return，允许继续往下走随机冰花算法
            ParseAndApplyElemData(_StageConf.elem);
            Debug.Log($"[玩法配置] 关卡 {stageId} 的 elem ({_StageConf.elem}) 不包含冰花机制，已解析并放行随机算法。");
        }
        
        // ==========================================
        // 1. 冰块玩法生成逻辑
        // ==========================================
        if (ChessStageController.Instance.CheckIceMechanic(stageId, out _, out int iceDegree))
        {
         
            var iceConfig = ChessStageController.Instance.IceConfig;
            int m_ice = iceConfig.Degree.ContainsKey(iceDegree) ? iceConfig.Degree[iceDegree] : 0;
            int n_groups = _phraseGroups.Count;
            int safeCount = n_groups - m_ice;
            Debug.Log($"<color=#FFA500><b>[玩法数据源: 动态算法分配]</b></color> 关卡 {stageId} 触发冰块算法。计算难度级别: <b>{iceDegree}</b>，期望生成冰块: <b>{m_ice}</b>个，安全余量: {safeCount}组。");
            // 规则：若 n-M <= 2，不出现冰块
            if (safeCount > 2 && m_ice > 0)
            {
                // 获取初始光标所在的成语
                var cursorGroups = new HashSet<PhraseGroup>();
                if (_cursor.Count >= 2 && _chessGroup.TryGetValue((_cursor[0], _cursor[1]), out var cg))
                {
                    foreach(var g in cg) cursorGroups.Add(g);
                }

                // 按安全优先级降序排序 (越安全的排在越前面)
                var sortedForIce = _phraseGroups.OrderBy(g => 
                {
                    // 优先级3：孤岛词 (不与其他词组交叉) -> 最高安全权重
                    bool isIsolated = !g.chesspieces.Any(p => IsMultiGroup(p.row, p.col));
                    if (isIsolated) return 1000; 
                    
                    // 优先级2：初始光标所在的词 -> 次高安全权重
                    if (cursorGroups.Contains(g)) return 500;
                    
                    return -g.chesspieces.Count(p => p.state == TileState.Default);
                })
                // 优先级1：初始字数量更多的优先
                // .ThenByDescending(g => g.chesspieces.Count(p => p.state == TileState.Default)) 
                // 优先级0：同等条件随机打乱
                .ThenBy(g => Guid.NewGuid()) 
                .ToList();
                
                List<PhraseGroup> selectedIceGroups = new List<PhraseGroup>();
                // 1. 第一轮贪心筛选：只挑选绝对不交叉的词组
                foreach (var g in sortedForIce)
                {
                    if (selectedIceGroups.Count >= m_ice) break;
                    if (cursorGroups.Contains(g)) continue; 
                    if (!g.chesspieces.Any(p => IsMultiGroup(p.row, p.col))) continue; // 孤岛词
                    // 检查是否与已选中的词组交叉（不与已选词组共享格子）
                    bool intersects = selectedIceGroups.Any(sel =>
                        g.chesspieces.Any(p => sel.chesspieces.Any(sp => sp.row == p.row && sp.col == p.col)));
                    if (!intersects)
                        selectedIceGroups.Add(g);
                    
                    // bool isCrossWithCursor = g.chesspieces.Any(p => cursorGroups.Any(cg => cg.chesspieces.Any(cp => cp.row == p.row && cp.col == p.col)));
                    // if (isCrossWithCursor) continue; // 如果和光标组有任何交叉，直接跳过，不给它盖冰块

                    // bool hasIntersection = false;
                    // // 拿当前成语去跟已经选中的成语逐一比对坐标
                    // foreach (var selected in selectedIceGroups)
                    // {
                    //     // 如果有任何一格的行和列完全重合，说明这两个成语交叉了
                    //     if (g.chesspieces.Any(p => selected.chesspieces.Any(sp => sp.row == p.row && sp.col == p.col)))
                    //     {
                    //         hasIntersection = true;
                    //         break;
                    //     }
                    // }
                    //
                    // // 只有完全不交叉的成语，才被选入冰块阵营
                    // if (!hasIntersection)
                    // {
                    //     selectedIceGroups.Add(g);
                    // }
                }
                // 2. 🌟 容错兜底：如果关卡太密（比如一共就3个成语挤在一起），第一轮选不够 m_ice 个，
                // 那么第二轮放宽条件，允许交叉，把数量补齐，绝对不让游戏崩溃或少生成冰块
                if (selectedIceGroups.Count < m_ice)
                {
                    foreach (var g in sortedForIce)
                    {
                        if (selectedIceGroups.Count >= m_ice) break;
                        if (cursorGroups.Contains(g)) continue;
                        if (!g.chesspieces.Any(p => IsMultiGroup(p.row, p.col))) continue;
                        if (!selectedIceGroups.Contains(g))
                        {
                            selectedIceGroups.Add(g);
                        }
                    }
                }
                // 3. 第三步：对最终敲定的这几组词全面覆盖冰块
                foreach (var iceGroup in selectedIceGroups)
                {
                    foreach (var p in iceGroup.chesspieces)
                    {
                        // 同步击穿修改所有交叉实例（防止阶段4渲染丢失）
                        if (_chessGroup.TryGetValue((p.row, p.col), out var crossGroups))
                        {
                            foreach (var cg1 in crossGroups)
                            {
                                var targetPiece = cg1.chesspieces.FirstOrDefault(cp => cp.row == p.row && cp.col == p.col);
                                if (targetPiece != null) targetPiece.hasIce = true;
                            }
                        }
                    
                        // 同步更新主唯一源
                        var hashPiece = _chesspiece.FirstOrDefault(cp => cp.row == p.row && cp.col == p.col);
                        if (hashPiece != null) hashPiece.hasIce = true;
                    }
                }
                // 🌟 [加在冰块生成逻辑大括号结束前的最后一行]
                string icePieces = string.Join(", ", _chesspiece.Where(p => p.hasIce).Select(p => $"({p.row},{p.col})-{p.letter}"));
                Debug.Log($"<color=#00FF00>[冰块生成-算法结束]</color> 关卡:{stageId} | 来源:<color=#FFA500>算法随机分配</color> | 难度:{iceDegree} | 目标:{m_ice}个 | 实际激活: [{icePieces}] ");
            }
            else if (m_ice > 0)
            {
                // 🌟 加上提示，让你在 Unity 控制台能直接看到原因！
                Debug.Log($"<color=#FF0000>[冰块未生成]</color> 关卡:{stageId} | 词组总数 {n_groups} - 冰块数 {m_ice} = {safeCount} (<=2)。触发保护规则，不生成！");
            }
        }
        // ==========================================
        // 2. 花朵玩法生成逻辑
        // ==========================================
        if (ChessStageController.Instance.CheckFlowerMechanic(stageId, out _, out int flowerDegree))
        {
            var flowerConfig = ChessStageController.Instance.FlowerConfig;
            int m_flower = flowerConfig.Degree.ContainsKey(flowerDegree) ? flowerConfig.Degree[flowerDegree] : 0;
            var defaultTiles = _chesspiece.Where(p => p.state == TileState.Default).ToList();
            int n_chars = defaultTiles.Count;
            Debug.Log($"<color=#FFA500><b>[玩法数据源: 动态算法分配]</b></color> 关卡 {stageId} 触发花朵算法。计算难度级别: <b>{flowerDegree}</b>，期望生成花朵: <b>{m_flower}</b>个，当前全盘初始字: {n_chars}个。");
            // 规则：若初始字 n - m <= 3，不出现花骨朵
            if (n_chars - m_flower > 3 && m_flower > 0)
            {
                // 获取初始光标组
                var cursorGroups = new HashSet<PhraseGroup>();
                if (_cursor.Count >= 2 && _chessGroup.TryGetValue((_cursor[0], _cursor[1]), out var cg))
                {
                    foreach(var g in cg) cursorGroups.Add(g);
                }
                var forbiddenCoordinates = new HashSet<(int row, int col)>();
                foreach (var cg2 in cursorGroups)
                {
                    foreach (var cp2 in cg2.chesspieces)
                    {
                        forbiddenCoordinates.Add((cp2.row, cp2.col));
                    }
                }
                // 筛选候选成语：排除光标所在的成语，并按初始字数量升序 (初始字少的优先)
                var flowerCandidates = _phraseGroups
                    .Where(g => !cursorGroups.Contains(g))
                    .OrderBy(g => g.chesspieces.Count(p => p.state == TileState.Default))
                    .ToList();

                // 规则：如果是固定关卡配置，主动抹除光标组身上可能已有的花朵 (容错)
                foreach(var g in cursorGroups) {
                    foreach(var p in g.chesspieces) p.hasFlower = false;
                }

                int placedFlowers = 0;
                foreach (var g in flowerCandidates)
                {
                    foreach (var p in g.chesspieces)
                    {
                        if (p.state == TileState.Default && !p.hasFlower && !forbiddenCoordinates.Contains((p.row, p.col)))
                        {
                            if (_chessGroup.TryGetValue((p.row, p.col), out var crossGroups))
                            {
                                foreach (var crossG in crossGroups)
                                {
                                    var targetPiece = crossG.chesspieces.FirstOrDefault(cp => cp.row == p.row && cp.col == p.col);
                                    if (targetPiece != null) targetPiece.hasFlower = true;
                                }
                            }
                            // 🌟【核心修复】：同步更新主数据源 _chesspiece，确保能通过校验成功存入存档并渲染
                            var hashPiece = _chesspiece.FirstOrDefault(cp => cp.row == p.row && cp.col == p.col);
                            if (hashPiece != null) hashPiece.hasFlower = true;
                   
                            placedFlowers++;
                            if (placedFlowers >= m_flower) break;
                        }
                    }
                    if (placedFlowers >= m_flower) break;
                }
                // 🌟 [加在花朵生成逻辑大括号结束前的最后一行]
                string flowerPieces = string.Join(", ", _chesspiece.Where(p => p.hasFlower).Select(p => $"({p.row},{p.col})-{p.letter}"));
                Debug.Log($"<color=#00FF00>[花朵生成-算法结束]</color> 关卡:{stageId} | 来源:<color=#FFA500>算法随机分配</color> | 难度:{flowerDegree} | 目标:{m_flower}个 | 实际激活: [{flowerPieces}] ");
            }
            else if (m_flower > 0)
            {
                // 🌟 加上提示，让你在 Unity 控制台能直接看到原因！
                Debug.LogWarning($"<color=#FF0000>[花朵未生成]</color> 关卡:{stageId} | 初始字数 {n_chars} - 需生成数 {m_flower} = {n_chars - m_flower} (<=3)。触发保护规则，不生成！");
            }
        }
        RemoveFlowerFromCursorGroup();
    }
    
    /// <summary>
    /// 🌟 新增辅助方法：精准解构并应用格式为 "41,8#62,3" 的固定玩法数据
    /// </summary>
    private void ParseAndApplyElemData(string elemStr)
    {
        // 1. 使用 '#' 拆分出独立的格子数据块
        string[] items = elemStr.Split('#', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (string item in items)
        {
            // 2. 使用 ',' 拆分坐标与类型
            string[] parts = item.Split(',');
            if (parts.Length < 2) continue;

            string coordStr = parts[0].Trim();
            if (coordStr.Length < 2) continue;

            // 3. 严格遵循 pass 的双位数解析约定：第一位为行(r)，第二位为列(c)
            int row = int.Parse(coordStr[0].ToString());
            int col = int.Parse(coordStr[1].ToString());
            if (!int.TryParse(parts[1], out int mechanicType)) continue;

            // 4. 点对点检索棋盘中的具体字块对象进行状态写入
         
            if (_chessGroup.TryGetValue((row, col), out var groups))
            {
                bool isApplied = false;
                foreach (var group in groups)
                {
                    // 找到每个词组中属于这个坐标的字块实例
                    var piece = group.chesspieces.FirstOrDefault(p => p.row == row && p.col == col);
                    if (piece != null)
                    {
                        if ((mechanicType is 8 or 9) && ChessStageController.Instance.IceConfig != null && ChessStageController.Instance.IceConfig.IsOpen)
                        {
                            piece.hasIce = true;
                            isApplied = true;
                        }
                        else if (mechanicType == 3 && ChessStageController.Instance.FlowerConfig != null && ChessStageController.Instance.FlowerConfig.IsOpen)
                        {
                            piece.hasFlower = true;
                            isApplied = true;
                        }
                    }
                }
                // 兜底同步修改 _chesspiece 里的实例（保持全盘数据一致性）
                var hashPiece = _chesspiece.FirstOrDefault(p => p.row == row && p.col == col);
                if (hashPiece != null)
                {
                    if (mechanicType == 8 && isApplied) hashPiece.hasIce = true;
                    if (mechanicType == 3 && isApplied) hashPiece.hasFlower = true;
                }
                if (isApplied)
                {
                    Debug.Log($"[固定玩法注入] 坐标 ({row}, {col}) 成功覆盖！类型代码: {mechanicType}");
                }
                else
                {
                    Debug.Log($"[固定玩法拦截] 坐标 ({row}, {col}) 玩法开关未开启，已忽略。");
                }
            }
            else
            {
                Debug.LogWarning($"[固定玩法失效] 配置了坐标 ({row}, {col})，但当前棋盘网格中不存在该格子！");
            }
        }

    }
    
    /// <summary>
    /// 🌟 强制安全校验：剔除初始光标所在词组的所有花朵
    /// </summary>
    private void RemoveFlowerFromCursorGroup()
    {
        // 1. 确保光标坐标有效，并获取光标所在的所有词组
        if (_cursor.Count >= 2 && _chessGroup.TryGetValue((_cursor[0], _cursor[1]), out var cursorGroups))
        {
            bool hasCleaned = false;
            foreach (var group in cursorGroups)
            {
                foreach (var p in group.chesspieces)
                {
                    if (p.hasFlower)
                    {
                        p.hasFlower = false; // 清除组内配置
                        
                        // 🌟 核心：必须同步清理全局主集合中的数据，防止渲染和存档残留！
                        var hashPiece = _chesspiece.FirstOrDefault(cp => cp.row == p.row && cp.col == p.col);
                        if (hashPiece != null) hashPiece.hasFlower = false;
                        
                        hasCleaned = true;
                    }
                    if (p.hasIce)
                    {
                        p.hasIce = false; // 清除组内配置
                        
                        // 🌟 核心：必须同步清理全局主集合中的数据，防止渲染和存档残留！
                        var hashPiece = _chesspiece.FirstOrDefault(cp => cp.row == p.row && cp.col == p.col);
                        if (hashPiece != null) hashPiece.hasIce = false;
                        
                        hasCleaned = true;
                    }
                }
            }
            if (hasCleaned)
            {
                Debug.Log($"[机制安全拦截] 初始光标({_cursor[0]},{_cursor[1]})所在词组存在花朵与冰块，已强制剔除以防卡死！");
            }
        }
    }
}
