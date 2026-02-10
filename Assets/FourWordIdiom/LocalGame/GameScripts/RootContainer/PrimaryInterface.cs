using System.Collections;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// 主界面控制器 - 处理游戏主界面的UI逻辑和交互
/// </summary>
public class PrimaryInterface : UIWindow
{
    [Header("UI组件")]
    [SerializeField] private Button GameStageBtn;          // 开始游戏按钮
    [SerializeField] private Button ModeBtn;          // 模式选择按钮
    [SerializeField] private Animator ModeIndicator;
    [SerializeField] private GameObject hardStageTable;          // 困难模式
    [SerializeField] private GameObject extrahardStageTable;          // 特别困难模式
    [SerializeField] private Image logo;       // 文字类型组件
    [SerializeField] private Text Stagetxt;           // 关卡文本
    [Header("UI LimitTime")]
    [SerializeField] private Button LimitTimeBtn;
    [SerializeField] private GameObject LimitTimeObj;
    [SerializeField] private GameObject LimitClaim;
    [SerializeField] private GameObject Worddouble;
    [SerializeField] private Text timetxt;
    [SerializeField] private Image limitOver;
    [Header("UI Task")]
    [SerializeField] private GameObject TaskClaim;
    [SerializeField] private Button TasksBtn;
    [SerializeField] private Text tasktimetxt;
    [SerializeField] private Image taskOver;
    [Header("UI Sign")]
    [SerializeField] private Button SignInBtn;
    [Header("UI Fish")]
    [SerializeField] private Button FishBtn;
    [SerializeField] private GameObject FishClaim;
    [SerializeField] private Image fishwifiimage;
    [SerializeField] private Text fishtimetext;
    [SerializeField] private Image fishrankimage;
    [SerializeField] private Text fishrankcount;
    [Header("UI Head")]
    [SerializeField] private Button HeadBtn;
    [SerializeField] private Image starticon;
    [SerializeField] private Image headicon;
    [Header("UI Butterfly")]
    [SerializeField] private GameObject ButterflyTime;
    [Header("蝶园")]
    [SerializeField] private Button ButterflyBtn;
    [Header("配置参数")]
    [SerializeField] private float topPanelDelay = 0.01f; // 顶部面板显示延迟时间
       

    /// <summary>
    /// 初始化按钮事件
    /// </summary>
    protected override void InitializeUIComponents()
    {      
        GameStageBtn.AddClickAction(OnPlayClick);
        // ModeBtn.AddClickAction(OnModeClick);
        SignInBtn.AddClickAction(ShowSignInPanel);
        LimitTimeBtn.AddClickAction(ClickLimintTime);
        TasksBtn.AddClickAction(OnTaskClick);
        HeadBtn.AddClickAction(OnHeadClick);
        FishBtn.AddClickAction(OnFishClick);
        ButterflyBtn.AddClickAction(OnButterflyClick);
    }

    private void Start()
    {
        StartCoroutine(DelayLoadRoutine());
    }

    /// <summary>
    /// 当对象启用时调用
    /// </summary>
    protected override void OnEnable()
    {
        GameCoreManager.Instance.PanelState = PanelState.MainMenuPanel;
        
        EnhancedVideoController.Instance.PlayVideo();
        LimitTimeManager.Instance.OnLimitTimeBtnUI += InitLimtBtnUI;
        DailyTaskManager.Instance.OnDailyTaskBtnUI += UpdateDailyTaskBtnUI;
        DailyTaskManager.Instance.OnDailyButterflyTaskUI += UpdateButterflyTime;
        EventDispatcher.instance.OnUpdateGameLobbyUI += UpdateGameLobbyUI;
        FishInfoController.Instance.OnFishTimeUpdated += UpdateFishTime;
        EventDispatcher.instance.OnChangeHeadIconUpdateUI += UpdateHeadBtnUI;
        //EventManager.OnChangeLanguageUpdateUI += InitUI;
        UpdateGameLobbyUI();
        UpdateHeadBtnUI();
        StartCoroutine(UpdateFishRankUI());
    }

    private void UpdateGameLobbyUI()
    {
        InitUI();
        CheckButtonsIsOpen();
        StartCoroutine(ShowTopPanel());
        UpdateTaskBtnUI();
    }
    
    private IEnumerator UpdateFishRankUI()
    {
        CheckFishBtn();
        FishClaim.gameObject.SetActive(false);
        // 提取重复使用的SaveData引用
        //var fishSave = GameDataManager.MainInstance.FishUserSave;
        FishInfoController.Instance.RoundResultFishRank();
        UpdateFishRank();
        
        while (FishBtn.gameObject.activeSelf)
        {
            yield return new WaitForSeconds(0.5f);
            FishInfoController.Instance.RoundResultFishRank();
            UpdateFishRank();
        }
    }
    
    private void CheckFishBtn()
    {
        if (GameDataManager.Instance.UserData.CurrentHexStage >= AppGameSettings.UnlockRequirements.FishOpenLevel
            ||GameDataManager.Instance.UserData.CurrentChessStage >= AppGameSettings.UnlockRequirements.FishOpenLevel
            || !string.IsNullOrEmpty(GameDataManager.Instance.FishUserSave.opentime))
        {
            FishBtn.gameObject.SetActive(FishInfoController.Instance.GetOpenFishFunction());
        }
        else
        {
            FishBtn.gameObject.SetActive(false);
        }
    }
    
    
    /// <summary>
    /// 点击选择游戏模式
    /// </summary>
    private void OnModeClick()
    {
        SystemManager.Instance.HidePanel(PanelType.HeaderSection);
        _windowAnimator.SetBool("IsCollapse", true);
        SystemManager.Instance.ShowPanel(PanelType.SelectMode);
    }
    
    private async void OnFishClick()
    {
        if (Game.IsNetworkActive)
        {
            if (string.IsNullOrEmpty(GameDataManager.Instance.FishUserSave.roundstarttime))
            {
                 SystemManager.Instance.ShowPanel(PanelType.CompetitionStart);
            }
            else
            {
                await GameCoreManager.Instance.SwitchToState(GameState.Fish); 
            }
        }
        else
        {
            MessageSystem.Instance.ShowTip(MultilingualManager.Instance.GetString("RestorePurchasesTips01"), false);
        }
       
    }
    
    private void OnHeadClick()
    {
        SystemManager.Instance.ShowPanel(PanelType.HeadScreen);
    }
    
    private void UpdateFishTime(string time="")
    {
        fishtimetext.text = time;
        if (GameDataManager.Instance.UserData.CurrentHexStage >= AppGameSettings.UnlockRequirements.FishOpenLevel||
            GameDataManager.Instance.UserData.CurrentChessStage >= AppGameSettings.UnlockRequirements.FishOpenLevel||
            !string.IsNullOrEmpty(GameDataManager.Instance.FishUserSave.opentime))
        {
            FishBtn.gameObject.SetActive(FishInfoController.Instance.GetOpenFishFunction());
        }
    }
    
    
    private void UpdateFishRank()
    {
        if (Game.IsNetworkActive)
        {
            fishwifiimage.gameObject.SetActive(false);
            if (GameDataManager.Instance.FishUserSave.rank > 0&& !string.IsNullOrEmpty(GameDataManager.Instance.FishUserSave.roundstarttime))
            {
                int rank = GameDataManager.Instance.FishUserSave.rank;
                switch (rank)
                {
                    case 1:
                        fishrankimage.sprite = LoadheadIcon("fishbtnred");
                        break;
                    case 2:
                        fishrankimage.sprite = LoadheadIcon("fishbtnblue");
                        break;
                    case 3:
                        fishrankimage.sprite = LoadheadIcon("fishbtngreen");
                        break;
                    case 4:
                    case 5:
                        fishrankimage.sprite = LoadheadIcon("fishbtngrey");
                        break;
                }

                if (!FishInfoController.Instance.RoundFishIsOver())
                {
                    fishtimetext.transform.parent.gameObject.SetActive(true);
                    fishrankimage.gameObject.SetActive(true);
                    fishrankcount.text = rank.ToString();
                }
                else
                {
                    FishClaim.gameObject.SetActive(true);
                    fishtimetext.transform.parent.gameObject.SetActive(false);
                    fishrankimage.gameObject.SetActive(false);
                }
            }
            else
            {
                fishtimetext.transform.parent.gameObject.SetActive(true);
                fishrankimage.gameObject.SetActive(false);
                FishClaim.gameObject.SetActive(false);
            }
        }
        else
        {
            fishtimetext.transform.parent.gameObject.SetActive(true);
            fishrankimage.gameObject.SetActive(false);
            fishwifiimage.gameObject.SetActive(true);
            FishClaim.gameObject.SetActive(false);
        }
    }

    
    private void UpdateHeadBtnUI()
    {
        if (GameDataManager.Instance.UserData.UserHeadId > 0)
        {
            headicon.sprite = LoadheadIcon("head" + GameDataManager.Instance.UserData.UserHeadId);
            headicon.transform.gameObject.SetActive(true);
            starticon.transform.gameObject.SetActive(false);
        }
        else
        {
            headicon.transform.gameObject.SetActive(false);
            starticon.transform.gameObject.SetActive(true);
        }
    }
    
    private void UpdateTaskBtnUI()
    {
         if (LimitTimeManager.Instance.IsComplete())
         {
             TaskClaim.gameObject.SetActive(false);
         }
        TasksBtn.gameObject.SetActive(GameDataManager.Instance.UserData.CurrentHexStage >= AppGameSettings.UnlockRequirements.DailyMissions);
        TaskClaim.GetComponentInChildren<Text>().text= MultilingualManager.Instance.GetString("ADPopReceive");
    }
    
    private void UpdateDailyTaskBtnUI()
    {
        if (!DailyTaskManager.Instance.IsAllComplete())
        {
            if (!DailyTaskManager.Instance.IsClaim())
            {
                if (TaskClaim.activeSelf)
                {
                    TaskClaim.gameObject.SetActive(false);
                    TaskClaim.GetComponent<CanvasGroup>().alpha = 0;
                }
            }
            else
            {
                TaskClaim.gameObject.SetActive(true);
                TaskClaim.GetComponent<CanvasGroup>().DOFade(1,0.2f);
                tasktimetxt.gameObject.SetActive(false);
            }
            taskOver.gameObject.SetActive(false);
        }
        else
        {
            tasktimetxt.gameObject.SetActive(false);
            TaskClaim.gameObject.SetActive(false);
            taskOver.gameObject.SetActive(true);
        }
    }

    private void CheckButtonsIsOpen()
    {
        HeadBtn.gameObject.SetActive(GameDataManager.Instance.UserData.CurrentHexStage >= AppGameSettings.UnlockRequirements.HeadOpenLevel);
        TasksBtn.transform.parent.gameObject.SetActive(GameDataManager.Instance.UserData.CurrentHexStage>= AppGameSettings.UnlockRequirements.DailyMissions);
        
        LimitTimeBtn.transform.parent.gameObject.SetActive(GameDataManager.Instance.UserData.CurrentHexStage >= AppGameSettings.UnlockRequirements.TimeLimitMode
        ||!string.IsNullOrEmpty(GameDataManager.Instance.UserData.limitOpenTime));
        
        SignInBtn.gameObject.SetActive(GameDataManager.Instance.UserData.CurrentHexStage >= AppGameSettings.UnlockRequirements.SignInRewards
        ||!string.IsNullOrEmpty(GameDataManager.Instance.UserData.signOpenTime));
        
        ButterflyBtn.gameObject.SetActive(ButterfliesManager.Instance.IsOpen);
    }
    
    private void UpdateTimeDisplay(string time)
    {
        if (!string.IsNullOrEmpty(time))
        {
            if (!LimitTimeManager.Instance.IsClaim())
            {
                timetxt.text = time; // 更新文本
            }
        }
    }
    
    private void UpdateLimintBtnUI()
    {
        if (!LimitTimeManager.Instance.IsComplete())
        {
            LimitTimeManager.Instance.OnLimitTimeUpdated += UpdateTimeDisplay; // 订阅事件
        }
    
        if (!DailyTaskManager.Instance.IsAllComplete())
        {
            LimitTimeManager.Instance.OnDailyTimeUpdated += UpdateDailyTaskTimeDisplay; // 订阅事件
        }
    
        InitLimtBtnUI();
    }
    
    private void UpdateDailyTaskTimeDisplay(string time)
    {
        bool shouldActivate = !string.IsNullOrEmpty(time) && !DailyTaskManager.Instance.IsAllComplete()
                                                          &&!DailyTaskManager.Instance.IsClaim();
    
        // 设置 tasktimetxt 的激活状态
        if (tasktimetxt.gameObject.activeSelf != shouldActivate)
        {
            tasktimetxt.gameObject.SetActive(shouldActivate);
        }
    
        // 如果需要激活，则更新文本
        if (shouldActivate)
        {
            tasktimetxt.text = time;
        }
    }

    private void InitLimtBtnUI(bool isanim=true)
    {
        //LimitTimeObj.gameObject.SetActive(!LimitTimeManager.Instance.IsClaim());
        LimitClaim.gameObject.SetActive(LimitTimeManager.Instance.IsClaim());
    
        if (!LimitTimeManager.Instance.IsComplete())
        {
            if (!LimitTimeManager.Instance.IsClaim())
            {
                Worddouble.gameObject.SetActive(LimitTimeManager.Instance.LimitTimeCanShow());
            
                if (LimitClaim.activeSelf)
                {
                    LimitClaim.gameObject.SetActive(false);
                    LimitClaim.GetComponent<CanvasGroup>().alpha = 0;
                }
            }
            else
            {
                LimitClaim.gameObject.SetActive(true);
                Worddouble.gameObject.SetActive(false);
                LimitClaim.GetComponent<CanvasGroup>().DOFade(1,0.2f);
            }
            limitOver.gameObject.SetActive(false);
        }
        else
        {
            Worddouble.gameObject.SetActive(false);
            timetxt.gameObject.SetActive(false);
            LimitClaim.gameObject.SetActive(false);
            limitOver.gameObject.SetActive(true);
        }
    }
    
    private void ClickLimintTime()
    {
        SystemManager.Instance.ShowPanel(PanelType.LimitTimeScreen);  
    }
    
    private void OnTaskClick()
    {
        SystemManager.Instance.ShowPanel(PanelType.DailyTasksScreen);
    }

    private void ShowSignInPanel()
    {
        SystemManager.Instance.ShowPanel(PanelType.SignWaterScreen);    
    }

    /// <summary>
    /// 当对象禁用时调用
    /// </summary>
    protected override void OnDisable()
    {
        base.OnDisable();
        //EventManager.OnChangeLanguageUpdateUI -= InitUI;
        
         LimitTimeManager.Instance.OnLimitTimeBtnUI -= InitLimtBtnUI;
         DailyTaskManager.Instance.OnDailyTaskBtnUI -= UpdateDailyTaskBtnUI;
         DailyTaskManager.Instance.OnDailyButterflyTaskUI -= UpdateButterflyTime;
         EventDispatcher.instance.OnUpdateGameLobbyUI -=UpdateGameLobbyUI ;
         FishInfoController.Instance.OnFishTimeUpdated -= UpdateFishTime;
         EventDispatcher.instance.OnChangeHeadIconUpdateUI -= UpdateHeadBtnUI;
        
         if (!LimitTimeManager.Instance.IsComplete())
         {
             LimitTimeManager.Instance.OnLimitTimeUpdated -= UpdateTimeDisplay; // 订阅事件
         }
        
         if (!DailyTaskManager.Instance.IsAllComplete())
         {
             LimitTimeManager.Instance.OnDailyTimeUpdated -= UpdateDailyTaskTimeDisplay; // 订阅事件
         }
     
    }

    /// <summary>
    /// 初始化UI
    /// </summary>
    public void InitUI()
    {
        // 设置关卡文本
        int Stage = 0;
        // 设置模式图标
        Sprite sprite = null;
        switch (GameDataManager.Instance.UserData.levelMode)
        {
            // case 1:
            //     Stage = GameDataManager.Instance.UserData.CurrentStage != 0 ? 
            //         GameDataManager.Instance.UserData.CurrentStage : 1;
            //     sprite = LoadheadIcon("icon_xiao");
            //     break;
            case 2:
                Stage = GameDataManager.Instance.UserData.CurrentChessStage;
                //sprite = LoadheadIcon("icon_pinzi");
                break;
            case 3:
                Stage = GameDataManager.Instance.UserData.CurrentHexStage;
                //sprite = LoadheadIcon("icon_layer");
                break;
        }
        
        Stage=Stage==0?1:Stage;
        
        StageHexController.Instance.CurLevelMode = GetLevelDifficulty(Stage);
        
        switch (StageHexController.Instance.CurLevelMode)
        {
            case LevelModes.Normal:
                hardStageTable.gameObject.SetActive(false);
                extrahardStageTable.gameObject.SetActive(false);
                break;
            case LevelModes.Hard:
                hardStageTable.gameObject.SetActive(true);
                extrahardStageTable.gameObject.SetActive(false);
                break;
            case LevelModes.ExtraHard:
                hardStageTable.gameObject.SetActive(false);
                extrahardStageTable.gameObject.SetActive(true);
                break;
        }
        
        Stagetxt.text = MultilingualManager.Instance.GetString("Level")+" " + Stage;
        if(sprite != null)
            ModeBtn.GetComponent<Image>().sprite = sprite;
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

    
    private void UpdateButterflyTime(string time="")
    {
        bool shouldActivate = GameDataManager.Instance.UserData.butterflyTaskIsOpen;
        if (ButterflyTime.activeSelf != shouldActivate)
        {
            ButterflyTime.gameObject.SetActive(shouldActivate);
        }
        
        if(shouldActivate)
            ButterflyTime.GetComponentInChildren<Text>().text=time;
    }

    

    /// <summary>
    /// 显示顶部面板
    /// </summary>
    private IEnumerator ShowTopPanel()
    {
        yield return new WaitForSeconds(topPanelDelay);
        UpdateLimintBtnUI();
        UpdateDailyTaskBtnUI();
        SystemManager.Instance.ShowPanel(PanelType.HeaderSection);
     
        yield return new WaitForSeconds(0.5f);
        if (GameCoreManager.Instance.IsTrueAuto)
        {
            OnEnterStageClick();
        }
    }
    
    private async void OnButterflyClick()
    {
        ButterflyBtn.interactable = false;

        try
        {
            // 此时 UI 线程会被释放（不卡顿），Loading 圈在转，直到资源加载完毕
            await GameCoreManager.Instance.SwitchToState(GameState.Butterfly);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"进入蝴蝶模块失败: {e}");
            if (this != null) // 检查自己是否还活着
            {
                ButterflyBtn.interactable = true;
                MessageSystem.Instance.HideLoadingAnimation();
            }
        }
    }
    
    /// <summary>
    /// 点击开始游戏按钮
    /// </summary>
    public async void OnPlayClick()
    {
        base.Close();
        StageHexController.Instance.SetStageData(StageHexController.Instance.CurrentStage);
        await GameCoreManager.Instance.SwitchToState(GameState.Gameplay);
        // OnEnterStageClick();
        // SystemManager.Instance.HidePanel(PanelType.PrimaryInterface);
        // SystemManager.Instance.HidePanel(PanelType.HeaderSection);
    }
    
    private Sprite LoadheadIcon(string showIcon)
    {
        return AssetBundleLoader.SharedInstance.GetSpriteFromAtlas(showIcon);
    }

    /// <summary>
    /// 进入关卡回调
    /// </summary>
    private void OnEnterStageClick()
    {
        
        switch (GameDataManager.Instance.UserData.levelMode)
        {
            case 1:
                StageHexController.Instance.SetStageData(StageHexController.Instance.CurrentStage);
                break;
            case 2:
                ChessStageController.Instance.SetStageData(ChessStageController.Instance.CurrentStage);
                break;
            case 3:
                StageHexController.Instance.SetStageData(StageHexController.Instance.CurrentStage);
                break;
        }

        switch (GameDataManager.Instance.UserData.levelMode)
        {
            case 1:
                SystemManager.Instance.ShowPanel(PanelType.HexGamePlayArea);
                break;
            case 2:
                SystemManager.Instance.ShowPanel(PanelType.ChessPlayArea);
                break;
            case 3:
                SystemManager.Instance.ShowPanel(PanelType.HexGamePlayArea);
                break;
        }
    }

    public override void OnHideAnimationEnd()
    {
        base.OnHideAnimationEnd();
    }
    private IEnumerator DelayLoadRoutine()
    {
        Debug.Log("场景启动，开始等待...");
        
        // 1. 延迟 3 秒 (挂起协程，不卡主线程)
        yield return new WaitForSeconds(1.0f); 

        // 2. 延迟结束，执行加载
        Debug.Log("1秒已到，开始加载 Bundle！");
        
        // 调用你写的加载器
        // AssetBundleLoader.SharedInstance.LoadAtlas("butterfly_ui","UI_Butterflymaunal");
        // // AssetBundleLoader.SharedInstance.LoadAtlas("butterfly_ui","UI_Butterflyscene");
        // AssetBundleLoader.SharedInstance.LoadAtlas("butterfly_ui","Butterfly_UI");
        // AssetBundleLoader.SharedInstance.GetSpriteFromBundle("butterfly_parts", "BF101-body");
        // AssetBundleLoader.SharedInstance.GetSpriteFromBundle("scenery1", "scenery1");
    }
}