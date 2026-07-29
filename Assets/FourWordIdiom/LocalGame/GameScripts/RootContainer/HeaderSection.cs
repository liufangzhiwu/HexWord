using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;


public class HeaderSection : UIWindow
{
    [Header("左侧按钮组")]
    public Button GmBtn;
    public Button SetBtn;
    public Button BackBtn;
    [Header("动态布局控制")]
    public HorizontalLayoutGroup centerLayoutGroup;
    
    [Header("中间按钮组")]
    public HorizontalLayoutGroup middleLayoutGroup;
    public Button ShopBtn;
    public GameObject GoldImage;
    public Text Goldtxt;    
    public Button PupaTable;

    public GameObject addObj;
    public GameObject redpoint;
    public GameObject GoldLeafredpoint;
    public GameObject sale;
    public GameObject PupaImage;
    public Text Pupatxt; 
    
    [Header("体力系统")]
    public Button energyBtn;
    public Text energyTxt;
    public Text energyTimer;
    public GameObject energyAdd;
    
    [Header("右侧按钮组 (词库)")]
    public Button MyThemeBtn;
    public Button PuzzlebookBtn;
    public Button LevelPuzzleBtn;
    
    [Header("游戏内控制")]
    public Button pauseBtn;
    public Text gameTimeTxt;
    public Image gameTimeBg;             // 时间的背景图组件
    
    // 蝶蛹进度
    [Header("游戏内专属：蝶蛹圆形进度条")]
    public GameObject pupaObj;             // 进度条的总开关节点
    public Image pupaProgressBar;          // 你的圆形 Fill 进度条 (Image Type = Filled)
    public GameObject _pupaCompleteEffectNode;
    // Start is called before the first frame update

    private bool _isShowEnergyAdd = true;
    
    // 缓存金币文本的最原始缩放值
    private Vector3 _originalGoldTextScale = Vector3.zero;
    protected void Start()
    {
        if (Goldtxt != null)
        {
            _originalGoldTextScale = Goldtxt.transform.localScale;
        }
        
        InitUI();
        InitializeButtons();       
        
        ThemeManager.Instance.themeButton = MyThemeBtn.gameObject;
        ThemeManager.Instance.CheckAndUpdateSkinRedPoint();
    }

    private Coroutine _coinAnimCoroutine;
    
    private void InitUI(int value=0,bool isanim=false)
    {
        if (_coinAnimCoroutine != null)
        {
            StopCoroutine(_coinAnimCoroutine);
            if (_originalGoldTextScale != Vector3.zero)
            {
                Goldtxt.transform.localScale = _originalGoldTextScale;
            }
        }
        if(isanim)
        {
            _coinAnimCoroutine = StartCoroutine(AnimateCoinAddition(value));
        }
        else
        {
            Goldtxt.text = GameDataManager.Instance.UserData.Gold.ToString();
        }
        
        redpoint.SetActive(!GameDataManager.Instance.UserData.isHideShopRedPoint);
        sale.SetActive(!GameDataManager.Instance.UserData.isHideShopRedPoint);
        
        MyThemeBtn.gameObject.SetActive(SystemManager.Instance.PanelIsShowing(PanelType.ChessPlayArea)&&ThemeManager.Instance.CanShowThemeBtn());

        InitPupaUI();
    }
    
    private IEnumerator AnimateCoinAddition(int amount)
    {
        int startValue = GameDataManager.Instance.UserData.Gold-amount;
        int targetValue = GameDataManager.Instance.UserData.Gold;
        float duration = 0.35f; // 动画持续时间
        float elapsed = 0f;
        // 记录原本的缩放大小（防止多次调用导致大小错乱）
        // Vector3 originalScale = Goldtxt.transform.localScale;
        // 设置最大放大倍数（比如 0.5f 代表最大会放大到 1.5 倍）
        float maxScaleAmount = 0.5f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration); // 归一化
            // 1. 处理数字滚动
            int currentValue = Mathf.RoundToInt(Mathf.Lerp(startValue, targetValue, t));
            Goldtxt.text = currentValue.ToString();
            // 2. 🔥 处理心跳式缩放放大缩小
            float scaleCurve = Mathf.Sin(t * Mathf.PI); 
            Goldtxt.transform.localScale = _originalGoldTextScale * (1f + scaleCurve * maxScaleAmount);
            yield return null;
        }
        Goldtxt.text = targetValue.ToString(); // 确保最终值正确显示
        Goldtxt.transform.localScale = _originalGoldTextScale;
    }

    protected void InitializeButtons()
    {       
        SetBtn.AddClickAction(OnSetClick);
        BackBtn.AddVibraClickAction(OnBackClick);
        ShopBtn.AddClickAction(OnShopClick);
#if Unity_ShowLog || UNITY_EDITOR
        GmBtn.AddClickAction(OnGmClick, "", false);
#endif
        PuzzlebookBtn.AddClickAction(OnClickPuzzleVocabulary);
        LevelPuzzleBtn.AddClickAction(OnClickStagePuzzleScreen);
        PupaTable.AddClickAction(()=>SystemManager.Instance.ShowPanel(PanelType.ButterflyHome));
        energyBtn.AddClickAction(()=>
        {
            // var userData = GameDataManager.Instance.UserData;
            // int stage = Mathf.Max(userData.CurrentStage, userData.CurrentChessStage);
            // bool isAdequate = (userData.Energy >= UserData.MAX_NATURAL_ENERGY) || (stage == 1);
            //
            // // 如果体力是满的，或者是第一关，直接截断！
            // if (isAdequate) 
            // {
            //     return;
            // }
            SystemManager.Instance.ShowPanel(PanelType.EnergyScreen);
        });
        pauseBtn.AddClickAction(OnPauseClicked);
        MyThemeBtn.AddClickAction(OnClickMyThemeBtn);
    }

    protected override void OnEnable()
    {
        LayoutElement pupaElement = PupaTable.GetComponent<LayoutElement>();
        if (pupaElement == null)
        {
            pupaElement = PupaTable.gameObject.AddComponent<LayoutElement>();
        }
        pupaElement.ignoreLayout = false;
        if (pauseBtn != null) pauseBtn.interactable = true;
        if (MyThemeBtn != null) MyThemeBtn.interactable = true;
        
        EventDispatcher.instance.OnUpdateLayerCoin += UpdateCoinLayer;
        EventDispatcher.instance.OnChangeGoldUI += InitUI;
        EventDispatcher.instance.OnChangeTopRaycast += ChangeTopRaycast;
        // EventDispatcher.instance.OnChessScoreChanged += UpdatePupaProgress; 
        EventDispatcher.instance.OnHighlightHeaderUI += HighlightEnergyAndPupa;
        EventDispatcher.instance.OnHighlightGoldAndEnergy += HighlightGoldAndEnergy;
        
        bool ishomeshow = SystemManager.Instance.PanelIsShowing(PanelType.PrimaryInterface);
        PuzzlebookBtn.gameObject.SetActive(ishomeshow&& GameDataManager.Instance.UserData.isShowVocabulary);
        GmBtn.gameObject.SetActive(ishomeshow);
        BackBtn.gameObject.SetActive(!ishomeshow);
        SetBtn.gameObject.SetActive(ishomeshow);
        energyBtn.gameObject.SetActive(ishomeshow);
        CustomFlyInManager.Instance.GoldObj=GoldImage.gameObject;

        if (SystemManager.Instance.PanelIsShowing(PanelType.StageFinishView) || SystemManager.Instance.PanelIsShowing(PanelType.ChessFinishView))
        {
            BackBtn.GetComponent<Image>().sprite =AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("UI_Icon_Home");
        }
        else
        {
            BackBtn.GetComponent<Image>().sprite =AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("UI_Icon_back");
        }
        EventDispatcher.instance.TriggerChangeTopRaycast(true);
        EventDispatcher.instance.TriggerChangeGoldUI(0,false);       

        //LevelPuzzleBtn.gameObject.SetActive(SystemManager.Instance.PanelIsShowing(PanelType.StageFinishView));
        
        if (SystemManager.Instance.PanelIsShowing(PanelType.HexGamePlayArea))
        {
            ShopBtn.gameObject.SetActive(false);
            PupaTable.gameObject.SetActive(StageHexController.Instance.CurStageData.PupaDatas!=null);
            pupaElement.ignoreLayout = true;
            RectTransform pupaRect = PupaTable.GetComponent<RectTransform>();
            pupaRect.anchorMin = Vector2.one;
            pupaRect.anchorMax = Vector2.one;
            pupaRect.anchoredPosition = new Vector2(40, -65);
            
            pupaObj.SetActive(false);
            pauseBtn.gameObject.SetActive(false);
            if (StageHexController.Instance.CurStageData.PupaDatas != null&&!GameDataManager.Instance.ButterflyData.IsOpenButterfly)
            {
                GameDataManager.Instance.ButterflyData.IsOpenButterfly = true;
            }
        }
        else if (SystemManager.Instance.PanelIsShowing(PanelType.ChessPlayArea))
        {
            BackBtn.gameObject.SetActive(false);
            ShopBtn.gameObject.SetActive(false);
            PupaTable.gameObject.SetActive(false);
            pauseBtn.gameObject.SetActive(false);
            
            if(ChessStageController.Instance.CurrentStage>1)
                pauseBtn.gameObject.SetActive(true);
            if (ChessStageController.Instance.CurrStageData.PupaDatas != null&&!GameDataManager.Instance.ButterflyData.IsOpenButterfly)
            {
                GameDataManager.Instance.ButterflyData.IsOpenButterfly = true;
            }
            
            bool showPupaProgress = ButterfliesManager.Instance.CanShowPupaProgressBarThisLevel(ChessStageController.Instance.OptimalTotalScore);
            
            pupaObj.SetActive(showPupaProgress);
            ChangeLayoutAlignment(true);
            ChessPlayArea.Instance._timerText = gameTimeTxt;
            UpdatePupaProgress(ChessStageController.Instance.CurrentTotalScore, true);
        }
        else if (SystemManager.Instance.PanelIsShowing(PanelType.StageFinishView) || 
                 SystemManager.Instance.PanelIsShowing(PanelType.ChessFinishView))
        {
            BackBtn.gameObject.SetActive(true);
            bool isChessFinish = SystemManager.Instance.PanelIsShowing(PanelType.ChessFinishView);
            ShopBtn.gameObject.SetActive(isChessFinish);
      
            ShopBtn.gameObject.SetActive(false);
            PupaTable.gameObject.SetActive(false);
            pupaObj.SetActive(false);
            pauseBtn.gameObject.SetActive(false);
            ChangeLayoutAlignment(false);
        }
        else
        {
            addObj.gameObject.SetActive(true);
            ShopBtn.gameObject.SetActive(true);
            PupaTable.gameObject.SetActive(false);
            pupaObj.SetActive(false);
            pauseBtn.gameObject.SetActive(false);
            ChangeLayoutAlignment(false);
        }

        InitPupaUI();
        InitEnergyUI();
        // 启用时开始重复调用 (1秒延迟，每秒1次)
        StartCoroutine(CheckLevelPuzzleVisibility());
        
        GoldLeafredpoint?.SetActive(ThemeManager.Instance.IsSkinRedPointActive);
        ThemeManager.Instance.OnSkinRedPointChanged += OnRedPointChanged;
    }
    /// <summary>
    /// 动态改变顶部资产区域的对齐方式
    /// </summary>
    /// <param name="isRightAlign">true: 右对齐(关卡内) | false: 左对齐(大厅等其他情况)</param>
    private void ChangeLayoutAlignment(bool isRightAlign)
    {
        if (centerLayoutGroup != null)
        {
            // 根据你截图里的设置，这里使用 UpperRight 和 UpperLeft。
            // 如果你的 UI 需要垂直居中，可以换成 MiddleRight 和 MiddleLeft。
            centerLayoutGroup.childAlignment = isRightAlign ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
            // 🌟 核心：强制刷新布局，防止切换时 UI 出现一帧的残影或位置不对
            LayoutRebuilder.ForceRebuildLayoutImmediate(centerLayoutGroup.GetComponent<RectTransform>());
        }
    }
    /// <summary>
    /// 动态改变顶部资产区域的对齐方式
    /// </summary>
    /// <param name="isRightAlign">true: 右对齐(关卡内) | false: 左对齐(大厅等其他情况)</param>
    private void MiddleLayoutAlignment(bool isRightAlign)
    {
        if (middleLayoutGroup != null)
        {
            // 根据你截图里的设置，这里使用 UpperRight 和 UpperLeft。
            // 如果你的 UI 需要垂直居中，可以换成 MiddleRight 和 MiddleLeft。
            middleLayoutGroup.childAlignment = isRightAlign ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            // 🌟 核心：强制刷新布局，防止切换时 UI 出现一帧的残影或位置不对
            LayoutRebuilder.ForceRebuildLayoutImmediate(middleLayoutGroup.GetComponent<RectTransform>());
        }
    }
    private IEnumerator CheckLevelPuzzleVisibility()
    {
        yield return new WaitForSeconds(0.5f);  
        bool isgameshow = SystemManager.Instance.PanelIsShowing(PanelType.HexGamePlayArea) ||
                          SystemManager.Instance.PanelIsShowing(PanelType.StageFinishView) ||
                          SystemManager.Instance.PanelIsShowing(PanelType.ChessFinishView);
        
        LevelPuzzleBtn.GetComponent<CanvasGroup>().alpha = 0f;
        
        bool hasLevelWords = false;
        while (true)
        {
            yield return new WaitForSeconds(0.8f);  
            if (isgameshow)
            {
                if(GameDataManager.Instance.UserData.levelMode == 1||GameDataManager.Instance.UserData.levelMode == 3)
                    hasLevelWords = StageHexController.Instance.CurStageData.FoundTargetPuzzles.Count > 0 ;
                else if (GameDataManager.Instance.UserData.levelMode == 2&&ChessStageController.Instance.CurrStageData!=null)
                    hasLevelWords = ChessStageController.Instance.CurrStageData.FoundTargetPuzzles.Count > 0;
                
                // Debug.Log("当前模式： " + GameDataManager.Instance.UserData.levelMode +" "+ hasLevelWords);
                LevelPuzzleBtn.gameObject.SetActive(hasLevelWords);
                LevelPuzzleBtn.GetComponent<CanvasGroup>().DOFade(1f,0.2f);
            }
            yield return new WaitForSeconds(0.3f);  
        }
    }

    /// <summary>
    /// 更改金币显示层级
    /// </summary>
    private void UpdateCoinLayer(bool istop, bool isshopbtnEnable = true, bool isshowPupa = false)
    {
        // ShopBtn.gameObject.SetActive(istop);
        GameObject coinObj = ShopBtn.gameObject;
        Canvas canvas = coinObj.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = coinObj.AddComponent<Canvas>();
            if (coinObj.GetComponent<GraphicRaycaster>() == null) coinObj.AddComponent<GraphicRaycaster>();
        }
        
        Canvas pupaCanvas = PupaTable.GetComponent<Canvas>();
        if (pupaCanvas == null)
        {
            pupaCanvas = PupaTable.gameObject.AddComponent<Canvas>();
            if (PupaTable.GetComponent<GraphicRaycaster>() == null) PupaTable.gameObject.AddComponent<GraphicRaycaster>();
        }

        bool isActivityOpen = SystemManager.Instance.PanelIsShowing(PanelType.LimitTimeScreen) ||
                              SystemManager.Instance.PanelIsShowing(PanelType.SevenSignScreen) ||
                              SystemManager.Instance.PanelIsShowing(PanelType.DailyTasksScreen) ||
                              SystemManager.Instance.PanelIsShowing(PanelType.ButterflyHome) ||
                              SystemManager.Instance.PanelIsShowing(PanelType.ShopScreen) ||
                              SystemManager.Instance.PanelIsShowing(PanelType.EnergyScreen) ||
                              SystemManager.Instance.PanelIsShowing(PanelType.GetItemScreen);
        
        bool isFinish = false;
        if (istop || isActivityOpen)
        {
            coinObj.gameObject.SetActive(true);
            bool isOldGamePupa = SystemManager.Instance.PanelIsShowing(PanelType.HexGamePlayArea) && 
                                 StageHexController.Instance.CurStageData.PupaDatas != null;
                             
            bool shouldShowPupa = isshowPupa || SystemManager.Instance.PanelIsShowing(PanelType.ButterflyHome) || isOldGamePupa;
            PupaTable.gameObject.SetActive(shouldShowPupa);
        }
        else if (SystemManager.Instance.PanelIsShowing(PanelType.StageFinishView))
        {
            coinObj.gameObject.SetActive(false);
            PupaTable.gameObject.SetActive(false);
        }
        else if (SystemManager.Instance.PanelIsShowing(PanelType.HexGamePlayArea))
        {
            coinObj.gameObject.SetActive(false);
            PupaTable.gameObject.SetActive(StageHexController.Instance.CurStageData.PupaDatas != null);
        }
        else if (SystemManager.Instance.PanelIsShowing(PanelType.ChessPlayArea))
        {
            // 🌟 核心防御：严格锁死，游戏局内绝不出现金币栏
            coinObj.gameObject.SetActive(false);
            PupaTable.gameObject.SetActive(false);
        }
        else if (SystemManager.Instance.PanelIsShowing(PanelType.ChessFinishView))
        {
            coinObj.gameObject.SetActive(true);
            PupaTable.gameObject.SetActive(false);
            isFinish = true;
        }
        else
        {
            bool isLobby = SystemManager.Instance.PanelIsShowing(PanelType.PrimaryInterface);
            coinObj.gameObject.SetActive(isLobby);
            PupaTable.gameObject.SetActive(isshowPupa);
        }
       
        if (istop)
        {
            canvas.overrideSorting = true;
            canvas.sortingLayerName = "RewardPanel";
            canvas.sortingOrder = 100;
            
            pupaCanvas.overrideSorting = true;
            pupaCanvas.sortingLayerName = "RewardPanel";
            pupaCanvas.sortingOrder = 100;
        }
        else
        {
            canvas.overrideSorting = false;
            canvas.sortingLayerName = "TopPanel";
            canvas.sortingOrder = 0;

            pupaCanvas.overrideSorting = false;
            pupaCanvas.sortingLayerName = "TopPanel";
            pupaCanvas.sortingOrder = 0;
        }

        // 🌟 核心时序修复：只有当资产栏确定显示时，加号(addObj)才根据功能启用
        // 如果资产栏已经因为在游戏局内(SetActive(false))被关闭，则保留加号的默认激活，防下次打开走光丢失
        if (coinObj.gameObject.activeSelf)
        {
            if (!isFinish)
            {
                addObj.gameObject.SetActive(isshopbtnEnable);
                ShopBtn.enabled = isshopbtnEnable;
            }
            else
            {
                addObj.gameObject.SetActive(true);
                ShopBtn.enabled = true;
            }
        }
        else
        {
            // 资产栏隐藏期间，加号原地保持基础激活状态，绝不篡改隐去
            addObj.gameObject.SetActive(true);
            ShopBtn.enabled = isshopbtnEnable;
        }

        if (isshowPupa)
        {
            InitPupaUI();
        }
        PupaTable.interactable = !isshowPupa;
    }
    /// <summary>
    /// 供退出/重连弹窗调用：将体力槽和蝶蛹进度条提到最顶层高亮显示，穿透黑色半透明蒙版
    /// </summary>
    public void HighlightEnergyAndPupa(bool isHighlight)
    {
        // energyBtn.transform.GetChild(1).gameObject.SetActive(!isHighlight);
        energyAdd.SetActive(!isHighlight);
        // 1. 给体力按钮挂载 Canvas 以便提层
        Canvas energyCanvas = energyBtn.GetComponent<Canvas>();
        if (energyCanvas == null) energyCanvas = energyBtn.gameObject.AddComponent<Canvas>();
        if (energyBtn.GetComponent<GraphicRaycaster>() == null) energyBtn.gameObject.AddComponent<GraphicRaycaster>();

        // 2. 给蝶蛹进度组挂载 Canvas 以便提层
        Canvas pupaCanvas = pupaObj.GetComponent<Canvas>();
        if (pupaCanvas == null) pupaCanvas = pupaObj.AddComponent<Canvas>();
        if (pupaObj.GetComponent<GraphicRaycaster>() == null) pupaObj.AddComponent<GraphicRaycaster>();
        
        // 3. 开始控制层级
        if (isHighlight)
        {
            energyBtn.gameObject.SetActive(true);
            // 提层到弹窗同一级 (PopPanel)
            energyCanvas.overrideSorting = true;
            energyCanvas.sortingLayerName = UIPanelLayer.TipsPanel;
            energyCanvas.sortingOrder = 101; // 保证在弹窗蒙版之上
            energyBtn.interactable = false;
            
            pupaCanvas.overrideSorting = true;
            pupaCanvas.sortingLayerName = UIPanelLayer.TipsPanel;
            pupaCanvas.sortingOrder = 101;
        }
        else
        {
            // 恢复默认层级
            energyCanvas.overrideSorting = false;
            energyCanvas.sortingOrder = 0;
            var userData = GameDataManager.Instance.UserData;
            int stage = Mathf.Max(userData.CurrentHexStage, userData.CurrentChessStage);
            bool isAdequate = (userData.Energy >= UserData.MAX_NATURAL_ENERGY) || (stage == 1);
            energyBtn.interactable = !isAdequate;

            pupaCanvas.overrideSorting = false;
            pupaCanvas.sortingOrder = 0;
            energyBtn.gameObject.SetActive(false);
        }
        
        // Debug.LogError($" {isHighlight} 最后关闭了吗" + energyCanvas.overrideSorting);
        _isShowEnergyAdd = !isHighlight;
        energyAdd.SetActive(!isHighlight);
        MiddleLayoutAlignment(isHighlight);
    }
    /// <summary>
    /// 供体力购买界面调用：将金币槽和体力槽提到最顶层高亮显示
    /// </summary>
    private void HighlightGoldAndEnergy(bool isHighlight)
    {
        // 1. 获取/添加 体力的 Canvas
        Canvas energyCanvas = energyBtn.GetComponent<Canvas>();
        if (energyCanvas == null) energyCanvas = energyBtn.gameObject.AddComponent<Canvas>();
        if (energyBtn.GetComponent<GraphicRaycaster>() == null) energyBtn.gameObject.AddComponent<GraphicRaycaster>();

        // 2. 获取/添加 金币(ShopBtn) 的 Canvas
        Canvas goldCanvas = ShopBtn.GetComponent<Canvas>();
        if (goldCanvas == null) goldCanvas = ShopBtn.gameObject.AddComponent<Canvas>();
        if (ShopBtn.GetComponent<GraphicRaycaster>() == null) ShopBtn.gameObject.AddComponent<GraphicRaycaster>();

        var userData = GameDataManager.Instance.UserData;
        int stage = Mathf.Max(userData.CurrentHexStage, userData.CurrentChessStage);
        bool isAdequate = (userData.Energy >= UserData.MAX_NATURAL_ENERGY) || (stage == 1);
        
        if (isHighlight)
        {
            // 双双提层到弹窗同一级
            energyCanvas.overrideSorting = true;
            energyCanvas.sortingLayerName = UIPanelLayer.TipsPanel;
            energyCanvas.sortingOrder = 101;
            energyBtn.interactable = false;

            goldCanvas.overrideSorting = true;
            goldCanvas.sortingLayerName = UIPanelLayer.TipsPanel;
            goldCanvas.sortingOrder = 101;
            ShopBtn.interactable = false;
            
        }
        else
        {
            // 恢复默认层级
            energyCanvas.overrideSorting = false;
            energyCanvas.sortingOrder = 0;
            energyBtn.interactable = !isAdequate;
            
            goldCanvas.overrideSorting = false;
            goldCanvas.sortingOrder = 0;
            ShopBtn.interactable = true;
        }
        
        energyAdd.SetActive(!isAdequate);
    }
    private void InitPupaUI()
    {
        ButterflyGrow butterflyGrow =ButterfliesManager.Instance.GetCurrentGrow();
        if(butterflyGrow == null)
            return;
        
        Pupatxt.text = $"{GameDataManager.Instance.ButterflyData.currPupa} / {butterflyGrow.Count}";
    }
    private Coroutine _energyCoroutine;
    private void InitEnergyUI()
    {
        // 确保不会重复开启协程
        if (_energyCoroutine != null) StopCoroutine(_energyCoroutine);
        _energyCoroutine = StartCoroutine(EnergyTimerCoroutine());
    }

    private IEnumerator EnergyTimerCoroutine()
    {
        WaitForSeconds wait = new WaitForSeconds(1f);
        while (true)
        {
            var userData = GameDataManager.Instance.UserData;
            
            // 每次循环都可以顺手让底层核对一下体力（处理切后台等情况产生的时间差）
            userData.CalculateEnergyRegen();
            int currentEnergy = userData.Energy;
            // 1. 刷新当前体力数值显示 (例如 "3" 或者 "充足")
            int stage = Mathf.Max(userData.CurrentHexStage, userData.CurrentChessStage);
            bool isAdequate = (currentEnergy >= UserData.MAX_NATURAL_ENERGY);
            // string energyStatus = userData.GetEnergyDisplayString();
            // energyAdd.SetActive((!isAdequate) && _isShowEnergyAdd); // 充足时不显示加号
            energyBtn.interactable = true;
            if (isAdequate && stage == 1)
            {
                // 1. 第一关充足：红心内为空，右侧显示“充足”
                energyTxt.text = "";
                energyTimer.text = "充足";
                energyAdd.SetActive(false);
                energyBtn.interactable = false;
            }
            else if (currentEnergy >= UserData.MAX_NATURAL_ENERGY)
            {
                // 2. 体力已满：红心内为空，右侧显示最大数值
                energyTxt.text = "";
                energyTimer.text = currentEnergy.ToString();
                energyAdd.SetActive(true);
            }
            else
            {
                // 3. 体力未满恢复中：红心显示当前值，右侧显示倒计时
                energyTxt.text = currentEnergy.ToString();
                
                int remainSeconds = userData.GetNextEnergyRegenSeconds();
                // 当倒计时归零时，触发体力增加！
                if (remainSeconds <= 0)
                {
                    userData.CalculateEnergyRegen();
                    currentEnergy = userData.Energy;
                    energyTxt.text = currentEnergy.ToString(); // 刷新红心内的数字
                    remainSeconds = userData.GetNextEnergyRegenSeconds();
                }
                
                // 格式化倒计时
                int mins = remainSeconds / 60;
                int secs = remainSeconds % 60;
                energyTimer.text = string.Format("{0:00}:{1:00}", mins, secs);
                energyAdd.SetActive(_isShowEnergyAdd);
            }
           
            // Canvas eCanvas = energyBtn.GetComponent<Canvas>();
            // bool isHighlightLocked = eCanvas != null && eCanvas.overrideSorting;
            if (stage != 1)
            {
                energyBtn.interactable = _isShowEnergyAdd;
                energyAdd.SetActive(_isShowEnergyAdd);
            }
            // if (!isHighlightLocked)
            // {
            //     energyBtn.interactable = !isAdequate; // 充足时无法点击
            // }
            // 等待1秒再执行下一次
            yield return wait;
        }
    }
    public void ShowPupaTableAnima(Transform startPoint, int pupa = 1, Transform parent  = null)
    {
        ButterflyGrow butterflyGrow =ButterfliesManager.Instance.GetCurrentGrow();
        if(butterflyGrow == null)
            return;

        StartCoroutine(ButterfliesManager.Instance.FlyPupaCoroutine(startPoint, PupaImage.transform, () =>
        {
            AudioManager.Instance.PlaySoundEffect("getPupa");
            GameDataManager.Instance.ButterflyData.AddPupa(pupa);
            Pupatxt.text = $"{GameDataManager.Instance.ButterflyData.currPupa} / {butterflyGrow.Count}";
            GameDataManager.Instance.UserData.SendCurrencyEvent(pupa,"蝶蛹","关卡内收集");
        }, 1.3f));
    }
    
    // 监听到事件后执行的方法
    private void UpdatePupaProgress(int currentScore)
    {
        UpdatePupaProgress(currentScore, false); // 默认带动画
    }
    /// <summary>
    /// 刷新蝶蛹圆环进度
    /// </summary>
    /// <param name="currentScore">当前获得的总分</param>
    /// <param name="isInstant">是否瞬间刷满(不播动画，用于界面刚打开时)</param>
    public void UpdatePupaProgress(int currentScore, bool isInstant)
    {
        if (pupaObj.activeSelf)
        {
            int threshold = ButterfliesManager.Instance.GetScoreThresholdForPupa();
            
            // 🌟 核心机制：如果总分超过了阈值(比如拿了150分，阈值60)，取余数得出30，让进度条循环显示！
            // 防止除以0报错，且保留当 currentScore 正好等于 threshold 时，视觉上呈现满环
            float targetFill = Mathf.Clamp01((float)currentScore / threshold);
            
            Text progressText = pupaObj.GetComponentInChildren<Text>(true);
            bool isJustCompleted = (targetFill >= 1f && pupaProgressBar.fillAmount < 1f);
            if (targetFill < 1f) progressText.text = "+1"; 
            
            pupaProgressBar.DOKill();
            if (isInstant)
            {
                // 界面刚打开，瞬间设置，不播平滑动画
                pupaProgressBar.fillAmount = targetFill;
                progressText.gameObject.SetActive(targetFill >= 1f); // 满了才显示数字
                if (targetFill >= 1f) progressText.text = "+1";
            }
            else
            {
                // 游戏进行中加分，花 0.3 秒平滑过渡过去
                // 平滑动画赋值（游戏中途）
                pupaProgressBar.DOFillAmount(targetFill, 0.3f).SetEase(Ease.OutQuad).OnComplete(() => 
                {
                    progressText.gameObject.SetActive(targetFill >= 1f); // 动画涨满了才显示数字
                    
                    // 如果是这次才刚刚达标满格，触发发光粒子特效！
                    if (isJustCompleted)
                    {
                        progressText.text = "+1";
                        PlayPupaCompleteEffect();
                    }
                });
            }
        }
    }
    
    /// <summary>
    /// 🌟 预留接口：蝶蛹进度收集完成时的粒子发光散开特效
    /// </summary>
    private void PlayPupaCompleteEffect()
    {
        if (_pupaCompleteEffectNode != null)
        {
            // 1. 先关闭再打开，能自动重置很多 UI 动画状态
            _pupaCompleteEffectNode.SetActive(false);
            _pupaCompleteEffectNode.SetActive(true);
            
            // 2. 找到粒子并让它喷发
            ParticleSystem ps = _pupaCompleteEffectNode.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                ps.Stop(); // 确保从头开始
                ps.Play();
            }
            
            DOVirtual.DelayedCall(2f, () => 
            {
                if (_pupaCompleteEffectNode != null)
                {
                    _pupaCompleteEffectNode.SetActive(false);
                }
            });
        }
    }
    /// <summary>
    /// 增加蝶蛹收集数量，并刷新文本 UI
    /// </summary>
    /// <param name="addCount">增加的数量</param>
    public void AddPupaCountAndUpdateUI(int addCount = 1)
    {
        ButterflyGrow butterflyGrow = ButterfliesManager.Instance.GetCurrentGrow();
        if(butterflyGrow == null) return;

        // 1. 更新底层数据
        GameDataManager.Instance.ButterflyData.AddPupa(addCount);
        
        // 可选：发送打点事件
        GameDataManager.Instance.UserData.SendCurrencyEvent(addCount, "蝶蛹", "树叶化蛹收集");

        // 2. 刷新界面文本
        Pupatxt.text = $"{GameDataManager.Instance.ButterflyData.currPupa} / {butterflyGrow.Count}";

        // 3. 🌟 增加一点 UI 表现力：让文字在增加时“跳动”一下，增加手感反馈
        Pupatxt.transform.DOKill();
        Pupatxt.transform.localScale = Vector3.one; // 恢复初始大小（假设初始是 1）
        Pupatxt.transform.DOPunchScale(new Vector3(0.3f, 0.3f, 0.3f), 0.3f, 5, 1f);
        
        // 4. 播放收集音效
        AudioManager.Instance.PlaySoundEffect("getPupa");
    }
    /// <summary>
    /// 触发时间警告表现（红色背景 + 单次呼吸放大）
    /// </summary>
    public void ShowTimeWarning()
    {
        if (gameTimeBg != null)
        {
            gameTimeBg.gameObject.SetActive(true);
            gameTimeBg.color = new Color(gameTimeBg.color.r, gameTimeBg.color.g, gameTimeBg.color.b, 1f);
            // 2. 杀掉旧动画，执行一次呼吸效果 (变大再缩回)
            gameTimeBg.transform.DOKill();
            gameTimeBg.DOFade(.15f, 1.5f).SetEase(Ease.InOutSine).SetLoops(4, LoopType.Yoyo)
                .OnComplete(()=>{ gameTimeBg.gameObject.SetActive(false); });
        }
    }
    /// <summary>
    /// 重置时间表现（恢复正常背景）
    /// </summary>
    public void ResetTimeWarning()
    {
        if (gameTimeBg != null)
        {
            // 停止动画，恢复缩放
            gameTimeBg.transform.DOKill();
            gameTimeBg.transform.localScale = new Vector3(0.9f,0.9f,0.9f);
            gameTimeBg.gameObject.SetActive(false);
        }
    }
    private void OnClickPuzzleVocabulary()
    {
        if(GameDataManager.Instance.UserData.levelMode == 1||GameDataManager.Instance.UserData.levelMode == 3)
            StageHexController.Instance.IsEnterVocabulary = false;
        else if (GameDataManager.Instance.UserData.levelMode == 2)
            ChessStageController.Instance.IsEnterVocabulary = false;
        
        SystemManager.Instance.ShowPanel(PanelType.WordVocabularyScreen);
        ChessGuideSystem.Instance.CloseGuide();
    }
    
    private void OnClickStagePuzzleScreen()
    {
        if(GameDataManager.Instance.UserData.levelMode == 1||GameDataManager.Instance.UserData.levelMode == 3)
            StageHexController.Instance.IsEnterVocabulary = true;
        else if (GameDataManager.Instance.UserData.levelMode == 2)
            ChessStageController.Instance.IsEnterVocabulary = true;
        
        SystemManager.Instance.ShowPanel(PanelType.LevelWordScreen);
        ChessGuideSystem.Instance.CloseGuide();
        
    }

    private void OnGmClick()
    {
        //string localIP = GetLocalIPAddress();
        //bool isloaclIp = IsInLocalNetwork(localIP);
        //bool isloaclIp = IsLocalDevice();
        if (true) 
        {
            SystemManager.Instance.ShowPanel(PanelType.DebugMenu);
        }
        //Debug.Log("TP-LINK 5G 当前IP地址: " + localIP + "设备是否在局域网内: " + isloaclIp);
    }
   

    private void OnSetClick()
    {
        
        SystemManager.Instance.ShowPanel(PanelType.OptionsView);
        ChessGuideSystem.Instance.CloseGuide();
    }

    private void OnShopClick()
    {
        SystemManager.Instance.ShowPanel(PanelType.ShopScreen);
        if (SystemManager.Instance.PanelIsShowing(PanelType.GetItemScreen))
        {
            SystemManager.Instance.HidePanel(PanelType.GetItemScreen);
        }
        ChessGuideSystem.Instance.CloseGuide();
    }

    private void OnBackClick()
    {
        base.Close();
        transform.GetComponent<HeaderSection>().AddCloseListener(() =>
        {
            SystemManager.Instance.ShowPanel(PanelType.PrimaryInterface);
            ChangeBackBtnState(false);
        });

        if (SystemManager.Instance.PanelIsShowing(PanelType.StageFinishView))
        {
            SystemManager.Instance.HidePanel(PanelType.StageFinishView);
        }
        if (SystemManager.Instance.PanelIsShowing(PanelType.HexGamePlayArea))
        {
            SystemManager.Instance.HidePanel(PanelType.HexGamePlayArea);
            GameDataManager.Instance.UserData.UpdateOnlineStageTime();
        }    
        
        if (SystemManager.Instance.PanelIsShowing(PanelType.ChessFinishView))
        {
            SystemManager.Instance.HidePanel(PanelType.ChessFinishView);
        }   
        if (SystemManager.Instance.PanelIsShowing(PanelType.ChessPlayArea))
        {
            SystemManager.Instance.HidePanel(PanelType.ChessPlayArea);
            GameDataManager.Instance.UserData.UpdateOnlineStageTime();
        }    
        
        ChessGuideSystem.Instance.CloseGuide();
    }
    
    private void OnPauseClicked()
    {
        if (SystemManager.Instance.PanelIsShowing(PanelType.ChessPlayArea) && ChessPlayArea.Instance != null)
        {
            // 调用游戏内的暂停逻辑（里面包含了停住时间和弹出面板）
            ChessPlayArea.Instance.OnPauseClick(); 
        }
        else
        {
            // 兜底保护
            SystemManager.Instance.ShowPanel(PanelType.PauseGameScreen);
        }
    }

    private void OnClickMyThemeBtn()
    {
        if (GameCoreManager.Instance.PanelState == PanelState.MainMenuPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.PrimaryInterface);
        }else if (GameCoreManager.Instance.PanelState == PanelState.GamePingPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.ChessPlayArea);
        }else if (GameCoreManager.Instance.PanelState == PanelState.GameHexPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.HexGamePlayArea);
        }
        
        SystemManager.Instance.HidePanel(PanelType.HeaderSection , true, () =>
        {
            SystemManager.Instance.ShowPanel(PanelType.MyThemeScreen);
        });
        //SystemManager.Instance.ShowPanel(PanelType.MyThemeScreen);
    }
    
    public void ChangeBackBtnState(bool isshow)
    {
        BackBtn.gameObject.SetActive(isshow);
        SetBtn.gameObject.SetActive(!isshow);
        //LevelPuzzleBtn.gameObject.SetActive(!isshow);
    }

    private void ChangeTopRaycast(bool isblock)
    {
        transform.GetComponent<CanvasGroup>().blocksRaycasts = isblock;
    }

    private void OnRedPointChanged(bool show)
    {
        if (GoldLeafredpoint != null)
            GoldLeafredpoint.SetActive(show);
    }
    
    /// <summary>
    /// 仅视觉表现：蝶蛹 UI 文本 +1 并播放跳动特效（不修改底层真实数据）
    /// </summary>
    public void PlayPupaCollectVisualEffect(int addVisualValue = 1)
    {
        if (pupaObj == null) return;

        // 1. 获取圆环进度条中间的那个文本
        Text progressText = pupaObj.GetComponentInChildren<Text>(true);
        if (progressText == null) return;

        // 2. 只有当它已经处于显示状态（即已经满环弹出了）才去修改它
        if (progressText.gameObject.activeSelf)
        {
            // 提取当前文本中的数字（去掉可能带有的 "+" 号和空格）
            string currentStr = progressText.text.Replace("+", "").Trim();
            
            if (int.TryParse(currentStr, out int currentVisualCount))
            {
                // 在原有数字基础上加上新飞过来的数量
                int nextCount = currentVisualCount + addVisualValue;
                progressText.text = $"+{nextCount}";
            }
            else
            {
                // 兜底：如果原文本没数字或者解析失败，直接显示当前增加的值
                progressText.text = $"+{addVisualValue}";
            }

            // 3. 给这个文本加一个 Q 弹的放大缩小反馈，增强“吸收”的手感
            progressText.transform.DOKill();
            progressText.transform.localScale = Vector3.one; // 恢复初始大小
            progressText.transform.DOPunchScale(new Vector3(0.5f, 0.5f, 0.5f), 0.3f, 5, 1f);
        }
        // 3. 播放音效
        // AudioManager.Instance.PlaySoundEffect("getPupa");
    }
    
    protected override void OnDisable()
    {
        //EventManager.ChangeBackBtnHandler -= ChangeBackBtnState;
        CustomFlyInManager.Instance.GoldObj = null;
        EventDispatcher.instance.OnUpdateLayerCoin -= UpdateCoinLayer;
        EventDispatcher.instance.OnChangeGoldUI -= InitUI;
        EventDispatcher.instance.OnChangeTopRaycast -= ChangeTopRaycast;
        // EventDispatcher.instance.OnChessScoreChanged -= UpdatePupaProgress; // 🌟 千万别忘了注销！
        EventDispatcher.instance.OnHighlightHeaderUI -= HighlightEnergyAndPupa;
        EventDispatcher.instance.OnHighlightGoldAndEnergy -= HighlightGoldAndEnergy;
        
        if (ThemeManager.Instance != null)
            ThemeManager.Instance.OnSkinRedPointChanged -= OnRedPointChanged;
        
        LevelPuzzleBtn.gameObject.SetActive(false);
        // 禁用时取消调用
        CancelInvoke(nameof(CheckLevelPuzzleVisibility));
        gameTimeBg.transform.DOKill();
        if(_energyCoroutine != null) StopCoroutine(_energyCoroutine);
        base.OnDisable();
    }

}



