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
        
        if (_cursor.Count < 2)
        {
            RegenerateCursorPosition();
            
            // 极限兜底：如果象限算法因为满盘或其他异常没能生成光标，使用权重排位兜底
            if (_cursor.Count < 2)
            {
                GroupWeightSort();
                Chesspiece cpp = FindMinRowNonePiece();
                if (cpp != null)
                {
                    this._cursor.Add(cpp.row);
                    this._cursor.Add(cpp.col);
                }
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
            Debug.Log($"<color=#FF4500>[动态难度-特殊拦截]</color> 关卡 {_StageNumber} 包含冰/花/树叶/固定机制，本次放弃动态干预，字数保持原设。");
            // 即使不做难度调整，依然建议执行一次基础数据终态校准，防止配置源文件本身存在全显词组或数量不一致
            VerifyAndSyncPuzzles();
            return; // 💥 直接中断，不往下走任何加减字的逻辑
        }
        
        if (wordCount > 0)   // 变简单，增加可见字
        {
            if (ChessDynamicHardManager.Instance.GetHardMode(_StageNumber) == 1)
            {
                Debug.Log($"<color=#00FF00>[动态难度-执行动作]</color> 进入 <b>小幅度简单</b> 模式，准备点亮字数: {wordCount}");
                IncreaseShowWord1(wordCount);
            }
            else
            {
                Debug.Log($"<color=#00FF00>[动态难度-执行动作]</color> 进入 <b>大幅度简单</b> 模式，准备点亮交叉字数: {wordCount}");
                IncreaseShowWord2(wordCount);
            }
        }
        else // 变难, 减少可见字
        {   
            Debug.Log($"<color=#FF8C00>[动态难度-执行动作]</color> 进入 <b>变难</b> 模式，准备隐藏字数: {Mathf.Abs(wordCount)}");
            DecreaseShowWord(wordCount);
        }
        
        // RegenerateCursorPosition();
        VerifyAndSyncPuzzles();
    }
    /// <summary>
    /// 校验关卡数据终态：
    /// 1. 检查是否有词组全部为 Default (全显示)，如果有，强制隐藏其中一个。
    /// 2. 严格校验棋盘上的隐藏字(None)与字盘(_puzzles)的数量是否一致，多退少补。
    /// </summary>
    public void VerifyAndSyncPuzzles()
    {
        System.Text.StringBuilder logSb = new System.Text.StringBuilder();
        bool hasModifications = false; // 记录是否发生了纠正动作
        logSb.AppendLine("<color=#00FFFF>[动态难度-安全校验]</color> 开始执行终态数据对齐与字盘(Bowl)同步...");
        
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
                SyncPieceState(targetToHide.row, targetToHide.col, TileState.None);
                logSb.AppendLine($"  <color=#FF8C00>[触发底线保护]</color> 发现全显词组 <b>{JsonConvert.SerializeObject(group.chesspieces)}</b>，已强制隐藏字块: <color=#FFD700>'{targetToHide.letter}'</color> 坐标:({targetToHide.row}, {targetToHide.col})");
                hasModifications = true;
            }
        }

        // ==========================================
        // 2. 统计棋盘上真实需要的隐藏字 (state == None)
        // ==========================================
        Dictionary<string, int> requiredCounts = new Dictionary<string, int>();
        int totalRequired = 0;
        foreach (var piece in _chesspiece)
        {
            if (piece.state is TileState.None or TileState.Check)
            {
                if (!requiredCounts.ContainsKey(piece.letter))
                    requiredCounts[piece.letter] = 0;
                
                requiredCounts[piece.letter]++;
                totalRequired++;
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
                    existingBowl.totalcount = reqCount;
                    logSb.AppendLine($"  <color=#00FF00>[字盘增补]</color> 汉字 <color=#FFD700>'{letter}'</color> 存量不足，数量调整: <b>{currCount} -> {reqCount}</b>");
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
                    logSb.AppendLine($"  <color=#00FF00>[字盘新增]</color> 汉字 <color=#FFD700>'{letter}'</color> 完全缺失，已生成新字块，需求数量: <b>{reqCount}</b>");
                }
                hasModifications = true;
            }
        }

        // 4.2 移除多余的字
        List<Bowl> bowlsToRemove = new List<Bowl>();
        foreach (var bowl in _puzzles)
        {
            int reqCount = requiredCounts.ContainsKey(bowl.letter) ? requiredCounts[bowl.letter] : 0;

            if (bowl.count > reqCount)
            {
                int oldCount = bowl.count;
                bowl.count = reqCount;
                bowl.totalcount = reqCount;
                if (bowl.count <= 0)
                {
                    bowlsToRemove.Add(bowl); // 记录需要彻底删除的 Bowl
                    logSb.AppendLine($"  <color=#FF8C00>[字盘剔除]</color> 汉字 <color=#FFD700>'{bowl.letter}'</color> 棋盘已不需要，彻底移除。");
                }
                else
                {
                    logSb.AppendLine($"  <color=#FF8C00>[字盘扣减]</color> 汉字 <color=#FFD700>'{bowl.letter}'</color> 数量冗余，数量调整: <b>{oldCount} -> {reqCount}</b>");
                }
                hasModifications = true;
            }
        }
        foreach(var b in bowlsToRemove) _puzzles.Remove(b);
        
        if (hasModifications)
        {
            logSb.AppendLine($"<color=#00FFFF>[动态难度-安全校验结束]</color> 字盘纠正执行完毕。最终棋盘待填挖空总数: <color=#FFD700>{totalRequired}</color>");
            Debug.Log(logSb.ToString());
        }
        else
        {
            Debug.Log($"<color=#00FFFF>[动态难度-安全校验]</color> 完美对齐，无任何异常。最终棋盘待填挖空总数: <color=#FFD700>{totalRequired}</color>");
        }
    }
    
    /// <summary>
    /// 增加可见字，小幅度简单 (减少待填的空格)
    /// </summary>
    /// <param name="number">计划增加的可见字数量 (正数)</param>
    private void IncreaseShowWord1(int number)
    {
        int originalTarget = number; // 记录计划增加的数量，用于排查日志
        var changedPieces = new List<Chesspiece>();

        // ==========================================
        // 【规则1】：获取并保护初始光标所在的词组 (一定不减字/不操作)
        // ==========================================
        // var protectedGroups = new HashSet<PhraseGroup>();
        // if (_cursor != null && _cursor.Count >= 2)
        // {
        //     // 获取光标所在的坐标，若有对应词组，将其全部加入保护名单
        //     if (_chessGroup.TryGetValue((_cursor[0], _cursor[1]), out var cursorGroups))
        //     {
        //         foreach (var cg in cursorGroups)
        //         {
        //             protectedGroups.Add(cg);
        //         }
        //     }
        // }
        var protectedGroups = GetProtectedCursorGroups();
        while (number > 0)
        {
            // ==========================================
            // 规则1 & 规则3 预处理：
            // 1. 剔除受光标保护的词组
            // 2. 必须包含至少一个“待填(None)”且“非交叉”的字，才具备操作资格
            // ==========================================
            var validGroups = _phraseGroups.Where(g => 
                !protectedGroups.Contains(g) && !IsSimpleLShapeGroup(g) &&
                g.chesspieces.Count(p => p.state == TileState.None) >= 2 &&
                g.chesspieces.Any(p => p.state == TileState.None && !IsMultiGroup(p.row, p.col))
            ).ToList();

            
            if (validGroups.Count == 0)
            {
                Debug.Log("[动态难度] 没有符合规则的词组可供增加显示字 (或剩余待填字皆为交叉字/皆受光标保护)，提前结束。");
                break;
            }

            // ==========================================
            // 规则2：待填字(None)最少的词里随机选一个
            // ==========================================
            // 计算当前所有合法词组中，包含 None 状态最少的数量
            int minNoneCount = validGroups.Min(g => g.chesspieces.Count(p => p.state == TileState.None));
            // 筛选出所有拥有这个最小数量的词组
            var minNoneGroups = validGroups.Where(g => g.chesspieces.Count(p => p.state == TileState.None) == minNoneCount).ToList();
            // 随机选中一个词组
            PhraseGroup selectedGroup = minNoneGroups[UnityEngine.Random.Range(0, minNoneGroups.Count)];

            // ==========================================
            // 规则4：选好词组后，优先在组内从后向前选择 (4号位优先，1号位最后)
            // ==========================================
            Chesspiece targetPiece = null;
            // 从尾到头倒序遍历
            for (int i = selectedGroup.chesspieces.Count - 1; i >= 0; i--)
            {
                var cp = selectedGroup.chesspieces[i];
                
                // ==========================================
                // 规则3：不加交叉字，遇到交叉字跳过
                // ==========================================
                if (cp.state == TileState.None && !IsMultiGroup(cp.row, cp.col))
                {
                    targetPiece = cp; // 找到了倒数第一个符合条件的待填非交叉字
                    break;
                }
            }

            if (targetPiece != null)
            {
                SyncPieceState(targetPiece.row, targetPiece.col, TileState.Default);
                // 3. 记录日志与计数
                changedPieces.Add(targetPiece);
                number--; 
            }
            else
            {
                // 理论上有 validGroups 的前置筛选，不应该走到这里，作为安全兜底
                Debug.LogWarning("[动态难度] 异常：选中词组中未能找到可点亮的字！");
                break; 
            }
        }

        // ==========================================
        // 【排查打印】：详细输出本次难度调整的操作明细
        // ==========================================
        if (changedPieces.Count > 0)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"<color=#00FF00>[动态难度-小幅变简单]</color> 增加可见字执行完毕！计划点亮: <b>{originalTarget}</b> 个，实际点亮: <b>{changedPieces.Count}</b> 个。");
            sb.AppendLine("被点亮(变为初始字)的字块明细如下：");
            
            for (int i = 0; i < changedPieces.Count; i++)
            {
                var p = changedPieces[i];
                sb.AppendLine($"  {i + 1}. 汉字: <color=#00FF00>'{p.letter}'</color> | 坐标: (行: {p.row}, 列: {p.col}) | 格子ID: {p.id}");
            }
            
            Debug.Log(sb.ToString());
        }
        else
        {
            Debug.Log($"<color=#00FF00>[动态难度-小幅变简单]</color> 执行完毕！本次未能点亮任何字 (可能是棋盘已满或受光标/交叉限制)。");
        }
    }

 /// <summary>
    /// 增加可见字，大幅度简单 (减少待填空格，优先点亮交叉字)
    /// </summary>
    /// <param name="number">计划增加的可见字数量 (正数)</param>
    private void IncreaseShowWord2(int number)
    {
        int originalTarget = number; // 记录计划增加的数量，用于排查日志
        var changedPieces = new List<Chesspiece>();
        var litSet = new HashSet<Chesspiece>(); // 防止同一个字被重复点亮
        
        // ==========================================
        // 【规则2】：获取并保护初始光标所在的词组 (一定不减字/不操作)
        // ==========================================
        // var protectedGroups = new HashSet<PhraseGroup>();
        // if (_cursor != null && _cursor.Count >= 2)
        // {
        //     if (_chessGroup.TryGetValue((_cursor[0], _cursor[1]), out var cursorGroups))
        //     {
        //         foreach (var cg in cursorGroups) protectedGroups.Add(cg);
        //     }
        // }
        var protectedGroups = GetProtectedCursorGroups();
        // 辅助函数：判断某个字是否符合【规则3】(交叉字 且 另一个词初始字数量 != 3)
        bool IsValidCrossPiece(Chesspiece cp, PhraseGroup currentGroup)
        {
            if (cp.state != TileState.None) return false;
            if (!IsMultiGroup(cp.row, cp.col)) return false; // 必须是交叉字
            if (litSet.Any(lit => lit.row == cp.row && lit.col == cp.col)) return false;
            // 👇 新增：确保当前组点亮该交叉字后，不会变成全显 (即点亮前空格必须 >= 2)
            if (currentGroup.chesspieces.Count(p => p.state == TileState.None) < 2) return false;
            // 检查与之交叉的“其他词组”
            if (_chessGroup.TryGetValue((cp.row, cp.col), out var crossGroups))
            {
                foreach (var otherGroup in crossGroups)
                {
                    if (otherGroup == currentGroup) continue; // 排除自己
                    if (otherGroup.chesspieces.Count(p => p.state == TileState.None) < 2) return false;
                    // 统计另一个词当前的初始可见字数量
                    int defaultCount = otherGroup.chesspieces.Count(p => p.state == TileState.Default);
                    if (defaultCount == 3) 
                    {
                        return false; // 如果另一个词初始字数量等于3，则否决该交叉字
                    }
                }
            }
            return true;
        }

        while (number > 0)
        {
            bool isFallbackMode = false; // 是否触发了降级兜底模式

            // ==========================================
            // 【规则1 & 规则3】预处理：筛选出有资格操作的词组
            // ==========================================
            var validGroups = _phraseGroups.Where(g => 
                !protectedGroups.Contains(g) && 
                !IsSimpleLShapeGroup(g) &&  
                g.chesspieces.Any(p => IsValidCrossPiece(p, g)) // 必须包含至少一个符合规则3的交叉字
            ).ToList();

            // 🌟 补充方案：如果严格符合规则的交叉字被耗尽了，启动降级模式找普通空格，防止死锁
            if (validGroups.Count == 0)
            {
                validGroups = _phraseGroups.Where(g => 
                    !protectedGroups.Contains(g) && 
                    g.chesspieces.Count(p => p.state == TileState.None) >= 2 &&
                    g.chesspieces.Any(p => p.state == TileState.None && !IsMultiGroup(p.row, p.col) // 找普通的待填字
                    && !litSet.Any(lit => lit.row == p.row && lit.col == p.col))
                ).ToList();

                if (validGroups.Count == 0)
                {
                    Debug.Log("[动态难度] 棋盘已满或所有待填字皆受光标保护，提前结束大幅变简单。");
                    break;
                }
                isFallbackMode = true; // 标记进入兜底模式
            }

            // ==========================================
            // 【规则1】：待填字最多的词里随机选一个
            // ==========================================
            // 找到包含最多 None 的数量
            int maxNoneCount = validGroups.Max(g => g.chesspieces.Count(p => p.state == TileState.None));
            // 筛选出所有拥有此最大值的词组
            var maxNoneGroups = validGroups.Where(g => g.chesspieces.Count(p => p.state == TileState.None) == maxNoneCount).ToList();
            // 随机选中一个词组
            PhraseGroup selectedGroup = maxNoneGroups[UnityEngine.Random.Range(0, maxNoneGroups.Count)];

            // ==========================================
            // 【规则4】：优先选取首位，从1号位到末尾选取 (正序遍历)
            // ==========================================
            Chesspiece targetPiece = null;
            for (int i = 0; i < selectedGroup.chesspieces.Count; i++)
            {
                var cp = selectedGroup.chesspieces[i];
                
                if (isFallbackMode)
                {
                    // 兜底模式下，只要是个非交叉的空格就行
                    if (cp.state == TileState.None && !IsMultiGroup(cp.row, cp.col))
                    {
                        targetPiece = cp;
                        break;
                    }
                }
                else
                {
                    // 严格模式下，校验【规则3】
                    if (IsValidCrossPiece(cp, selectedGroup))
                    {
                        targetPiece = cp;
                        break;
                    }
                }
            }

            if (targetPiece != null)
            {
              
                SyncPieceState(targetPiece.row, targetPiece.col, TileState.Default);
                litSet.Add(targetPiece); // 👇 新增：切实记录到集合中
                // 3. 记录日志与计数
                changedPieces.Add(targetPiece);
                number--; 
            }
            else
            {
                Debug.LogWarning("[动态难度] 异常：选中词组中未能找到可点亮的字！");
                break; 
            }
        }

        // ==========================================
        // 【排查打印】：详细输出大幅变简单的操作明细
        // ==========================================
        if (changedPieces.Count > 0)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"<color=#00FFFF>[动态难度-大幅变简单]</color> 执行完毕！计划点亮: <b>{originalTarget}</b> 个，实际点亮: <b>{changedPieces.Count}</b> 个。");
            sb.AppendLine("被点亮的字块明细如下：");
            
            for (int i = 0; i < changedPieces.Count; i++)
            {
                var p = changedPieces[i];
                string crossInfo = IsMultiGroup(p.row, p.col) ? "<color=#FFA500>(交叉字)</color>" : "<color=#808080>(普通字-兜底降级)</color>";
                sb.AppendLine($"  {i + 1}. 汉字: <color=#00FFFF>'{p.letter}'</color> {crossInfo} | 坐标: ({p.row}, {p.col}) | ID: {p.id}");
            }
            
            Debug.Log(sb.ToString());
        }
        else
        {
            Debug.Log($"<color=#00FFFF>[动态难度-大幅变简单]</color> 执行完毕！本次未能点亮任何字。");
        }
        
        // (注：后续的字盘整理(Bowl逻辑)交由统一的 VerifyAndSyncPuzzles() 自动执行多退少补)
    }
    
   /// <summary>
    /// 减少可见字 (增加难度)
    /// </summary>
    /// <param name="number">数量 (负数)</param>
    private void DecreaseShowWord(int number)
    {
        int originalTarget = Mathf.Abs(number); // 记录计划减少的数量，用于排查日志
        int minShow = 3; // 整个棋盘最少留 3 个显示字
        int maxBowlSlots = 32;// 👇 修改：明确这是 Bowl UI 的槽位上
        var changedPieces = new List<Chesspiece>();
        var hiddenSet = new HashSet<Chesspiece>(); // 🔧 防止同一个字被反复隐藏

        // ==========================================
        // 【规则】：获取并保护初始光标所在的词组
        // ==========================================
        // var protectedGroups = new HashSet<PhraseGroup>();
        // if (_cursor != null && _cursor.Count >= 2)
        // {
        //     // 如果光标坐标有对应的词组，将其全部加入保护名单
        //     if (_chessGroup.TryGetValue((_cursor[0], _cursor[1]), out var cursorGroups))
        //     {
        //         foreach (var cg in cursorGroups)
        //         {
        //             protectedGroups.Add(cg);
        //         }
        //     }
        // }
        var protectedGroups = GetProtectedCursorGroups();
        // number 是负数，每次成功隐藏一个字就 number++，直到完成指定数量
        while (number < 0)
        {
            if (ShowCount() <= minShow)
            {
                Debug.Log($"[动态难度] 当前可见字已达到最低安全底线({minShow}个)，停止增加难度。");
                break;
            }
            // 👇 修改：使用去重后的汉字种类数来判断是否溢出屏幕
            if (UniqueNoneLetterCount() >= maxBowlSlots)
            {
                Debug.Log($"<color=#FF0000>[动态难度-UI保护]</color> 底部字盘所需种类已达上限({maxBowlSlots}种)，强制停止隐藏以防 UI 溢出。");
                break;
            }
            // ==========================================
            // 规则1 & 规则3 & 光标保护：
            // 1. 过滤掉受光标保护的词组
            // 2. 过滤出“至少包含一个非交叉的可见字(Default)”的词组。
            // ==========================================
            var validGroups = _phraseGroups.Where(g => 
                !protectedGroups.Contains(g) && // 核心：如果是光标所在的组，直接一票否决
                g.chesspieces.Any(p => p.state == TileState.Default && !IsMultiGroup(p.row, p.col) 
                                                                    && !hiddenSet.Any(h => h.row == p.row && h.col == p.col) && !IsSimpleLShapeGroup(g))
            ).ToList();

            if (validGroups.Count == 0)
            {
                Debug.Log("[动态难度] 没有符合规则的词组可供减少显示字 (或剩余词组均受光标保护)，提前结束。");
                break;
            }

            // ==========================================
            // 规则2：待填字(None)最少的词里随机选一个
            // ==========================================
            int minNoneCount = validGroups.Min(g => g.chesspieces.Count(p => p.state == TileState.None));
            var minNoneGroups = validGroups.Where(g => g.chesspieces.Count(p => p.state == TileState.None) == minNoneCount).ToList();
            PhraseGroup selectedGroup = minNoneGroups[UnityEngine.Random.Range(0, minNoneGroups.Count)];

            // ==========================================
            // 规则4：选好词组后，优先在组内从后向前选择 (4号位优先，1号位最后)
            // ==========================================
            Chesspiece targetPiece = null;
            // 倒序遍历：i 从 Count - 1 递减到 0
            for (int i = selectedGroup.chesspieces.Count - 1; i >= 0; i--)
            {
                var cp = selectedGroup.chesspieces[i];
                
                // ==========================================
                // 规则3：不删交叉字，遇到交叉字跳过
                // ==========================================
                if (cp.state == TileState.Default && !IsMultiGroup(cp.row, cp.col) 
                                                  && !hiddenSet.Any(h => h.row == cp.row && h.col == cp.col))
                {
                    targetPiece = cp; // 找到了符合条件的倒数第一个非交叉可见字
                    break;
                }
            }

            if (targetPiece != null)
            {
                SyncPieceState(targetPiece.row, targetPiece.col, TileState.None);
                hiddenSet.Add(targetPiece);
                changedPieces.Add(targetPiece);
                number++; 
            }
            else
            {
                Debug.LogWarning("[动态难度] 异常：选中词组中未能找到可隐藏的字！");
                break; 
            }
        }
        
        // ==========================================
        // 【新增】：详细的控制台排查打印
        // ==========================================
        if (changedPieces.Count > 0)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"<color=#FF8C00>[动态难度-变难隐藏]</color> 减少可见字执行完毕！计划隐藏: <b>{originalTarget}</b> 个，实际隐藏: <b>{changedPieces.Count}</b> 个。");
            sb.AppendLine("被隐藏的字块明细如下：");
            
            for (int i = 0; i < changedPieces.Count; i++)
            {
                var p = changedPieces[i];
                sb.AppendLine($"  {i + 1}. 汉字: <color=#00FF00>'{p.letter}'</color> | 坐标: (行: {p.row}, 列: {p.col}) | 格子ID: {p.id}");
            }
            
            Debug.Log(sb.ToString());
        }
        else
        {
            Debug.Log($"<color=#FF8C00>[动态难度-变难隐藏]</color> 执行完毕！本次未隐藏任何字 (可能是达到了最低显示限制或均受光标保护)。");
        }
    }
    /// <summary>
    /// 获取动态难度中需要保护的光标词组（若光标在交叉字上，只保留空格最少的一个组）
    /// </summary>
    private HashSet<PhraseGroup> GetProtectedCursorGroups()
    {
        var protectedGroups = new HashSet<PhraseGroup>();
        if (_cursor == null || _cursor.Count < 2) return protectedGroups;

        if (_chessGroup.TryGetValue((_cursor[0], _cursor[1]), out var cursorGroups))
        {
            // 如果光标是交叉字（关联多个组），排除空格最多的那个组
            if (cursorGroups.Count > 1)
            {
                // 找出空格最多的组
                var maxNoneGroup = cursorGroups
                    .OrderByDescending(g => g.chesspieces.Count(p => p.state == TileState.None))
                    .First();
                // 把除了它之外的组加入保护列表
                foreach (var g in cursorGroups)
                {
                    if (g != maxNoneGroup) protectedGroups.Add(g);
                }
            }
            else
            {
                // 只有一个组，正常保护
                foreach (var g in cursorGroups) protectedGroups.Add(g);
            }
        }
        return protectedGroups;
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
    
    /// <summary>
    /// 👇 新增：统一同步指定坐标上所有字块实例的状态，解决交叉字多实例导致的逻辑不同步和反复抽中问题
    /// </summary>
    private void SyncPieceState(int row, int col, TileState newState)
    {
        // 1. 同步全盘 HashSet 数据源
        var hashPiece = _chesspiece.FirstOrDefault(p => p.row == row && p.col == col);
        if (hashPiece != null) hashPiece.state = newState;

        // 2. 同步所有包含该坐标的词组中的实例 (重要：修复交叉字两端状态不一致的核心)
        if (_chessGroup.TryGetValue((row, col), out var groups))
        {
            foreach (var g in groups)
            {
                var piece = g.chesspieces.FirstOrDefault(p => p.row == row && p.col == col);
                if (piece != null) piece.state = newState;
            }
        }
    }
    
    /// <summary> 该格子是否被多个词组共享 </summary>
    public bool IsMultiGroup(int r, int c) =>
        _chessGroup.TryGetValue((r, c), out var set) && set.Count > 1;
    
    /// <summary>
    /// 判断一个词组是否为“简单L型”——仅通过唯一交叉字与另一个词组相连，且该交叉字只属于这两个词组
    /// </summary>
    private bool IsSimpleLShapeGroup(PhraseGroup group)
    {
        // 1. 本词组必须恰好有 1 个交叉字
        var crossCells = group.chesspieces.Where(p => IsMultiGroup(p.row, p.col)).ToList();
        if (crossCells.Count != 1) return false;
    
        var crossCell = crossCells[0];
        if (_chessGroup.TryGetValue((crossCell.row, crossCell.col), out var groupsAtCell))
        {
            // 2. 该交叉字必须恰好属于两个词组（本组 + 另一组）
            if (groupsAtCell.Count == 2 && groupsAtCell.Contains(group))
            {
                // 3. 取出另一个词组
                var otherGroup = groupsAtCell.First(g => g != group);
                // 4. 另一个词组也必须只有这一个交叉字（即当前格子）
                int otherCrossCount = otherGroup.chesspieces.Count(p => IsMultiGroup(p.row, p.col));
                return otherCrossCount == 1;
            }
        }
        return false;
    }
    
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
    // 👇 新增：获取当前棋盘上待填的【不同汉字的种类数】 (完美对应底部 Bowl UI 的实际占用槽位数)
    private int UniqueNoneLetterCount() =>
        _chesspiece.Where(p => p.state == TileState.None).Select(p => p.letter).Distinct().Count();
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
  
        var representative = sortedGroups[0].chesspieces
                    .Where(p => p.state == TileState.None)
                    .OrderBy(p => p.row)   // 组内 row 最小
                    .ThenByDescending(p=>p.col)    // 组内col最大
                    .FirstOrDefault();     // 本组代表
        
        Debug.Log("看看所有组的分数: " + JsonConvert.SerializeObject(sortedGroups));
        return representative;
    }
    
    /// <summary>
    /// 【象限空格数算法】：仅在关卡配置没有初始光标时触发，用于寻找最佳开局光标位置。
    /// </summary>
    public void RegenerateCursorPosition()
    {
        // 节点：是否有定义初始光标 -> 有 -> 受动态难度影响 -> 废除当前光标
        _cursor.Clear();

        // 计算矩阵的中心分割线 (结合原代码中的象限逻辑)
        // row 实际上代表 X 轴（横坐标，从左向右递增）。
        // col 实际上代表 Y 轴（纵坐标，从下向上递增）。
        // 上方（一二象限）即 row < horizontal
        // 左侧（二三象限）即 col < vertical
        int horizontal = Mathf.FloorToInt((_MaxRow + 1) / 2f); 
        int vertical = Mathf.FloorToInt((_MaxCol + 1) / 2f);   

        // 局部辅助函数：获取词组中所有空格(None)的数量
        int GetEmptyCount(PhraseGroup g) => g.chesspieces.Count(p => p.state == TileState.None);
        // 局部辅助函数：获取词组中首个空格的索引（找不到返回-1）
        int GetFirstEmptyIndex(PhraseGroup g) => g.chesspieces.FindIndex(p => p.state == TileState.None);

        // 局部辅助函数：处理 Noah 批注的特殊情况（如果词跨象限，至少一半落在一、二象限）
        bool IsInTopHalf(PhraseGroup g)
        {
            if (g.chesspieces.Count == 0) return false;
            if (g.direction == 1) // 1为横向
            {
                // 情况1：词语的所有字高度，整体≤h' (在实际坐标系中，上方即 col >= v_prime)
                return g.chesspieces.All(p => p.col >= vertical);
            }
            else // 0为纵向
            {
                // 情况2：若词语纵向，则需要该成语前两个字的高度≤h'
                if (g.chesspieces.Count >= 2)
                {
                    return g.chesspieces[0].col >= vertical && g.chesspieces[1].col >= vertical;
                }
                else
                {
                    return g.chesspieces.All(p => p.col >= vertical);
                }
            }
        }

        // 节点：从矩阵上方（一二象限）寻找“空格数≤2”的成语（包含 Noah 的特殊情况批注）
        var candidates = _phraseGroups.Where(g => 
            GetEmptyCount(g) > 0 && 
            GetEmptyCount(g) <= 2 && 
            IsInTopHalf(g)
        ).ToList();

        // 节点：如果没有 -> 从矩阵中寻找空格最少的成语
        if (candidates.Count == 0)
        {
            var allWithEmpty = _phraseGroups.Where(g => GetEmptyCount(g) > 0).ToList();
            if (allWithEmpty.Count == 0) return; // 容错：全盘已满，无空格

            int minEmpty = allWithEmpty.Min(g => GetEmptyCount(g));
            candidates = allWithEmpty.Where(g => GetEmptyCount(g) == minEmpty).ToList();
        }

        PhraseGroup selectedGroup = null;

        if (candidates.Count == 1)
        {
            // 节点：只有1个
            selectedGroup = candidates[0];
        }
        else if (candidates.Count > 1)
        {
            // 辅助判断：是否在第二象限 (上方[col >= v_prime] 且 左侧[row < h_prime])
            bool IsInQuadrant2(PhraseGroup g)
            {
                int q2Count = g.chesspieces.Count(p => p.col >= vertical && p.row < horizontal);
                return q2Count >= (g.chesspieces.Count / 2f);
            }
            
            // 节点：有多个 -> 进入层级排序筛选
            var sortedCandidates = candidates.OrderByDescending(g => 
            {
                // 筛选 1：非交叉字的成语优先 (true 优先)
                int firstEmptyIdx = GetFirstEmptyIndex(g);
                var firstPiece = g.chesspieces[firstEmptyIdx];
                bool isNotCross = !IsMultiGroup(firstPiece.row, firstPiece.col);
                return isNotCross ? 1 : 0;
            })
            .ThenByDescending(g => GetFirstEmptyIndex(g)) // 筛选 2：优先首个空格在成语中靠后 (Index越大越靠后)
            .ThenBy(g => GetEmptyCount(g))                // 筛选 3：若有多个，优先空格少的 (升序)
            .ThenByDescending(g => IsInQuadrant2(g) ? 1 : 0) // 筛选 4：优先左侧区域（第二象限）
            .ThenByDescending(g => g.direction == 1 ? 1 : 0) // 筛选 5：优先横向成语 (横向 direction == 1)
            .ToList();

            // 提取排序第一名的所有特征，构建“并列第一”池，以实现节点中的“如果多个，随机一个 / 否则随机纵向”
            var best = sortedCandidates.First();
            int bestFirstIdx = GetFirstEmptyIndex(best);
            bool bestNotCross = !IsMultiGroup(best.chesspieces[bestFirstIdx].row, best.chesspieces[bestFirstIdx].col);
            int bestEmptyCount = GetEmptyCount(best);
            bool bestInQ2 = IsInQuadrant2(best);
            bool bestIsHoriz = best.direction == 1;

            var finalPool = sortedCandidates.Where(g => 
                (!IsMultiGroup(g.chesspieces[GetFirstEmptyIndex(g)].row, g.chesspieces[GetFirstEmptyIndex(g)].col)) == bestNotCross &&
                GetFirstEmptyIndex(g) == bestFirstIdx &&
                GetEmptyCount(g) == bestEmptyCount &&
                IsInQuadrant2(g) == bestInQ2 &&
                (g.direction == 1) == bestIsHoriz
            ).ToList();

            // 随机选取最终赢家
            selectedGroup = finalPool[UnityEngine.Random.Range(0, finalPool.Count)];
        }

        // 节点：确定该成语的首个空格作为光标初始位置
        if (selectedGroup != null)
        {
            int idx = GetFirstEmptyIndex(selectedGroup);
            if (idx != -1)
            {
                var targetPiece = selectedGroup.chesspieces[idx];
                _cursor.Add(targetPiece.row);
                _cursor.Add(targetPiece.col);
                Debug.Log($"[动态难度-光标重置] 遵循流程图选中词组 {selectedGroup.id}，光标位置设为: ({targetPiece.row}, {targetPiece.col})");
            }
        }
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
        if (ChessStageController.Instance.CheckIceMechanic(stageId, out _, out int initialIceDegree))
        {
         
            var iceConfig = ChessStageController.Instance.IceConfig;
            int n_groups = _phraseGroups.Count;
            int currentDegree = initialIceDegree;
            int m_ice = 0;
            int safeCount = 0;
            // 👇 新增：难度降级循环验证
            while (currentDegree >= 0)
            {
                m_ice = iceConfig.Degree.ContainsKey(currentDegree) ? iceConfig.Degree[currentDegree] : 0;
                safeCount = n_groups - m_ice;

                // 规则：若 n-M > 2 且配置数量大于0，则满足条件
                if (safeCount > 2 && m_ice > 0)
                {
                    if (currentDegree != initialIceDegree)
                    {
                        Debug.Log($"<color=#FFA500>[冰块难度降级]</color> 初始难度 {initialIceDegree} 余量不足，已降级至难度 <b>{currentDegree}</b>，生成数量调整为 <b>{m_ice}</b>。");
                    }
                    break; // 满足条件，跳出降级循环
                }
                currentDegree--; // 不满足，尝试降级
            }
            if (currentDegree < 0) m_ice = 0; // 即使降到0也不行，彻底归零
            Debug.Log($"<color=#FFA500><b>[玩法数据源: 冰块动态算法分配]</b></color> 关卡 {stageId} 触发冰块算法。计算难度级别: <b>{currentDegree}</b>(初始:{initialIceDegree})，期望生成冰块: <b>{m_ice}</b>个，安全余量: {safeCount}组。");
    
            // 规则：若 n-M <= 2，不出现冰块
            if (m_ice > 0)
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
                }
                // 2. 容错兜底：如果关卡太密（比如一共就3个成语挤在一起），第一轮选不够 m_ice 个，
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
                Debug.Log($"<color=#00FF00>[冰块生成-算法结束]</color> 关卡:{stageId} | 来源:<color=#FFA500>算法随机分配</color> | 最终难度:{currentDegree} | 目标:{m_ice}个 | 实际激活: [{icePieces}] ");
            }
            else
            {
                // 🌟 加上提示，让你在 Unity 控制台能直接看到原因！
                Debug.Log($"<color=#FF0000>[冰块未生成]</color> 关卡:{stageId} | 词组总数 {n_groups}，即使降至0级也无法满足 safeCount > 2。触发绝对保护规则！");
            }
        }
        // ==========================================
        // 2. 花朵玩法生成逻辑
        // ==========================================
        if (ChessStageController.Instance.CheckFlowerMechanic(stageId, out _, out int initialFlowerDegree))
        {
            var flowerConfig = ChessStageController.Instance.FlowerConfig;
            var defaultTiles = _chesspiece.Where(p => p.state == TileState.Default).ToList();
            int m_flower = 0;
            int n_chars = defaultTiles.Count;
            int currentDegree = initialFlowerDegree;
            // 👇 新增：难度降级循环验证
            while (currentDegree >= 0)
            {
                m_flower = flowerConfig.Degree.ContainsKey(currentDegree) ? flowerConfig.Degree[currentDegree] : 0;
                
                // 规则：若初始字 n - m > 3 且配置数量大于0，则满足条件
                if (n_chars - m_flower > 3 && m_flower > 0)
                {
                    if (currentDegree != initialFlowerDegree)
                    {
                        Debug.Log($"<color=#FFA500>[花朵难度降级]</color> 初始难度 {initialFlowerDegree} 初始字不足，已降级至难度 <b>{currentDegree}</b>，生成数量调整为 <b>{m_flower}</b>。");
                    }
                    break;
                }
                currentDegree--;
            }

            if (currentDegree < 0) m_flower = 0;
            Debug.Log($"<color=#FFA500><b>[玩法数据源: 花朵动态算法分配]</b></color> 关卡 {stageId} 触发花朵算法。计算难度级别: <b>{currentDegree}</b>(初始:{initialFlowerDegree})，期望生成花朵: <b>{m_flower}</b>个，当前全盘初始字: {n_chars}个。");
            
            // 规则：若初始字 n - m <= 3，不出现花骨朵
            if (m_flower > 0)
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
                            // 同步更新主数据源 _chesspiece，确保能通过校验成功存入存档并渲染
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
                Debug.Log($"<color=#00FF00>[花朵生成-算法结束]</color> 关卡:{stageId} | 来源:<color=#FFA500>算法随机分配</color> | 最终难度:{currentDegree} | 目标:{m_flower}个 | 实际激活: [{flowerPieces}] ");
            }
            else 
            {
                // 🌟 加上提示，让你在 Unity 控制台能直接看到原因！
                Debug.LogWarning($"<color=#FF0000>[花朵未生成]</color> 关卡:{stageId} | 初始字数 {n_chars}，即使降至0级也无法满足 n_chars - m_flower > 3。触发绝对保护规则！");
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
