using DG.Tweening;
using Middleware;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主界面控制器 - 处理游戏主界面的UI逻辑和交互
/// </summary>
public class PrimaryInterface : UIWindow
{
    [Header("lOGO")]
    [SerializeField] private Image logo;       // 文字类型组件
    [Header("UI组件")]
    [SerializeField] private Button OverallBtn;         // 名人堂入口
    [SerializeField] private Button ZenRankBtn;         // 禅意排名按钮
    [Header("新手奖励活动")]
    [SerializeField] private BeginnerBonusEntryBtn _beginnerBonusEntryBtn; // 结算页右侧的新手奖励入口按钮组件

    [Header("礼包入口（破冰 / 限时折扣）")]
    [SerializeField] private Button breakIceGiftBtn;     // 破冰礼包入口（与结算页同一份登记）
    [SerializeField] private Button discountGiftBtn;     // 限时折扣/复购礼包入口
    //[SerializeField] private Button HexaBtn;         // 层层消按钮
    [Header("ButterflyGarden")]
    [SerializeField] private Button ButterflyBtn;
    [SerializeField] private GameObject ButterflyRedpoint;
    [SerializeField] private GameObject ButterflyLock;
    [SerializeField] private GameObject ButterflyUnlockText;
    [Header("UI LimitTime")]
    [SerializeField] private Button LimitTimeBtn;
    [SerializeField] private GameObject LimitTimeObj;
    [SerializeField] private GameObject LimitClaim;
    [SerializeField] private GameObject Worddouble;
    [SerializeField] private Text timetxt;
    [SerializeField] private Image limitOver;
    [SerializeField] private GameObject limitLock;
    [Header("UI Task")]
    [SerializeField] private GameObject TaskClaim;
    [SerializeField] private Button TasksBtn;
    [SerializeField] private Text tasktimetxt;
    [SerializeField] private Image taskOver;
    [SerializeField] private GameObject taskLock;
    
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
    [SerializeField] private GameObject fishLock;   // 捕鱼入口锁图标（WT 预留，未挂则为空）
 
    [Header("UI Butterfly")]
    [SerializeField] private GameObject ButterflyTime;
    public GameObject GoldLeafredpoint;
    [Header("UI Headers")]
    [SerializeField] private Button HeadBtn;
    [SerializeField] private Image headicon;
    [SerializeField] private Image headBorder;
    [SerializeField] private GameObject headRedpoint;
    [SerializeField] private Button myThemeBtn; // 皮肤
    [SerializeField] private GameObject themeGoldLeafredpoint;
    [SerializeField] private GameObject themeLock;
    [SerializeField] private GameObject themeLockText;
    [SerializeField] private Button Puzzlebook;  // 生词本
    [SerializeField] private Button SettingBtn;

    // ===== 同步自 WT 1.9.8：开始关卡区 / 下一元素提示相关引用 =====
    [Header("UI 开始关卡")]
    [SerializeField] private Button GameStageBtn;          // 开始游戏按钮
    [SerializeField] private Text Stagetxt;                // 关卡文本
    [SerializeField] private GameObject hardStageTable;          // 困难模式
    [SerializeField] private GameObject extrahardStageTable;     // 特别困难模式
    [SerializeField] private Button ModeBtn;          // 模式选择按钮
    [SerializeField] private Animator ModeIndicator;  // 模式按钮指示器
    [SerializeField] private GameObject nextUnlockTip;    // 下一种游戏元素提示
    [SerializeField] private Image nextStageImage;        // 下一种游戏图片
    [SerializeField] private GameObject stageSpace;      // 有图片时占位
    [SerializeField] private GameObject specialStageContent; // 特殊关卡：content 里显示该关的图 + 关卡数

    [Header("配置参数")]
    [SerializeField] private float topPanelDelay = 0.01f; // 顶部面板显示延迟时间

    // ======================= 主页礼包入口（破冰 / 限时折扣） =======================
    /// <summary>礼包入口按钮倒计时协程</summary>
    private Coroutine _settlementGiftCoroutine;
    private GameObject _breakIceTimeObj;
    private Text _breakIceTimeText;
    private GameObject _discountTimeObj;
    private Text _discountTimeText;
    /// <summary>刷新重入保护（ClearSettlementGift 会同步回调刷新事件）</summary>
    private bool _refreshingGiftEntry;

    /// <summary>
    /// 大厅展示动画倍速：2 = 入场动画快一倍（MainShow 原约 0.97s → 约 0.49s）
    /// </summary>
    private const float LobbyEnterSpeed = 2f;

    /// <summary>顶部面板入场后到金币层刷新的常规等待</summary>
    private const float TopPanelRevealGap = 0.3f;

    /// <summary>首次从加载页进大厅时的衔接等待（顶栏已按 2x 播放）</summary>
    private const float TopPanelRevealGapFast = 0.15f;

    /// <summary>
    /// 一次性标记：next 次显示大厅时使用 2 倍速入场动画。
    /// 由 GameCoreManager 在「加载完成 → 首次进大厅」时置位，消费后立刻清空，
    /// 保证从排行榜/商店/关卡等界面返回大厅时仍是正常速度。
    /// </summary>
    public static bool FastEnterOnce = false;

    /// <summary>本次显示是否走了加速入场（供 ShowTopPanel 同步顶栏节奏）</summary>
    private bool _fastEnterThisShow;
    
    private void Start()
    {
        //logo.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromBundle(ToolUtil.GetLanguageBundle(),"ui_logo");
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
        GameStageBtn.AddVibraClickAction(OnPlayClick);
        ModeBtn.AddClickAction(OnModeClick);
        SevenSignBtn.AddVibraClickAction(ShowSevenSignScreen);
        LimitTimeBtn.AddClickAction(ClickLimintTime);
        TasksBtn.AddClickAction(OnTaskClick);
        HeadBtn.AddVibraClickAction(OnHeadClick);
        FishBtn.AddClickAction(OnFishClick);
        
        ButterflyBtn.AddClickAction(OnButterflyClick);
        //HexaBtn.AddClickAction(ClickHexaBtnClick);
        
        myThemeBtn.AddVibraClickAction(OnClickMyThemeBtn);
        Puzzlebook.AddVibraClickAction(OnClickPuzzleVocabulary);
        SettingBtn.AddVibraClickAction(OnSetClick);

        // ======================= 主页礼包入口（破冰 / 限时折扣） =======================
        // 默认隐藏，由 RefreshSettlementGiftEntry 按 ShopManager 的结算礼包登记决定
        if (breakIceGiftBtn != null)
        {
            breakIceGiftBtn.AddVibraClickAction(OnBreakIceGiftBtnClick);
            breakIceGiftBtn.gameObject.SetActive(false);
        }
        if (discountGiftBtn != null)
        {
            discountGiftBtn.AddVibraClickAction(OnDiscountGiftBtnClick);
            discountGiftBtn.gameObject.SetActive(false);
        }
    }
    
    /// <summary>
    /// 当对象启用时调用
    /// </summary>
    protected override void OnEnable()
    {
        // 只有「加载页 → 首次进大厅」这一次走 2 倍速入场（MainShow 原 0.6 倍速 / 0.583s clip ≈ 0.97s）；
        // 从排行榜、商店、关卡等界面返回大厅时保持正常速度。
        _fastEnterThisShow = FastEnterOnce;
        FastEnterOnce = false;
        if (_fastEnterThisShow)
            PlayEnterAtSpeed(LobbyEnterSpeed);

        // 主页出现的第一帧就同步顶部状态：把结算页残留的「关内成语」入口收掉，避免一闪而过
        EventDispatcher.instance.TriggerUpdateLayerCoin(false, true, false);

        InitUI();
        GameCoreManager.Instance.PanelState = PanelState.MainMenuPanel;
        InitUI();
        //EnhancedVideoController.Instance.PlayVideo();
        LimitTimeManager.Instance.OnLimitTimeBtnUI += InitLimtBtnUI;
        DailyTaskManager.Instance.OnDailyTaskBtnUI += UpdateDailyTaskBtnUI;
        DailyTaskManager.Instance.OnDailyButterflyTaskUI += UpdateButterflyTime;
        EventDispatcher.instance.OnChangeHeadIconUpdateUI += UpdateHeadBtnUI;
        FishInfoController.Instance.OnFishTimeUpdated += UpdateFishTime;
        EventDispatcher.instance.OnChangeGoldUI += InitUI;
        if (OverallRankingManager.Instance != null)
        {
            OverallRankingManager.Instance.OnAuditConfigLoaded += OnAuditConfigRefreshUI;
        }
    
        CheckButtonsIsOpen();
        StartCoroutine(ShowTopPanel());
        UpdateHeadBtnUI();
        StartCoroutine(CheckOverallRankBtn());
        Game.self.Ads?.HideBanner();
        StartCoroutine(CheckZenRankBtn());
        
        StartCoroutine(CheckLobbyPopupsRoutine());
        DailyTaskManager.Instance.UpdateMaxButterflyTime();
        
        ThemeManager.Instance.OnSkinRedPointChanged += OnRedPointChanged;
        OnRedPointChanged(ThemeManager.Instance.IsSkinRedPointActive);
        
        GameCoreManager.Instance.SetBackgroundImage(new Color(1,1,1,1));
        SevenSignBtn.gameObject.SetActive(StreakManager.Instance.UnlockStreak());
        
        ThemeManager.Instance.themeButton = myThemeBtn.gameObject;
        
        GameCoreManager.Instance.UpdateMyPersonInfo();
        
        string numStr = GameDataManager.Instance.UserData.Zenlevel;
        int zenLevel = int.Parse(numStr.Substring(numStr.Length - 2));
        AchievementManager.Instance.AddAvatarFrameItems(AvatarUnlockType.FlowerRank, zenLevel);

        // 主页礼包入口：订阅登记变化 + 进场立刻按当前登记刷新一次
        if (ShopManager.shopManager != null)
            ShopManager.shopManager.OnSettlementGiftChanged += RefreshSettlementGiftEntry;

        // ★ 活动期间进主页就要看到入口：结算页登记是内存态，重启/换场景会丢，
        //   所以进主页先按活动条件补登记一次（只登记不弹窗），再刷新显隐。
        RefreshHomeGiftEntryOnEnter();
        StartCoroutine(RefreshHomeGiftEntryDelayed());
    }

    /// <summary>进主页时补登记「破冰 / 限时折扣」入口并刷新显隐。</summary>
    private void RefreshHomeGiftEntryOnEnter()
    {
        if (ShopManager.shopManager != null)
            ShopManager.shopManager.TryEnsureHomeSettlementGiftEntry();

        RefreshSettlementGiftEntry();
    }

    /// <summary>
    /// 兜底：极早期进主页时商店配置可能还没解析完（选包为空 → 登记不上），
    /// 稍后再补判一次，避免「活动期间第一次进主页没有入口」。
    /// </summary>
    private IEnumerator RefreshHomeGiftEntryDelayed()
    {
        yield return new WaitForSeconds(0.8f);

        if (ShopManager.shopManager == null || !isActiveAndEnabled) yield break;
        if (ShopManager.shopManager.CurrentSettlementGift != ShopManager.SettlementGiftKind.None) yield break;

        RefreshHomeGiftEntryOnEnter();
    }
    
    private void OnRedPointChanged(bool show)
    {
        if (themeGoldLeafredpoint != null)
            themeGoldLeafredpoint.SetActive(show);
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
        yield return new WaitForSeconds(0.5f);
        // ==========================================
        // 3. 月度榜：每月赛季结算检查
        // ==========================================
        bool hasMonthlySettlement = false;
        yield return StartCoroutine(OverallRankingManager.Instance.CheckMonthlySettlementRoutine(
            (hasBool) =>
            {
                hasMonthlySettlement = hasBool;
            }));
        if (hasMonthlySettlement)
        {
            yield return new WaitUntil(() => !SystemManager.Instance.PanelIsShowing(PanelType.OverallSettlementScreen));
        }
        
        //更改为15关之后出现（16开始出现）
        // HexaBtn.gameObject.SetActive(ChessStageController.Instance.CurrentStage>15||StageController.Instance.CurrentStage>15 );
      
        // ==========================================
        // 1. 禅意榜：赛季结算检查
        // ==========================================
        bool hasTriggeredSettlement = false;
        yield return StartCoroutine(ZenRankManager.Instance.CheckAndShowSettlementRoutine(
            PanelType.PrimaryInterface, (res) => { hasTriggeredSettlement = res; }));

        if (hasTriggeredSettlement)
        {
            yield return new WaitUntil(() => !SystemManager.Instance.PanelIsShowing(PanelType.ZenRankStartScreen));
        }
        // ==========================================
        // 2. 如果已加入新赛季，再拉取当前榜单数据
        // ==========================================
        if (GameDataManager.Instance.UserData.isJoinedZenRank)
        {
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

                // CheckRankProgress 可能在未解锁时把 ZenBtn 关掉，关掉后不能起协程
                if (rankBtn.gameObject.activeInHierarchy)
                {
                    rankBtn.FetchMyCurrentRank(); // 主动请求真实排名数据
                }
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
        FishClaim.gameObject.SetActive(false);
        // 提取重复使用的SaveData引用
        //var fishSave = GameDataManager.MainInstance.FishUserSave;
        FishInfoController.Instance.RoundResultFishRank();
        UpdateFishRank();
        while (FishBtn.gameObject.activeSelf)
        {
            yield return new WaitForSeconds(0.5f);
            FishInfoController.Instance.RoundResultFishRank();
            yield return new WaitForSeconds(0.8f);
            
            foreach (var ai in GameDataManager.Instance.FishUserSave.aiSaveDatas)
            {
                FishInfoController.Instance.CheckAIPassLevel(ai.aiid,null);
            }
            
            UpdateFishRank();
        }
    }

    private IEnumerator CheckZenRankBtn()
    {
        ZenRankButton rankComp = ZenRankBtn.GetComponent<ZenRankButton>();
        if (rankComp == null) yield break;
        // 先执行一次初始化刷新
        rankComp.CheckRankProgress();
        
        if (!ZenRankManager.Instance.IsOpen())
            yield break;
        
        WaitForSeconds waitTime = new WaitForSeconds(0.5f);
        while (ZenRankBtn.gameObject.activeSelf)
        {
            yield return waitTime;
            rankComp.CheckRankProgress();
        }
    }
    
    private IEnumerator CheckOverallRankBtn()
    {
        OverallBtn.GetComponent<OverallEntranceButton>().RefreshState();
        WaitForSeconds waitTime = new WaitForSeconds(0.5f);
        while (OverallBtn.gameObject.activeSelf)
        {
            yield return waitTime;
            OverallBtn.GetComponent<OverallEntranceButton>().RefreshState();
        }
    }
    private void OnAuditConfigRefreshUI()
    {
        // 审核配置是异步拉取的：进大厅时 AuditConfig 可能还是默认值，导致排行榜入口先被判成隐藏。
        // 这里在配置就绪后无条件重算一次入口状态（原来只有「已开捕鱼」的玩家才会重算，等于低关卡玩家永远看不到入口）。
        var overallComp = OverallBtn != null ? OverallBtn.GetComponent<OverallEntranceButton>() : null;
        if (overallComp != null) overallComp.RefreshState();

        if (ZenRankBtn != null)
        {
            var zenComp = ZenRankBtn.GetComponent<ZenRankButton>();
            if (zenComp != null) zenComp.CheckRankProgress();
        }
    }
    #region 功能按钮
    private void OnSetClick()
    {
        SystemManager.Instance.ShowPanel(PanelType.OptionsView);
    }
    private void OnClickPuzzleVocabulary()
    {
        if (GameDataManager.Instance.UserData.levelMode == 1)
            StageHexController.Instance.IsEnterVocabulary = false;
        else if (GameDataManager.Instance.UserData.levelMode == 2)
            ChessStageController.Instance.IsEnterVocabulary = false;
       
        SystemManager.Instance.ShowPanel(PanelType.WordVocabularyScreen);
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
    
    private void OnFishClick()
    {
        if (GameCoreManager.Instance.IsNetworkActive)
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
        // SystemManager.Instance.HidePanel(PanelType.HeaderSection,true, () =>
        // {
            SystemManager.Instance.ShowPanel(PanelType.PersonInfoScreen);
        // });
        // SystemManager.Instance.HidePanel(PanelType.PrimaryInterface);
    }
    
    private void UpdateFishTime(string time="")
    {
        if (!FishInfoController.Instance.RoundFishIsOver())
        {
            fishtimetext.text = time;
            if (GameDataManager.Instance.UserData.CurrentHexStage >= AppGameSettings.UnlockRequirements.FishOpenLevel||
                GameDataManager.Instance.UserData.CurrentChessStage >= AppGameSettings.UnlockRequirements.FishOpenLevel||
                !string.IsNullOrEmpty(GameDataManager.Instance.FishUserSave.opentime))
            {
                FishBtn.gameObject.SetActive(FishInfoController.Instance.GetOpenFishFunction());
            }
        }
        else
        {
            fishtimetext.text="已结束";
        }
        
        // if (!fishtimetext.transform.parent.gameObject.activeSelf)
        // {
        //     fishtimetext.transform.parent.gameObject.SetActive(true);
        // }
            
        //UpdateFishRank();
    }
    
    
    private void UpdateFishRank()
    {
        // 如果按钮未解锁，强制隐藏所有附属状态！
        if (!FishBtn.interactable)
        {
            fishtimetext.gameObject.SetActive(true);
            fishtimetext.text = $"第{AppGameSettings.UnlockRequirements.FishOpenLevel}关";

            if (fishtimetext.transform.parent != null)
            {
                Image bg = fishtimetext.transform.parent.GetComponent<Image>();
                if (bg != null) bg.enabled = true;
            }
            fishrankimage.gameObject.SetActive(false);
            FishClaim.gameObject.SetActive(false);
            fishwifiimage.gameObject.SetActive(false);
            return;
        }
        if (GameCoreManager.Instance.IsNetworkActive)
        {
            fishwifiimage.gameObject.SetActive(false);
            if (GameDataManager.Instance.FishUserSave.rank > 0&& !string.IsNullOrEmpty(GameDataManager.Instance.FishUserSave.roundstarttime))
            {
                int rank = GameDataManager.Instance.FishUserSave.rank;
                switch (rank)
                {
                    case 1:
                        fishrankimage.sprite = LoadCommonIcon("fishbtnred");
                        break;
                    case 2:
                        fishrankimage.sprite = LoadCommonIcon("fishbtnblue");
                        break;
                    case 3:
                        fishrankimage.sprite = LoadCommonIcon("fishbtngreen");
                        break;
                    case 4:
                    case 5:
                        fishrankimage.sprite = LoadCommonIcon("fishbtngrey");
                        break;
                }

                if (!FishInfoController.Instance.RoundFishIsOver())
                {
                    fishtimetext.transform.parent.gameObject.SetActive(true);
                    fishrankimage.gameObject.SetActive(true);
                    fishrankcount.text = rank.ToString();
                    fishtimetext.gameObject.SetActive(true);
                    if (fishtimetext.transform.parent != null)
                    {
                        Image bg = fishtimetext.transform.parent.GetComponent<Image>();
                        if (bg != null) bg.enabled = true;
                    }
                }
                else
                {
                    FishClaim.gameObject.SetActive(true);
                    //fishtimetext.transform.parent.gameObject.SetActive(false);
                    fishtimetext.text = "已结束";
                    fishrankimage.gameObject.SetActive(false);
                    fishtimetext.gameObject.SetActive(false);
                    if (fishtimetext.transform.parent != null)
                    {
                        Image bg = fishtimetext.transform.parent.GetComponent<Image>();
                        if (bg != null) bg.enabled = false;
                    }
                }
            }
            else
            {
                fishtimetext.transform.parent.gameObject.SetActive(true);
                fishtimetext.gameObject.SetActive(true);
                if (fishtimetext.transform.parent != null)
                {
                    Image bg = fishtimetext.transform.parent.GetComponent<Image>();
                    if (bg != null) bg.enabled = true;
                }
                fishrankimage.gameObject.SetActive(false);
                FishClaim.gameObject.SetActive(false);
            }
        }
        else
        {
            fishtimetext.transform.parent.gameObject.SetActive(true);
            fishtimetext.gameObject.SetActive(true);
            if (fishtimetext.transform.parent != null)
            {
                Image bg = fishtimetext.transform.parent.GetComponent<Image>();
                if (bg != null) bg.enabled = true;
            }
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
        
        if (GameDataManager.Instance.UserData.UserHeadBorderId > 0)
        {
            headBorder.sprite = LoadheadIcon("AvatarFrameIcon" + GameDataManager.Instance.UserData.UserHeadBorderId);
            headBorder.transform.gameObject.SetActive(true);
        } else
        {
            headBorder.transform.gameObject.SetActive(false);
        }
    }
    
    private void UpdateDailyTaskBtnUI()
    {
        if (!TasksBtn.interactable) return;
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

        // 同步自 WT 1.9.8：任务/限时/捕鱼/皮肤/蝶园 统一走「入口加锁」刷新
        CheckFishBtnUI();
        CheckTasksBtnUI();
        CheckLimitBtnUI();
        CheckThemeBtnUI();
        CheckButterflyBtnUI();

        if (headRedpoint != null)
            headRedpoint.gameObject.SetActive(GameDataManager.Instance.UserData.isGetNewHeadIcon);
    }

    /// <summary>当前进度：HexWord 双模式取较大值，避免只认拼字关卡把六边形玩家误锁</summary>
    private int CurrentProgressStage =>
        Mathf.Max(GameDataManager.Instance.UserData.CurrentHexStage,
                  GameDataManager.Instance.UserData.CurrentChessStage);

    /// <summary>捕鱼入口：显隐 + 未解锁态（第X关文案 + 图标半透明）</summary>
    private void CheckFishBtnUI()
    {
        bool isLegacyOpened = FishInfoController.Instance.IsOpen();
        bool isShow = CurrentProgressStage > AppGameSettings.ShowRequirements.FishShowLevel || isLegacyOpened;
        bool openTime = FishInfoController.Instance.GetOpenFishFunction(); // 是否在开启时间内
        if (FishBtn.transform.parent != null)
            FishBtn.transform.parent.gameObject.SetActive(isShow && openTime);
        if (!isShow) return;

        bool isLocked = !isLegacyOpened;
        FishBtn.interactable = !isLocked;
        if (fishLock != null) fishLock.SetActive(isLocked);

        Image fish = FishBtn.transform.childCount > 0 ? FishBtn.transform.GetChild(0).GetComponent<Image>() : null;
        if (isLocked)
        {
            if (fishtimetext != null)
            {
                fishtimetext.gameObject.SetActive(true);
                fishtimetext.text = $"第{AppGameSettings.UnlockRequirements.FishOpenLevel}关";
                if (fishtimetext.transform.parent != null)
                {
                    Image bg = fishtimetext.transform.parent.GetComponent<Image>();
                    if (bg != null) bg.enabled = true;
                }
            }
            if (fish != null) fish.color = new Color(1, 1, 1, 0.5f);
            // 隐藏其他活动状态相关的图标
            if (fishrankimage != null) fishrankimage.gameObject.SetActive(false);
            if (FishClaim != null) FishClaim.gameObject.SetActive(false);
            if (fishwifiimage != null) fishwifiimage.gameObject.SetActive(false);
            return;
        }

        if (fish != null) fish.color = Color.white;
        StartCoroutine(UpdateFishRankUI());
    }

    /// <summary>每日任务入口：显隐 + 未解锁态</summary>
    private void CheckTasksBtnUI()
    {
        bool isLegacyOpened = DailyTaskManager.Instance.IsOpen();
        bool isShow = CurrentProgressStage > AppGameSettings.ShowRequirements.DailyMissionsShow || isLegacyOpened;
        if (TasksBtn.transform.parent != null)
            TasksBtn.transform.parent.gameObject.SetActive(isShow);
        if (!isShow) return;

        bool isLocked = !isLegacyOpened;
        TasksBtn.interactable = !isLocked;
        if (taskLock != null) taskLock.SetActive(isLocked);

        Image taskIcon = TasksBtn.transform.childCount > 0 ? TasksBtn.transform.GetChild(0).GetComponent<Image>() : null;
        if (isLocked)
        {
            if (tasktimetxt != null)
            {
                tasktimetxt.gameObject.SetActive(true);
                tasktimetxt.text = $"第{AppGameSettings.UnlockRequirements.DailyMissions}关";
                if (tasktimetxt.transform.parent != null)
                {
                    Image bg = tasktimetxt.transform.parent.GetComponent<Image>();
                    if (bg != null) bg.enabled = true;
                }
            }
            if (taskIcon != null) taskIcon.color = new Color(1, 1, 1, 0.5f);
            if (TaskClaim != null) TaskClaim.gameObject.SetActive(false);
            if (taskOver != null) taskOver.gameObject.SetActive(false);
        }
        else
        {
            if (taskIcon != null) taskIcon.color = Color.white;
            if (TaskClaim != null)
            {
                Text claimTxt = TaskClaim.GetComponentInChildren<Text>();
                if (claimTxt != null) claimTxt.text = MultilingualManager.Instance.GetString("ADPopReceive");
            }
        }
    }

    /// <summary>限时活动入口：显隐 + 未解锁态</summary>
    private void CheckLimitBtnUI()
    {
        bool isLegacyOpened = LimitTimeManager.Instance.IsOpen();
        bool isShow = CurrentProgressStage > AppGameSettings.ShowRequirements.TimeLimitShow || isLegacyOpened;
        LimitTimeBtn.gameObject.SetActive(isShow);
        if (!isShow) return;

        bool isLocked = !isLegacyOpened;
        LimitTimeBtn.interactable = !isLocked;
        if (limitLock != null) limitLock.SetActive(isLocked);

        Image deng = LimitTimeBtn.transform.childCount > 0 ? LimitTimeBtn.transform.GetChild(0).GetComponent<Image>() : null;
        if (isLocked)
        {
            if (timetxt != null)
            {
                timetxt.gameObject.SetActive(true);
                timetxt.text = $"第{AppGameSettings.UnlockRequirements.TimeLimitMode}关";
                if (timetxt.transform.parent != null)
                {
                    Image parentBg = timetxt.transform.parent.GetComponent<Image>();
                    if (parentBg != null) parentBg.enabled = true;
                }
            }
            if (deng != null) deng.color = new Color(1, 1, 1, 0.5f);
            if (LimitTimeBtn.transform.childCount > 1)
                LimitTimeBtn.transform.GetChild(1).gameObject.SetActive(false);
            if (LimitTimeObj != null)
            {
                LimitTimeObj.SetActive(true);
                Image bg = LimitTimeObj.GetComponent<Image>();
                if (bg != null) bg.enabled = true;
            }
        }
        else
        {
            if (deng != null) deng.color = Color.white;
            if (LimitTimeBtn.transform.childCount > 1)
                LimitTimeBtn.transform.GetChild(1).gameObject.SetActive(true);
        }
    }

    /// <summary>皮肤入口：显隐 + 未解锁态</summary>
    private void CheckThemeBtnUI()
    {
        bool isLegacyOpened = ThemeManager.Instance.IsOpen();
        bool isShow = CurrentProgressStage > AppGameSettings.ShowRequirements.ThemeShowLevel || isLegacyOpened;
        myThemeBtn.gameObject.SetActive(isShow);
        if (!isShow) return;

        bool isLocked = !isLegacyOpened;
        myThemeBtn.interactable = !isLocked;
        if (themeLock != null) themeLock.SetActive(isLocked);
        if (isLocked)
        {
            if (themeLockText != null)
            {
                themeLockText.SetActive(true);
                Text lockText = themeLockText.GetComponentInChildren<Text>(true);
                if (lockText != null) lockText.text = $"第{AppGameSettings.UnlockRequirements.ThemeOpenLevel}关";
            }
            if (themeGoldLeafredpoint != null) themeGoldLeafredpoint.SetActive(false);
            return;
        }
        if (themeLockText != null) themeLockText.SetActive(false);
        ThemeManager.Instance.CheckAndUpdateSkinRedPoint();
    }

    /// <summary>蝶园入口：显隐 + 未解锁态 + 进度渲染（保留 HexWord 原有实现）</summary>
    private void CheckButterflyBtnUI()
    {
        ButterflyBtn.gameObject.SetActive(ButterfliesManager.Instance.IsOpen);
        // 🌟 核心修改：全收集满后，隐藏按钮上的进度条和文字
        bool isAllCollected = ButterfliesManager.Instance.IsAllButterfliesCollected();
        Slider pupaSlider = ButterflyBtn.GetComponentInChildren<Slider>(true);
        Text pupaText = ButterflyBtn.GetComponentInChildren<Text>(true);
        // 3. 未解锁状态：隐藏进度条、文字和红点
        bool isLocked = !ButterfliesManager.Instance.IsOpen;
        if (isLocked)
        {
            // 锁文案节点是 WT 新增的，HexWord 预制体暂无，做空保护
            if (ButterflyUnlockText != null)
            {
                ButterflyUnlockText.SetActive(true);
                var lockText = ButterflyUnlockText.GetComponentInChildren<Text>(true);
                if (lockText != null) lockText.text = $"第{AppGameSettings.UnlockRequirements.ButterflyGardenOpenLevel}关";
            }
            if (pupaSlider != null) pupaSlider.gameObject.SetActive(false);
            if (pupaText != null) pupaText.gameObject.SetActive(false);
            if (ButterflyRedpoint != null) ButterflyRedpoint.gameObject.SetActive(false);
            return;
        }
        // 4. 已解锁状态：隐藏锁文本，处理正常的进度逻辑
        if (ButterflyUnlockText != null) ButterflyUnlockText.gameObject.SetActive(false);
        
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
            int maxCount = butterflyGrow?.Count ?? 0;
            int currPupa = GameDataManager.Instance.ButterflyData.currPupa;
            
            if (pupaText != null) 
            {
                pupaText.text = $"{currPupa} / {(maxCount > 0 ? maxCount.ToString() : "&")}";
            }
            if (pupaSlider != null)
            {
                pupaSlider.value = maxCount > 0 ? Mathf.Clamp01((float)currPupa / maxCount) : 0f;
            }
            
            if (ButterflyRedpoint != null)
                ButterflyRedpoint.gameObject.SetActive(ButterfliesManager.Instance.showButterflyRedPoint);
        }
    }
    
    private void UpdateTimeDisplay(string time)
    {
        if (!LimitTimeBtn.interactable)
        {
            timetxt.gameObject.SetActive(true);
            timetxt.text = $"第{AppGameSettings.UnlockRequirements.TimeLimitMode}关";
            
            if (timetxt.transform.parent != null)
            {
                Image bg = timetxt.transform.parent.GetComponent<Image>();
                if (bg != null) bg.enabled = true;
            }
            return;
        }
        
        bool shouldActivate = !string.IsNullOrEmpty(time) && 
                              !LimitTimeManager.Instance.IsClaim() && 
                              LimitTimeBtn.interactable;
        
        timetxt.gameObject.SetActive(shouldActivate);
        if (shouldActivate) timetxt.text = time;
        
        // 🌟 核心修复：控制 LimitTimeObj 的黑色底板 Image
        if (timetxt.transform.parent != null)
        {
            Image bg = timetxt.transform.parent.GetComponent<Image>();
            if (bg != null) 
            {
                // 如果时间文字显示，或者处于已完成状态，底板才显示
                bg.enabled = shouldActivate || limitOver.gameObject.activeSelf;
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
        if (!TasksBtn.interactable)
        {
            tasktimetxt.gameObject.SetActive(true);
            tasktimetxt.text = $"第{AppGameSettings.UnlockRequirements.DailyMissions}关";
            
            if (tasktimetxt.transform.parent != null)
            {
                Image bg = tasktimetxt.transform.parent.GetComponent<Image>();
                if (bg != null) bg.enabled = true;
            }
            return;
        }
        bool shouldActivate = !string.IsNullOrEmpty(time) && !DailyTaskManager.Instance.IsAllComplete()
                                                          &&!DailyTaskManager.Instance.IsClaim()
                                                          && TasksBtn.interactable;
    
        tasktimetxt.gameObject.SetActive(shouldActivate);
        if (shouldActivate) tasktimetxt.text = time;
        
        // 控制 TimeObj 的黑色底板 Image
        if (tasktimetxt.transform.parent != null)
        {
            Image bg = tasktimetxt.transform.parent.GetComponent<Image>();
            if (bg != null) 
            {
                // 如果时间文字显示，或者任务已完成状态，底板才显示
                bg.enabled = shouldActivate || taskOver.gameObject.activeSelf;
            }
        }
    }

    private void InitLimtBtnUI()
    {
        if (!LimitTimeBtn.interactable)
        {
            LimitTimeObj.gameObject.SetActive(true);
            timetxt.gameObject.SetActive(true);
            timetxt.text = $"第{AppGameSettings.UnlockRequirements.TimeLimitMode}关";
            
            LimitClaim.gameObject.SetActive(false);
            Worddouble.gameObject.SetActive(false);
            limitOver.gameObject.SetActive(false);
            
            Image lockBg = LimitTimeObj.GetComponent<Image>();
            if (lockBg != null) lockBg.enabled = true;
            
            if (timetxt.transform.parent != null)
            {
                Image parentBg = timetxt.transform.parent.GetComponent<Image>();
                if (parentBg != null) parentBg.enabled = true;
            }
            return;
        }
        
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
        Image bg = LimitTimeObj.GetComponent<Image>();
        if (bg != null) 
        {
            bg.enabled = (timetxt.gameObject.activeSelf && !string.IsNullOrEmpty(timetxt.text)) 
                         || limitOver.gameObject.activeSelf;
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
        AnalyticMgr.PopShow("禅意之境引流");
        Application.OpenURL("https://appgallery.huawei.com/app/detail?id=chengyu.idiom.hexa.zen.huawei");
    }
    
    private void OnButterflyClick()
    {
        
        if (GameCoreManager.Instance.PanelState == PanelState.MainMenuPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.PrimaryInterface);
        }else if (GameCoreManager.Instance.PanelState == PanelState.FinishXiaoPanel||
            GameCoreManager.Instance.PanelState == PanelState.FinishHexPanel)
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

    #region 主页礼包入口（破冰 / 限时折扣）

    /// <summary>
    /// 刷新主页的「破冰礼包 / 限时折扣礼包」入口按钮。
    /// 数据源与结算页完全一致（ShopManager.CurrentSettlementGift）：
    ///   - 结算页登记了破冰礼包 → 显示破冰入口；
    ///   - 登记了复购/升层礼包 → 显示限时折扣入口；
    ///   - 没有登记 / 已购买 / 已过期 → 两个都隐藏。
    /// </summary>
    public void RefreshSettlementGiftEntry()
    {
        // ClearSettlementGift 会回调 OnSettlementGiftChanged → 再次进入本方法，这里挡一下重入
        if (_refreshingGiftEntry) return;
        _refreshingGiftEntry = true;

        ShopManager.SettlementGiftKind kind = ShopManager.shopManager != null
            ? ShopManager.shopManager.CurrentSettlementGift
            : ShopManager.SettlementGiftKind.None;

        bool showBreakIce = kind == ShopManager.SettlementGiftKind.BreakIce;
        bool showDiscount = kind == ShopManager.SettlementGiftKind.Repurchase;

        // 登记还在但限时已经过了（比如玩家挂后台很久） → 收起入口并清掉登记，避免展示过期礼包
        if ((showBreakIce || showDiscount) && ShopManager.shopManager != null)
        {
            DateTime endTime = ShopManager.shopManager.GetSettlementGiftEndTime();
            if (endTime != DateTime.MinValue && DateTime.Now >= endTime)
            {
                ShopManager.shopManager.ClearSettlementGift("主页礼包入口已过期");
                showBreakIce = false;
                showDiscount = false;
            }
        }

        SetGiftEntryVisible(breakIceGiftBtn, showBreakIce);
        SetGiftEntryVisible(discountGiftBtn, showDiscount);

        RefreshGiftEntryCountdown();

        _refreshingGiftEntry = false;
    }

    /// <summary>入口按钮显隐：显示时淡入 + 轻微放大（与结算页礼包按钮一致）。</summary>
    private void SetGiftEntryVisible(Button btn, bool visible)
    {
        if (btn == null) return;

        GameObject go = btn.gameObject;
        if (go.activeSelf == visible) return;

        RectTransform rt = btn.transform as RectTransform;

        if (!visible)
        {
            if (rt != null) rt.DOKill();
            go.SetActive(false);
            return;
        }

        go.SetActive(true);
        if (rt == null) return;

        CanvasGroup group = rt.GetComponent<CanvasGroup>();
        if (group == null) group = go.AddComponent<CanvasGroup>();

        rt.DOKill();
        group.DOKill(false);

        group.alpha = 0f;
        rt.localScale = Vector3.one * 0.9f;

        group.DOFade(1f, 0.18f).SetEase(Ease.OutQuad);
        rt.DOScale(Vector3.one, 0.24f).SetEase(Ease.OutBack);
    }

    /// <summary>
    /// 入口按钮上的限时倒计时：按登记的截止时间每秒刷新；归零后收起按钮并清掉登记。
    /// 没有限时（截止时间为空）→ 只隐藏时间条，按钮保留。
    /// </summary>
    private void RefreshGiftEntryCountdown()
    {
        CacheGiftEntryTimeNodes();
        StopSettlementGiftCountdown();

        if (!IsGiftEntryVisible()) return;

        DateTime endTime = ShopManager.shopManager != null
            ? ShopManager.shopManager.GetSettlementGiftEndTime()
            : DateTime.MinValue;

        double remain = (endTime - DateTime.Now).TotalSeconds;
        if (endTime == DateTime.MinValue || remain <= 0)
        {
            ApplyGiftEntryTime(false, 0);
            return;
        }

        ApplyGiftEntryTime(true, remain);
        if (isActiveAndEnabled)
            _settlementGiftCoroutine = StartCoroutine(GiftEntryCountdownCoroutine());
    }

    private IEnumerator GiftEntryCountdownCoroutine()
    {
        WaitForSeconds wait = new WaitForSeconds(1f);

        while (IsGiftEntryVisible())
        {
            DateTime endTime = ShopManager.shopManager != null
                ? ShopManager.shopManager.GetSettlementGiftEndTime()
                : DateTime.MinValue;

            if (endTime == DateTime.MinValue)
            {
                ApplyGiftEntryTime(false, 0);
                break;
            }

            double remain = (endTime - DateTime.Now).TotalSeconds;
            if (remain <= 0)
            {
                ApplyGiftEntryTime(false, 0);
                SetGiftEntryVisible(breakIceGiftBtn, false);
                SetGiftEntryVisible(discountGiftBtn, false);
                if (ShopManager.shopManager != null)
                    ShopManager.shopManager.ClearSettlementGift("主页礼包入口限时结束");
                Debug.Log("[PrimaryInterface] 礼包入口限时结束，按钮收起");
                break;
            }

            ApplyGiftEntryTime(true, remain);
            yield return wait;
        }

        _settlementGiftCoroutine = null;
    }

    private void StopSettlementGiftCountdown()
    {
        if (_settlementGiftCoroutine != null)
        {
            StopCoroutine(_settlementGiftCoroutine);
            _settlementGiftCoroutine = null;
        }
    }

    private bool IsGiftEntryVisible()
    {
        return (breakIceGiftBtn != null && breakIceGiftBtn.gameObject.activeSelf)
            || (discountGiftBtn != null && discountGiftBtn.gameObject.activeSelf);
    }

    private void ApplyGiftEntryTime(bool show, double remainSeconds)
    {
        SetOneGiftEntryTime(breakIceGiftBtn, _breakIceTimeObj, _breakIceTimeText, show, remainSeconds);
        SetOneGiftEntryTime(discountGiftBtn, _discountTimeObj, _discountTimeText, show, remainSeconds);
    }

    private void SetOneGiftEntryTime(Button btn, GameObject timeObj, Text timeText, bool show, double remainSeconds)
    {
        if (btn == null || !btn.gameObject.activeSelf) return;

        bool visible = show && remainSeconds > 0;
        if (timeObj != null && timeObj.activeSelf != visible) timeObj.SetActive(visible);

        // 格式与结算页 / AdsDiscountScreen 保持一致（X小时Y分）
        if (timeText != null && visible)
            timeText.text = UIUtilities.FormatTimeRemaining(TimeSpan.FromSeconds(remainSeconds));
    }

    /// <summary>按钮下的倒计时节点（TimeObj/TimeText）没绑定时按路径自动查找。</summary>
    private void CacheGiftEntryTimeNodes()
    {
        if (_breakIceTimeText == null && breakIceGiftBtn != null)
            ResolveGiftEntryTimeNodes(breakIceGiftBtn.transform, out _breakIceTimeObj, out _breakIceTimeText);

        if (_discountTimeText == null && discountGiftBtn != null)
            ResolveGiftEntryTimeNodes(discountGiftBtn.transform, out _discountTimeObj, out _discountTimeText);
    }

    private static void ResolveGiftEntryTimeNodes(Transform btn, out GameObject timeObj, out Text timeText)
    {
        timeObj = null;
        timeText = null;

        Transform timeNode = btn.Find("TimeObj");
        if (timeNode == null) return;

        timeObj = timeNode.gameObject;
        timeText = timeNode.GetComponentInChildren<Text>(true);
    }

    /// <summary>破冰礼包入口：打开破冰礼包弹窗。</summary>
    private void OnBreakIceGiftBtnClick()
    {
        Debug.Log("[PrimaryInterface] 点击破冰礼包入口");

        if (ShopManager.shopManager == null
            || !ShopManager.shopManager.HasSettlementGift(ShopManager.SettlementGiftKind.BreakIce))
        {
            RefreshSettlementGiftEntry();
            return;
        }

        ShopManager.shopManager.OpenSettlementGiftPopup();
    }

    /// <summary>限时折扣礼包入口（复购/升层）：打开限时折扣礼包弹窗。</summary>
    private void OnDiscountGiftBtnClick()
    {
        Debug.Log("[PrimaryInterface] 点击限时折扣礼包入口");

        if (ShopManager.shopManager == null
            || !ShopManager.shopManager.HasSettlementGift(ShopManager.SettlementGiftKind.Repurchase))
        {
            RefreshSettlementGiftEntry();
            return;
        }

        ShopManager.shopManager.OpenSettlementGiftPopup();
    }

    #endregion
    /// <summary>
    /// 当对象禁用时调用
    /// </summary>
    protected override void OnDisable()
    {
        //EventManager.OnChangeLanguageUpdateUI -= InitUI;
        EventDispatcher.instance.OnChangeGoldUI -= InitUI;
        
        LimitTimeManager.Instance.OnLimitTimeBtnUI -= InitLimtBtnUI;
        DailyTaskManager.Instance.OnDailyTaskBtnUI -= UpdateDailyTaskBtnUI;
        DailyTaskManager.Instance.OnDailyButterflyTaskUI -= UpdateButterflyTime;
        EventDispatcher.instance.OnChangeHeadIconUpdateUI -= UpdateHeadBtnUI;
        FishInfoController.Instance.OnFishTimeUpdated -= UpdateFishTime;
        if (OverallRankingManager.Instance != null)
        {
            OverallRankingManager.Instance.OnAuditConfigLoaded -= OnAuditConfigRefreshUI;
        }
        if (ThemeManager.Instance != null)
            ThemeManager.Instance.OnSkinRedPointChanged -= OnRedPointChanged;

        if (ShopManager.shopManager != null)
            ShopManager.shopManager.OnSettlementGiftChanged -= RefreshSettlementGiftEntry;
        StopSettlementGiftCountdown();
 
        if (!LimitTimeManager.Instance.IsComplete())
        {
            LimitTimeManager.Instance.OnLimitTimeUpdated -= UpdateTimeDisplay; // 订阅事件
        }
 
        if (!DailyTaskManager.Instance.IsAllComplete())
        {
            LimitTimeManager.Instance.OnDailyTimeUpdated -= UpdateDailyTaskTimeDisplay; // 订阅事件
        }
        base.OnDisable();
    }

    /// <summary>
    /// 初始化UI
    /// </summary>
    public void InitUI(int index=0,bool active=false)
    {
        // 设置关卡文本
        int Stage = 0;
        LevelModes difficulty = LevelModes.Normal;
        // 设置模式图标
        Sprite sprite = null;
        int currentPlayMode = GameDataManager.Instance.UserData.levelMode;
        switch (currentPlayMode)
        {
            case 1:
            case 3:
                Stage = GameDataManager.Instance.UserData.CurrentHexStage != 0 ? 
                    GameDataManager.Instance.UserData.CurrentHexStage : 1;
                sprite = LoadCommonIcon("icon_xiao");
                StageHexController.Instance.CurLevelMode = GetLevelDifficulty(Stage);
                difficulty = StageHexController.Instance.CurLevelMode;
                break;
            case 2:
                Stage = GameDataManager.Instance.UserData.CurrentChessStage != 0 ? 
                    GameDataManager.Instance.UserData.CurrentChessStage : 1;
                sprite = LoadCommonIcon("icon_pinzi");
                ChessStageController.Instance.CurLevelMode = GetLevelDifficulty(Stage);
                difficulty = ChessStageController.Instance.CurLevelMode;
                break;
        }
        if(sprite != null && ModeBtn != null)
            ModeBtn.GetComponent<Image>().sprite = sprite;
        
        Stage=Stage==0?1:Stage;
        // HexWord 的 PrimaryInterface 预制体没有 WT 的「关卡/下一元素」这一组节点，统一做空保护
        if (Stagetxt != null)
            Stagetxt.text = MultilingualManager.Instance.GetString("Level")+" " + Stage;

        if (currentPlayMode != 2)
        {
            // 1. 开启自身专属 UI
            if (hardStageTable != null) hardStageTable.gameObject.SetActive(difficulty == LevelModes.Hard);
            if (extrahardStageTable != null) extrahardStageTable.gameObject.SetActive(difficulty == LevelModes.ExtraHard);
        
            // 2. 强制关闭拼字模式专属 UI，防止残留
            if (nextUnlockTip != null) nextUnlockTip.SetActive(false);
            // 如果非拼字模式不需要展示底图和占位，可以在这里补上隐藏：
            if (stageSpace != null) stageSpace.SetActive(false);
            if (specialStageContent != null) specialStageContent.SetActive(false);
            if (Stagetxt != null) Stagetxt.gameObject.SetActive(true);
        }
        else
        {
            // 1. 强制关闭消消乐模式专属的难度 UI，防止残留
            if (hardStageTable != null) hardStageTable.gameObject.SetActive(false);
            if (extrahardStageTable != null) extrahardStageTable.gameObject.SetActive(false);
            
            int stageBgIndex = ChessStageController.Instance.GetLevelGameplayID(Stage);
            if (nextStageImage != null)
                nextStageImage.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("stageBtnBg" + stageBgIndex, "UI_MainBase");
            if (stageSpace != null)
                stageSpace.SetActive(stageBgIndex is not (0 or 4 or 5 or 6));

            // 普通关卡：用 levelImage 上的关卡文本；特殊关卡：改用 content 显示该关的图（stageBtnBg{index}）+ 关卡数
            // 特殊关卡 = 困难(1)/极难(2)/记忆(3)/emoji(7)；冰块(4)/花朵(5)/树叶(6) 仍走普通关卡那套（levelImage 图集 + levelImage 上的文本）
            bool isSpecialStage = stageBgIndex is not (0 or 4 or 5 or 6);
            if (Stagetxt != null)
                Stagetxt.gameObject.SetActive(!isSpecialStage);
            if (specialStageContent != null)
            {
                specialStageContent.SetActive(isSpecialStage);
                if (isSpecialStage)
                {
                    Image specialImage = specialStageContent.GetComponent<Image>();
                    if (specialImage != null)
                        specialImage.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("stageBtnBg" + stageBgIndex, "UI_MainBase");
                    Text specialText = specialStageContent.GetComponentInChildren<Text>(true);
                    if (specialText != null)
                        specialText.text = Stagetxt != null ? Stagetxt.text : MultilingualManager.Instance.GetString("Level") + " " + Stage;
                }
            }
            // var unlockHint = ChessStageController.Instance.GetNextMechanicUnlockHint(Stage);
            // if (unlockHint.HasValue)
            // {
            //     nextUnlockTip.SetActive(true);
            //     // 从多语言表获取原始文案，例如 "还差{a}关解锁花朵"
            //     string rawText = MultilingualManager.Instance.GetString(unlockHint.Value.textKey,"pingzi");
            //     // 替换占位符并赋值给 UI Text
            //     nextUnlockTip.GetComponentInChildren<Text>().text = rawText.Replace("{a}", unlockHint.Value.remainCount.ToString());
            // }
            // else
            // {
            //     // 没有要预告的机制，隐藏提示气泡
            //     nextUnlockTip.SetActive(false);
            // }
        }
        
        UpdateWinTimes();
        // ButterflyRedpoint.gameObject.SetActive(ButterfliesManager.Instance.showButterflyRedPoint);
        headRedpoint.gameObject.SetActive(GameDataManager.Instance.UserData.isGetNewHeadIcon);
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
        UIWindow uiWindow = SystemManager.Instance.ShowPanel(PanelType.HeaderSection);
        // 顶栏入场与大厅本体保持一致：仅首次进大厅时按 2x 播放（Top 状态 0.6 倍速，0.333s clip ≈ 0.56s）
        if (_fastEnterThisShow)
            uiWindow?.PlayEnterAtSpeed(LobbyEnterSpeed);
        Debug.Log("查看是否展示" + uiWindow.IsWindowVisible + " " + uiWindow.gameObject.activeSelf);

        yield return new WaitForSeconds(_fastEnterThisShow ? TopPanelRevealGapFast : TopPanelRevealGap);
        EventDispatcher.instance.TriggerUpdateLayerCoin(false,true,false);
        
        yield return new WaitForSeconds(0.1f);
        // 刷新新手奖励入口显示，并置顶到右侧最高优先级
        if (_beginnerBonusEntryBtn != null)
        {
            _beginnerBonusEntryBtn.RefreshVisibility();
            if (_beginnerBonusEntryBtn.gameObject.activeSelf)
            {
                _beginnerBonusEntryBtn.transform.SetAsFirstSibling();
                _beginnerBonusEntryBtn.SetPreviousBorderProgress(); // 先钉在旧刻度上
            }
        }
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
        bool hasUnlimitedEnergy = GameDataManager.Instance.UserData.HasUnlimitedEnergy();
        if (!hasUnfinishedSave&& !hasUnlimitedEnergy)
        {
            // 🌟 核心拦截：检查并扣除体力！
            // ConsumeEnergy 会自动处理：如果是第一关返回 true 不扣体力，如果体力>=1返回 true 并扣除。
            if (ChessStageController.Instance.CurrentStage != 1 && GameDataManager.Instance.UserData.Energy <= 0)
            {
                // 体力不足，拦截并提示玩家
                // MessageSystem.Instance.ShowTip("体力不足，休息一下吧！");
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
                    StageHexController.Instance.SetStageData(StageHexController.Instance.CurrentStage);
                    break;
                case 2:
                    ChessStageController.Instance.SetStageData(ChessStageController.Instance.CurrentStage);
                    break;
                case 3:
                    StageHexController.Instance.SetStageData(StageHexController.Instance.CurrentStage);
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

        Game.self.Ads.OnEnterGameScene();
    }
    
    private Sprite LoadheadIcon(string showIcon)
    {
        return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(showIcon,"UserHeadIcons");
    }
    
    private Sprite LoadCommonIcon(string showIcon)
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
                SystemManager.Instance.ShowPanel(PanelType.GamePlayArea);
                break;
            case 2:
                SystemManager.Instance.ShowPanel(PanelType.ChessPlayArea);
                break;
            case 3:
                SystemManager.Instance.ShowPanel(PanelType.HexGamePlayArea);
                break;
        }
    }

}