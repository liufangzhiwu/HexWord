using System;
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
    public Text discountText;
    
    [Header("未知的")]
    public GameObject GoldLeafredpoint;

    private bool _isShowEnergyAdd = true;
    
    // 缓存金币文本的最原始缩放值
    private Vector3 _originalGoldTextScale = Vector3.zero;

    // ======================= 无限体力缓存 =======================
    private bool _isUnlimitedEnergyActive;
    private double _unlimitedEnergyRemainSeconds;

    protected override void InitializeUIComponents()
    {
        BackBtn.onClick.AddListener(OnBackClick);
        ShopBtn.AddVibraClickAction(OnShopClick);
#if Unity_ShowLog || UNITY_EDITOR
        GmBtn.AddClickAction(OnGmClick, "", false);
#endif
        LevelPuzzleBtn.AddClickAction(OnClickStagePuzzleScreen);
        PupaTable.AddClickAction(()=>SystemManager.Instance.ShowPanel(PanelType.ButterflyHome));
        energyBtn.AddClickAction(()=> SystemManager.Instance.ShowPanel(PanelType.EnergyScreen));
    }

    protected void Start()
    {
        if (Goldtxt != null)
        {
            _originalGoldTextScale = Goldtxt.transform.localScale;
        }
        
        InitUI();
        ThemeManager.Instance.CheckAndUpdateSkinRedPoint();
    }

    private Coroutine _coinAnimCoroutine;
    
    private void InitUI(int value=0,bool isanim=false)
    {
        if(GameDataManager.Instance==null) return;
        
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
        
        // 红点（进店消费入口角标）
        redpoint.SetActive(!GameDataManager.Instance.UserData.isHideShopRedPoint);

        // ★ 节日/周末礼包角标：有活动礼包时 sale 显示并展示礼包标签
        RefreshActivityBadge();
    }

    /// <summary>
    /// 刷新商店顶部的节日/周末促销角标：
    ///  - 有活动礼包（节日/周末礼包）：sale 显示，discountText 显示礼包标签；
    ///  - 无活动礼包：sale 隐藏。
    /// </summary>
    /// <summary>
    /// 刷新商店顶部的节日/周末促销角标：
    ///  - 有活动礼包（节日/周末礼包）且未进店消费：sale 显示，discountText 显示"节日X折"或"周末X折"；
    ///  - 无活动礼包 or 已进店消费（跨天自动重置）：sale 隐藏。
    /// </summary>
    private void RefreshActivityBadge()
    {
        ShopEntryBadgeInfo info = null;
        if (ShopManager.shopManager != null)
        {
            // ★ 改为调用 GetHeaderActivityBadge()，返回文案为"节日X折"/"周末X折"，且进店消失
            info = ShopManager.shopManager.GetHeaderActivityBadge();
        }

        bool show = info != null && info.showBadge;

        if (sale != null)
        {
            if (sale.activeSelf != show)
                sale.SetActive(show);
        }

        if (show && discountText != null)
        {
            discountText.text = info.text;
            Debug.Log($"[Shop][HeaderSection] 活动礼包角标: {info.text} (pack={info.pack?.produceNameId})");
        }
        else
        {
            Debug.Log("[Shop][HeaderSection] 无活动礼包或已进店消费，隐藏促销角标");
        }
    }
    
    private IEnumerator AnimateCoinAddition(int amount)
    {
        int startValue = GameDataManager.Instance.UserData.Gold-amount;
        int targetValue = GameDataManager.Instance.UserData.Gold;
        float duration = 0.35f;
        float elapsed = 0f;
        float maxScaleAmount = 0.5f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            int currentValue = Mathf.RoundToInt(Mathf.Lerp(startValue, targetValue, t));
            Goldtxt.text = currentValue.ToString();
            float scaleCurve = Mathf.Sin(t * Mathf.PI); 
            Goldtxt.transform.localScale = _originalGoldTextScale * (1f + scaleCurve * maxScaleAmount);
            yield return null;
        }
        Goldtxt.text = targetValue.ToString();
        Goldtxt.transform.localScale = _originalGoldTextScale;
    }

    protected override void OnEnable()
    {
        if (GoldLeafredpoint != null)
        {
            GoldLeafredpoint.SetActive(ThemeManager.Instance.IsSkinRedPointActive);
        }
        CustomFlyInManager.Instance.GoldObj= GoldImage.gameObject;
        ThemeManager.Instance.OnSkinRedPointChanged += OnRedPointChanged;
        EventDispatcher.instance.OnUpdateLayerCoin += UpdateCoinLayer;
        EventDispatcher.instance.OnChangeGoldUI += InitUI;
        EventDispatcher.instance.OnChangeTopRaycast += ChangeTopRaycast;
        EventDispatcher.instance.OnHighlightHeaderUI += HighlightEnergy;
        EventDispatcher.instance.OnHighlightGoldAndEnergy += HighlightGoldAndEnergy;
        
        bool ishomeshow = SystemManager.Instance.PanelIsShowing(PanelType.PrimaryInterface);
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
        
        if (SystemManager.Instance.PanelIsShowing(PanelType.GamePlayArea)||SystemManager.Instance.PanelIsShowing(PanelType.HexGamePlayArea))
        { 
            BackBtn.gameObject.SetActive(true);
            LevelPuzzleBtn.gameObject.SetActive(false);
            PupaTable.gameObject.SetActive(StageHexController.Instance.CurStageData.PupaDatas!=null);
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
        StartCoroutine(CheckLevelPuzzleVisibility());
    }

    // ======================= 无限体力辅助 =======================

    private bool IsUnlimitedEnergyActive()
    {
        if (GameDataManager.Instance == null || GameDataManager.Instance.UserData == null)
            return false;

        var userData = GameDataManager.Instance.UserData;
        if (string.IsNullOrEmpty(userData.unlimitedEnergyEndTime))
            return false;

        DateTime end;
        if (!DateTime.TryParse(userData.unlimitedEnergyEndTime, out end))
            return false;

        return end > DateTime.Now;
    }

    private double GetUnlimitedEnergyRemainSeconds()
    {
        if (GameDataManager.Instance == null || GameDataManager.Instance.UserData == null)
            return 0;

        var userData = GameDataManager.Instance.UserData;
        if (string.IsNullOrEmpty(userData.unlimitedEnergyEndTime))
            return 0;

        DateTime end;
        if (!DateTime.TryParse(userData.unlimitedEnergyEndTime, out end))
            return 0;

        double remain = (end - DateTime.Now).TotalSeconds;
        return remain > 0 ? remain : 0;
    }

    private string FormatUnlimitedRemainTime(double remainSeconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt((float)remainSeconds));
        int hours = totalSeconds / 3600;
        int mins = (totalSeconds % 3600) / 60;
        int secs = totalSeconds % 60;

        if (hours > 0)
            return string.Format("{0}:{1:00}:{2:00}", hours, mins, secs);
        return string.Format("{0:00}:{1:00}", mins, secs);
    }

    private void RefreshUnlimitedEnergyCache()
    {
        _isUnlimitedEnergyActive = IsUnlimitedEnergyActive();
        _unlimitedEnergyRemainSeconds = _isUnlimitedEnergyActive ? GetUnlimitedEnergyRemainSeconds() : 0;
    }

    // ======================= 原有逻辑 =======================

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
                
                LevelPuzzleBtn.gameObject.SetActive(hasLevelWords);
                LevelPuzzleBtn.GetComponent<CanvasGroup>().DOFade(1f,0.2f);
            }
            yield return new WaitForSeconds(0.3f);  
        }
    }

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
            bool isOldGamePupa = SystemManager.Instance.PanelIsShowing(PanelType.GamePlayArea)||SystemManager.Instance.PanelIsShowing(PanelType.HexGamePlayArea) && 
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
        else if (SystemManager.Instance.PanelIsShowing(PanelType.GamePlayArea)||SystemManager.Instance.PanelIsShowing(PanelType.HexGamePlayArea))
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
            addObj.gameObject.SetActive(true);
            ShopBtn.enabled = isshopbtnEnable;
        }

        if (isshowPupa)
        {
            InitPupaUI();
        }

        RefreshUnlimitedEnergyCache();

        if (SystemManager.Instance.PanelIsShowing(PanelType.ChessPlayArea)
            ||SystemManager.Instance.PanelIsShowing(PanelType.HexGamePlayArea)
            ||SystemManager.Instance.PanelIsShowing(PanelType.GamePlayArea))
        {
            energyBtn.gameObject.SetActive(false);
        }
        else
        {
            energyBtn.gameObject.SetActive(!PupaTable.gameObject.activeInHierarchy && 
                                           !SystemManager.Instance.PanelIsShowing(PanelType.ChessFinishView));
        }

        if (_isUnlimitedEnergyActive)
        {
            energyAdd.SetActive(false);
            energyBtn.interactable = false;
        }
        
        BackBtn.gameObject.SetActive(!SystemManager.Instance.PanelIsShowing(PanelType.ChessPlayArea)&&
                                     !SystemManager.Instance.PanelIsShowing(PanelType.PrimaryInterface));
        PupaTable.interactable = !isshowPupa;
    }

    public void HighlightEnergy(bool isHighlight)
    {
        RefreshUnlimitedEnergyCache();

        energyAdd.SetActive(!isHighlight && !_isUnlimitedEnergyActive);
        Canvas energyCanvas = energyBtn.GetComponent<Canvas>();
        if (energyCanvas == null) energyCanvas = energyBtn.gameObject.AddComponent<Canvas>();
        if (energyBtn.GetComponent<GraphicRaycaster>() == null) energyBtn.gameObject.AddComponent<GraphicRaycaster>();
        if (isHighlight)
        {
            energyBtn.gameObject.SetActive(true);
            energyCanvas.overrideSorting = true;
            energyCanvas.sortingLayerName = UIPanelLayer.TipsPanel;
            energyCanvas.sortingOrder = 101;
            energyBtn.interactable = false;
        }
        else
        {
            energyCanvas.overrideSorting = false;
            energyCanvas.sortingOrder = 0;
            
            if(GameDataManager.Instance==null) return;
            
            var userData = GameDataManager.Instance.UserData;
            int stage = Mathf.Max(userData.CurrentHexStage, userData.CurrentChessStage);
            bool isAdequate = (userData.Energy >= UserData.MAX_NATURAL_ENERGY) || (stage == 1);

            energyBtn.interactable = !isAdequate && !_isUnlimitedEnergyActive;
            energyBtn.gameObject.SetActive(false);
        }
        _isShowEnergyAdd = !isHighlight && !_isUnlimitedEnergyActive;
        energyAdd.SetActive(_isShowEnergyAdd);
    }

    private void HighlightGoldAndEnergy(bool isHighlight)
    {
        if(GameDataManager.Instance==null) return;

        RefreshUnlimitedEnergyCache();
        
        Canvas energyCanvas = energyBtn.GetComponent<Canvas>();
        if (energyCanvas == null) energyCanvas = energyBtn.gameObject.AddComponent<Canvas>();
        if (energyBtn.GetComponent<GraphicRaycaster>() == null) energyBtn.gameObject.AddComponent<GraphicRaycaster>();

        Canvas goldCanvas = ShopBtn.GetComponent<Canvas>();
        if (goldCanvas == null) goldCanvas = ShopBtn.gameObject.AddComponent<Canvas>();
        if (ShopBtn.GetComponent<GraphicRaycaster>() == null) ShopBtn.gameObject.AddComponent<GraphicRaycaster>();

        var userData = GameDataManager.Instance.UserData;
        int stage = Mathf.Max(userData.CurrentHexStage, userData.CurrentChessStage);
        bool isAdequate = (userData.Energy >= UserData.MAX_NATURAL_ENERGY) || (stage == 1);
        
        if (isHighlight)
        {
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
            energyCanvas.overrideSorting = false;
            energyCanvas.sortingOrder = 0;
            energyBtn.interactable = !isAdequate && !_isUnlimitedEnergyActive;
            
            goldCanvas.overrideSorting = false;
            goldCanvas.sortingOrder = 0;
            ShopBtn.interactable = true;
        }
        
        energyAdd.SetActive(!isAdequate && !_isUnlimitedEnergyActive);
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
        if (_energyCoroutine != null) StopCoroutine(_energyCoroutine);
        _energyCoroutine = StartCoroutine(EnergyTimerCoroutine());
    }

    private IEnumerator EnergyTimerCoroutine()
    {
        WaitForSeconds wait = new WaitForSeconds(1f);
        while (true)
        {
            var userData = GameDataManager.Instance.UserData;
            
            userData.CalculateEnergyRegen();

            RefreshUnlimitedEnergyCache();

            int currentEnergy = userData.Energy;
            int stage = Mathf.Max(userData.CurrentHexStage, userData.CurrentChessStage);

            if (_isUnlimitedEnergyActive)
            {
                energyTxt.text = "∞";
                energyTimer.text = FormatUnlimitedRemainTime(_unlimitedEnergyRemainSeconds);
                energyAdd.SetActive(false);
                energyBtn.interactable = false;

                yield return wait;
                continue;
            }

            bool isAdequate = (currentEnergy >= UserData.MAX_NATURAL_ENERGY);
            energyBtn.interactable = true;
            if (isAdequate && stage == 1)
            {
                energyTxt.text = "";
                energyTimer.text = "充足";
                energyAdd.SetActive(false);
                energyBtn.interactable = false;
            }
            else if (currentEnergy >= UserData.MAX_NATURAL_ENERGY)
            {
                energyTxt.text = "";
                energyTimer.text = currentEnergy.ToString();
                energyAdd.SetActive(true);
            }
            else
            {
                energyTxt.text = currentEnergy.ToString();
                
                int remainSeconds = userData.GetNextEnergyRegenSeconds();
                if (remainSeconds <= 0)
                {
                    userData.CalculateEnergyRegen();
                    currentEnergy = userData.Energy;
                    energyTxt.text = currentEnergy.ToString();
                    remainSeconds = userData.GetNextEnergyRegenSeconds();
                }
                
                int mins = remainSeconds / 60;
                int secs = remainSeconds % 60;
                energyTimer.text = string.Format("{0:00}:{1:00}", mins, secs);
                energyAdd.SetActive(_isShowEnergyAdd);
            }
           
            if (stage != 1)
            {
                energyBtn.interactable = _isShowEnergyAdd;
                energyAdd.SetActive(_isShowEnergyAdd);
            }
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
        if (true) 
        {
            SystemManager.Instance.ShowPanel(PanelType.DebugMenu);
        }
    }

    private void OnShopClick()
    {
        ShopManager.shopManager.OpenFixedStore();
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
        
        if (SystemManager.Instance.PanelIsShowing(PanelType.HexGamePlayArea))
        {
            AudioManager.Instance.TriggerVibration(40, 50);
            SystemManager.Instance.HidePanel(PanelType.HexGamePlayArea);
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
    }
    
    public void ChangeBackBtnState(bool isshow)
    {
        BackBtn.gameObject.SetActive(isshow);
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
        EventDispatcher.instance.OnUpdateLayerCoin -= UpdateCoinLayer;
        EventDispatcher.instance.OnChangeGoldUI -= InitUI;
        EventDispatcher.instance.OnChangeTopRaycast -= ChangeTopRaycast;
        EventDispatcher.instance.OnHighlightHeaderUI -= HighlightEnergy;
        EventDispatcher.instance.OnHighlightGoldAndEnergy -= HighlightGoldAndEnergy;
        
        if (ThemeManager.Instance != null)
            ThemeManager.Instance.OnSkinRedPointChanged -= OnRedPointChanged;
        
        LevelPuzzleBtn.gameObject.SetActive(false);
        CancelInvoke(nameof(CheckLevelPuzzleVisibility));
        if(_energyCoroutine != null) StopCoroutine(_energyCoroutine);
        base.OnDisable();
    }
}