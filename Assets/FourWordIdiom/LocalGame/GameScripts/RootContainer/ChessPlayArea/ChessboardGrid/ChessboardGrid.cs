using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    
    private ObjectPool LetterTilePool;
    private ObjectPool GroupShinePool;

    public ChessPlayArea GamePlayArea { get; private set; }

    // 存放棋盘的字块
    public readonly Dictionary<(int row, int col), ChessView> GridList = new();
    public readonly Dictionary<ChessView, int> TileErrorCounts = new();
    public bool GameOver { get; private set; }
    // 需要更新的字块
    //private readonly HashSet<ChessView> updateViews = new();
    // 当前选择的格子
    public ChessView selecteTile;
    private StringBuilder selectedPuzzle; // 完成词的收集
    // 🌟 新增变量：用于缓存等待触发报错引导的格子
    public ChessView pendingErrorTutorialTile;
    public void Initialize(ChessPlayArea play)
    {
        if (PuzzleItemObj == null)
        {
            PuzzleItemObj = AssetBundleLoader.SharedInstance.LoadGameObject("commonitem", "ChessTileView");
        }
        if (_groupShinePrefab == null)
        {
            _groupShinePrefab = AssetBundleLoader.SharedInstance.LoadGameObject("commonitem", "WaitGroupShineEffect");
        }
        GroupShinePool = new ObjectPool(_groupShinePrefab.gameObject, ObjectPool.CreatePoolContainer(transform,"GroupShinePool"), 3, PoolBehaviour.GameObject);
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
        HashSet<string> previouslyCompletedIds = new HashSet<string>();
        var targetGroup = GetChessGroups(selecteTile.Row, selecteTile.Col)
            .OrderByDescending(g => g.direction) // 1 优先
            .FirstOrDefault(); // 只取一
        
        if (targetGroup == null) yield break;
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
                if (!groupSuccess) // 最高优先级
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
                    // selectedPuzzle.Clear();
                    // group.chesspieces.ForEach(v => selectedPuzzle.Append(v.letter));
                    // string foundWord = selectedPuzzle.ToString();
                    // // Debug.Log("在 CheckChessGroupState 填入词组" + group.id + " "+selectedPuzzle.ToString());
                    // if (!CurrStageData.FoundTargetPuzzles.Contains(foundWord))
                    // {
                    //     GamePlayArea.AddFoundPuzzle(foundWord);
                    // }
                    // GamePlayArea.AddFoundPuzzle(selectedPuzzle.ToString());
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
                if (curr.Answer != selecteTile.Answer)
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
            // Debug.Log("执行2");
            // yield return new WaitUntil(() => checkGroup.Count == 0);
            // yield return CheckCompleted();
            // Debug.Log("执行3");
            // 🌟 【终极修复】：在这里！等所有华丽特效全部播完、屏幕安稳下来后，再安全弹出引导！
           
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
        ChessView view = GridList.Values.FirstOrDefault(grid => grid.chesspiece?.bowl?.id == puzzle.bowl.id);
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
            
            if (chessViews.All(v => v.CurrState == TileState.Success || 
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
                // Debug.Log("添加的词组 " + string.Join(",", selectedPuzzle));
                // Debug.Log($"[CheckSuccessful] 帧={Time.frameCount}");
                // checkGroup.Add(chessViews);
                correctGroups.Add(chessViews);
                // Debug.Log("在CheckSuccessful 填入词组" + phraseGroup.id + " "+selectedPuzzle.ToString());
                // GamePlayArea.AddFoundPuzzle(selectedPuzzle.ToString());
                // isPlaySound = true;
                // 👇 🌟 冰块玩法：检测相邻格子并碎冰
                // BreakAdjacentIce(chessViews);
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
        }
        else
        {
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
        
        List<GameObject> allShineInstances = new List<GameObject>();
        const float effectDuration = 1.2f;
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

            // 4. 🎯 收割树叶快照：由于刚才没有抢跑改写，树叶数据完好无损，100% 成功起飞飞向滑块！
            // foreach (var view in viewsInGroup)
            // {
            //     if (view.chesspiece.hasLeaf)
            //     {
            //         GamePlayArea.PlayLeafFlyToCollectionPoint(view.transform);
            //         view.chesspiece.hasLeaf = false; // 飞走解绑
            //         ChessStageController.Instance.CurrStageData.CollectedLeaves++;
            //     }
            // }
        }
        
        // ==========================================
        // 🟢 先统一播放【正确组】的华丽特效
        // ==========================================
        for (int groupIdx = 0; groupIdx < correctGroups.Count; groupIdx++)
        {
                var viewsInGroup = correctGroups[groupIdx];
                // StringBuilder sb = new StringBuilder();
                // foreach (var v in viewsInGroup) sb.Append(v.Answer);
                
                bool hasIceBroken = BreakAdjacentIce(viewsInGroup);
                bool hasBloomingFlower = ProcessFlowerBlooming(viewsInGroup);
                // 如果有花朵，等待花朵绽放动画播完，再进行后续的闪光框和飘分！
                if (hasBloomingFlower || hasIceBroken)
                {
                    yield return new WaitForSeconds(0.4f); 
                }
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
                // int baseScore = ChessStageController.Instance.GetBaseScore();
                // int comboBonus = ChessStageController.Instance.GetComboScoreReward(ChessStageController.Instance.PuzzleComboCount);
                // int scoreDiff = baseScore + comboBonus;
                int finalGroupScore = groupActualScores[groupIdx];
                int currentComboInSystem = ChessStageController.Instance.PuzzleComboCount;
                GamePlayArea.ShowBoardFloatingScore(bestView.transform, dir, finalGroupScore, currentComboInSystem >= 2);
                // GamePlayArea.ShowBoardFloatingScore(bestView.transform, dir, scoreDiff, ChessStageController.Instance.PuzzleComboCount >= 2);
                // GamePlayArea.ScoreFlyPos = bestView.transform.position;
                // GamePlayArea.AddFoundPuzzle(sb.ToString());
                
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
                        view.PlayLeafFillFailedAnim();
                        
                        // 永久剥夺本关后续生成树叶的权利！
                        ChessStageController.Instance.IsLeafDeadThisLevel = true; 
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
    public bool SearchNextTile2()
    {
        // 1. 同步计算并挪动底层基础操作光标（继续保持原有的同组优先手感）
        ChessView bestChoice = GetBestNextTile();
        if (bestChoice == null) return true;
        
        SetCheckView(bestChoice, bestChoice.CurrState is not TileState.Error);
        return true;
    }
 
    /// <summary>
    /// 核心算法：计算下一个最优的空格子
    /// （完美融合了原版同组优先手感 + 冰块花朵树叶新规则）
    /// </summary>
    private ChessView GetBestNextTile()
    {
        var candidates = GridList.Values
            .Where(v => (v.CurrState == TileState.None || v.CurrState == TileState.Error) 
                        && !v.chesspiece.hasIce) // 绝对禁止选冰块
            .ToList();

        if (!candidates.Any()) return null;

        // 🌟 提取当前上下文：获取当前选中的格子所属的词组
        List<PhraseGroup> activeGroups = selecteTile != null 
            ? GetChessGroups(selecteTile.Row, selecteTile.Col).ToList() 
            : new List<PhraseGroup>();
        
        // 🚀 核心修正：获取玩家当前正在填写的词组方向（1为横向/左右，0为纵向/上下）
        // 如果当前选中的格子有明确方向，以此为准；否则默认优先横向
        int currentDirection = selecteTile != null ? selecteTile.Direction : 1;
        if (activeGroups.Count == 1) 
        {
            currentDirection = activeGroups[0].direction;
        }
        int solvedCount = CurrStageData.FoundTargetPuzzles.Count;
        bool isEarlyGame = solvedCount <= 1;
        var sortedCandidates = candidates.Select(candidate =>
        {
            var myGroups = GetChessGroups(candidate.Row, candidate.Col).ToList();
            // ================== 原版手感核心还原 ==================
            bool isForwardInSameDir = false;   // 1. 同向且在当前格子之后 (完美顺延)
            bool isForwardInOtherDir = false;  // 2. 交叉向且在当前格子之后 (拐弯顺延)
            bool isBackwardInSameDir = false;  // 3. 同向但在当前格子之前 (同向回头草)
            bool isBackwardInOtherDir = false; // 4. 交叉向但在当前格子之前 (交叉回头草)
            
            if (selecteTile != null)
            {
                foreach (var ag in activeGroups)
                {
                    // 1. 找到当前光标在组内的索引
                    int currentIndex = ag.chesspieces.FindIndex(p => p.Equals(selecteTile.chesspiece));
                    if (currentIndex < 0) continue;
                    
                    // 1. 优先尝试向后寻找空格 (Forward)
                    int forwardEmptyIdx = ag.chesspieces.FindIndex(currentIndex + 1, p => 
                        GridList.TryGetValue((p.row, p.col), out var view) && 
                        (view.CurrState == TileState.None || view.CurrState == TileState.Error));
                    // 2. 如果向后没找到，才允许向前折返寻找 (Backward)
                    int backwardEmptyIdx = -1;
                    if (forwardEmptyIdx == -1) 
                    {
                        backwardEmptyIdx = ag.chesspieces.FindIndex(0, currentIndex, p => 
                            GridList.TryGetValue((p.row, p.col), out var view) && 
                            (view.CurrState == TileState.None || view.CurrState == TileState.Error));
                    }
                    // 3. 将候选格子对号入座
                    if (forwardEmptyIdx >= 0 && ag.chesspieces[forwardEmptyIdx].Equals(candidate.chesspiece))
                    {
                        if (ag.direction == currentDirection) isForwardInSameDir = true;
                        else isForwardInOtherDir = true;
                    }
                    else if (backwardEmptyIdx >= 0 && ag.chesspieces[backwardEmptyIdx].Equals(candidate.chesspiece))
                    {
                        if (ag.direction == currentDirection) isBackwardInSameDir = true;
                        else isBackwardInOtherDir = true;
                    }
                }
            }
            
            // 🚀 核心修正：智能顺位权重（IndexInPhrase）
            // 如果候选格子存在与当前操作“同方向”的成语，优先取同方向的顺位！
            // 这样从上往下填遇到交叉格时，会锁定纵向成语的 Index（例如 2），而不会被横向成语的 Index 0 带偏。
            int indexInPhrase = 4;
            if (myGroups.Any())
            {
                var sameDirGroup = myGroups.FirstOrDefault(g => g.direction == currentDirection);
                if (sameDirGroup != null)
                    indexInPhrase = sameDirGroup.chesspieces.FindIndex(p => p.Equals(candidate.chesspiece));
                else // 如果没有同方向的组，才取其他方向的最小顺位
                    indexInPhrase = myGroups.Min(g => g.chesspieces.FindIndex(p => p.Equals(candidate.chesspiece)));
                
            }
            // 2. 是否与当前词组存在交叉？(调用你原本写的 HasCrossWithSelected)
            bool isCrossWithCurrent = HasCrossWithSelected(candidate, myGroups, activeGroups);
            
            // ================== 新玩法规则 ==================
            // 4. 特殊玩法降权 (花朵)
            bool hasFlowerInGroup = myGroups.Any(g => g.chesspieces.Any(p => p.hasFlower));
            int mechanicPriority = hasFlowerInGroup ? 1 : 2; // 冰块已经在最开始被排除了，正常=2，花朵=1
            
            // 5. 树叶空格策略
            int emptyCount = myGroups.Min(g => g.chesspieces.Count(p => 
                GridList.TryGetValue((p.row, p.col), out var view) && 
                (view.CurrState == TileState.None || view.CurrState == TileState.Error)));
            
            // int dist = selecteTile != null ? ManhattanDistance(candidate.chesspiece, selecteTile.chesspiece) : 0;
            int groupMinDist = int.MaxValue;
            if (selecteTile != null && myGroups.Any())
            {
                groupMinDist = myGroups.Min(g => g.chesspieces
                    .Where(p => GridList.TryGetValue((p.row, p.col), out var view) && 
                                (view.CurrState == TileState.None || view.CurrState == TileState.Error))
                    .Select(p => ManhattanDistance(p, selecteTile.chesspiece))
                    .DefaultIfEmpty(int.MaxValue) // 兜底防止组内无空格报错
                    .Min());
            }
            
            // 成语成熟度
            int maxFilledCountInGroups = myGroups.Any() ? myGroups.Max(g => g.chesspieces.Count(p =>
                GridList.TryGetValue((p.row, p.col), out var view) && 
                view != null && (view.CurrState is TileState.Default or TileState.Fill or TileState.Success)
            )) : 0;
            float groupMaturityScore = maxFilledCountInGroups * 10f;
            // 空间微调：让导航产生倾向性（如果当前是纵向，微弱提升纵向格子的分数）
            bool hasSameDirection = myGroups.Any(g => g.direction == currentDirection);
            float spatialFineTuneScore = (hasSameDirection ? 0.5f : 0f) + (candidate.Col * 0.01f);

            float combinedTile2Score = groupMaturityScore + spatialFineTuneScore;
            
            // 6. 自身交叉情况 & 组内索引
            // bool isGlobalCross = myGroups.Count > 1;
            // int minIndex = EmptyIndexInGroup(candidate.chesspiece, myGroups); // 使用你原本写好的 EmptyIndexInGroup

            // 打包所有维度的数据，准备交由 LINQ 仲裁
            return new
            {
                View = candidate,
                IsForwardInSameDir = isForwardInSameDir,     // 标识 1
                IsForwardInOtherDir = isForwardInOtherDir,   // 标识 2
                IsBackwardInSameDir = isBackwardInSameDir,   // 标识 3
                IsBackwardInOtherDir = isBackwardInOtherDir, // 标识 4
                IsCrossWithCurrent = isCrossWithCurrent,
                MechanicPriority = mechanicPriority,
                EmptyCount = emptyCount,
                Tile2Score = combinedTile2Score,
                groupMinDist = groupMinDist,
                IndexInPhrase = indexInPhrase, // 🌟 捕获首位分值
            };
        })
        // 🏆 ---------------- 开始链式排序 (权重从上往下，绝对压制) ---------------- 🏆
        .OrderByDescending(x => x.IsForwardInSameDir)      // 【最优先】同向，且位置靠后的顺延
        .ThenByDescending(x => x.IsForwardInOtherDir)      // 【第二优】交叉向，且位置靠后的顺延（哪怕要拐弯，也绝不吃同向的回头草）
        .ThenByDescending(x => x.IsBackwardInSameDir)      // 【第三优】顺延无路可走，开始尝试同向折返
        .ThenByDescending(x => x.IsBackwardInOtherDir)     // 【第四优】交叉向折返
            .ThenByDescending(x => x.IsCrossWithCurrent)    // 【顺位3】🔥(修复点) 优先跳入与刚刚完成的词相交的词组！
            .ThenByDescending(x => x.MechanicPriority)      // 【顺位4】花朵等特殊机制优先
            .ThenBy(x => isEarlyGame ? -x.EmptyCount : x.EmptyCount) // 【顺位5】树叶等空格策略
            .ThenByDescending(x => x.Tile2Score)            // 【顺位6】成熟度与方向得分
            .ThenBy(x => x.groupMinDist)                        // 【顺位7】找最近的距离
            .ThenBy(x => x.IndexInPhrase)                   // 【顺位8】🔥(修复点) 降级为最终兜底，确保进入新词时优选首个字
            .ToList();

        return sortedCandidates.FirstOrDefault()?.View;
    }
    /// <summary>
    /// 🌟 规范新增：事件驱动型全局树叶自适应刷新器
    /// 只有在开局、消除成功起飞、填满失败枯萎时，才会被精准调用一次！
    /// </summary>
    public void GenerateNextLevelLeaf()
    {
        bool isLeafLevel = ChessStageController.Instance.CheckLeafMechanic(ChessStageController.Instance.CurrentStage, out _);
        bool isLeafAlive = !ChessStageController.Instance.IsLeafDeadThisLevel;

        if (!isLeafLevel || !isLeafAlive)
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
        if (isFirstLeafEntry && selecteTile != null && selecteTile.CurrState == TileState.Check|| selecteTile.CurrState == TileState.None)
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
            .OrderBy(x => preferMoreEmpty ? -x.EmptyCount : x.EmptyCount)
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
                        .OrderBy(v => GetChessGroups(v.Row, v.Col).Count() > 1 ? 1 : 0) 
                        .ThenBy(v => bestGroup.chesspieces.FindIndex(p => p.Equals(v.chesspiece))) 
                        .FirstOrDefault();
                    // ChessStageController.Instance.LeafGenCounter++; // 换肤计数累加
                    bestLeafTile?.ShowLeaf(true); // 树叶亮起，随后在此死死钉住，直到下一次事件爆发！
                }
            }
        }
    }
    /// <summary>
    /// 查找下一个空白格子
    /// </summary>
    public bool SearchNextTile()
    {
        // 一. 查找当前字的组内关联
        List<PhraseGroup> phraseGroups = GetChessGroups(selecteTile.Row, selecteTile.Col).ToList();
        // 1. 全部未成功格子
        var candidates = GridList.Values
            // .Where(k => k.CurrState is TileState.None  or TileState.Error)
            .Where(k => (k.CurrState is TileState.None or TileState.Error) && !k.chesspiece.hasIce)
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
    public List<ChessView> GetCurrentSelectGroup2()
    {
        PhraseGroup phrases = GetChessGroups(selecteTile.Row, selecteTile.Col)
            .OrderByDescending(g => g.direction) // 1 优先
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
        // 0. 基础过滤
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
        foreach (var v in completedGroupViews) tilesToBreak.Add(v);
        // 如果邻居有冰块，打碎它！
        foreach (var neighbor in tilesToBreak)
        {
            if (neighbor.chesspiece.hasIce)
            {
                neighbor.chesspiece.hasIce = false;
                StartCoroutine(neighbor.PlayIceBreakAnim());
                isBroken = true;
            }
        }
        if (isBroken)
        {
            // AudioManager.Instance.PlaySoundEffect("IceBreak"); 
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
        int solvedWords = currStageData.FoundTargetPuzzles.Count + 1; // +1 因为当前这组正在结算
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
            StartCoroutine(f.PlayFlowerBloomAnim());
            hasBlooming = true;
        }

        return hasBlooming;
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

    private void OnBowlGoldCollected(BowlView bowl)
    {
        // // 从当前需要金箔的集合中移除该字母
        // if (_currentGoldLetters.Contains(bowl.letter))
        //     _currentGoldLetters.Remove(bowl.letter);
        //
        // // 触发金箔奖励逻辑（例如增加玩家金箔数量）
        // GameDataManager.Instance.UserData.UpdateGoldLeaf(1); // 假设获得1个金箔
        // MessageSystem.Instance.ShowTip("获得金箔 +1");
        //
        // // 若集合为空，可触发额外效果
        // if (_currentGoldLetters.Count == 0)
        // {
        //     // 所有金箔已被收集，可刷新UI或播放音效
        // }
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
    
}