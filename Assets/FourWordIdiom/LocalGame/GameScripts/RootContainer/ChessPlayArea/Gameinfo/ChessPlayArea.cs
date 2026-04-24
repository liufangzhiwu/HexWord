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
    [SerializeField] public Image effectMask;  //蒙版
    [Header("道具光效")]
    [SerializeField] public GameObject lightParticlePrefab; // 飞行的粒子/光效预制体
    
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
    //单个词语开始进行消除的时间
    private DateTime wordStartTime;
    /// <summary>
    /// 单个词语消除使用时长
    /// </summary>
    private float wordUserSeconds;
    private bool requireFocusCheck = false;
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
            var go = AssetBundleLoader.SharedInstance.LoadGameObject("useritems", "GameFinish");
            _stageOverObj = Instantiate(go, transform); 
            _stageOverObj.transform.SetAsLastSibling();
            _stageOverObj.transform.localScale = Vector3.one * 100;
            _stageOverObj.SetActive(false);
        }
        chessboardGrid.Initialize(this);
        puzzleTileTable.Initialize(this);
    }

    protected void Start()
    {
        lightParticlePrefab = AssetBundleLoader.SharedInstance.LoadGameObject("useritems", "ShowTipTuowei");
    }

    protected override void OnEnable()
    {
        PrepareForAnimation();
        base.OnEnable();
        UpdateUI();
        GameCoreManager.Instance.PanelState = PanelState.GamePingPanel;
        EventDispatcher.instance.OnCheckShowChessTutorial += CheckShowChessTutorialEvent;
        EventDispatcher.instance.OnAutoPassLevel += AutoPassLevel;
        StartCoroutine(SetupGameData());
        AudioManager.Instance.PlaySoundEffect("EnterStage");

        wordStartTime = DateTime.Now;
        
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

    
    private void OnApplicationFocus(bool focusStatus)
    {
        // 应用进入后台
        if (!focusStatus)
        {
            if(Game.self.Ads?.IsPlaying==true) return; //播放广告中
            
            wordUserSeconds += (float)DateTime.Now.Subtract(wordStartTime).TotalSeconds;
            requireFocusCheck = true;
        }
        else if (requireFocusCheck)
        {
            requireFocusCheck = false;
            wordStartTime = DateTime.Now;
        }
    }
    
    #endregion

    #region UI操作
    /// <summary>
    /// 更新游戏区域的UI
    /// </summary>
    private void UpdateUI()
    {
        if (SystemManager.Instance.PanelIsShowing(PanelType.ChessFinishView))
        {
            SystemManager.Instance.HidePanel(PanelType.ChessFinishView);
        }
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
        // Transform CompCost = CompleteBtn.transform.GetChild(0);
        Transform CompCount = CompleteBtn.transform.GetChild(1);
        Transform compText = CompCount.GetChild(0);
        Transform compAdd = CompCount.GetChild(1);
        if (GameDataManager.Instance.UserData.toolInfo[102].count > 0)
        {
            compText.GetComponent<Text>().text = GameDataManager.Instance.UserData.toolInfo[102].count.ToString();
            compText.gameObject.SetActive(true);
            compAdd.gameObject.SetActive(false);
            // CompCost.gameObject.SetActive(false);
        }
        else
        {
            // CompCost.GetComponentInChildren<Text>().text = GameDataManager.Instance.UserData.toolInfo[104].cost.ToString();
            // CompCost.gameObject.SetActive(true);
            compAdd.gameObject.SetActive(true);
            compText.gameObject.SetActive(false);
        }

        // Transform HintCost = HitsBtn.transform.GetChild(0);
        Transform HintCount = HitsBtn.transform.GetChild(1);
        Transform hintText = HintCount.GetChild(0);
        Transform hintAdd = HintCount.GetChild(1);
        if (GameDataManager.Instance.UserData.toolInfo[101].count > 0)
        {
            hintText.GetComponent<Text>().text = GameDataManager.Instance.UserData.toolInfo[101].count.ToString();
            hintText.gameObject.SetActive(true);
            hintAdd.gameObject.SetActive(false);
            // HintCost.gameObject.SetActive(false);
        }
        else
        {
            // HintCost.GetComponentInChildren<Text>().text = GameDataManager.Instance.UserData.toolInfo[102].cost.ToString();
            // HintCost.gameObject.SetActive(true);
            hintText.gameObject.SetActive(false);
            hintAdd.gameObject.SetActive(true);
        }
    }
    #endregion


    private IEnumerator SetupGameData()
    {
        //清理一下棋盘
        chessboardGrid.Clear();
        puzzleTileTable.Clear();
        yield return new WaitForEndOfFrame();
        IsClickAuto = false;
        float spacing = 4f;
        // 设置尺寸
        // int maxRow = CurrStageData.MaxRow;
        // int maxCol = CurrStageData.MaxCol;
        // int minRow = CurrStageData.MinRow;
        // int minCol = CurrStageData.MinCol;
        int rowCount = CurrStageData.MaxRow - CurrStageData.MinRow + 1;
        int colCount = CurrStageData.MaxCol - CurrStageData.MinCol + 1;
        float boardWidth = chessboardGrid.GetComponent<RectTransform>().rect.width;  // 棋盘可用宽度
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
        // 填入最后一个单词
        var puzzles = CurrStageData.FoundTargetPuzzles;
        if (puzzles != null && puzzles.Count > 0)
        {
            string word = puzzles[^1]; // 或 puzzles[0]
            UpdateLevelData(word);
        }
        // 填入蝶蛹字
        if (ChessStageController.Instance.pupaLetter != null)
        {
            Chesspiece pupa = ChessStageController.Instance.pupaLetter;
            if (chessboardGrid.GridList.TryGetValue((pupa.row, pupa.col), out ChessView pupaChessView))
            {
                pupaChessView.ShowButterflyPupa(true);
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
        SystemManager.Instance.ShowPanel(PanelType.HeaderSection);
        yield return new WaitUntil(() => isAnimFinished);
        // 检查一下是否存在错误的成功状态
        chessboardGrid.FixChessState();
        yield return new WaitForSeconds(0.2f);
        // 触发新手引导检查
        EventDispatcher.instance.TriggerCheckShowChessTutorial();
        yield return new WaitForSeconds(0.3f);
        //在第7关且词语少于9个的时候可以显示横幅广告
        Game.self.Ads?.ShowBanner();
        yield return new WaitForSeconds(0.4f);
        // 飞蝴蝶道具
        if (ChessStageController.Instance.IsFirstEnterStage&&useButterflyCount<=2)
        {
            if (!new[] { 1, }.Contains(CurrStageData.StageId))
            {
                for (int i = 0; i < useButterflyCount; i++)
                {
                    GameObject effectButt = Instantiate(butterflyPrefab, butterflyObj.transform.parent);
                    EffectButterFlays.Add(effectButt);
                }
                
                ToolInfo toolInfo =  GameDataManager.Instance.UserData.toolInfo[103];
                if (toolInfo.count > 0||GameDataManager.Instance.UserData.butterflyTaskIsOpen)
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
        AutoPassLevel();
    }
    
    LevelModes GetLevelDifficulty(int levelNumber) {
        if (levelNumber % 5 == 0) {
            if ((levelNumber / 5) % 2 == 1) {
                return LevelModes.Hard;
            } else {
                return LevelModes.ExtraHard;
            }
        }
        return LevelModes.Normal;
    }

    public IEnumerator SetupGame()
    {
        chessboardGrid.CreateChess();
        puzzleTileTable.CreatePuzzle();
        yield return new WaitUntil(() => chessboardGrid.GridList.Count > 0);
        
        ChessStageController.Instance.CurLevelMode=GetLevelDifficulty(CurrStageData.StageId);
        
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
        yield return null;
    }
    /// <summary>
    /// 重置UI状态，清理旧动画，并强制隐藏所有元素防止“走光”
    /// </summary>
    private void PrepareEnterAnimation()
    {
        // ==========================================
        // 1. 杀掉残余动画
        // ==========================================
        if (Stagetxt != null) { DOTween.Kill(Stagetxt.rectTransform); DOTween.Kill(Stagetxt); }
        if (chessboardGrid != null) DOTween.Kill(chessboardGrid.transform);
        if (puzzleTileTable != null) 
        {
            DOTween.Kill(puzzleTileTable.transform);
            if (puzzleTileTable.TryGetComponent<CanvasGroup>(out var cg)) DOTween.Kill(cg);
        }
        if (HitsBtn != null) DOTween.Kill(HitsBtn.transform);
        if (CompleteBtn != null) DOTween.Kill(CompleteBtn.transform);

        // ==========================================
        // 2. 🔥 核心修复：强行把所有元素设为“隐藏/缩小”的初始状态！
        // ==========================================
        // 顶部文字变透明
        if (Stagetxt != null) 
        {
            Color c = Stagetxt.color;
            c.a = 0f;
            Stagetxt.color = c;
        }

        // 棋盘强行缩为 0
        if (chessboardGrid != null) 
        {
            chessboardGrid.transform.localScale = Vector3.zero;
        }

        // 下方字库强行变透明
        if (puzzleTileTable != null && puzzleTileTable.TryGetComponent<CanvasGroup>(out var tableCG))
        {
            tableCG.alpha = 0f; 
        }

        // 按钮强行缩为 0
        if (HitsBtn != null) HitsBtn.transform.localScale = Vector3.zero;
        if (CompleteBtn != null) CompleteBtn.transform.localScale = Vector3.zero;
    }
    /// <summary>
    /// 播放入场动画, 建议在面板打开/初始化完成时调用这个方法
    /// </summary> 
    /// <param name="onComplete">动画播放完毕后的回调函数（可选）</param>
    public void PlayEnterAnimation(Action onComplete = null)
    {
        // 创建 DOTween 序列，编排入场节奏
        Sequence enterSeq = DOTween.Sequence();
        // 获取两个新的父节点
        Transform puzzleParent = puzzleTileTable != null ? puzzleTileTable.transform.parent : null;
        Transform btnGroupParent = HitsBtn != null ? HitsBtn.transform.parent : null;
        // --- 步骤 A：顶部关卡文字淡入并下落 ---
        if (Stagetxt != null)
        {
            enterSeq.Append(Stagetxt.rectTransform.DOAnchorPosY(Stagetxt.rectTransform.anchoredPosition.y - 50f, 0.4f).SetEase(Ease.OutBack));
            enterSeq.Join(Stagetxt.DOFade(1f, 0.4f));
        }

        // --- 步骤 B：上方棋盘整体淡入 ---
        if (chessboardGrid != null && chessboardGrid.TryGetComponent<CanvasGroup>(out var gridCG))
        {
            enterSeq.Append(gridCG.DOFade(1f, 0.5f).SetEase(Ease.InOutSine));
        }
        // --- 步骤 C：下方待选字盘【父节点】滑入并淡入 ---
        if (puzzleParent != null)
        {
            RectTransform parentRect = puzzleParent.GetComponent<RectTransform>();
            if (parentRect != null)
            {
                enterSeq.Insert(0.2f, parentRect.DOAnchorPosY(parentRect.anchoredPosition.y + 300f, 0.5f).SetEase(Ease.OutCubic));
            }
            if (puzzleParent.TryGetComponent<CanvasGroup>(out var tableCG))
            {
                enterSeq.Insert(0.2f, tableCG.DOFade(1f, 0.5f)); 
            }
        }
        // --- 步骤 D: 按钮【父节点】整体淡入 ---
        if (btnGroupParent != null && btnGroupParent.TryGetComponent<CanvasGroup>(out var btnCG))
        {
            // 整个按钮组一起在 0.6 秒处平滑淡入
            enterSeq.Insert(0.6f, btnCG.DOFade(1f, 0.4f).SetEase(Ease.InOutSine));
        }
        
        enterSeq.OnComplete(() =>
        {
            // 如果传入了回调方法，就执行它
            onComplete?.Invoke();
        });
    }
    /// <summary>
    /// 重置UI状态，防止重复打开时动画错乱
    /// </summary>
    private void PrepareForAnimation()
    {
        // 获取两个新的父节点
        Transform puzzleParent = puzzleTileTable != null ? puzzleTileTable.transform.parent : null;
        Transform btnGroupParent = HitsBtn != null ? HitsBtn.transform.parent : null;
        
        // ==========================================
        // 1. 杀掉旧动画
        // ==========================================
        if (Stagetxt != null) { DOTween.Kill(Stagetxt.rectTransform); DOTween.Kill(Stagetxt); }
        if (chessboardGrid != null) 
        {
            DOTween.Kill(chessboardGrid.transform);
            if (chessboardGrid.TryGetComponent<CanvasGroup>(out var gridCG)) DOTween.Kill(gridCG);
        }
    
        if (puzzleParent != null)
        {
            DOTween.Kill(puzzleParent);
            if (puzzleParent.TryGetComponent<CanvasGroup>(out var tableCG)) DOTween.Kill(tableCG);
        }

        if (btnGroupParent != null) 
        {
            DOTween.Kill(btnGroupParent);
            if (btnGroupParent.TryGetComponent<CanvasGroup>(out var btnCG)) DOTween.Kill(btnCG);
        }
        // ==========================================
        // 2. 强制初始状态 (隐藏)
        // ==========================================
        if (Stagetxt != null) 
        {
            Color c = Stagetxt.color; c.a = 0f; Stagetxt.color = c; 
            Stagetxt.rectTransform.anchoredPosition = new Vector2(Stagetxt.rectTransform.anchoredPosition.x, Stagetxt.rectTransform.anchoredPosition.y + 50f);
        }

        if (chessboardGrid != null) 
        {
            chessboardGrid.transform.localScale = Vector3.one;
            if (chessboardGrid.TryGetComponent<CanvasGroup>(out var cg)) cg.alpha = 0f;
        }

        // 按钮父级透明度降为 0
        if (btnGroupParent != null) 
        {
            btnGroupParent.localScale = Vector3.one;
            if (btnGroupParent.TryGetComponent<CanvasGroup>(out var cg)) cg.alpha = 0f;
        }

        // 字库父级初始位置往下偏移 300，透明度 0
        if (puzzleParent != null)
        {
            RectTransform tableRect = puzzleParent.GetComponent<RectTransform>();
            if (tableRect != null)
                tableRect.anchoredPosition = new Vector2(tableRect.anchoredPosition.x, tableRect.anchoredPosition.y - 300f);
        
            if (puzzleParent.TryGetComponent<CanvasGroup>(out var tableCG)) tableCG.alpha = 0f;
        }
    }
    /// <summary>
    /// 处理游戏内操作回调
    /// </summary>
    /// <param name="game">点击的物体</param>
    /// <param name="source">操作名称</param>
    public void HandleGamePlayCall(GameObject game, string source)
    {
        // Debug.LogWarning("进来了新手引导检查：" + source);
        // if (GameDataManager.Instance.UserData.ChessTutorialProgress.Values.All(v=>v))
        //     return;

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
#if Unity_ShowLog || UNITY_EDITOR
        if (GameCoreManager.Instance.IsTrueAuto) return;
#endif
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
        // float energy = GameDataManager.Instance.ChessDynamicHardSave.EnergyValue;
        TimeSpan timeSpan = DateTime.Now.Subtract(wordStartTime);
        wordUserSeconds+=(float)timeSpan.TotalSeconds;
        
        AnalyticMgr.LevelProgress(puzzleId, puzzle, wordUserSeconds,
            wordErrorCount,ChessStageController.Instance.PuzzleComboCount,usetoolCount);

        wordUserSeconds = 0;
        wordStartTime = DateTime.Now;
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
        wordUserSeconds = 0;
        Debug.Log("关卡完成时当前进度：" + JsonConvert.SerializeObject(GameDataManager.Instance.UserData.ChessTutorialProgress));
        puzzleTileTable.transform.parent.gameObject.SetActive(false);
        yield return new WaitForSeconds(0.1f);
        bool isOk = false;
        ShowEffectMask(b => isOk = b);
        yield return new WaitUntil(() => isOk);
        ChessStageController.Instance.CompleteStage(CurrStageInfo.StageNumber, wordErrorCount);
        EventDispatcher.instance.TriggerChangeTopRaycast(false);
    }
    /// <summary>
    /// 渐显蒙版，并阻挡点击
    /// </summary>
    private void ShowEffectMask(Action<bool> action = null)
    {
        if (effectMask == null) return;
        effectMask.gameObject.SetActive(true);
        effectMask.raycastTarget = true; // 确保阻挡点击穿透
        
        // 确保从透明开始，花 0.2 秒渐变到 0.6 的半透明黑
        Color c = effectMask.color;
        c.a = 0f;
        effectMask.color = c;
        effectMask.DOFade(0.6f, 0.2f).SetEase(Ease.OutQuad)
            .OnComplete(()=>
            {
                _stageOverObj.SetActive(true);
                action?.Invoke(true);
            });
    }
    /// <summary>
    /// 渐隐蒙版，并恢复点击
    /// </summary>
    private void HideEffectMask()
    {
        if (effectMask == null) return;
        
        // 花 0.2 秒渐变回完全透明，播完后关掉节点
        effectMask.DOFade(0f, 0.2f).SetEase(Ease.InQuad).OnComplete(() => 
        {
            effectMask.gameObject.SetActive(false);
        });
    }
    // 暂未在游戏中打开词库
    private void ClickLevelPuzzle()
    {
        ChessStageController.Instance.IsEnterVocabulary = true;
        SystemManager.Instance.ShowPanel(PanelType.LevelWordScreen);
    }
    public void UseComplete(bool isReset = false)
    {
        ToolInfo toolInfo = GameDataManager.Instance.UserData.toolInfo[102];

        if(toolInfo == null || chessboardGrid.GameOver)
        {
            // Debug.LogError("[GameManager] There is no hint with the given hint id: ");
            CompleteBtn.enabled = true;
            return;
        }

        bool useCoins = false;

        if(toolInfo.count <= 0)
        {
            GetItemScreen.limitRewordType = LimitRewordType.Tipstool;
            SystemManager.Instance.ShowPanel(PanelType.GetItemScreen);
            return;
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
            GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, -1, "关卡内使用");
            InitToolUI();
        }

        AudioManager.Instance.PlaySoundEffect("ItemUSe02");
        // 实现业务
        StartCoroutine(chessboardGrid.CompletedPhrase());

        // 触发新手引导检查
        HandleGamePlayCall(CompleteBtn.gameObject, "UseComplete");
    }
    /// <summary>
    /// 自动完成道具的“青蛙跳”光效
    /// </summary>
    public void PlayAutoCompleteJumpEffect(List<ChessView> targets, Action onComplete)
    {
        // 开启一个协程来完美接管时间轴
        StartCoroutine(JumpAndRevealCoroutine(targets, onComplete));
    }
    private IEnumerator JumpAndRevealCoroutine(List<ChessView> targets, Action onComplete)
    {
        if (targets == null || targets.Count == 0 || lightParticlePrefab == null) 
        {
            onComplete?.Invoke();
            yield break;
        }

        List<ChessView> emptyTargets = targets.Where(t => 
            t.CurrState == TileState.None || 
            t.CurrState == TileState.Error || 
            t.CurrState == TileState.Fill ||
            t.CurrState == TileState.Check).ToList();

        if (emptyTargets.Count == 0)
        {
            onComplete?.Invoke();
            yield break;
        }
        
        GameObject particle = Instantiate(lightParticlePrefab, transform);
        particle.transform.position = CompleteBtn.transform.position; 
        particle.transform.SetAsLastSibling();
        
        Vector3 startPos = particle.transform.position;
        Vector3 firstTargetPos = emptyTargets[0].transform.position;
        Vector3[] firstPath = CreateBezierPath(startPos, firstTargetPos, -0.3f);
        
        Sequence seq = DOTween.Sequence();
        
        // 1. 飞到第 1 个格子
        seq.Append(particle.transform.DOPath(firstPath, 0.2f, PathType.Linear).SetEase(Ease.InOutSine));
        seq.AppendCallback(() => {
            // 删掉外面的 SetTipMessage，因为你的 PlayRevealAnimation 里面已经有了，防止重复调用！
            emptyTargets[0].PlayRevealAnimation1(emptyTargets[0].transform); 
        });
        
        // 2. 依次跳跃
        for (int i = 1; i < emptyTargets.Count; i++)
        {
            int currentIndex = i;
            // 🔥 核心修复：动态计算跳跃高度！绝对完美的青蛙跳比例！
            // 取“上一个格子”和“当前格子”的距离
            float distance = Vector3.Distance(emptyTargets[currentIndex - 1].transform.position, emptyTargets[currentIndex].transform.position);
            // 跳跃高度设定为距离的一半（比如相距 100 像素，就往上跳 50 像素）
            float jumpHeight = distance * 0.5f;
            seq.Append(particle.transform.DOJump(emptyTargets[currentIndex].transform.position, jumpHeight, 1, 0.1f).SetEase(Ease.Linear));
            
            seq.AppendCallback(() => {
                emptyTargets[currentIndex].PlayRevealAnimation1(emptyTargets[currentIndex].transform); 
            });
        }
        
        // 钻进去消失
        seq.Append(particle.transform.DOScale(Vector3.zero, 0.15f));
        
        // ==========================================
        // 🔥 核心时间轴控制：耐心等待特效播放完毕
        // ==========================================
        
        // 1. 死等 DOTween 的青蛙跳和飞行彻底结束
        yield return seq.WaitForCompletion();
        Destroy(particle);

        // 2. 此时，最后一个格子的 PlayRevealAnimation 协程才刚刚被触发！
        // 你的协程逻辑是：等 0.2 秒 -> 弹文字缩放(0.3秒) -> 等 3.5 秒 -> 销毁。
        // 💡 为了最佳的爽快感：我们只等文字完美弹出来（0.2 + 0.3 = 0.5秒），就立刻变绿！
        // 千万不要等 3.5 秒特效全删了才变绿，那样玩家会觉得卡顿。背景残留着华丽的粒子时整句变绿，视觉冲击力最强！
        yield return new WaitForSeconds(0.35f);

        // 3. 时间刚刚好！通知游戏，播放整句变绿的成功波浪动画！
        onComplete?.Invoke();
    }
    /// <summary>
    /// 自动完成道具的“青蛙跳”光效
    /// </summary>
    public void PlayAutoCompleteJumpEffect2(List<ChessView> targets, Action onComplete)
    {
        if (targets == null || targets.Count == 0 || lightParticlePrefab == null) 
        {
            onComplete?.Invoke();
            return;
        }
        List<ChessView> emptyTargets = targets.Where(t => 
            t.CurrState == TileState.None || 
            t.CurrState == TileState.Error || 
            t.CurrState == TileState.Fill ||
            t.CurrState == TileState.Check).ToList();
        Debug.LogWarning("进入了几个 "+ emptyTargets.Count);
        // 如果没有空格子，直接回调完成
        if (emptyTargets.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }
        
        // 在“完成”按钮生成光效
        GameObject particle = Instantiate(lightParticlePrefab, transform);
        particle.transform.position = CompleteBtn.transform.position; 
        particle.transform.SetAsLastSibling();
        
        Vector3 startPos = particle.transform.position;
        Vector3 firstTargetPos = emptyTargets[0].transform.position;
        // 生成到第一个格子的弧线 (负数代表向另一边弯)
        Vector3[] firstPath = CreateBezierPath(startPos, firstTargetPos, -0.3f);
        
        Sequence seq = DOTween.Sequence();
        // 1. 第一步：从按钮飞到词组的第 1 个格子
        seq.Append(particle.transform.DOPath(firstPath, 0.4f, PathType.Linear).SetEase(Ease.InOutSine));
        seq.AppendCallback(() => {
            emptyTargets[0].SetTipMessage();     // 显示字
            emptyTargets[0].PlayRevealAnimation1(emptyTargets[0].transform); // 播放高亮闪烁
        });
        // 2. 第二步：在词组内的剩余格子上“依次跳跃” (高度 50，跳 1 次)
        for (int i = 1; i < emptyTargets.Count; i++)
        {
            int currentIndex = i;
            seq.Append(particle.transform.DOJump(targets[i].transform.position, 50f, 1, 0.2f).SetEase(Ease.Linear));
            // seq.Append(particle.transform.DOJump(targets[i].transform.position, 1.0f, 1, 0.2f).SetEase(Ease.Linear));
            // 🔥 核心新增：每跳落地一个格子，就立刻点亮当前的字！
            seq.AppendCallback(() => {
                emptyTargets[currentIndex].SetTipMessage();     // 显示字
                emptyTargets[currentIndex].PlayRevealAnimation1(emptyTargets[currentIndex].transform); // 播放高亮闪烁
            });
        }
        // seq.Append(particle.transform.DOScale(Vector3.zero, 0.15f));
        // 3. 跳跃彻底结束，销毁粒子，回调继续执行后续逻辑
        seq.OnComplete(() => {
            Destroy(particle);
            onComplete?.Invoke();
        });
    }
    
    /// <summary>
    /// 使用提示工具
    /// </summary>
    public void UseTips()
    {
        ToolInfo toolInfo = GameDataManager.Instance.UserData.toolInfo[101];

        if (toolInfo == null || chessboardGrid.GameOver)
        {
            // Debug.LogError("[GameManager] There is no hint with the given hint id: ");
            return;
        }

        if (chessboardGrid.IsSelectTip())
        {
            MessageSystem.Instance.ShowTip("已经提示过了！");
            return;
        }
        
        bool useCoins = false;
        if(toolInfo.count <= 0)
        {
            GetItemScreen.limitRewordType = LimitRewordType.SingleTipsttool;
            SystemManager.Instance.ShowPanel(PanelType.GetItemScreen);
            return;
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
            GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.SingleTipsttool, 1, "道具购买");
            GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.SingleTipsttool, -1, "关卡内使用");
        }
        else
        {
            GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.SingleTipsttool, -1, "关卡内使用");
            InitToolUI();
        }
        // chessboardGrid.SetSelectTip();
        
        AudioManager.Instance.PlaySoundEffect("ItemUSe01");
        DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedUseTipsTool,1);
        StartCoroutine(FlyHintEffect(chessboardGrid.selecteTile));
        // 触发新手引导检查
        if (CurrStageInfo.StageNumber == 2)
        {
            BowlView hitBowl = puzzleTileTable.GridList
                .FirstOrDefault(bowl => bowl.letter == chessboardGrid.selecteTile.chesspiece.letter);
            HandleGamePlayCall(hitBowl!.gameObject, "UseTips");
        }
     
    }

    private IEnumerator FlyHintEffect(ChessView targetTile)
    {
        if (lightParticlePrefab == null) yield break;

        // 1. 在提示按钮的位置生成光效
        GameObject particle = Instantiate(lightParticlePrefab, transform);
        particle.transform.position = HitsBtn.transform.position;
        particle.transform.SetAsLastSibling(); // 放到最顶层
        // 🔥 解决“太小”的问题：初始设为0，瞬间放大到原来的 2.5倍 (倍数可根据你的预制体自己调)
        Vector3 targetScale = Vector3.one * 2.5f; 
        particle.transform.localScale = Vector3.zero;
        particle.transform.DOScale(targetScale, 0.2f).SetEase(Ease.OutBack);
        // 2. 计算动态距离和时长
        Vector3 startPos = particle.transform.position;
        Vector3 endPos = targetTile.transform.position;
        float duration = 0.25f;
        // 3. 生成贝塞尔弧线路径
        Vector3[] pathPoints = CreateBezierPath(startPos, endPos, 0.3f); // 150f是弧度，可以调大调小
        // 锁定屏幕防止飞行时玩家乱点
        EventDispatcher.instance.TriggerChangeTopRaycast(false);
        // 4. 沿着弧线飞行
        bool isFlying = true;
        particle.transform.DOPath(pathPoints, duration, PathType.Linear).SetEase(Ease.InOutSine).OnComplete(() =>
        {
            isFlying = false;
        });
        particle.transform.DOScale(Vector3.zero, 0.15f).SetDelay(duration - 0.15f);
        // 等待飞到目标
        yield return new WaitUntil(() => !isFlying);
        
        // 3. 到达目标！销毁粒子
        Destroy(particle);
        
        // 4. 显示提示字
        targetTile.SetTipMessage();
        
        // 5. 触发边缘高亮动画
        StartCoroutine(  targetTile.PlayRevealAnimation(targetTile.transform));
        
        EventDispatcher.instance.TriggerChangeTopRaycast(true);
    }
    /// <summary>
    /// 生成二阶贝塞尔曲线路径点 (完美适配任意分辨率和Canvas缩放)
    /// </summary>
    /// <param name="bendFactor">弯曲比例（0.2~0.5之间效果最好，正负代表向左/向右弯）</param>
    private Vector3[] CreateBezierPath(Vector3 start, Vector3 end, float bendFactor = 0.3f, int segments = 10)
    {
        Vector3[] path = new Vector3[segments + 1];
        Vector3 mid = (start + end) / 2f;
        
        // 1. 获取起点到终点的方向，并计算实际世界距离
        Vector3 dir = (end - start).normalized;
        float dist = Vector3.Distance(start, end);
        
        // 2. 计算出垂直于飞行方向的向量 (2D平面内的法线)
        Vector3 perpendicular = new Vector3(-dir.y, dir.x, 0); 
        
        // 3. 控制点：中点 + 垂直方向 * (总距离 * 弯曲比例)
        // 这样无论 UI 被缩放得多小，弧线永远是刚好鼓出去一截的完美状态！
        Vector3 controlPoint = mid + perpendicular * (dist * bendFactor);

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float u = 1 - t;
            path[i] = (u * u * start) + (2 * u * t * controlPoint) + (t * t * end);
        }
        return path;
    }
    private void UseButterfly()
    {
        ToolInfo toolInfo = GameDataManager.Instance.UserData.toolInfo[103];
        
        if ((toolInfo == null || toolInfo.count <= 0)&&!GameDataManager.Instance.UserData.butterflyTaskIsOpen)
        {
            Debug.LogError("蝴蝶道具数据为空！");
            // crossPuzzleGrid.SetPuzzleBoardState(true);
            butterflyObj.GetComponent<RectTransform>().DOAnchorPosX(-300, 0.3f);
            return;
        }

        if (!GameDataManager.Instance.UserData.butterflyTaskIsOpen)
        {
            GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Butterfly, -1,"关卡内使用");
        }
     
        DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedUseButterflyTool,1);
        useButterflyCount--;
        
        GameObject Effect_Butterfly = EffectButterFlays[useButterflyCount];
        butterflyObj.GetComponentInChildren<Text>().text = (useButterflyCount+1).ToString();
        Effect_Butterfly.gameObject.SetActive(false);
        
        if(useButterflyCount==0)
            AudioManager.Instance.PlaySoundEffect("showButterfly");
        
        ChessView selectView = chessboardGrid.GetRandomNoneNonTipChess();
        butterChess.Add(selectView);
        
        // ChessView  selectNext  蝴蝶搜索的位置
        // 播放起飞
        butterflyObj.GetComponent<RectTransform>().DOAnchorPosX(0,0.3f).OnComplete(() =>
        {
           
            Vector3[] MovePoints = GetButterflyPath(butterflyObj.transform,selectView.transform.position + new Vector3(3f, 0,0));
       
            Effect_Butterfly.transform.DOScale(new Vector3(50, 50, 50), 0.1f).OnComplete(() =>
            {
                Effect_Butterfly.transform.DOLocalRotate(Vector3.zero,0f);
                Effect_Butterfly.gameObject.SetActive(true);
                butterflyObj.GetComponentInChildren<Text>().text = useButterflyCount.ToString();
                
                selectView.chesspiece.tip = true;
                Effect_Butterfly.transform.DOScale(new Vector3(50, 50, 50), 0.25f).OnComplete(() =>
                {
                    // if(useButterflyCount>0)
                    //     UseButterfly();
                    // else
                        butterflyObj.GetComponent<RectTransform>().DOAnchorPosX(-300, 0.3f);
                }); 
                if(useButterflyCount>0)
                    UseButterfly();
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
        ChessGuideSystem.Instance.CloseGuide();
        HideEffectMask();
        chessboardGrid.Clear();
        puzzleTileTable.Clear();
        _stageOverObj.gameObject.SetActive(false);

        if(EventDispatcher.instance != null)
        {
            // EventDispatcher.instance.OnChangeGoldUI -= InitToolUI;
            EventDispatcher.instance.OnCheckShowChessTutorial -= CheckShowChessTutorialEvent;
            EventDispatcher.instance.OnAutoPassLevel -= AutoPassLevel;
        }

        if (EffectButterFlays.Count > 0)
        {
            foreach (GameObject Effect_Butterfly in EffectButterFlays)
            {
                Effect_Butterfly.gameObject.SetActive(false);
            }
        }

        butterflyObj.GetComponent<RectTransform>().DOAnchorPosX(-200, 0f);
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
        ChessStageController.Instance.OnUpdateRewardPuzzle(false);
    }
    
    // 自动完成的字
    public void AddCompleteCount(ChessView  word)
    {
        if (CurrStageInfo.StageNumber == 5 && IsClickAuto)
            return;
        
        // ButterWordAddIcon(word);
        ChessStageController.Instance.UseCompleteCount++;
    }

    // 自动跑关
    public void AutoPassLevel()
    {
#if Unity_ShowLog || UNITY_EDITOR
        if (GameCoreManager.Instance.IsTrueAuto)
        {
            // 实现业务
            StartCoroutine(HandleCoroutine());
        }
#endif
    }

    private IEnumerator HandleCoroutine()
    {
        yield return new WaitForSeconds(0.2f);
        if (!string.IsNullOrEmpty(chessboardGrid.selecteTile.Answer))
        {
            BowlView bowl = puzzleTileTable.GridList.FirstOrDefault(v => 
                v.letter == chessboardGrid.selecteTile.Answer &&
                v.bowl.status == 0);
            if(bowl!=null)
                puzzleTileTable.OnPuzzleSelected(bowl);
        }
        yield return null;
        // yield return chessboardGrid.CompletedPhrase();
    }
    
    // 检查是否蝉蛹字
    public void CheckPupaChess(ChessView view)
    {
        if (ChessStageController.Instance.pupaLetter != null)
        {
            Chesspiece pupa = ChessStageController.Instance.pupaLetter;
            if (pupa.Equals(view.chesspiece))
            {
                CurrStageData.PupaDatas = null;
                ChessStageController.Instance.pupaLetter = null;
                ButterfliesManager.Instance.AddObtainedPupaOnGamePanel(view.transform);
                view.ShowButterflyPupa(false);
                //ButterfliesManager.Instance.AddObtainedPupa(view.transform,1, butterflyPoint);
            }
            else if(view.GetPupaObjIsShow())
            {
                Debug.Log($"为什么没有计算？ + 当前{view.Answer} pupa{pupa.letter} "+ pupa.Equals(view.chesspiece));
            }
           
        }
    }
}
