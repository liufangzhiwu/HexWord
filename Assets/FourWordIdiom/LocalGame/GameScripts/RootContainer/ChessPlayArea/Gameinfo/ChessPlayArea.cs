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

/**
 * 核心与生命周期 (ChessPlayArea.Core.cs)
 * 主文件，负责生命周期与核心引用
 */
public partial class ChessPlayArea : UIWindow
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
    [SerializeField] private GameObject leafZenReplacement;  
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
        HitsBtn.AddVibraClickAction(UseTips, "");
        CompleteBtn.AddVibraClickAction(() => UseComplete(), "");
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
            _pupaTrailPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("useritems", "GreenTrailEffect");
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
            _zenCorrectTrailPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("useritems", "huaEffect");
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
            _zenWrongTrailPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("useritems", "huaEffect"); 
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
            _rollingScorePrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "RollingScore"); 
        }
        if (_rollingScorePrefab != null) 
        {
            _rollingScorePool = new ObjectPool(_rollingScorePrefab, ObjectPool.CreatePoolContainer(transform, "RollingScorePool"), 3, PoolBehaviour.GameObject);
        }
        if (_floatingScorePrefab == null)
        {
            _floatingScorePrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "FloatingScore"); 
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
        lightParticlePrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("useritems", "ShowTipTuowei");
        _lightParticlePool = new ObjectPool(lightParticlePrefab, ObjectPool.CreatePoolContainer(transform, "LightParticlePool"), 4, PoolBehaviour.GameObject);
    }
    
    // 初始化所有池子（在 BoardInitialize 或 Start 中调用）
    private void InitAllLeafPools()
    {
        for (int i = 1; i <= 4; i++)
        {
            // 假设预制体名字分别是 LeafPrefab_1, LeafPrefab_2...
            GameObject prefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("useritems", $"UIEffect_shuye0{i}");
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
        EventDispatcher.instance.OnChangeGoldUI += InitToolUI;
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
        CompleteBtn.gameObject.SetActive(CurrStageData.StageId >= 6);
        InitToolUI();
    }

    #endregion

    
    
    /// <summary>
    /// 打开关内词语本
    /// </summary>
    private void ClickLevelPuzzle()
    {
        ChessStageController.Instance.IsEnterVocabulary = true;
        SystemManager.Instance.ShowPanel(PanelType.LevelWordScreen);
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
        
        EventDispatcher.instance.OnChangeGoldUI -= InitToolUI;
        EventDispatcher.instance.OnCheckShowChessTutorial -= CheckShowChessTutorialEvent;
        EventDispatcher.instance.OnAutoPassLevel -= AutoPassLevel;
        EventDispatcher.instance.OnChessScoreChanged -= OnChessScoreChanged;

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
    
}
