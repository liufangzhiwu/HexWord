using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Middleware;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class ChessPlayArea : UIWindow
{
    public static ChessPlayArea Instance;
    [SerializeField] private GameObject GameBase;
    [SerializeField] private Text Stagetxt;
    [SerializeField] public Button HitsBtn;      // 提示按钮
    [SerializeField] public Button CompleteBtn;     // 完成按钮
    [SerializeField] private Button PuzzleBtn;   //  关内词语按钮
    [SerializeField] public GameObject butterflyPrefab; // 蝴蝶特效
    [SerializeField] public GameObject butterflyObj;   // 蝴蝶节点
    [SerializeField] public Image effectMask;  //蒙版
    [Header("词语面板")]
    // 字块矩阵面板
    [SerializeField] public ChessboardGrid chessboardGrid;
    [HideInInspector]public (float row, float col) startLocation = (0, 0);
    // 待填入字块集面板
    [SerializeField] public ChessBowlGrid puzzleTileTable;
    [Header("关卡难度常驻标签")]
    [SerializeField] private GameObject _hardTagObj;      // “困难”标签节点
    [SerializeField] private GameObject _extraHardTagObj; // “极难”标签节点
    
    [Header("时间与控制 (新增)")]
    [HideInInspector] public Text _timerText;       // 倒计时文本显示 (由header赋值)
    
    [Header("禅意分数展示")]
    [SerializeField] private GameObject zentable;
    [SerializeField] private Text _zenScoreText;        // 总分数展示文本 (图里的 "188")
    [SerializeField] private Image _scoreBorder;        // 分数牌的边框 (用于闪红/闪黄)
    [SerializeField] private GameObject _floatingScoreOriginalPos;          // 记录飘字的初始坐标
    [SerializeField] private GameObject _lotusParticle; // 莲花图标周围的光点粒子节点
    [Header("分数 UI 对象池预制体")]
    private GameObject _rollingScorePrefab; // 拖入老虎机滚出的旧分数预制体
    private GameObject _floatingScorePrefab; // 拖入飘字预制体

    [Header("树叶进度控制")] 
    [SerializeField] private Slider leafSlider;     // 树叶进度条
    [SerializeField] private GameObject leafFlyPoint;  // 叶子飞入点
    [SerializeField] private GameObject leafGold;    // 叶子金币奖励图标
    [SerializeField] private GameObject leafPupa;    //  叶子蝶蛹奖励图标
    [SerializeField] private GameObject leafLotus;    // 叶子莲花奖励
    
    [Header("Combo 特效")]
    [SerializeField] private GameObject _comboScreenFX; // 拖入你挂在面板下的特效物体
    [Header("飞行特效预制体与对象池")]
    [SerializeField] private GameObject _pupaTrailPrefab;       // 飞向蝶蛹的拖尾粒子
    [SerializeField] private GameObject _zenCorrectTrailPrefab; // 飞向禅意分的黄色拖尾粒子
    [SerializeField] private GameObject _zenWrongTrailPrefab;   // 飞向禅意分的红色拖尾粒子
    [Header("卡关道具提醒 (新增)")]
    private bool _hasTriggeredHintReminderThisLevel = false; // 本关是否已经触发过特效
    private bool _isStuckTimerRunning = false;               // 卡关计时器是否在跑
    private float _stuckTimer = 0f;                          // 卡关计时(秒)
    // 假设这是你从配置表读取的数据，请替换为你实际的配置表读取代码，如 GameDataManager.Instance.Config.PropRemindTime
    private float PropRemindTime => 15f;
    
    private ObjectPool _pupaTrailPool;
    private ObjectPool _zenCorrectTrailPool;
    private ObjectPool _zenWrongTrailPool;
    private ObjectPool _rollingScorePool;
    private ObjectPool _floatingScorePool;
    // 🌟  改用 Dictionary 存储池子，支持多皮肤多特效
    private Dictionary<int, ObjectPool> _leafPoolDict = new Dictionary<int, ObjectPool>();
    [Header("横幅对象缓存池")]
    // 键为 styleNumber (1~4)，值为对应的横幅实例
    private Dictionary<int, GameObject> _bannerCachePool = new Dictionary<int, GameObject>();
    private GameObject _bannerLiziCache;   // 横幅的飞行粒子缓存
    
    public GameObject lightParticlePrefab; // 飞行的粒子/光效预制体
    private ObjectPool _lightParticlePool; // 青蛙跳和提示光效的对象池
    
    private int _lastZenScore = 0;                      // 记录上次的分数，用来计算差值
    
    #region 计时系统数据
    private float _remainingTime = 300f;            // 剩余时间 (初始 300 秒 = 5 分钟)
    private bool _isTimerRunning = false;           // 计时器开关
    private bool _isWarningTriggered = false;       // 是否已经触发过警告特效
    #endregion
    
    private int usetoolCount;     // 所有道具使用
    private int ComboErrorCount;  // 连续错误计数
    private int wordErrorCount;   // 总错误计数
    
    // 蝴蝶道具设置
    List<GameObject> EffectButterFlays = new List<GameObject>();
    List<ChessView> butterChess = new List<ChessView>();
    private int useButterflyCount;
    private bool firstenter;

    private GameObject _bottomLine;
    #region 数据相关
    private float _currentWordActiveSeconds = 0f;
    /// <summary>
    /// 单个词语消除使用时长
    /// </summary>
    private float wordUserSeconds;
    private HashSet<string> UsedPuzzles = new HashSet<string>(); //找出的词组
    
    
    #endregion
    // 当前关卡配置数据
    public ChessStageInfo CurrStageInfo
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
    [HideInInspector] public Vector3? ScoreFlyPos = null; // 🌟 接收指定的起飞坐标
   
    #region 生命周期
    protected override void Awake()
    {
        base.Awake();
        Instance = this;
    }

    protected override void InitializeUIComponents()
    {
        HitsBtn.AddClickAction(UseTips, "");
        CompleteBtn.AddClickAction(() => UseComplete(), "");
        PuzzleBtn.AddClickAction(ClickLevelPuzzle);
        BoardInitialize();
    }
    /// <summary>
    /// 棋盘初始化 (终极安全版)
    /// </summary>
    private void BoardInitialize()
    {
        // ==========================================
        // 1. 蝶蛹特效加载与池化
        // ==========================================
        if (_pupaTrailPrefab == null)
        {
            _pupaTrailPrefab = AssetBundleLoader.SharedInstance.LoadGameObject("useritems", "GreenTrailEffect");
        }
        if (_pupaTrailPrefab != null) 
        {
            _pupaTrailPool = new ObjectPool(_pupaTrailPrefab, ObjectPool.CreatePoolContainer(transform,"pupaTrailPool"), 3, PoolBehaviour.GameObject);
        }
        else 
        {
            Debug.LogError("🚨 AB包加载失败：在 useritems 中找不到 GreenTrailEffect！");
        }

        // ==========================================
        // 2. 正确加分特效加载与池化
        // ==========================================
        if (_zenCorrectTrailPrefab == null)
        {
            _zenCorrectTrailPrefab = AssetBundleLoader.SharedInstance.LoadGameObject("useritems", "huaEffect");
        }
        if (_zenCorrectTrailPrefab != null)
        {
            _zenCorrectTrailPool = new ObjectPool(_zenCorrectTrailPrefab, ObjectPool.CreatePoolContainer(transform,"zenCorrectTrailPool"), 3, PoolBehaviour.GameObject);
        }
        else 
        {
            Debug.LogError("🚨 AB包加载失败：在 useritems 中找不到 huaEffect！");
        }

        // ==========================================
        // 3. 错误扣分特效加载与池化
        // ==========================================
        if (_zenWrongTrailPrefab == null)
        {
            // 如果你有单独的红色特效，替换名字；没有的话可以复用，或者留空
            _zenWrongTrailPrefab = AssetBundleLoader.SharedInstance.LoadGameObject("useritems", "huaEffect"); 
        }
        if (_zenWrongTrailPrefab != null)
        {
            _zenWrongTrailPool = new ObjectPool(_zenWrongTrailPrefab, ObjectPool.CreatePoolContainer(transform,"zenWrongTrailPool"), 3, PoolBehaviour.GameObject);
        }
        else 
        {
            Debug.LogError("🚨 AB包加载失败：错误特效为空！");
        }
        // ==========================================
        // 4. 分数动画对象池初始化
        // ==========================================
        if (_rollingScorePrefab == null)
        {
            _rollingScorePrefab = AssetBundleLoader.SharedInstance.LoadGameObject("commonitem", "RollingScore"); 
        }
        if (_rollingScorePrefab != null) 
        {
            _rollingScorePool = new ObjectPool(_rollingScorePrefab, ObjectPool.CreatePoolContainer(transform, "RollingScorePool"), 3, PoolBehaviour.GameObject);
        }
        if (_floatingScorePrefab == null)
        {
            _floatingScorePrefab = AssetBundleLoader.SharedInstance.LoadGameObject("commonitem", "FloatingScore"); 
        }
        if (_floatingScorePrefab != null) 
        {
            _floatingScorePool = new ObjectPool(_floatingScorePrefab, ObjectPool.CreatePoolContainer(transform, "FloatingScorePool"), 3, PoolBehaviour.GameObject);
        }

        InitAllLeafPools();
        chessboardGrid.Initialize(this);
        puzzleTileTable.Initialize(this);
    }
    
    protected void Start()
    {
        lightParticlePrefab = AssetBundleLoader.SharedInstance.LoadGameObject("useritems", "ShowTipTuowei");
        _lightParticlePool = new ObjectPool(lightParticlePrefab, ObjectPool.CreatePoolContainer(transform, "LightParticlePool"), 4, PoolBehaviour.GameObject);
    }
    
    // 初始化所有池子（在 BoardInitialize 或 Start 中调用）
    private void InitAllLeafPools()
    {
        for (int i = 1; i <= 4; i++)
        {
            // 假设预制体名字分别是 LeafPrefab_1, LeafPrefab_2...
            GameObject prefab = AssetBundleLoader.SharedInstance.LoadGameObject("useritems", $"UIEffect_shuye0{i}");
            _leafPoolDict[i] = new ObjectPool(prefab, ObjectPool.CreatePoolContainer(transform, $"LeafPool_{i}"), 4, PoolBehaviour.GameObject);
        }
    }
    
    protected override void OnEnable()
    {
        base.OnEnable();
        PrepareForAnimation();
        UpdateUI();
        _remainingTime = CurrStageData.RemainingTime;
        GameCoreManager.Instance.PanelState = PanelState.GamePingPanel;
        EventDispatcher.instance.OnCheckShowChessTutorial += CheckShowChessTutorialEvent;
        EventDispatcher.instance.OnAutoPassLevel += AutoPassLevel;
        // 👇 新增：监听分数变化事件，并初始化当前分数
        EventDispatcher.instance.OnChessScoreChanged += OnChessScoreChanged;
        _lastZenScore = ChessStageController.Instance.CurrentTotalScore;
        _zenScoreText.text = _lastZenScore.ToString();
        
        StartCoroutine(SetupGameData());
        AudioManager.Instance.PlaySoundEffect("EnterLevel");
        
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
        UpdateTimerUI();
        ClearAndResetLeafSliderComponents();
        GameCoreManager.Instance.SetBackgroundImage(new Color(1,1,1,0.75f));
    }
    #endregion
    #region 计时与生命周期更新
    /// <summary>
    /// 🌟 核心拦截器：检查是否有阻挡计时的弹窗，或者广告正在播放
    /// </summary>
    private bool IsGamePausedByUI()
    {
        // 如果有任何遮挡屏幕的弹窗出现，时间停止！
        bool isPopShowing = 
            SystemManager.Instance.PanelIsShowing(PanelType.LevelWordScreen) ||   // 词典弹窗
            SystemManager.Instance.PanelIsShowing(PanelType.GetItemScreen) ||     // 道具购买弹窗
            SystemManager.Instance.PanelIsShowing(PanelType.PauseGameScreen) ||   // 暂停弹窗
            SystemManager.Instance.PanelIsShowing(PanelType.FailGameScreen) ||    // 失败弹窗
            SystemManager.Instance.PanelIsShowing(PanelType.ContinueGameWindow) ||// 重连弹窗
            SystemManager.Instance.PanelIsShowing(PanelType.RateUsScreen);           // 评分弹窗
            
        // 完美涵盖看广告期间！广告SDK只要处于播放状态，时间停止！
        bool isAdPlaying = Game.self.Ads != null && Game.self.Ads.IsPlaying;
        
        return isPopShowing || isAdPlaying;
    }
    private void Update()
    {
        // 防抖：限制单帧最大时间流逝为 0.5 秒，防止切后台回来瞬间蒸发大量时间
        float dt = Mathf.Min(Time.deltaTime, 0.5f);
        // ==========================================
        // 🌟 1. 绝对防御：如果弹窗打开或在看广告，冻结一切！
        // ==========================================
        if (IsGamePausedByUI()) 
        {
            // 为了保证看广告回来后连击条不断，在暂停期间不断将连击时间戳后移，完美抵消流逝的时间
            if (ChessStageController.Instance.PuzzleComboCount > 0)
            {
                ChessStageController.Instance.LastCorrectWordTimestamp += dt;
            }
            return; // 结束执行，倒计时绝对不走！
        }
        // 2. 常规的开关检查 只有在计时器运行，且时间大于0时才倒计时
        if (!_isTimerRunning || _remainingTime <= 0) return;
        
        // ==========================================
        // 🌟 3. 真实活跃时间累加
        // ==========================================
        _remainingTime -= dt;
        CurrStageData.RemainingTime = Mathf.Max(0, _remainingTime);
        
        CurrStageData.TotalActiveSeconds += dt; // 关卡总活跃时长 (上报用)
        _currentWordActiveSeconds += dt;        // 单个词寻找时长 (断连击用)
   
        // --- 新增：累加卡关等待时间 ---
        if (_isStuckTimerRunning && !_hasTriggeredHintReminderThisLevel)
        {
            _stuckTimer += dt;
        }
        
        // ==========================================
        // 4. 警告与超时处理
        // ==========================================
        if (_remainingTime <= 60f && !_isWarningTriggered)
        {
            TriggerTimeWarning();
        }
        if (_remainingTime <= 0f)
        {
            _remainingTime = 0f;
            _isTimerRunning = false;
            bool isActuallyWon = chessboardGrid != null && 
                                 (chessboardGrid.GameOver || 
                                  (chessboardGrid.GridList.Count > 0 && 
                                   chessboardGrid.GridList.Values.All(item => item.CurrState == TileState.Success || item.IsOK)));
            if(!isActuallyWon)
                HandleTimeOut();
        }      
        UpdateTimerUI();
        
        // 5. 连击进度条检查
        if (ChessStageController.Instance.PuzzleComboCount > 0)
        {
            ChessStageController.Instance.CheckAndResetComboOnIdle();
            float comboProgress = ChessStageController.Instance.GetComboTimeProgress();
            if (ChessStageController.Instance.PuzzleComboCount <= 0)
            {
                if (_comboScreenFX != null) _comboScreenFX.SetActive(false);
            }
        }
        if (ChessStageController.Instance.PuzzleComboCount <= 0)
        {
            if (_comboScreenFX != null && _comboScreenFX.activeSelf) 
            {
                _comboScreenFX.SetActive(false);
            }
        }
    }
    /// <summary>
    /// 🌟 玩家进行了任何有效操作（点击棋盘、词块、道具），尝试唤醒计时器
    /// </summary>
    public void NotifyPlayerInteraction()
    {
        // 1. 如果时间已经耗尽（GameOver状态），绝对不启动！防止狂点重跑
        if (_remainingTime <= 0f) return;
        
        // 2. 如果游戏已经结束，不启动
        if (chessboardGrid != null && chessboardGrid.GameOver) return;
        
        // 3. 如果当前有弹窗遮挡（如暂停、购买道具），不启动
        if (IsGamePausedByUI()) return;
        
        // 4. 如果已经在运行中，跳过
        if (_isTimerRunning) return;

        // 满足所有条件，正式启动计时！
        _isTimerRunning = true;
        // Debug.Log("🌟 玩家触发交互，倒计时正式开始！");
    }
    private void UpdateTimerUI()
    {
        if (_timerText == null) return;
        
        float safeTime = Mathf.Max(0, _remainingTime);
        int minutes = Mathf.FloorToInt(safeTime / 60F);
        int seconds = Mathf.FloorToInt(safeTime % 60f);
        _timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        
        bool hasLevelWords = ChessStageController.Instance.CurrStageData.FoundTargetPuzzles.Count > 0;
        // 只有当状态发生改变（从隐藏变成需要显示）时，才重新重置并播放动画
        if (hasLevelWords && !PuzzleBtn.gameObject.activeSelf)
        {
            PuzzleBtn.gameObject.SetActive(true);
            CanvasGroup pbcg = PuzzleBtn.GetComponent<CanvasGroup>();
            if (pbcg != null)
            {
                pbcg.DOKill();
                pbcg.alpha = 0f;
                pbcg.DOFade(1f, 0.5f);
            }
        }
        else if (!hasLevelWords && PuzzleBtn.gameObject.activeSelf)
        {
            // 从显示变成隐藏
            PuzzleBtn.GetComponent<CanvasGroup>()?.DOKill();
            PuzzleBtn.gameObject.SetActive(false);
        }
    }

    private void TriggerTimeWarning()
    {
        _isWarningTriggered = true;
        HeaderSection header = SystemManager.Instance.GetPanel(PanelType.HeaderSection) as HeaderSection;
        if (header != null)
        {
            header.ShowTimeWarning();
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
        // 1. 设置关卡文字
        Stagetxt.text = MultilingualManager.Instance.GetString("Level")+ " " + CurrStageInfo.StageNumber;
        // ==========================================
        // 🌟 2. 新增：判断并显示关卡难度常驻小标签
        // ==========================================
        // 先默认把两个标签都隐藏
        if (_hardTagObj != null) _hardTagObj.SetActive(false);
        if (_extraHardTagObj != null) _extraHardTagObj.SetActive(false);

        // 获取当前关卡的难度
        LevelModes currentMode = ChessStageController.Instance.GetLevelDifficultyMode(CurrStageData.StageId);
        
        // 根据难度开启对应的常驻小标签
        if (currentMode == LevelModes.Hard)
        {
             _hardTagObj.SetActive(true);
             _hardTagObj.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("Hard");
        }
        else if (currentMode == LevelModes.ExtraHard)
        {
             _extraHardTagObj.SetActive(true);
             _extraHardTagObj.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("VeryHard");
        }
        // ==========================================
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
        if (GameDataManager.Instance.UserData.toolInfo[104].count > 0)
        {
            compText.GetComponent<Text>().text = GameDataManager.Instance.UserData.toolInfo[104].count.ToString();
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
        if (GameDataManager.Instance.UserData.toolInfo[102].count > 0)
        {
            hintText.GetComponent<Text>().text = GameDataManager.Instance.UserData.toolInfo[102].count.ToString();
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
        _hasTriggeredHintReminderThisLevel = false;
        _isStuckTimerRunning = false;
        _stuckTimer = 0f;
        
        //清理一下棋盘
        chessboardGrid.Clear();
        puzzleTileTable.Clear();
        yield return new WaitForEndOfFrame();
        EventDispatcher.instance.TriggerChangeTopRaycast(false);
        
        
        IsClickAuto = false;
        // ==========================================
        // 🌟 修复关键 1：先执行你原版的缩放和尺寸适配逻辑
        // ==========================================
        RectTransform chessRectTransform = chessboardGrid.GetComponent<RectTransform>();
        RectTransform btnParent = HitsBtn.transform.parent.GetComponentInParent<RectTransform>();
        RectTransform bowlRectTransform = puzzleTileTable.GetComponent<RectTransform>();
        
        if (UIUtilities.IsiPad())
        {
            VerticalLayoutGroup vlg = chessboardGrid.transform.parent.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) { 
                vlg.childControlWidth = false; vlg.childForceExpandWidth = false; 
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
            else if(scale > 1f)
            {
                VerticalLayoutGroup vlg = chessboardGrid.transform.parent.GetComponent<VerticalLayoutGroup>();
                if (vlg != null) { 
                    vlg.childControlWidth = false; vlg.childForceExpandWidth = false;
                    vlg.childAlignment = TextAnchor.UpperCenter; // 保证胖手机上的1242容器整体居中
                }
                chessRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, UIUtilities.REFERENCE_WIDTH); 
                btnParent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, UIUtilities.REFERENCE_WIDTH); 
                bowlRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, UIUtilities.REFERENCE_WIDTH + 2); 
            }
        }
        // ==========================================
        // 🌟 修复关键 2：强制刷新布局，确保下面拿到的是设置后的真实宽度
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
        SystemManager.Instance.ShowPanel(PanelType.HeaderSection);
        // 协程在这里暂停，直到 isAnimFinished 变成 true 才往下走
        yield return new WaitUntil(() => isAnimFinished);
        _currentWordActiveSeconds = 0f;
        wordUserSeconds = 0;
        ResetTimeWarning();
        _remainingTime = CurrStageData.RemainingTime;
        _isTimerRunning = false;
        UpdateTimerUI();
       
        ChessStageController.Instance.CurLevelMode= ChessStageController.Instance.GetLevelDifficultyMode(CurrStageData.StageId);
        
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
        yield return null;
        // 检查一下是否存在错误的成功状态
        chessboardGrid.FixChessState();
        yield return new WaitForSeconds(0.2f);
        // 触发新手引导检查
        EventDispatcher.instance.TriggerCheckShowChessTutorial();
        AdRuleManager.Instance.TryShowBanner();
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
        // _isTimerRunning = true;
        // yield return new WaitUntil(()=>_isTimerRunning);
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
            bool hasGoldLeaf= false;
        
            foreach (var bowl in CurrStageData.Puzzles)
            {
                if (bowl.isGoldLeaf)
                {
                    hasGoldLeaf=true;
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
        }else if (CurrStageData.GoldLeafCount > 0)
        {
            Debug.Log(string.Format("{0} 关，金箔生成数量 {1}",CurrStageData.StageId, CurrStageData.GoldLeafCount));
        }
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
        // 👇 🌟 新增步骤：禅意分数面板 Q弹放大 (在时间轴 0.2秒 时触发)
        if (zentable != null)
        {
            enterSeq.Insert(0.2f, zentable.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack));
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
        if (_comboScreenFX != null) 
        {
            _comboScreenFX.SetActive(false);
        }
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
        if (zentable != null) DOTween.Kill(zentable.transform);
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
        if (zentable != null) zentable.transform.localScale = Vector3.zero;
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
            // ChessView targetChess = game.GetComponent<ChessView>();
            // if (targetChess.CurrState == TileState.Success)
            // {
            //     Debug.LogWarning("拦截死锁：目标字块已处于 Success 状态，放弃弹出撤回引导。");
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
        StartCoroutine(CheckShowChessTutorial());
    }
    private IEnumerator CheckShowChessTutorial()
    {
        yield return new WaitForSeconds(0.1f);

        if(CurrStageData.StageId == 1 && !GameDataManager.Instance.UserData.ChessTutorialProgress[1])
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
        if (CurrStageData.StageId == 2 && !GameDataManager.Instance.UserData.ChessTutorialProgress[4])
        {
            ChessGuideSystem.Instance.currentTutorial = 4;
            ChessGuideSystem.Instance.activeToolObject = HitsBtn.gameObject;
            ChessGuideSystem.Instance.toolSourceName = "UseTips";
            ChessGuideSystem.Instance.DisplayGuide();
        }else 
        if (CurrStageData.StageId == 6 && !GameDataManager.Instance.UserData.ChessTutorialProgress[5])
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
            if (ChessStageController.Instance.CheckIceMechanic(stageId, out bool isIceFirst, out _))
            {
                if (isIceFirst && (!GameDataManager.Instance.UserData.ChessTutorialProgress.ContainsKey(6) || !GameDataManager.Instance.UserData.ChessTutorialProgress[6]))
                {
                    Debug.Log("🌟 [引导注入] 正式弹窗展示冰块新手引导");
                    // 寻找棋盘上第一个被冰块冻住的格子，作为小手指向的目标
                    var allIceTiles = chessboardGrid.GridList.Values.Where(v => v.chesspiece.hasIce).ToList();
                    ChessGuideSystem.Instance.ChesspieceList = allIceTiles;
                    ChessGuideSystem.Instance.activeToolObject = null;
                    ChessGuideSystem.Instance.currentTutorial = 6;
                    ChessGuideSystem.Instance.toolSourceName = "IceTutorial";
                    ChessGuideSystem.Instance.DisplayGuide();
                    yield break; // 强行拦截一维时间轴，一次只弹一个引导
                }
            }

            // 花朵新手引导
            if (ChessStageController.Instance.CheckFlowerMechanic(stageId, out bool isFlowerFirst, out _))
            {
                if (isFlowerFirst && (!GameDataManager.Instance.UserData.ChessTutorialProgress.ContainsKey(7) || !GameDataManager.Instance.UserData.ChessTutorialProgress[7]))
                {
                    Debug.Log("🌟 [引导注入] 正式弹窗展示花朵新手引导");
                    var allFlowerTiles = chessboardGrid.GridList.Values.Where(v => v.chesspiece.hasFlower).ToList();
                    ChessGuideSystem.Instance.ChesspieceList = allFlowerTiles;
                    ChessGuideSystem.Instance.activeToolObject = null;
                    ChessGuideSystem.Instance.currentTutorial = 7;
                    ChessGuideSystem.Instance.toolSourceName = "FlowerTutorial";
                    ChessGuideSystem.Instance.DisplayGuide();
                    yield break;
                }
            }

            // 树叶新手引导
            if (ChessStageController.Instance.CheckLeafMechanic(stageId, out bool isLeafFirst))
            {
                if (isLeafFirst && (!GameDataManager.Instance.UserData.ChessTutorialProgress.ContainsKey(8) || !GameDataManager.Instance.UserData.ChessTutorialProgress[8]))
                {
                    Debug.Log("🌟 [引导注入] 正式弹窗展示树叶新手引导");
                    var allLeafTiles = chessboardGrid.GridList.Values.Where(v => v.chesspiece.hasLeaf).ToList();
                    ChessGuideSystem.Instance.ChesspieceList = allLeafTiles;
                    ChessGuideSystem.Instance.activeToolObject = null;
                    ChessGuideSystem.Instance.currentTutorial = 8;
                    ChessGuideSystem.Instance.toolSourceName = "LeafTutorial";
                    ChessGuideSystem.Instance.DisplayGuide();
                    yield break;
                }
            }
        }
    }

    // 添加找到的成语
    public void AddFoundPuzzle(string puzzle,float timeSpent = 0f, bool isFromTool = false)
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
        Debug.Log($"当前连击: {ChessStageController.Instance.PuzzleComboCount}, 获得总分: {curScore}, 理论最高分: {maxScore}, 进度比例: {progressRatio * 100}%");
        
        // =================================================================
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
            ComboErrorCount,ChessStageController.Instance.PuzzleComboCount,usetoolCount,energy);

        wordUserSeconds = 0;
        _currentWordActiveSeconds = 0f; // 🌟 上报完后，直接把秒表清零，准备找下一个字！
    }
    #region 🌟 过关与特效核心流水线重构
    /// <summary>
    /// 游戏结束
    /// </summary>
    public void GamePlayOver(bool isJump = false)
    {
        _isTimerRunning = false; // 👇 🌟 新增：通关了，立刻停止倒计时！
        // 🌟 修复关键：通关瞬间，立刻强制关闭连击特效！
        if (_comboScreenFX != null) 
        {
            _comboScreenFX.SetActive(false);
        }
        EventDispatcher.instance.TriggerChangeTopRaycast(false);
        if (HitsBtn != null) HitsBtn.interactable = false;
        if (CompleteBtn != null) CompleteBtn.interactable = false;
        PuzzleBtn.interactable = false;
        StartCoroutine(HandleStageCompletion(isJump));
        if (!isJump) ShowGoldLeafAnim();
    }

    private void ShowGoldLeafAnim()
    {
        foreach (var chessView in ChessStageController.Instance.GoldLeafChessViews)
        {
            chessView._bg.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("goldLeaf");
            chessView.FlyToThemeBtn(ThemeManager.Instance.themeButton,this.transform,null);
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
            bool isBannerFinished = false;
            // 4选1动态加载横幅预制体，内部带有 2.5 秒的展示时常生命周期
            StartCoroutine( PlayZenToCenterBannerFlow(() => isBannerFinished = true));
            // 耐心等待横幅播完或玩家点击关闭
            yield return new WaitUntil(() => isBannerFinished); 
        }
        else
        {
            // 🌟【时序安全修复】：如果确认为跳关，无缝闪过横幅展示期，不进行任何动态预制体实例化
            yield return null; 
        }
        HitsBtn.interactable = true;
        CompleteBtn.interactable = true;
        ChessStageController.Instance.CompleteStage(CurrStageInfo.StageNumber, wordErrorCount, isJump);
    }
     /// <summary>
    /// 动态加载并展示横幅，处理超过百分比文本
    /// </summary>
    private void ShowNewBannerEffect(Action onComplete)
    {
        if (effectMask != null)
        {
            effectMask.gameObject.SetActive(true);
            effectMask.raycastTarget = true;
            effectMask.color = new Color(0,0,0,0);
            effectMask.DOFade(0.6f, 0.2f);
        }

        // 1. 获取控制器刚才算好的数据
        var rule = ChessStageController.Instance.CurrentMatchedRule;
        int styleNumber = ChessStageController.Instance.CurrentBannerStyle; // 这应该是 1, 2, 3, 4
        float beyondPercent = ChessStageController.Instance.BeyondPercent;

        // 2. 动态加载对于的预制体 (假设预制体名字叫 UIEffect_Banner_1 等)
        GameObject activeBanner = null;
        if (_bannerCachePool.TryGetValue(styleNumber, out activeBanner) && activeBanner != null)
        {
            // 命中缓存！直接激活，无需再次消耗 CPU 去 Instantiate
            activeBanner.SetActive(true);
            activeBanner.transform.SetAsLastSibling();
            
            // 如果横幅内有依靠 OnEnable 触发的进场动画（比如 DOTweenAnimation 组件），
            // SetActive(true) 会自动重新播放它们。
        }
        else
        {
            // 🌟 2. 缓存没命中（玩家本次打开游戏后第一次抽到该样式），执行实例化并塞入缓存
            string prefabName = $"UIEffect_jiesuan0{styleNumber}"; 
            GameObject bannerPrefab = AssetBundleLoader.SharedInstance.LoadGameObject("useritems", prefabName);

            if (bannerPrefab != null)
            {
                activeBanner = Instantiate(bannerPrefab, this.transform);
                activeBanner.transform.SetAsLastSibling();
                
                // 存入缓存字典
                _bannerCachePool[styleNumber] = activeBanner;
            }
        }
            // 3. 赋值文本数据（请根据你预制体里的实际结构获取）
            if (activeBanner != null)
            {
                if (rule != null)
                {
                    // 例：获取激励主标题与副标题
                    string title = MultilingualManager.Instance.GetString(rule.TitleKey, "pingzi");
                    string desc = MultilingualManager.Instance.GetString(rule.LongTextKey, "pingzi");
                    // 你需要根据预制体里的具体层级找 Text 组件，这里假设存在
                    Text titleText = activeBanner.transform.Find("Text01")?.GetComponent<Text>();
                    
                    // 写入百分比 (这里处理 0% 防护，大厂通常会做个假的最低限度)
                    float displayPercent = beyondPercent <= 0 ? 88.5f : beyondPercent;
                    string percentStr = string.Format(desc, displayPercent);
                    Text percentText = activeBanner.transform.Find("Text02")?.GetComponent<Text>();
                    if (percentText != null)
                    {
                        percentText.text = percentStr;
                        
                    }
                    Debug.Log($"🌟 [横幅准备完毕] 标题: {title} | 鼓励词: {percentStr} | 超越: {displayPercent:F1}%");
                    if (rule.ScatterFlowers) MessageSystem.Instance.ShowTip($"{title} \n {percentStr} \n 撒花！");
                    if (titleText != null)
                    {
                        titleText.text = styleNumber == 4 ? percentStr : title;
                    }
                }
                // 4. 控制横幅的生命周期 (假设停留 2.5 秒后自动销毁进入下一环节，或者你给预制体上的按钮绑点击事件)
                DOVirtual.DelayedCall(2.5f, () => 
                {
                    activeBanner.SetActive(false);
                    if (effectMask != null) 
                    {
                        effectMask.DOFade(0f, 0.2f).OnComplete(() => effectMask.gameObject.SetActive(false));
                    }
                    onComplete?.Invoke(); // 触发回调，允许流水线继续走阶段三
                });
            }
        else
        {
            Debug.LogError($"🚨 横幅预制体实例化失败！缺少样式 {styleNumber}");
            // 兜底机制：就算预制体炸了，游戏也能正常玩下去
            if (effectMask != null) effectMask.gameObject.SetActive(false);
            onComplete?.Invoke(); 
        }
    }
    /// <summary>
    /// 🌟 新增：串联“禅意分位置起飞 ➔ 屏幕中心消失 ➔ 弹出常驻横幅”的视觉工作流
    /// </summary>
    private IEnumerator PlayZenToCenterBannerFlow(Action onComplete)
    {
        // 1. 动态索取或生成过关专用的飞行拖尾粒子
        if (_bannerLiziCache == null)
        {
            GameObject liziPrefab = AssetBundleLoader.SharedInstance.LoadGameObject("useritems", "SoftFireAdditiveRed");
            if (liziPrefab != null)
            {
                _bannerLiziCache = Instantiate(liziPrefab, this.transform);
            }
        }

        if (_bannerLiziCache != null)
        {
            _bannerLiziCache.SetActive(false);
            // 起点定位在禅意分数牌（zentable）上
            _bannerLiziCache.transform.position = zentable != null ? zentable.transform.position : transform.position;
            _bannerLiziCache.transform.SetAsLastSibling();
            _bannerLiziCache.SetActive(true);

            // 净化物理残留
            var trails = _bannerLiziCache.GetComponentsInChildren<TrailRenderer>(true);
            foreach (var t in trails) t.Clear();
            var pss = _bannerLiziCache.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var p in pss) { p.Clear(); p.Play(); }

            // 终点定位在屏幕中央（当前 PlayArea 容器的中心）
            Vector3 screenCenterPos = transform.position;

            bool isFlyDone = false;
            // 飞行动画：平滑向中心聚拢
            _bannerLiziCache.transform.DOMove(screenCenterPos, 0.65f).SetEase(Ease.OutQuad).OnComplete(() => {
                isFlyDone = true;
            });

            yield return new WaitUntil(() => isFlyDone);
            _bannerLiziCache.SetActive(false); // 抵达中心瞬间彻底消失
        }

        // 2. 粒子消失的刹那，原地无缝唤醒无蒙版高性能过关横幅
        bool isBannerFinished = false;
        ShowNewBannerEffect(() => isBannerFinished = true);
        yield return new WaitUntil(() => isBannerFinished);

        onComplete?.Invoke();
    }
    #endregion
    private void ClickLevelPuzzle()
    {
        ChessStageController.Instance.IsEnterVocabulary = true;
        SystemManager.Instance.ShowPanel(PanelType.LevelWordScreen);
    }
    public void UseComplete(bool isReset = false)
    {
        NotifyPlayerInteraction(); // 🌟 触发唤醒计时
        ToolInfo toolInfo = GameDataManager.Instance.UserData.toolInfo[104];

        if(toolInfo == null || chessboardGrid.GameOver)
        {
            // Debug.LogError("[GameManager] There is no hint with the given hint id: ");
            CompleteBtn.enabled = true;
            return;
        }

        bool useCoins = false;

        if(toolInfo.count <= 0)
        {
            GetItemScreen.limitRewordType = LimitRewordType.AutoComplete;
            // GetItemScreen.targetWord = GetCurrentSelectedPhrase(); // 🌟 赋值
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
            GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.AutoComplete, -1, "关卡内使用", GetCurrentSelectedPhrase());
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
        
        GameObject particle = _lightParticlePool.GetObject(transform);
        particle.transform.position = CompleteBtn.transform.position; 
        particle.transform.SetAsLastSibling();
        
        Vector3 startPos = particle.transform.position;
        Vector3 firstTargetPos = emptyTargets[0].transform.position;
        Vector3[] firstPath = CreateBezierPath(startPos, firstTargetPos, -0.3f);
        
        Sequence seq = DOTween.Sequence();
        
        // 1. 飞到第 1 个格子
        seq.Append(particle.transform.DOPath(firstPath, 0.4f, PathType.Linear).SetEase(Ease.InOutSine));
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
            seq.Append(particle.transform.DOJump(emptyTargets[currentIndex].transform.position, jumpHeight, 1, 0.2f).SetEase(Ease.Linear));
            
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
        _lightParticlePool.ReturnObjectToPool(particle.GetComponent<PoolObject>());
        // 2. 此时，最后一个格子的 PlayRevealAnimation 协程才刚刚被触发！
        // 你的协程逻辑是：等 0.2 秒 -> 弹文字缩放(0.3秒) -> 等 3.5 秒 -> 销毁。
        // 💡 为了最佳的爽快感：我们只等文字完美弹出来（0.2 + 0.3 = 0.5秒），就立刻变绿！
        // 千万不要等 3.5 秒特效全删了才变绿，那样玩家会觉得卡顿。背景残留着华丽的粒子时整句变绿，视觉冲击力最强！
        yield return new WaitForSeconds(0.35f);

        // 3. 时间刚刚好！通知游戏，播放整句变绿的成功波浪动画！
        onComplete?.Invoke();
    }
    
    /// <summary>
    /// 使用提示工具
    /// </summary>
    public void UseTips()
    {
        NotifyPlayerInteraction(); // 🌟 触发唤醒计时
        ToolInfo toolInfo = GameDataManager.Instance.UserData.toolInfo[102];

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
            GetItemScreen.limitRewordType = LimitRewordType.Tipstool;
            // GetItemScreen.targetWord = GetCurrentSelectedPhrase(); // 🌟 赋值
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
            GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, 1, "道具购买");
            GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, -1, "关卡内使用");
        }
        else
        {
            GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, -1, "关卡内使用",GetCurrentSelectedPhrase());
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
    /// <summary>
    /// 获取当前成语
    /// </summary>
    private string GetCurrentSelectedPhrase()
    {
        if (chessboardGrid.selecteTile == null) return "";

        // 获取当前格子所属的第一个词组（通常提示或自动完成针对的就是当前高亮的这组）
        var phraseGroup = chessboardGrid.GetCurrentSelectGroup2(); 
        if (phraseGroup == null || phraseGroup.Count == 0) return "";

        // 拼接成语
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (var view in phraseGroup)
        {
            sb.Append(view.Answer); // 注意：这里要用 Answer (正确答案)
        }
        return sb.ToString();
    }
    private IEnumerator FlyHintEffect(ChessView targetTile)
    {
        if (lightParticlePrefab == null) yield break;

        // 1. 在提示按钮的位置生成光效
        GameObject particle = _lightParticlePool.GetObject(transform);
        particle.transform.position = HitsBtn.transform.position;
        particle.transform.SetAsLastSibling(); // 放到最顶层
        // 🔥 解决“太小”的问题：初始设为0，瞬间放大到原来的 2.5倍 (倍数可根据你的预制体自己调)
        Vector3 targetScale = Vector3.one * 2.5f; 
        particle.transform.localScale = Vector3.zero;
        particle.transform.DOScale(targetScale, 0.2f).SetEase(Ease.OutBack);
        // 2. 计算动态距离和时长
        Vector3 startPos = particle.transform.position;
        Vector3 endPos = targetTile.transform.position;
        float duration = 0.5f;
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
        _lightParticlePool.ReturnObjectToPool(particle.GetComponent<PoolObject>());
        
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
        chessboardGrid.Clear();
        puzzleTileTable.Clear();
       
        if(EventDispatcher.instance != null)
        {
            // EventDispatcher.instance.OnChangeGoldUI -= InitToolUI;
            EventDispatcher.instance.OnCheckShowChessTutorial -= CheckShowChessTutorialEvent;
            EventDispatcher.instance.OnAutoPassLevel -= AutoPassLevel;
            EventDispatcher.instance.OnChessScoreChanged -= OnChessScoreChanged;
        }

        if (EffectButterFlays.Count > 0)
        {
            foreach (GameObject effect in EffectButterFlays)
            {
                if (effect != null) Destroy(effect);
            }
        }
        EffectButterFlays.Clear();
        butterflyObj.GetComponent<RectTransform>().DOAnchorPosX(-200, 0f);
        foreach (var banner in _bannerCachePool.Values)
        {
            if (banner != null) banner.SetActive(false);
        }
        if (_bannerLiziCache != null) _bannerLiziCache.SetActive(false); // 安全卸载粒子
        // CanvasScaler scaler = FindObjectOfType<Canvas>().GetComponent<CanvasScaler>();
        // scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.Expand;
        _isTimerRunning = false; // 隐藏界面时停止计时
        if (_remainingTime <= 0f)
        {
            ChessStageController.Instance.ClearCurrentLevelSave();
        }
        _timerText?.transform.DOKill(); // 清理警告动画
        if (_comboScreenFX != null) _comboScreenFX.SetActive(false);
        _zenScoreText.DOKill(true);
        _zenScoreText.rectTransform.DOKill(true);
        _zenScoreText.transform.localScale = Vector3.one;
        _scoreBorder.gameObject.SetActive(false);
        
        ClearAndResetLeafSliderComponents();
        GameCoreManager.Instance.SetBackgroundImage(Color.white);
        base.OnDisable();
    }
    // 有错误调用的
    public void AddWordError(ChessView tile, int tileErrorCount)
    {
        wordErrorCount ++;
        ComboErrorCount ++;
        
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
        
        bool wasPunished = ChessStageController.Instance.OnUpdateRewardPuzzle(false, tileErrorCount);
        if (wasPunished)
        {
            // 🌟 既然已经扣过分、飞过粒子了，这波错误就“翻篇”了，计数重置！
            chessboardGrid.TileErrorCounts[tile] = 0;
        }
        if (_comboScreenFX != null) 
        {
            _comboScreenFX.SetActive(false);
        }
    }
    
    /// <summary>
    /// 卡关后延迟2秒播放道具提醒动画
    /// </summary>
    private IEnumerator PlayHintReminderEffectDelay()
    {
        // 1. 等待 2 秒钟
        yield return new WaitForSeconds(2f);

        // 2. 确认游戏还没结束，且提示按钮在界面上是开启状态
        if (HitsBtn != null && HitsBtn.gameObject.activeInHierarchy && !chessboardGrid.GameOver)
        {
            // 方案A：使用 DOTween 做一个强烈的视觉提示 (如：Q弹呼吸放大)
            HitsBtn.transform.DOKill(true); // 杀掉旧动画防止冲突
            HitsBtn.transform.DOScale(1.2f, 0.25f)
                .SetLoops(6, LoopType.Yoyo)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => HitsBtn.transform.localScale = Vector3.one);

            // 方案B：如果你有专门的流光/高亮特效预制体，可以直接在这里实例化
            // GameObject hintFx = Instantiate(HintReminderPrefab, HitsBtn.transform);
            
            // 建议配合一个轻微的“叮”音效吸引注意力
            // AudioManager.Instance.PlaySoundEffect("HintShowEffect");
        }
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
    }
    /// <summary>
    /// 暂停/恢复 切换
    /// </summary>
    public void OnPauseClick()
    {
        _isTimerRunning = !_isTimerRunning;
        
        // TODO: 如果你有暂停面板，可以在这里弹出来
        SystemManager.Instance.ShowPanel(PanelType.PauseGameScreen);
    }
    /// <summary>
    /// 供【暂停弹窗】调用：玩家点击了“继续游戏”
    /// </summary>
    public void ResumeGame()
    {
        _isTimerRunning = true; // 恢复时间流逝
    }
    
    /// <summary>
    /// 时间耗尽：游戏失败
    /// </summary>
    private void HandleTimeOut()
    {
        _isTimerRunning = false; // 停住时间
        CurrStageData.RemainingTime = 0f;
        CurrStageData.SaveToFile();
        if (_timerText != null)
        {
            _timerText.transform.DOKill(); 
            _timerText.transform.localScale = Vector3.one; 
        }
        
        // 屏蔽屏幕操作
        EventDispatcher.instance.TriggerChangeTopRaycast(false);
        // TODO: 弹出你的失败结算/复活界面
        SystemManager.Instance.ShowPanel(PanelType.FailGameScreen);
    }
    /// <summary>
    /// 复活接口：看完广告后调用此方法重新开始
    /// </summary>
    /// <param name="addSeconds">复活额外给的秒数，默认给 60 秒</param>
    public void ReviveGame(float addSeconds = 60f)
    {
        _remainingTime += addSeconds;
        CurrStageData.RemainingTime = _remainingTime;
        _isWarningTriggered = false;
        
        // 恢复 UI 状态
        _timerText.color = Color.white; 
        _timerText.transform.DOKill(); 
        _timerText.transform.localScale = Vector3.one; 
        Outline outline = _timerText.GetComponent<Outline>();
        if (outline != null) outline.effectColor = new Color(0, 0, 0, 0.5f); // 假设你原本的描边是半透明黑色，按需修改
        
        ResetTimeWarning();
        UpdateTimerUI();
        _isTimerRunning = true; // 重新跑秒
        EventDispatcher.instance.TriggerChangeTopRaycast(true); // 解除屏幕屏蔽
    }
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
    /// 重置时间警告特效（文字变白、取消描边变红、停止心跳动画）
    /// </summary>
    private void ResetTimeWarning()
    {
        _isWarningTriggered = false;
        HeaderSection header = SystemManager.Instance.GetPanel(PanelType.HeaderSection) as HeaderSection;
        if (header != null)
        {
            header.ResetTimeWarning();
        }
    }
    
    /// <summary>
    /// 监听到分数变化时的处理逻辑
    /// </summary>
   private void OnChessScoreChanged(int newScore,int scoreDiff)
    {
        // int scoreDiff = newScore - _lastZenScore;
        
        // 分数没变就不播动画
        if (scoreDiff == 0)
        {
            // 如果是因为发呆断连击进来的，顺手把连击特效关掉
            if (_comboScreenFX != null && ChessStageController.Instance.PuzzleComboCount <= 0)
                _comboScreenFX.SetActive(false);
            return;
        }
        
        // 状态判定
        bool isDeduction = scoreDiff < 0;
        // 记录一下分数，防止连续触发时算错差值
        _lastZenScore = newScore;
        // 起飞点：屏幕中下方，或者用填词盘的位置
        Vector3 startPos;
        if (ScoreFlyPos.HasValue)
        {
            startPos = ScoreFlyPos.Value;
            ScoreFlyPos = null; // 用完立刻清空，防止影响下一次
        }
        else
        {
            startPos = chessboardGrid.selecteTile != null ? 
                chessboardGrid.selecteTile.transform.position : 
                chessboardGrid.transform.position; 
        }
        // 1. 发射禅意分粒子 (对/错)
        FlyToZenScore(startPos, newScore, scoreDiff, isDeduction);
        // 2. 如果是加分，发射蝶蛹粒子
        if (!isDeduction)
        {
            FlyToPupa(startPos, newScore);
        }
    }
    
    /// <summary>
    /// 飞向禅意分 UI
    /// </summary>
    private void FlyToZenScore(Vector3 startPos, int targetScore, int scoreDiff, bool isDeduction)
    {
        ObjectPool pool = isDeduction ? _zenWrongTrailPool : _zenCorrectTrailPool;
        GameObject particle = pool.GetObject();
        if (particle == null) 
        {
            // 如果没配粒子，直接执行 UI 更新兜底
            UpdateZenScoreUI(targetScore, scoreDiff, isDeduction);
            return;
        }
        particle.SetActive(false);
        particle.transform.position = startPos;
        // 强制清除拖尾和粒子的历史轨迹
        TrailRenderer[] trails = particle.GetComponentsInChildren<TrailRenderer>(true);
        foreach (var t in trails) t.Clear();
        ParticleSystem[] pss = particle.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var p in pss) p.Clear();
        particle.transform.SetAsLastSibling();
        particle.SetActive(true);
        
        Vector3 endPos = _zenScoreText.transform.position;
        
        // ==========================================
        // 🌟 核心动态时间计算：距离 ÷ 速度 = 时间
        // ==========================================
        float distance = Vector3.Distance(startPos, endPos);
        // 限制最快不低于 0.4 秒，最慢不超过 1.2 秒
        float duration = Mathf.Clamp(distance * 0.5f, 1.4f, 2.2f); 

        Vector3 midPos = (startPos + endPos) / 2f;
        midPos.x += distance * 0.3f; // 向左侧弯曲弧线

        Vector3[] path = new Vector3[] { startPos, midPos, endPos };

        // 飞行 0.6 秒
        particle.transform.DOPath(path, duration, PathType.CatmullRom).SetEase(Ease.InQuad).OnComplete(() => 
        {
            pool.ReturnObjectToPool(particle.GetComponent<PoolObject>());
            // 🌟 命中！执行禅意分的UI跳动和飘字特效
            UpdateZenScoreUI(targetScore, scoreDiff, isDeduction);
        });
    }
    
    /// <summary>
    /// 飞向顶部 Header 的蝶蛹 UI
    /// </summary>
    private void FlyToPupa(Vector3 startPos, int targetScore)
    {
        HeaderSection header = SystemManager.Instance.GetPanel(PanelType.HeaderSection) as HeaderSection;
        if (header == null || !header.pupaObj.activeSelf) return; // 蝶蛹没开启就不飞

        GameObject particle = _pupaTrailPool.GetObject();
        if (particle == null)
        {
            header.UpdatePupaProgress(targetScore, false); // 兜底
            return;
        }
        particle.SetActive(false);
        particle.transform.position = startPos;
        TrailRenderer[] trails = particle.GetComponentsInChildren<TrailRenderer>(true);
        foreach (var t in trails) t.Clear();
        ParticleSystem[] pss = particle.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var p in pss) p.Clear();
        particle.transform.SetAsLastSibling();
        particle.SetActive(true);

        Vector3 endPos = header.pupaProgressBar.transform.position;
        // ==========================================
        // 🌟 核心动态时间计算：距离 ÷ 速度 = 时间
        // ==========================================
        float distance = Vector3.Distance(startPos, endPos);
        float duration = Mathf.Clamp(distance*0.5f, 1.4f, 2.2f);
        
        Vector3 midPos = (startPos + endPos) / 2f;
        midPos.x -= distance * 0.3f; // 向右侧弯曲弧线，和禅意分岔开

        Vector3[] path = new Vector3[] { startPos, midPos, endPos };

        particle.transform.DOPath(path, duration, PathType.CatmullRom).SetEase(Ease.InQuad).OnComplete(() => 
        {
            _pupaTrailPool.ReturnObjectToPool(particle.GetComponent<PoolObject>());
            // 🌟 命中！通知头部 UI 更新蝶蛹进度！
            header.UpdatePupaProgress(targetScore, false);
        });
    }
    
    /// <summary>
    /// 粒子命中后，执行原本的 UI 跳动和爆点特效
    /// </summary>
   /// <summary>
    /// 粒子命中后，执行UI的老虎机滚动替换和爆点特效
    /// </summary>
    private void UpdateZenScoreUI(int newScore, int scoreDiff, bool isDeduction)
    {
        bool isCombo = !isDeduction && ChessStageController.Instance.PuzzleComboCount >= 2;

        // 🌟 1. 强制归位并停止主角(分数文本)之前的动画，防止快速连击导致位置偏移
        _zenScoreText.DOKill(true);
        _zenScoreText.rectTransform.DOKill(true);
        _zenScoreText.transform.localScale = Vector3.one;

        Vector2 centerPos = _zenScoreText.rectTransform.anchoredPosition;
        
        // 设定滚动方向：加分向上顶(正50)，减分向下砸(负50)
        float offset = isDeduction ? -50f : 50f; 

        // ==========================================
        // 🌟 核心：老虎机式滚动替换效果
        // ==========================================
        // ① 克隆旧分数作为“替身”，让它滚出屏幕
        GameObject oldScoreObj = _rollingScorePool.GetObject();
        oldScoreObj.transform.SetParent(_zenScoreText.transform.parent, false);
        oldScoreObj.transform.SetAsFirstSibling(); // 放到最底层，不遮挡新分数
        Text oldScoreText = oldScoreObj.GetComponent<Text>();
        RectTransform oldScoreRT = oldScoreObj.GetComponent<RectTransform>();
        oldScoreText.text = _zenScoreText.text;
        oldScoreRT.anchoredPosition = centerPos;
        oldScoreText.color = _zenScoreText.color;
        // 替身滚出并渐隐 (Ease.InBack 带有往回蓄力一下再冲出去的物理感)
        oldScoreRT.DOAnchorPosY(centerPos.y + offset, 0.4f).SetEase(Ease.InBack);
        oldScoreText.DOFade(0f, 0.4f).OnComplete(() =>
        {
            oldScoreRT.DOKill();
            oldScoreText.DOKill();
            _rollingScorePool.ReturnObjectToPool(oldScoreObj.GetComponent<PoolObject>());
        }); // 滚完立刻销毁

        // ② 将主角(真文本)直接设为新分数，并把它拉到屏幕外准备“进场”
        _zenScoreText.text = newScore.ToString();
        _zenScoreText.rectTransform.anchoredPosition = new Vector2(centerPos.x, centerPos.y - offset);
        
        // 初始透明度设为 0
        Color startColor = _zenScoreText.color; 
        startColor.a = 0f; 
        _zenScoreText.color = startColor;

        // 主角滚入中心并渐显 (Ease.OutBack 带有越过中心点再弹回来的Q弹感)
        _zenScoreText.rectTransform.DOAnchorPosY(centerPos.y, 0.5f).SetEase(Ease.OutBack);
        _zenScoreText.DOFade(1f, 0.4f);
        
        // ==========================================
        // 以下保持原状：边框、莲花特效、飘字等
        // ==========================================
        // 边框闪烁
        _scoreBorder.DOKill(); 
        // 只有扣分 (isDeduction) 或 连击 (isCombo) 时，才需要边框闪烁
        if (isDeduction || isCombo)
        {
            _scoreBorder.gameObject.SetActive(true); // 🌟 1. 播放前：强行开启节点
            
            Color borderColor = isDeduction ? Color.red : Color.yellow;
            borderColor.a = 1f;
            _scoreBorder.color = borderColor;
            
            // 🌟 2. 播放后：利用 OnComplete 在动画播完的瞬间彻底关闭节点
            _scoreBorder.DOFade(0f, 0.6f).SetEase(Ease.OutQuad).OnComplete(() => 
            {
                _scoreBorder.gameObject.SetActive(false); 
            });
            
            // 命中爆开莲花粒子
            // _lotusParticle.SetActive(false);
            // _lotusParticle.SetActive(true); 
            // ParticleSystem ps = _lotusParticle.GetComponent<ParticleSystem>();
            // if (ps != null) ps.Play();
        }
        else 
        {
            // 普通加分不需要闪烁，直接确保它处于关闭状态
            _scoreBorder.gameObject.SetActive(false); 
        }
        
        // 飘字动画 去除再展示飘字
        // GameObject floatObj = _floatingScorePool.GetObject();
        // floatObj.transform.SetParent(_floatingScoreOriginalPos.transform, false);
        // floatObj.transform.SetAsLastSibling();
        // floatObj.transform.localScale = Vector3.one;
        // Text floatText = floatObj.GetComponent<Text>();
        // CanvasGroup floatCG = floatObj.GetComponent<CanvasGroup>();
        // if (floatCG == null) floatCG = floatObj.AddComponent<CanvasGroup>();
        // // 清理旧状态
        // floatCG.DOKill(false);
        // floatText.rectTransform.DOKill(false);
        // bool enableGradient = !(isDeduction || isCombo);
        // var meshEffects = floatObj.GetComponents<UnityEngine.UI.BaseMeshEffect>();
        // foreach (var effect in meshEffects)
        // {
        //     if (effect.GetType().Name.Contains("Gradient")) effect.enabled = enableGradient;
        // }
        //
        // // 🌟 5. 获取纯色目标颜色
        // Color targetColor = Color.white;
        // if (isDeduction) targetColor = Color.red; // 扣分：红色
        // else if (isCombo) targetColor = new Color(0.6f, 1f, 0f); // 连击：黄绿色
        //
        // // 设定起始 Alpha = 0，位置复位，内容更新
        // floatText.color = new Color(targetColor.r, targetColor.g, targetColor.b, 1f);
        // floatText.rectTransform.anchoredPosition = Vector2.zero;
        // floatText.text = scoreDiff > 0 ? $"+{scoreDiff}" : scoreDiff.ToString();
        // floatText.SetAllDirty();
        // floatCG.alpha = 0f;
        // Sequence floatSeq = DOTween.Sequence();
        // floatSeq.SetTarget(floatObj);
        // // 相对移动 + 淡入淡出
        // float randomX = UnityEngine.Random.Range(-5f, 5f);
        // floatSeq.Join(floatText.rectTransform.DOAnchorPos(new Vector2( randomX,  60f), 1.2f).SetRelative(true).SetEase(Ease.OutQuad));
        // floatSeq.Join(floatCG.DOFade(1f, 0.2f));
        // floatSeq.Insert(0.8f, floatCG.DOFade(0f, 0.4f));
        // floatSeq.OnComplete(() =>
        // {
        //     _floatingScorePool.ReturnObjectToPool(floatObj.GetComponent<PoolObject>());
        // });
        
        _comboScreenFX.SetActive(isCombo);
    }
    /// <summary>
    /// 给指定区域拍个快照
    /// </summary>
    /// <param name="targetRect">想要截取的区域（通常是包含背景和棋盘的父节点）</param>
    /// <param name="onCaptured">截取完成后的回调，返回生成的 Sprite</param>
    private IEnumerator CaptureBoardSnapshot(RectTransform targetRect, Action<Sprite> onCaptured)
    {
        // 1. 临时隐藏你不想出现在快照里的东西（比如底部的字盘、按钮等）
        // puzzleTileTable.gameObject.SetActive(false);
        // HitsBtn.gameObject.SetActive(false);
        // CompleteBtn.gameObject.SetActive(false);
        // 如果有顶部的分数栏，也可以在这里 SetActive(false)

        // 2. 🌟 关键：必须等到这一帧所有的 UI 都渲染完毕，才能开始截图
        yield return new WaitForEndOfFrame();

        // 3. 计算想要截图的区域在屏幕上的坐标和大小
        Vector3[] corners = new Vector3[4];
        targetRect.GetWorldCorners(corners);
        
        // 判断 Canvas 的渲染模式
        Canvas canvas = targetRect.GetComponentInParent<Canvas>();
        Camera cam = (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;
        
        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);

        int width = Mathf.FloorToInt(topRight.x - bottomLeft.x);
        int height = Mathf.FloorToInt(topRight.y - bottomLeft.y);
        Rect rect = new Rect(bottomLeft.x, bottomLeft.y, width, height);

        // 4. 读取屏幕像素并生成贴图
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(rect, 0, 0);
        tex.Apply();

        // 5. 将 Texture2D 转换为 Sprite
        Sprite snapshotSprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));

        // 6. 恢复刚才隐藏的 UI
        // puzzleTileTable.gameObject.SetActive(true);
        // HitsBtn.gameObject.SetActive(true);
        // CompleteBtn.gameObject.SetActive(true);

        // 7. 返回结果
        onCaptured?.Invoke(snapshotSprite);
    }
    
    /// <summary>
    /// 🌟 规范 API：供外部调用的棋盘飘字方法
    /// </summary>
    public void ShowBoardFloatingScore(Transform targetTile, int dir, int scoreDiff, bool isCombo)
    {
        // 1. 生成在 this.transform (PlayArea) 下，保证层级在最顶端，不会被棋盘格子遮挡
        GameObject floatObj = _floatingScorePool.GetObject(this.transform); 
        floatObj.transform.SetAsLastSibling();
        
        Text floatText = floatObj.GetComponent<Text>();
        CanvasGroup floatCG = floatObj.GetComponent<CanvasGroup>();
        if (floatCG == null) floatCG = floatObj.AddComponent<CanvasGroup>();
        
        // 2. 杀掉旧动画，重置 Scale (极其重要，防止变小)
        floatCG.DOKill(false);
        floatText.rectTransform.DOKill(false);
        floatObj.transform.DOKill(false);
        floatObj.transform.localScale = Vector3.one;
        
        // 3. 设置初始位置为目标格子的世界坐标
        floatObj.transform.position = targetTile.position;
        
        // 4. 根据方向，给初始位置加上偏移量，让字从格子的上方/右方出现，而不是中心
        RectTransform floatRT = floatObj.GetComponent<RectTransform>();
        Vector2 flyDir;
        if (dir == 1) // 横向词 -> 飘字在上方
        {
            floatRT.anchoredPosition += new Vector2(0, 60f); // 初始位置向上偏移
            flyDir = new Vector2(UnityEngine.Random.Range(-15f, 15f), 100f); // 继续向上飘
        }
        else // 纵向词 -> 飘字在右方
        {
            floatRT.anchoredPosition += new Vector2(60f, 0); // 初始位置向右偏移
            flyDir = new Vector2(100f, UnityEngine.Random.Range(-15f, 15f)); // 继续向右飘
        }

        // 5. 设置颜色和文字
        bool enableGradient = !isCombo;
        var meshEffects = floatObj.GetComponents<UnityEngine.UI.BaseMeshEffect>();
        foreach (var effect in meshEffects)
        {
            if (effect.GetType().Name.Contains("Gradient")) effect.enabled = enableGradient;
        }
        
        Color targetColor = isCombo ? new Color(0.6f, 1f, 0f) : Color.white; 
        floatText.color = new Color(targetColor.r, targetColor.g, targetColor.b, 1f);
        floatText.text = $"+{scoreDiff}";
        floatText.SetAllDirty();
        floatCG.alpha = 0f;
        
        // 6. 执行动画
        Sequence floatSeq = DOTween.Sequence();
        floatSeq.SetTarget(floatObj);
        
        // 先稍微缩小一点作为起点，实现Q弹放大的效果
        floatObj.transform.localScale = Vector3.one * 0.5f; 
        
        // 弹出：瞬间放大并显现
        floatSeq.Append(floatObj.transform.DOScale(1.2f, 0.2f).SetEase(Ease.OutBack));
        floatSeq.Join(floatCG.DOFade(1f, 0.2f));
        
        // 飞行：沿着计算好的方向飘动
        floatSeq.Join(floatText.rectTransform.DOAnchorPos(flyDir, 1.2f).SetRelative(true).SetEase(Ease.OutQuad));
        
        // 消失：淡出，并且恢复到标准大小 (不设为0.5，防止影响下一次)
        floatSeq.Insert(0.8f, floatCG.DOFade(0f, 0.4f));
        floatSeq.Insert(0.8f, floatObj.transform.DOScale(1f, 0.4f));
        
        floatSeq.OnComplete(() =>
        {
            _floatingScorePool.ReturnObjectToPool(floatObj.GetComponent<PoolObject>());
        });
    }
    
    /// <summary>
    /// 🌟 新增：处理成功消除时，树叶划过贝塞尔弧线飞向收集点的华丽动效
    /// </summary>
    /// <param name="startTransform">起飞的格子 Transform</param>
    public void PlayLeafFlyToCollectionPoint(Transform startTransform)
    {
        if (leafFlyPoint == null || startTransform == null) return;
        // 获取当前应该用的皮肤索引
        int skinIndex = (ChessStageController.Instance.LeafGenCounter % 4) + 1;
        // 从对应的池子里拿对应的预制体
        GameObject flyLeaf = _leafPoolDict[skinIndex].GetObject(transform);
        flyLeaf.SetActive(true);
        flyLeaf.transform.position = startTransform.position;
        flyLeaf.transform.localScale = Vector3.one;
        
        flyLeaf.SetActive(true);
        // 强制移除克隆体身上的呼吸动画组件，防止飞行时乱抖
        flyLeaf.transform.DOKill();
        
        Vector3 startPos = flyLeaf.transform.position;
        Vector3 endPos = leafFlyPoint.transform.position;
        // 1. 恒定速度控制
        float speed = 800f; // 像素/秒，可根据美术效果调整
        float distance = Vector3.Distance(startPos, endPos);
        float duration = Mathf.Clamp(distance / speed, 1.4f, 2.0f); // 最小/最大时间限制
        
        // 0.3f 代表向右上方鼓起一个优雅的弧度
        Vector3[] pathPoints = CreateBezierPath(startPos, endPos, 0.35f, 12); 

        // 3. 编排飞行交响乐
        Sequence flySeq = DOTween.Sequence();
        // 沿着贝塞尔曲线飞过去
        flySeq.Append(flyLeaf.transform.DOPath(pathPoints, duration, PathType.Linear).SetEase(Ease.InQuad));
        // 飞行时带有一点树叶飘落的自然自转
        flySeq.Join(flyLeaf.transform.DORotate(new Vector3(0, 0, 360f), 0.75f, RotateMode.FastBeyond360).SetEase(Ease.Linear));
        // 快接近终点时慢慢缩小钻进去
        // flySeq.Insert(0.55f, flyLeaf.transform.DOScale(Vector3.zero, 0.2f));
        flySeq.AppendCallback(() => {
            // 到达终点后，播放一个小动画再回收
            Sequence landSeq = DOTween.Sequence();
            landSeq.Append(flyLeaf.transform.DOScale(1.3f, 0.1f).SetEase(Ease.OutQuad)); // 轻微弹起
            landSeq.Append(flyLeaf.transform.DOScale(0f, 0.25f).SetEase(Ease.InBack));   // 缩小消失
            landSeq.OnComplete(() => {
                _leafPoolDict[skinIndex].ReturnObjectToPool(flyLeaf.GetComponent<PoolObject>());
                // 进度条更新逻辑保持不变
                leafSlider.transform.DOKill();
                leafSlider.transform.DOScale(new Vector3(1.05f, 1.15f, 1f), 0.1f).SetLoops(2, LoopType.Yoyo);
                int curCollected = ChessStageController.Instance.CurrStageData.CollectedLeaves;
                leafSlider.value = curCollected;
                TriggerRewardNodeFeedback(curCollected);
            });
        });
    }
    
    /// <summary>
    /// 阶段点果冻爆点核心驱动器
    /// </summary>
    private void TriggerRewardNodeFeedback(int currentCount)
         {
        GameObject targetNode = null;
        int zenBonus = 0;

        if (currentCount == 2) targetNode = leafGold;
        else if (currentCount == 5) targetNode = leafPupa;
        else if (currentCount >= 10)
        {
            targetNode = leafLotus;
            zenBonus = 50; // 莲花大满贯给予50禅意分
        }

        if (targetNode == null) return;

        // ① 大厂标配 Q 弹缓动：通过 Punch 产生极强的肉感和果冻敲击感
        targetNode.transform.DOKill(true);
        targetNode.transform.SetAsLastSibling(); // 提层防遮挡
        targetNode.transform.DOPunchScale(new Vector3(0.45f, 0.45f, 0f), 0.55f, 12, 0.5f);

        // ② 子物体粒子爆发
        var pss = targetNode.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in pss)
        {
            ps.gameObject.SetActive(true);
            ps.Clear(true);
            ps.Play(true);
        }

        // ③ 禅意分平滑发放
        if (zenBonus > 0)
        {
            int nextScore = ChessStageController.Instance.CurrentTotalScore + zenBonus;
            // 呼叫你的分数变动机制，飞出黄色能量拖尾线
            OnChessScoreChanged(nextScore, zenBonus);
        }
        
        // AudioManager.Instance.PlaySoundEffect("RewardNode_Unlock");
    }
    
    /// <summary>
    /// 🌟 规范重构：全面加强出入场清理（完美解决走光、非法状态与内存留存）
    /// </summary>
    private void ClearAndResetLeafSliderComponents()
    {
        bool isLeafLevel = ChessStageController.Instance.CheckLeafMechanic(ChessStageController.Instance.CurrentStage, out _);
        
        if (leafSlider != null)
        {
            leafSlider.transform.parent.gameObject.SetActive(isLeafLevel);
            if (!isLeafLevel) return;
            
            leafSlider.transform.DOKill();
            leafSlider.transform.localScale = Vector3.one;
            leafSlider.value = ChessStageController.Instance.CurrStageData.CollectedLeaves;
            if (ChessStageController.Instance.CurrStageInfo != null)
            {
                leafSlider.maxValue = ChessStageController.Instance.CurrStageInfo.PhraseGroups.Count;
            }
            else
            {
                // 最大值动态同步关卡成语总数
                leafSlider.maxValue = 10; // 安全兜底
            }
        }
        Image leafImg = leafFlyPoint.transform.GetChild(0).GetComponent<Image>();
        if (leafImg != null)
        {
            int skinIndex = (ChessStageController.Instance.LeafGenCounter % 4) + 1; // 1, 2, 3 循环
            // 从你的图集Atlas或AssetBundleLoader中加载对应的叶子切图
            leafImg.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas($"leaf_skin_0{skinIndex}");
        }
        // 强行规制三个节点至标准状态
        GameObject[] rewardNodes = { leafGold, leafPupa, leafLotus };
        foreach (var node in rewardNodes)
        {
            if (node != null)
            {
                node.transform.DOKill();
                node.transform.localScale = Vector3.one;
                // 默认强制停火子物体挂载的所有粒子，打扫干净战场
                var pss = node.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in pss) { ps.Stop(); ps.gameObject.SetActive(false); }
            }
        }
    }
    
   
}
