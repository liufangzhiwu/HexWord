using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Middleware;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;


public class DashCompetition : UIWindow
{               
    [SerializeField] private Button CloseBtn; // 关闭按钮    
    [SerializeField] private Button HelpBtn; // 关闭按钮    
    [SerializeField] private Image Background; // 关闭按钮    
  
          
    [SerializeField] private GameObject BoxsParent;        
    [SerializeField] private Image titleImage;        
    [SerializeField] private Text timeText;        
    [SerializeField] private Text tipsText; 
    [SerializeField] private GameObject FishLists;   
    private FishItem _fishItemPrefab;
    private List<FishItem> fishItems=new List<FishItem>();
    private ObjectPool objectPool; // 对象池实例
    private FishItem userfishItem;
    
    public Image oneBoxImage;        
    public Image twoBoxImage;        
    public Image threeBoxImage; 
    
    protected void Start()
    {
        LoadPanelUI();
        
        if (_fishItemPrefab == null)
        {
            _fishItemPrefab = AssetBundleLoader.SharedInstance.LoadGameObject("commonitem", "FishItem").GetComponent<FishItem>();
        }
    
        // 初始化对象池
        objectPool = new ObjectPool(_fishItemPrefab.gameObject, ObjectPool.CreatePoolContainer(transform, "FishItemPool"));
       
        CrateFishAIItem();
        InitButton();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        FishInfoController.Instance.dashparent = this;
        InitUI();
        Game.self?.Ads?.HideBanner();
        FishInfoController.Instance.OnFishTimeUpdated += UpdateFishTime;
        FishInfoController.Instance.OnFishMatchOver += OnClosePanel;

        if (fishItems.Count >= 4)
        {
            StartCoroutine(ShowFishAnim(0.5f));
            //StartCoroutine(UpdateFishItemUI());
        }
        CheckUserName();
        AudioManager.Instance.PlaySoundEffect("ShowUI");
        
        EventDispatcher.instance.TriggerUpdateLayerCoin(false,false);
    }

    private void CheckUserName()
    {
        if (!GameDataManager.Instance.UserData.isChangeUserName)
        {
            SystemManager.Instance.ShowPanel(PanelType.HeadScreen);
            GameDataManager.Instance.UserData.isChangeUserName = true;
        }
    }
    
    private void CrateFishAIItem()
    {
        int round= GameDataManager.Instance.FishUserSave.curround;            

        // 从对象池获取 ShopItem 对象
        userfishItem = objectPool.GetObject<FishItem>(FishLists.transform);
        // 赋值 FishItem 的数据
        userfishItem.SetUserFishData(this);
        List<FishAISaveData> fishAISaveDatas = FishInfoController.Instance.GetRoundFishaiSaveItems(round);
        int i = 0;
        foreach (FishAISaveData taskSaveData in fishAISaveDatas)
        {
            // 从对象池获取 ShopItem 对象
            FishItem fishItem = objectPool.GetObject<FishItem>(FishLists.transform);
            // 赋值 FishItem 的数据
            fishItem.SetAiFishData(taskSaveData,this);
            
            if (i == fishAISaveDatas.Count - 1)
            {
                fishItem.line.gameObject.SetActive(false);
            }
            fishItems.Add(fishItem);
            i++;
        }
        
        StartCoroutine(ShowFishAnim(2.1f ));
    }

    IEnumerator ShowFishAnim(float time)
    {
        yield return new WaitForSeconds(0.12f);
        userfishItem.GetComponent<Animator>().enabled = true;
        yield return new WaitForSeconds(0.12f);
        
        foreach (FishItem fishItem in fishItems)
        {
            fishItem.GetComponent<Animator>().enabled = true;
            fishItem.wordbg.transform.localScale = Vector3.zero;
            yield return new WaitForSeconds(0.12f);
        }
        
        userfishItem.wordbg.transform.DOScale(Vector3.one, 0.2f);
        foreach (FishItem fishItem in fishItems)
        {
            fishItem.wordbg.transform.DOScale(Vector3.one, 0.2f);
        }
        yield return new WaitForSeconds(time);
        StartCoroutine(UpdateFishItemUI());
    }

    private IEnumerator UpdateFishItemUI()
    {
        // 前置条件检查：确保必要元素存在且比赛未结束
        if(fishItems.Count < 4) 
            yield break;
        
        bool progressComplete=FishInfoController.Instance.RoundFishIsOver();
        
        //
        // if (progressComplete)
        // {
        //     // 优化动画触发条件
        //     StartCoroutine(PlayerBoxFlyAnim(null));
        //     yield break;
        // }

        // 优化循环间隔为1秒，提升响应速度
        var waitInterval = new WaitForSeconds(1);

        // 提取重复使用的SaveData引用
        var fishSave = GameDataManager.Instance.FishUserSave;

        // 初始化AI数据（仅执行一次）
        if(fishSave.aiSaveDatas.Count == 0)
        {
            InitializeAIData(fishSave.curround);
        }

        //更新玩家UI
        userfishItem.UpdateUI(true);       

        while (!fishSave.iscliam)
        {
            progressComplete=FishInfoController.Instance.RoundFishIsOver();
            FishInfoController.Instance.RoundResultFishRank();
            // 合并UI更新逻辑
            UpdateAllFishUI();
            
            if (progressComplete)
            {
                // 优化动画触发条件
                if (SystemManager.Instance.PanelIsShowing(PanelType.DashCompetition))
                {             
                    yield return new WaitForSeconds(0.9f);
                    StartCoroutine(PlayerBoxFlyAnim(null));
                }
            }
            yield return waitInterval;
        }
    }
   
    // 提取AI初始化逻辑
    private void InitializeAIData(int round)
    {
        var taskSaveDatas = FishInfoController.Instance.GetRoundFishaiSaveItems(round);
        
        // 添加集合边界检查
        for (int i = 0; i < Mathf.Min(taskSaveDatas.Count, fishItems.Count); i++)
        {
            fishItems[i].SetAiFishData(taskSaveDatas[i], this);
            if (i == fishItems.Count - 1)
            {
                fishItems[i].line.gameObject.SetActive(false);
            }
        }
    }

    // 合并UI更新逻辑
    private void UpdateAllFishUI()
    {
        //progressComplete = false;
        //var fishSave = GameDataManager.MainInstance.FishUserSave;

        //更新玩家UI
        userfishItem.UpdateUI(false);
        //bool playerComplete = CheckProgress(userfishItem, fishSave.Puzzleprogress);

        // 更新AI UI
        foreach (var fishItem in fishItems)
        {
            fishItem.UpdateUI();
            //bool aiComplete = CheckProgress(fishItem, fishItem.fishaiSaveData.Puzzleprogress);
            
            //if(aiComplete) progressComplete = true;
        }

       // progressComplete |= playerComplete;
    }

    // 封装进度检查逻辑
    private bool CheckProgress(FishItem item, int progress)
    {
        return progress >= 100 && !item.isclaim;
    }

    private IEnumerator PlayerBoxFlyAnim(Action onComplete)
    {
        List<FishRankInfo> rankInfos = new List<FishRankInfo>();
        rankInfos= FishInfoController.Instance.RoundResultFishRank();
        yield return new WaitForSeconds(0.5f);
        if (rankInfos.Count>0)
        {
            for (int i = 0; i < rankInfos.Count; i++)
            {
                FishRankInfo rankInfo = rankInfos[i];
                if (rankInfo.IsPlayer&&!GameDataManager.Instance.FishUserSave.iscliam)
                {
                    Debug.Log("飞入动画" + rankInfo.name + "排名:" + rankInfo.Rank);
                    userfishItem.FlyBoxToTarget(GameDataManager.Instance.FishUserSave.rank);
                    //yield return new WaitForSeconds(0.5f);
                }
                else
                {
                    FishItem fishItem = fishItems.Find((item)=>item.fishaiSaveData.ainame==rankInfo.name);
                    if (fishItem!=null&&!fishItem.isclaim)
                    {
                        Debug.Log("飞入动画"+rankInfo.name+"排名:"+rankInfo.Rank);
                        fishItem.FlyBoxToTarget(fishItem.fishaiSaveData.rank);
                        //yield return new WaitForSeconds(0.5f);
                    }
                }
            }
            
            if (GameDataManager.Instance.FishUserSave.isRoundOver)
            {
                if (GameDataManager.Instance.FishUserSave.rank <= 3&&GameDataManager.Instance.FishUserSave.rank >0)
                {
                    yield return new WaitForSeconds(1.2f);
                    SystemManager.Instance.ShowPanel(PanelType.MatchSuccess);
                }
                else
                {
                    yield return new WaitForSeconds(0.5f);
                    SystemManager.Instance.ShowPanel(PanelType.CompetitionFail);
                }
                onComplete?.Invoke();
            }
        }
       
    }

    private void LoadPanelUI()
    {
        // 获取 RectTransform 组件
        RectTransform rectTransform =FishLists.GetComponent<RectTransform>();
        if (UIUtilities.IsiPad())
        {
            Background.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("fishbigbg");
            FishLists.GetComponent<VerticalLayoutGroup>().spacing = -40;
            BoxsParent.transform.localScale = new Vector3(1.071f, 1.071f, 1f);
            BoxsParent.GetComponent<HorizontalLayoutGroup>().spacing =15f;
            BoxsParent.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -685);
            titleImage.transform.parent.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -970);
         
            // 设置偏移值
            rectTransform.offsetMax = new Vector2(0, -1120.8f); // right 和 top
            rectTransform.offsetMin = new Vector2(0, 1543.05f); // Left 和 Bottom
        }
        else
        {
            Background.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("fishbg");
            float scale=UIUtilities.GetScreenRatio();
            FishLists.GetComponent<VerticalLayoutGroup>().spacing =Math.Clamp(-40 * scale, -40,100);
            BoxsParent.transform.localScale = Vector3.one*scale;
            BoxsParent.GetComponent<HorizontalLayoutGroup>().spacing = 40f*scale;
            BoxsParent.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -670);
            titleImage.transform.parent.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -985);
            // 设置偏移值
            //rectTransform.offsetMax = new Vector2(0, -1180); // right 和 top
            rectTransform.offsetMax = new Vector2(0, -1180); // right 和 top
            rectTransform.offsetMin = new Vector2(0, 1683); // Left 和 Bottom
        }
        
        //titleImage.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromBundle(ToolUtil.GetLanguageBundle(),"ui_dashTitle");
    }
    
    private void InitUI()
    {
        int round= GameDataManager.Instance.FishUserSave.curround;
        FishaiInfoItem infoItem = FishInfoController.Instance.GetCurrFishItem(round);
        tipsText.text= string.Format( MultilingualManager.Instance.GetString("CarpMatchDes"), infoItem?.wordCount ?? 100 );
    }
   
    private void UpdateFishTime(string time="")
    {
        timeText.text = time;
    }

    protected void InitButton()
    {
        CloseBtn.AddClickAction(OnClosePanel); // 绑定关闭按钮事件
        HelpBtn.AddClickAction(OnHelpPanel);
    }
 
    private void OnHelpPanel()
    {
        SystemManager.Instance.ShowPanel(PanelType.CompetitionHelp);
    }

    private void OnClosePanel()
    {
        userfishItem.OnHide();

        // 更新AI UI
        foreach (var fishItem in fishItems)
        {
            fishItem.OnHide();
        }
        
        if (GameCoreManager.Instance.PanelState == PanelState.MainMenuPanel)
        {
            SystemManager.Instance.ShowPanel(PanelType.PrimaryInterface);
        }else if (GameCoreManager.Instance.PanelState == PanelState.FinishHexPanel)
        {
            //SystemManager.Instance.ShowPanel(PanelType.StageFinishView);
        }

        SystemManager.Instance.ShowPanel(PanelType.HeaderSection);
        
        base.Close(); // 隐藏面板
    }
    
    public override void OnHideAnimationEnd()
    {
       
        base.OnHideAnimationEnd();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        FishInfoController.Instance.OnFishTimeUpdated -= UpdateFishTime;
        FishInfoController.Instance.OnFishMatchOver -= OnClosePanel;
        
        EventDispatcher.instance.TriggerUpdateLayerCoin(true,true);
    }
}
