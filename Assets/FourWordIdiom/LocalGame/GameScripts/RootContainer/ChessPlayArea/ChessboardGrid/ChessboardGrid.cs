using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 字块矩阵面板
/// </summary>
public class ChessboardGrid : MonoBehaviour
{
    // 增加一个词组列表，为每一个字增加词组列表
    [SerializeField] private GameObject PuzzleItemObj; // 预制体

    private ChessStageProgressData CurrStageData
    {
        get => ChessStageController.Instance.CurrStageData;
    }

    private ObjectPool LetterTilePool;

    public ChessPlayArea GamePlayArea { get; private set; }

    // 存放棋盘的字块
    public readonly Dictionary<(int row, int col), ChessView> GridList = new();
    public bool GameOver { get; private set; }
    // 需要更新的字块
    //private readonly HashSet<ChessView> updateViews = new();
    // 当前选择的格子
    public ChessView selecteTile;
    private StringBuilder selectedPuzzle; // 完成词的收集
    private List<List<ChessView>> checkGroup = new ();  // 带改变状态的组

    public void Initialize(ChessPlayArea play)
    {
        if (PuzzleItemObj == null)
        {
            PuzzleItemObj = AssetBundleLoader.SharedInstance.LoadGameObject("commonitem", "ChessTileView");
        }

        LetterTilePool = new ObjectPool(PuzzleItemObj.gameObject, transform, 3, PoolBehaviour.CanvasGroup);
        GamePlayArea = play;

        selectedPuzzle = new StringBuilder();
    }

    #region 词语操作

    /// <summary>
    /// 直接完成选中的成语, 道具操作
    /// </summary>
    public IEnumerator CompletedPhrase()
    {
        Dictionary<Chesspiece, List<PhraseGroup>> friendGroups = new();
        HashSet<string> handledIds = new HashSet<string>();

        var targetGroup = GetChessGroups(selecteTile.Row, selecteTile.Col)
            .OrderByDescending(g => g.direction) // 1 优先
            .FirstOrDefault(); // 只取一
        
        if (targetGroup == null) yield break;
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
        EventDispatcher.instance.TriggerChangeTopRaycast(true);
        // ==========================================
        
        selectedPuzzle.Clear();
        float delay1 = 0f; // 增加延迟变量
        for (int i = 0; i < targetGroup?.chesspieces.Count; i++)
        {
            friendGroups.TryAdd(targetGroup.chesspieces[i],
                GetChessGroups(targetGroup.chesspieces[i].row, targetGroup.chesspieces[i].col).ToList());

            if (GridList.TryGetValue((targetGroup.chesspieces[i].row, targetGroup.chesspieces[i].col),
                    out ChessView view2))
            {
                // chessViews.Add(view2);
                if (view2.chesspiece?.bowl != null)
                {
                    GamePlayArea.puzzleTileTable.OnNotifyResult(view2.chesspiece.bowl, 0);
                }

                if (view2.CurrState != TileState.Default)
                {
                    Bowl rb = GamePlayArea.puzzleTileTable.CleanBowlView(view2.chesspiece);
                    if (rb != null)
                    {
                        view2.chesspiece!.bowl = rb;
                    }
                }

                if (view2.CurrState is not TileState.Default and not TileState.Success)
                {
                    GamePlayArea.AddCompleteCount(view2);
                }

                view2.SetTileState(TileState.Success,false);
                view2.IsOK = true;
                view2.PlaySuccessAnimation(delay1, () => view2.UpdateTile(true));
                delay1 += 0.08f;
                selectedPuzzle.Append(view2.chesspiece?.letter);
                GamePlayArea.CheckPupaChess(view2);
            }

            handledIds.Add(targetGroup.id);
        }
        
        GamePlayArea.AddFoundPuzzle(selectedPuzzle.ToString());

        // 处理组内其他词的相关词组
        foreach (var kvp in friendGroups)
        {
            // 循环检查朋友在其组内是错误的
            // 此处如何跳过selecteTile所关联的组， 因为上面已经处理了
            foreach (PhraseGroup group in kvp.Value)
            {
                if (handledIds.Contains(group.id)) continue;
                // 是否都正确，正确就设置素材success,
                bool groupSuccess = group.chesspieces.All(p =>
                    GridList.TryGetValue((p.row, p.col), out ChessView v) && v.Correct);
                if (!groupSuccess) // 最高优先级
                {
                    continue;
                }
                
                float delay2 = 0f; // 增加延迟变量
                // 都是正确的， 将格子状态设置成success，并添加找到的词
                selectedPuzzle.Clear();
                group.chesspieces.ForEach(g =>
                {
                    if (GridList.TryGetValue((g.row, g.col), out ChessView v))
                    {
                        v.SetTileState(TileState.Success,false);
                        v.IsOK = true;
                        // GamePlayArea.ButterWordAddIcon(v);
                        v.PlaySuccessAnimation(delay2, () => v.UpdateTile(true));
                        delay2 += 0.08f;
                    }

                    selectedPuzzle.Append(g.letter);
                });
                Debug.Log("在 CompletedPhrase2 填入词组" + group.id + " " + selectedPuzzle.ToString());
                GamePlayArea.AddFoundPuzzle(selectedPuzzle.ToString());
            }
        }

        yield return new WaitForEndOfFrame();
        AudioManager.Instance.PlaySoundEffect("ThemeCompleted");
        yield return CheckCompleted();
        SearchNextTile();
    }

    /// <summary>
    /// 提示选中格子的字
    /// </summary>
    public void SetSelectTip()
    {
        selecteTile.SetTipMessage();
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
        if (data.chesspiece.bowl != null)
        {
            // 填入的旧词恢复正常
            GamePlayArea.puzzleTileTable.OnNotifyResult(data.chesspiece.bowl, 0);
            data.chesspiece.bowl = null;
        }

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
                    selectedPuzzle.Clear();
                    group.chesspieces.ForEach(v => selectedPuzzle.Append(v.letter));
                    // Debug.Log("在 CheckChessGroupState 填入词组" + group.id + " "+selectedPuzzle.ToString());
                    GamePlayArea.AddFoundPuzzle(selectedPuzzle.ToString());
                    tileState = TileState.Success;
                    break;
                }

                //Debug.Log($"组名 {group.id} 朋友 " + friend.letter + " 是否正确" + groupSuccess);
                // 是否有填满的成员，但是错误了，该朋友设置 error
                //bool groupError = group.chesspieces.Any(p =>
                //    GridList.TryGetValue((p.row, p.col), out ChessView v) && (v.CurrState!= TileState.None  && v.CurrState != TileState.Check && !v.Correct));
                bool groupError = group.chesspieces.All(p =>
                                      GridList.TryGetValue((p.row, p.col), out ChessView v) &&
                                      v.CurrState != TileState.None &&
                                      v.CurrState != TileState.Check) &&
                                  group.chesspieces.Any(p =>
                                      GridList.TryGetValue((p.row, p.col), out ChessView v) &&
                                      !v.Correct);
                if (groupError) // 第二优先级
                {
                    GamePlayArea.AddWordError(1);
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
        //if (!_handing)
        //{
        ChessBowlGrid._isProcessing = true;
        if (puzzle.bowl.status == 0)
            yield return SetPuzzleBoardState(puzzle);
        else
            yield return CancelPuzzleBoardState(puzzle);
        //}

        ChessBowlGrid._isProcessing = false;
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
            bowl.status = 1;
            selecteTile.SetPuzzle(bowl);
            GamePlayArea.puzzleTileTable.OnNotifyResult(bowl, 1);
            ChessView curr = selecteTile;
            bool flyover = false;
            EventDispatcher.instance.TriggerChangeTopRaycast(false);
            puzzle.FlyToCell(curr, transform.parent, () =>
            {
                curr.UpdateTile(true);
                flyover = true;
                if (curr.Answer != selecteTile.Answer)
                    GamePlayArea.HandleGamePlayCall(puzzle.gameObject, "SetChess"); // 设置字块事件
            });
            yield return CheckSuccessful(curr);
            // Debug.Log("执行1");
            // Debug.Log($"[调用链] 即将进入 WaitUntil 帧={Time.frameCount}  flyover={flyover}");
            yield return new WaitUntil(() => flyover);
            // Debug.Log($"[调用链] WaitUntil 通过 帧={Time.frameCount}  flyover={flyover}");
            yield return HandleChessUIState();
            // Debug.Log("执行2");
            // yield return new WaitUntil(() => checkGroup.Count == 0);
            // yield return CheckCompleted();
            // Debug.Log("执行3");
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
        ChessView view = GridList.Values.ToList().Find(grid => grid.chesspiece?.bowl?.id == puzzle.bowl.id);
        if (view != null)
        {
            // 填入的旧词恢复正常
            view.chesspiece.bowl = null;
            SetCheckView(view);
        }

        yield return null;
        GamePlayArea.HandleGamePlayCall(puzzle.gameObject, "ClickChess"); // 点击了取消事件
        //view.SetTileState(TileState.None);

        // 填入的旧词恢复正常
        //GamePlayArea.puzzleTileTable.OnNotifyResult(puzzle.bowl, 0);
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
    public IEnumerator CheckSuccessful(ChessView selecteTile)
    {
        List<PhraseGroup> phraseGroups = GetChessGroups(selecteTile.Row, selecteTile.Col)
            .OrderBy(pg => pg.direction == selecteTile.Direction)
            .ToList();
        bool isPlaySound = false;
        ChessView nexterr = null;
        Dictionary<string, bool> result = new Dictionary<string, bool>();
        foreach (var phraseGroup in phraseGroups)
        {
            //List<ChessView> chessViews = new List<ChessView>();
            // 1. 拿当前词组所有格子
            List<ChessView> chessViews = phraseGroup.chesspieces
                .Select(p => GridList.GetValueOrDefault((p.row, p.col)))
                .Where(v => v != null)
                .ToList();
            // 2. 只要有空格（未填）→ 全部正常色 + 跳过
            int filled = chessViews.Count(v => v.CurrState != TileState.None && v.CurrState != TileState.Check);
            if (filled < chessViews.Count)
            {
                result.Add(phraseGroup.id, true); // 有未填的, 查找下一个
                continue;
            }

            // 3. 全部已填 → 比对答案
            bool allCorrect = chessViews.All(v => v.Correct);
            if (allCorrect)
            {
                result.Add(phraseGroup.id, true); // 该组正确
                // 全对 → 统一绿色 ， 如果需要做动画，将这组拿出来最后做
                selectedPuzzle.Clear();
                chessViews.ForEach(v =>
                {
                    if (v.chesspiece.bowl != null)
                    {
                        v.chesspiece.bowl.status = 2;
                        // Debug.Log("统一绿色时: "+ v.Answer +" " + JsonConvert.SerializeObject(v.chesspiece.bowl));
                        GamePlayArea.puzzleTileTable.OnNotifyResult(v.chesspiece.bowl, 2);
                    }
                    
                    v.SetTileState(TileState.Success, false);
                    selectedPuzzle.Append(v.Answer);
                    // GamePlayArea.ButterWordAddIcon(v);
                    //StartCoroutine(v.PlayErrorAnimation(true));
                 
                });
                // Debug.Log("添加的词组 " + string.Join(",", selectedPuzzle));
                // Debug.Log($"[CheckSuccessful] 帧={Time.frameCount}");
                checkGroup.Add(chessViews);
                // Debug.Log("在CheckSuccessful 填入词组" + phraseGroup.id + " "+selectedPuzzle.ToString());
                GamePlayArea.AddFoundPuzzle(selectedPuzzle.ToString());
                isPlaySound = true;
            }
            else
            {
               // selectedPuzzle.Clear();
                // 词组内的所有词都已填入，才判断是正确还是错误，正确设置绿色，错误设置红色，若词组内没有填完，则保持原色
                // 有错 → 已填入的都变红
                chessViews.Where(v => v.CurrState != TileState.Default && v.CurrState != TileState.Success)
                    .ToList()
                    .ForEach(v =>
                    {
                        v.SetTileState(TileState.Error, false);
                        if(nexterr == null)
                            nexterr = v;
                       // selectedPuzzle.Append(v.Answer);
                    });
                checkGroup.Add(chessViews);
                // Debug.Log("添加的词组 " + string.Join(",", selectedPuzzle));
                GamePlayArea.AddWordError(1);
                result.Add(phraseGroup.id, false);
            }
        }

        if (isPlaySound)
            AudioManager.Instance.PlaySoundEffect("ThemeCompleted");

        if (result.Values.All(re => re == true))
        {
            // Debug.Log("进入result 全对？");  //是否将这个当前字置为上一个待填字
            SearchNextTile();
        }
        else
        {
            //     Debug.Log("有错误，当前状态: " + selecteTile.CurrState);
            AudioManager.Instance.PlaySoundEffect("ChoiceError_UI");
            if (selecteTile.CurrState == TileState.Success)
            {
                //         Debug.Log("是否有错误 " + nexterr?.Answer);
                if (nexterr != null) // 当前是正确的，但是组内其他是错误，让其被选择
                {
                    selecteTile.SetChoose(false);
                    this.selecteTile = nexterr;
                    nexterr.SetChoose(true);
                    // ChessStageController.Instance.ModifyCursor(nexterr.Row, nexterr.Col);
                }
                // else // 没有其他错误，找下一个
                SearchNextTile();
            }
            else
            {
                //         // 当前错误，让其选择
                //         Debug.Log("进来了吗？ ");
                selecteTile.SetChoose(false);
                SearchNextTile();
                //         // 首次错误触发，新手引导
                GamePlayArea.HandleGamePlayCall(selecteTile.gameObject, "ChessError"); // 字块错误事件
            }
        }

        yield return new WaitForSeconds(0.3f);
    }

    private IEnumerator HandleChessUIState()
    {
        // Debug.Log("开始更新字块 " + checkGroup.Count);
        //Debug.Log($"开始更新字块 [HandleChessUIState] 帧={Time.frameCount}");
        List<List<ChessView>> toRemove = new List<List<ChessView>>();
        float maxDelay = 0f; // 记录最长的一波动画需要多久
        foreach (var chessList in checkGroup)
        {
            float delay = 0f; // 波浪延迟计数器
            foreach (var chessView in chessList)
            {
               // Debug.Log("更新的字块样式? " + chessView.Answer + " -> " + chessView.CurrState);
               if (chessView.CurrState == TileState.Success)
               {
                   chessView.SetChoose(false);
                   chessView.IsOK = true;
                   GamePlayArea.CheckPupaChess(chessView);
                   
                   chessView.PlaySuccessAnimation(delay, () => chessView.UpdateTile(true));
                   delay += 0.08f; // 每个字依次间隔 0.08 秒起跳
               }
               else if (chessView.CurrState is TileState.Error or TileState.Default)
               {
                    chessView.UpdateTile(true);
                    chessView.PlayError(true);
               }
            }
            if (delay > maxDelay) maxDelay = delay;
            toRemove.Add(chessList);
        }

        for (int i = toRemove.Count - 1; i >= 0; i--)
            checkGroup.Remove(toRemove[i]);
        // Debug.Log("更新结束后, " + checkGroup.Count);
        yield return new WaitForSeconds(maxDelay + 0.3f);
        yield return CheckCompleted();
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
        // 一. 查找当前字的组内关联
        List<PhraseGroup> phraseGroups = GetChessGroups(selecteTile.Row, selecteTile.Col).ToList();
        
        // foreach (var group in phraseGroups)
        // {
        //     bool clickedTail = (selecteTile.Row, selecteTile.Col) ==
        //                        (group.chesspieces.Last().row,
        //                            group.chesspieces.Last().col);
        //     var sequence = clickedTail
        //         ? group.chesspieces.AsEnumerable().Reverse()
        //         : group.chesspieces;
        //     foreach (var chess in sequence)
        //     {
        //         if (GridList.TryGetValue((chess.row, chess.col), out var chessView))
        //         {
        //             if (chessView.CurrState == TileState.None && selecteTile != chessView)
        //             {
        //                 SetCheckView(chessView);
        //                 return true;
        //             }
        //         }
        //     }
        // }

        // // 二. 查找当前字组内所有字的关联,从左到右,从上到下, 看看哪个字是关联组有空格，并且空格是交叉空格优先
        // var allPieces = phraseGroups.SelectMany(g => g.chesspieces).Distinct().ToList();
        // var bestEmpty = allPieces
        //     .SelectMany(cp => GetChessGroups(cp.row, cp.col))
        //     .Distinct()
        //     .SelectMany(g => g.chesspieces
        //         .Select(cp => new
        //         {
        //             cp.row,
        //             cp.col,
        //             view = GridList.TryGetValue((cp.row, cp.col), out var v) ? v : null,
        //             group = g
        //         }))
        //     .Where(x => x.view != null && x.view.CurrState == TileState.None)
        //     .OrderByDescending(x => GetChessGroups(x.row, x.col).Count(gg => gg != x.group) > 1 ? 1 : 0)
        //     .ThenBy(x => x.group.direction == 1 ? x.col : x.row)
        //     .FirstOrDefault();
        // if (bestEmpty != null)
        // {
        //     SetCheckView(bestEmpty.view);
        //     return true;
        // }
        // 三. 全局搜索最近的一个空格, 查找这个空格组内有没有交叉空格, 如果有选取它,如果没有则组内按顺序选择空格，而非最近的它
        // 1. 全部未成功格子
        var candidates = GridList.Values
            .Where(k => k.CurrState is TileState.None  or TileState.Error)
            .ToList();
        // Debug.Log("这里进入了吗 " + candidates.Count);
        if (!candidates.Any()) return true;

        Dictionary<ChessView, float> chessWeight = new Dictionary<ChessView, float>();
        foreach (var candidate in candidates)
        {
            chessWeight[candidate] = 0;
            if(candidate.Row == selecteTile.Row && candidate.Col == selecteTile.Col)
                chessWeight[candidate] += 500;
            
            bool inGroup = phraseGroups.Any(g =>
            {
                bool isIn = g.chesspieces.Contains(candidate.chesspiece);
                int idx = g.chesspieces.FindIndex(p => GridList.TryGetValue((p.row, p.col), out var view) && view.CurrState is TileState.None or TileState.Error);
                bool isFirst =  idx >= 0 && g.chesspieces[idx].Equals(candidate.chesspiece);
                return isIn && isFirst;
            });
            // Debug.Log($" {candidate.Answer} 是否是首字" + inGroup);
            if (inGroup)
                chessWeight[candidate] += 200;
            // bool ownFirst = phraseGroups.Any(g =>
            // {
            //     int idx = g.chesspieces.FindIndex(p => GridList.TryGetValue((p.row,p.col), out var view) && view.CurrState is TileState.None or TileState.Error);
            //     return idx >= 0 && g.chesspieces[idx].Equals(candidate.chesspiece);
            // });
            // if(ownFirst)
            //     chessWeight[candidate] += 80;
            
            // 空格子所属的所有组
            List<PhraseGroup> emptyGroups = GetChessGroups(candidate.Row, candidate.Col).ToList();
            // 判断空格子是否与选中格子存在交叉
            if(HasCrossWithSelected(candidate, emptyGroups, phraseGroups))
                chessWeight[candidate] += 100;
            
            // 空格和选中框是一个组, 那么比较组内的位置是否大于
            bool greaterSelect = phraseGroups.Any(g => g.chesspieces.Contains(candidate.chesspiece) && 
                                                       g.chesspieces.FindIndex(p=>p.Equals(selecteTile.chesspiece)) < g.chesspieces.FindIndex(p=>p.Equals(candidate.chesspiece)));
            if (greaterSelect)
                chessWeight[candidate] += 40;
            
            // 判断是否首个空位
            bool hasFirst = emptyGroups.Any(g =>
            {
                int idx = g.chesspieces.FindIndex(p => GridList.TryGetValue((p.row,p.col), out var view) && view.CurrState is TileState.None or TileState.Error);
                return idx >= 0 && g.chesspieces[idx].Equals(candidate.chesspiece);
            });
            // Debug.Log($"找到的是 {candidate.Answer} 位置:{candidate.Col} {candidate.Row}  首字: {hasFirst}");
            if(hasFirst)
                chessWeight[candidate] += 40;
            
            // 判断空格的词组有几个已填字
            int defaultCount = emptyGroups.Max(g => g.chesspieces.Count(p =>
                GridList.TryGetValue((p.row,p.col), out var view) && view.CurrState is TileState.Default or TileState.Fill or TileState.Success));
            chessWeight[candidate] += defaultCount * 10;
            // Debug.Log($"找到的是 {candidate.Answer} 位置:{candidate.Col} {candidate.Row}  交叉字: {defaultCount}");
            // 空位在成语中第n位，d += n * 2
            int maxIndex = EmptyIndexInGroup(candidate.chesspiece, emptyGroups) + 1;
            chessWeight[candidate] += maxIndex * 2;
            // 计算空格距离选中格子的距离
            int dist = ManhattanDistance(candidate.chesspiece, selecteTile.chesspiece);
            // Debug.Log($"找到的是 {candidate.Answer} 位置:{candidate.Col} {candidate.Row}  距离: {dist}");
            chessWeight[candidate] += 2f / (dist + 1f);
            // 空格是否有横向组
            bool hasHorizontal = emptyGroups.Any(g => g.direction == 1);
            if(hasHorizontal)
                chessWeight[candidate] += 0.1f;
            // 计算空格的Y轴分
            chessWeight[candidate] += candidate.Col * 0.01f;
            candidate.SetScore(chessWeight[candidate]);
        }
        if (chessWeight.Count == 0) return true;  
        ChessView maxView = chessWeight.Aggregate((kvp1, kvp2) => kvp1.Value > kvp2.Value ? kvp1 : kvp2).Key;
        if (maxView != null)
        {
            SetCheckView(maxView, maxView.CurrState is not TileState.Error);
            // Debug.Log($"找到的是 {maxView.Answer} 状态{maxView.CurrState} 位置:{maxView.Col} {maxView.Row} {maxView.Direction}");
        }
            
        
        return true;
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

    private void ShowTopPanel()
    {
        SystemManager.Instance.ShowPanel(PanelType.HeaderSection);
    }

    /// <summary>
    /// 创建字块
    /// </summary>
    private IEnumerator SetupGrid(bool isAmin, Action call = null)
    {
       
        checkGroup.Clear();
        HashSet<Chesspiece> boardData = CurrStageData.BoardSnapshot;
        // Debug.Log("棋盘数据 :" + JsonConvert.SerializeObject(boardData));
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
            }
        }
        
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
        LetterTilePool.ReturnAllObjectsToPool();
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
        PhraseGroup phrases = GetChessGroups(selecteTile.Row, selecteTile.Col, selecteTile.Direction).FirstOrDefault();

        return phrases.chesspieces
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
        // 0. 基础过滤
        var candidates = GridList.Values
            .Where(v => v.CurrState == TileState.None &&
                        v.chesspiece is { tip: false })
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
            .FirstOrDefault();

        if (crossFirst != null) return crossFirst.view;

        // 2. 无交叉 → 同样剔 tip 组后排序
        return candidates
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
            .First()
            .view;
    }

    /// <summary>
    /// 重置格子中心点
    /// </summary>
    public void ResetTilePivot()
    {
        foreach (var cell in GridList.Values)
        {
            cell.TileTransform.pivot = new Vector2(0.5f, 0.5f);
        }
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
}