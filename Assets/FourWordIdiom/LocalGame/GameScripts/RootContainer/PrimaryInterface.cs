using DG.Tweening;
using Middleware;
using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;
using Slider = UnityEngine.UI.Slider;

/// <summary>
/// 主界面控制器 - 处理游戏主界面的UI逻辑和交互
/// </summary>
public class PrimaryInterface : UIWindow
{
    [Header("lOGO")]
    [SerializeField] private Image logo;       // 文字类型组件
    
    [Header("UI组件")]
    [SerializeField] private Button GameStageBtn;          // 开始游戏按钮
    [SerializeField] private Text Stagetxt;           // 关卡文本
    [SerializeField] private GameObject hardStageTable;          // 困难模式
    [SerializeField] private GameObject extrahardStageTable;          // 特别困难模式
    [SerializeField] private Button ButterflyBtn;
    [SerializeField] private Button ModeBtn;          // 模式选择按钮
    [SerializeField] private Animator ModeIndicator;
    [SerializeField] private Button ZenRankBtn;         // 禅意排名按钮
    //[SerializeField] private Button HexaBtn;         // 层层消按钮
    //[SerializeField] private Button ScoreboardBtn;        // 积分排行按钮
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
    [SerializeField] private Button SevenSignBtn;
    [SerializeField] private Image notSign;
    [SerializeField] private Image fillSign;
    [Header("UI Fish")]
    [SerializeField] private Button FishBtn;
    [SerializeField] private GameObject FishClaim;
    [SerializeField] private Image fishwifiimage;
    [SerializeField] private Text fishtimetext;
    [SerializeField] private Image fishrankimage;
    [SerializeField] private Text fishrankcount;
    [Header("UI Headers")]
    [SerializeField] private Button HeadBtn;
    [SerializeField] private Image headicon;
    [Header("UI Butterfly")]
    [SerializeField] private GameObject ButterflyTime;
    [SerializeField] private Button myThemeBtn; // 服务协议按钮
    public GameObject GoldLeafredpoint;
    [SerializeField] private GameObject ButterflyRedpoint;
    
    [Header("配置参数")]
    [SerializeField] private float topPanelDelay = 0.01f; // 顶部面板显示延迟时间
    
    private void Start()
    {
        logo.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromBundle(ToolUtil.GetLanguageBundle(),"ui_logo");
        if (!GameDataManager.Instance.UserData.IsFirstLaunch)
        {
            ModeIndicator.speed = 0.5f;
        }
    }


    /// <summary>
    /// 初始化按钮事件
    /// </summary>
    protected override void InitializeUIComponents()
    {      
        GameStageBtn.AddClickAction(OnPlayClick);
        ModeBtn.AddClickAction(OnModeClick);
        SevenSignBtn.AddClickAction(ShowSevenSignScreen);
        LimitTimeBtn.AddClickAction(ClickLimintTime);
        TasksBtn.AddClickAction(OnTaskClick);
        HeadBtn.AddClickAction(OnHeadClick);
        FishBtn.AddClickAction(OnFishClick);

        //ScoreboardBtn.AddClickAction(OnScoreboardClick);
        ButterflyBtn.AddClickAction(OnButterflyClick);
        //HexaBtn.AddClickAction(ClickHexaBtnClick);
        
        myThemeBtn.AddClickAction(OnClickMyThemeBtn);
    }
    
    /// <summary>
    /// 当对象启用时调用
    /// </summary>
    protected override void OnEnable()
    {
        InitUI();
        GameCoreManager.Instance.PanelState = PanelState.MainMenuPanel;
        //EnhancedVideoController.Instance.PlayVideo();
        LimitTimeManager.Instance.OnLimitTimeBtnUI += InitLimtBtnUI;
        DailyTaskManager.Instance.OnDailyTaskBtnUI += UpdateDailyTaskBtnUI;
        DailyTaskManager.Instance.OnDailyButterflyTaskUI += UpdateButterflyTime;
        EventDispatcher.instance.OnChangeHeadIconUpdateUI += UpdateHeadBtnUI;
        FishInfoController.Instance.OnFishTimeUpdated += UpdateFishTime;
        EventDispatcher.instance.OnChangeGoldUI += InitUI;
        
        CheckButtonsIsOpen();
        StartCoroutine(ShowTopPanel());
        UpdateTaskBtnUI();
        UpdateHeadBtnUI();
        StartCoroutine(UpdateFishRankUI());
        StartCoroutine(CheckZenRankBtn());
        Game.self.Ads?.HideBanner();
        
        StartCoroutine(CheckLobbyPopupsRoutine());

        DailyTaskManager.Instance.UpdateMaxButterflyTime();
        
        GoldLeafredpoint?.SetActive(ThemeManager.Instance.IsSkinRedPointActive);
        ThemeManager.Instance.OnSkinRedPointChanged += OnRedPointChanged;
        
        GameCoreManager.Instance.SetBackgroundImage(new Color(1,1,1,1));
        
        SevenSignBtn.gameObject.SetActive(StreakManager.Instance.UnlockStreak());
    }
    
    private void OnRedPointChanged(bool show)
    {
        if (GoldLeafredpoint != null)
            GoldLeafredpoint.SetActive(show);
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
    }
    
    // ==========================================
    // 🌟 新增：专门管理大厅“按顺序弹窗”的协程
    // ==========================================
    private IEnumerator CheckLobbyPopupsRoutine()
    {
        //HexaBtn.gameObject.SetActive(false);
        // 稍微等一下（0.3秒），让大厅的底图和头部的 TopPanel 先加载完
        // 这样弹窗出来时，背后的画面是完整的，视觉体验更好
        yield return new WaitForSeconds(0.3f);
        
        //更改为15关之后出现（16开始出现）
        // HexaBtn.gameObject.SetActive(ChessStageController.Instance.CurrentStage>15||StageController.Instance.CurrentStage>15 );
      
        // ==========================================
        // 1. 禅意榜：赛季结算检查
        // ==========================================
        bool isJoined = GameDataManager.Instance.UserData.isJoinedZenRank;
        // 先向服务器请求排行榜数据，拿到【真实的】 RemainingSeconds！
        if (isJoined)
        {
            bool hasTriggeredSettlement = false;
            yield return StartCoroutine(ZenRankManager.Instance.CheckAndShowSettlementRoutine(
                PanelType.PrimaryInterface,(res) => { hasTriggeredSettlement = res; }));
            if (hasTriggeredSettlement)
            {
                yield return new WaitUntil(() => !SystemManager.Instance.PanelIsShowing(PanelType.ZenRankStartScreen));
            }
            
            yield return StartCoroutine(ZenRankManager.Instance.FetchLeaderboardDataRoutine(GameDataManager.Instance.UserData.Zenlevel));
        }
        // ==========================================
        // 2. 禅意榜：主动拉取最新排名并赋值给按钮UI
        // ==========================================
        // 走到这里说明结算弹窗已处理完（或者根本没结算），通知按钮拉取最新的排名数据
        if (ZenRankBtn != null)
        {
            ZenRankButton rankBtn = ZenRankBtn.GetComponent<ZenRankButton>();
            if (rankBtn != null)
            {
                rankBtn.CheckRankProgress();
                rankBtn.FetchMyCurrentRank(); // 主动请求真实排名数据
            }
        }
        
        // ==========================================
        // 💡 架构扩展示例：
        // 以后你所有的自动弹窗都可以按顺序写在这里，绝对不会打架！
        // ==========================================
        
        // 2. 比如检查每日签到
        // if (SignInManager.Instance.NeedShowSignIn())
        // {
        //     SystemManager.Instance.ShowPanel(PanelType.SignWaterScreen);
        //     yield return new WaitUntil(() => !SystemManager.Instance.PanelIsShowing(PanelType.SignWaterScreen));
        // }

        // 3. 比如检查离线挂机收益
        // yield return StartCoroutine(OfflineRewardManager.CheckRoutine());
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

    private IEnumerator CheckZenRankBtn()
    {
        ZenRankBtn.GetComponent<ZenRankButton>().CheckRankProgress();
        WaitForSeconds waitTime = new WaitForSeconds(0.5f);
        while (ZenRankBtn.gameObject.activeSelf)
        {
            yield return waitTime;
            ZenRankBtn.GetComponent<ZenRankButton>().CheckRankProgress();
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

    #region 功能按钮
    
    /// <summary>
    /// 点击选择游戏模式
    /// </summary>
    private void OnModeClick()
    {
        SystemManager.Instance.HidePanel(PanelType.HeaderSection);
        _windowAnimator.SetBool("IsCollapse", true);
        SystemManager.Instance.ShowPanel(PanelType.SelectMode);
    }
    
    private void OnFishClick()
    {
        if (Game.IsNetworkActive)
        {
            if (string.IsNullOrEmpty(GameDataManager.Instance.FishUserSave.roundstarttime))
            {
                SystemManager.Instance.ShowPanel(PanelType.CompetitionStart);
                //GameDataManager.MainInstance.FishUserSave.UpdateOpenTime();
            }
            else
            {
                SystemManager.Instance.ShowPanel(PanelType.DashCompetition);
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

        // if (!fishtimetext.transform.parent.gameObject.activeSelf)
        // {
        //     fishtimetext.transform.parent.gameObject.SetActive(true);
        // }
            
        //UpdateFishRank();
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
        }
        else
        {
            headicon.transform.gameObject.SetActive(false);
        }
    }
    private void UpdateTaskBtnUI()
    {
        // if (LimitTimeManager.instance.IsComplete())
        // {
        //     TaskClaim.gameObject.SetActive(false);
        // }
        //TasksBtn.gameObject.SetActive(GameDataManager.MainInstance.UserData.CurrentStage >= AppGameSettings.UnlockRequirements.DailyMissions);
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
        HeadBtn.gameObject.SetActive(
            GameDataManager.Instance.UserData.CurrentHexStage >= AppGameSettings.UnlockRequirements.HeadOpenLevel
            || GameDataManager.Instance.UserData.CurrentChessStage >= AppGameSettings.UnlockRequirements.HeadOpenLevel);
        
        TasksBtn.transform.parent.gameObject.SetActive(GameDataManager.Instance.UserData.CurrentHexStage>= AppGameSettings.UnlockRequirements.DailyMissions
                                      ||GameDataManager.Instance.UserData.CurrentChessStage>= AppGameSettings.UnlockRequirements.DailyMissions
        ||!string.IsNullOrEmpty(GameDataManager.Instance.FishUserSave.opentime));
        
        LimitTimeBtn.gameObject.SetActive(GameDataManager.Instance.UserData.CurrentHexStage >= AppGameSettings.UnlockRequirements.TimeLimitMode
                                          ||GameDataManager.Instance.UserData.CurrentChessStage >= AppGameSettings.UnlockRequirements.TimeLimitMode
        ||!string.IsNullOrEmpty(GameDataManager.Instance.UserData.limitOpenTime));
  
        ButterflyBtn.gameObject.SetActive(ButterfliesManager.Instance.IsOpen);
        // 🌟 核心修改：全收集满后，隐藏按钮上的进度条和文字
        bool isAllCollected = ButterfliesManager.Instance.IsAllButterfliesCollected();
        Slider pupaSlider = ButterflyBtn.GetComponentInChildren<Slider>(true);
        Text pupaText = ButterflyBtn.GetComponentInChildren<Text>(true);
        if (isAllCollected)
        {
            // 🎉 全收集满：关掉进度条和进度文字，给玩家一个干净清爽的满级蝴蝶按钮
            if (pupaSlider != null) pupaSlider.gameObject.SetActive(false);
            if (pupaText != null) pupaText.gameObject.SetActive(false);
        }
        else
        {
            // 🐛 还没满：正常打开并刷新进度
            if (pupaSlider != null) pupaSlider.gameObject.SetActive(true);
            if (pupaText != null) pupaText.gameObject.SetActive(true);
            ButterflyGrow butterflyGrow = ButterfliesManager.Instance.GetCurrentGrow();
            if (pupaText != null) 
            {
                pupaText.text =  $"{GameDataManager.Instance.ButterflyData.currPupa} / {butterflyGrow?.Count.ToString() ?? "&"}"; 
            }
            
            float progressValue = 0f;
            if (butterflyGrow != null && butterflyGrow.Count > 0)
            { 
                progressValue = Mathf.Clamp01((float)GameDataManager.Instance.ButterflyData.currPupa / butterflyGrow.Count);
            }
            if (pupaSlider != null) pupaSlider.value = progressValue;
            
            ButterflyRedpoint.gameObject.SetActive(ButterfliesManager.Instance.showButterflyRedPoint);
        }
       
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

    private void InitLimtBtnUI()
    {
        LimitTimeObj.gameObject.SetActive(!LimitTimeManager.Instance.IsClaim());
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

    private void ShowSevenSignScreen()
    {
        SystemManager.Instance.ShowPanel(PanelType.SevenSignScreen);    
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
    private void ClickHexaBtnClick()
    {
        AnalyticMgr.PopShow("成语消：禅意之境引流");
        Application.OpenURL("https://appgallery.huawei.com/app/detail?id=chengyu.idiom.hexa.zen.huawei");
    }
    
    private void OnButterflyClick()
    {
        
        if (GameCoreManager.Instance.PanelState == PanelState.MainMenuPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.PrimaryInterface);
        }else if (GameCoreManager.Instance.PanelState == PanelState.FinishHexPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.StageFinishView);
        }else if (GameCoreManager.Instance.PanelState == PanelState.FinishPingPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.ChessFinishView);
        }
        
        SystemManager.Instance.HidePanel(PanelType.HeaderSection , true, () =>
        {
            SystemManager.Instance.ShowPanel(PanelType.ButterflyHome);
        });
    }
    #endregion
    /// <summary>
    /// 当对象禁用时调用
    /// </summary>
    protected override void OnDisable()
    {
        base.OnDisable();
        //EventManager.OnChangeLanguageUpdateUI -= InitUI;
        EventDispatcher.instance.OnChangeGoldUI -= InitUI;
        
        LimitTimeManager.Instance.OnLimitTimeBtnUI -= InitLimtBtnUI;
        DailyTaskManager.Instance.OnDailyTaskBtnUI -= UpdateDailyTaskBtnUI;
        DailyTaskManager.Instance.OnDailyButterflyTaskUI -= UpdateButterflyTime;
        EventDispatcher.instance.OnChangeHeadIconUpdateUI -= UpdateHeadBtnUI;
        FishInfoController.Instance.OnFishTimeUpdated -= UpdateFishTime;
        
        if (ThemeManager.Instance != null)
            ThemeManager.Instance.OnSkinRedPointChanged -= OnRedPointChanged;
 
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
    public void InitUI(int index=0,bool active=false)
    {
        // 设置关卡文本
        // 设置关卡文本
        int Stage = 0;
        LevelModes levelmode = LevelModes.Normal;
        // 设置模式图标
        Sprite sprite = null;
        switch (GameDataManager.Instance.UserData.levelMode)
        {
            case 1:
            case 3:
                Stage = GameDataManager.Instance.UserData.CurrentHexStage != 0 ? 
                    GameDataManager.Instance.UserData.CurrentHexStage : 1;
                sprite = LoadheadIcon("icon_xiao");
                StageHexController.Instance.CurLevelMode = GetLevelDifficulty(Stage);
                levelmode = StageHexController.Instance.CurLevelMode;
                break;
            case 2:
                Stage = GameDataManager.Instance.UserData.CurrentChessStage != 0 ? 
                    GameDataManager.Instance.UserData.CurrentChessStage : 1;
                sprite = LoadheadIcon("icon_pinzi");
                ChessStageController.Instance.CurLevelMode = GetLevelDifficulty(Stage);
                levelmode = ChessStageController.Instance.CurLevelMode;
                break;
        }
        
        Stage=Stage==0?1:Stage;
        
        switch (levelmode)
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

        UpdateWinTimes();
    }
    
    private void UpdateWinTimes()
    {
        Text winTimes = SevenSignBtn.GetComponentInChildren<Text>();
        
        int curStreak = StreakManager.Instance.GetCurrentStreak();
      
        notSign.gameObject.SetActive(curStreak <= 0);
        fillSign.gameObject.SetActive(curStreak > 0);
       
        if (curStreak <= 0)
        {
            winTimes.text = "0";
            // winTimes.color = new Color(0.509804f, 0.509804f, 0.509804f,1f);
            // winTimes.GetComponent<Outline>().effectColor = new Color(0.509804f, 0.509804f, 0.509804f,1f);
        }
        else
        {
            winTimes.text = curStreak.ToString();
            // winTimes.color = new Color(0.9960785f, 1f, 0.7686275f,1f);
            // winTimes.GetComponent<Outline>().effectColor = new Color(0.9960785f, 1f, 0.7686275f,1f);
        }
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
        
        yield return new WaitForSeconds(0.1f);
        //AdsManager.Instance.HideBannerAd();
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

    

    /// <summary>
    /// 点击开始游戏按钮
    /// </summary>
    public void OnPlayClick()
    {
        bool hasUnfinishedSave = (GameDataManager.Instance.UserData.levelMode == 2 && ChessStageController.Instance.HasUnfinishedSave());
        if (!hasUnfinishedSave)
        {
            // 🌟 核心拦截：检查并扣除体力！
            // ConsumeEnergy 会自动处理：如果是第一关返回 true 不扣体力，如果体力>=1返回 true 并扣除。
            if (ChessStageController.Instance.CurrentStage != 1 && GameDataManager.Instance.UserData.Energy <= 0)
            {
                // 体力不足，拦截并提示玩家
                // MessageSystem.Instance.ShowTip("体力不足，休息一下吧！");
                
                // TODO: 如果你有买体力或看广告回体力的弹窗，可以在这里自动弹出来引导玩家
                SystemManager.Instance.ShowPanel(PanelType.PrimaryInterface); 
                SystemManager.Instance.ShowPanel(PanelType.EnergyScreen);
                // StartCoroutine(ShowHandleEnergyScreen());
                return; // 终止进入关卡
            }

            // 顺手保存一下扣完体力后的最新数据
            GameDataManager.Instance.CommitGameData();
        }
        
        try
        {
            switch (GameDataManager.Instance.UserData.levelMode)
            {
                case 1:
                case 3:
                    StageHexController.Instance.SetStageData(StageHexController.Instance.CurrentStage);
                    break;
                case 2:
                    ChessStageController.Instance.SetStageData(ChessStageController.Instance.CurrentStage);
                    break;
            }
           
        }catch (System.Exception e)
        {
            Debug.LogError("设置关卡数据失败: " + e);
        }

        
        if (SystemManager.Instance.PanelIsShowing(PanelType.HeaderSection))
        {
            SystemManager.Instance.HidePanel(PanelType.HeaderSection,true, OnEnterStageClick);
            SystemManager.Instance.HidePanel(PanelType.PrimaryInterface);
        }
        else
        {
            if (SystemManager.Instance.PanelIsShowing(PanelType.PrimaryInterface))
            {
                SystemManager.Instance.HidePanel(PanelType.PrimaryInterface, true, OnEnterStageClick);
            }
            else
            {
                OnEnterStageClick();
            }
        }
       
       
    }

    private IEnumerator ShowHandleEnergyScreen()
    {
        yield return new WaitForSeconds(0.5f);
        SystemManager.Instance.ShowPanel(PanelType.EnergyScreen);
    }
    
    private Sprite LoadheadIcon(string showIcon)
    {
        return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(showIcon);
    }

    /// <summary>
    /// 进入关卡回调
    /// </summary>
    private static void OnEnterStageClick()
    {
        switch (GameDataManager.Instance.UserData.levelMode)
        {
            case 1:
            case 3:
                SystemManager.Instance.ShowPanel(PanelType.HexGamePlayArea);
                break;
            case 2:
                SystemManager.Instance.ShowPanel(PanelType.ChessPlayArea);
                break;
        }
    }

    private void OnScoreboardClick()
    {

    }

}