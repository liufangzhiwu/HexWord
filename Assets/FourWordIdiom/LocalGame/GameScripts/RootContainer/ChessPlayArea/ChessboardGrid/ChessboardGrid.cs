using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Coffee.UIEffects;
using DG.Tweening;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

/// <summary>
/// 字块矩阵面板
/// </summary>
public class ChessboardGrid : MonoBehaviour
{
    [Header("字块预制体")]
    [SerializeField] public GameObject PuzzleItemObj; // 预制体
    // 🌟 组成功时框起来的闪光特效预制体
    [Header("词组特效预制体")]
    [SerializeField] private GameObject _groupShinePrefab;
    
    private ChessStageProgressData CurrStageData => ChessStageController.Instance.CurrStageData;
    
    //格子粒子
    private GameObject ToolTipsEffect;
    public ObjectPool TipsEffectPool;
    
    private ObjectPool LetterTilePool;
    private ObjectPool GroupShinePool;

    public ChessPlayArea GamePlayArea { get; private set; }

    // 存放棋盘的字块
    public readonly Dictionary<(int row, int col), ChessView> GridList = new();
    public readonly Dictionary<ChessView, int> TileErrorCounts = new();
    private List<List<ChessView>> _lastCorrectGroups;   //记录上一次完成的正确词组
    private PhraseGroup _pendingDeadlockGroup; // 🌟 新增：用于在逻辑与动画之间传递具体的死锁破冰目标
    public bool GameOver { get; private set; }
    // 需要更新的字块
    //private readonly HashSet<ChessView> updateViews = new();
    // 当前选择的格子
    public ChessView selecteTile;
    private StringBuilder selectedPuzzle; // 完成词的收集
    // 🌟 新增变量：用于缓存等待触发报错引导的格子
    public ChessView pendingErrorTutorialTile;
    
    // 🌟 新增：记录当前这条连击链是否已经触发过激活音效
    [HideInInspector] public bool hasPlayedComboSoundThisChain = false;
    // 🌟 新增：全局输入阻断锁（当协程、道具动画播放时，绝对禁止玩家物理交互）
    [HideInInspector] public bool IsBlockInput = false;
    
    [HideInInspector] public string currentFailingPhrase = "";      // 当前正在连续答错的成语
    [HideInInspector] public int currentPhraseConsecutiveErrors = 0; // 该成语的连续答错次数
    
    public void Initialize(ChessPlayArea play)
    {
        if (PuzzleItemObj == null)
        {
            PuzzleItemObj = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "ChessTileView");
        }
        if (_groupShinePrefab == null)
        {
            _groupShinePrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "WaitGroupShineEffect");
        }
        GroupShinePool = new ObjectPool(_groupShinePrefab.gameObject, ObjectPool.CreatePoolContainer(transform,"GroupShinePool"), 3, PoolBehaviour.GameObject);
        LetterTilePool = new ObjectPool(PuzzleItemObj.gameObject, transform, 3, PoolBehaviour.CanvasGroup);

        if (ToolTipsEffect == null)
        {
            ToolTipsEffect = AdvancedBundleLoader.SharedInstance.LoadGameObject("useritems", "ToolTipsEffect");    
        }
        TipsEffectPool = new ObjectPool(ToolTipsEffect, ObjectPool.CreatePoolContainer(transform, "TipsEffectPool"), 8, PoolBehaviour.GameObject);
        GamePlayArea = play;
        selectedPuzzle = new StringBuilder();
    }

    #region 词语操作

    /// <summary>
    /// 直接完成选中的成语, 道具操作
    /// </summary>
    public IEnumerator CompletedPhrase2()
    {
        Dictionary<Chesspiece, List<PhraseGroup>> friendGroups = new();
        HashSet<string> handledIds = new HashSet<string>();
        HashSet<string> previouslyCompletedIds = new HashSet<string>();
        var targetGroup = GetChessGroups(selecteTile.Row, selecteTile.Col)
            .Where(g => !g.chesspieces.Any(p => 
                GridList.TryGetValue((p.row, p.col), out var v) && v.chesspiece.hasIce))
            .OrderByDescending(g => g.direction) // 1 优先
            .FirstOrDefault(); // 只取一

        if (targetGroup == null)
        {
            MessageSystem.Instance.ShowTip("该成语被冰块冻住了，请先破冰！");
            GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.AutoComplete, 1, "冰块拦截退还");
            GamePlayArea.InitToolUI();
            yield break; // 彻底斩断后续逻辑
        }
        if (targetGroup.chesspieces.All(p => GridList.TryGetValue((p.row, p.col), out var v) && v.CurrState == TileState.Success))
        {
            previouslyCompletedIds.Add(targetGroup.id);
        }
        for (int i = 0; i < targetGroup.chesspieces.Count; i++)
        {
            var piece = targetGroup.chesspieces[i];
            var fGroups = GetChessGroups(piece.row, piece.col).ToList();
            
            // 存入交叉字典
            friendGroups.TryAdd(piece, fGroups);

            foreach (var fg in fGroups)
            {
                if (fg.chesspieces.All(p => GridList.TryGetValue((p.row, p.col), out var v) && v.CurrState == TileState.Success))
                {
                    previouslyCompletedIds.Add(fg.id);
                }
            }
        }
        // ==========================================
        // 🔥 新增：收集目标词组的所有格子，准备跳跃！
        // ==========================================
        List<ChessView> jumpTargets = new List<ChessView>();
        foreach (var p in targetGroup.chesspieces)
        {
            if (GridList.TryGetValue((p.row, p.col), out ChessView v))
                jumpTargets.Add(v);
        }
        bool isJumping = true;
        // 屏蔽点击，呼叫 UI 播放跳跃光效
        EventDispatcher.instance.TriggerChangeTopRaycast(false);
        GamePlayArea.PlayAutoCompleteJumpEffect(jumpTargets, () => {
            isJumping = false; // 跳跃结束，放行协程
        });
        yield return new WaitUntil(() => !isJumping);
        
        // ==========================================
        
        selectedPuzzle.Clear();
        List<ChessView> mainGroupViews = new List<ChessView>();
        
        for (int i = 0; i < targetGroup?.chesspieces.Count; i++)
        {
            var piece = targetGroup.chesspieces[i];
            if (GridList.TryGetValue((piece.row, piece.col), out ChessView view2))
            {
                if (view2.CurrState != TileState.Success)
                {
                    if (view2.chesspiece?.bowl != null)
                    {
                        GamePlayArea.puzzleTileTable.OnNotifyResult(view2.chesspiece.bowl, 0);
                        view2.chesspiece.bowl = null;
                    }
                    else if (view2.CurrState == TileState.Fill || view2.CurrState == TileState.Error)
                    {
                        GamePlayArea.puzzleTileTable.OnNotifyResult(new Bowl { letter = view2.chesspiece?.letter }, 0);
                    }

                    if (view2.CurrState != TileState.Default)
                    {
                        // Bowl rb = GamePlayArea.puzzleTileTable.CleanBowlView(view2.chesspiece);
                        BowlView targetBowlView = GamePlayArea.puzzleTileTable.GridList.FirstOrDefault(b => b.letter == view2.chesspiece.letter);
                        if (targetBowlView != null) 
                        {
                            Bowl rb = targetBowlView.bowl;
                            view2.chesspiece!.bowl = rb;
                        
                            // 🌟 第一步：发 1，模拟扣除一个库存 (count--)
                            GamePlayArea.puzzleTileTable.OnNotifyResult(rb, 1);
                            // 🌟 第二步：发 2，彻底销毁 (如果 count <= 0)
                            GamePlayArea.puzzleTileTable.OnNotifyResult(rb, 2);
                        }
                    }

                    if (view2.CurrState is not TileState.Default and not TileState.Success)
                        GamePlayArea.AddCompleteCount(view2);
                    
                    if (view2.chesspiece?.bowl != null)
                    {
                        view2._isGoldLeaf=view2.chesspiece.bowl.isGoldLeaf;
                    }

                    view2.SetTileState(TileState.Success, false);
                }
                if (view2.chesspiece != null && view2.chesspiece.hasLeaf)
                {
                    view2.isPendingLeafFlight = true;
                    view2.chesspiece.hasLeaf = false; // 瞬间解绑，防重刷
                    ChessStageController.Instance.CurrStageData.CollectedLeaves++;
                }
                mainGroupViews.Add(view2);
                selectedPuzzle.Append(view2.chesspiece?.letter);
            }

            handledIds.Add(targetGroup.id);
        }
        List<List<ChessView>> correctGroups = new List<List<ChessView>>();
        List<List<ChessView>> errorGroups = new List<List<ChessView>>(); // 道具不会出错，留空备用
        if (!previouslyCompletedIds.Contains(targetGroup.id))
        {
            // checkGroup.Add(mainGroupViews); 
            correctGroups.Add(mainGroupViews);
            // GamePlayArea.AddFoundPuzzle(selectedPuzzle.ToString());
        }
        // 处理组内其他词的相关词组
        foreach (var kvp in friendGroups)
        {
            // 循环检查朋友在其组内是错误的
            // 此处如何跳过selecteTile所关联的组， 因为上面已经处理了
            foreach (PhraseGroup group in kvp.Value)
            {
                if (handledIds.Contains(group.id)) continue;
                if (previouslyCompletedIds.Contains(group.id)) continue;
                // 是否都正确，正确就设置素材success,
                bool groupSuccess = group.chesspieces.All(p =>
                    GridList.TryGetValue((p.row, p.col), out ChessView v) && v.Correct);
                bool hasIceInFriendGroup = group.chesspieces.Any(p => 
                    GridList.TryGetValue((p.row, p.col), out ChessView v) && v.chesspiece.hasIce);
                if (!groupSuccess || hasIceInFriendGroup) // 最高优先级
                {
                    continue;
                }
                selectedPuzzle.Clear();
                List<ChessView> friendViews = new List<ChessView>();
              
                group.chesspieces.ForEach(g =>
                {
                    if (GridList.TryGetValue((g.row, g.col), out ChessView v))
                    {
                        if (v.CurrState != TileState.Success)
                        {
                            if (v.chesspiece.bowl != null)
                            {
                                GamePlayArea.puzzleTileTable.OnNotifyResult(v.chesspiece.bowl, 2);
                            }
                            v.SetTileState(TileState.Success, false);
                        }
                        
                        if (v.chesspiece.hasLeaf)
                        {
                            v.isPendingLeafFlight = true;
                            v.chesspiece.hasLeaf = false;
                            ChessStageController.Instance.CurrStageData.CollectedLeaves++;
                        }
                        friendViews.Add(v); 
                    }
                    selectedPuzzle.Append(g.letter);
                });
                // checkGroup.Add(friendViews);
                correctGroups.Add(friendViews);
                // GamePlayArea.AddFoundPuzzle(selectedPuzzle.ToString());
                handledIds.Add(group.id);
            }
        }
        yield return PlayGroupSuccessSequence(correctGroups, errorGroups);
        EventDispatcher.instance.TriggerChangeTopRaycast(true);
        // SearchNextTile();
    }
    /// <summary>
    /// 直接完成选中的成语 (直接物理填入 + 5秒扫光 + 完美交叉判定)
    /// </summary>
    public IEnumerator CompletedPhrase()
    {
        // ========================================================================
        // 步骤一：提取关卡所有成语词组并检索当前盘面的实时空格数与冰块状态
        // ========================================================================
        var allGroupsInStage = GamePlayArea.CurrStageInfo.PhraseGroups;

        // 筛选符合主干条件的组：没有被冰块覆盖，总空格 >= 2，且【至少包含1个未提示的空格】
        var primaryCandidateGroups = allGroupsInStage.Where(g => 
        {
            bool hasIce = g.chesspieces.Any(p => GridList.TryGetValue((p.row, p.col), out var v) && v.chesspiece.hasIce && !v.iceLogicBroken);
            
            int totalEmptyCount = g.chesspieces.Count(p => GridList.TryGetValue((p.row, p.col), out var v) && 
                v.CurrState != TileState.Success && v.CurrState != TileState.Default);
                
            int untippedEmptyCount = g.chesspieces.Count(p => GridList.TryGetValue((p.row, p.col), out var v) && 
                v.CurrState != TileState.Success && v.CurrState != TileState.Default && !v.chesspiece.tip);
            
            return !hasIce && totalEmptyCount >= 2 && untippedEmptyCount >= 1;
        }).ToList();

        // ========================================================================
        // 核心分支 A：满足条件的干净词组达到或超过 3 个 -> 启动多组随机单字【直接填入并扫光】
        // ========================================================================
        if (primaryCandidateGroups.Count >= 3)
        {
            EventDispatcher.instance.TriggerChangeTopRaycast(false);
            
            var shuffledGroups = primaryCandidateGroups.OrderBy(x => Random.value).ToList();
            List<ChessView> jumpTargets = new List<ChessView>();

            // 抽取跳跃目标
            foreach (var group in shuffledGroups)
            {
                if (jumpTargets.Count >= 3) break;
                
                var untippedEmptyTilesInGroup = group.chesspieces
                    .Select(p => GridList.GetValueOrDefault((p.row, p.col)))
                    .Where(v => v != null && 
                                v.CurrState != TileState.Success && 
                                v.CurrState != TileState.Default && 
                                !v.chesspiece.tip && 
                                !jumpTargets.Contains(v)) // 🛡️ 绝对防御：剔除已经被前面词组抢走的交叉字！
                    .ToList();

                if (untippedEmptyTilesInGroup.Count == 0) continue; 
                ChessView targetTile = untippedEmptyTilesInGroup[Random.Range(0, untippedEmptyTilesInGroup.Count)];
                jumpTargets.Add(targetTile);
            }

            if (jumpTargets.Count > 0)
            {
                // 播放青蛙跳跃路径光效
                bool isJumping = true;
                GamePlayArea.PlayAutoCompleteJumpEffect(jumpTargets, () => {
                    isJumping = false; 
                });
                yield return new WaitUntil(() => !isJumping);

                List<List<ChessView>> correctGroups = new List<List<ChessView>>();
                List<List<ChessView>> errorGroups = new List<List<ChessView>>();

                // 🌟 修复 1：记录填入前，哪些相关组已经提前完成了（防止重复播通关动画）
                HashSet<string> previouslyCompletedIds = new HashSet<string>();
                HashSet<PhraseGroup> affectedGroups = new HashSet<PhraseGroup>();
                foreach (var targetTile in jumpTargets)
                {
                    var groups = GetChessGroups(targetTile.Row, targetTile.Col);
                    foreach (var g in groups)
                    {
                        affectedGroups.Add(g);
                        if (g.chesspieces.All(p => GridList.TryGetValue((p.row, p.col), out var v) && v.CurrState == TileState.Success))
                        {
                            previouslyCompletedIds.Add(g.id);
                        }
                    }
                }

                // 直接物理填入并触发扫光！
                foreach (var targetTile in jumpTargets)
                {
                    // 1. 清理错字/临时字
                    if (targetTile.CurrState == TileState.Fill || targetTile.CurrState == TileState.Error)
                    {
                        Bowl dummyBowl = targetTile.chesspiece.bowl ?? new Bowl { letter = targetTile.chesspiece.letter };
                        GamePlayArea.puzzleTileTable.OnNotifyResult(dummyBowl, 0);
                        targetTile.chesspiece.bowl = null;
                    }

                    // 2. 从下方字盘扣除库存并销毁
                    BowlView matchingBowl = GamePlayArea.puzzleTileTable.GridList.FirstOrDefault(v => 
                        v.letter == targetTile.Answer && v.bowl.status == 0);

                    if (matchingBowl != null)
                    {
                        targetTile.SetPuzzle(matchingBowl.bowl);
                        GamePlayArea.puzzleTileTable.OnNotifyResult(matchingBowl.bowl, 1); 
                        GamePlayArea.puzzleTileTable.OnNotifyResult(matchingBowl.bowl, 2); 
                    }
                    else
                    {
                        Bowl fallbackBowl = new Bowl { letter = targetTile.Answer, status = 2, count = 0 };
                        targetTile.SetPuzzle(fallbackBowl);
                    }
                    if (targetTile.CurrState is not TileState.Default and not TileState.Success)
                    {
                        GamePlayArea.AddCompleteCount(targetTile);
                    }
                    // 3. 设为成功并处理树叶
                    targetTile.SetTileState(TileState.Success, false);
                    // if (targetTile.chesspiece != null && targetTile.chesspiece.hasLeaf)
                    // {
                    //     targetTile.isPendingLeafFlight = true;
                    //     targetTile.chesspiece.hasLeaf = false;
                    //     ChessStageController.Instance.CurrStageData.CollectedLeaves++;
                    // }

                    // 4. 触发弹跳变绿和 5 秒扫光特效！
                    targetTile.PlaySuccessAnimation(0.5f, () => {
                        targetTile.UpdateTile(true);
                    });
                    targetTile.PlayHintShiny(5f);
                }

                // 🌟 修复 2：统一检查所有受影响的词组（包含交叉组），看看有没有刚好被连带凑齐的
                foreach (var group in affectedGroups)
                {
                    if (previouslyCompletedIds.Contains(group.id)) continue;

                    bool groupSuccess = group.chesspieces.All(p => 
                        GridList.TryGetValue((p.row, p.col), out ChessView v) && v.Correct);
                    bool hasIceInGroup = group.chesspieces.Any(p => 
                        GridList.TryGetValue((p.row, p.col), out ChessView v) && v.chesspiece.hasIce && !v.iceLogicBroken);
                    
                    if (groupSuccess && !hasIceInGroup)
                    {
                        List<ChessView> groupViews = new List<ChessView>();
                        group.chesspieces.ForEach(g =>
                        {
                            if (GridList.TryGetValue((g.row, g.col), out ChessView v))
                            {
                                if (v.CurrState != TileState.Success)
                                {
                                    if (v.chesspiece.bowl != null) GamePlayArea.puzzleTileTable.OnNotifyResult(v.chesspiece.bowl, 2);
                                    v.SetTileState(TileState.Success, false);
                                }
                                if (v.chesspiece.hasLeaf)
                                {
                                    v.isPendingLeafFlight = true;
                                    v.chesspiece.hasLeaf = false;
                                    ChessStageController.Instance.CurrStageData.CollectedLeaves++;
                                }
                                groupViews.Add(v); 
                            }
                        });
                        correctGroups.Add(groupViews);
                    }
                }

                // 如果有整组完成，播放大满贯表现
                if (correctGroups.Count > 0)
                {
                    yield return PlayGroupSuccessSequence(correctGroups, errorGroups);
                }

                // 🌟 修复 3：防乱跳保护。只有在当前光标所在的格子被刚好填满变绿时，才执行重定向寻找下一个空格
                if (selecteTile == null || selecteTile.CurrState == TileState.Success || selecteTile.CurrState == TileState.Default)
                {
                    ChessView lastHintedTile = jumpTargets.LastOrDefault();
                    if (lastHintedTile != null) selecteTile = lastHintedTile;
                    SearchNextTile();
                }
            }

            EventDispatcher.instance.TriggerChangeTopRaycast(true);
            IsBlockInput = false;
        }
        // ========================================================================
        // 核心分支 B：无法凑齐 3 个理想词组 -> 降级为寻找全盘空格子最多的非冰冻成语，执行整组直接通关
        // ========================================================================
        else
        {
            var fallbackGroupRank = allGroupsInStage.Select(g => 
            {
                bool hasIce = g.chesspieces.Any(p => GridList.TryGetValue((p.row, p.col), out var v) && v.chesspiece.hasIce && !v.iceLogicBroken);
                int emptyCount = g.chesspieces.Count(p => GridList.TryGetValue((p.row, p.col), out var v) && 
                    v.CurrState != TileState.Success && v.CurrState != TileState.Default);
                return new { Group = g, HasIce = hasIce, EmptyCount = emptyCount };
            })
            .Where(x => x.EmptyCount > 0)
            .OrderByDescending(x => x.HasIce ? 0 : 1) 
            .ThenByDescending(x => x.EmptyCount)
            .ToList();

            if (fallbackGroupRank.Count == 0)
            {
                MessageSystem.Instance.ShowTip("全盘已全部解答完毕，无符合要求的词组！");
                yield break;
            }

            if (fallbackGroupRank[0].HasIce)
            {
                MessageSystem.Instance.ShowTip("其余成语均被冰块冻住了，请先破冰！");
                GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.AutoComplete, 1, "冰块拦截退还");
                GamePlayArea.InitToolUI();
                yield break; 
            }

            int maxEmpties = fallbackGroupRank[0].EmptyCount;
            var finalCandidates = fallbackGroupRank
                .Where(x => x.EmptyCount == maxEmpties && !x.HasIce)
                .Select(x => x.Group)
                .ToList();
            
            PhraseGroup targetGroup = finalCandidates[Random.Range(0, finalCandidates.Count)];

            List<ChessView> jumpTargets = new List<ChessView>();
            foreach (var p in targetGroup.chesspieces)
            {
                if (GridList.TryGetValue((p.row, p.col), out ChessView v)) jumpTargets.Add(v);
            }
            bool isJumping = true;
            EventDispatcher.instance.TriggerChangeTopRaycast(false);
            GamePlayArea.PlayAutoCompleteJumpEffect(jumpTargets, () => {
                isJumping = false; 
            });
            yield return new WaitUntil(() => !isJumping);

            selectedPuzzle.Clear();
            List<ChessView> mainGroupViews = new List<ChessView>();
            Dictionary<Chesspiece, List<PhraseGroup>> friendGroups = new Dictionary<Chesspiece, List<PhraseGroup>>();
            HashSet<string> handledIds = new HashSet<string>();
            HashSet<string> previouslyCompletedIds = new HashSet<string>();

            if (targetGroup.chesspieces.All(p => GridList.TryGetValue((p.row, p.col), out var v) && v.CurrState == TileState.Success))
            {
                previouslyCompletedIds.Add(targetGroup.id);
            }
            for (int i = 0; i < targetGroup.chesspieces.Count; i++)
            {
                var piece = targetGroup.chesspieces[i];
                var fGroups = GetChessGroups(piece.row, piece.col).ToList();
                friendGroups.TryAdd(piece, fGroups);

                foreach (var fg in fGroups)
                {
                    if (fg.chesspieces.All(p => GridList.TryGetValue((p.row, p.col), out var v) && v.CurrState == TileState.Success))
                    {
                        previouslyCompletedIds.Add(fg.id);
                    }
                }
            }

            for (int i = 0; i < targetGroup.chesspieces.Count; i++)
            {
                var piece = targetGroup.chesspieces[i];
                if (GridList.TryGetValue((piece.row, piece.col), out ChessView view2))
                {
                    if (view2.CurrState != TileState.Success)
                    {
                        if (view2.chesspiece?.bowl != null)
                        {
                            GamePlayArea.puzzleTileTable.OnNotifyResult(view2.chesspiece.bowl, 0);
                            view2.chesspiece.bowl = null;
                        }
                        else if (view2.CurrState == TileState.Fill || view2.CurrState == TileState.Error)
                        {
                            GamePlayArea.puzzleTileTable.OnNotifyResult(new Bowl { letter = view2.chesspiece?.letter }, 0);
                        }

                        if (view2.CurrState != TileState.Default)
                        {
                            BowlView targetBowlView = GamePlayArea.puzzleTileTable.GridList.FirstOrDefault(b => b.letter == view2.chesspiece.letter);
                            if (targetBowlView != null) 
                            {
                                Bowl rb = targetBowlView.bowl;
                                view2.chesspiece!.bowl = rb;
                                GamePlayArea.puzzleTileTable.OnNotifyResult(rb, 1); 
                                GamePlayArea.puzzleTileTable.OnNotifyResult(rb, 2); 
                            }
                        }

                        if (view2.CurrState is not TileState.Default and not TileState.Success)
                            GamePlayArea.AddCompleteCount(view2);

                        if (view2.chesspiece?.bowl != null)
                        {
                            view2._isGoldLeaf = view2.chesspiece.bowl.isGoldLeaf;
                        }

                        view2.SetTileState(TileState.Success, false);
                    }
                    if (view2.chesspiece != null && view2.chesspiece.hasLeaf)
                    {
                        view2.isPendingLeafFlight = true;
                        view2.chesspiece.hasLeaf = false; 
                        ChessStageController.Instance.CurrStageData.CollectedLeaves++;
                    }
                    mainGroupViews.Add(view2);
                    selectedPuzzle.Append(view2.chesspiece?.letter);
                }
                handledIds.Add(targetGroup.id);
            }

            List<List<ChessView>> fallbackCorrectGroups = new List<List<ChessView>>();
            List<List<ChessView>> fallbackErrorGroups = new List<List<ChessView>>(); 
            if (!previouslyCompletedIds.Contains(targetGroup.id))
            {
                fallbackCorrectGroups.Add(mainGroupViews);
            }

            foreach (var kvp in friendGroups)
            {
                foreach (PhraseGroup group in kvp.Value)
                {
                    if (handledIds.Contains(group.id)) continue;
                    if (previouslyCompletedIds.Contains(group.id)) continue;

                    bool groupSuccess = group.chesspieces.All(p => GridList.TryGetValue((p.row, p.col), out ChessView v) && v.Correct);
                    bool hasIceInFriendGroup = group.chesspieces.Any(p => GridList.TryGetValue((p.row, p.col), out ChessView v) && v.chesspiece.hasIce);
                    if (!groupSuccess || hasIceInFriendGroup) continue;

                    selectedPuzzle.Clear();
                    List<ChessView> friendViews = new List<ChessView>();

                    group.chesspieces.ForEach(g =>
                    {
                        if (GridList.TryGetValue((g.row, g.col), out ChessView v))
                        {
                            if (v.CurrState != TileState.Success)
                            {
                                if (v.chesspiece.bowl != null) GamePlayArea.puzzleTileTable.OnNotifyResult(v.chesspiece.bowl, 2);
                                v.SetTileState(TileState.Success, false);
                            }
                            if (v.chesspiece.hasLeaf)
                            {
                                v.isPendingLeafFlight = true;
                                v.chesspiece.hasLeaf = false;
                                ChessStageController.Instance.CurrStageData.CollectedLeaves++;
                            }
                            friendViews.Add(v); 
                        }
                        selectedPuzzle.Append(g.letter);
                    });
                    fallbackCorrectGroups.Add(friendViews);
                    handledIds.Add(group.id);
                }
            }

            yield return PlayGroupSuccessSequence(fallbackCorrectGroups, fallbackErrorGroups);
            
            // 🌟 修复 4：兜底分支的防乱跳保护。同样，如果光标本就在别处看戏，它不需要挪窝。
            if (selecteTile == null || selecteTile.CurrState == TileState.Success || selecteTile.CurrState == TileState.Default)
            {
                if (mainGroupViews.Count > 0) selecteTile = mainGroupViews.Last();
                SearchNextTile();
            }
            
            EventDispatcher.instance.TriggerChangeTopRaycast(true);
            IsBlockInput = false;
        }
    }
    /// <summary>
    /// 是否已经提示过
    /// </summary>
    /// <returns></returns>
    public bool IsSelectTip()
    {
        if (selecteTile == null) return true;
        if (selecteTile.chesspiece == null) return true;
        return selecteTile.chesspiece.tip;
    }

    // 选中格子接收数据的方法,
    private void ReceiveData(ChessView data)
    {
        // 0. 解锁被点格子
        if (data.CurrState == TileState.Fill || data.CurrState == TileState.Error)
        {
            Bowl dummyBowl = data.chesspiece.bowl ?? new Bowl { letter = data.chesspiece.letter };
            GamePlayArea.puzzleTileTable.OnNotifyResult(dummyBowl, 0);
            data.chesspiece.bowl = null;
        }
        else if (data.chesspiece.bowl != null) // 保留原有兜底逻辑
        {
            GamePlayArea.puzzleTileTable.OnNotifyResult(data.chesspiece.bowl, 0);
            data.chesspiece.bowl = null;
        }
        UpdateDirectionOnManualClick(data);
        
        GamePlayArea.HandleGamePlayCall(data.gameObject, "ClickChess"); // 设置字块事件
        SetCheckView(data);
        CheckChessGroupState(data);
    }

    // 扫描组内所有词，这些词在其他分组是否完成且错误
    private void CheckChessGroupState(ChessView chessView)
    {
        List<PhraseGroup> selfGroups = GetChessGroups(chessView.Row, chessView.Col).ToList();

        // 查找当前词的会包含哪些词组，找到这些词组的成员，以及成员所包含的词组
        Dictionary<Chesspiece, List<PhraseGroup>> friendGroups = new();
        //Debug.Log("没有找到? "+ JsonConvert.SerializeObject(chessView.chesspiece));
        foreach (PhraseGroup myGroup in selfGroups)
        {
            foreach (Chesspiece firend in myGroup.chesspieces)
            {
                if (!friendGroups.ContainsKey(firend))
                {
                    friendGroups.Add(firend, GetChessGroups(firend.row, firend.col).ToList());
                    //Debug.Log("找到" + firend.letter + " 组" + JsonConvert.SerializeObject(friendGroups.Values));
                }
            }
        }

        // 检查朋友的组是否都完成了
        Dictionary<Chesspiece, TileState> FirendState = new();
        foreach (var kvp in friendGroups)
        {
            Chesspiece friend = kvp.Key;

            //Debug.Log($"{friend.letter} 有组：" + kvp.Value.Count);
            TileState tileState = TileState.None;
            // 循环检查朋友在其组内是错误的
            foreach (PhraseGroup group in kvp.Value)
            {
                // 是否都正确，正确就设置素材success, 该朋友也是success
                bool groupSuccess = group.chesspieces.All(p =>
                    GridList.TryGetValue((p.row, p.col), out ChessView v) && v.CurrState == TileState.Success);
                if (groupSuccess) // 最高优先级
                {
                    tileState = TileState.Success;
                    break;
                }

                bool groupError = group.chesspieces.All(p =>
                                      GridList.TryGetValue((p.row, p.col), out ChessView v) &&
                                      v.CurrState != TileState.None &&
                                      v.CurrState != TileState.Check) &&
                                  group.chesspieces.Any(p =>
                                      GridList.TryGetValue((p.row, p.col), out ChessView v) &&
                                      !v.Correct);
                if (groupError) // 第二优先级
                {
                    // GamePlayArea.AddWordError(1);
                    tileState = TileState.Error;
                    break;
                }

                //Debug.Log($"组名 {group.id} 朋友 " + friend.letter + " 是否错误" + groupError);
                // 是否有空的成员，若有该朋友设置 fill
                bool groupFill = group.chesspieces.Any(p =>
                    {
                        if (GridList.TryGetValue((p.row, p.col), out ChessView v))
                        {
                            //Debug.Log("检查朋友空状态: " + v.CurrState);
                            return v.CurrState == TileState.None || v.CurrState == TileState.Check;
                        }

                        return false;
                    }
                );
                if (groupFill)
                {
                    tileState = TileState.Fill;
                }
                //Debug.Log($"组名 {group.id} 朋友 " + friend.letter + " 是否为空" + groupFill);
            }

            FirendState.Add(friend, tileState);
        }

        // 修改朋友的状态, 朋友词组没有练成组，则恢复fill状态，连成组是错的则error,
        //Debug.Log("传入词: " + chessView.Answer + " " + JsonConvert.SerializeObject(chessView.chesspiece));
        foreach (var firend in FirendState)
        {
            if (GridList.TryGetValue((firend.Key.row, firend.Key.col), out ChessView firendview))
            {
                if (firend.Value != TileState.None &&
                    (firendview.CurrState == TileState.Fill || firendview.CurrState == TileState.Error) 
                    && firendview != chessView)
                    firendview.SetTileState(firend.Value);
            }
        }
    }
    
    // 处理点击设置字的操作
    public IEnumerator HandleBlowViewState(BowlView puzzle)
    {
        if (selecteTile != null && selecteTile.CurrState == TileState.Success)
        {
            yield break; // 直接丢弃本次非法点击
        }
        
        if (puzzle.bowl.status == 0)
            yield return SetPuzzleBoardState(puzzle);
        else
            yield return CancelPuzzleBoardState(puzzle);
        
        ChessBowlGrid.IsTutorialBlocking = false;
    }

    /// <summary>
    /// 设置格子的字
    /// </summary>
    /// <param name="puzzle"></param>
    public IEnumerator SetPuzzleBoardState(BowlView puzzle)
    {
        if (selecteTile)
        {
            //Debug.Log("设置时——" + _handing);
            if (selecteTile.chesspiece.bowl != null)
            {
                // Debug.Log("选中的词 :" + selecteTile.Answer + " 以前词: " + JsonConvert.SerializeObject(selecteTile.chesspiece.bowl));
                // 填入的旧词恢复正常
                GamePlayArea.puzzleTileTable.OnNotifyResult(selecteTile.chesspiece.bowl, 0);
            }

            Bowl bowl = puzzle.bowl;
            // bowl.status = 1;
            selecteTile.SetPuzzle(bowl);
            GamePlayArea.puzzleTileTable.OnNotifyResult(bowl, 1);
            ChessView curr = selecteTile;
            bool flyover = false;
            EventDispatcher.instance.TriggerChangeTopRaycast(false);
            puzzle.FlyToCell(curr, transform.parent, () =>
            {
                curr.UpdateTile(true);
                flyover = true;
                bool isGuideShowing = SystemManager.Instance != null && SystemManager.Instance.PanelIsShowing(PanelType.ChessLearningGuide);
                if (isGuideShowing || selecteTile == null || curr.Answer != selecteTile.Answer)
                    GamePlayArea.HandleGamePlayCall(puzzle.gameObject, "SetChess"); // 设置字块事件
            });
         
            List<List<ChessView>> correctGroups = new List<List<ChessView>>();
            List<List<ChessView>> errorGroups = new List<List<ChessView>>();
            yield return CheckSuccessful(curr,correctGroups, errorGroups);
            // Debug.Log("执行1");
            // Debug.Log($"[调用链] 即将进入 WaitUntil 帧={Time.frameCount}  flyover={flyover}");
            yield return new WaitUntil(() => flyover);
            // Debug.Log($"[调用链] WaitUntil 通过 帧={Time.frameCount}  flyover={flyover}");
            // yield return HandleChessUIState();
            if (pendingErrorTutorialTile != null)
            {
                GamePlayArea.HandleGamePlayCall(pendingErrorTutorialTile.gameObject, "ChessError");
                pendingErrorTutorialTile = null; // 触发完立刻清空
            }
            yield return PlayGroupSuccessSequence(correctGroups, errorGroups);
            if (pendingErrorTutorialTile != null)
            {
                // 如果错字没有被天上飞过来的其他字“洗白”成绿色，就弹引导
                if (pendingErrorTutorialTile.CurrState != TileState.Success && !pendingErrorTutorialTile.IsOK)
                {
                    GamePlayArea.HandleGamePlayCall(pendingErrorTutorialTile.gameObject, "ChessError");
                }
                
                // 引导已经弹出（UI遮罩会接管拦截），或者格子被幸运洗白了，底层字盘锁都可以安全解除了！
                ChessBowlGrid.IsTutorialBlocking = false; 
                pendingErrorTutorialTile = null; 
            }
            // 🌟 【终极修复】：在这里！等所有华丽特效全部播完、屏幕安稳下来后，再安全弹出引导！
            if (ChessGuideSystem.Instance.toolSourceName == "WaitLeafAnimation")
            {
                ChessGuideSystem.Instance.currentTutorial = 9; 
                ChessGuideSystem.Instance.toolSourceName = "LeafTutorialStep2"; // 正式切入第二步
                ChessGuideSystem.Instance.activeToolObject = null;
                
                // 再次拉起引导界面，展示“太棒了！继续收集...”的面板
                ChessGuideSystem.Instance.DisplayGuide(); 
            }
            EventDispatcher.instance.TriggerChangeTopRaycast(flyover);
        }
        else
            yield return null;
        
        GamePlayArea.AutoPassLevel();
    }

    /// <summary>
    /// 取消格子的字
    /// </summary>
    /// <param name="puzzle"></param>
    public IEnumerator CancelPuzzleBoardState(BowlView puzzle)
    {
        GamePlayArea.puzzleTileTable.OnNotifyResult(puzzle.bowl, 0);
        ChessView view = GridList.Values.FirstOrDefault(grid => grid.chesspiece?.bowl?.id == puzzle.bowl.id
        && grid.CurrState != TileState.Success);
        if (view == null)
        {
            view = GridList.Values.LastOrDefault(grid => 
                (grid.CurrState == TileState.Fill || grid.CurrState == TileState.Error) && 
                grid.chesspiece.letter == puzzle.letter);
        }
        if (view != null)
        {
            // 填入的旧词恢复正常
            view.chesspiece.bowl = null;
            SetCheckView(view);
        }
        yield return null;
        GamePlayArea.HandleGamePlayCall(puzzle.gameObject, "ClickChess"); //
    }
    
    /// <summary>
    /// 检查是否完成通关
    /// </summary>
    private IEnumerator CheckCompleted()
    {
        GameDataManager.Instance.UpdateLevelProgress(CurrStageData);

        yield return new WaitForSeconds(0.1f);
        // Debug.Log("是否进入完成检查");
        if(GameOver) yield break;
        // 检查是否完成
        if (GridList.Values.All(item => item.IsOK))
        {
            GameOver = true;
            Debug.Log("已全部完成，进行下一个关");
            GamePlayArea.GamePlayOver();
        }
    }

    /// <summary>
    /// 检查是否连接成功一组单词
    /// </summary>
    private IEnumerator CheckSuccessful(ChessView selecteTile, List<List<ChessView>> correctGroups, List<List<ChessView>> errorGroups)
    {
        List<PhraseGroup> phraseGroups = GetChessGroups(selecteTile.Row, selecteTile.Col)
            .OrderBy(pg => pg.direction == selecteTile.Direction)
            .ToList();
        // bool isPlaySound = false;
        ChessView nexterr = null;
        // Dictionary<string, bool> result = new Dictionary<string, bool>();
        bool hasErrorInThisMove = false; // 🌟 标记：这一次填字是否导致了任何词组错误
        bool hasAnyGroupFilled = false; // 标记：是否有任何一个成语被刚刚填满了
        foreach (var phraseGroup in phraseGroups)
        {
            // 1. 拿当前词组所有格子
            List<ChessView> chessViews = phraseGroup.chesspieces
                .Select(p => GridList.GetValueOrDefault((p.row, p.col)))
                .Where(v => v != null)
                .ToList();
            
            if (chessViews.All(v => (v.CurrState == TileState.Success && v.IsOK) || 
                                    correctGroups.Any(cg => cg.Contains(v)))) continue;      
            
            // 2. 只要有空格（未填）→ 全部正常色 + 跳过
            int filled = chessViews.Count(v => v.CurrState != TileState.None && v.CurrState != TileState.Check);
            if (filled < chessViews.Count) continue;
            
            hasAnyGroupFilled = true;
            // 3. 全部已填 → 比对答案
            bool allCorrect = chessViews.All(v => v.Correct);
            if (allCorrect)
            {
                // result.Add(phraseGroup.id, true); // 该组正确
                // 全对 → 统一绿色 ， 如果需要做动画，将这组拿出来最后做
                selectedPuzzle.Clear();
                foreach (var v in chessViews)
                {
                    if (v.chesspiece.hasLeaf)
                    {
                        v.isPendingLeafFlight = true; // 拍照留存给动画层
                        v.chesspiece.hasLeaf = false; // 瞬间解绑，避免被重刷机制误杀
                        ChessStageController.Instance.CurrStageData.CollectedLeaves++;
                    }
                }
                
                chessViews.ForEach(v =>
                {
                    if (v.chesspiece.bowl != null)
                    {
                        // v.chesspiece.bowl.status = 2;
                        // Debug.Log("统一绿色时: "+ v.Answer +" " + JsonConvert.SerializeObject(v.chesspiece.bowl));
                        GamePlayArea.puzzleTileTable.OnNotifyResult(v.chesspiece.bowl, 2);
                      
                    }
                    
                    // v.SetTileState(TileState.Success, false);
                    selectedPuzzle.Append(v.Answer);
                    v.SetTileState(TileState.Success, false);
                    // GamePlayArea.ButterWordAddIcon(v);
                    //StartCoroutine(v.PlayErrorAnimation(true));
                 
                });

                correctGroups.Add(chessViews);
            }
            else
            {
                hasErrorInThisMove = true;
               // selectedPuzzle.Clear();
                // 词组内的所有词都已填入，才判断是正确还是错误，正确设置绿色，错误设置红色，若词组内没有填完，则保持原色
                // 有错 → 已填入的都变红
                chessViews.Where(v => v.CurrState != TileState.Default && v.CurrState != TileState.Success)
                    .ToList()
                    .ForEach(v =>
                    {
                        v.SetTileState(TileState.Error, false);
                        if(nexterr == null && !v.Correct)
                        {
                            nexterr = v;
                        }
                       // selectedPuzzle.Append(v.Answer);
                    });
                errorGroups.Add(chessViews);
            }
        }
        if (hasErrorInThisMove)
        {
            // 给当前填错的格子错误次数 +1
            if (!TileErrorCounts.ContainsKey(selecteTile)) TileErrorCounts[selecteTile] = 0;
            TileErrorCounts[selecteTile]++;
            
            // 把格子本身，和它已经错的次数传过去
            GamePlayArea.AddWordError(selecteTile, TileErrorCounts[selecteTile]);
        }

        if (hasAnyGroupFilled && hasErrorInThisMove)
        {
            // 1. 播放填错音效
            AudioManager.Instance.PlaySoundEffect("ChoiceError_UI");
            // 2. 强行清理全盘其他格子的选中状态
            foreach (var item in GridList.Values)
            {
                if (item.CurrState == TileState.Check) item.SetTileState(TileState.None);
                item.SetChoose(false);
            }
            // 3. 🎯 【关键拦截】：光标哪也不去，死死锁在玩家刚刚填入的最后一个格子上！
            // 确保它处于 Check 状态，并且亮起选择框，方便玩家原地改错
            // 正常情况下光标锁在刚填的格子上，但如果它因交叉组变绿了（Success）
            // 我们必须将光标转移到真正报错变红的格子上（nexterr）！
            ChessView targetCursorTile = selecteTile;
            if (selecteTile.CurrState == TileState.Success && nexterr != null)
            {
                targetCursorTile = nexterr;
            }
            targetCursorTile.SetChoose(true);
            this.selecteTile = targetCursorTile;
            // 同步光标快照到底层，确保重进游戏时位置正确
            ChessStageController.Instance.ModifyCursor(targetCursorTile.Row, targetCursorTile.Col);
            // 4. 通知新手引导或错字事件
            this.pendingErrorTutorialTile = targetCursorTile;
            // GamePlayArea.HandleGamePlayCall(targetCursorTile.gameObject, "ChessError");
            if (!GameDataManager.Instance.UserData.ChessTutorialProgress[3])
            {
                ChessBowlGrid.IsTutorialBlocking = true;
            }
        }
        else
        {
            
            PreBreakIceLogic(correctGroups);
            PreBreakFlowerLogic(correctGroups); // 🌟 新增：紧接着破冰计算花朵绽放
            yield return null;
            if (selecteTile == null || selecteTile.CurrState == TileState.Success)
            {
                var refTile = correctGroups.LastOrDefault()?.LastOrDefault();
                if (refTile != null) selecteTile = refTile;
            }
            SearchNextTile();
            if (hasAnyGroupFilled)
            {
                // 🌟 【核心点】：只有在成语填满且全对消词成功的那一帧，才批准发射下一片树叶！
                // 它会在新光标已经就位、老数据已经安全隔离的这一瞬间，完美清算并孵化出下一片循环换肤的叶子！
                // GenerateNextLevelLeaf(); 
            }
        }

        yield return null;
    }
    
    /// <summary>
    /// 🔥 核心视觉：整组瞬间亮起、包裹闪光框、同时缩放
    /// </summary>
    private IEnumerator PlayGroupSuccessSequence(List<List<ChessView>> correctGroups, List<List<ChessView>> errorGroups)
    {
        // 🌟 核心防死锁：如果没有正确或者错误组（代表只是没填满的常规点击），也必须确保在结尾触发一次跳转指引！
        if (correctGroups.Count == 0 && errorGroups.Count == 0) yield break;
        
        _lastCorrectGroups = correctGroups;   // ← 添加这一行
        
        List<GameObject> allShineInstances = new List<GameObject>();
        const float effectDuration = 0.6f;
        const float padding = 15f;
        
        List<int> groupActualScores = new List<int>();
        
        // ========================================================================
        // 🟢 【时序净化核心】：在没有任何格子状态被篡改前，第一帧先发加分、先起飞树叶！
        // ========================================================================
        foreach (var viewsInGroup in correctGroups)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var view in viewsInGroup)
            {
                sb.Append(view.Answer);
                if (view.isPendingLeafFlight)
                {
                    GamePlayArea.PlayLeafFlyToCollectionPoint(view.transform);
                    view.isPendingLeafFlight = false; // 飞完复位清空
                }
            }
            // 1. 🎯 【补齐加分漏判】：计算本次消除应得的纯正禅意分与连击加成
            int baseScore = ChessStageController.Instance.GetBaseScore();
            int comboBonus = ChessStageController.Instance.GetComboScoreReward(ChessStageController.Instance.PuzzleComboCount);
            int scoreDiff = baseScore + comboBonus;
            groupActualScores.Add(scoreDiff);
            // 2. 🎯 寻找最优最空旷的格子位置，作为黄色能量粒子线的起飞原点
            int groupDirection = viewsInGroup[0].chesspiece.direction; // 1横向，0纵向
            ChessView bestScoreOriginView = viewsInGroup.Last(); 
            for (int i = viewsInGroup.Count - 1; i >= 0; i--)
            {
                var v = viewsInGroup[i];
                bool hasNeighbor = (groupDirection == 1) ? GridList.ContainsKey((v.Row, v.Col + 1)) : GridList.ContainsKey((v.Row + 1, v.Col));
                if (!hasNeighbor) { bestScoreOriginView = v; break; }
            }

            // 3. 🎯 爆发加分机制：设置指定起飞坐标，发射向右上角的黄色飞行动画与老虎机滚动！
            GamePlayArea.ScoreFlyPos = bestScoreOriginView.transform.position;
            // ChessStageController.Instance.CurrentTotalScore += scoreDiff; // 物理加分数据沉淀
            GamePlayArea.AddFoundPuzzle(sb.ToString()); // 触发总加分事件
        }
        
        // ==========================================
        // 🟢 先统一播放【正确组】的华丽特效
        // ==========================================
        for (int groupIdx = 0; groupIdx < correctGroups.Count; groupIdx++)
        {
                var viewsInGroup = correctGroups[groupIdx];
                // StringBuilder sb = new StringBuilder();
                // foreach (var v in viewsInGroup) sb.Append(v.Answer);
                // 🌟 无条件处理花朵（破冰和花朵各自独立）
                // bool hasBloomingFlower = ProcessFlowerBlooming(viewsInGroup);
                //
                // bool hasIceBroken = BreakAdjacentIce(viewsInGroup);
                // bool hasDeadlockIceBroken = false;
                // if (!hasIceBroken)
                // {
                //     hasDeadlockIceBroken = CheckAndBreakDeadlockIce();
                // }
                // if (hasDeadlockIceBroken) hasIceBroken = true;
                //
                //
                // // 如果有花朵，等待花朵绽放动画播完，再进行后续的闪光框和飘分！
                // if (hasBloomingFlower || hasIceBroken)
                // {
                //     yield return new WaitForSeconds(0.6f); 
                // }
                // ==========================================
                // 【正确表现】：底框闪烁、瞬间放大、喷发粒子
                // ==========================================
                // ---------- 精确计算放大后的包围盒 ----------
                float minX = float.MaxValue, maxX = float.MinValue;
                float minY = float.MaxValue, maxY = float.MinValue;
                
                // 1. 遍历字块，获取最准确的【本地坐标】边界
                foreach (var view in viewsInGroup)
                {
                    Vector3[] corners = new Vector3[4];
                    view.TileTransform.GetWorldCorners(corners); 
                    
                    for (int j = 0; j < 4; j++)
                    {
                        // 🌟 核心：先将世界坐标转换到当前面板的本地像素坐标系，再算最大最小值！
                        Vector3 localCorner = transform.InverseTransformPoint(corners[j]);
                        minX = Mathf.Min(minX, localCorner.x);
                        maxX = Mathf.Max(maxX, localCorner.x);
                        minY = Mathf.Min(minY, localCorner.y);
                        maxY = Mathf.Max(maxY, localCorner.y);
                    }
                }
                // 2. 计算出纯正的本地中心点和宽高
                Vector2 localCenter = new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f);
                float localWidth = maxX - minX;
                float localHeight = maxY - minY;
                
                // 添加额外留白（避免紧贴字块边缘）
                // 3. 应用缩放 (targetScale) 和留白 (padding)
                float singleTileW = viewsInGroup[0].TileTransform.rect.width;
                float singleTileH = viewsInGroup[0].TileTransform.rect.height;
                Vector2 baseSize = new Vector2(localWidth + padding * 2, localHeight + padding * 2);
                Vector2 expandedSize = new Vector2(baseSize.x + singleTileW * 0.05f, baseSize.y + singleTileH * 0.05f);
                Vector2 rippleTargetSize = new Vector2(baseSize.x + singleTileW * .65f, baseSize.y + singleTileH * .65f);
                
                // ======== 框1：外部散开的光晕框 (先生成，垫在下层) ========
                GameObject outerShine = GroupShinePool.GetObject(transform);
                RectTransform outerRT = outerShine.GetComponent<RectTransform>();
                outerRT.SetAsLastSibling(); 
                outerRT.anchorMin = new Vector2(0.5f, 0.5f);
                outerRT.anchorMax = new Vector2(0.5f, 0.5f);
                outerRT.pivot = new Vector2(0.5f, 0.5f);
                outerRT.localPosition = localCenter; 
                outerRT.localScale = Vector3.one;
                outerRT.sizeDelta = baseSize; // 初始和内部框一样大
                
                // ======== 框2：内部包裹常亮框 (后生成，盖在顶层) ========
                GameObject innerShine = GroupShinePool.GetObject(transform);
                RectTransform innerRT = innerShine.GetComponent<RectTransform>();
                innerRT.SetAsLastSibling(); 
                innerRT.anchorMin = new Vector2(0.5f, 0.5f);
                innerRT.anchorMax = new Vector2(0.5f, 0.5f);
                innerRT.pivot = new Vector2(0.5f, 0.5f);
                innerRT.localPosition = localCenter; 
                innerRT.localScale = Vector3.one;
                innerRT.sizeDelta = baseSize; // 大小永远固定，紧紧包裹词组
                
                allShineInstances.Add(outerShine);
                allShineInstances.Add(innerShine);
                
                Sequence shineSeq = DOTween.Sequence();
                Image outerImg = outerShine.GetComponent<Image>();
                Image innerImg = innerShine.GetComponent<Image>();
          
                if (outerImg != null && innerImg != null)
                {
                    Color oc = outerImg.color; oc.a = 0f; outerImg.color = oc;
                    Color ic = innerImg.color; ic.a = 0f; innerImg.color = ic;
                    float fadeInTime = 0.2f;
                    float fadeOutTime = 0.3f;
                    // 1. 同一时间显现 (淡入)
                    // 内部框：完全高亮 (Alpha = 1)
                    shineSeq.Insert(0f, innerImg.DOFade(1f, fadeInTime));
                    // 外部框：颜色浅一点 (Alpha = 0.45)
                    shineSeq.Insert(0f, outerImg.DOFade(0.25f, fadeInTime));
                    // 2. 外部框慢慢变大散开 (动作贯穿整个特效展示时间，缓动让散开先快后慢)
                    shineSeq.Insert(0f, innerRT.DOSizeDelta(expandedSize, effectDuration).SetEase(Ease.OutQuad));
                    shineSeq.Insert(0f, outerRT.DOSizeDelta(rippleTargetSize, effectDuration).SetEase(Ease.OutQuad));
                    // 3. 结束：跟着缩小然后再消失 (在动画快结束的前0.2秒触发)
                    shineSeq.Insert(effectDuration - fadeOutTime, innerImg.DOFade(0f, fadeOutTime).SetEase(Ease.InQuad));
                    shineSeq.Insert(effectDuration - fadeOutTime, outerImg.DOFade(0f, fadeOutTime).SetEase(Ease.InQuad));
                }
            
                // ==========================================
                // 🌟 核心修改：寻找没有相邻格子的位置作为飘字原点
                // ==========================================
                int dir = viewsInGroup[0].chesspiece.direction; // 1横向，0纵向
                ChessView bestView = viewsInGroup.Last(); // 默认最后一个格子
                // 倒序遍历，寻找最边缘、最空旷的格子
                for (int i = viewsInGroup.Count - 1; i >= 0; i--)
                {
                    var v = viewsInGroup[i];
                    bool hasNeighbor = false;
                
                    if (dir == 1) // 横向，飘字在上方，检查上方 (Col + 1) 是否有格子
                    {
                        hasNeighbor = GridList.ContainsKey((v.Row, v.Col + 1));
                    }
                    else // 纵向，飘字在右方，检查右方 (Row + 1) 是否有格子
                    {
                        hasNeighbor = GridList.ContainsKey((v.Row + 1, v.Col));
                    }

                    if (!hasNeighbor)
                    {
                        bestView = v;
                        break; // 找到满足条件的格子，立刻选定
                    }
                }
                // 计算这次得分
                int finalGroupScore = groupActualScores[groupIdx];
                int currentComboInSystem = ChessStageController.Instance.PuzzleComboCount;
                GamePlayArea.ShowBoardFloatingScore(bestView.transform, dir, finalGroupScore, currentComboInSystem >= 2);
                
                AudioManager.Instance.PlaySoundEffect("Complete");

                foreach (var view in viewsInGroup)
                {
                    
                        view.SetTileState(TileState.Success, false);
                        // 取消框选，并设置通关大满贯标记！
                        view.SetChoose(false); 
                        view.IsOK = true; 
                        
                        // 🌟 调用动画：注意这里传进去的值是 1.2f (总时长)
                        view.PlaySuccessAnimation(effectDuration, () => {
                            view.UpdateTile(true); 
                        });
                }
                yield return new WaitForSeconds(0.3f);
                // 🌟 【重构点】：在能量爆发的高潮，触发周围的冰块碎裂和花朵绽放！
                bool hasBloomingFlower = ProcessFlowerBlooming(viewsInGroup);
                bool hasIceBroken = BreakAdjacentIce(viewsInGroup);
                bool hasDeadlockIceBroken = false;
            
                if (!hasIceBroken)
                {
                    hasDeadlockIceBroken = CheckAndBreakDeadlockIce();
                }
                if (hasDeadlockIceBroken) hasIceBroken = true;

                // 如果有冰块破碎或花朵绽放，稍微延长等待时间，让附加特效飞一会儿
                if (hasBloomingFlower || hasIceBroken)
                {
                    yield return new WaitForSeconds(0.25f); 
                }
                
                if (currentComboInSystem >= 2 && !hasPlayedComboSoundThisChain)
                {
                    // ⚠️ 请将 "ComboHit" 替换为你工程中的真实连击音效名称
                    // 如果你有不同阶段的连击音效，也可以写成：$"ComboHit_{currentComboInSystem}"
                    AudioManager.Instance.PlaySoundEffect("ComboHit",0,1); 
                    hasPlayedComboSoundThisChain = true; // 马上上锁，在本次连击持续时间内绝对不再播
                }
        }
        // ==========================================
        // 🔴 再统一播放【错误组】的红色抖动
        // ==========================================
        foreach (var viewsInGroup in errorGroups)
        {
            foreach (var view in viewsInGroup)
            {
                // 🌟 这是一个安全兜底：如果手速极快把它改对（变成 Success）了，就不抖动它了，但绝对不会触发加分和粒子！
                if (view.CurrState is TileState.Error or TileState.Default)
                {
                    if (view.chesspiece.hasLeaf)
                    {
                        // 播放枯萎动画并隐藏
                        // view.PlayLeafFillFailedAnim();
                        
                        // 永久剥夺本关后续生成树叶的权利！
                        // ChessStageController.Instance.IsLeafDeadThisLevel = true; 
                        GenerateNextLevelLeaf();
                    }
                    view.UpdateTile(true);
                    view.PlayError(true);
                }
            }
            yield return new WaitForSeconds(0.1f);
        }
        if (!GameOver && GridList.Values.Any(v => !v.IsOK)) 
        {
            GenerateNextLevelLeaf();
        }
        if (!GameOver && (selecteTile == null || selecteTile.CurrState == TileState.Success))
        {
            SearchNextTile();
            Debug.LogWarning("[光标终审重定向] 全盘消除与碎冰静止，执行全局唯一一次寻路。");
        }
        yield return new WaitForSeconds(effectDuration  + 0.1f); 
        // 4. 销毁所有的闪光底框，打扫战场
        foreach (var shine in allShineInstances)
        {
            if (shine != null) GroupShinePool.ReturnObjectToPool(shine.GetComponent<PoolObject>());
        }
        // 5. 动画和清理全干完后，检查是否通关！
        yield return CheckCompleted();
        CheckAndTriggerLastGroupFocus();
    }
    /// <summary>
    /// 死锁保护破冰：当所有未完成成语都被冰完全覆盖时，
    /// 随机选取一个未完成成语，强制打碎它上面的所有冰块，防止玩家无法继续操作。
    /// 返回 true 表示触发并执行了死锁破冰。
    /// </summary>
    private bool CheckAndBreakDeadlockIce()
    {
        // 如果由于某些原因（比如自动完成道具绕过了预判逻辑），进行兜底查找
        if (_pendingDeadlockGroup == null)
        {
            var allGroups = GamePlayArea.CurrStageInfo.PhraseGroups;
            List<PhraseGroup> unsolvedGroups = new List<PhraseGroup>();
            foreach (var g in allGroups)
            {
                bool isSolved = g.chesspieces.All(p => GridList.TryGetValue((p.row, p.col), out var v) && (v.CurrState == TileState.Success || v.IsOK));
                if (!isSolved) unsolvedGroups.Add(g);
            }
            if (unsolvedGroups.Count == 0) return false;
            
            bool isAllCoveredByIce = unsolvedGroups.All(g =>
                 g.chesspieces.Any(p => GridList.TryGetValue((p.row, p.col), out var v) && v.chesspiece.hasIce && !v.iceLogicBroken));
            if (!isAllCoveredByIce) return false;
            _pendingDeadlockGroup = unsolvedGroups.OrderByDescending(g => 
                g.chesspieces.Count(p => GridList.TryGetValue((p.row, p.col), out var v) && v.chesspiece.hasIce)
            ).First();
        }
        // var allGroups = GamePlayArea.CurrStageInfo.PhraseGroups;
        // List<PhraseGroup> unsolvedGroups = new List<PhraseGroup>();
        bool iceBroken = false;
        foreach (var p in _pendingDeadlockGroup.chesspieces)
        {
            if (GridList.TryGetValue((p.row, p.col), out var v) && v.chesspiece.hasIce)
            {
                v.chesspiece.hasIce = false;
                v.iceLogicBroken = false; // 顺手重置逻辑标记
                ChessStageController.Instance.ModifyChreepiece(v.chesspiece);
                StartCoroutine(v.PlayIceBreakAnim());
                iceBroken = true;
            }
        }

        _pendingDeadlockGroup = null; // 碎冰完成，清理缓存
        if (iceBroken)
        {
            Debug.Log($"[破冰系统] 触发死锁保护！");
            AudioManager.Instance.PlaySoundEffect("IceBreak",0,1); // 可选：补个碎冰音效
        }
        return iceBroken;
    }
    /// <summary>
    /// 预先执行逻辑破冰：标记所有关联冰块格子和死锁保护格子为 iceLogicBroken，
    /// 让光标可以立即选中这些格子，但视觉冰块仍然保留。
    /// 返回 true 表示至少有一个格子被标记（后续应调用视觉破冰播放动画）。
    /// </summary>
    private bool PreBreakIceLogic(List<List<ChessView>> completedGroupViews)
    {
        bool adjacentBrokeAny = false;
        for (int groupIdx = 0; groupIdx < completedGroupViews.Count; groupIdx++)
        {
            var viewsInGroup = completedGroupViews[groupIdx];
            HashSet<PhraseGroup> associatedGroups = new HashSet<PhraseGroup>();
            // 1. 收集被完成词组的直接交叉组和四向相邻组
            foreach (var v in viewsInGroup)
            {
                var myGroups = GetChessGroups(v.Row, v.Col);
                foreach (var g in myGroups) associatedGroups.Add(g);
            }

            // 2. 遍历所有关联词组的格子，只要有冰块就全部打碎！
            HashSet<ChessView> tilesToBreak = new HashSet<ChessView>();
            foreach (var group in associatedGroups)
            {
                foreach (var p in group.chesspieces)
                {
                    if (GridList.TryGetValue((p.row, p.col), out var view))
                    {
                        tilesToBreak.Add(view);
                    }
                }
            }

            // 把刚完成的组的格子也算进去兜底
            foreach (var v in viewsInGroup) tilesToBreak.Add(v);
            // 如果邻居有冰块，打碎它！
            foreach (var neighbor in tilesToBreak)
            {
                if (neighbor.chesspiece.hasIce)
                {
                    neighbor.iceLogicBroken = true;
                    adjacentBrokeAny = true; // 🌟 标记：按规则解开了冰块！
                }
            }
        }
        
        if (adjacentBrokeAny) return true;
        var allGroups = GamePlayArea.CurrStageInfo.PhraseGroups;
        List<PhraseGroup> unsolvedGroups = new List<PhraseGroup>();
        foreach (var g in allGroups)
        {
            bool isSolved = g.chesspieces.All(p => 
                GridList.TryGetValue((p.row, p.col), out var v) && 
                (v.CurrState == TileState.Success || v.IsOK));
            if (!isSolved) unsolvedGroups.Add(g);
        }

        if (unsolvedGroups.Count > 0)
        {
            bool isDeadlock = unsolvedGroups.All(g =>
                g.chesspieces.Any(p => GridList.TryGetValue((p.row, p.col), out var v) && v.chesspiece.hasIce && !v.iceLogicBroken ));

            if (isDeadlock)
            {
                _pendingDeadlockGroup = unsolvedGroups.OrderByDescending(g => 
                    g.chesspieces.Count(p => GridList.TryGetValue((p.row, p.col), out var v) && v.chesspiece.hasIce)
                ).First();
                foreach (var p in _pendingDeadlockGroup.chesspieces)
                {
                    if (GridList.TryGetValue((p.row, p.col), out var v) && v.chesspiece.hasIce && !v.iceLogicBroken)
                    {
                        v.iceLogicBroken = true;
                    }
                }
                Debug.Log($"[预破冰] 检测到死锁，预标记词组 {_pendingDeadlockGroup.id} 的冰块");
            }
        }
        return true;
    }
    /// <summary>
/// 🌟 新增：预先执行逻辑绽放。算出哪些花朵即将消除，打上 flowerLogicBroken 标记
/// </summary>
private void PreBreakFlowerLogic(List<List<ChessView>> completedGroupViews)
{
    var currStageData = ChessStageController.Instance.CurrStageData;
    var flowerConfig = ChessStageController.Instance.FlowerConfig;

    // 🌟 关键点：将已经被逻辑破冰 (iceLogicBroken) 的格子也纳入有效花朵池！
    var allValidFlowers = GridList.Values.Where(v => v.chesspiece.hasFlower && !v.chesspiece.hasIce).ToList();
    if (allValidFlowers.Count == 0) return;

    HashSet<ChessView> flowersToBloom = new HashSet<ChessView>();
    
    // 1. 本组成语如果有花，必须全部消除
    foreach (var v in completedGroupViews.SelectMany(g => g).Where(x => x.chesspiece.hasFlower && !x.chesspiece.hasIce))
    {
        flowersToBloom.Add(v);
    }
    
    // 2. 判断是否是倒数第2个成语
    int totalWords = ChessStageController.Instance.CurrStageInfo.PhraseGroups.Count;
    int solvedWords = currStageData.FoundTargetPuzzles.Count; 
    bool isSecondToLast = (totalWords - solvedWords) <= 1;

    if (isSecondToLast)
    {
        foreach (var f in allValidFlowers) flowersToBloom.Add(f);
    }
    else
    {
        int initY = flowerConfig?.InitNumber > 0 ? flowerConfig.InitNumber : 2;
        int minY = flowerConfig?.MinNumber > 0 ? flowerConfig.MinNumber : 1;
        int currentY = Mathf.Max(minY, initY - (currStageData.FlowerActionCount / 2));
        int remainingQuota = currentY - flowersToBloom.Count;

        if (remainingQuota > 0 && completedGroupViews.Count > 0)
        {
            float centerRow = completedGroupViews[0].Average(v => (float)v.Row);
            float centerCol = completedGroupViews[0].Average(v => (float)v.Col);
            var remainingFlowers = allValidFlowers.Where(f => !flowersToBloom.Contains(f)).ToList();
            var nearestTiles = remainingFlowers.OrderBy(f => 
                Mathf.Abs(f.Row - centerRow) + Mathf.Abs(f.Col - centerCol)
            ).Take(remainingQuota).ToList();
            foreach (var targetFlower in nearestTiles) flowersToBloom.Add(targetFlower);
        }
    }

    // 3. 预先打上逻辑破碎标记，供寻路算法识别
    foreach (var f in flowersToBloom)
    {
        f.flowerLogicBroken = true;
    }
}
     /// <summary>
    /// 交叉定义：
    /// emptyGroups 中**任意组**的**第一个可用空格**（None/Error）
    /// 空格子所在词组 与 选中词组 只要 共享过同一个字块（不论状态），就算交叉。
    /// </summary>
    public bool HasCrossWithSelected(ChessView candidate, List<PhraseGroup> emptyGroups, List<PhraseGroup> groups)
    {
        // ① 真正的交叉组：与选中组共享过字块
        var crossGroups = emptyGroups
            .Where(eg => eg.chesspieces.Any(ep => groups
                .SelectMany(sg => sg.chesspieces)
                .Contains(ep)))
            .ToList();

        // ② 在这些交叉组里，当前格子是否是第一个可用空格
        return crossGroups.Any(g =>
        {
            int idx = g.chesspieces.FindIndex(p =>
                GridList.TryGetValue((p.row, p.col), out var view) &&
                view.CurrState is TileState.None or TileState.Error);
            return idx >= 0 && g.chesspieces[idx].Equals(candidate.chesspiece);
        });
    }
    /// <summary>
    /// 计算空格子到选中格子的曼哈顿距离（|Δrow| + |Δcol|）
    /// </summary>
    public int ManhattanDistance(Chesspiece emptyPiece, Chesspiece selectedPiece)
    {
        return Mathf.Abs(emptyPiece.row - selectedPiece.row) +
               Mathf.Abs(emptyPiece.col - selectedPiece.col);
    }
    /// <summary>
    /// 查找下一个空白格子
    /// </summary>
    public bool SearchNextTile()
    {
        // 1. 同步计算并挪动底层基础操作光标（继续保持原有的同组优先手感）
        ChessView bestChoice = GetBestNextTile(out PhraseGroup nextActiveGroup);
        if (bestChoice == null) return true;
        if (nextActiveGroup != null) 
        {
            _lastActiveDirection = nextActiveGroup.direction; // 锁定最新跳转流向
        }
        SetCheckView(bestChoice, bestChoice.CurrState is not TileState.Error);
        return true;
    }
    private int _lastActiveDirection = 1;
    /// <summary>
   /// 核心算法：基于场景梯队与严格决策树的智能寻路引擎
   /// 
   /// =========================================================================================
   /// 📝 【光标寻路规则注册表 (Rule Registry) - 对应架构流程图】
   /// 🌟 极客坐标系声明：Row = Y (向下递增), Col = X (向左递增/向右递减)
   /// 🌟 绝对方向声明：direction = 1 (横向), direction = 0 (纵向)
   /// 🌟 绝对视觉序公式：(100 - Col) * 1000 + Row (确保遵循人类“从上到下、从左到右”的阅读习惯)
   /// 
   /// [主导词组推断 (Active Group Inference)] - 🌟 动量隔离与绝对防拐弯
   /// 引入【HasMomentum】概念：仅在顺延打字时生效，彻底切断幽灵方向对自由点击的劫持！
   /// - 0. 绿字继承者 (IsAutoJumpHeir) [终极护盾]: 包含Success绿字 + 前置空已填满。彻底粉碎幽灵方向对绿字跳跃后的伪装劫持！
   /// - 1. 动量连贯 (IsImmediateAndDirMatch): 锚点 + UI方向一致 + 紧邻空位。全宇宙最高顺延优先级！
   /// - 2. 锚点连贯兜底 (IsImplicitContinuous): 锚点 + 紧邻空位。无视底层方向强行防拐弯！
   /// - 3. 动量跨栏 (IsForwardAndDirMatch): 锚点 + UI方向一致 + 远端空位。强行跳跃防堵死！
   /// - 4. 锚点跨栏兜底 (IsImplicitForward): 锚点 + 远端空位。
   /// - 5. 横向特权 (IsHorizontal): 🌟 几何推断(Row跨度>Col跨度)。无锚点(如首步点击)时，绝对优先横向！
   /// - 6. 显式连贯 (IsExplicitContinuous): 无锚点时的 UI 顺延。
   /// - 7. 显式跨栏 (IsExplicitForward): 无锚点时的 UI 跨栏。
   /// - 8. 完成度抗衡 (FillCount): 跳跃到新交叉点无惯性时，选择已填字数(完成度)最高的词组，先易后难，强行扭转方向残留。
   /// - 9. 紧邻空位保底 (HasImmediateEmpty): 任何紧邻光标空位优先。
   /// - 10. 远端顺延保底 (HasForwardEmpty) 
   /// - 11. 惯性方向兜底 (IsDirMatch)。
   /// 
   /// [评估单元隔离 (Strict Group Isolation)] - 交叉防串台规则
   /// - 候选目标是 (格子, 候选词组) 的组合，杜绝交叉格子借助无关词组属性作弊提权。
   /// 
   /// [层级 A：绝对过滤区 (Filter)] - 🌟 严格死路拦截
   /// - A1 (格子级拦截): 仅允许 None/Error 状态，且格子自身未被冰块覆盖的格子。
   /// - A2 (词组级拦截): 若候选词组中包含【任何未破冰的格子】，视为“死路”，该词组直接淘汰！绝对不向玩家推荐无法完成的成语。
   ///
   /// [层级 B：场景梯队区 (Tier)] - 🌟 决定大方向的绝对顺位 (T1-T4)
   /// 核心原则：同向保连贯，异向找首空:
   /// - B1 & B2 (T1/T2 分流规则):
   ///    - 场景 1【跨词智能劫持】：若 selecteTile 刚填满变绿 (CurrState == Success)。
   ///      -> 触发【跨词首位统筹】：代表玩家准备解答新词，此时取消顺延/回头判定，新词全组空位统一为 T1，完美交由底层视觉顺位选出新词的第一个空。
   ///      -> 🛡️ [防跳尾巴]: 解决“苦中作乐”拐弯时，因误判顺延导致错误跳到末尾空格的 Bug。
   ///    - 场景 2【同向连续打字】：若在正常打字中 (CurrState != Success)。
   ///      -> 执行【严格分流】：光标前方为空格(candidateIndex > currentIndex) 划入 T1(主词顺延)；光标后方为空格 划入 T2(主词回头)。
   ///      -> 🚫 [防劫持净化]: 纯净无杂质，绝不回头！彻底废除“首字破例特权”，解决“良辰吉日”打断往后填字心流的 Bug；同时解决“死马当活马医”跨栏跳跃的 Bug。
   /// - B3 (T3): 交叉成语 -> 包含与当前主词相交的成语，统一交由瀑布树排位。
   /// - B4 (T4): 全局寻路 -> 无交叉时，启动全局搜索新词，优先最易补全的词。
   ///    - [T4 预期行为补充]: T4 触发时常伴随大跨度空间跳跃。引擎会优先遵循 C1-Pro 原则去寻找全盘最容易补全的词（先易后难心流），而非物理距离最近的词。
   /// - B6 (+10): 避让花朵 -> 若候选组合包含未破花朵，梯队强制降级 (+10)。
   /// 
   /// [层级 C：瀑布决策树 (Cascading Tie-Breakers)] - 🌟 完美人类直觉模拟 (免疫编辑器乱序)
   /// 严格按顺序逐级淘汰, 打破唯数量论的空间撕裂, 抹平视觉与数学的代沟:
   /// - C1-Pro (IsEasyWin): 【一步之遥特权】仅剩1个空格的词组，享有无视距离的绝对优先级。
   /// - C-FirstEmpty (FirstEmptyIndex): 【先易后难统筹】全局寻找首个空位最靠后的词组！
   /// - C2-Zone (ZoneDistance): 【视觉区块归拢】测量距离【最近空格】的距离并除以8！强行把距离相近的交叉词拉入平局，防止微小数学差异破坏阅读直觉！
   /// - C3-Pro (GroupStartCoord): 【视觉起源霸权】同区块内，优先阅读最左上方的词组！完美制裁独立 L 型，强制寻顶！
   /// - C5 (IsHorizontal): 【十字特权】起点相同时，顺应“先横后竖”(几何跨度判定法，绝对精准)。
   /// - C1-Sub (MinEmptySpaces): 视觉区块相同时比拼空格数，再去计较差2个还是差3个空。
   /// - C2-Raw (GroupDistance): 【真实距离兜底】词组物理最短距离微调。 (修复2空与3空的微弱差异导致乱跳，距离权重提前)。
   /// - C3 (IsSameDirection): 同向优先 -> 候选词组方向与主导词方向一致。
   /// - C4 (CoordinateScore): 【绝对视觉归位】大局已定后，光标无条件、强制吸附到选定词组最左上方 (视觉序最小) 的空格！彻底抹杀因抄近道而落点在交叉点的 Bug！
   /// =========================================================================================
   /// </summary>
   /// <summary>
   /// 核心算法：基于场景梯队与严格决策树的智能寻路引擎
   /// </summary>
   /// <summary>
    /// 核心算法：基于场景梯队与严格决策树的智能寻路引擎（主词防劫持版）
    /// </summary>
    /// <summary>
    /// 核心算法：基于场景梯队与严格决策树的智能寻路引擎（完美跨词首空版）
    /// </summary>
    private ChessView GetBestNextTile(out PhraseGroup chosenGroup)
    {
        chosenGroup = null;
        
        var candidates = GridList.Values
            .Where(v => (v.CurrState == TileState.None || v.CurrState == TileState.Error) && 
                        (!v.chesspiece.hasIce || v.iceLogicBroken))
            .ToList();

        if (!candidates.Any())
        {
            var globalIceTile = GridList.Values
                .FirstOrDefault(v => (v.CurrState == TileState.None || v.CurrState == TileState.Error) && 
                                     v.chesspiece.hasIce && !v.iceLogicBroken);
            if (globalIceTile != null)
            {
                globalIceTile.iceLogicBroken = true;
                candidates = GridList.Values
                    .Where(v => (v.CurrState == TileState.None || v.CurrState == TileState.Error) && 
                                (!v.chesspiece.hasIce || v.iceLogicBroken))
                    .ToList();
            }
        }

        if (!candidates.Any()) return null;

        // 1. 获取当前光标所在的所有词组
        List<PhraseGroup> currentGroups = selecteTile != null ? GetChessGroups(selecteTile.Row, selecteTile.Col).ToList() : new List<PhraseGroup>();

        // 找出本次操作中所有“已经填满”的词组集合
        List<PhraseGroup> completedGroupsThisMove = new List<PhraseGroup>();
        foreach (var g in currentGroups)
        {
            bool isFull = g.chesspieces.All(p => {
                return GridList.TryGetValue((p.row, p.col), out var v) && 
                       !(v.CurrState == TileState.None || v.CurrState == TileState.Error);
            });
            if (isFull) completedGroupsThisMove.Add(g);
        }

        // ========================================================================
        // 🌟🌟🌟【核心修正点：寻路心流状态切换】🌟🌟🌟
        // 如果有成语在这一帧通关了，代表旧词打字流结束，强制清空 activeGroups，
        // 从而令 isActiveGroupFull 为 true，彻底关闭微观顺延(T1~T4)，全面激活宏观跳转(T5~T7)。
        // ========================================================================
        bool hasJustCompleted = completedGroupsThisMove.Count > 0;
        List<PhraseGroup> activeGroups = hasJustCompleted ? new List<PhraseGroup>() : currentGroups.Where(g => !completedGroupsThisMove.Contains(g)).ToList();
        bool isActiveGroupFull = activeGroups.Count == 0;

        var evaluatedPairs = candidates.SelectMany(tile => 
        {
            var groups = GetChessGroups(tile.Row, tile.Col).ToList();
            return groups.Select(group => new { Tile = tile, EvalGroup = group });
        })
        .Select(pair =>
        {
            var candidateTile = pair.Tile;
            var evalGroup = pair.EvalGroup;
            var sortedPieces = evalGroup.chesspieces.OrderBy(p => (100 - p.col) * 1000 + p.row).ToList();
            
            int tier = 99;                  
            string reason = "硬性过滤";      

            var emptyPiecesInEval = sortedPieces.Where(p => 
                GridList.TryGetValue((p.row, p.col), out var v) && 
                (v.CurrState == TileState.None || v.CurrState == TileState.Error) &&
                (!v.chesspiece.hasIce || v.iceLogicBroken)
            ).ToList();
            
            int minEmptySpaces = emptyPiecesInEval.Count;

            int firstEmptyIndexInGroup = -1;
            if (emptyPiecesInEval.Any()) {
                firstEmptyIndexInGroup = sortedPieces.FindIndex(p => p.Equals(emptyPiecesInEval.First()));
            }

            int candidateIndex = sortedPieces.FindIndex(p => p.Equals(candidateTile.chesspiece));
            bool isFirstEmptyInGroup = firstEmptyIndexInGroup != -1 && candidateIndex == firstEmptyIndexInGroup;

            // [层级 A：词组级死路拦截] 
            bool hasUnbrokenIceInGroup = sortedPieces.Any(p => 
                GridList.TryGetValue((p.row, p.col), out var v) && 
                v.chesspiece.hasIce && !v.iceLogicBroken
            );
            if (activeGroups.Contains(evalGroup)) 
            {
                // [微观寻路]：当前成语未圆满时的组内流向（完美继承防拐弯逻辑）
                // 🌟 核心修改：赋予当前解答组“冰块豁免权”！
                // 只要光标已经在该词组内（玩家手动选中），就允许在露出的空格之间正常顺延。
                bool isCurrentLine = evalGroup.direction == _lastActiveDirection;
                int currentIndexInActive = sortedPieces.FindIndex(p => p.Equals(selecteTile.chesspiece));
                
                if (candidateIndex > currentIndexInActive)
                {
                    tier = isCurrentLine ? 1 : 3; 
                    reason = isCurrentLine ? "T1: 主行组内顺延" : "T3: 交叉行组内顺延";
                }
                else
                {
                    tier = isCurrentLine ? 2 : 4; 
                    reason = isCurrentLine ? "T2: 主行回头补空" : "T4: 交叉行回头补空"; 
                }
            }
            else if (hasUnbrokenIceInGroup)
            {
                tier = 99; 
                reason = "过滤：包含未破冰格子，死路拦截";
            }
            else if (activeGroups.Contains(evalGroup)) 
            {
                // [微观寻路]：当前成语未圆满时的组内流向（完美继承防拐弯逻辑）
                bool isCurrentLine = evalGroup.direction == _lastActiveDirection;
                int currentIndexInActive = sortedPieces.FindIndex(p => p.Equals(selecteTile.chesspiece));
                
                if (candidateIndex > currentIndexInActive)
                {
                    tier = isCurrentLine ? 1 : 3; 
                    reason = isCurrentLine ? "T1: 主行组内顺延" : "T3: 交叉行组内顺延";
                }
                else
                {
                    tier = isCurrentLine ? 2 : 4; 
                    reason = isCurrentLine ? "T2: 主行回头补空" : "T4: 交叉行回头补空"; 
                }
            }
            else if (isActiveGroupFull)
            {
                // [宏观寻路]：成语圆满通关后，或者无关联时的跨词跳转
                // 🌟 注意：这里卡死了 isFirstEmptyInGroup，所有非首空格子一律抹杀
                if (isFirstEmptyInGroup) 
                {
                    bool isCrossingAtLastFilledSpace = selecteTile != null && evalGroup.chesspieces.Any(p => p.row == selecteTile.Row && p.col == selecteTile.Col);
                    bool isCrossingWithAnySuccessGroup = completedGroupsThisMove.Any(cg => 
                        evalGroup.chesspieces.Any(cp => cg.chesspieces.Any(acp => acp.row == cp.row && acp.col == cp.col))
                    );
                    
                    if (isCrossingAtLastFilledSpace)
                    {
                        tier = 5; reason = "T5: 最后一个空格直接交叉优先跳转【新词首空】";
                    }
                    else if (isCrossingWithAnySuccessGroup)
                    {
                        tier = 6; reason = "T6: 联合交叉优先跳转【新词首空】";
                    }
                    else
                    {
                        tier = 7; reason = "T7: 全局无关新词跳转【新词首空】";
                    }
                }
                else
                {
                    tier = 99; reason = "过滤：非新词首个空格位，宏观跳转时直接淘汰";
                }
            }
            else
            {
                tier = 99; reason = "过滤：主词未圆满，强制锁定主干路径";
            }

            bool hasUnbrokenFlower = sortedPieces.Any(p => 
                GridList.TryGetValue((p.row, p.col), out var v) && v.chesspiece.hasFlower && !v.flowerLogicBroken);
            if (hasUnbrokenFlower && tier < 99)
            {
                tier += 10; 
                reason += " [降级:避让花朵]";
            }
            
            int groupDistance = 999;
            if (selecteTile != null && emptyPiecesInEval.Any())
            {
                groupDistance = emptyPiecesInEval.Min(p => ManhattanDistance(p, selecteTile.chesspiece));
            }

            var trueStart = sortedPieces.First();
            int groupStartCoordinate = (100 - trueStart.col) * 1000 + trueStart.row;
            int isHorizontal = evalGroup.direction == 1 ? 1 : 0;
            int candidateCoordinateScore = (100 - candidateTile.Col) * 1000 + candidateTile.Row;

            return new
            {
                View = candidateTile,
                EvalGroup = evalGroup,
                Tier = tier,
                Reason = reason,
                MinEmptySpaces = minEmptySpaces,
                FirstEmptyIndexInGroup = firstEmptyIndexInGroup,
                GroupDistance = groupDistance,
                GroupStartCoordinate = groupStartCoordinate,
                IsHorizontal = isHorizontal,
                IndexInGroup = candidateIndex,
                CandidateCoordinateScore = candidateCoordinateScore  
            };
        })
        .Where(x => x.Tier < 99) 
        // ------------------------------------------------------------------
        // 🏆 严格权重级联决策树
        // ------------------------------------------------------------------
        .OrderBy(x => x.Tier)                             
        .ThenBy(x => x.MinEmptySpaces)                    
        .ThenByDescending(x => x.FirstEmptyIndexInGroup)  
        .ThenBy(x => x.GroupDistance)                     
        .ThenByDescending(x => x.IsHorizontal)            
        .ThenBy(x => x.GroupStartCoordinate)              
        .ThenBy(x => x.IndexInGroup)                      
        .ThenBy(x => x.CandidateCoordinateScore)          
        .ToList();

        var winner = evaluatedPairs.FirstOrDefault();
        if (winner != null)
        {
            chosenGroup = winner.EvalGroup;
        }

        return winner?.View;
    }
    /// <summary>
    /// 🌟 规范新增：事件驱动型全局树叶自适应刷新器
    /// 只有在开局、消除成功起飞、填满失败枯萎时，才会被精准调用一次！
    /// </summary>
    public void GenerateNextLevelLeaf()
    {
        bool isLeafLevel = ChessStageController.Instance.CheckLeafMechanic(ChessStageController.Instance.CurrentStage, out _);
        // bool isLeafAlive = !ChessStageController.Instance.IsLeafDeadThisLevel;

        if (!isLeafLevel)
        {
            // 如果树叶死掉了或者是普通关，强行确保全盘干净无树叶
            foreach (var cell in GridList.Values) if (cell.chesspiece.hasLeaf) cell.ShowLeaf(false);
            return;
        }
        bool hasExistingLeaf = GridList.Values.Any(v => v.chesspiece.hasLeaf || v.isPendingLeafFlight);
        if (hasExistingLeaf) return;
        // 1. 安全前置清理
        foreach (var cell in GridList.Values) if (cell.chesspiece.hasLeaf) cell.ShowLeaf(false);

        // 2. 核心判定一：开局特权
        bool isFirstLeafEntry = (ChessStageController.Instance.LeafGenCounter == 0);
        if (isFirstLeafEntry && selecteTile != null && selecteTile.CurrState == TileState.Check || selecteTile.CurrState == TileState.None)
        {
            // ChessStageController.Instance.LeafGenCounter++;
            selecteTile.ShowLeaf(true); // 首次进入强制在初始选中的光标格子上诞生
            return;
        }

        // 3. 核心判定二：自适应阶段对冲机制
        int curLeaves = ChessStageController.Instance.CurrStageData.CollectedLeaves;
        int distanceToGold = 2 - curLeaves;
        int distanceToPupa = 5 - curLeaves;
        int distanceToLotus = 10 - curLeaves;

        int minDistanceToReward = int.MaxValue;
        if (distanceToGold > 0) minDistanceToReward = Mathf.Min(minDistanceToReward, distanceToGold);
        else if (distanceToPupa > 0) minDistanceToReward = Mathf.Min(minDistanceToReward, distanceToPupa);
        else if (distanceToLotus > 0) minDistanceToReward = Mathf.Min(minDistanceToReward, distanceToLotus);

        bool preferMoreEmpty = (minDistanceToReward == 1);

        // 收集盘面上所有还没解开、且没有被冰块100%封印的成语候选组
        var validGroups = GamePlayArea.CurrStageInfo.PhraseGroups.Where(g => 
        {
            bool allSuccess = g.chesspieces.All(p => GridList.TryGetValue((p.row, p.col), out var v) && v.CurrState == TileState.Success);
            if (allSuccess) return false;

            bool allIce = g.chesspieces.All(p => GridList.TryGetValue((p.row, p.col), out var v) && v.chesspiece.hasIce);
            if (allIce) return false;

            bool hasValidEmpty = g.chesspieces.Any(p => GridList.TryGetValue((p.row, p.col), out var v) && (v.CurrState == TileState.None || v.CurrState == TileState.Check || v.CurrState == TileState.Error));
            return hasValidEmpty;
        }).ToList();

        if (validGroups.Any())
        {
            // 依据你的策略对冲排序
            var sortedGroups = validGroups.Select(g => {
                int emptyCount = g.chesspieces.Count(p => GridList.TryGetValue((p.row, p.col), out var v) && (v.CurrState == TileState.None || v.CurrState == TileState.Check || v.CurrState == TileState.Error));
                bool hasFlower = g.chesspieces.Any(p => p.hasFlower);
                bool hasIce = g.chesspieces.Any(p => p.hasIce);
                return new { Group = g, EmptyCount = emptyCount, HasFlower = hasFlower, HasIce = hasIce };
            })
            .OrderBy(x => x.EmptyCount)
            .ThenByDescending(x => x.HasFlower ? 1 : 0)
            .ThenBy(x => x.HasIce ? 1 : 0)
            .ToList();

            var bestGroup = sortedGroups.FirstOrDefault()?.Group;
            if (bestGroup != null)
            {
                // 在挑选出的最优成语词组内部，收集所有 None 状态的纯空格，执行【纯随机抓取诞生】！
                List<ChessView> availableNoneCells = new List<ChessView>();
                foreach (var p in bestGroup.chesspieces)
                {
                    if (GridList.TryGetValue((p.row, p.col), out var v) && v.CurrState == TileState.None)
                    {
                        availableNoneCells.Add(v);
                    }
                }
                // 兜底：如果游戏快过关只剩最终一个空格，此时它处于光标 Check 状态，None 找不到，降级选择当前光标格！
                if (availableNoneCells.Count == 0)
                {
                    foreach (var p in bestGroup.chesspieces)
                    {
                        if (GridList.TryGetValue((p.row, p.col), out var v) && (v.CurrState == TileState.Check || v.CurrState == TileState.Error))
                            availableNoneCells.Add(v);
                    }
                }
                if (availableNoneCells.Count > 0)
                {
                    // ChessView randomLeafTile = availableNoneCells[UnityEngine.Random.Range(0, availableNoneCells.Count)];
                    ChessView bestLeafTile = availableNoneCells
                        .OrderBy(v => GetChessGroups(v.Row, v.Col).Count()) 
                        .ThenBy(v => bestGroup.chesspieces.FindIndex(p => p.Equals(v.chesspiece))) 
                        .FirstOrDefault();
                    // ChessStageController.Instance.LeafGenCounter++; // 换肤计数累加
                    bestLeafTile?.ShowLeaf(true); // 树叶亮起，随后在此死死钉住，直到下一次事件爆发！
                }
            }
        }
    }

    /// <summary>
    /// 返回空位在词组中的“从左到右”索引（0 起始）。
    /// 若空位是交叉字且是 emptyGroups 中**任意一个**词组的**首次出现位置**，返回 该位置。
    /// </summary>
    public int EmptyIndexInGroup(Chesspiece emptyPiece, List<PhraseGroup> emptyGroups)
    {
        // 1. 普通索引（0 起始）
        int normalIdx = emptyGroups
            .Select(g => g.chesspieces.FindIndex(p => p.Equals(emptyPiece)))
            .FirstOrDefault(i => i >= 0);

        // 2. 交叉且首空 → 返回首空索引
        var firstEmptyIdx = emptyGroups
            .Select(g => new { g, idx = g.chesspieces.FindIndex(p =>
                GridList.TryGetValue((p.row, p.col), out var v) &&
                v.CurrState is TileState.None or TileState.Error) })
            .Where(x => x.idx >= 0 && x.g.chesspieces[x.idx].Equals(emptyPiece))
            .Select(x => x.idx)
            .FirstOrDefault();
        
        return firstEmptyIdx != -1 ? firstEmptyIdx : normalIdx ;
    }
    // 设置格子选择状态
    private void SetCheckView(ChessView data , bool clean = true)
    {
        if (clean)
        {
            foreach (var item in GridList.Values)
            {
                if (item.CurrState == TileState.Check)
                {
                    item.SetTileState(TileState.None);
                }

                item.SetChoose(false);
            }

            data.SetTileState(TileState.Check);
        }
        else
        {
            data.SetChoose(true);
        }
        selecteTile = data;
        ChessStageController.Instance.ModifyCursor(selecteTile.Row, selecteTile.Col);
    }
/// <summary>
    /// 🌟 仅在手动点击时触发：精准提炼玩家点击意图（引入双首位空位少优先规则）
    /// </summary>
    private void UpdateDirectionOnManualClick(ChessView data)
    {
        var intersectingGroups = GetChessGroups(data.Row, data.Col).ToList();
        
        // 1. 过滤出尚未完全通关的词组
        var incompleteGroups = intersectingGroups.Where(g => 
            !g.chesspieces.All(p => GridList.TryGetValue((p.row, p.col), out var v) && (v.CurrState == TileState.Success || v.IsOK))
        ).ToList();

        if (incompleteGroups.Count == 0)
        {
            // 如果都填满了，默认横向优先
            _lastActiveDirection = intersectingGroups.Any(g => g.direction == 1) ? 1 : intersectingGroups[0].direction;
            return;
        }

        // 2. 判定该格子是否为未填满词组的“首个空位”
        var firstEmptyGroups = incompleteGroups.Where(g => IsFirstEmptyOfGroup(g, data)).ToList();

        if (firstEmptyGroups.Count > 0)
        {
            // 🌟【核心规则重构】：若当前交叉空格同时是多个词组的首位
            var firstEmptyGroupsSorted = firstEmptyGroups.Select(g => new {
                Group = g,
                // 精准计算该词组当前盘面实际残留的空格总数（包含 None, Error 以及当前选中的 Check 状态）
                EmptyCount = g.chesspieces.Count(p => GridList.TryGetValue((p.row, p.col), out var v) && 
                    (v.CurrState == TileState.None || v.CurrState == TileState.Error || v.CurrState == TileState.Check))
            })
            .OrderBy(x => x.EmptyCount)                            // 1. 核心要求：词组空位少的绝对优先
            .ThenByDescending(x => x.Group.direction == 1 ? 1 : 0) // 2. 平局逻辑：空位同等数量下，横向优先
            .ToList();

            _lastActiveDirection = firstEmptyGroupsSorted[0].Group.direction;
            
            UnityEngine.Debug.Log($"<color=#00FF00><b>[手动点击流向重定向]</b></color> 坐标({data.Row},{data.Col})触发双首位决策 -> 锁定方向: {(_lastActiveDirection == 1 ? "横向" : "纵向")} (目标组空位数: {firstEmptyGroupsSorted[0].EmptyCount})");
        }
        else
        {
            // 🌟 核心规则：若手动选择的不是首空（点在词组中间），则尽量保持原有流向，防止乱跳
            if (!incompleteGroups.Any(g => g.direction == _lastActiveDirection))
            {
                _lastActiveDirection = incompleteGroups.Any(g => g.direction == 1) ? 1 : incompleteGroups[0].direction;
            }
        }
    }

    /// <summary>
    /// 🌟 判定指定格子是否为该词组视觉序上的“第一个未填空格”
    /// </summary>
    private bool IsFirstEmptyOfGroup(PhraseGroup g, ChessView tile)
    {
        var sorted = g.chesspieces.OrderBy(p => (100 - p.col) * 1000 + p.row).ToList();
        int idx = sorted.FindIndex(p => p.row == tile.Row && p.col == tile.Col);
        if (idx == -1) return false;
        
        // 检查在当前格子之前，是否还存在任何未填的空格 (None 或 Error)
        for (int i = 0; i < idx; i++)
        {
            if (GridList.TryGetValue((sorted[i].row, sorted[i].col), out var v))
            {
                if (v.CurrState == TileState.None || v.CurrState == TileState.Error || v.CurrState == TileState.Check)
                {
                    return false; // 前面还有空格，说明当前选中的并不是首个空格
                }
            }
        }
        return true; 
    }
    /// <summary>
    /// 根据棋子 id 和方向返回匹配的组
    /// </summary>
    private IEnumerable<PhraseGroup> GetChessGroups(int row, int col, int? direction = null)
    {
        if (CurrStageData.ChessGroup.TryGetValue((row, col), out var set))
            return direction.HasValue
                ? set.Where(g => g.direction == direction.Value)
                : set; // 不过滤方向
        return Enumerable.Empty<PhraseGroup>();
    }

    #endregion

    #region 棋盘操作

    /// <summary>
    /// 生成棋盘
    /// </summary>
    /// <param name="isAmin"></param>
    /// <param name="isResetAnim"></param>
    public void CreateChess(bool isAmin = true, bool isResetAnim = false)
    {
        StartCoroutine(SetupGrid(isAmin));
    }

    /// <summary>
    /// 创建字块
    /// </summary>
    private IEnumerator SetupGrid(bool isAmin, Action call = null)
    {
        HashSet<Chesspiece> boardData = CurrStageData.BoardSnapshot;
        Debug.Log("当前关卡: "+CurrStageData.StageId+" 棋盘数据 :" + JsonConvert.SerializeObject(boardData));
        yield return null;
        List<int> cousor = CurrStageData.Cousor;
        bool isSetDefault = false;
        foreach (Chesspiece ppp in boardData.ToList())
        {
            ChessView cell = LetterTilePool.GetObject<ChessView>();
            cell.SetInit(ppp);
            cell.OnSelectHandler += ReceiveData;
            SetCellPosition(cell);
            cell.startPosition = cell.TileTransform.anchoredPosition;
            // 检查是否有初始光标
            if (cousor.Count > 0)
            {
                if (ppp.row == cousor[0] && ppp.col == cousor[1] && ppp.state != TileState.Default)
                {
                    if (ppp.bowl == null)
                        cell.SetTileState(TileState.Check);
                    else
                        cell.SetChoose(true);

                    selecteTile = cell;
                    isSetDefault = true;
                    
                    // var groups = GetChessGroups(cell.Row, cell.Col).ToList();
                    // if (groups.Count > 0) _lastActiveDirection = groups.Any(g => g.direction == 1) ? 1 : groups[0].direction;
                }
            }

            GridList.Add((ppp.row, ppp.col), cell);
            //SetCellPosition(cell.TileTransform, cell.row, cell.col);
        }
        GameOver = false;
        
        if (isSetDefault == false)
        {
            ChessView topLeftCell = FindLeftTopCursor();
            if (topLeftCell != null)
            {
                topLeftCell.SetTileState(TileState.Check);
                selecteTile = topLeftCell;
                isSetDefault = true;
                ChessStageController.Instance.ModifyCursor(selecteTile.Row, selecteTile.Col);
                
                // var groups = GetChessGroups(selecteTile.Row, selecteTile.Col).ToList();
                // if (groups.Count > 0) _lastActiveDirection = groups.Any(g => g.direction == 1) ? 1 : groups[0].direction;
            }
        }
        if (selecteTile != null)
        {
            UpdateDirectionOnManualClick(selecteTile);
        }
        GenerateNextLevelLeaf();
        
        if (isAmin)
        {
            yield return new WaitForSeconds(0.8f);
            call?.Invoke();
        }
    
    }

    /// <summary>
    /// 清理棋盘
    /// </summary>
    public void Clear()
    {
        GridList.Clear();
        TileErrorCounts.Clear(); // 🌟 清理错误记录
        LetterTilePool.ReturnAllObjectsToPool();
        hasPlayedComboSoundThisChain = false;
        IsBlockInput = false;
        EventDispatcher.instance.TriggerChangeTopRaycast(true);
    }

    /// <summary>
    /// 设置棋盘内的位置
    /// </summary>
    private void SetCellPosition(ChessView cell)
    {
        // 1. 统一锚点 → 左下
        cell.TileTransform.anchorMax = Vector2.zero;
        cell.TileTransform.anchorMin = Vector2.zero;
        cell.TileTransform.pivot = Vector2.zero;
        // cell.TileTransform.anchoredPosition = Vector2.zero;

        // 2. 内边距 & 尺寸
        Vector2 spacing = new Vector2(4, 4);
        Vector2 cellSize = CurrStageData.ActiveSize;
        int colCount = CurrStageData.MaxCol - CurrStageData.MinCol + 1;
        int minRow = CurrStageData.MinRow;
        //
        float x = GamePlayArea.startLocation.row + cellSize.x / 2f + (cell.Row - minRow) * (cellSize.x + spacing.x);
        float y = 17 + cellSize.y / 2f + (cell.Col + colCount - CurrStageData.MaxCol - 1) * (cellSize.y + spacing.y) + spacing.y;
        
        SetCellCenter(cell.TileTransform, cellSize);
        cell.TileTransform.anchoredPosition = new Vector2(x, y);
        cell.TileTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, cellSize.x);
        cell.TileTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, cellSize.y);
        
        // 6. 【关键】把 Content 撑到含内边距的总尺寸
        //int rowCount = ChessStageController.Instance.CurrStageData.MaxRow;
        //int colCount = ChessStageController.Instance.CurrStageData.MaxCol;
        //Vector2 totalSize = new Vector2(
        //    padding.x * 2 + rowCount * cellSize.x + (rowCount + 2) * spacing.x,
        //    padding.y * 2 + colCount * cellSize.y + (colCount + 2) * spacing.y);
        //Debug.Log("棋盘尺寸" + totalSize);
        //GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, totalSize.x);
        //GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, totalSize.y);
    }
    /// <summary>
    /// 把格子 pivot 设成中心，并重新计算 anchoredPosition，视觉位置不动
    /// </summary>
    private void SetCellCenter(RectTransform rt, Vector2 cellSize)
    {
        // 存视觉中心
        Vector3 worldCenter = rt.TransformPoint(rt.rect.center);

        // 改轴心
        rt.pivot = Vector2.one * 0.5f;

        // 补偿位置（本地 anchored 空间）
        Vector2 offset = cellSize * (Vector2.one * 0.5f - rt.pivot);
        rt.anchoredPosition += offset;
        // 确保世界中心不动
        rt.SetPositionAndRotation(worldCenter, rt.rotation);
    }
    #endregion

    /// <summary>
    /// 查找左上角的光标位置
    /// </summary>
    private ChessView FindLeftTopCursor()
    {
        return GridList.Values
            .Where(cell => cell.CurrState == TileState.None)
            .OrderByDescending(cell => cell.Col) // 先按 col 降序（最右 → 最左）
            .ThenBy(cell => cell.Row) // 再按 row 升序（最上 → 最下）
            .FirstOrDefault();
    }

    private void OnDisable()
    {
        Clear();
    }

    // 新手教程获取
    public List<ChessView> GetCurrentSelectGroup()
    {
        PhraseGroup phrases = GetChessGroups(selecteTile.Row, selecteTile.Col)
            .OrderByDescending(g => g.direction == _lastActiveDirection ? 1 : 0).FirstOrDefault();

        return phrases.chesspieces
            .Select(p => GridList.TryGetValue((p.row, p.col), out ChessView chess) ? chess : null)
            .Where(v => v != null)
            .ToList();
    }
    public List<ChessView> GetCurrentSelectGroup2()
    {
        PhraseGroup phrases = GetChessGroups(selecteTile.Row, selecteTile.Col)
            .OrderByDescending(g => g.direction == _lastActiveDirection ? 1 : 0) // 1 优先
            .FirstOrDefault(); // 只取一
        return phrases?.chesspieces
            .Select(p => GridList.TryGetValue((p.row, p.col), out ChessView chess) ? chess : null)
            .Where(v => v != null)
            .ToList();
    }
    
    /// <summary>
    /// 在 GridList 中随机返回一个满足：
    /// 1. 当前状态为 None
    /// 2. ChessPiece.Tip == false
    /// 的字块；若无满足条件的字块则返回 null。
    /// </summary>
    public ChessView GetRandomNoneNonTipChess()
    {
        // 0. 基础过滤（只找空的、没被冻住的、自身没被提示过的）
        var candidates = GridList.Values
            .Where(v => v.CurrState == TileState.None &&
                        v.chesspiece is { tip: false } &&
                        !v.chesspiece.hasIce)
            .ToList();
            
        if (candidates.Count == 0) return null;

        // 统一 helper：该格所属任意组存在 tip=true → 直接淘汰
        bool HasTipInAnyGroup(int row, int col) =>
            GetChessGroups(row, col)
                .Any(g => g.chesspieces.Any(cp =>
                    GridList.TryGetValue((cp.row, cp.col), out var vv) &&
                    vv.chesspiece is { tip: true }));

        // 1. 交叉优先（≥2组 + 任意组均无 tip）
        var crossFirst = candidates
            .Where(v => GetChessGroups(v.Row, v.Col).Count() >= 2 &&
                        !HasTipInAnyGroup(v.Row, v.Col))
            .Select(v => new
            {
                view = v,
                info = GetChessGroups(v.Row, v.Col)
                    .Where(g => !g.chesspieces.Any(cp => // 只留无 tip 的组
                        GridList.TryGetValue((cp.row, cp.col), out var vv) &&
                        vv.chesspiece is { tip: true }))
                    .Select(g => new
                    {
                        group = g,
                        noneCount = g.chesspieces.Count(cp =>
                            GridList.TryGetValue((cp.row, cp.col), out var vv) &&
                            vv.CurrState == TileState.None)
                    })
                    .FirstOrDefault(x => x.group != null)
            })
            .Where(x => x.info.group != null)
            .OrderByDescending(x => x.info.noneCount) // 剩余空格降序
            .ThenBy(x => x.info.group.direction == 1 ? x.view.Col : x.view.Row)
            .FirstOrDefault(); // 🌟 安全调用

        if (crossFirst != null) return crossFirst.view;

        // 2. 无交叉 → 同样剔 tip 组后排序
        var noCrossFirst = candidates
            .Where(v => GetChessGroups(v.Row, v.Col).Count() < 2 &&
                        !HasTipInAnyGroup(v.Row, v.Col))
            .Select(v => new
            {
                view = v,
                info = GetChessGroups(v.Row, v.Col)
                    .Where(g => !g.chesspieces.Any(cp =>
                        GridList.TryGetValue((cp.row, cp.col), out var vv) &&
                        vv.chesspiece is { tip: true }))
                    .Select(g => new
                    {
                        group = g,
                        noneCount = g.chesspieces.Count(cp =>
                            GridList.TryGetValue((cp.row, cp.col), out var vv) &&
                            vv.CurrState == TileState.None)
                    })
                    .FirstOrDefault(x => x.group != null)
            })
            .Where(x => x.info.group != null)
            .OrderByDescending(x => x.info.noneCount)
            .ThenBy(x => x.info.group.direction == 1 ? x.view.Col : x.view.Row)
            .FirstOrDefault(); // 🌟 修复点：将 First() 改为 FirstOrDefault()

        if (noCrossFirst != null) return noCrossFirst.view;

        // 🌟 3. 终极防崩溃兜底方案：
        // 如果代码执行到这里，说明盘面上所有剩下的空格子，它们所在的成语都已经有被提示过的字了。
        // 这时候我们放宽要求，无视 "HasTipInAnyGroup" 的限制，直接从最基础的 candidates 里随便挑一个返回给蝴蝶道具！
        return candidates.OrderBy(x => Random.value).FirstOrDefault();
    }
    
    /// <summary>
    /// 修复可能出现的状态问题
    /// </summary>
    public void FixChessState()
    {
        foreach (var cell in GridList.Values)
        {
            if (cell.CurrState == TileState.Success)
                cell.IsOK = true;
        }
        
        if (GridList.Values.All(item => item.IsOK))
        {
            GameOver = true;
            Debug.Log("已全部完成，进行下一个关");
            GamePlayArea.GamePlayOver();
        }
    }
    /// <summary>
    /// 🌟 独立方法：处理被完成词组的关联冰块打碎
    /// </summary>
    private bool BreakAdjacentIce(List<ChessView> completedGroupViews)
    {
        HashSet<PhraseGroup> associatedGroups = new HashSet<PhraseGroup>();
        bool isBroken = false;
      
        // 1. 收集被完成词组的直接交叉组和四向相邻组
        foreach (var v in completedGroupViews)
        {
            var myGroups = GetChessGroups(v.Row, v.Col);
            foreach (var g in myGroups)
            {
                if (g.chesspieces.All(p => GridList.TryGetValue((p.row, p.col), out var view) && view.CurrState == TileState.Success))
                    continue;
                associatedGroups.Add(g);
            }
        }
        // 2. 遍历所有关联词组的格子，只要有冰块就全部打碎！
        HashSet<ChessView> tilesToBreak = new HashSet<ChessView>();
        foreach (var group in associatedGroups)
        {
            foreach (var p in group.chesspieces)
            {
                if (GridList.TryGetValue((p.row, p.col), out var view))
                {
                    tilesToBreak.Add(view);
                }
            }
        }
        // 把刚完成的组的格子也算进去兜底
        foreach (var v in completedGroupViews) tilesToBreak.Add(v);
        // 如果邻居有冰块，打碎它！
        foreach (var neighbor in tilesToBreak)
        {
            if (neighbor.chesspiece.hasIce)
            {
                neighbor.chesspiece.hasIce = false;
                // neighbor.iceLogicBroken = false; // 🌟 顺手修复：清空逻辑破冰标记
                ChessStageController.Instance.ModifyChreepiece(neighbor.chesspiece);
                StartCoroutine(neighbor.PlayIceBreakAnim());
                isBroken = true;
            }
        }
        if (isBroken)
        {
            AudioManager.Instance.PlaySoundEffect("IceBreak",0,1); 
        }
        return isBroken;
    }
    /// <summary>
    /// 🌟 独立方法：处理花朵的整组绽放逻辑
    /// </summary>
    private bool ProcessFlowerBlooming(List<ChessView> completedGroupViews)
    {
        bool hasBlooming = false;
        var currStageData = ChessStageController.Instance.CurrStageData;
        var flowerConfig = ChessStageController.Instance.FlowerConfig;

        // 获取全盘所有【有花朵且没有被冰块盖住】的有效格子
        var allValidFlowers = GridList.Values.Where(v => v.chesspiece.hasFlower && !v.chesspiece.hasIce).ToList();
        if (allValidFlowers.Count == 0) return false;

        HashSet<ChessView> flowersToBloom = new HashSet<ChessView>();
        // 1. 本组成语如果有花，必须全部消除
        foreach (var v in completedGroupViews.Where(x => x.chesspiece.hasFlower && !x.chesspiece.hasIce))
        {
            flowersToBloom.Add(v);
        }
        
        // 2. 判断是否是倒数第2个成语
        int totalWords = ChessStageController.Instance.CurrStageInfo.PhraseGroups.Count;
        int solvedWords = currStageData.FoundTargetPuzzles.Count; // +1 因为当前这组正在结算
        bool isSecondToLast = (totalWords - solvedWords) == 1;

        if (isSecondToLast)
        {
            // 倒数第二题答对，剩下所有花全部绽放！
            foreach (var f in allValidFlowers)
            {
                flowersToBloom.Add(f);
            }
        }
        else
        {
            // 3. 计算需要消除的 Y 数量
            int initY = flowerConfig?.InitNumber > 0 ? flowerConfig.InitNumber : 2;
            int minY = flowerConfig?.MinNumber > 0 ? flowerConfig.MinNumber : 1;
            int currentY = Mathf.Max(minY, initY - (currStageData.FlowerActionCount / 2));

            // 🌟 核心修复：扣除当前成语自己占用的花朵格子配额，看看外面还能消几朵
            int remainingQuota = currentY - flowersToBloom.Count;
            if (remainingQuota > 0)
            {
                float centerRow = completedGroupViews.Average(v => (float)v.Row);
                float centerCol = completedGroupViews.Average(v => (float)v.Col);
                // 在剩下的花朵中寻找最近的 Y 朵
                var remainingFlowers = allValidFlowers.Where(f => !flowersToBloom.Contains(f)).ToList();
                var nearestTiles = remainingFlowers.OrderBy(f => 
                    Mathf.Abs(f.Row - centerRow) + Mathf.Abs(f.Col - centerCol)
                ).Take(remainingQuota).ToList();
                foreach (var targetFlower in nearestTiles)
                {
                    flowersToBloom.Add(targetFlower);
                }
            }
            currStageData.FlowerActionCount++;
        }

        // 统一播放绽放动画
        foreach (var f in flowersToBloom)
        {
            f.chesspiece.hasFlower = false; // 内存状态直接解锁
            ChessStageController.Instance.ModifyChreepiece(f.chesspiece);
            StartCoroutine(f.PlayFlowerBloomAnim());
            hasBlooming = true;
        }
        if (hasBlooming)
        {
            // 请将 "FlowerBloom" 替换为你工程中实际的花朵绽放音效名称
            AudioManager.Instance.PlaySoundEffect("FlowerBloom",0,1); 
        }
        return hasBlooming;
    }
        /// <summary>
    /// 🌟 大厂规范重构：提示/蝴蝶道具独立核心填入与寻路驱动流水线
    /// 完整解决：1. 下方字块未能同步消除扣除；2. 智能寻路引擎失效，光标无法自动跳转至下一个有效空格。
    /// </summary>
    public IEnumerator ExecuteHintFillFlow(ChessView targetTile)
    {
        if (targetTile == null) targetTile = selecteTile;
        if (targetTile == null || GameOver) yield break;

        // ------------------------------------------------------------------
        // 流程一：安全回滚目标格子已存在的临时/错误字块
        // ------------------------------------------------------------------
        if (targetTile.CurrState == TileState.Fill || targetTile.CurrState == TileState.Error)
        {
            Bowl dummyBowl = targetTile.chesspiece.bowl ?? new Bowl { letter = targetTile.chesspiece.letter };
            GamePlayArea.puzzleTileTable.OnNotifyResult(dummyBowl, 0); // 归还库存
            targetTile.chesspiece.bowl = null;
        }

        // ------------------------------------------------------------------
        // 流程二：双向绑定并彻底消除下方待填字盘中的对应字块
        // ------------------------------------------------------------------
        // 从字盘搜寻匹配当前格子正确答案、且未被用光的有效字块
        BowlView matchingBowl = GamePlayArea.puzzleTileTable.GridList.FirstOrDefault(v => 
            v.letter == targetTile.Answer && v.bowl.status == 0);

        if (matchingBowl != null)
        {
            targetTile.SetPuzzle(matchingBowl.bowl); // 数据链绑定
            GamePlayArea.puzzleTileTable.OnNotifyResult(matchingBowl.bowl, 1); // 第一步：发 1 模拟使用，扣除一个库存 (count--) / 变灰锁定
            GamePlayArea.puzzleTileTable.OnNotifyResult(matchingBowl.bowl, 2); // 第二步：发 2 系统核验，若 count <= 0 则彻底从 GridList 卸载消除并回收
        }
        else
        {
            // 极端边界兜底：若外部字块因异常无库存，虚构一个匹配的Bowl资产进行静默消除，确保逻辑不卡死
            Bowl fallbackBowl = new Bowl { letter = targetTile.Answer, status = 1, count = 0 };
            targetTile.SetPuzzle(fallbackBowl);
        }

        // ------------------------------------------------------------------
        // 流程三：物理状态转产与单格华丽粒子爆发
        // ------------------------------------------------------------------
        // 强制变更为绝对绿色的 Success 状态（此状态在 ChessView 中天然具备不可点击、不可撤回权）
        targetTile.SetTileState(TileState.Success, false);
        
        // 唤醒单格常驻绿色底板、Q弹缩放及完成粒子爆发
        targetTile.PlaySuccessAnimation(0.5f, () => {
            targetTile.UpdateTile(true);
        });
        targetTile.PlayHintShiny(5f);
        // ------------------------------------------------------------------
        // 流程四：强控始发锚点，规整时序合流判定
        // ------------------------------------------------------------------
        // 必须让寻路引擎知道当前触发成功的始发格子是谁
        selecteTile = targetTile;

        List<List<ChessView>> correctGroups = new List<List<ChessView>>();
        List<List<ChessView>> errorGroups = new List<List<ChessView>>();
        
        // 检查本次提示填入是否恰好促成了某个或多个成语词组的“大满贯”通关
        yield return CheckSuccessful(targetTile, correctGroups, errorGroups);
        
        // ------------------------------------------------------------------
        // 流程五：驱动智能寻路引擎，强制执行下一光标重定向自动跳转
        // ------------------------------------------------------------------
        if (correctGroups.Count > 0)
        {
            // 触发了成语整组通关，合流进入华丽闪光包裹框、加分、飞叶子、碎冰机制
            yield return PlayGroupSuccessSequence(correctGroups, errorGroups);
        }
        else
        {
            // 未触发整组通关，CheckSuccessful 内部在 else 块虽有寻路，
            // 但为了对抗多线程及物理缓动迟滞，此处显式强控进行二次终审重定向跳转
            // SearchNextTile();
        }

        yield return null;
        yield return CheckCompleted();
    }
    
     #region 金箔相关逻辑
    
    private HashSet<string> _currentGoldLetters = new HashSet<string>(); // 需要金箔的字母集合
    
    /// <summary>
    /// 根据规则选择 n 个需要放置金箔的字块（卡关词组可为多组，最多取2个不同的卡关词组）
    /// </summary>
    public List<ChessView> SelectGoldLeafPositions(int goldLeafCount)
    {
        if (goldLeafCount <= 0) return new List<ChessView>();

        // 收集所有词组及待填字块
        HashSet<PhraseGroup> allGroups = new HashSet<PhraseGroup>();
        foreach (var set in CurrStageData.ChessGroup.Values)
            foreach (var group in set)
                allGroups.Add(group);

        Dictionary<PhraseGroup, List<ChessView>> groupPendingTiles = new Dictionary<PhraseGroup, List<ChessView>>();
        foreach (var group in allGroups)
        {
            var pending = new List<ChessView>();
            foreach (var piece in group.chesspieces)
            {
                if (GridList.TryGetValue((piece.row, piece.col), out ChessView view))
                {
                    if (view.CurrState != TileState.Success && view.CurrState != TileState.Default)
                        pending.Add(view);
                }
            }
            if (pending.Count > 0)
                groupPendingTiles[group] = pending;
        }

        if (groupPendingTiles.Count == 0) return new List<ChessView>();

        // 获取候选词组列表（按优先级排序）
        List<PhraseGroup> candidateGroups = GetCandidateGroups(groupPendingTiles);
        List<ChessView> selectedTiles = new List<ChessView>();
        HashSet<PhraseGroup> usedGroups = new HashSet<PhraseGroup>();

        // 收集所有候选词组中的待填字块
        List<ChessView> allPendingTiles = new List<ChessView>();
        foreach (var group in candidateGroups)
        {
            if (groupPendingTiles.TryGetValue(group, out var tiles) && tiles.Count > 0)
            {
                allPendingTiles.AddRange(tiles);
                usedGroups.Add(group);
            }
        }

        // 随机选择至多 2 个字块（不足则全部取用）
        int takeCount = Mathf.Min(2, allPendingTiles.Count);
        for (int i = 0; i < takeCount; i++)
        {
            int randomIndex = Random.Range(0, allPendingTiles.Count);
            selectedTiles.Add(allPendingTiles[randomIndex]);
            // 如果需要允许重复选择同一个字块，则不移除；通常应避免重复选择同一位置的字块，所以移除已选的字块
            allPendingTiles.RemoveAt(randomIndex);
        }

        // 剩余需要随机选取的数量
        int remaining = goldLeafCount - selectedTiles.Count;
        if (remaining <= 0) return selectedTiles;

        // 收集其他词组（排除已选中的词组）的所有待填字块
        List<ChessView> otherTiles = new List<ChessView>();
        foreach (var kvp in groupPendingTiles)
        {
            if (usedGroups.Contains(kvp.Key)) continue;
            foreach (var tile in kvp.Value)
            {
                if (!selectedTiles.Contains(tile))
                    otherTiles.Add(tile);
            }
        }
        otherTiles = otherTiles.Distinct().ToList();

        if (otherTiles.Count == 0) return selectedTiles;

        int realTake = Mathf.Min(remaining, otherTiles.Count);
        var randomOthers = otherTiles.OrderBy(x => Random.value).Take(realTake).ToList();
        selectedTiles.AddRange(randomOthers);

        return selectedTiles;
    }
    
    /// <summary>
    /// 获取候选词组列表（按优先级排序：孤岛词 > 待填字数最多 > 第四象限 > 随机）
    /// 返回的列表可能包含多个词组，优先级最高的排在最前。
    /// </summary>
    private List<PhraseGroup> GetCandidateGroups(Dictionary<PhraseGroup, List<ChessView>> groupPendingTiles)
    {
        // 优先级1：孤岛词（该词组所有字块都只属于它自己）
        var islandGroups = groupPendingTiles.Keys
            .Where(group => group.chesspieces.All(piece => GetChessGroups(piece.row, piece.col).Count() == 1))
            .ToList();

        if (islandGroups.Count > 0)
        {
            // 在孤岛词中找待填字最多的
            int maxPending = islandGroups.Max(g => groupPendingTiles[g].Count);
            var maxPendingIslands = islandGroups.Where(g => groupPendingTiles[g].Count == maxPending).ToList();

            // 收集这些孤岛词中的所有待填字块
            // var allTiles = maxPendingIslands.SelectMany(g => groupPendingTiles[g]).ToList();
            //
            // // 随机选择两个（不足则全选）
            // var selectedTiles = allTiles.OrderBy(x => Random.value).Take(2).ToList();
            return maxPendingIslands;
        }

        // 没有孤岛词，则考虑所有词组
        // 优先级2：待填字最多
        int maxPendingAll = groupPendingTiles.Values.Max(list => list.Count);
        var maxPendingGroups = groupPendingTiles.Keys.Where(g => groupPendingTiles[g].Count == maxPendingAll).ToList();

        if (maxPendingGroups.Count == 0) return new List<PhraseGroup>();

        // 优先级3：优先选有第四象限字块的
        var withFourthAll = maxPendingGroups.Where(g => groupPendingTiles[g].Any(tile => IsInFourthQuadrant(tile))).ToList();
        if (withFourthAll.Count > 0)
        {
            return withFourthAll.OrderBy(x => Random.value).ToList();
        }
        else
        {
            return maxPendingGroups.OrderBy(x => Random.value).ToList();
        }
    }

    /// <summary>
    /// 判断字块是否位于棋盘的第四象限（以棋盘中心为原点，x向右正，y向上正）
    /// </summary>
    private bool IsInFourthQuadrant(ChessView tile)
    {
        // 获取棋盘面板的 RectTransform
        RectTransform boardRect = GetComponent<RectTransform>();
        // 字块的局部坐标（相对于棋盘面板，锚点左下角）
        Vector2 localPos = tile.TileTransform.localPosition;
        // 棋盘中心坐标（面板宽高的一半）
        Vector2 boardCenter = new Vector2(boardRect.rect.width * 0.5f, boardRect.rect.height * 0.5f);
        // 转换为以中心为原点的坐标
        Vector2 relativePos = localPos - boardCenter;
        // 第四象限：x > 0, y < 0
        return relativePos.x > 0 && relativePos.y < 0;
    }

    /// <summary>
    /// 根据待填字块选择规则，在待填字母块上显示金箔
    /// </summary>
    /// <param name="chessboard">棋盘实例</param>
    /// <param name="goldLeafCount">需要出现的金箔总数</param>
    public void ShowGoldLeafFromChessboard(ChessboardGrid chessboard, int goldLeafCount)
    {
        if (chessboard == null || goldLeafCount <= 0) return;
        
        // 1. 获取需要显示金箔的待填字块
        var targetTiles = chessboard.SelectGoldLeafPositions(goldLeafCount);
        if (targetTiles.Count == 0) return;
        
        ChessStageController.Instance.GoldLeafChessViews.Clear();
       
        ClearAllGoldLeafOnBowls();
        
        foreach (var tile in targetTiles)
        {
            if (tile.CurrState != TileState.Success&&tile.CurrState!=TileState.Default) // 只对未成功的格子显示金箔
                _currentGoldLetters.Add(tile.Answer);
        }

        // int index=_currentGoldLetters.Count-1;
        //
        // string remaining = _currentGoldLetters.ToArray()[index];
        //
        // _currentGoldLetters.Remove(remaining);
        // _currentGoldLetters.Add("柔");
        
        foreach (var kvp in _currentGoldLetters)
        {
            Debug.Log(string.Format("金箔生成字符 {0}",kvp));
        }
        
        Debug.Log(string.Format("{0} 关，金箔生成数量 {1}",CurrStageData.StageId, goldLeafCount));
        
        CurrStageData.UpdateGoldLeafCount(goldLeafCount);
        
        // 4. 为匹配的字母块设置金箔
        foreach (var bowl in GamePlayArea.puzzleTileTable.GridList)
        {
            if (_currentGoldLetters.Contains(bowl.letter) && !bowl.locked)
            {
                bowl.SetGoldLeaf(true);
            }
            else
            {
                bowl.SetGoldLeaf(false);
            }
        }
    }

    /// <summary>
    /// 清除所有字母块上的金箔标记
    /// </summary>
    public void ClearAllGoldLeafOnBowls()
    {
        foreach (var bowl in GamePlayArea.puzzleTileTable.GridList)
        {
            bowl.SetGoldLeaf(false);
            bowl.ClearGoldLeaf();
            //bowl.OnGoldLeafCollected -= OnBowlGoldCollected;
        }
        _currentGoldLetters.Clear();
    }
    
    #endregion
    
    /// <summary>
    /// 🌟 检测并触发最后一词的波浪聚焦特效
    /// </summary>
    private void CheckAndTriggerLastGroupFocus()
    {
        // 1. 获取全盘所有的词组
        var allGroups = GamePlayArea.CurrStageInfo.PhraseGroups;
        
        // 2. 筛选出还没有完成的词组
        List<PhraseGroup> unsolvedGroups = new List<PhraseGroup>();
        foreach (var g in allGroups)
        {
            bool isSolved = g.chesspieces.All(p => 
                GridList.TryGetValue((p.row, p.col), out var v) && 
                (v.CurrState == TileState.Success || v.IsOK));
                
            if (!isSolved) unsolvedGroups.Add(g);
        }

        // 3. 🎯 如果刚好只剩下最后 1 个词组！
        if (unsolvedGroups.Count == 1)
        {
            var lastGroup = unsolvedGroups[0];
            int unsolvedCount = lastGroup.chesspieces.Count(p => 
                GridList.TryGetValue((p.row, p.col), out var v) && 
                v.CurrState != TileState.Success);
            if (unsolvedCount < 4) return;
            
            float currentDelay = 0f; // 波浪延迟累加器
            
            // 按照视觉阅读顺序排序 (从上到下，从左到右)
            var sortedPieces = lastGroup.chesspieces.OrderBy(p => (100 - p.col) * 1000 + p.row).ToList();
            
            foreach (var p in sortedPieces)
            {
                if (GridList.TryGetValue((p.row, p.col), out var v))
                {
                    // 只对还没有填对的空格播放光圈
                    if (v.CurrState is TileState.None or TileState.Fill or TileState.Error or TileState.Check)
                    {
                        v.PlayFocusWaveAnim(currentDelay);
                        currentDelay += 0.1f; // 🌟 每隔 0.1 秒触发下一个，形成完美波浪
                    }
                }
            }
            
            // 可选：播放一个特殊的提示音效
            // AudioManager.Instance.PlaySoundEffect("LastWordFocus");
        }
    }
}