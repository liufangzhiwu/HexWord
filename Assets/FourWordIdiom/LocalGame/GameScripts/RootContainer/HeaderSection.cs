using System.Collections;
using Middleware;
using UnityEngine;
using UnityEngine.UI;


public class HeaderSection : UIWindow
{
    public Button GmBtn;
    public Button SetBtn;
    public Button BackBtn;
    public Button ShopBtn;
    public Button PuzzlebookBtn;
    public Button LevelPuzzleBtn;
    public Button PupaTable;
    public GameObject PupaImage;
    public Text Pupatxt; 
    public GameObject GoldImage;
    public Text Goldtxt;       

    // Start is called before the first frame update
    protected void Start()
    {
        InitUI();
        InitializeButtons();       
    }

    private void InitUI(int value=0,bool isanim=false)
    {
        if(value>0&&isanim)
        {
            StartCoroutine(AnimateCoinAddition(value));
        }
        else
        {
            Goldtxt.text = GameDataManager.Instance.UserData.Gold.ToString();
        }
    }
    
    private IEnumerator AnimateCoinAddition(int amount)
    {
        int startValue = GameDataManager.Instance.UserData.Gold-amount;
        int targetValue = GameDataManager.Instance.UserData.Gold;
        float duration = 0.2f; // 动画持续时间
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration); // 归一化
            int currentValue = Mathf.RoundToInt(Mathf.Lerp(startValue, targetValue, t));
            Goldtxt.text = currentValue.ToString();
            yield return null;
        }
        Goldtxt.text = targetValue.ToString(); // 确保最终值正确显示
    }

    protected void InitializeButtons()
    {       
        SetBtn.AddClickAction(OnSetClick);
        BackBtn.AddClickAction(OnBackClick);
        ShopBtn.AddClickAction(OnShopClick);
#if Unity_ShowLog || UNITY_EDITOR
        GmBtn.AddClickAction(OnGmClick, "", false);
#endif
        PuzzlebookBtn.AddClickAction(OnClickPuzzleVocabulary);
        LevelPuzzleBtn.AddClickAction(OnClickStagePuzzleScreen);
    }

    protected override void OnEnable()
    {
         EventDispatcher.instance.OnUpdateLayerCoin += UpdateCoinLayer;
        EventDispatcher.instance.OnChangeGoldUI += InitUI;
         EventDispatcher.instance.OnChangeTopRaycast += ChangeTopRaycast;
        bool ishomeshow = SystemManager.Instance.PanelIsShowing(PanelType.PrimaryInterface);
        PuzzlebookBtn.gameObject.SetActive(ishomeshow&& GameDataManager.Instance.UserData.isShowVocabulary);
        GmBtn.gameObject.SetActive(ishomeshow);
        BackBtn.gameObject.SetActive(!ishomeshow);
        SetBtn.gameObject.SetActive(ishomeshow);
        
        CustomFlyInManager.Instance.GoldObj=GoldImage.gameObject;

        if (SystemManager.Instance.PanelIsShowing(PanelType.StageFinishView))
        {
            BackBtn.GetComponent<Image>().sprite =AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("UI_Icon_Home");
        }
        else
        {
            BackBtn.GetComponent<Image>().sprite =AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("UI_Icon_back");
        }
        
        if (SystemManager.Instance.PanelIsShowing(PanelType.HexGamePlayArea))
        {
            ShopBtn.gameObject.SetActive(false);
            PupaTable.gameObject.SetActive(StageHexController.Instance.CurStageData.PupaDatas!=null);
        }
        else
        {
            ShopBtn.gameObject.SetActive(true);
            PupaTable.gameObject.SetActive(false);
        }
        
         EventDispatcher.instance.TriggerChangeTopRaycast(true);
         EventDispatcher.instance.TriggerChangeGoldUI(0,false);       

        LevelPuzzleBtn.gameObject.SetActive(SystemManager.Instance.PanelIsShowing(PanelType.StageFinishView));

        if (SystemManager.Instance != null)
        {
            // 启用时开始重复调用 (0秒延迟，每秒1次)
            InvokeRepeating(nameof(CheckLevelPuzzleVisibility), 1f, 1f);
        }
    }
    
    private void CheckLevelPuzzleVisibility()
    {
        if (SystemManager.Instance != null)
        {
            bool isgameshow = SystemManager.Instance.PanelIsShowing(PanelType.HexGamePlayArea);
    
            if (isgameshow)
            {
                bool hasLevelWords = StageHexController.Instance.CurStageData.FoundTargetPuzzles.Count > 0;
                LevelPuzzleBtn.gameObject.SetActive(hasLevelWords);
            }
        }
    }

    /// <summary>
    /// 更改金币显示层级
    /// </summary>
    private void UpdateCoinLayer(bool istop,bool isshopbtnEnable=true)
    {
        GameObject coinObj = ShopBtn.gameObject;
        Canvas canvas= coinObj.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas= coinObj.AddComponent<Canvas>();
            coinObj.AddComponent<GraphicRaycaster>();
        }
           
        if (istop)
        {
            canvas.overrideSorting=true;
            canvas.sortingLayerName="TipsPanel";
            canvas.sortingOrder=100;
        }
        else
        {
            canvas.overrideSorting=true;
            canvas.sortingLayerName="TopPanel";
            canvas.sortingOrder=0;
        }
        
        ShopBtn.enabled = isshopbtnEnable;
    }
    
    private void OnClickPuzzleVocabulary()
    {
        StageHexController.Instance.IsEnterVocabulary = false;
        SystemManager.Instance.ShowPanel(PanelType.WordVocabularyScreen);
    }
    
    private void OnClickStagePuzzleScreen()
    {
        StageHexController.Instance.IsEnterVocabulary = true;
        SystemManager.Instance.ShowPanel(PanelType.LevelWordScreen);
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
    }

    private void OnShopClick()
    {
        if (GameCoreManager.Instance.PanelState == PanelState.MainMenuPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.PrimaryInterface);
        }else if (GameCoreManager.Instance.PanelState == PanelState.FinishPanel)
        {
            //SystemManager.Instance.HidePanel(PanelType.StageFinishView);
        }
       
        UnityTimer.Delay(0.2f, () =>
        {
            SystemManager.Instance.HidePanel(PanelType.HeaderSection);
        });
        
        UnityTimer.Delay(0.5f, () =>
        {
            SystemManager.Instance.ShowPanel(PanelType.ShopScreen);
        });
      
        // SystemManager.Instance.ShowPanel(PanelType.RewardAdsScreen);
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
    }

    public void ChangeBackBtnState(bool isshow)
    {
        BackBtn.gameObject.SetActive(isshow);
        SetBtn.gameObject.SetActive(!isshow);
        LevelPuzzleBtn.gameObject.SetActive(!isshow);
    }

    private void ChangeTopRaycast(bool isblock)
    {
        transform.GetComponent<CanvasGroup>().blocksRaycasts = isblock;
    }

    public void ShowPupaTableAnima(Transform startPoint, int pupa = 1, Transform parent  = null)
    {
        
        ButterflyGrow butterflyGrow =ButterfliesManager.Instance.GetCurrentGrow();
        if(butterflyGrow == null)
            return;

        StartCoroutine(ButterfliesManager.Instance.FlyPupaCoroutine(startPoint, PupaImage.transform, () =>
        {
            GameDataManager.Instance.ButterflyData.AddPupa(pupa);
            Pupatxt.text = $"{GameDataManager.Instance.ButterflyData.currPupa} / {butterflyGrow.Count}";
        }, 0.6f));


    }

    public override void OnHideAnimationEnd()
    {
        base.OnHideAnimationEnd();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        //EventDispatcher.ChangeBackBtnHandler -= ChangeBackBtnState;
        EventDispatcher.instance.OnUpdateLayerCoin -= UpdateCoinLayer;
        EventDispatcher.instance.OnChangeGoldUI -= InitUI;
        EventDispatcher.instance.OnChangeTopRaycast -= ChangeTopRaycast;
        CustomFlyInManager.Instance.GoldObj = null;
        // 禁用时取消调用
        //CancelInvoke(nameof(CheckLevelPuzzleVisibility));
    }

}



