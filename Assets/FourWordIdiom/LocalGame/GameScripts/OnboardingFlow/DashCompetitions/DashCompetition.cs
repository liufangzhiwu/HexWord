using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
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
            _fishItemPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "FishItem").GetComponent<FishItem>();
        }
    
        // 初始化对象池
        objectPool = new ObjectPool(_fishItemPrefab.gameObject, ObjectPool.CreatePoolContainer(transform, "FishItemPool"));
       
        CrateFishAIItem();
        InitButton();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        //FishInfoController.Instance.dashparent = this;
        InitUI();
        //AdsManager.Instance.HideBannerAd();
        //FishInfoController.Instance.OnFishTimeUpdated += UpdateFishTime;
        //FishInfoController.Instance.OnFishMatchOver += OnClosePanel;

        if (fishItems.Count >= 4)
        {
            StartCoroutine(ShowFishAnim(0.5f));
            //StartCoroutine(UpdateFishItemUI());
        }
        CheckUserName();
        AudioManager.Instance.PlaySoundEffect("ShowUI");
    }

    private void CheckUserName()
    {
        // if (!GameDataManager.instance.UserData.isChangeUserName)
        // {
        //     SystemManager.Instance.ShowPanel(PanelType.HeadScreen);
        //     GameDataManager.instance.UserData.isChangeUserName = true;
        // }
    }
    
    private void CrateFishAIItem()
    {
        //int round= GameDataManager.instance.FishUserSave.curround;            

        // 从对象池获取 ShopItem 对象
        userfishItem = objectPool.GetObject<FishItem>(FishLists.transform);
        // 赋值 FishItem 的数据
        userfishItem.SetUserFishData(this);
        //List<FishAISaveData> fishAISaveDatas = FishInfoController.Instance.GetRoundFishaiSaveItems(round);
        int i = 0;
        // foreach (FishAISaveData taskSaveData in fishAISaveDatas)
        // {
        //     // 从对象池获取 ShopItem 对象
        //     FishItem fishItem = objectPool.GetObject<FishItem>(FishLists.transform);
        //     // 赋值 FishItem 的数据
        //     fishItem.SetAiFishData(this);
        //     
        //     if (i == fishAISaveDatas.Count - 1)
        //     {
        //         fishItem.line.gameObject.SetActive(false);
        //     }
        //     fishItems.Add(fishItem);
        //     i++;
        // }
        
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

        bool progressComplete = false;

        if (progressComplete)
        {
            // 优化动画触发条件
            StartCoroutine(PlayerBoxFlyAnim(null));
            yield break;
        }

        // 优化循环间隔为1秒，提升响应速度
        var waitInterval = new WaitForSeconds(1);

        // 提取重复使用的SaveData引用
        //var fishSave = GameDataManager.instance.FishUserSave;

        // 初始化AI数据（仅执行一次）
        // if(fishSave.aiSaveDatas.Count == 0)
        // {
        //     InitializeAIData(fishSave.curround);
        // }
        //
        // //更新玩家UI
        // userfishItem.UpdateUI(true);       
        //
        // while (!fishSave.isRoundOver)
        // {                
        //     FishInfoController.Instance.RoundResultFishRank();
        //     // 合并UI更新逻辑
        //     UpdateAllFishUI();
        //
        //     // 优化动画触发条件
        //     if (progressComplete && 
        //        SystemManager.Instance.PanelIsShowing(PanelType.DashCompetition))
        //     {             
        //         yield return new WaitForSeconds(1.8f);
        //         StartCoroutine(PlayerBoxFlyAnim(null));
        //     }
        //     yield return waitInterval;
        // }
    }
   
    // 提取AI初始化逻辑
    private void InitializeAIData(int round)
    {
        // var taskSaveDatas = FishInfoController.Instance.GetRoundFishaiSaveItems(round);
        //
        // // 添加集合边界检查
        // for (int i = 0; i < Mathf.Min(taskSaveDatas.Count, fishItems.Count); i++)
        // {
        //     fishItems[i].SetAiFishData(taskSaveDatas[i], this);
        //     if (i == fishItems.Count - 1)
        //     {
        //         fishItems[i].line.gameObject.SetActive(false);
        //     }
        // }
    }

    // 合并UI更新逻辑
    private void UpdateAllFishUI()
    {
        //progressComplete = false;
        //var fishSave = GameDataManager.instance.FishUserSave;

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
        // List<FishRankInfo> rankInfos = new List<FishRankInfo>();
        // rankInfos= FishInfoController.Instance.RoundResultFishRank();
        yield return new WaitForSeconds(0.5f);
       
    }

    private void LoadPanelUI()
    {
        // 获取 RectTransform 组件
        RectTransform rectTransform =FishLists.GetComponent<RectTransform>();
        if (UIUtilities.IsiPad())
        {
            Background.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("fishbigbg");
            FishLists.GetComponent<VerticalLayoutGroup>().spacing = -60;
            BoxsParent.transform.localScale = new Vector3(1.051f, 1.051f, 1f);
            BoxsParent.GetComponent<HorizontalLayoutGroup>().spacing =-70;
            BoxsParent.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 595);
            titleImage.transform.parent.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 326);
         
            // 设置偏移值
            rectTransform.offsetMax = new Vector2(0, -1020.8f); // right 和 top
            rectTransform.offsetMin = new Vector2(0, 1543.05f); // Left 和 Bottom
        }
        else
        {
            Background.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("fishbg");
            float scale=UIUtilities.GetScreenRatio();
            // if (scale <= 0.95f)
            // {
                //FishLists.GetComponent<VerticalLayoutGroup>().spacing = -42*scale;
                FishLists.GetComponent<VerticalLayoutGroup>().spacing =Math.Clamp(-70 * scale, -70,100);
                BoxsParent.transform.localScale = Vector3.one;
                BoxsParent.GetComponent<HorizontalLayoutGroup>().spacing = -20f/scale;
                BoxsParent.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 513/scale);
                titleImage.transform.parent.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 315/scale);
                // 设置偏移值
                //rectTransform.offsetMax = new Vector2(0, -1118); // right 和 top
                rectTransform.offsetMax = new Vector2(0, -750/scale); // right 和 top
                rectTransform.offsetMin = new Vector2(0, 1683); // Left 和 Bottom
            //}
            // else
            // {
            //     FishLists.GetComponent<VerticalLayoutGroup>().spacing = -85;
            //     BoxsParent.transform.localScale = Vector3.one;
            //     BoxsParent.GetComponent<HorizontalLayoutGroup>().spacing = 0;
            //     BoxsParent.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 724);
            //     titleImage.transform.parent.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 415);
            //     // 设置偏移值
            //     rectTransform.offsetMax = new Vector2(0, -886); // right 和 top
            //     rectTransform.offsetMin = new Vector2(0, 1492); // Left 和 Bottom
            // }
        }
        
        switch (GameDataManager.Instance.UserData.LanguageCode)
        {
            case "JS":
                titleImage.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("dashJantitle");
                break;  
            case "CT":
                titleImage.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("fanDashTitle");
                break;
        }
    }
    
    private void InitUI()
    {
        tipsText.text= MultilingualManager.Instance.GetString("CarpMatchDes");
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

        base.Close(); // 隐藏面板
    }
    
    public override void OnHideAnimationEnd()
    {
       
        base.OnHideAnimationEnd();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        //FishInfoController.Instance.OnFishTimeUpdated -= UpdateFishTime;
        //FishInfoController.Instance.OnFishMatchOver -= OnClosePanel;
    }
}
