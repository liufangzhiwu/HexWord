using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class HeaderSection : UIWindow
{
    [Header("GM工具按钮")]
    public Button GmBtn;
    
    [Header("显隐控制")] 
    public Button BackBtn;
    public Button LevelPuzzleBtn;
    public GameObject Gap;
    
    [Header("体力系统")]
    public Button energyBtn;
    public Text energyTxt;
    public Text energyTimer;
    public GameObject energyAdd;
    
    [Header("蝶蛹进度")] 
    public Button PupaTable;
    public Text Pupatxt;
    
    [Header("金币钮组")]
    public Button ShopBtn;
    public GameObject GoldImage;
    public Text Goldtxt;    
    public GameObject addObj;
    public GameObject redpoint;
    public GameObject sale;
    [Header("未知的")]
    public GameObject GoldLeafredpoint;
    // [Header("右侧按钮组 (词库)")]
    // public Button MyThemeBtn;
    // public Button PuzzlebookBtn;
    
    // Start is called before the first frame update

    private bool _isShowEnergyAdd = true;
    
    // 缓存金币文本的最原始缩放值
    private Vector3 _originalGoldTextScale = Vector3.zero;

    protected override void InitializeUIComponents()
    {
        BackBtn.onClick.AddListener(OnBackClick);
        ShopBtn.AddVibraClickAction(OnShopClick);
#if Unity_ShowLog || UNITY_EDITOR
        GmBtn.AddClickAction(OnGmClick, "", false);
#endif
        // PuzzlebookBtn.AddVibraClickAction(OnClickPuzzleVocabulary);
        LevelPuzzleBtn.AddClickAction(OnClickStagePuzzleScreen);
        PupaTable.AddClickAction(()=>SystemManager.Instance.ShowPanel(PanelType.ButterflyHome));
        energyBtn.AddClickAction(()=> SystemManager.Instance.ShowPanel(PanelType.EnergyScreen));
        // MyThemeBtn.AddVibraClickAction(OnClickMyThemeBtn);
    }

    protected void Start()
    {
        if (Goldtxt != null)
        {
            _originalGoldTextScale = Goldtxt.transform.localScale;
        }
        
        InitUI();
        // ThemeManager.Instance.themeButton = MyThemeBtn.gameObject;
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
        
        // MyThemeBtn.gameObject.SetActive(SystemManager.Instance.PanelIsShowing(PanelType.ChessPlayArea)&&ThemeManager.Instance.CanShowThemeBtn());
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

    protected override void OnEnable()
    {
        // if (MyThemeBtn != null) MyThemeBtn.interactable = true;
        if (GoldLeafredpoint != null)
        {
            GoldLeafredpoint.SetActive(ThemeManager.Instance.IsSkinRedPointActive);
        }
        CustomFlyInManager.Instance.GoldObj= GoldImage.gameObject;
        ThemeManager.Instance.OnSkinRedPointChanged += OnRedPointChanged;
        EventDispatcher.instance.OnUpdateLayerCoin += UpdateCoinLayer;
        EventDispatcher.instance.OnChangeGoldUI += InitUI;
        EventDispatcher.instance.OnChangeTopRaycast += ChangeTopRaycast;
        // EventDispatcher.instance.OnChessScoreChanged += UpdatePupaProgress; 
        EventDispatcher.instance.OnHighlightHeaderUI += HighlightEnergy;
        EventDispatcher.instance.OnHighlightGoldAndEnergy += HighlightGoldAndEnergy;
        
        bool ishomeshow = SystemManager.Instance.PanelIsShowing(PanelType.PrimaryInterface);
        // PuzzlebookBtn.gameObject.SetActive(ishomeshow&& GameDataManager.Instance.UserData.isShowVocabulary);
        GmBtn.gameObject.SetActive(ishomeshow);
        BackBtn.gameObject.SetActive(!ishomeshow);
        energyBtn.gameObject.SetActive(ishomeshow);
        ShopBtn.gameObject.SetActive(ishomeshow);
        PupaTable.gameObject.SetActive(false);
        
        if (SystemManager.Instance.PanelIsShowing(PanelType.StageFinishView) || SystemManager.Instance.PanelIsShowing(PanelType.ChessFinishView))
        {
            BackBtn.GetComponent<Image>().sprite =AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("UI_Icon_Home");
        }
        else
        {
            BackBtn.GetComponent<Image>().sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("UI_Icon_back");
        }
        EventDispatcher.instance.TriggerChangeTopRaycast(true);
        EventDispatcher.instance.TriggerChangeGoldUI(0,false);       
        
        if (SystemManager.Instance.PanelIsShowing(PanelType.GamePlayArea))
        { 
            BackBtn.gameObject.SetActive(true);
            LevelPuzzleBtn.gameObject.SetActive(false);
            PupaTable.gameObject.SetActive(StageHexController.Instance.CurStageData.PupaDatas!=null);
            // RectTransform pupaRect = PupaTable.GetComponent<RectTransform>();
            // pupaRect.anchorMin = Vector2.one;
            // pupaRect.anchorMax = Vector2.one;
            // pupaRect.anchoredPosition = new Vector2(40, -65);
            //
            if (StageHexController.Instance.CurStageData.PupaDatas != null&&!GameDataManager.Instance.ButterflyData.IsOpenButterfly)
            {
                GameDataManager.Instance.ButterflyData.IsOpenButterfly = true;
            }
        }
        else if (SystemManager.Instance.PanelIsShowing(PanelType.ChessPlayArea))
        {
            LevelPuzzleBtn.gameObject.SetActive(false);
            ShopBtn.gameObject.SetActive(false);
           
            if (ChessStageController.Instance.CurrStageData.PupaDatas != null&&!GameDataManager.Instance.ButterflyData.IsOpenButterfly)
            {
                GameDataManager.Instance.ButterflyData.IsOpenButterfly = true;
            }
        }
        else if (SystemManager.Instance.PanelIsShowing(PanelType.StageFinishView))
        {
            BackBtn.gameObject.SetActive(true);
            ShopBtn.gameObject.SetActive(true);
            LevelPuzzleBtn.gameObject.SetActive(true);
        }
        else if(SystemManager.Instance.PanelIsShowing(PanelType.ChessFinishView))
        {
            BackBtn.gameObject.SetActive(true);
            ShopBtn.gameObject.SetActive(true);
            LevelPuzzleBtn.gameObject.SetActive(true);
        }

        InitPupaUI();
        InitEnergyUI();
        // 启用时开始重复调用 (1秒延迟，每秒1次)
        StartCoroutine(CheckLevelPuzzleVisibility());
        
      
    }

    private IEnumerator CheckLevelPuzzleVisibility()
    {
        yield return new WaitForSeconds(0.5f);  
        bool isgameshow = SystemManager.Instance.PanelIsShowing(PanelType.StageFinishView) ||
                          SystemManager.Instance.PanelIsShowing(PanelType.ChessFinishView);
        
        LevelPuzzleBtn.GetComponent<CanvasGroup>().alpha = 0f;
        
        bool hasLevelWords = false;
        while (true)
        {
            yield return new WaitForSeconds(0.8f);  
            if (isgameshow)
            {
                if(GameDataManager.Instance.UserData.levelMode == 1)
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
    private void UpdateCoinLayer(bool istop, bool isshopbtnEnable = true, bool isshowPupa = false,bool shoGap=false)
    {
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
        
        bool isshowGap=!istop || shoGap;
        
        Gap.gameObject.SetActive(isshowGap);
        bool isFinish = false;
        if (istop || isActivityOpen)
        {
            coinObj.gameObject.SetActive(true);
            bool isOldGamePupa = SystemManager.Instance.PanelIsShowing(PanelType.GamePlayArea) && 
                                 StageHexController.Instance.CurStageData.PupaDatas != null;
                             
            bool shouldShowPupa = isshowPupa || SystemManager.Instance.PanelIsShowing(PanelType.ButterflyHome) || isOldGamePupa;
            PupaTable.gameObject.SetActive(shouldShowPupa);
        }
        else if (SystemManager.Instance.PanelIsShowing(PanelType.StageFinishView))
        {
            coinObj.gameObject.SetActive(false);
            PupaTable.gameObject.SetActive(false);
            BackBtn.gameObject.SetActive(true);
        }
        else if (SystemManager.Instance.PanelIsShowing(PanelType.GamePlayArea))
        {
            coinObj.gameObject.SetActive(false);
            PupaTable.gameObject.SetActive(StageHexController.Instance.CurStageData.PupaDatas != null);
        }
        else if (SystemManager.Instance.PanelIsShowing(PanelType.ChessPlayArea))
        {
            coinObj.gameObject.SetActive(false);
            PupaTable.gameObject.SetActive(false);
        }
        else if (SystemManager.Instance.PanelIsShowing(PanelType.ChessFinishView))
        {
            coinObj.gameObject.SetActive(true);
            PupaTable.gameObject.SetActive(false);
            BackBtn.gameObject.SetActive(true);
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

        if (SystemManager.Instance.PanelIsShowing(PanelType.ChessPlayArea))
        {
            energyBtn.gameObject.SetActive(false);
            BackBtn.gameObject.SetActive(false);
        }
        else
        {
            energyBtn.gameObject.SetActive(!PupaTable.gameObject.activeInHierarchy && 
                                           !SystemManager.Instance.PanelIsShowing(PanelType.ChessFinishView));
        }
        
        PupaTable.interactable = !isshowPupa;
       
    }
    /// <summary>
    /// 供退出/重连弹窗调用：将体力槽和蝶蛹进度条提到最顶层高亮显示，穿透黑色半透明蒙版
    /// </summary>
    public void HighlightEnergy(bool isHighlight)
    {
        energyAdd.SetActive(!isHighlight);
        // 1. 给体力按钮挂载 Canvas 以便提层
        Canvas energyCanvas = energyBtn.GetComponent<Canvas>();
        if (energyCanvas == null) energyCanvas = energyBtn.gameObject.AddComponent<Canvas>();
        if (energyBtn.GetComponent<GraphicRaycaster>() == null) energyBtn.gameObject.AddComponent<GraphicRaycaster>();
        // 3. 开始控制层级
        if (isHighlight)
        {
            energyBtn.gameObject.SetActive(true);
            // 提层到弹窗同一级 (PopPanel)
            energyCanvas.overrideSorting = true;
            energyCanvas.sortingLayerName = UIPanelLayer.TipsPanel;
            energyCanvas.sortingOrder = 101; // 保证在弹窗蒙版之上
            energyBtn.interactable = false;
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
            energyBtn.gameObject.SetActive(false);
        }
        _isShowEnergyAdd = !isHighlight;
        energyAdd.SetActive(!isHighlight);
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

        StartCoroutine(ButterfliesManager.Instance.FlyPupaCoroutine(startPoint, PupaTable.transform.GetChild(0), () =>
        {
            AudioManager.Instance.PlaySoundEffect("getPupa");
            GameDataManager.Instance.ButterflyData.AddPupa(pupa);
            Pupatxt.text = $"{GameDataManager.Instance.ButterflyData.currPupa} / {butterflyGrow.Count}";
            GameDataManager.Instance.UserData.SendCurrencyEvent(pupa,"蝶蛹","关卡内收集");
        }, 1.3f));
    }
 
    private void OnClickPuzzleVocabulary()
    {
        if (GameDataManager.Instance.UserData.levelMode == 1)
            StageHexController.Instance.IsEnterVocabulary = false;
        else if (GameDataManager.Instance.UserData.levelMode == 2)
            ChessStageController.Instance.IsEnterVocabulary = false;
        
        SystemManager.Instance.ShowPanel(PanelType.WordVocabularyScreen);
        ChessGuideSystem.Instance.CloseGuide();
    }
    
    private void OnClickStagePuzzleScreen()
    {
        if(GameDataManager.Instance.UserData.levelMode == 1)
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
        if (SystemManager.Instance.PanelIsShowing(PanelType.GamePlayArea))
        {
            AudioManager.Instance.TriggerVibration(40, 50);
            SystemManager.Instance.HidePanel(PanelType.GamePlayArea);
            GameDataManager.Instance.UserData.UpdateOnlineStageTime();
        }    
        
        if (SystemManager.Instance.PanelIsShowing(PanelType.ChessFinishView))
        {
            SystemManager.Instance.HidePanel(PanelType.ChessFinishView);
        }   
        if (SystemManager.Instance.PanelIsShowing(PanelType.ChessPlayArea))
        {
            AudioManager.Instance.TriggerVibration(40, 50);
            SystemManager.Instance.HidePanel(PanelType.ChessPlayArea);
            GameDataManager.Instance.UserData.UpdateOnlineStageTime();
        }    
        
        ChessGuideSystem.Instance.CloseGuide();
    }

    private void OnClickMyThemeBtn()
    {
        if (GameCoreManager.Instance.PanelState == PanelState.MainMenuPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.PrimaryInterface);
        }else if (GameCoreManager.Instance.PanelState == PanelState.GamePingPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.ChessPlayArea);
        }else if (GameCoreManager.Instance.PanelState == PanelState.GameXiaoPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.GamePlayArea);
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
    
    protected override void OnDisable()
    {
        HighlightEnergy(false);
        HighlightGoldAndEnergy(false);
        //EventManager.ChangeBackBtnHandler -= ChangeBackBtnState;
        EventDispatcher.instance.OnUpdateLayerCoin -= UpdateCoinLayer;
        EventDispatcher.instance.OnChangeGoldUI -= InitUI;
        EventDispatcher.instance.OnChangeTopRaycast -= ChangeTopRaycast;
        // EventDispatcher.instance.OnChessScoreChanged -= UpdatePupaProgress; // 🌟 千万别忘了注销！
        EventDispatcher.instance.OnHighlightHeaderUI -= HighlightEnergy;
        EventDispatcher.instance.OnHighlightGoldAndEnergy -= HighlightGoldAndEnergy;
        
        if (ThemeManager.Instance != null)
            ThemeManager.Instance.OnSkinRedPointChanged -= OnRedPointChanged;
        
        LevelPuzzleBtn.gameObject.SetActive(false);
        // 禁用时取消调用
        CancelInvoke(nameof(CheckLevelPuzzleVisibility));
        if(_energyCoroutine != null) StopCoroutine(_energyCoroutine);
        base.OnDisable();
    }

}



