using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FourWordIdiom.LocalGame.GameScripts.Controller;
using UnityEngine;
using UnityEngine.UI;

/**
 * 核心流水线 (ChessPlayArea.Flow.cs)
 * 负责游戏核心流水线：通关、找对词、错误处理
 */
public partial class ChessPlayArea
{
    private IEnumerator SetupGameData()
    {
        _hasTriggeredHintReminderThisLevel = false;
        _isStuckTimerRunning = false;
        _stuckTimer = 0f;

        //清理一下棋盘
        chessboardGrid.Clear();
        puzzleTileTable.Clear();
        yield return new WaitForEndOfFrame();
        EventDispatcher.instance.TriggerChangeTopRaycast(false);
        SetToolButtonsEnabled(false); // 强制禁用所有道具按钮
        chessboardGrid.IsBlockInput = true; // 强制锁死棋盘
        ChessBowlGrid.IsTutorialBlocking = true;
        
        IsClickAuto = false;
        // ==========================================
        // 先执行你原版的缩放和尺寸适配逻辑
        // ==========================================
        RectTransform chessRectTransform = chessboardGrid.GetComponent<RectTransform>();
        RectTransform btnParent = HitsBtn.transform.parent.GetComponentInParent<RectTransform>();
        RectTransform bowlRectTransform = puzzleTileTable.GetComponent<RectTransform>();

        if (UIUtilities.IsiPad())
        {
            VerticalLayoutGroup vlg = chessboardGrid.transform.parent.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                vlg.childControlWidth = false;
                vlg.childForceExpandWidth = false;
                vlg.childAlignment = TextAnchor.UpperCenter; // 保证iPad上的1242容器整体居中
            }

            chessRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, UIUtilities.REFERENCE_WIDTH);
            btnParent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, UIUtilities.REFERENCE_WIDTH);
            bowlRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, UIUtilities.REFERENCE_WIDTH + 2);
            chessRectTransform.localScale = Vector3.one;
            bowlRectTransform.localScale = Vector3.one;
            btnParent.localScale = Vector3.one;
        }
        else
        {
            float scale = UIUtilities.GetScreenRatio();
            if (scale < 0.85f)
            {
                chessRectTransform.localScale = new Vector3(scale + 0.08f, scale + 0.08f, scale + 0.08f);
                bowlRectTransform.localScale = new Vector3(scale + 0.06f, scale + 0.06f, scale + 0.06f);
                btnParent.localScale = new Vector3(scale, scale, scale);
            }
            else if (scale > 1f)
            {
                VerticalLayoutGroup vlg = chessboardGrid.transform.parent.GetComponent<VerticalLayoutGroup>();
                if (vlg != null)
                {
                    vlg.childControlWidth = false;
                    vlg.childForceExpandWidth = false;
                    vlg.childAlignment = TextAnchor.UpperCenter; // 保证胖手机上的1242容器整体居中
                }

                chessRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                    UIUtilities.REFERENCE_WIDTH);
                btnParent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, UIUtilities.REFERENCE_WIDTH);
                bowlRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                    UIUtilities.REFERENCE_WIDTH + 2);
            }
        }

        // ==========================================
        // 强制刷新布局，确保下面拿到的是设置后的真实宽度
        // ==========================================
        yield return new WaitForEndOfFrame();
        LayoutRebuilder.ForceRebuildLayoutImmediate(chessboardGrid.GetComponent<RectTransform>());
        float spacing = 4f;
        // 设置尺寸
        // int maxRow = CurrStageData.MaxRow;
        // int maxCol = CurrStageData.MaxCol;
        // int minRow = CurrStageData.MinRow;
        // int minCol = CurrStageData.MinCol;
        int rowCount = CurrStageData.MaxRow - CurrStageData.MinRow + 1;
        int colCount = CurrStageData.MaxCol - CurrStageData.MinCol + 1;
        float boardWidth = chessboardGrid.GetComponent<RectTransform>().rect.width; // 棋盘可用宽度
        float boardHeight = chessboardGrid.GetComponent<RectTransform>().rect.height; // 棋盘可用高度
        float widthTotalSpacing = (rowCount - 1) * spacing;
        float heightTotalSpacing = (colCount - 1) * spacing;

        float usableWidth = boardWidth - spacing * 2 - widthTotalSpacing;
        float usableHeight = boardHeight - spacing * 2 - heightTotalSpacing;

        float singleWidth = Mathf.Min(usableWidth / rowCount, 161f);
        float singleHeight = Mathf.Min(usableHeight / colCount, 161f);
        float usableSize = Mathf.Min(singleWidth, singleHeight);
        float leftMargin = (boardWidth - (usableSize * rowCount + widthTotalSpacing)) / 2f + 2;
        float bottomMargin = (boardHeight - (usableSize * colCount + heightTotalSpacing)) / 2f;

        // Debug.Log($"棋盘宽{boardWidth} 高{boardHeight} 内最大row {maxRow} 最小row {minRow}, 最大col {maxCol} 最小col {minCol}, 相差row {rowCount} 相差col {colCount}");
        // Debug.Log($"左边距{leftMargin} 底边距{bottomMargin} 每格尺寸: {usableSize-1} × {usableSize-2} 像素");
        ChessStageController.Instance.CurrStageData.ActiveSize = new Vector2(usableSize - 1, usableSize - 2);
        startLocation = (leftMargin, bottomMargin);

        //// 设置待填字
        GridLayoutGroup grid = puzzleTileTable.GetComponent<GridLayoutGroup>();
        int puzzleCount = ChessStageController.Instance.CurrStageData.Puzzles.Count;
        RectTransform gridRect = grid.GetComponent<RectTransform>();
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(gridRect.parent.GetComponent<RectTransform>());
        float availableWidth = gridRect.rect.width;
        int desiredCols = Mathf.CeilToInt(puzzleCount / 4f);
        int colCount2 = Mathf.Clamp(desiredCols, 6, 8);
        // int colCount2 = Mathf.Max(6, Mathf.CeilToInt(puzzleCount / 4f));
        float spacingX = grid.spacing.x;
        float paddingLeft = grid.padding.left;
        float paddingRight = grid.padding.right;
        float totalCellWidth = availableWidth - paddingLeft - paddingRight - (colCount2 - 1) * spacingX;
        float cellWidth = totalCellWidth / colCount2;
        cellWidth = Mathf.Min(cellWidth, 200f);
        float cellHeight = cellWidth - 2f;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = colCount2;
        grid.cellSize = new Vector2(cellWidth, cellHeight);
        grid.transform.localScale = Vector3.one;
        yield return null;
        puzzleTileTable.transform.parent.gameObject.SetActive(true);
        yield return SetupGame();
        // 数据处理
        UsedPuzzles.Clear();
        // 填入最后一个单词
        var puzzles = CurrStageData.FoundTargetPuzzles;
        if (puzzles != null && puzzles.Count > 0)
        {
            string word = puzzles[^1]; // 或 puzzles[0]
            UpdateLevelData(word);
            foreach (var puzzle in puzzles)
            {
                UsedPuzzles.Add(puzzle);
            }
        }

        yield return null;
        // 让棋盘开始显示出来
        bool isAnimFinished = false;
        PlayEnterAnimation(() =>
        {
            isAnimFinished = true; // 动画播完，标记设为 true
        });
        // 协程在这里暂停，直到 isAnimFinished 变成 true 才往下走
        yield return new WaitUntil(() => isAnimFinished);
        _currentWordActiveSeconds = 0f;
        wordUserSeconds = 0;
        ResetTimeWarning();
        _remainingTime = CurrStageData.RemainingTime;
        _isTimerRunning = false;
        UpdateTimerUI();
        
        if (ChessStageController.Instance.IsFirstEnterStage)
        {
            yield return new WaitForSeconds(1f);
            ShopManager.shopManager.ShowLimitAdsPanel();
        }
        
        // bool gotoNext = false;
        // if (!ChessStageController.Instance.IsFirstEnterStage)
        // {
        //     _isTimerRunning = false;
        //     yield return new WaitForSeconds(0.1f);
        //     // 1. 先冻结时间，屏蔽底下的棋盘点击
        //     // EventDispatcher.instance.TriggerChangeTopRaycast(false);
        //     RectTransform captureArea = chessboardGrid.GetComponent<RectTransform>();
        //     Sprite snapshot = null;
        //     yield return CaptureBoardSnapshot(captureArea, (sprite) => {
        //         snapshot = sprite;
        //     });
        //     
        //     // 2. 呼出重连弹窗
        //     SystemManager.Instance.ShowPanel(PanelType.ContinueGameWindow);
        //     var continueWindow = SystemManager.Instance.GetPanel(PanelType.ContinueGameWindow).GetComponent<ContinueGameWindow>();
        //     continueWindow.Init(
        //         remainTime: _remainingTime, // 传入刚才恢复的剩余时间
        //         boardSnapshot: snapshot, // 传给弹窗脚本
        //         onContinue: () => 
        //         {
        //             // 玩家点击【继续】：时间开始流逝，解除屏幕屏蔽
        //             _isTimerRunning = true;
        //             gotoNext = true;
        //             // EventDispatcher.instance.TriggerChangeTopRaycast(true);
        //         },
        //         onQuit: () =>
        //         {
        //             QuitGameAndDeductEnergy();
        //         }
        //     );
        // }
        // else
        // {
        //     // 如果是全新开局，没有任何阻挡，直接让时间开跑
        //     _isTimerRunning = false;
        //     gotoNext = true;
        // }
        // yield return new WaitUntil(() => gotoNext);
        if (ChessStageController.Instance.IsFirstEnterStage)
        {
            ChessStageController.Instance.CurLevelMode =
                ChessStageController.Instance.GetLevelDifficultyMode(CurrStageData.StageId);

            switch (ChessStageController.Instance.CurLevelMode)
            {
                case LevelModes.Normal:
                    break;
                case LevelModes.Hard:
                    SystemManager.Instance.ShowPanel(PanelType.HardView);
                    yield return new WaitForSeconds(0.8f);
                    SystemManager.Instance.HidePanel(PanelType.HardView);
                    break;
                case LevelModes.ExtraHard:
                    SystemManager.Instance.ShowPanel(PanelType.HardView);
                    yield return new WaitForSeconds(0.8f);
                    SystemManager.Instance.HidePanel(PanelType.HardView);
                    break;
            }
        }

        yield return null;
        // 检查一下是否存在错误的成功状态
        chessboardGrid.FixChessState();
        // 触发新手引导检查
        chessboardGrid.IsBlockInput = false;
        ChessBowlGrid.IsTutorialBlocking = false;
        EventDispatcher.instance.TriggerCheckShowChessTutorial();
        yield return new WaitUntil(() => !SystemManager.Instance.PanelIsShowing(PanelType.ChessLearningGuide));
        AdRuleManager.Instance.TryShowBanner();
        yield return new WaitForSeconds(0.1f);
        // 飞蝴蝶道具
        if (ChessStageController.Instance.IsFirstEnterStage && useButterflyCount <= 2)
        {
            if (!new[] { 1, }.Contains(CurrStageData.StageId))
            {
                for (int i = 0; i < useButterflyCount; i++)
                {
                    GameObject effectButt = Instantiate(butterflyPrefab, butterflyObj.transform.parent);
                    EffectButterFlays.Add(effectButt);
                }

                ToolInfo toolInfo = GameDataManager.Instance.UserData.toolInfo[103];
                if (toolInfo.count > 0 || GameDataManager.Instance.UserData.butterflyTaskIsOpen)
                {
                    GameBase.GetComponent<CanvasGroup>().blocksRaycasts = false;
                    EventDispatcher.instance.TriggerChangeTopRaycast(false);
                    yield return new WaitForSeconds(0.2f);
                    UseButterfly();
                    yield return new WaitForSeconds(1.2f);
                    GameBase.GetComponent<CanvasGroup>().blocksRaycasts = true;
                }
            }

            ComboErrorCount = 0;
            wordErrorCount = 0;
            usetoolCount = 0;
        }
        
        SetToolButtonsEnabled(true);
        EventDispatcher.instance.TriggerChangeTopRaycast(true);
        AutoPassLevel();
    }

    public IEnumerator SetupGame()
    {
        chessboardGrid.ClearAllGoldLeafOnBowls();
        chessboardGrid.CreateChess();
        puzzleTileTable.CreatePuzzle();
        yield return new WaitUntil(() => chessboardGrid.GridList.Count > 0);

        if (CurrStageData.FoundTargetPuzzles.Count <= 0)
        {
            bool hasGoldLeaf = false;

            foreach (var bowl in CurrStageData.Puzzles)
            {
                if (bowl.isGoldLeaf)
                {
                    hasGoldLeaf = true;
                    break;
                }
            }

            if (!hasGoldLeaf && ThemeManager.Instance.CanShowGoldLeaf())
            {
                //显示金箔
                int showGoldLeafCount =
                    Random.Range(ThemeManager.Instance.CountRange.x, ThemeManager.Instance.CountRange.y + 1);

                //List<ChessView> goldLeafPositions = chessboardGrid.SelectGoldLeafPositions(showGoldLeafCount);
                chessboardGrid.ShowGoldLeafFromChessboard(chessboardGrid, showGoldLeafCount);
            }
        }
        else if (CurrStageData.GoldLeafCount > 0)
        {
            Debug.Log(string.Format("{0} 关，金箔生成数量 {1}", CurrStageData.StageId, CurrStageData.GoldLeafCount));
        }
    }

    /// <summary>
    /// 处理游戏内操作回调
    /// </summary>
    /// <param name="game">点击的物体</param>
    /// <param name="source">操作名称</param>
    public void HandleGamePlayCall(GameObject game, string source)
    {
        NotifyPlayerInteraction(); // 🌟 触发唤醒计时
        if (SystemManager.Instance.PanelIsShowing(PanelType.ChessLearningGuide))
        {
            // 如果当前正在展示的是“撤回错字 (教程3)”引导，绝对不允许后台迟到的 SetChess 事件进来捣乱！
            if (source == "SetChess" && ChessGuideSystem.Instance.currentTutorial == 3)
            {
                Debug.LogWarning("拦截时序冲突：错误引导展示中，丢弃迟到的 SetChess 事件");
                return;
            }
        }

        ChessGuideSystem.Instance.activeToolObject = game;
        ChessGuideSystem.Instance.toolSourceName = source;
        if (source == "SetChess")
        {
            if (!SystemManager.Instance.PanelIsShowing(PanelType.ChessLearningGuide))
                return;
            ChessGuideSystem.Instance.OnClickCallback();
        }
        else if (source == "ChessError" && GameDataManager.Instance.UserData.ChessTutorialProgress[3] == false)
        {
            // if (game == null) return;
            // ChessView targetChess = game.GetComponent<ChessView>();
            // if (targetChess == null || targetChess.CurrState == TileState.Success || targetChess.IsOK)
            // {
            //     Debug.LogWarning("🚨 [绝对防御] 拦截死锁：目标字块已处于 Success 状态，放弃弹出撤回引导。");
            //     return; 
            // }

            ChessGuideSystem.Instance.ChesspieceList = new List<ChessView> { game.GetComponent<ChessView>() };
            if (SystemManager.Instance.PanelIsShowing(PanelType.ChessLearningGuide))
                ChessGuideSystem.Instance.OnClickCallback();
            else
            {
                ChessGuideSystem.Instance.currentTutorial = 3;
                ChessGuideSystem.Instance.DisplayGuide();
            }
        }
        else if (source == "ClickChess")
        {
            // Debug.LogWarning("点击了棋子：" + source);
            if (!SystemManager.Instance.PanelIsShowing(PanelType.ChessLearningGuide))
                return;

            ChessGuideSystem.Instance.OnClickCallback();
        }
        else if (source == "UseTips")
        {
            ChessGuideSystem.Instance.activeToolObject = game;
            ChessGuideSystem.Instance.OnClickCallback();
        }
        else
        {
            ChessGuideSystem.Instance.CloseGuide();
        }
    }

    // 填字新手检查事件
    private void CheckShowChessTutorialEvent()
    {
#if Unity_ShowLog || UNITY_EDITOR
        if (GameCoreManager.Instance.IsTrueAuto) return;
#endif
        CheckShowChessTutorial();
    }

    private void CheckShowChessTutorial()
    {
        if (CurrStageData.StageId == 1 && !GameDataManager.Instance.UserData.ChessTutorialProgress[1])
        {
            ChessGuideSystem.Instance.ChesspieceList = chessboardGrid.GetCurrentSelectGroup();
            foreach (BowlView bowlView in puzzleTileTable.GridList)
            {
                ChessGuideSystem.Instance.TargetPuzzle.Add(bowlView);
            }

            ChessGuideSystem.Instance.currentTutorial = 1;
            ChessGuideSystem.Instance.toolSourceName = "FirstStage";
            ChessGuideSystem.Instance.activeToolObject = puzzleTileTable.GridList[0].gameObject;
            ChessGuideSystem.Instance.DisplayGuide();
        }
        else if (CurrStageData.StageId == 2 && !GameDataManager.Instance.UserData.ChessTutorialProgress[4])
        {
            ChessGuideSystem.Instance.currentTutorial = 4;
            ChessGuideSystem.Instance.activeToolObject = HitsBtn.gameObject;
            ChessGuideSystem.Instance.toolSourceName = "UseTips";
            ChessGuideSystem.Instance.DisplayGuide();
        }
        else if (CurrStageData.StageId == 6 && !GameDataManager.Instance.UserData.ChessTutorialProgress[5])
        {
            ChessGuideSystem.Instance.currentTutorial = 5;
            ChessGuideSystem.Instance.activeToolObject = CompleteBtn.gameObject;
            ChessGuideSystem.Instance.toolSourceName = "UseComplete";
            ChessGuideSystem.Instance.DisplayGuide();
        }

        // 👇 🌟 触发特殊玩法的新手引导
        if (ChessStageController.Instance.IsFirstEnterStage)
        {
            int stageId = CurrStageData.StageId;

            // 冰块新手引导
            bool hasIceInBoard = chessboardGrid.GridList.Values.Any(v => v.chesspiece != null && v.chesspiece.hasIce);
            if (hasIceInBoard && (!GameDataManager.Instance.UserData.ChessTutorialProgress.ContainsKey(6) ||
                                  !GameDataManager.Instance.UserData.ChessTutorialProgress[6]))
            {
                Debug.Log("🌟 [引导注入] 正式弹窗展示冰块新手引导");
                // 寻找棋盘上第一个被冰块冻住的格子，作为小手指向的目标
                var allIceTiles = chessboardGrid.GridList.Values.Where(v => v.chesspiece.hasIce).ToList();
                if (allIceTiles.Count <= 0) return;
                ChessGuideSystem.Instance.ChesspieceList = allIceTiles;
                ChessGuideSystem.Instance.activeToolObject = null;
                ChessGuideSystem.Instance.currentTutorial = 6;
                ChessGuideSystem.Instance.toolSourceName = "IceTutorial";
                ChessGuideSystem.Instance.DisplayGuide();
                return; // 强行拦截一维时间轴，一次只弹一个引导
            }

            // 花朵新手引导
            bool hasFlowerInBoard =
                chessboardGrid.GridList.Values.Any(v => v.chesspiece != null && v.chesspiece.hasFlower);
            if (hasFlowerInBoard && (!GameDataManager.Instance.UserData.ChessTutorialProgress.ContainsKey(7) ||
                                     !GameDataManager.Instance.UserData.ChessTutorialProgress[7]))
            {
                Debug.Log("🌟 [引导注入] 正式弹窗展示花朵新手引导");
                var allFlowerTiles = chessboardGrid.GridList.Values.Where(v => v.chesspiece.hasFlower).ToList();
                if (allFlowerTiles.Count <= 0) return;
                ChessGuideSystem.Instance.ChesspieceList = allFlowerTiles;
                ChessGuideSystem.Instance.activeToolObject = null;
                ChessGuideSystem.Instance.currentTutorial = 7;
                ChessGuideSystem.Instance.toolSourceName = "FlowerTutorial";
                ChessGuideSystem.Instance.DisplayGuide();
                return;
            }

            // 树叶新手引导
            // if (ChessStageController.Instance.CheckLeafMechanic(stageId, out bool isLeafFirst))
            bool isLeafGameplayActive = leafSlider != null && leafSlider.transform.parent.gameObject.activeInHierarchy;
           
            if (isLeafGameplayActive && (!GameDataManager.Instance.UserData.ChessTutorialProgress.ContainsKey(8) ||
                                         !GameDataManager.Instance.UserData.ChessTutorialProgress[8]))
            {
                Debug.Log("🌟 [引导注入] 正式开启树叶新机制两步引导：进入第一步");
                List<ChessView> allRelatedGroups = chessboardGrid.GetCurrentSelectGroup2();
                ChessGuideSystem.Instance.ChesspieceList = allRelatedGroups;
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                foreach (var tile in allRelatedGroups)
                {
                    sb.Append(tile.Answer); // 获取正确答案的字
                }

                ChessGuideSystem.Instance.targetPhrase = sb.ToString();
                // 3. 筛选该词组内所有未填写的格子，并在下方字盘中找出对应的待填字块
                // ChessGuideSystem.Instance.TargetPuzzle.Clear();
                foreach (var tile in allRelatedGroups)
                {
                    if (tile.CurrState is TileState.None or TileState.Check)
                    {
                        BowlView matchingBowl = puzzleTileTable.GridList.FirstOrDefault(b =>
                            b.letter == tile.chesspiece.letter &&
                            b.bowl.status == 0 &&
                            !ChessGuideSystem.Instance.TargetPuzzle.Contains(b));

                        if (matchingBowl != null && !ChessGuideSystem.Instance.TargetPuzzle.Contains(matchingBowl))
                        {
                            ChessGuideSystem.Instance.TargetPuzzle.Add(matchingBowl);
                        }
                    }
                }

                // ChessGuideSystem.Instance.collectionPointObject = leafSlider.transform.parent.gameObject;
                ChessGuideSystem.Instance.activeToolObject = ChessGuideSystem.Instance.TargetPuzzle.Count > 0
                    ? ChessGuideSystem.Instance.TargetPuzzle[0].gameObject
                    : null;
                ChessGuideSystem.Instance.activeToolObject = null;
                ChessGuideSystem.Instance.currentTutorial = 8;
                ChessGuideSystem.Instance.toolSourceName = "LeafTutorialStep1";
                ChessGuideSystem.Instance.DisplayGuide();
                return;
            }
        }
    }

    // 添加找到的成语
    public void AddFoundPuzzle(string puzzle, float timeSpent = 0f, bool isFromTool = false)
    {
        if (string.IsNullOrEmpty(puzzle) || UsedPuzzles.Contains(puzzle))
            return;

        float currentWordSeconds = _currentWordActiveSeconds;

        ChessStageController.Instance.AddFoundPuzzle(puzzle, currentWordSeconds);
        RecordPuzzleAnalytics(puzzle, currentWordSeconds);
        ComboErrorCount = 0;
        usetoolCount = 0;
        // --- 新增：玩家答对，重置卡关倒计时 ---
        // 注意：不重置 _hasTriggeredHintReminderThisLevel，保证每关只出一次特效
        _isStuckTimerRunning = false;
        _stuckTimer = 0f;

        UsedPuzzles.Add(puzzle);
        UpdateLevelData(puzzle); // 打开词典按钮时填入

        // 👇 ================== 新增：UI 进度条刷新调用点 ==================
        // 1. 获取当前进度的比例值 (0.0 ~ 1.0 之间)
        float progressRatio = ChessStageController.Instance.GetScoreProgressRatio();

        // 2. 获取具体的分数（如果你的UI需要显示 "120/500" 这种格式）
        int curScore = ChessStageController.Instance.CurrentTotalScore;
        int maxScore = ChessStageController.Instance.OptimalTotalScore;

        // TODO: 在这里控制你的 UI 进度条
        // 比如: MyProgressBar.fillAmount = progressRatio;
        // 比如: MyScoreText.text = $"{curScore} / {maxScore}";
        Debug.Log(
            $"当前连击: {ChessStageController.Instance.PuzzleComboCount}, 获得总分: {curScore}, 理论最高分: {maxScore}, 进度比例: {progressRatio * 100}%");

        // =================================================================
        CheckAndTriggerPraise(puzzle);
    }

    // 有错误调用的
    public void AddWordError(ChessView tile, int tileErrorCount)
    {
        wordErrorCount++;
        ComboErrorCount++;

        if (!_hasTriggeredHintReminderThisLevel)
        {
            if (ComboErrorCount == 3)
            {
                // 刚达到连续3次错误，开始暗中倒计时
                _isStuckTimerRunning = true;
                _stuckTimer = 0f;
            }
            else if (ComboErrorCount > 3 && _isStuckTimerRunning && _stuckTimer >= PropRemindTime)
            {
                // 时间达到了配置表的 X 秒，且玩家再次答错 -> 触发提醒
                _hasTriggeredHintReminderThisLevel = true; // 锁定，本关不再触发
                _isStuckTimerRunning = false;
                StartCoroutine(PlayHintReminderEffectDelay());
            }
        }

        // 👇 🌟 核心修改点：记录扣分前后的差值
        int oldScore = ChessStageController.Instance.CurrentTotalScore;

        bool wasPunished = ChessStageController.Instance.OnUpdateRewardPuzzle(false, tileErrorCount);
        if (wasPunished)
        {
            // 获取实际扣除的负数分数 (例如 -5)
            int scoreDiff = ChessStageController.Instance.CurrentTotalScore - oldScore;

            // 触发棋盘上的减分飘字
            if (scoreDiff < 0)
            {
                ShowBoardDeductionFloatingScore(tile.transform, scoreDiff);
            }

            // 🌟 既然已经扣过分、飞过粒子了，这波错误就“翻篇”了，计数重置！
            chessboardGrid.TileErrorCounts[tile] = 0;
        }

        if (_comboScreenFX != null)
        {
            _comboScreenFX.SetActive(false);
        }

        if (chessboardGrid != null)
        {
            chessboardGrid.hasPlayedComboSoundThisChain = false;
        }
        
        // 👇 新增：精准记录特定词组的试错次数
        string targetPhrase = GetCurrentSelectedPhrase(); // 获取玩家正在填的词
    
        if (chessboardGrid.currentFailingPhrase == targetPhrase)
        {
            // 还是这个词，继续累加
            chessboardGrid.currentPhraseConsecutiveErrors++;
        }
        else
        {
            // 玩家换目标了，或者第一次答错，重置并记录新词
            chessboardGrid.currentFailingPhrase = targetPhrase;
            chessboardGrid.currentPhraseConsecutiveErrors = 1;
        }
    }

    private void UpdateLevelData(string puzzle)
    {
        ChessStageController.Instance.PuzzleData.CurPuzzle = puzzle;
        if (!GameDataManager.Instance.UserData.GetWordVocabulary().LevelWords.Contains(puzzle))
        {
            GameDataManager.Instance.UserData.AddStagePuzzle(puzzle);
        }

        int wordIndex = GameDataManager.Instance.UserData.GetWordVocabulary().LevelWords.IndexOf(puzzle);
        ChessStageController.Instance.PuzzleData.IsVocabularyPuzzle = true;
        ChessStageController.Instance.IsEnterVocabulary = true;
        ChessStageController.Instance.IsEnterPuzzle = true;
        ChessStageController.Instance.PuzzleData.PageIndex = wordIndex + 1;
    }

    // 发送数据封装
    private void RecordPuzzleAnalytics(string puzzle, float currentWordSeconds)
    {
        int puzzleId = CurrStageData.FoundTargetPuzzles.Count;
        float energy = GameDataManager.Instance.ChessDynamicHardSave.EnergyValue;

        wordUserSeconds += currentWordSeconds;

        AnalyticMgr.LevelProgress(puzzleId, puzzle, wordUserSeconds,
            ComboErrorCount, ChessStageController.Instance.PuzzleComboCount, usetoolCount, energy);

        wordUserSeconds = 0;
        _currentWordActiveSeconds = 0f; // 🌟 上报完后，直接把秒表清零，准备找下一个字！
    }

    #region 🌟 过关与特效核心流水线重构

    /// <summary>
    /// 游戏结束
    /// </summary>
    public void GamePlayOver(bool isJump = false)
    {
        _isTimerRunning = false; // 通关了，立刻停止倒计时！
        // 通关瞬间，立刻强制关闭连击特效！
        if (_comboScreenFX != null)
        {
            _comboScreenFX.SetActive(false);
        }

        EventDispatcher.instance.TriggerChangeTopRaycast(false);
        SetToolButtonsEnabled(false);
   
        StartCoroutine(HandleStageCompletion(isJump));
        if (!isJump) ShowGoldLeafAnim();
    }

    private void ShowGoldLeafAnim()
    {
        foreach (var chessView in ChessStageController.Instance.GoldLeafChessViews)
        {
            chessView._bg.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("goldLeaf");
            chessView.FlyToThemeBtn(MyThemeBtn.gameObject, this.transform, null);
        }

        chessboardGrid.ClearAllGoldLeafOnBowls();
    }

    /// <summary>
    /// 处理关卡完成
    /// </summary>
    private IEnumerator HandleStageCompletion(bool isJump)
    {
        wordUserSeconds = 0;
        // puzzleTileTable.transform.parent.gameObject.SetActive(false);
        ChessStageController.Instance.FinalizeStageData(isJump);
        yield return null;
        if (!isJump)
        {
            // 👇 🌟 新增：如果是树叶关卡，先播放奖励结算飞行动画，等飞完再继续
            if (CurrStageData.CollectedLeaves > 0)
            {
                yield return StartCoroutine(PlayLeafRewardsFlyOutFlow());
            }

            bool isBannerFinished = false;
            // 4选1动态加载横幅预制体，内部带有 2.5 秒的展示时常生命周期
            StartCoroutine(PlayZenToCenterBannerFlow(() => isBannerFinished = true));
            // 耐心等待横幅播完或玩家点击关闭
            yield return new WaitUntil(() => isBannerFinished);
        }
        else
        {
            // 🌟【时序安全修复】：如果确认为跳关，无缝闪过横幅展示期，不进行任何动态预制体实例化
            yield return null;
        }
        
        ChessStageController.Instance.CompleteStage(CurrStageInfo.StageNumber, wordErrorCount, isJump);
    }

    #endregion

    /// <summary>
    /// 供【暂停弹窗的确认退出】或【失败弹窗的放弃复活】调用
    /// 玩家彻底不玩了，正式扣除体力并退回主界面！
    /// </summary>
    public void QuitGameAndDeductEnergy()
    {
        _isTimerRunning = false;

        // 🌟 核心：在此刻正式扣除体力！
        GameDataManager.Instance.UserData.ConsumeEnergy(CurrStageInfo.StageNumber, 1, "退出关卡消耗");
        Debug.Log("玩家彻底退出关卡，扣除 1 点体力！");
        ChessStageController.Instance.ClearCurrentLevelSave();

        // TODO: 关掉游戏界面，回到主大厅
        SystemManager.Instance.HidePanel(PanelType.ChessPlayArea);
        SystemManager.Instance.ShowPanel(PanelType.PrimaryInterface);
    }

    /// <summary>
    /// 获取当前词组的专属连续错误次数，并在获取后清零
    /// </summary>
    public int PopCurrentPhraseErrorCount(string correctPhrase)
    {
        // 如果玩家刚刚填对的词，正是他之前一直死磕答错的词
        if (chessboardGrid.currentFailingPhrase == correctPhrase)
        {
            int errCount = chessboardGrid.currentPhraseConsecutiveErrors;
        
            // 答对了，记忆清零，避免干扰下一个词
            chessboardGrid.currentFailingPhrase = "";
            chessboardGrid.currentPhraseConsecutiveErrors = 0;
        
            return errCount;
        }
    
        return 0; // 一次性答对，或者之前错的是别的词
    }
    
    /// <summary>
    /// 填词成功鼓励横幅
    /// 在 ChessPlayArea.cs 里的某个合适位置 (比如 AddFoundPuzzle 尾部)
    /// </summary>
    public void CheckAndTriggerPraise(string completedPuzzle)
    {
        // 1. 组装上下文数据 (你需要从当前棋盘或控制器中获取这些真实数据)
        PraiseContext context = new PraiseContext
        {
            IsFirstWord = ChessStageController.Instance.CurrStageData.FoundTargetPuzzles.Count == 1,
            CurrentCombo = ChessStageController.Instance.PuzzleComboCount,
            WordsRemaining = ChessStageController.Instance.CurrStageInfo.PhraseGroups.Count -
                             ChessStageController.Instance.CurrStageData.FoundTargetPuzzles.Count,
            
            // InitialLettersCount = chessboardGrid.GetCurrentPhraseInitialCount(),
            InitialLettersCount = ChessStageController.Instance.GetPhraseInitialCountByWord(completedPuzzle),
            
            ErrorsOnThisWord = PopCurrentPhraseErrorCount(completedPuzzle),
            TotalErrorsInLevel = wordErrorCount
        };
        ChessStageController.Instance.EnqueuePraiseCheck(context);
        // // 2. 请求仲裁
        // PraiseConfig winner = ChessStageController.Instance.EvaluatePraiseFeedback(context);
        //
        // // 3. 播放表现
        // if (winner != null)
        // {
        //     Debug.Log($"[正反馈触发] 抽中反馈ID: {winner.FeedbackID}, 准备播放样式: {winner.BannerStyle}");

            // 随机抽取一句文案
            // string finalPhraseKey = "";
            // if (winner.TextLoop != null && winner.TextLoop.Length > 0)
            // {
            //     finalPhraseKey = winner.TextLoop[UnityEngine.Random.Range(0, winner.TextLoop.Length)];
            // }
            // ShowPraiseUI(winner);
            // 呼叫 UI 播放横幅和文字 (这里对接你具体的 UI 特效逻辑)
            // PlayPraiseUI(winner.BannerStyle, finalPhraseKey, targetTile.transform.position);
        // }
    }
}