using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Middleware;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

public class ChessPlayArea : UIWindow
{
    [SerializeField] private GameObject GameBase;
    [SerializeField] private Text Stagetxt;
    [SerializeField] public Button HitsBtn;      // 提示按钮
    [SerializeField] public Button CompleteBtn;     // 完成按钮
    [SerializeField] private Button PuzzleBtn;   //  关内词语按钮
    [SerializeField] public GameObject butterflyPrefab; // 蝴蝶特效
    [SerializeField] public GameObject butterflyObj;   // 蝴蝶节点
    
    [Header("词语面板")]
    // 字块矩阵面板
    [SerializeField] public ChessboardGrid chessboardGrid;
    [HideInInspector]public (float row, float col) startLocation = (0, 0);
    // 待填入字块集面板
    [SerializeField] public ChessBowlGrid puzzleTileTable;

    private int usetoolCount;     // 所有道具使用
    private int ComboErrorCount;  // 连续错误计数
    private int wordErrorCount;   // 总错误计数
    
    // 蝴蝶道具设置
    List<GameObject> EffectButterFlays = new List<GameObject>();
    List<ChessView> butterChess = new List<ChessView>();
    private int useButterflyCount;
    private bool firstenter;

    private GameObject _bottomLine;
    private GameObject _stageOverObj;
    #region 数据相关
    private DateTime StartTime;
    private HashSet<string> UsedPuzzles = new HashSet<string>(); //找出的词组
    #endregion
    // 当前关卡配置数据
    private ChessStageInfo CurrStageInfo
    {
        get => ChessStageController.Instance.CurrStageInfo;
    }
    private ChessStageProgressData CurrStageData
    {
        get => ChessStageController.Instance.CurrStageData;
    }

    [HideInInspector]
    [Tooltip("当前使用的教学工具对象")]
    private GameObject activeObject; // 当前操作对象
    private string sourceName;   // 操作来源名称
    private bool IsClickAuto;    // 是否在教学关点击的自动完成
    
    #region 生命周期

    protected override void InitializeUIComponents()
    {
        HitsBtn.AddClickAction(UseTips, "");
        CompleteBtn.AddClickAction(() => UseComplete(), "");
        // PuzzleBtn.AddClickAction(ClickLevelPuzzle);
        BoardInitialize();
        PuzzleBtn.gameObject.SetActive(false);
    }

    

    /// <summary>
    /// 棋盘初始化
    /// </summary>
    private void BoardInitialize()
    {
        if(_stageOverObj == null)
        {
            var go = AdvancedBundleLoader.SharedInstance.LoadGameObject(ToolUtil.GetLanguageBundle(), "item_finishEffect");
            _stageOverObj = Instantiate(go, transform); 
            _stageOverObj.transform.SetAsLastSibling();
            _stageOverObj.SetActive(false);
        }
        chessboardGrid.Initialize(this);
        puzzleTileTable.Initialize(this);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        UpdateUI();

        EventDispatcher.instance.OnCheckShowChessTutorial += CheckShowChessTutorialEvent;

        StartCoroutine(SetupGameData());
        AudioManager.Instance.PlaySoundEffect("EnterLevel");

        StartTime = DateTime.Now;
        
        EnhancedVideoController.Instance.TogglePause();
        // bool hasLevelWords = ChessStageController.Instance.CurrStageData.FoundTargetPuzzles.Count > 0;
        // if (hasLevelWords)
        // {
        //     PuzzleBtn.gameObject.SetActive(true);
        // }
        // else
        // {
        //     PuzzleBtn.gameObject.SetActive(false);
        //     PuzzleBtn.GetComponent<CanvasGroup>().alpha = 0f;
        // }
        ToolInfo toolInfo = GameDataManager.Instance.UserData.toolInfo[103];
        if (GameDataManager.Instance.UserData.butterflyTaskIsOpen)
        {
            useButterflyCount = AppGameSettings.MaxButterfliesPerLevel;
        }
        else
        {
            useButterflyCount = toolInfo.count >= 2 ? AppGameSettings.MaxButterfliesPerLevel : toolInfo.count;
        }
        butterChess.Clear();
    }

    #endregion

    #region UI操作
    /// <summary>
    /// 更新游戏区域的UI
    /// </summary>
    private void UpdateUI()
    {
        Stagetxt.text = MultilingualManager.Instance.GetString("Level")+ " " + CurrStageInfo.StageNumber;
        HitsBtn.gameObject.SetActive(CurrStageData.StageId >= 2);
        CompleteBtn.gameObject.SetActive(CurrStageData.StageId >= 5);
        InitToolUI();
    }

    /// <summary>
    /// 更新道具按钮
    /// </summary>
    /// <param name="value"></param>
    /// <param name="isfirst"></param>
    private void InitToolUI(int value =0, bool isfirst = false)
    {
        Transform CompCost = CompleteBtn.transform.GetChild(0);
        Transform CompCount = CompleteBtn.transform.GetChild(1);
        if (GameDataManager.Instance.UserData.toolInfo[104].count > 0)
        {
            CompCount.GetComponentInChildren<Text>().text = GameDataManager.Instance.UserData.toolInfo[104].count.ToString();
            CompCount.gameObject.SetActive(true);
            CompCost.gameObject.SetActive(false);
        }
        else
        {
            CompCost.GetComponentInChildren<Text>().text = GameDataManager.Instance.UserData.toolInfo[104].cost.ToString();
            CompCost.gameObject.SetActive(true);
            CompCount.gameObject.SetActive(false);
        }

        Transform HintCost = HitsBtn.transform.GetChild(0);
        Transform HintCount = HitsBtn.transform.GetChild(1);
        if (GameDataManager.Instance.UserData.toolInfo[102].count > 0)
        {
            HintCount.GetComponentInChildren<Text>().text = GameDataManager.Instance.UserData.toolInfo[102].count.ToString();
            HintCount.gameObject.SetActive(true);
            HintCost.gameObject.SetActive(false);
        }
        else
        {
            HintCost.GetComponentInChildren<Text>().text = GameDataManager.Instance.UserData.toolInfo[102].cost.ToString();
            HintCost.gameObject.SetActive(true);
            HintCount.gameObject.SetActive(false);
        }
    }
    #endregion


    private IEnumerator SetupGameData()
    {
        if (SystemManager.Instance.PanelIsShowing(PanelType.ChessFinishView))
        {
            SystemManager.Instance.HidePanel(PanelType.ChessFinishView);
        }
        //清理一下棋盘
        chessboardGrid.Clear();
        puzzleTileTable.Clear();
        IsClickAuto = false;
        float spacing = 4f;
        // 设置尺寸
        // int maxRow = CurrStageData.MaxRow;
        // int maxCol = CurrStageData.MaxCol;
        // int minRow = CurrStageData.MinRow;
        // int minCol = CurrStageData.MinCol;
        int rowCount = CurrStageData.MaxRow - CurrStageData.MinRow + 1;
        int colCount = CurrStageData.MaxCol - CurrStageData.MinCol + 1;
        float boardWidth = 1242; // 棋盘可用宽度
        float boardHeight = chessboardGrid.GetComponent<RectTransform>().rect.height; // 棋盘可用高度

        float widthTotalSpacing = (rowCount - 1) * spacing;
        float heightTotalSpacing = (colCount - 1) * spacing;

        float usableWidth = boardWidth - spacing * 2 - widthTotalSpacing;
        float usableHeight = boardHeight - spacing * 2 - heightTotalSpacing;
        
        float singleWidth = Mathf.Min(usableWidth / rowCount, 161f);
        float singleHeight = Mathf.Min(usableHeight / colCount, 161f);
        
        float usableSize = Mathf.Min(singleWidth, singleHeight);
        
        float leftMargin = (boardWidth - (usableSize * rowCount + widthTotalSpacing) ) / 2f +2;
        float bottomMargin = (boardHeight - (usableSize * colCount + heightTotalSpacing) ) / 2f ;

        // Debug.Log($"棋盘宽{boardWidth} 高{boardHeight} 内最大row {maxRow} 最小row {minRow}, 最大col {maxCol} 最小col {minCol}, 相差row {rowCount} 相差col {colCount}");
        // Debug.Log($"左边距{leftMargin} 底边距{bottomMargin} 每格尺寸: {usableSize-1} × {usableSize-2} 像素");
        ChessStageController.Instance.CurrStageData.ActiveSize = new Vector2(usableSize -1 , usableSize - 2);
        startLocation = (leftMargin, bottomMargin);
        
        // (int row, int col) GridSize = ChessStageController.Instance.CurrStageData.GridSize;
        //
        // Vector2 cellSize = GridSize switch
        // {
        //     (8,8) => new Vector2(142, 141),   // ≥142 → 8档
        //     (7,7) => new Vector2(162, 161),   // ≥162 → 7档
        //     (9,8) => new Vector2(126, 125),   // ≥126 → 9档
        //     _ => new Vector2(126, 125)  // 更小 → 9档兜底
        // };
        //// 设置待填字
        GridLayoutGroup grid = puzzleTileTable.GetComponent<GridLayoutGroup>();

        if (Mathf.Ceil(ChessStageController.Instance.CurrStageData.Puzzles.Count / 4) <= 4)
        {
            grid.constraintCount = 6;
            grid.cellSize = new Vector2(198, 196);
        }
        else
        {
            int gridSize = ChessStageController.Instance.CurrStageData.Puzzles.Count > 28 ? 8 : 7;
            // 每列数量 8 149*147  是7 则设置 170*168
            grid.constraintCount = gridSize;  
            grid.cellSize = gridSize == 8 ? new Vector2(149, 147) : new Vector2(170, 168);  // 7档
        }
       
        yield return new WaitForSeconds(0.1f);
        puzzleTileTable.transform.parent.gameObject.SetActive(true);
        yield return SetupGame();
        // 填入最后一个单词
        var puzzles = CurrStageData.FoundTargetPuzzles;
        if (puzzles != null && puzzles.Count > 0)
        {
            string word = puzzles[^1]; // 或 puzzles[0]
            UpdateLevelData(word);
        }
        
        //在第7关且词语少于9个的时候可以显示横幅广告
        // 获取广告 RectTransform 组件
        if (CurrStageInfo.StageNumber >= 11)
        {
            Game.Ads?.ShowBanner();
            // 设置偏移值
            // rectTransform.offsetMin = new Vector2(0, 0); // Left 和 Bottom
        }
        else
        {
            Game.Ads?.HideBanner();
        }
        yield return new WaitForSeconds(0.4f);
        
        if (ChessStageController.Instance.IsFirstEnterStage)
        {
            if (!new[] { 1, }.Contains(CurrStageData.StageId))
            {
                for (int i = 0; i < useButterflyCount; i++)
                {
                    GameObject effectButt = Instantiate(butterflyPrefab, butterflyObj.transform.parent);
                    EffectButterFlays.Add(effectButt);
                }
                
                ToolInfo toolInfo =  GameDataManager.Instance.UserData.toolInfo[103];
                if (toolInfo.count > 0)
                {
                    GameBase.GetComponent<CanvasGroup>().blocksRaycasts = false;
                    EventDispatcher.instance.TriggerChangeTopRaycast(false);
                    yield return new WaitForSeconds(0.2f);
                    UseButterfly();
                }
            }
            
            ComboErrorCount = 0;
            wordErrorCount = 0;
            usetoolCount = 0;
        }
        
    }

    public IEnumerator SetupGame()
    {
        chessboardGrid.CreateChess();
        puzzleTileTable.CreatePuzzle();
        yield return new WaitUntil(()=> puzzleTileTable.GridList.Count > 0);
        RectTransform chessRectTransform = chessboardGrid.GetComponent<RectTransform>();
        RectTransform btnParent = HitsBtn.transform.parent.GetComponentInParent<RectTransform>();
        RectTransform bowlRectTransform = puzzleTileTable.GetComponent<RectTransform>();
        if (UIUtilities.IsiPad())
        {
            VerticalLayoutGroup vlg = chessboardGrid.transform.parent.GetComponent<VerticalLayoutGroup>();
            // 只取消宽度控制，保留高度控制
            vlg.childControlWidth = false;
            vlg.childForceExpandWidth = false;
            chessRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, UIUtilities.REFERENCE_WIDTH); // 1242px
            btnParent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, UIUtilities.REFERENCE_WIDTH); // 1242px
            bowlRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, UIUtilities.REFERENCE_WIDTH + 2); // 1244px
        }
        else
        {
            float scale = UIUtilities.GetScreenRatio();
            if (scale < 0.85f)
            {
                chessRectTransform.localScale = new Vector3(scale + 0.08f, scale + 0.08f, scale + 0.08f);
                bowlRectTransform.localScale = new Vector3(scale + 0.06f, scale + 0.06f, scale + 0.06f);
                btnParent.localScale = new Vector3(scale,scale,scale);
            }
            else if(scale > 1f)
            {
                VerticalLayoutGroup vlg = chessboardGrid.transform.parent.GetComponent<VerticalLayoutGroup>();
                // 只取消宽度控制，保留高度控制
                vlg.childControlWidth = false;
                vlg.childForceExpandWidth = false;
                chessRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, UIUtilities.REFERENCE_WIDTH); // 1242px
                btnParent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, UIUtilities.REFERENCE_WIDTH); // 1242px
                bowlRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, UIUtilities.REFERENCE_WIDTH + 2); // 1244px
            }
        }

        // if (_bottomLine == null)
        // {
        //     RectTransform target = bowlRectTransform;
        //     Vector3[] corners = new Vector3[4];
        //     target.GetWorldCorners(corners);
        //     Vector2 topLeftScreen = RectTransformUtility.WorldToScreenPoint(null, corners[1]);
        //     Debug.Log("屏幕top " + topLeftScreen);
        //     
        //     GameObject linePre = AdvancedBundleLoader.SharedInstance.LoadGameObject("rootcanvas", "BottomLine");
        //     _bottomLine = Instantiate(linePre, target.parent.parent);
        //     
        //     RectTransform parentPt = target.parent as RectTransform;
        //     RectTransformUtility.ScreenPointToLocalPointInRectangle(parentPt, topLeftScreen, null, out Vector2 localPos);
        //
        //     Debug.Log("输出的本地位置" + localPos);
        //     RectTransform rt = _bottomLine.GetComponent<RectTransform>();
        //     // 1. 上下贴边（从目标顶部 → 底部）
        //     rt.anchorMin = new Vector2(0, 1);   // 左右贴边
        //     rt.anchorMax = new Vector2(1, 0);
        //     rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, target.rect.height);
        //     rt.anchoredPosition = new Vector2(0, localPos.y);
        // }
        // 触发新手引导检查
        EventDispatcher.instance.TriggerCheckShowChessTutorial();
        
    }
    /// <summary>
    /// 处理游戏内操作回调
    /// </summary>
    /// <param name="game">点击的物体</param>
    /// <param name="source">操作名称</param>
    public void HandleGamePlayCall(GameObject game, string source)
    {
        // Debug.LogWarning("进来了新手引导检查：" + source);
        if (GameDataManager.Instance.UserData.ChessTutorialProgress.Values.All(v=>v))
            return;

        // Debug.LogWarning("进来了新手引导检查2：" + game.name);
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
        StartCoroutine(CheckShowChessTutorial());
    }
    private IEnumerator CheckShowChessTutorial()
    {
        yield return new WaitForSeconds(0.1f);

        if(CurrStageData.StageId == 1 && ChessStageController.Instance.IsFirstEnterStage)
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
        }else 
        if (CurrStageData.StageId == 2 && ChessStageController.Instance.IsFirstEnterStage)
        {
            ChessGuideSystem.Instance.currentTutorial = 4;
            ChessGuideSystem.Instance.activeToolObject = HitsBtn.gameObject;
            ChessGuideSystem.Instance.toolSourceName = "UseTips";
            ChessGuideSystem.Instance.DisplayGuide();
        }else 
        if (CurrStageData.StageId == 5 && ChessStageController.Instance.IsFirstEnterStage)
        {
            ChessGuideSystem.Instance.currentTutorial = 5;
            ChessGuideSystem.Instance.activeToolObject = CompleteBtn.gameObject;
            ChessGuideSystem.Instance.toolSourceName = "UseComplete";
            ChessGuideSystem.Instance.DisplayGuide();
        }
    }

    // 添加找到的成语
    public void AddFoundPuzzle(string puzzle)
    {
        // CurrStageData.FoundTargetPuzzles ??= new List<string>();
        // Debug.LogWarning("传递的词：" + puzzle);
        // CurrStageData.FoundTargetPuzzles.Add(puzzle);
        // GameDataManager.Instance.UserData.AddStagePuzzle(puzzle);
        if (UsedPuzzles.Contains(puzzle))   // 已出现过 → 跳过
            return;
        
        ChessStageController.Instance.AddFoundPuzzle(puzzle);
        RecordPuzzleAnalytics(puzzle);
        ComboErrorCount = 0;
        usetoolCount = 0;
        UsedPuzzles.Add(puzzle);
        UpdateLevelData(puzzle); // 打开词典按钮时填入
        // if (!PuzzleBtn.gameObject.activeSelf)
        // {
        //     PuzzleBtn.gameObject.SetActive(true);
        //     PuzzleBtn.GetComponent<CanvasGroup>().DOFade(1f,0.2f);
        // }
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
    private void RecordPuzzleAnalytics(string puzzle)
    {
        int puzzleId = CurrStageData.FoundTargetPuzzles.Count;
        TimeSpan timeSpan = DateTime.Now.Subtract(StartTime);
        float secondsValue = (float)Math.Round(timeSpan.TotalSeconds, 1);
        
        AnalyticMgr.LevelProgress(puzzleId, puzzle, secondsValue,
            wordErrorCount, -1, usetoolCount);
        
        StartTime = DateTime.Now;
    }

    /// <summary>
    /// 游戏结束
    /// </summary>
    public void GamePlayOver()
    {
         StartCoroutine(HandleStageCompletion());
    }

    /// <summary>
    /// 处理关卡完成
    /// </summary>
    private IEnumerator HandleStageCompletion()
    {
        Debug.Log("关卡完成时当前进度：" + JsonConvert.SerializeObject(GameDataManager.Instance.UserData.ChessTutorialProgress));
        puzzleTileTable.transform.parent.gameObject.SetActive(false);
        yield return new WaitForSeconds(0.1f);
        _stageOverObj.SetActive(true);
        ChessStageController.Instance.CompleteStage(CurrStageInfo.StageNumber, wordErrorCount);
        EventDispatcher.instance.TriggerChangeTopRaycast(false);
    }

    // 暂未在游戏中打开词库
    private void ClickLevelPuzzle()
    {
        ChessStageController.Instance.IsEnterVocabulary = true;
        SystemManager.Instance.ShowPanel(PanelType.LevelWordScreen);
    }
    public void UseComplete(bool isReset = false)
    {
        ToolInfo toolInfo = GameDataManager.Instance.UserData.toolInfo[104];

        if(toolInfo == null || chessboardGrid.GameOver)
        {
            Debug.LogError("[GameManager] There is no hint with the given hint id: ");
            CompleteBtn.enabled = true;
            return;
        }

        bool useCoins = false;

        if(toolInfo.count <= 0)
        {
            if (CanUseTool(toolInfo))
            {
                useCoins = true;
            }
            else
            {
                MessageSystem.Instance.ShowTip("TipGoldInsufficient", false);
                SystemManager.Instance.ShowPanel(PanelType.RewardAdsScreen);
                return;
            }
        }

        if (CurrStageInfo.StageNumber == 5)
        {
     
            if (GameDataManager.Instance.UserData.ChessTutorialProgress[5])
            {
                usetoolCount++;
            }
            else
            {
                IsClickAuto = true;
            }
        }
        else
        {
            usetoolCount++;
        }

        if (useCoins)
        {
            // 更新道具
            GameDataManager.Instance.UserData.UpdateGold(-toolInfo.cost, false, true, "购买道具");
            GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.AutoComplete, 1, "购买道具");
            GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.AutoComplete, -1, "关卡内使用");
        }
        else
        {
            GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.AutoComplete, -1, "关卡内使用");
            InitToolUI();
        }

        AudioManager.Instance.PlaySoundEffect("ItemUSe02");
        // 实现业务
        StartCoroutine(chessboardGrid.CompletedPhrase());

        // 触发新手引导检查
        HandleGamePlayCall(CompleteBtn.gameObject, "UseComplete");
    }
    /// <summary>
    /// 使用提示工具
    /// </summary>
    public void UseTips()
    {
        ToolInfo toolInfo = GameDataManager.Instance.UserData.toolInfo[102];

        if (toolInfo == null || chessboardGrid.GameOver)
        {
            Debug.LogError("[GameManager] There is no hint with the given hint id: ");
            return;
        }

        if (chessboardGrid.IsSelectTip()) return;
        
        bool useCoins = false;
        if(toolInfo.count <= 0)
        {
            if (CanUseTool(toolInfo))
            {
                useCoins = true;
            }
            else
            {
                MessageSystem.Instance.ShowTip("TipGoldInsufficient", false);
                SystemManager.Instance.ShowPanel(PanelType.RewardAdsScreen);
                return;
            }
        }

        // 第二关新手引导 不计数
        if (CurrStageInfo.StageNumber == 2)
        {
            if (GameDataManager.Instance.UserData.ChessTutorialProgress[4])
            {
                usetoolCount++;
                ChessStageController.Instance.UseTipToolCount++;
            }
        }
        else
        {
            usetoolCount++;
            ChessStageController.Instance.UseTipToolCount++;
        }
        
        if (useCoins)
        {
            // 更新道具
            GameDataManager.Instance.UserData.UpdateGold(-toolInfo.cost, false, true, "购买道具");
            GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, 1, "道具购买");
            GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, -1, "关卡内使用");
        }
        else
        {
            GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, -1, "关卡内使用");
            InitToolUI();
        }
        // TODO
        chessboardGrid.SetSelectTip();
        AudioManager.Instance.PlaySoundEffect("ItemUSe01");
        DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedUseTipsTool,1);
        // 触发新手引导检查
        if (CurrStageInfo.StageNumber == 2)
        {
            BowlView hitBowl = puzzleTileTable.GridList
                .FirstOrDefault(bowl => bowl.letter == chessboardGrid.selecteTile.chesspiece.letter);
            HandleGamePlayCall(hitBowl!.gameObject, "UseTips");
        }
     
    }

    private bool CanUseTool(ToolInfo toolInfo)
    {
        if(toolInfo.cost <= GameDataManager.Instance.UserData.Gold)
        {
            return true;
        }
        return false;
    }

    private void UseButterfly()
    {
        ToolInfo toolInfo = GameDataManager.Instance.UserData.toolInfo[103];
        
        if (toolInfo == null || toolInfo.count <= 0)
        {
            Debug.LogError("蝴蝶道具数据为空！");
            // crossPuzzleGrid.SetPuzzleBoardState(true);
            butterflyObj.GetComponent<RectTransform>().DOAnchorPosX(-300, 0.3f);
            return;
        }
        
        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Butterfly, -1,"关卡内使用");
        DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedUseButterflyTool,1);
        useButterflyCount--;
        
        GameObject Effect_Butterfly = EffectButterFlays[useButterflyCount];
        butterflyObj.GetComponentInChildren<Text>().text = (useButterflyCount+1).ToString();
        Effect_Butterfly.gameObject.SetActive(false);
        
        if(useButterflyCount==0)
            AudioManager.Instance.PlaySoundEffect("showButterfly");
        
        // ChessView  selectNext  蝴蝶搜索的位置
        // 播放起飞
        butterflyObj.GetComponent<RectTransform>().DOAnchorPosX(0,0.3f).OnComplete(() =>
        {
            ChessView selectView = chessboardGrid.GetRandomNoneNonTipChess();
            butterChess.Add(selectView);
            Vector3[] MovePoints = GetButterflyPath(butterflyObj.transform,selectView.transform.position + new Vector3(3f, 0,0));
       
            Effect_Butterfly.transform.DOScale(new Vector3(50, 50, 50), 0.1f).OnComplete(() =>
            {
                Effect_Butterfly.transform.DOLocalRotate(Vector3.zero,0f);
                Effect_Butterfly.gameObject.SetActive(true);
                butterflyObj.GetComponentInChildren<Text>().text = useButterflyCount.ToString();
                
                Effect_Butterfly.transform.DOScale(new Vector3(50, 50, 50), 0.45f).OnComplete(() =>
                {
                    selectView.chesspiece.tip = true;
           
                    if(useButterflyCount>0)
                        UseButterfly();
                    else
                        butterflyObj.GetComponent<RectTransform>().DOAnchorPosX(-300, 0.3f);
                });                   
            });
            
            Effect_Butterfly.transform.DOPath(MovePoints, 1.2f).SetEase(Ease.Linear).OnComplete(() =>
            {
                Effect_Butterfly.transform.DOLocalRotate(new Vector3(0f, 150f, 20f), 0f);
                Effect_Butterfly.transform.DOScale(new Vector3(40, 40, 40),0.1f);
                
                Vector3 endWorld = selectView.TileTransform.TransformPoint(selectView.TileTransform.rect.center);
                Vector3 endLocal = Effect_Butterfly.transform.parent.InverseTransformPoint(endWorld);

                Effect_Butterfly.transform.DOLocalMove(endLocal, 0.85f).SetEase(Ease.Linear).OnComplete(
                () => {
                    selectView.SetTipMessage();
                    Effect_Butterfly.transform.DOScale(new Vector3(40, 40, 40),0.4f).OnComplete(() =>
                    {
                        Effect_Butterfly.transform.DOLocalMoveY(1480, 0.7f);
                        Effect_Butterfly.transform.DOLocalMoveX( - 300,0.7f).SetEase(Ease.Linear).OnComplete(() =>
                        {
                            Effect_Butterfly.transform.localPosition = new Vector3(-300f,0f,0f);
                            Effect_Butterfly.gameObject.SetActive(false);
                            Effect_Butterfly.transform.DOLocalRotate(Vector3.zero,0f);

                            if (useButterflyCount < 1)
                            {
                                GameBase.GetComponent<CanvasGroup>().blocksRaycasts = true;
                                EventDispatcher.instance.TriggerChangeTopRaycast(true);
                            }
                        });
                        
                        // crossPuzzleGrid.SetPuzzleBoardState(true);
                    });
                });
                
                // Effect_Butterfly.transform.DOLocalRotate(new Vector3(0, 150f, 45f), 0.55f).OnComplete(() =>
                // {
                //
                // });
            });
            
        });
    }
    private Vector3[] GetButterflyPath(Transform starttrans, Vector3 endPos)
    {
        Vector3 butterflyEndPos = endPos;
        var midPos = (butterflyEndPos + starttrans.position) / 1.5f;
        var bezierMidPos = (midPos + starttrans.position) / 2; // + Vector3.right * 8;
        Vector3[] movePoints = CustomFlyInManager.Instance.CreatTwoBezierCurve(starttrans.position,butterflyEndPos,bezierMidPos).ToArray();
        return movePoints;
    }
    /// <summary>
    /// 蝴蝶字自动完成并给金币
    /// </summary>
    /// <param name="word"></param>
    public void ButterWordAddIcon(ChessView  word)
    {
       ChessView view = butterChess.Find(ch=>ch == word);
       if (view == null) return;
       butterChess.Remove(view);
        CustomFlyInManager.Instance.FlyInGold(view.transform,() =>
        {
            //coinObject.transform.DOLocalMoveY(0,0);
            GameDataManager.Instance.UserData.Gold += 1;
            EventDispatcher.instance.TriggerChangeGoldUI(1, true);
        },1);
    }
    protected override  void OnDisable()
    {
        _stageOverObj.gameObject.SetActive(false);

        if(EventDispatcher.instance != null)
        {
            // EventDispatcher.instance.OnChangeGoldUI -= InitToolUI;
            EventDispatcher.instance.OnCheckShowChessTutorial -= CheckShowChessTutorialEvent;
        }
        EffectButterFlays.Clear();
        // CanvasScaler scaler = FindObjectOfType<Canvas>().GetComponent<CanvasScaler>();
        // scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.Expand;
        base.OnDisable();
    }
    // 有错误调用的
    public void AddWordError(int i)
    {
        wordErrorCount += i;
        ComboErrorCount += i;
    }
    
    // 自动完成的字
    public void AddCompleteCount(ChessView  word)
    {
        if (CurrStageInfo.StageNumber == 5 && IsClickAuto)
            return;
        
        // ButterWordAddIcon(word);
        ChessStageController.Instance.UseCompleteCount++;
    }

   
}
