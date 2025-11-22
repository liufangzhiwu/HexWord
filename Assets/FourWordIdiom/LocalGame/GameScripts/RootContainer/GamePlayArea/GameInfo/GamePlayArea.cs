using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Random = System.Random;

public class GamePlayArea : UIWindow
{
    [SerializeField] private GameObject GameBase;   
    [SerializeField] private GameObject ButterflyPrefab;
    [SerializeField] private GameObject ButterflyObj;
    [SerializeField] private GameObject StageOverObj;      
    [SerializeField] private GameObject ResetCostObj;      
    [SerializeField] private GameObject ResetCounttxt;        
         
    [SerializeField] private GameObject HintCostObj;       
    [SerializeField] private GameObject HintCounttxt;        

    [SerializeField] private Button PuzzleTipsBtn;
    [SerializeField] private Button LayerBtn;
    [SerializeField] private Button LevelPuzzleBtn;
    [SerializeField] private Text Stagetxt;
    
    //选定词面板
    [SerializeField] private ChoicePuzzleTable choicePuzzleTable;
    //字块矩阵面板
    [SerializeField] private CrossPuzzleGrid crossPuzzleGrid = null;
    //组成词处理面板
    [SerializeField] private PuzzleTileBoard PuzzleTileBoard = null;
    //可选词汇列表
    private HashSet<string> selectablePuzzles;
    // 获取棋盘上所有剩余的单词
    List<string> leftPuzzles;
    private ObjectPool tileLetterTextPool;
    private WordMatrixExplorer boardExplorer;
    private DateTime StartTime;

    List<GameObject> Effect_Butterflys=new List<GameObject>(); 
    private int wordErrorCount;
    private int usetoolCount;
    
    private int useButterflyCount;
    private bool IsUseButterfly;
    private bool firstenter;
    
    [Header("Detection Settings")]
    public float inactivityThreshold = 8f; // 无操作判定阈值（秒）
    public bool checkKeyboard = true;
    public bool checkMouseMovement = true;
    public bool checkMouseClicks = true;
    public bool checkTouch = true;
    
    private Coroutine inactivityCheckCoroutine;
   

    private StageInfo CurStageInfo
    {
        get { return StageHexController.Instance.CurStageInfo; }
    }
    
    private StageProgressData CurStageData
    {
        get { return StageHexController.Instance.CurStageData; }
    }

    protected override void Awake()
    {
        base.Awake();
        InitializeButtons();
        Initialize();
        firstenter = true;
        IsUseButterfly=false;
    }

    protected  void InitializeButtons()
    {
        LayerBtn.AddClickAction(()=>ToolItemReset(),"");
        PuzzleTipsBtn.AddClickAction(UseTips,"");
        LevelPuzzleBtn.AddClickAction(OnClickWordVocabulary);
    }

    public void Initialize()
    {
        selectablePuzzles = new HashSet<string>();
        
        choicePuzzleTable.Initialize();
        //PuzzleTileBoard.InitPuzzleTileBoard();      
        crossPuzzleGrid.Initialize();
    }

    private void InitUI()
    {
        Stagetxt.text =MultilingualManager.Instance.GetString("Level")+" " + CurStageInfo.StageNumber;
        InitToolUI();
    }

    private void InitToolUI(int value=0,bool isfirst=false)
    {
        if (GameDataManager.Instance.UserData.toolInfo[101].count > 0) 
        {
            ResetCounttxt.GetComponentInChildren<Text>().text =GameDataManager.Instance.UserData.toolInfo[101].count.ToString();
            ResetCostObj.gameObject.SetActive(false);
            ResetCounttxt.gameObject.SetActive(true);
        }
        else
        {
            ResetCostObj.GetComponentInChildren<Text>().text = GameDataManager.Instance.UserData.toolInfo[101].cost.ToString();
            ResetCostObj.gameObject.SetActive(true);
            ResetCounttxt.gameObject.SetActive(false);
        }

        if (GameDataManager.Instance.UserData.toolInfo[102].count > 0)
        {
            HintCounttxt.GetComponentInChildren<Text>().text = GameDataManager.Instance.UserData.toolInfo[102].count.ToString();
            HintCostObj.gameObject.SetActive(false);
            HintCounttxt.gameObject.SetActive(true);
        }
        else
        {
            HintCostObj.GetComponentInChildren<Text>().text = GameDataManager.Instance.UserData.toolInfo[102].cost.ToString();
            HintCostObj.gameObject.SetActive(true);
            HintCounttxt.gameObject.SetActive(false);
        }
    }

    protected override void OnEnable()
    {
        InitUI();
        EventDispatcher.instance.OnLetterSelected += OnLetterSelected;
        //EventManager.OnChangeLanguageUpdateUI += InitUI;
        //EventManager.OnComboTriggerButterfly +=UseButterfly;
        EventDispatcher.instance.OnChoicePuzzleSetStatus += ChoicePuzzleSetStatus;
        EventDispatcher.instance.OnCheckShowTutorial += CheackShowTotrialEvent;
            
        StartCoroutine(SetupGameData());
        boardExplorer = new WordMatrixExplorer(CurStageData.BoardSnapshot,CurStageInfo.Puzzles);
        AudioManager.Instance.PlaySoundEffect("EnterStage");
        EventDispatcher.instance.TriggerChoicePuzzleSetStatus(true);
        
        LevelPuzzleBtn.gameObject.SetActive(false);
        //LevelPuzzleBtn.gameObject.SetActive(GameDataManager.Instance.UserData.GetWordVocabulary().LevelWords.Count > 0);
        
        StartTime = DateTime.Now;
        EventDispatcher.instance.OnChangeGoldUI += InitToolUI;
        
        // ToolInfo toolInfo = GameDataManager.Instance.UserData.toolInfo[103];
        // if (GameDataManager.Instance.UserData.butterflyTaskIsOpen)
        // {
        //     useButterflyCount =AppGameSettings.MaxButterfliesPerLevel;
        // }
        // else
        // {
        //     useButterflyCount = toolInfo.count>=2? AppGameSettings.MaxButterfliesPerLevel: toolInfo.count;
        // }
        
        // 开始检测协程

        if (CurStageInfo.StageNumber <= 4)
        {
            StartCoroutine(CheckInactivity());
            StageHexController.Instance.tipPuzzle = "";
        }
        
       
    }

    IEnumerator SetupGameData()
    {
        IsUseButterfly=false;
        // 等待当前帧的所有渲染操作完成
        yield return new WaitForSeconds(0.2f);

        SetupGame();
        PuzzleTipsBtn.gameObject.SetActive(GameDataManager.Instance.UserData.CurrentHexStage >=3);
        LayerBtn.gameObject.SetActive(GameDataManager.Instance.UserData.CurrentHexStage >=2);
        // 获取 RectTransform 组件
        RectTransform rectTransform = GetComponent<RectTransform>();
        rectTransform.offsetMin = new Vector2(0, 0); // Left 和 Bottom
        //在第7关且词语少于9个的时候可以显示横幅广告
        //if (CurStageInfo.StageNumber >= 7 && StageController.Instance.CurStageInfo.Puzzles.Count <= 9)
        if (CurStageInfo.StageNumber >= 7)
        {
 //             bool ishowbanner= AdsManager.Instance.ShowBannerAd(rectTransform);
 //             if (ishowbanner)
 //             {
 //                 rectTransform.offsetMin = new Vector2(0, 140); // Left 和 Bottom
 //             }
 //             else
 //             {
 // #if UNITY_IOS 
 //                 // 设置偏移值
 //                 rectTransform.offsetMin = new Vector2(0, 230); // Left 和 Bottom
 // #elif UNITY_ANDROID
 //                 rectTransform.offsetMin = new Vector2(0, 140); // Left 和 Bottom
 // #endif
 //             }
            //Debug.LogError("底部位置" + rectTransform.offsetMin);
        }
        else
        {
            //AdsManager.Instance.HideBannerAd();
            // 设置偏移值
            //rectTransform.offsetMin = new Vector2(0, 0); // Left 和 Bottom
        }

        yield return new WaitForSeconds(0.4f);
        
        if (CurStageData.StageId !=1&&CurStageData.StageId !=2&&CurStageData.StageId !=3&&CurStageData.StageId !=6&& CurStageData.PuzzleHints.Count <= 0
            &&StageHexController.Instance.IsFirstEnterStage)
        {
            if (useButterflyCount <= 2)
            {
                for (int i = 0; i < useButterflyCount; i++)
                {
                    GameObject Effect_Butt=Instantiate(ButterflyPrefab,ButterflyObj.transform.parent); 
                    Effect_Butterflys.Add(Effect_Butt);
                }
            }
            
            //ToolInfo toolInfo =  GameDataManager.Instance.UserData.toolInfo[103];

            crossPuzzleGrid.SetPuzzleBoardState(true);
        }
        else
        {
            crossPuzzleGrid.SetPuzzleBoardState(true);
        }
        
        if (StageHexController.Instance.IsFirstEnterStage)
        {
            wordErrorCount = 0;
            usetoolCount = 0;
            //ShopManager.shopManager.ShowLimitAdsPanel();
        }
           
    }

    //设置当前关卡数据（配置数据、保存进度数据）
    public void SetupGame()
    {
        // 确保所选单词不可见
        choicePuzzleTable.Reset();

        // 根据关卡数据生产字块矩阵
        crossPuzzleGrid.CreatePuzzles(true);

        //先设置单词列表处理器
        PuzzleTileBoard.Setup(CurStageInfo, CurStageData);   
        
        UpdateSelectablePuzzles();
        UpdateLeftPuzzles();
    } 

    /// <summary>
    /// 选中词语回调事件
    /// </summary>
    private void OnLetterSelected(string Puzzle, List<int[]> gridCellPositions)
    {
        /// <summary>
        /// 检查词语是否为当前关卡词语
        /// </summary>
        if (StageHexController.Instance.CurStageInfo.IsPuzzleInStage(Puzzle))
        {
            StartCoroutine(PuzzleSelectedEvent(Puzzle, gridCellPositions));
            //是否新手引导的词
            if (Puzzle == GuideSystem.Instance.targetPuzzle && GameDataManager.Instance.UserData.GetTutorialProgress() <= 2)
            {
                GuideSystem.Instance.CloseGuide();
            }
        }
        else
        {
             EventDispatcher.instance.TriggerPlayChoicePuzzle(gridCellPositions, true);
             EventDispatcher.instance.TriggerUpdateRewardPuzzle(false);
             wordErrorCount++;
        }
    }

    /// <summary>
    /// 处理已组合词语事件（重构版）
    /// 新增差异化处理：根据词语类型和游戏状态执行不同逻辑
    /// </summary>
    IEnumerator PuzzleSelectedEvent(string puzzle, List<int[]> gridCellPositions)
    {
        // 第一阶段：基础响应
        choicePuzzleTable.FadeOut(0.2f);      

        // 检查是否已找到
        if (choicePuzzleTable.CheckPuzzleFound(puzzle)) yield break;
        
        // 保存词语
         StageHexController.Instance.AddFoundPuzzle(puzzle);

        // 差异化点2：根据词语类型选择动画
        PlayPuzzleAnimation(puzzle, gridCellPositions);

        // 禁用网格单元
        crossPuzzleGrid.RemovePuzzleFound(gridCellPositions);

        // 差异化点3：根据游戏模式调整反馈
        PlaySelectionFeedback(0);

        // 差异化点4：存档条件判断
        if (ShouldSaveProgress(puzzle))
        {
            GameDataManager.Instance.UpdateLevelProgress(CurStageData);
        }

        yield return new WaitForSeconds(0.2f);

        // 更新可选词语
        UpdateSelectablePuzzles();

        // 关卡完成检查
        if (CheckStageComplete())
        {
            EventDispatcher.instance.TriggerChangeTopRaycast(false);
            yield return new WaitForSeconds(0.8f);
            HandleStageCompletion();
        }
        else
        {
            // 差异化点5：智能重置判断
            //StartCoroutine(HandleResetLogic());
            EventDispatcher.instance.TriggerUpdateRewardPuzzle(true);
            EventDispatcher.instance.TriggerCheckShowTutorial();
        }

        UpdateLeftPuzzles();
        
        wordErrorCount = 0;
    }

    // 差异化点2：动画选择
    private void PlayPuzzleAnimation(string puzzle, List<int[]> positions)
    {
        // 根据词语长度选择不同动画
        if (puzzle.Length > 4)
        {
             //LettersToMovePuzzleTileAnim(puzzle, positions);
        }
        else
        {
            LettersToMovePuzzleTileAnim(puzzle, positions);
        }
        
        // if(puzzle==tipPuzzle)
        // {
        //     tipPuzzle = "";
        // }
    }

    // 差异化点3：反馈调整
    private void PlaySelectionFeedback(int mode)
    {
        // 基础音效
        AudioManager.Instance.PlaySoundEffect("ciright");

        // 仅限普通模式触发震动
        if (mode == 0)
        {
            AudioManager.Instance.TriggerVibration(1, 10);
        }

        // 解谜模式添加额外效果
        if (mode == 1)
        {
            AudioManager.Instance.TriggerVibration(1, 10);
            //PlaySparkleEffect();
        }
    }

    // 差异化点4：存档逻辑
    private bool ShouldSaveProgress(string puzzle)
    {
        // 重要词语立即保存，普通词语延迟保存
        return CurStageData.Puzzles.Contains(puzzle);
    }

    // 差异化点5：智能重置
    private IEnumerator HandleResetLogic()
    {
        // 无选择时：根据关卡进度决定重置方式
        if (selectablePuzzles.Count == 0)
        {
            // 进度不足时使用普通重置
            yield return ResetTool();
        }
    }

    // 差异化点6：完成处理
    private void HandleStageCompletion()
    {       
        _windowAnimator.Play("StageOver");
        StageOverObj.gameObject.SetActive(true);
        StageHexController.Instance.CompleteStage(CurStageInfo.StageNumber);

        StageHexController.Instance.ActiveTileSize = 0;
        //EventDispatcher.instance.TriggerChangeTopRaycast(false);
    }
    
    private void ChoicePuzzleSetStatus(bool status)
    {
        choicePuzzleTable.gameObject.SetActive(status);
    }
  

    private void CheackShowTotrialEvent()
    {
        StartCoroutine(CheackShowTotrial());
    }
    
    IEnumerator CheackShowTotrial()
    { 
        yield return new WaitForSeconds(0.1f);
        
        // if (GameDataManager.Instance.UserData.GetTutorialProgress() < 2)
        // {
        //     string Str = GetRandomTipsPuzzle(true);
        //     //selectablePuzzles.Contains(Str);
        //     GuideSystem.Instance.PuzzleGrids= crossPuzzleGrid.GetPuzzleTileRowCol(Str);
        //     GuideSystem.Instance.targetPuzzle = Str;
        //     GuideSystem.Instance.DisplayGuide();
        // }

        if (CurStageData.StageId == 2&&GameDataManager.Instance.UserData.GetTutorialProgress()==2)
        {
            yield return new WaitForSeconds(0.2f);
            //提示首字
            GuideSystem.Instance.activeToolObject= LayerBtn.gameObject;
            GuideSystem.Instance.DisplayGuide();
        }
        
        if (CurStageData.StageId == 3&&GameDataManager.Instance.UserData.GetTutorialProgress()==3)
        {
            yield return new WaitForSeconds(0.2f);
            GuideSystem.Instance.activeToolObject =PuzzleTipsBtn.gameObject;
            GuideSystem.Instance.DisplayGuide();
        }
    }
    
    /// <summary>
	/// 更新当前关卡可以直接选择组合的成语
	/// </summary>
	private void UpdateSelectablePuzzles()
    {
        selectablePuzzles.Clear();
        selectablePuzzles = boardExplorer.ExploreWordMatrix();
    }

    private void UpdateLeftPuzzles()
    {
        if(CurStageData.FoundTargetPuzzles!=null)
            leftPuzzles = CurStageInfo.Puzzles.FindAll(Puzzle => !CurStageData.FoundTargetPuzzles.Contains(Puzzle));
    }

    /// <summary>
	/// 检查关卡是否完成
	/// </summary>
	private bool CheckStageComplete()
    {
        for (int i = 0; i < CurStageInfo.Puzzles.Count; i++)
        {
            if (!CurStageData.FoundTargetPuzzles.Contains(CurStageInfo.Puzzles[i]))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 字符串移动到组合词面板动画
    /// </summary>
    private void LettersToMovePuzzleTileAnim(string Puzzle, List<int[]> gridPositions)
    {
        List<PuzzleTile> gridLetterTiles = crossPuzzleGrid.GetPuzzleGridsAtPos(gridPositions);
        PuzzleTileItem puzzleTile = PuzzleTileBoard.GetPuzzleTile(Puzzle);

        // Sanity check
        if (Puzzle.Length == 0 || Puzzle.Length != gridLetterTiles.Count || Puzzle.Length != puzzleTile.currentPuzzle.Length)
        {
            Debug.LogError("[GridController] AnimateLettersToPuzzleList: The Puzzle length does not match!!");
            return;
        }

        // 如果字母对象池为空，则创建
        if (tileLetterTextPool == null)
        {
            CreateTileLetterTextPool(gridLetterTiles[0]);
        }
        
        //飞入动画
        for (int i = 0; i < gridPositions.Count; i++)
        {
            int leid = i;
            
            PuzzleTile gridLetterTile = gridLetterTiles[i];
            
            Text letterText = tileLetterTextPool.GetObject<Text>();    
            letterText.text = gridLetterTile.Letter.ToString();
            letterText.transform.position = gridLetterTile.TileView.transform.position;
            letterText.transform.localScale = Vector3.one;
            Vector2 toScale = puzzleTile.TextPuzzles[leid].transform.localScale;         
            letterText.transform.SetParent(transform, true);   
            float startDelay =0.6f;
           
            toScale *= puzzleTile.TextPuzzles[leid].fontSize / (float)letterText.fontSize;           

            letterText.transform.DOScaleZ(1f, 0.12f).OnComplete(() =>
            {

                // 修改后的曲线路径动画
                Vector3 startPos = letterText.transform.position;
                Vector3 endPos = puzzleTile.TextPuzzles[leid].transform.position;

                // 创建贝塞尔曲线控制点（在起点和终点之间添加偏移量）
                Vector3 controlPoint = CalculateCurveControlPoint(startPos, endPos);

                // 创建路径点数组（起点 -> 控制点 -> 终点）
                Vector3[] pathPoints = new Vector3[] {
                    controlPoint,
                    endPos
                };

                // 创建路径动画
                letterText.transform.DOPath(
                    pathPoints,          // 路径点数组
                    startDelay         // 动画持续时间
                )
                .SetEase(Ease.OutCubic);  // 设置缓动函数使结尾更平滑

                //letterText.transform.DOMove(puzzleTile.TextPuzzles[leid].transform.position, startDelay);               
            });           

            letterText.transform.DOScale(toScale, 0.5f).OnComplete(() =>
            {
                //显示到组成词面板
                PuzzleTileBoard.ShowPuzzleFound(Puzzle, leid,() => {
                    tileLetterTextPool.ReturnObjectToPool(letterText.GetComponent<PoolObject>()); 
                });
            });
        }
    }

    // 计算曲线控制点的辅助方法
    Vector3 CalculateCurveControlPoint(Vector3 start, Vector3 end)
    {
        // 计算起点和终点的中点
        Vector3 midPoint = (start + end) / 2f;

        // 计算垂直偏移方向（可根据需要调整）
        Vector3 direction = (end - start).normalized;
        Vector3 perpendicular = Vector3.Cross(direction, Vector3.left).normalized;

        // 计算偏移量（基于距离动态调整）
        float distance = Vector3.Distance(start, end);
        float offsetMagnitude = Mathf.Clamp(distance * 0.5f, 1f, 5f);

        // 添加随机偏移使每条路径略有不同
        float randomOffset = UnityEngine.Random.Range(-0.3f, 0.3f);

        // 返回最终控制点位置
        return midPoint + (perpendicular * offsetMagnitude) + (Vector3.left * randomOffset);
    }

    /// <summary>
    /// 创建字母块文本池。
    /// </summary>
    /// <param name="letterTile">Letter tile.</param>
    private void CreateTileLetterTextPool(PuzzleTile letterTile)
    {
        Text letterTileTextTemplate = Instantiate(letterTile.TileView._textDisplay);
        letterTileTextTemplate.name = "letter_tile";
        letterTileTextTemplate.gameObject.SetActive(false);
        letterTileTextTemplate.transform.SetParent(transform);
        
        Transform letterTransform = ObjectPool.CreatePoolContainer(transform, "Text_pool");
        
        tileLetterTextPool = new ObjectPool(letterTileTextTemplate.gameObject,letterTransform);
       
    }
    
    private void OnClickWordVocabulary()
    {
        StageHexController.Instance.IsEnterVocabulary = true;
        SystemManager.Instance.ShowPanel(PanelType.LevelWordScreen);
    }

    private bool CanUseTool(ToolInfo toolInfo)
    {
        if (toolInfo.cost <= GameDataManager.Instance.UserData.Gold)
        {                
            return true;
        }
        return false;
    }

    IEnumerator ResetTool()
    {
        yield return new WaitForSeconds(0.45f);
        ToolItemReset(true);
    }

    /// <summary>
    /// 使用重置工具
    /// </summary>
    public void ToolItemReset(bool isReset=false)
    {
        //LayerBtn.enabled = false;
        if (isReset)
        {
            //GamPuzzleBoard();
            return;
        }
        
        ToolInfo toolInfo = GameDataManager.Instance.UserData.toolInfo[101];
        
        if (toolInfo == null)
        {
            Debug.LogError("[GameManager] There is no hint with the given hint id: ");
            LayerBtn.enabled = true;
            return;
        }

        bool useCoins =false;

        if (toolInfo.count <= 0) 
        {
            if (CanUseTool(toolInfo))
            {
                //PopupManager.Instance.Show("not_enough_coins");
                useCoins = true;                    
            }
            else
            {
                MessageSystem.Instance.ShowTip("TipGoldInsufficient", false);
                //AdsManager.Instance.ShowRewardedPanel("item_gold");
                //SystemManager.Instance.ShowPanel(PanelType.RewardAdsScreen);
                LayerBtn.enabled = true;
                return;
            }
        }
        usetoolCount++;

        string Str = GetRandomTipsPuzzle();
        if (!string.IsNullOrEmpty(Str))
        {
            if (useCoins) 
            {
                GameDataManager.Instance.UserData.UpdateGold(-toolInfo.cost,false,true,"购买道具");
                GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Resettool, 1,"购买道具");
                GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Resettool, -1,"关卡内使用");
            }
            else
            {
                GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Resettool, -1, "关卡内使用");
                InitToolUI();
            }
            
            DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedUseTipAllWordTool,1);
            AudioManager.Instance.PlaySoundEffect("chongzhidaoju");
            
            CurStageData.AddPuzzleHints(Str);
            //int index= CurStageData.GetPuzzleHintCount(Str)+1;
            List<PuzzleTile> puzzleDatas = crossPuzzleGrid.GetPuzzleTileRowCol(Str);
            ShowLetterTips(puzzleDatas[0],LayerBtn.transform);
            //puzzleDatas[0].TileView.ShowTipPuzzle();          
        }  
        else
        {
            MessageSystem.Instance.ShowTip("TipAllWordPrompted");
        }             
    }
    
    /// <summary>
    /// 显示字母作为提示
    /// </summary>
    public void ShowLetterTips(PuzzleTile puzzleTile, Transform start = null)
    {
        if (puzzleTile == null)
        {
            Debug.Log(puzzleTile.Letter + "无法找到目标词块");
            return;
        }

        if (start != null)
        {
            Transform startTransform = puzzleTile.TileView.gameObject.transform;
            GameObject effect = AdvancedBundleLoader.SharedInstance.LoadGameObject("useritems", "ShowTipTuowei");
            CustomFlyInManager.Instance.FlyIn(start, startTransform, effect, () =>
            {
                puzzleTile.TileView.ShowTipPuzzle();       
            });
        }
        else
        {
            puzzleTile.TileView.ShowTipPuzzle();       
        }
    }

    private async void GamPuzzleBoard()
    {
        await PuzzleMatrixGenerator.Shared.RebuildPuzzleMatrixAsync(() =>
        {
            // 根据关卡数据生产字块矩阵
            crossPuzzleGrid.ResetPuzzles(true);
            UpdateSelectablePuzzles(); 
        });
        //yield return new WaitForSeconds(0.8f);
        LayerBtn.enabled = true;
    }

    private void OnClickPuzzleVocabulary()
    {
        StageHexController.Instance.IsEnterVocabulary = true;
        //UIManager.Instance.ShowPanel(PanelName.StagePuzzleScreen);
    }

    /// <summary>
    /// 使用提示工具
    /// </summary>
    public void UseTips()
    {
        ToolInfo toolInfo = GameDataManager.Instance.UserData.toolInfo[102];
        
        if (toolInfo == null)
        {
            Debug.LogError("[GameManager] There is no hint with the given hint id: ");
            return;
        }
        bool useCoins = false;

        if (toolInfo.count <= 0)
        {
            if (CanUseTool(toolInfo))
            {
                //PopupManager.Instance.Show("not_enough_coins");
                useCoins = true;                   
            }
            else
            {
                MessageSystem.Instance.ShowTip("TipGoldInsufficient", false);
                //AdsManager.Instance.ShowRewardedPanel("item_gold");
                //SystemManager.Instance.ShowPanel(PanelType.RewardAdsScreen);
                return;
            }
        }

        usetoolCount++;
        string Str = GetRandomTipsPuzzle();
        if (!string.IsNullOrEmpty(Str))
        {
            CurStageData.AddPuzzleHints(Str);
            //int index= CurStageData.GetPuzzleHintCount(Str)+1;
            List<PuzzleTile> puzzleDatas= crossPuzzleGrid.GetPuzzleTileRowCol(Str);

            StartCoroutine(ShowTipsPuzzle(puzzleDatas));
            
            //CurStageData.UpdateCharacterHint(Str,index);
            if (useCoins)
            {
                GameDataManager.Instance.UserData.UpdateGold(-toolInfo.cost,false,true,"购买道具");
                GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, 1,"道具购买");
                GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, -1,"关卡内使用");
                AudioManager.Instance.PlaySoundEffect("tishidaoju");
                DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedUseTipsTool,1);
            }
            else
            {
                GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool,-1,"关卡内使用");
                InitToolUI();
                AudioManager.Instance.PlaySoundEffect("tishidaoju");
                DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedUseTipsTool,1);
            }
        }
        else
        {
            MessageSystem.Instance.ShowTip("TipAllWordPrompted");
        }         
    }

    private IEnumerator ShowTipsPuzzle(List<PuzzleTile> puzzleDatas)
    {
        foreach (var item in puzzleDatas)
        {
            item.TileView.ShowTipPuzzle();
            yield return new WaitForSeconds(0.05f);
        }
    }
    
    private IEnumerator ShowNoDoTipsPuzzle(List<PuzzleTile> puzzleDatas)
    {
        foreach (var item in puzzleDatas)
        {
            item.TileView.ShowNoDoTipPuzzle();
            yield return new WaitForSeconds(0.05f);
        }
    }

    /// <summary>
    /// 未操作时显示提示
    /// </summary>
    public void ShowTipPuzzle()
    {
        string Str = GetRandomTipsPuzzle();
        if (!string.IsNullOrEmpty(Str))
        {
            //CurStageData.AddPuzzleHints(Str);
            StageHexController.Instance.tipPuzzle = Str;
            //int index= CurStageData.GetPuzzleHintCount(Str)+1;
            List<PuzzleTile> puzzleDatas= crossPuzzleGrid.GetPuzzleTileRowCol(Str);

            StartCoroutine(ShowNoDoTipsPuzzle(puzzleDatas));
            
        }
    }

    /// <summary>
    /// 从剩余可选词汇中随机选取一个未被完全提示的成语。
    /// </summary>
    /// <returns>未被完全提示的成语，如果没有则返回 null。</returns>
    private string GetRandomTipsPuzzle(bool isguide=false)
    {
        //从存储的提示数据中找到未显示完的成语进行提示(且是可选择成语)
        string str = CurStageData.FindFirstHintedPuzzle(selectablePuzzles);
        if (!string.IsNullOrEmpty(str))
        {
            return str;
        }

        str = GetSelectablePuzzle(isguide);
        return str;
    }

    /// <summary>
    /// 从剩余可选词汇中随机选取一个未被完全提示的成语。
    /// </summary>
    /// <returns>未被完全提示的成语，如果没有则返回 null。</returns>
    private string GetSelectablePuzzle(bool isguide=false)
    {
        // 从关卡组合成语中找到未显示的成语进行提示
        var availablePuzzles = selectablePuzzles.Where(Puzzle => !CurStageData.IsPuzzleFullyHinted(Puzzle)).ToList();
        if(CurStageData.PuzzleHints !=null)
            availablePuzzles = availablePuzzles.Where(Puzzle => !CurStageData.PuzzleHints.Contains(Puzzle)).ToList();
        
        if (availablePuzzles.Count > 0)
        {
            // 随机选择并返回
            int randomIndex = isguide?0: new Random().Next(availablePuzzles.Count);
            string str = availablePuzzles[randomIndex];
            return str;
        }
        return null; // 或者抛出异常
    }
   
    
    // 检测无操作的协程
    private IEnumerator CheckInactivity()
    {
        // 使用WaitForSecondsRealtime避免时间缩放影响
        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(0.5f);
    
        yield return new WaitForSeconds(4.5f);
        
        while (true)
        {
            float elapsed = Time.time - StageHexController.Instance.lastActivityTime;
        
            Debug.Log($"无操作时间:{elapsed} 提示词 {StageHexController.Instance.tipPuzzle}");
            
            // 检查是否超过阈值
            if (elapsed > inactivityThreshold&&GameDataManager.Instance.UserData.GetTutorialProgress()>=2
                                             &&string.IsNullOrEmpty(StageHexController.Instance.tipPuzzle))
            {
                ShowTipPuzzle(); // 直接调用UseTips方法
                // 可选: 触发后暂停检查一段时间，避免频繁调用
                yield return new WaitForSecondsRealtime(2f);
            }
        
            yield return wait;
        }
    }
   
    
    protected override void OnDisable()
    {
        base.OnDisable();
        firstenter = false;
        //StageOverObj.gameObject.SetActive(false);
        EventDispatcher.instance.OnLetterSelected -= OnLetterSelected;
        //EventManager.OnChangeLanguageUpdateUI -= InitUI;
        //EventManager.OnComboTriggerButterfly -=UseButterfly;
        EventDispatcher.instance.OnChoicePuzzleSetStatus -= ChoicePuzzleSetStatus;
        EventDispatcher.instance.OnCheckShowTutorial -= CheackShowTotrialEvent;
        EventDispatcher.instance.OnChangeGoldUI -= InitToolUI;
        Effect_Butterflys.Clear();
        StageOverObj.gameObject.SetActive(false);
        //StopCoroutine(CheckInactivity());
    }

}



