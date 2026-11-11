using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using FourWordIdiom.LocalGame.GameScripts.Controller;
using UnityEngine;
using UnityEngine.UI;

public class ChessBoxItem
{
    public int Id;
    public int Level;
    public List<KeyValuePair<int, int>> Rewards;
}

public class ChessFinishView : UIWindow
{
    [Header("活动按钮")]
    // [SerializeField]
    //[SerializeField] private Button _signBtn;
    [SerializeField] private LimitBtnTable _limitBtnTable;
    [SerializeField] private MatchFishTable _matchFishtable;
    [SerializeField] private TaskTable _tasktable;
    [SerializeField] private Button butterflyBtn;
    [SerializeField] private Button removeAdsBtn; //去广告限时按钮
    [SerializeField] private Button removeAdsGiftBtn; //破冰礼包按钮（去广告一周礼包）
    [SerializeField] private Button giftButton; //限时折扣礼包按钮（复购/升层）
    [SerializeField] private Text removeAds; //去广告限时文本
    // 注：结算页「下一关」按钮上的困难/极难角标已按 WT 移除（hardTable / tehardTable 节点连同引用一并删掉）

    
    [Header("新手奖励活动")]
    [SerializeField] private BeginnerBonusEntryBtn _beginnerBonusEntryBtn; // 结算页右侧的新手奖励入口按钮组件
    
    [Header("禅意飞行特效")]
    [SerializeField] private Text zenScoreText;
    [SerializeField] private ZenRankButton _zenRankBtn;    // 段位榜入口按钮
    [SerializeField] private Image _centerLotusImage;     // 场景里中间那个荷花的 Image 组件
    
    [Space]
    [Header("下一关按钮")]
    [SerializeField] private Button nextStageBtn;
    [SerializeField] private Text levelText;
    [SerializeField] private GameObject stageSpace;
    [SerializeField] private Image nextStageImage;
    [SerializeField] private GameObject specialStageContent; // 特殊关卡：content 里显示该关的图 + 关卡数
    [SerializeField] private GameObject nextUnlockTip;
    [SerializeField] private GameObject _butterflyTimerDisplay;
    
    [Header("蝶园按钮")]
    [SerializeField] private GameObject ButterflyRedpoint;
    [SerializeField] private GameObject ButterflyLock;
    [SerializeField] private GameObject ButterflyLockText;
    
    [Space]
    [Header("限时活动")]
    [SerializeField] private Button Content;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private Text progressText;
    [SerializeField] private Text reachLevelText;
    [SerializeField] private GameObject Showlimiticon;
    [SerializeField] private GameObject Enlimiticon;
    
    [Space]
    [Header("横幅鼓励提示 UI (新增)")]
    [Header("结算页激励文案UI节点 (需手动挂载)")]
    [SerializeField] private GameObject _encourageTitleRoot;   // 顶部标题的父节点 (比如"新纪录！")
    [SerializeField] private Text _encourageTitleText;
    [SerializeField] private GameObject _encouragePhraseRoot;  // 激励长文案的父节点
    [SerializeField] private Text _encouragePhraseText;
    [SerializeField] private Image _encourageEmojiIcon;
    
    private GameObject _treasureBoxEffect;
    private GameObject lotusInstance;
    private GameObject _pupaPrefab;
    private ObjectPool _pupaPool;
    private int _currentProgressSegment = 0;

    private float sliderProgress;

    // 防御数据双重累加的标记
    private bool _isWordsExtracted = false;
    // 标记是否已经播放过本局的奖励和动画
    private bool _hasPlayedRewardSequence = false;
    private bool isTriggerFirstWin;
    private bool isShowWinSign;

    // ======================= 限时去广告 BUFF 相关 =======================
    /// <summary>限时去广告倒计时协程</summary>
    private Coroutine _removeAdsCoroutine = null;
    /// <summary>限时去广告 BUFF 图标根节点（若未单独配置，则退化为 removeAdsBtn 本身）</summary>
    private GameObject _removeAdsBuffRoot;

    // ======================= 结算页礼包按钮倒计时 =======================
    /// <summary>结算页礼包按钮倒计时协程</summary>
    private Coroutine _settlementGiftCoroutine = null;
    private GameObject _breakIceTimeObj;
    private Text _breakIceTimeText;
    private GameObject _giftTimeObj;
    private Text _giftTimeText;

    // ======================= 禅意分滚动音效 =======================
    /// <summary>禅意分滚动期间循环播放的音源，滚动结束后需停止回收</summary>
    private AudioSource _zenRollSfx = null;
    
    // 记录当前UI显示的蝶蛹数量，供动画依次递增，防止跳跃
    private int _currentVisualPupa = -1;
    
    protected override void InitializeUIComponents()
    {
        nextStageBtn.AddVibraClickAction(OnNextButtonClick);
        _limitBtnTable._limitTimeEventButton.AddClickAction(OnLimitTimeEventButtonClicked);
       
        Content.onClick.AddListener(() =>
        {
            SystemManager.Instance.ShowPanel(PanelType.LimitTimeScreen);
            
            AchievementManager.Instance.DisableFinishAchieveTable();
        });
        butterflyBtn.AddClickAction(OnButterflyClick);

        // 去广告 BUFF 按钮点击事件
        if (removeAdsBtn != null)
        {
            removeAdsBtn.AddClickAction(OnRemoveAdsBtnClick);
            _removeAdsBuffRoot = removeAdsBtn.gameObject;
        }

        // ======================= 结算页礼包入口（破冰 / 限时折扣） =======================
        // 两个按钮默认隐藏，等结算策略（TryShowSettlementGiftPopup）决定后由 RefreshSettlementGiftButtons 显示
        if (removeAdsGiftBtn != null)
        {
            removeAdsGiftBtn.AddVibraClickAction(OnRemoveAdsGiftBtnClick);
            removeAdsGiftBtn.gameObject.SetActive(false);
        }
        if (giftButton != null)
        {
            giftButton.AddVibraClickAction(OnGiftButtonClick);
            giftButton.gameObject.SetActive(false);
        }
        
        // 初始化莲花状态（放在屏幕中央或者特定初始位置）
        GameObject prefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "lotus_icon");
        lotusInstance = Instantiate(prefab, _centerLotusImage.transform, false);
        _pupaPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "Pupa");
        _pupaPool = new ObjectPool(_pupaPrefab, transform, 2, PoolBehaviour.GameObject);
        CustomFlyInManager.Instance.finishlevelBtnObj = nextStageBtn.gameObject;
    }

    private IEnumerator FetchLeaderboardDataMod(string leaderboardName)
    {
       yield return StartCoroutine(ZenRankManager.Instance.FetchLeaderboardDataRoutine(leaderboardName));
       _zenRankBtn.SyncTextFromCache();
    }
    
    protected override void OnEnable()
    {
        base.OnEnable();
        GameCoreManager.Instance.PanelState = PanelState.FinishPingPanel;
        SystemManager.Instance.ShowPanel(PanelType.HeaderSection);
        // 强制先刷新按钮状态
        if (_zenRankBtn != null)
            _zenRankBtn.GetComponent<ZenRankButton>().CheckRankProgress();

        if (_zenRankBtn != null && _zenRankBtn.gameObject.activeSelf)
        {
            string boardId = GameDataManager.Instance.UserData.Zenlevel;
            if (!string.IsNullOrEmpty(boardId))
            {
                StartCoroutine(FetchLeaderboardDataMod(boardId));
            }
        }
        //  利用现有的 curIsEnter 判定是否是刚打完一局全新进入的
        if (GameDataManager.Instance.UserData.curIsEnter)
        {
            _hasPlayedRewardSequence = false;
        }
        // 锁定当前的视觉蝶蛹数量，作为所有动画的基础起始点
        int oldPupaCount = GameDataManager.Instance.ButterflyData.currPupa -
                           ChessStageController.Instance.EarnedPupaThisLevel;
        _currentVisualPupa = Mathf.Max(0, oldPupaCount);
        InitializeUI();
        UnlockBtnsUI();
        GameDataManager.Instance.UserData.curIsEnter = false;
        LimitTimeManager.Instance.OnDailyTimeUpdated += UpdateTimeDisplay;
        LimitTimeManager.Instance.OnLimitTimeBtnUI += UpdateSliderProgress;
        DailyTaskManager.Instance.OnDailyButterflyTaskUI += UpdateButterflyTime;
        FishInfoController.Instance.OnFishTimeUpdated += _matchFishtable.UpdateFishTime;
        AudioManager.Instance.PlaySoundEffect("StageFinish");   
        
        StartCoroutine(UpdateFishRankUI());
        DailyTaskManager.Instance.UpateButterflyTaskUI();
        butterflyBtn.gameObject.SetActive(ButterfliesManager.Instance.IsOpen);
        // 去重：oldPupaCount 已在方法开头声明，这里直接复用（同步合并时重复引入）
        UpdateButterflyProgressUI(Mathf.Max(0, oldPupaCount), true);

        if (GameDataManager.Instance.UserData.ischangetheme)
        {
            int times = 1;
            if (GameDataManager.Instance.UserData.ThemeItemUses.Keys.Contains(GameDataManager.Instance.UserData
                    .userthemeid))
            {
                times = GameDataManager.Instance.UserData.ThemeItemUses[GameDataManager.Instance.UserData.userthemeid];
            }

            AnalyticMgr.ThemeUse(GameDataManager.Instance.UserData.userthemeid, times);
        }
        isShowWinSign = StreakManager.Instance.IsCanShowWinSign();
        // 注：CheckFishiTable() 为旧版遗留调用，HexWord 已无该方法（捕鱼入口由 UnlockBtnsUI + MatchFishTable.CheckFishBtn 处理）

        // ======================= 刷新限时去广告 BUFF =======================
        RefreshRemoveAdsBuff();

        // 结算页礼包入口（破冰 / 限时折扣）：按当前登记状态刷新（策略尚未决定时两个都隐藏）
        RefreshSettlementGiftButtons();
    }

    // ======================= 限时去广告 BUFF 逻辑 =======================

    /// <summary>
    /// 刷新限时去广告 BUFF 的显示与倒计时协程。
    /// - 处于限时去广告期间：显示 removeAdsBtn，启动每秒刷新协程；
    /// - 否则：隐藏 removeAdsBtn，停止协程。
    /// </summary>
    private void RefreshRemoveAdsBuff()
    {
        if (removeAdsBtn == null) return;

        // 停旧协程（避免重复启动）
        if (_removeAdsCoroutine != null)
        {
            StopCoroutine(_removeAdsCoroutine);
            _removeAdsCoroutine = null;
        }

        bool isActive = IsLimitRemoveAdsActive();

        if (isActive)
        {
            removeAdsBtn.gameObject.SetActive(true);
            // 立即刷新一次文本，避免等待 1 秒
            UpdateRemoveAdsBuffText();
            _removeAdsCoroutine = StartCoroutine(RemoveAdsBuffTimerCoroutine());
            Debug.Log($"[ChessFinishView] 限时去广告 BUFF 显示, 剩余={ShopManager.shopManager?.GetRemoveAdsRemainSeconds():F0}s");
        }
        else
        {
            removeAdsBtn.gameObject.SetActive(false);
            Debug.Log("[ChessFinishView] 限时去广告 BUFF 隐藏（未生效或已过期）");
        }
    }

    /// <summary>是否处于"限时去广告"生效期间（7天/1天）</summary>
    private bool IsLimitRemoveAdsActive()
    {
        if (ShopManager.shopManager == null) return false;

        // 已拥有永久去广告：BUFF 不需要展示（永久去广告已通过设置页/其他入口展示）
        if (ShopManager.shopManager.IsRemoveAdsOwned()) return false;

        // 限时去广告生效期间
        if (!ShopManager.shopManager.IsRemoveAdsActive()) return false;

        // 剩余时间 > 0
        return ShopManager.shopManager.GetRemoveAdsRemainSeconds() > 0;
    }

    /// <summary>
    /// 限时去广告倒计时协程：每秒刷新一次文本，倒计时结束隐藏 BUFF。
    /// </summary>
    private IEnumerator RemoveAdsBuffTimerCoroutine()
    {
        WaitForSeconds wait = new WaitForSeconds(1f);
        while (true)
        {
            if (!IsLimitRemoveAdsActive())
            {
                // 时间到期或状态失效 → 隐藏 BUFF，退出协程
                if (removeAdsBtn != null)
                    removeAdsBtn.gameObject.SetActive(false);
                Debug.Log("[ChessFinishView] 限时去广告已结束，BUFF 自动隐藏");
                _removeAdsCoroutine = null;
                yield break;
            }

            UpdateRemoveAdsBuffText();
            yield return wait;
        }
    }

    /// <summary>刷新 removeAds 文本：显示剩余时间（如 "1天 23:59:59" / "05:30"）</summary>
    private void UpdateRemoveAdsBuffText()
    {
        if (removeAds == null) return;
        if (ShopManager.shopManager == null)
        {
            removeAds.text = "";
            return;
        }

        double remainSeconds = ShopManager.shopManager.GetRemoveAdsRemainSeconds();
        removeAds.text = FormatRemoveAdsRemainTime(remainSeconds);
    }

    /// <summary>
    /// 剩余时间格式化：与复购礼包 / AdsDiscountScreen 的倒计时统一（X小时Y分，走多语言符号）。
    /// 以前这里是「X天 HH:mm:ss / HH:mm:ss / mm:ss」。
    /// </summary>
    private string FormatRemoveAdsRemainTime(double remainSeconds)
    {
        return UIUtilities.FormatTimeRemaining(TimeSpan.FromSeconds(Math.Max(0d, remainSeconds)));
    }

    /// <summary>
    /// 点击限时去广告 BUFF：弹窗说明当前状态和剩余时间倒计时。
    /// 需求："点击弹窗说明当前状态和剩余时间倒计时，点了解按钮或关闭按钮，关闭弹窗"
    /// TODO：确认 PanelType 名称与弹窗脚本，当前调用示例为 PanelType.RemoveAdsScreen
    /// </summary>
    private void OnRemoveAdsBtnClick()
    {
        Debug.Log($"[ChessFinishView] 点击限时去广告 BUFF, 剩余={ShopManager.shopManager?.GetRemoveAdsRemainSeconds():F0}s");

        // 展示弹窗（RemoveAdsScreen 已在 ShopItem.BuyRemoveAdsEvent 中被调用，此处复用同一面板）
        SystemManager.Instance.ShowPanel(PanelType.RemoveAdsScreen);
    }

    // ======================= 结算页礼包入口（破冰 / 限时折扣） =======================

    /// <summary>
    /// 刷新「破冰礼包 / 限时折扣礼包」按钮的显隐。
    /// 由结算策略决定后调用（ChessStageController 结算流程 + AdsDiscountScreen 关闭时）：
    ///   - 当前结算页登记的是破冰礼包 → 显示 removeAdsGiftBtn；
    ///   - 登记的是复购/升层礼包 → 显示 giftButton；
    ///   - 没有登记（未命中策略 / 已购买）→ 两个都隐藏。
    /// </summary>
    public void RefreshSettlementGiftButtons()
    {
        ShopManager.SettlementGiftKind kind = ShopManager.shopManager != null
            ? ShopManager.shopManager.CurrentSettlementGift
            : ShopManager.SettlementGiftKind.None;

        bool showBreakIce = kind == ShopManager.SettlementGiftKind.BreakIce;
        bool showRepurchase = kind == ShopManager.SettlementGiftKind.Repurchase;

        SetSettlementGiftButtonVisible(removeAdsGiftBtn, showBreakIce);
        SetSettlementGiftButtonVisible(giftButton, showRepurchase);

        // 按钮上的限时倒计时（TimeObj/TimeText）跟着礼包的结束时间走
        RefreshSettlementGiftCountdown();

        string packId = ShopManager.shopManager != null ? ShopManager.shopManager.GetSettlementGiftPackId() : string.Empty;
        DateTime giftEndTime = ShopManager.shopManager != null
            ? ShopManager.shopManager.GetSettlementGiftEndTime()
            : DateTime.MinValue;

        Debug.Log($"[ChessFinishView] 结算礼包按钮刷新: kind={kind}, 破冰按钮={showBreakIce}, 限时折扣按钮={showRepurchase}, " +
                  $"礼包={packId}, 截止时间={giftEndTime:yyyy-MM-dd HH:mm:ss}");
    }

    /// <summary>
    /// 结算页礼包按钮的显隐：
    ///   - 显示：淡入 + 轻微放大（0.24s），避免跟着结算页「啪」一下冒出来显得突兀；
    ///   - 隐藏：直接关掉并杀掉动画。
    /// 只有状态真的变化时才处理，避免每次刷新都重播动画。
    /// </summary>
    private void SetSettlementGiftButtonVisible(Button btn, bool visible)
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
    /// 结算页礼包按钮上的倒计时：按登记礼包的限时截止时间每秒刷新。
    ///  - 没有倒计时（限时为空）→ 只隐藏 TimeObj，按钮保留；
    ///  - 倒计时归零 → 收起按钮并清掉登记（礼包已过期，点开也没意义）。
    /// </summary>
    private void RefreshSettlementGiftCountdown()
    {
        CacheSettlementGiftTimeNodes();

        if (_settlementGiftCoroutine != null)
        {
            StopCoroutine(_settlementGiftCoroutine);
            _settlementGiftCoroutine = null;
        }

        if (!IsSettlementGiftButtonVisible()) return;

        DateTime endTime = ShopManager.shopManager != null
            ? ShopManager.shopManager.GetSettlementGiftEndTime()
            : DateTime.MinValue;

        double remain = (endTime - DateTime.Now).TotalSeconds;
        if (endTime == DateTime.MinValue || remain <= 0)
        {
            ApplySettlementGiftTime(false, 0);
            return;
        }

        ApplySettlementGiftTime(true, remain);
        if (isActiveAndEnabled)
            _settlementGiftCoroutine = StartCoroutine(SettlementGiftCountdownCoroutine());
    }

    /// <summary>每秒刷新按钮上的剩余时间，归零后收起按钮。</summary>
    private IEnumerator SettlementGiftCountdownCoroutine()
    {
        WaitForSeconds wait = new WaitForSeconds(1f);

        while (IsSettlementGiftButtonVisible())
        {
            DateTime endTime = ShopManager.shopManager != null
                ? ShopManager.shopManager.GetSettlementGiftEndTime()
                : DateTime.MinValue;

            if (endTime == DateTime.MinValue)
            {
                // 没有限时 → 不显示倒计时，按钮保留
                ApplySettlementGiftTime(false, 0);
                break;
            }

            double remain = (endTime - DateTime.Now).TotalSeconds;
            if (remain <= 0)
            {
                ApplySettlementGiftTime(false, 0);
                HideSettlementGiftButtons();
                if (ShopManager.shopManager != null)
                    ShopManager.shopManager.ClearSettlementGift("结算页礼包限时结束");
                Debug.Log("[ChessFinishView] 结算礼包限时结束，按钮收起");
                break;
            }

            ApplySettlementGiftTime(true, remain);
            yield return wait;
        }

        _settlementGiftCoroutine = null;
    }

    private bool IsSettlementGiftButtonVisible()
    {
        return (removeAdsGiftBtn != null && removeAdsGiftBtn.gameObject.activeSelf)
            || (giftButton != null && giftButton.gameObject.activeSelf);
    }

    private void HideSettlementGiftButtons()
    {
        if (removeAdsGiftBtn != null) removeAdsGiftBtn.gameObject.SetActive(false);
        if (giftButton != null) giftButton.gameObject.SetActive(false);
    }

    /// <summary>把倒计时刷到「当前可见」的那个礼包按钮上（另一个按钮隐藏时跳过）。</summary>
    private void ApplySettlementGiftTime(bool show, double remainSeconds)
    {
        SetOneSettlementGiftTime(removeAdsGiftBtn, _breakIceTimeObj, _breakIceTimeText, show, remainSeconds);
        SetOneSettlementGiftTime(giftButton, _giftTimeObj, _giftTimeText, show, remainSeconds);
    }

    private void SetOneSettlementGiftTime(Button btn, GameObject timeObj, Text timeText, bool show, double remainSeconds)
    {
        if (btn == null || !btn.gameObject.activeSelf) return;

        bool visible = show && remainSeconds > 0;
        if (timeObj != null && timeObj.activeSelf != visible) timeObj.SetActive(visible);

        // ★ 格式必须和 AdsDiscountScreen 的倒计时一致（同一个工具方法：X小时Y分）
        if (timeText != null && visible)
            timeText.text = UIUtilities.FormatTimeRemaining(TimeSpan.FromSeconds(remainSeconds));
    }

    /// <summary>按钮下的倒计时节点（TimeObj/TimeText）没绑定时按路径自动查找。</summary>
    private void CacheSettlementGiftTimeNodes()
    {
        if (_breakIceTimeText == null && removeAdsGiftBtn != null)
            ResolveSettlementGiftTimeNodes(removeAdsGiftBtn.transform, out _breakIceTimeObj, out _breakIceTimeText);

        if (_giftTimeText == null && giftButton != null)
            ResolveSettlementGiftTimeNodes(giftButton.transform, out _giftTimeObj, out _giftTimeText);
    }

    private static void ResolveSettlementGiftTimeNodes(Transform btn, out GameObject timeObj, out Text timeText)
    {
        timeObj = null;
        timeText = null;

        Transform timeNode = btn.Find("TimeObj");
        if (timeNode == null) return;

        timeObj = timeNode.gameObject;
        timeText = timeNode.GetComponentInChildren<Text>(true);
    }

    /// <summary>破冰礼包按钮（去广告一周）：打开破冰礼包弹窗。</summary>
    private void OnRemoveAdsGiftBtnClick()
    {
        Debug.Log("[ChessFinishView] 点击破冰礼包按钮");

        if (ShopManager.shopManager == null
            || !ShopManager.shopManager.HasSettlementGift(ShopManager.SettlementGiftKind.BreakIce))
        {
            // 礼包已经买过 / 登记被清掉 → 直接收起按钮，避免点了没反应
            RefreshSettlementGiftButtons();
            return;
        }

        ShopManager.shopManager.OpenSettlementGiftPopup();
    }

    /// <summary>限时折扣礼包按钮（复购/升层）：打开限时折扣礼包弹窗。</summary>
    private void OnGiftButtonClick()
    {
        Debug.Log("[ChessFinishView] 点击限时折扣礼包按钮");

        if (ShopManager.shopManager == null
            || !ShopManager.shopManager.HasSettlementGift(ShopManager.SettlementGiftKind.Repurchase))
        {
            RefreshSettlementGiftButtons();
            return;
        }

        ShopManager.shopManager.OpenSettlementGiftPopup();
    }
    
    /// <summary>
    /// 统一处理活动按钮的三态：未到展示等级隐藏 -> 达到展示未到解锁显示+上锁 -> 达到解锁完全放开
    /// </summary>
    private void SetupActivityButton(Button btn, GameObject lockObj, int showLevel, bool isLegacyOpened)
    {
        if (btn == null) return;

        int curStage = GameDataManager.Instance.UserData.CurrentChessStage;

        // 1. 显隐控制：未达到展示等级，直接隐藏
        bool isShow = curStage > showLevel || isLegacyOpened;
        btn.gameObject.SetActive(isShow);
        if (!isShow) return;

        // 2. 解锁控制：是否未解锁
        bool isLocked = !isLegacyOpened;

        // 3. 锁头物体显隐
        if (lockObj != null) 
        {
            lockObj.SetActive(isLocked);
        }

        // 4. 交互处理：
        // 如果想要按钮变灰/无法点击，设为 !isLocked；
        // 如果想要玩家点击时弹出“X关解锁”提示，可保持 interactable=true，在点击事件前置拦截。
        btn.interactable = !isLocked;
        if (btn.transform.childCount > 0)
        {
            Image icon = btn.transform.GetChild(0).GetComponent<Image>();
            if (icon != null)
            {
                Color c = icon.color;
                c.a = isLocked ? 0.5f : 1f; // 上锁半透明，解锁恢复100%
                icon.color = c;
            }
        }
    }
    private void RefreshButterflyButtonState()
    {
        if (butterflyBtn == null) return;

        int curStage = GameDataManager.Instance.UserData.CurrentChessStage;
        bool isLegacyOpened = ButterfliesManager.Instance.IsOpen;
        bool isShow = curStage > AppGameSettings.ShowRequirements.ButterflyGardenShowLevel || isLegacyOpened;
        butterflyBtn.gameObject.SetActive(isShow);
        if (!isShow) return;
        
        if (GameDataManager.Instance.UserData.CurrentHexStage >= AppGameSettings.UnlockRequirements.FishOpenLevel
            || GameDataManager.Instance.UserData.CurrentChessStage >= AppGameSettings.UnlockRequirements.FishOpenLevel)
        {
            // 补齐局部引用（原代码直接使用未声明的 slider / pupaText）
            Slider slider = butterflyBtn.GetComponentInChildren<Slider>(true);
            Text pupaText = butterflyBtn.GetComponentInChildren<Text>(true);
            if (slider != null) slider.gameObject.SetActive(true);
            if (pupaText != null) pupaText.gameObject.SetActive(true);
            UpdateButterflyProgressUI(_currentVisualPupa, true);
        }
    }
    
    
    private void CheckReturnFirstWinScreen()
    {
        int curStreak = StreakManager.Instance.GetCurrentStreak();
        int offlineSeconds= PlayerPrefs.GetInt("offline_Seconds", 0);
        int goldcount= GameDataManager.Instance.UserData.Gold;
        int todaywinTime = GameDataManager.Instance.UserData.chessdayPassStageCount;
        int toolcount = GameDataManager.Instance.UserData.toolInfo[102].count +
                        GameDataManager.Instance.UserData.toolInfo[104].count;

        //1296000 15天
        isTriggerFirstWin = offlineSeconds >=1296000 && todaywinTime <= 1 &&goldcount<= 300
                                 && toolcount <= 3 && curStreak == 1;
        
        Debug.Log("是否触发回归奖励: "+isTriggerFirstWin+"离线时间(秒数): "+offlineSeconds+"今日拼字玩法通关次数: "+todaywinTime+"金币数量: "+goldcount+"道具数量: "+toolcount+"连胜天数: "+curStreak);
        
    }

    /// <summary>
    /// 动态刷新顶部过关文本（处理限时活动“还差一关”的精准预判）
    /// </summary>
    private void UpdateLimitEventText()
    {
        int currStage = GameDataManager.Instance.UserData.CurrentChessStage;
        // 默认显示：普通关卡号
        string rawText = MultilingualManager.Instance.GetString("ScheduleLess", "pingzi");
        reachLevelText.text = rawText.Replace("X", currStage.ToString());

        if (LimitTimeManager.Instance == null || LimitTimeManager.Instance.CurlimitData == null)
            return;
        if (!_limitBtnTable._limitTimeEventButton.gameObject.activeSelf || !_limitBtnTable._limitTimeEventButton.interactable)    
            return;
        int pendingWords = _isWordsExtracted ? 0 : ChessStageController.Instance.LimitPuzzleCount;
        int totalDone = GetCompletedWordCount() + pendingWords; // 现在是总词数
        int totalTarget = LimitTimeManager.Instance.CurlimitData.num;
        int remainWords = totalTarget - totalDone;

        // 已经完成或超额，保留默认文本
        if (remainWords <= 0) return;

        bool isDoubleTimeActive = LimitTimeManager.Instance.LimitTimeCanShow();
        // 逐关累加后续关卡词组数，直到满足剩余词数
        int need = remainWords;
        int nextStage = currStage;
        int maxStage = currStage + 50; // 安全边界，防止无限循环
        while (need > 0 && nextStage < maxStage)
        {
            // 获取下一关的词组数（按 pass 中的 '#' 分割计数）
            int wordCount = 5; // 默认兜底
            try
            {
                var conf = ChessStageController.Instance.PackInfos.Get(nextStage);
                if (conf != null && !string.IsNullOrEmpty(conf.pass))
                {
                    wordCount = conf.pass.Split('#').Length;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UI预判] 获取关卡 {nextStage} 配置失败，使用兜底数量 5。原因: {e.Message}");
            }

            if (isDoubleTimeActive)
            {
                wordCount *= 2;
            }

            need -= wordCount;
            if (need > 0)
            {
                nextStage++; // 如果扣完还是不够，再推入下一关
            }
        }

        // 修复：因为顺序调整了，现在 nextStage == currStage 就代表“只需要打接下来的这一关”
        if (nextStage == currStage)
        {
            reachLevelText.text = MultilingualManager.Instance.GetString("OneMore", "pingzi");
        }
        else
        {
            // 需要多关，替换文本中的占位符
            reachLevelText.text = rawText.Replace("X", nextStage.ToString());
        }
    }

    private void InitializeUI()
    {
        _isWordsExtracted = false; //  每次打开面板，重置提取标记
        // _centerLotusImage.gameObject.SetActive(true);
        if (progressText != null) progressText.gameObject.SetActive(false);
        _centerLotusImage.color = new Color32(255, 255, 255, 0);
        _currentProgressSegment = 0;
        // 面板刚打开时，底层还没加本局词数，直接拿旧进度设定起跑线
        if (LimitTimeManager.Instance.CurlimitData != null)
        {
            int oldWordCount = GetCompletedWordCount();
            progressSlider.value = Mathf.Clamp01((float)oldWordCount / LimitTimeManager.Instance.CurlimitData.num);
        }
        
        UpdateLimitEventText();
        if (LimitTimeManager.Instance.IsComplete())
            progressSlider.transform.parent.gameObject.SetActive(false);
        else
            progressSlider.transform.parent.gameObject.SetActive(true);

        // 设置关卡文本
        int stage = GameDataManager.Instance.UserData.CurrentChessStage;
        int stageBgIndex = ChessStageController.Instance.GetLevelGameplayID(stage);
        // 说明：stageSpace / nextUnlockTip 是 WT 结算页独有的占位节点，HexWord 的 ChessFinishView.prefab 里没有这两个节点，
        // 同步过来后必须做空保护，否则整段 OnEnable 会空引用中断，结算页直接出不来。
        if (nextStageImage != null)
            nextStageImage.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("stageBtnBg" + stageBgIndex, "UI_MainBase");
        if (stageSpace != null)
            stageSpace.SetActive(stageBgIndex is not (0 or 4 or 5 or 6));
        levelText.text = MultilingualManager.Instance.GetString("Level") + " " + stage;

        // 普通关卡：用 levelImage 上的关卡文本；特殊关卡：改用 content 显示该关的图（stageBtnBg{index}）+ 关卡数
        // 特殊关卡 = 困难(1)/极难(2)/记忆(3)/emoji(7)；冰块(4)/花朵(5)/树叶(6) 仍走普通关卡那套（levelImage 图集 + levelImage 上的文本）
        bool isSpecialStage = stageBgIndex is not (0 or 4 or 5 or 6);
        if (levelText != null)
            levelText.gameObject.SetActive(!isSpecialStage);
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
                    specialText.text = levelText != null ? levelText.text : MultilingualManager.Instance.GetString("Level") + " " + stage;
            }
        }
        var unlockHint = ChessStageController.Instance.GetNextMechanicUnlockHint(stage);
        if (unlockHint.HasValue && nextUnlockTip != null)
        {
            nextUnlockTip.SetActive(true);
            // 从多语言表获取原始文案，例如 "还差{a}关解锁花朵"
            string rawText = MultilingualManager.Instance.GetString(unlockHint.Value.textKey,"pingzi");
            // 替换占位符并赋值给 UI Text
            Text unlockTipText = nextUnlockTip.GetComponentInChildren<Text>(true);
            if (unlockTipText != null)
                unlockTipText.text = rawText.Replace("{a}", unlockHint.Value.remainCount.ToString());
        }
        else if (nextUnlockTip != null)
        {
            // 没有要预告的机制，隐藏提示气泡
            nextUnlockTip.SetActive(false);
        }
        // 先设置原始分数 (注意防负数保护)
        int addZenCount = ChessStageController.Instance.CurrentTotalScore;
        int oldZenScore = Mathf.Max(0, GameDataManager.Instance.UserData.overallZenScore - addZenCount);
        zenScoreText.text = oldZenScore.ToString();
        
        bool isLimitUnlocked = LimitTimeManager.Instance.IsOpen();
        if (!LimitTimeManager.Instance.IsComplete() && isLimitUnlocked)
        {
            Content.gameObject.SetActive(true);
            // StartCoroutine(UpdateProgress());
        }
        else
        {
            Content.gameObject.SetActive(false);
        }

        if (ChessStageController.Instance.IsCurrentStageSkipped)
        {
            _encourageTitleText.text = "";
            _encouragePhraseText.text = "";
        }
        else
        {
            RenderEncouragementTexts();
        }
        lotusInstance?.SetActive(false);
        // 拦截奖励序列：如果是第一次进，播放动画；如果是从榜单返回，直接展示按钮
        if (!_hasPlayedRewardSequence)
        {
            _hasPlayedRewardSequence = true; // 标记为已播放
            nextStageBtn.gameObject.SetActive(false); // 将隐藏逻辑移到这里：只有首次进入才先隐藏，等动画播完再显示
            StartCoroutine(PlayRewardSequence());
        }
        else
        {
            // 已经播放过了（从排行榜返回），直接把下一步按钮显示出来，防止界面卡死
            nextStageBtn.gameObject.SetActive(true);
            nextStageBtn.gameObject.GetComponent<CanvasGroup>().alpha = 1;

            // 维持限时活动图标的状态
            if (_limitBtnTable._limitTimeEventButton.gameObject.activeSelf && LimitTimeManager.Instance != null &&
                GetCompletedWordCount() >= LimitTimeManager.Instance.CurlimitData.num)
            {
                Showlimiticon.SetActive(true);
            }
            
            if (GameCoreManager.Instance.IsTrueAuto)
            {
                StartCoroutine(AutoClickNextDelay());
            }
        }
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
        DailyTaskManager.Instance.UpdateMaxButterflyTime();
    }
    private IEnumerator AutoClickNextDelay()
    {
        yield return new WaitForSeconds(1.5f);
        OnNextButtonClick();
    }
    /// <summary>
    /// 核心：从 Controller 提取配置，根据概率渲染文案
    /// </summary>
    private void RenderEncouragementTexts()
    {
        // 1. 获取已经判定好的规则
        var rule = ChessStageController.Instance.CurrentMatchedRule;
        int bannerStyle = ChessStageController.Instance.CurrentBannerStyle;
        // 兜底：先全部隐藏
        if (_encourageTitleRoot != null) _encourageTitleRoot.SetActive(false);
        if (_encouragePhraseRoot != null) _encouragePhraseRoot.SetActive(false);
        
        if (rule == null) return;

        // 2. 扔骰子 (0-99)，根据你的配置表概率控制显示
        int rollTitle = UnityEngine.Random.Range(0, 100);
        int rollPhrase = UnityEngine.Random.Range(0, 100);

        // 3. 处理位置5（顶部标题）
        if (ChessStageController.Instance.CurrentStage-1 > 1 && rule.TitleRate > 0 && rollTitle < rule.TitleRate)
        {
            _encourageTitleRoot.SetActive(true);
            string titleKey = ChessStageController.Instance.GetCurrentMatchedTitleKey();
            _encourageTitleText.text = MultilingualManager.Instance.GetString(titleKey, "pingzi");
            // 进场小动画
            _encourageTitleRoot.transform.DOPunchScale(Vector3.one * 0.1f, 0.4f);
            
        }

        // 4. 处理位置6（结算页长文案及图标）
        if (rule.StimulateRate > 0 && rollPhrase < rule.StimulateRate)
        {
            _encouragePhraseRoot.SetActive(true);
            // 🌟 核心修复：Type 7 专属的双参数文本解析！
            if (rule.Type == 7)
            {
                // 获取配置文案 (兼容配在 PhraseKey 或 LongTextKey 里的情况)
                string targetText = MultilingualManager.Instance.GetString(rule.PhraseKey, "pingzi");
                if (!string.IsNullOrEmpty(rule.LongTextKey))
                {
                    targetText = MultilingualManager.Instance.GetString(rule.LongTextKey, "pingzi");
                }

                // 准备数据：{0}=本局谜题数，{1}=超越百分比
                // int puzzleCount = ChessStageController.Instance.CurrStageInfo.PhraseGroups.Count;
                int puzzleCount = ChessStageController.Instance.CurrStageInfo.StageNumber;
                string zenPercentStr = ChessStageController.Instance.DisplayZenPercent.ToString("F2");
                    
                try 
                {
                    // 注入双参数，彻底解决 FormatException 崩溃问题！
                    _encouragePhraseText.text = "\ud83c\udf1f" + string.Format(targetText, puzzleCount, zenPercentStr);
                }
                catch (Exception e)
                {
                    // 终极护盾：万一策划改配表了，捕获异常并直接显示原文，绝不卡死游戏！
                    _encouragePhraseText.text = "\ud83c\udf1f" + targetText;
                    Debug.LogError($"[文案格式化错误] Type 7 文本解析异常: {e.Message}");
                }
            }
            else
            {
                // 其他常规类型 (Type 0~6) 的原版逻辑保持不动
                // 组装文本（优先读长文案，没有长文案读短文案）
                string phraseText = MultilingualManager.Instance.GetString(rule.PhraseKey, "pingzi");
                if (!string.IsNullOrEmpty(rule.LongTextKey))
                {
                    string rawText = MultilingualManager.Instance.GetString(rule.LongTextKey, "pingzi");
                    float formattedX = ChessStageController.Instance.DisplayZenPercent;
                    _encouragePhraseText.text = "\ud83c\udf1f" + string.Format(rawText, formattedX.ToString("F2")) + phraseText;
                }
                else
                {
                    _encouragePhraseText.text = "\ud83c\udf1f" + phraseText;
                }
            }
                // 加载表情 Emoji
                // if (_encourageEmojiIcon != null && !string.IsNullOrEmpty(rule.EmojiKey))
                // {
                //     var emojiSprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("commonitem", rule.EmojiKey);
                //     if (emojiSprite != null) _encourageEmojiIcon.sprite = emojiSprite;
                // }
        }

        if (bannerStyle != 1 && bannerStyle != 2)
        {
            Debug.Log($"当前横幅类型为 {bannerStyle}，不满足结算页文案展示条件 (需为 1 或 2)，强制隐藏。");
            _encouragePhraseRoot.SetActive(false);
        }
    }

    private void UpdateTimeDisplay(string time)
    {
        if (!_tasktable.TaskBtn.interactable)
        {
            _tasktable.taskTime.gameObject.SetActive(true);
            _tasktable.taskTime.text = $"第{AppGameSettings.UnlockRequirements.DailyMissions}关";
            if (_tasktable.taskTime.transform.parent != null)
            {
                Image bg = _tasktable.taskTime.transform.parent.GetComponent<Image>();
                if (bg != null) bg.enabled = true;
            }
            return;
        }

        bool shouldActivate = !string.IsNullOrEmpty(time);
        _tasktable.taskTime.gameObject.SetActive(shouldActivate);
        
        if (shouldActivate) 
            _tasktable.taskTime.text = time;

        //  3. 核心修复：只控制底板的 Image 显隐
        if (_tasktable.taskTime.transform.parent != null)
        {
            Image bg = _tasktable.taskTime.transform.parent.GetComponent<Image>();
            if (bg != null) 
            {
                // 因为我没看到你的 TaskTable 结构，如果有 taskOver 节点，这里可以加上 || _tasktable.taskOver.activeSelf
                bg.enabled = shouldActivate; 
            }
        }
    }

    private void UpdateButterflyTime(string time = "")
    {
        bool shouldActivate = GameDataManager.Instance.UserData.butterflyTaskIsOpen;
        if (_butterflyTimerDisplay.activeSelf != shouldActivate)
        {
            _butterflyTimerDisplay.gameObject.SetActive(shouldActivate);
        }

        if (shouldActivate)
            _butterflyTimerDisplay.GetComponentInChildren<Text>().text = time;
    }

    /// <summary>
    /// 🌟 新增：刷新结算界面的蝶蛹进度展示
    /// </summary>
    private void UpdateButterflyProgressUI(int targetCount = -1, bool isInit = false)
    {
        // 1. 判断是否开启蝶园
        bool isOpen = ButterfliesManager.Instance.IsOpen;
        if (!isOpen) return;

        bool isAllCollected = ButterfliesManager.Instance.IsAllButterfliesCollected();
        Slider slider = butterflyBtn.GetComponentInChildren<Slider>(true);
        Text pupaText = butterflyBtn.GetComponentInChildren<Text>(true);
        if (isAllCollected)
        {
            // 全收集满：关掉进度条和进度文字
            if (slider != null) slider.gameObject.SetActive(false);
            if (pupaText != null) pupaText.gameObject.SetActive(false);
            return; // 结束执行
        }

        if (slider != null) slider.gameObject.SetActive(true);
        if (pupaText != null) pupaText.gameObject.SetActive(true);

        // 2. 获取目标配置
        ButterflyGrow butterflyGrow = ButterfliesManager.Instance.GetCurrentGrow();
        int displayCount = targetCount >= 0 ? targetCount : Mathf.Max(0, _currentVisualPupa);

        // 3. 计算进度比例 (0.0f ~ 1.0f)
        float progressValue = 0f;
        if (butterflyGrow != null && butterflyGrow.Count > 0)
        {
            progressValue = Mathf.Clamp01((float)displayCount / butterflyGrow.Count);
        }

        // 4. 设置进度条 (支持自定义 ProgressBar 或原生 Image Filled)
        // 如果你用的是原生的 Image Filled 模式：
        if (slider != null)
        {
            slider.DOKill(); // 杀掉这根进度条上的旧动画，防止鬼畜抖动
            if (isInit) slider.value = progressValue;
            // DOValue 是 DOTween 专门给 Slider 写的魔法方法！花 0.5 秒平滑涨过去！
            else slider.DOValue(progressValue, 0.5f).SetEase(Ease.OutQuad);
        }

        // 5. 设置文字 (例如 "10 / 60")
        if (pupaText != null)
        {
            string newText = $"{targetCount} / {butterflyGrow?.Count.ToString() ?? "&"}";
            if (!isInit && pupaText.text != newText)
            {
                pupaText.text = newText;
            
                // 杀掉旧动画，防止快速连续飞入时动画冲突
                pupaText.transform.DOKill();
                pupaText.transform.localScale = Vector3.one; 
                // 文字瞬间放大 1.3 倍并弹回
                pupaText.transform.DOPunchScale(Vector3.one * 0.03f, 0.3f, 10, 1);
            }
            else
            {
                pupaText.text = newText;
            }
        }
        bool canSynthesize = butterflyGrow != null && displayCount >= butterflyGrow.Count;
        ButterflyRedpoint.gameObject.SetActive(ButterfliesManager.Instance.showButterflyRedPoint || canSynthesize);
    }
    
    private void UpdateSliderProgress()
    {
        if (LimitTimeManager.Instance == null) return;
        if (DOTween.IsTweening(progressSlider)) return;
        
        int wordCount = GetCompletedWordCount();
        LimitDataItem limitData = LimitTimeManager.Instance.CurlimitData;
        if (limitData == null) return;
        float targetProgress = Mathf.Clamp01((float)wordCount / limitData.num);
        // 如果进度没变化（比如本局没获得词），直接瞬间赋值并结束
        if (targetProgress < progressSlider.value || Mathf.Abs(progressSlider.value - targetProgress) < 0.01f)
        {
            progressSlider.DOKill();
            progressSlider.value = targetProgress;
            UpdateLimitEventText();
            return;
        }
        
        progressSlider.DOKill(); // 清理旧动画
        Sequence seq = DOTween.Sequence();
        seq.AppendInterval(0.5f);
        seq.Append(progressSlider.DOValue(targetProgress, 0.8f).SetEase(Ease.OutQuad));
        seq.OnComplete(UpdateLimitEventText);
    }
    
    /// <summary>
    /// 获取当前限时活动已完成词语数
    /// </summary>
    private int GetCompletedWordCount()
    {
        if (LimitTimeManager.Instance == null) return 0;
        return LimitTimeManager.Instance.GetCurWordCount();
    }


    /// <summary>
    /// 播放奖励获取序列动画
    /// </summary>
    private IEnumerator PlayRewardSequence()
    {
        _tasktable.taskEffect.gameObject.SetActive(false);
        _matchFishtable.matchEffect.gameObject.SetActive(false);
        
        isShowWinSign = StreakManager.Instance.IsCanShowWinSign();
        bool isJump = ChessStageController.Instance.IsJump;

        if (isShowWinSign&&!isJump)
        {
            SystemManager.Instance.ShowPanel(PanelType.SignWinScreen);
        }
        else
        {
            CheckReturnFirstWinScreen();
        }

        // ★ 结算页更优先的弹窗（插屏 / 七日胜利 / 灯明心悟 / 锦鲤竞速…）会弹时，破冰/复购礼包不弹（按钮保留）
        CancelSettlementGiftIfBlocked();
        
        if (isShowWinSign||isTriggerFirstWin)
        {
            nextStageBtn.gameObject.GetComponent<CanvasGroup>().alpha = 0;
            nextStageBtn.gameObject.SetActive(true);
            nextStageBtn.gameObject.GetComponent<CanvasGroup>().DOFade(1, 0.3f);
            
            yield return new WaitUntil(() => !SystemManager.Instance.PanelIsShowing(PanelType.SignWinScreen)
                                             &&!SystemManager.Instance.PanelIsShowing(PanelType.SevenSignScreen)
                                             &&!SystemManager.Instance.PanelIsShowing(PanelType.ReturnFirstWinScreen));
        }
        
        // ==========================================
        // 队列首位：全权交由专属协程处理禅修榜的三种状态
        // ==========================================
        yield return StartCoroutine(HandleZenRankFlowRoutine());
        
        int cachedLimitCount = ChessStageController.Instance.LimitPuzzleCount;
        int leafCollected = ChessStageController.Instance.CurrStageData.CollectedLeaves;
        if (leafCollected > 0)
        {
            EventDispatcher.instance.TriggerUpdateLayerCoin(true, false, false);
        }
        // =========================================
        //  队列第二位：限时活动逻辑及面板
        // =========================================
        if (_limitBtnTable._limitTimeEventButton.gameObject.activeSelf && _limitBtnTable._limitTimeEventButton.interactable)
        {
            _limitBtnTable.CheckAndShowLimitedTimeEvent(Enlimiticon.transform);
            _isWordsExtracted = true; // 锁住预判文案
            yield return new WaitForSeconds(FinishFlowTiming.LimitPanelGap);

            // 根据进度决定是否自动弹出限时活动面板
            if (LimitTimeManager.Instance != null &&
                GetCompletedWordCount() >= LimitTimeManager.Instance.CurlimitData.num)
            {
                Showlimiticon.SetActive(true);
                int pupaBeforeLimit = GameDataManager.Instance.ButterflyData.currPupa;
                
                SystemManager.Instance.ShowPanel(PanelType.LimitTimeScreen);
                yield return new WaitUntil(() => !SystemManager.Instance.PanelIsShowing(PanelType.LimitTimeScreen));
                
                int pupaEarnedFromLimit = GameDataManager.Instance.ButterflyData.currPupa - pupaBeforeLimit;
                if (pupaEarnedFromLimit > 0 && butterflyBtn.gameObject.activeSelf)
                {
                    // 从限时活动按钮(世界坐标)起飞向蝴蝶按钮
                    yield return StartCoroutine(PlayPupaFlyAnim(pupaEarnedFromLimit, _limitBtnTable._limitTimeEventButton.transform.position, true));
                }
            }
            else
            {
                Showlimiticon.SetActive(false);
            }
        }
        AdRuleManager.Instance.TryShowInterstitial((issuccess) =>
        {
            if (issuccess)
            {
                AnalyticMgr.InsetAdSuccess("关卡插屏");
                GameDataManager.Instance.UserData.totalInsetSeeAds++;
            }
            else
            {
                AnalyticMgr.InsetAdFail("关卡插屏");
            }
            Debug.Log($"[AdRule] 调用广告插屏 {issuccess}");
        });
        // 将按钮显示逻辑提出来，并加一个安全判定。确保流程走到这里时，必定会展示按钮
        if (!nextStageBtn.gameObject.activeSelf)
        {
            nextStageBtn.gameObject.SetActive(true);
            
            CanvasGroup btnCanvasGroup = nextStageBtn.gameObject.GetComponent<CanvasGroup>();
            if (btnCanvasGroup != null)
            {
                btnCanvasGroup.alpha = 0;
                btnCanvasGroup.DOFade(1, 0.3f);
            }
        }
        // ==========================================
        // 🌟【新时序接入】：检查并弹出【新手奖励】
        // ==========================================
        int clearedStage = GameDataManager.Instance.UserData.CurrentChessStage - 1;
        if (BeginnerBonusManager.Instance.CheckStageCleared(clearedStage, isJump))
        {
            // 等待新手奖励弹窗关闭（玩家点关闭回结算页，或点“立即参与”直接进下一关）
            yield return new WaitUntil(() => !SystemManager.Instance.PanelIsShowing(PanelType.BeginnerBonusPanel));
            
            // 刷新入口显隐状态
            if (_beginnerBonusEntryBtn != null)
            {
                _beginnerBonusEntryBtn.RefreshVisibility();
            }

            // 如果玩家在新手奖励里点了“立即参与”，结算页已被关闭，安全跳出协程
            if (!SystemManager.Instance.PanelIsShowing(PanelType.ChessFinishView))
            {
                yield break;
            }
        }
        // ==========================================
        // 平滑涨满本关新增的外边框刻度
        // ==========================================
        if (_beginnerBonusEntryBtn != null && _beginnerBonusEntryBtn.gameObject.activeSelf)
        {
            _beginnerBonusEntryBtn.UpdateBorderProgress(true); // 传入 true，播放 0.5s 平滑填满动画
        }
        if(!isJump)
            AchievementManager.Instance.InitFinishAchieveTable(transform);
        
        // ==========================================
        // “新版禅意总分”数字滚动
        // ==========================================
        if (ChessStageController.Instance.CurrentTotalScore > 0)
        {
            int addZenCount = ChessStageController.Instance.CurrentTotalScore;
            
            // 读取新版独立总分
            int targetZenScore = GameDataManager.Instance.UserData.overallZenScore;
            int oldZenScore = Mathf.Max(0, targetZenScore - addZenCount);
            
            bool isRolling = true;

            // ★ 禅意分开始变化时起播，一直循环到分数加完（2609 A-3(5)）
            _zenRollSfx = AudioManager.Instance.PlaySoundEffectLoop("event_rank_change");

            DOTween.To(() => oldZenScore, x => 
                {
                    zenScoreText.text = x.ToString();
                }, targetZenScore, 1.0f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => 
                {
                    isRolling = false;
                    // 分数加完，立刻收掉滚动音效，不要拖到下一段
                    AudioManager.Instance.StopLoopSoundEffect(_zenRollSfx);
                    _zenRollSfx = null;
                    zenScoreText.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f);
                });
                
            yield return new WaitUntil(() => !isRolling);
            // 兜底：面板中途关闭等异常路径下也要把音效收掉
            AudioManager.Instance.StopLoopSoundEffect(_zenRollSfx);
            _zenRollSfx = null;
            yield return new WaitForSeconds(FinishFlowTiming.AfterZenRoll);
        }
      
        // // ==========================================
        // // 播放莲花飞向禅意榜的动画！
        // // ==========================================
        bool isRankActive = GameDataManager.Instance.UserData.isJoinedZenRank && 
                            ZenRankManager.Instance.RemainingSeconds > 0;
        if (isRankActive && _zenRankBtn != null && _zenRankBtn.gameObject.activeSelf && ChessStageController.Instance.CurrentTotalScore > 0)
        { 
            yield return StartCoroutine(PlayZenLotusFlyAnim());
        }

        if (butterflyBtn.gameObject.activeSelf && ChessStageController.Instance.EarnedPupaThisLevel > 0)
        {
            yield return StartCoroutine(PlayPupaFlyAnim(ChessStageController.Instance.EarnedPupaThisLevel,Vector3.zero, false));
        }

        //  步骤三：树叶额外金币起飞（从中央飞向 Header 金币槽）
        // ==========================================
        var leafReward = ChessStageController.Instance.GetAllLeafRewards(leafCollected);
        if (leafReward != null && leafReward.Count > 0)
        {
            CustomFlyInManager.Instance.FlyInGold(Content.transform, () =>
            {
                GameDataManager.Instance.UserData.UpdateGold(leafReward[0].Value, true, true, "树叶收集结算获得");
                // EventDispatcher.instance.TriggerChangeGoldUI(leafReward[0].Value, true);
            });
            yield return new WaitForSeconds(FinishFlowTiming.CoinFlyGap);
        }

        ChessStageController.Instance.LimitPuzzleCount = cachedLimitCount;
        if (!GameDataManager.Instance.UserData.isAllCompleteTask && _tasktable.TaskBtn.gameObject.activeSelf)
        {
            _tasktable.CheckTasksScreen();
            yield return new WaitForSeconds(FinishFlowTiming.TaskPanelGap);
        }

        ChessStageController.Instance.LimitPuzzleCount = 0;
        if (FishInfoController.Instance.IsOpen() &&
            FishInfoController.Instance.IsShowFishProgressAnim() && 
            _matchFishtable.FishBtn.gameObject.activeSelf)
        {
            _matchFishtable.ShowFishWordAnim();
            StartCoroutine(UpdateFishRankUI());
            //yield return new WaitForSeconds(1.2f);
        }

        // 等待限时活动进度更新
        //yield return new WaitForSeconds(0.5f);
        
#if Unity_ShowLog || UNITY_EDITOR
        if (GameCoreManager.Instance.IsTrueAuto)
        {
            yield return new WaitForSeconds(1.2f);
            OnNextButtonClick();
        }
#endif
    }
    
    private IEnumerator UpdateFishRankUI()
    {
        FishInfoController.Instance.RoundResultFishRank();
        _matchFishtable.UpdateFishRank();

        WaitForSeconds wait = new WaitForSeconds(1f);
        while (_matchFishtable.FishBtn.gameObject.activeSelf)
        {
            yield return wait;
            FishInfoController.Instance.RoundResultFishRank();
            _matchFishtable.UpdateFishRank();
        }
    }

    private IEnumerator CheckZenRankBtn()
    {
        if (!ZenRankManager.Instance.IsOpen())
            yield break;
        
        ZenRankButton rankComp = _zenRankBtn.GetComponent<ZenRankButton>();
        WaitForSeconds waitTime = new WaitForSeconds(0.5f);
        while (_zenRankBtn.gameObject.activeSelf)
        {
            yield return waitTime;
            rankComp.CheckRankProgress();
        }
    }

    private void OnNextButtonClick()
    {
        _hasPlayedRewardSequence = false; // 离开结算页去下一关时重置标记

        // 离开结算页：结算礼包登记作废（下次结算重新决定）
        ShopManager.shopManager?.ClearSettlementGift("离开结算页");

        SystemManager.Instance.HidePanel(PanelType.HeaderSection, false, LoadNextStage);
        Close();
    }

    private void LoadNextStage()
    {
        ChessStageController.Instance.SetStageData(ChessStageController.Instance.CurrentStage);
        SystemManager.Instance.ShowPanel(PanelType.ChessPlayArea);
    }


    private void UnlockBtnsUI()
    {
        int curStage = GameDataManager.Instance.UserData.CurrentChessStage;

        // 1. 蝶园入口
        RefreshButterflyButtonState();
        // 2. 禅修榜入口（委托给组件自身的 CheckRankProgress 处理三态）
        StartCoroutine(CheckZenRankBtn());
        // 3. 每日任务按钮
        SetupActivityButton(
            _tasktable.TaskBtn, 
            _tasktable.LockObj, // 若预制体上有锁图标节点可在此传入
            AppGameSettings.ShowRequirements.DailyMissionsShow, // 若有专属展示等级换成 ShowRequirements
            DailyTaskManager.Instance.IsOpen()
        );

        // 4. 限时活动按钮
        SetupActivityButton(
            _limitBtnTable._limitTimeEventButton, 
            _limitBtnTable.LockObj,
            AppGameSettings.ShowRequirements.TimeLimitShow,
            LimitTimeManager.Instance.IsOpen()
        );
        _limitBtnTable.InitLimtBtnUI();
        
        bool isLimitUnlocked = _limitBtnTable._limitTimeEventButton.gameObject.activeSelf && 
                               _limitBtnTable._limitTimeEventButton.interactable;
        
        if (isLimitUnlocked && !LimitTimeManager.Instance.IsComplete())
        {
            Content.gameObject.SetActive(true);
            DailyTaskManager.Instance.UpateButterflyTaskUI();
        }
        else
        {
            Content.gameObject.SetActive(false);
        }

        // 5. 捕鱼对战入口
        if (_matchFishtable != null && _matchFishtable.FishBtn != null)
        {
            bool isLegacyOpened = FishInfoController.Instance.IsOpen();
            bool openTime = FishInfoController.Instance.GetOpenFishFunction();
            
            bool isShow = (curStage > AppGameSettings.ShowRequirements.FishShowLevel || isLegacyOpened) && openTime;
            _matchFishtable.FishBtn.transform.parent.gameObject.SetActive(isShow);
            if (isShow)
            {
                bool isLocked = !isLegacyOpened;
                _matchFishtable.FishBtn.interactable = !isLocked;
                if (_matchFishtable.LockObj != null) _matchFishtable.LockObj.SetActive(isLocked);
                
                Image fishIcon = _matchFishtable.FishBtn.transform.GetChild(0).GetComponent<Image>();
                if (fishIcon != null)
                {
                    Color c = fishIcon.color;
                    c.a = isLocked ? 0.5f : 1f;
                    fishIcon.color = c;
                }
                
                if (!isLocked)
                {
                    _matchFishtable.CheckFishBtn();
                    StartCoroutine(UpdateFishRankUI());
                }
                else
                {
                    // 寻找鱼活动的锁底下的Text赋值（预制体没挂锁节点时跳过）
                    if (_matchFishtable.LockObj != null)
                    {
                        Text lockText = _matchFishtable.LockObj.GetComponentInChildren<Text>(true);
                        if (lockText != null) lockText.text = $"第{AppGameSettings.UnlockRequirements.FishOpenLevel}关";
                    }
                }
            }
        }

        // 6. 新手奖励入口
        if (_beginnerBonusEntryBtn != null)
        {
            _beginnerBonusEntryBtn.RefreshVisibility();
        }
    }

    private void UnlockButton(Button button, int unlockLevel, string panelName, string openTime)
    {
        int currentStage = Mathf.Max(GameDataManager.Instance.UserData.CurrentChessStage,
            GameDataManager.Instance.UserData.CurrentHexStage);
        bool isUnlocked = currentStage >= unlockLevel || !string.IsNullOrEmpty(openTime);

        button.gameObject.SetActive(isUnlocked);

        if (!isUnlocked) return;
        if (currentStage != unlockLevel) return;

        AudioManager.Instance.PlaySoundEffect("BtnUnlock");
        //SystemManager.Instance.ShowPanel(panelName);
    }

    /// <summary>
    /// 限时活动按钮点击处理
    /// </summary>
    private void OnLimitTimeEventButtonClicked()
    {
        SystemManager.Instance.ShowPanel(PanelType.LimitTimeScreen);
        
        AchievementManager.Instance.DisableFinishAchieveTable();
    }

    /// <summary>
    /// 播放莲花飞向禅意排行榜的动画
    /// </summary>
    private IEnumerator PlayZenLotusFlyAnim()
    {
        // 1. 获取本局获得的禅意数量（替换为你实际增加的变量，比如这里测试用 +15）
        int addZenCount =
            ChessStageController.Instance.CurrentTotalScore; // 比如：GameDataManager.Instance.UserData.AddZenCount

        // 如果增加了禅意，且排行榜按钮开启了，才播动画
        if (addZenCount > 0 && _zenRankBtn != null && _zenRankBtn.gameObject.activeSelf)
        {
            Vector3 startPos = _centerLotusImage != null
                ? _centerLotusImage.transform.position
                : zenScoreText.transform.position + new Vector3(0, 80f, 0);

            // ==========================================
            // 🌟 阶段一：原地的暗荷花，直接替换图片变成亮荷花！
            // ==========================================
            // 1. 直接替换图片
            // Image ligh = _centerLotusImage.transform.GetChild(0).GetComponent<Image>();
            _centerLotusImage.DOFade(1, FinishFlowTiming.LotusLightFade).OnComplete(() =>
            {
                // 2. 顺便给它一个“砰”地亮起的弹跳小动效 (放大到 1.15 倍再缩回来)
                _centerLotusImage.transform.DOScale(1.02f, 0.2f).SetLoops(1, LoopType.Yoyo);
            });
            // 3. 等荷花点亮后稍作停顿，让玩家看清，再起飞
            yield return new WaitForSeconds(FinishFlowTiming.LotusLightFade + FinishFlowTiming.LotusLightHold);

            // 初始化莲花状态（放在屏幕中央或者特定初始位置）
            Canvas canvas = lotusInstance.GetComponent<Canvas>();
            if (canvas == null) canvas = lotusInstance.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingLayerName = "PopPanel"; // 保持和你前面配置的一样
            canvas.sortingOrder = 100;
            GraphicRaycaster raycaster = lotusInstance.GetComponent<GraphicRaycaster>();
            if (raycaster == null) raycaster = lotusInstance.AddComponent<GraphicRaycaster>();
            raycaster.enabled = false; // 飞行期间不要阻挡点击
            CanvasGroup cg = lotusInstance.GetComponent<CanvasGroup>();
            if (cg == null) cg = lotusInstance.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.alpha = 1f; // 如果你想稍微有点透明度，可以改成 0.9f
            lotusInstance.SetActive(true);
            Text textComponent = lotusInstance.GetComponentInChildren<Text>();
            if (textComponent != null) textComponent.text = "+" + addZenCount;

            lotusInstance.transform.position = _centerLotusImage.transform.position;
            lotusInstance.transform.localScale = Vector3.zero; // 先缩小，准备弹出
            // ==========================================
            // 第二步：计算绕开中心障碍物的弧线路径
            // ==========================================
            // Vector3 startPos = lotusInstance.transform.position;
            Vector3 endPos = _zenRankBtn.transform.position;
            // 取起点和终点的中心点
            Vector3 midPos = (startPos + endPos) / 2f;
            float distance = Vector3.Distance(startPos, endPos);

            // 🌟 1. 获取当前 Canvas 的真实物理边界
            RectTransform canvasRect = GetComponentInParent<Canvas>().GetComponent<RectTransform>();
            Vector3[] corners = new Vector3[4];
            canvasRect.GetWorldCorners(corners);

            // corners[0]是左下角，corners[2]是右上角
            float canvasRightX = corners[2].x;
            float canvasWidth = corners[2].x - corners[0].x;

            // 🌟 2. 划定护城河：屏幕最右侧往回退 10% 的宽度，作为绝对安全线
            float safeRightX = canvasRightX - (canvasWidth * 0.1f);

            // 🌟 3. 计算理想偏移量（把 0.4 稍微降到 0.35，对长屏幕来说弧度更自然）
            float idealTargetX = midPos.x + (distance * 0.35f);

            // 🌟 4. 核心数学拦截公式：控制点X <= 2 * 安全边界 - 中心点X
            float maxAllowedX = 2f * safeRightX - midPos.x;

            // 🌟 5. 最终取值：谁小听谁的（确保绝不越过安全线）
            midPos.x = Mathf.Min(idealTargetX, maxAllowedX) - 1.2f;

            // 构成 3 个点的路径数组
            Vector3[] pathPoints = new Vector3[] { startPos, midPos, endPos };

            // 2. 使用 DOTween 创建连续动画序列
            Sequence seq = DOTween.Sequence();
            seq.SetLink(lotusInstance);
            seq.AppendCallback(() => AudioManager.Instance.PlaySoundEffect("LotusPop", 0, 1));
            // 动作1：在起点瞬间弹出来，稍微悬浮一下给玩家看清楚
            seq.Append(lotusInstance.transform.DOScale(1.2f, 0.3f).SetEase(Ease.OutBack));
            // 动作2：停顿展示一下数字，让玩家看清楚加了多少 (耗时 0.3秒)
            seq.AppendInterval(0.3f);

            // 动作3：朝排行榜按钮飞过去！
            // PathType.CatmullRom 会让物体平滑地穿过我们设定的那个侧边中点
            seq.Append(lotusInstance.transform.DOPath(pathPoints, 1.0f, PathType.CatmullRom).SetEase(Ease.InCubic));
            // 边飞边缩小，表现出“飞入框内”的纵深感
            // seq.Join(lotusInstance.transform.DOScale(0.4f, 1.8f).SetEase(Ease.InQuad));
            bool isAnimFinished = false;
            // 动作4：飞到目标后的回调
            seq.OnComplete(() =>
            {
                // 1. 飞到了，把飞行道具隐藏
                lotusInstance.SetActive(false);
                // 2. 通知目标按钮：“砸到你了，请播放你的震动特效和刷新逻辑！”
                _zenRankBtn.PlayAbsorbEffect(addZenCount);
                isAnimFinished = true;

                // 🌟 (可选) 在这里调用刷新排行榜按钮文字/总数的逻辑
                if (_zenRankBtn != null)
                    _zenRankBtn.GetComponent<ZenRankButton>().FetchMyCurrentRank();
            });

            yield return new WaitUntil(() => isAnimFinished);
            // 落位后稍作缓冲，再让结算界面去播下一个（比如灯笼）的动画
            yield return new WaitForSeconds(FinishFlowTiming.AfterLotusLand);
        }
    }

    /// <summary>
    /// 播放蝶蛹飞向蝴蝶按钮，并增加进度的动画
    /// </summary>
    private IEnumerator PlayPupaFlyAnim(int earnedPupa, Vector3 startPos, bool useWorldPos)
    {
        if (ButterfliesManager.Instance.IsAllButterfliesCollected()) yield break;
        // 如果本局获得了蝶蛹，才播放动画
        if (earnedPupa > 0 && butterflyBtn != null)
        {
            int completedCount = 0;
            bool allDone = false;
            // 循环生成蝶蛹 (获得几个就飞几次)
            for (int i = 0; i < earnedPupa; i++)
            {
                GameObject pupaInstance = _pupaPool.GetObject();

                Canvas canvas = pupaInstance.GetComponent<Canvas>();
                if (canvas == null) canvas = pupaInstance.AddComponent<Canvas>();
                
                canvas.overrideSorting = true;
                canvas.sortingLayerName = "PopPanel";
                canvas.sortingOrder = 10;
                pupaInstance.SetActive(true);
                if (useWorldPos)
                    pupaInstance.transform.position = startPos; // 比如从限时按钮飞出
                else
                    pupaInstance.transform.localPosition = startPos; // 比如从屏幕中间飞出
                
                pupaInstance.transform.localScale = Vector3.one;

                Sequence seq = DOTween.Sequence();
                seq.SetLink(pupaInstance);

                // 动作1：向上弹起
                seq.Append(pupaInstance.transform.DOLocalMoveY(150f, 0.4f).SetRelative(true).SetEase(Ease.OutQuad));

                // 动作2：飞向蝴蝶按钮，同时缩小
                seq.Append(pupaInstance.transform.DOMove(butterflyBtn.transform.position, 0.6f).SetEase(Ease.InBack));
                seq.Join(pupaInstance.transform.DOScale(0.5f, 0.6f));
                
                // 动作3：飞到了！
                seq.OnComplete(() =>
                {
                    AudioManager.Instance.PlaySoundEffect("getPupa"); // 播放获得音效
                    completedCount++;
                    _currentVisualPupa++;
                    UpdateButterflyProgressUI(_currentVisualPupa); // 触发 UI 更新动画

                    // 可选：让按钮有个被击中的弹跳反馈
                    butterflyBtn.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 10, 1);
                    if (completedCount >= earnedPupa)
                        allDone = true;

                    _pupaPool.ReturnObjectToPool(pupaInstance.GetComponent<PoolObject>());
                });
                // 每个蝶蛹启动后，间隔一小段再启动下一个（最后一个不用等）
                if (i < earnedPupa - 1)
                    yield return new WaitForSeconds(FinishFlowTiming.PupaStagger);
            }
            // 等待所有蝶蛹动画彻底结束
            yield return new WaitUntil(() => allDone);
            // 所有蝶蛹都飞完了，稍微停顿一下再继续后面的流程
            // yield return new WaitForSeconds(0.8f);
        }
    }

    private void OnButterflyClick()
    {
        if (GameCoreManager.Instance.PanelState == PanelState.MainMenuPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.PrimaryInterface);
        }
        else if (GameCoreManager.Instance.PanelState == PanelState.FinishXiaoPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.StageFinishView);
        }
        else if (GameCoreManager.Instance.PanelState == PanelState.FinishPingPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.ChessFinishView);
        }

        SystemManager.Instance.HidePanel(PanelType.HeaderSection, true,
            () => { SystemManager.Instance.ShowPanel(PanelType.ButterflyHome); });
    }

    /// <summary>
    /// 统筹处理禅修榜结算、加入雷达、名次变动的三重状态
    /// </summary>
    private IEnumerator HandleZenRankFlowRoutine()
    {
        // 0. 前置检查：功能是否已经解锁？没解锁直接跳过
        bool isZenUnlocked = _zenRankBtn != null && _zenRankBtn.gameObject.activeSelf;
        if (!isZenUnlocked) yield break;

        // ★ 礼包优先：破冰/复购弹窗先弹、弹完（或被关掉）之后，再走排行榜的开始界面 / 名次变化弹窗
        yield return StartCoroutine(WaitSettlementGiftPopupClosed());
  
        var userData = GameDataManager.Instance.UserData;
        if (userData.isJoinedZenRank && userData.HasUnclaimedUpdateJoin)
        {
            userData.HasUnclaimedUpdateJoin = false;
        }
        // 正常首次解锁的条件（已经包含 !isJoinedZenRank）
        bool isFirstUnlockZen = !userData.isJoinedZenRank &&
                                userData.CurrentChessStage == ZenRankManager.Instance.GetUnlockStageCondition();
        // 升级更新触发的条件，必须要求玩家当前【不在榜单内】
        bool isUpdateUnlockZen = !userData.isJoinedZenRank && userData.HasUnclaimedUpdateJoin;
        
        if (isFirstUnlockZen || isUpdateUnlockZen)
        {
            // ---------- 检查昵称（与 ZenRankButton 点击逻辑一致）----------
            // 判断是否允许弹出起名窗
            if (userData.CanShowCharInfoPopup())
            {
                // 标记已弹出，避免后续重复弹窗
                userData.MarkCharInfoPopupShown();
                // 弹出起名面板，阻塞等待关闭
                SystemManager.Instance.ShowPanel(PanelType.RewardNamePanel);
                yield return new WaitUntil(() => !SystemManager.Instance.PanelIsShowing(PanelType.RewardNamePanel));
            }
            // ---------- 昵称检查结束 ----------
            
            UIWindow radarWindow = SystemManager.Instance.ShowPanel(PanelType.ZenRankStartScreen);
            if (radarWindow != null)
            {
                ZenRankStartScreen startScreen = radarWindow.GetComponent<ZenRankStartScreen>();
                startScreen.SetSourcePanel(PanelType.ChessFinishView);
                startScreen.SetForcedMode(true); // 🌟 开启强制模式，隐藏关闭按钮！
            }

            GameDataManager.Instance.UserData.HasUnclaimedUpdateJoin = false;
            // 既然强制弹出了匹配页，玩家必须点击匹配，匹配完会自动触发进下一关
            // 此时结算页会被强行关闭。所以在这里使用死循环挂起，不让后续代码继续执行！
            yield return new WaitUntil(() => !SystemManager.Instance.PanelIsShowing(PanelType.ZenRankStartScreen));
            yield break; // 匹配结束界面关闭后，直接退出协程
        }
        // ==========================================
        // 🌟【关键修复】：静默检查赛季状态 (绝对不在这里弹窗)
        // ==========================================
        bool hasSettlement = false;
        bool isCheckFinished = false;
        // 直接调用底层 API 请求，不走带 UI 的 Routine
        yield return APIGateway.Instance.LeaderboardApi.CheckZenSettlement((res) =>
        {
            if (res != null)
            {
                hasSettlement = res.has_settlement;
            }
            isCheckFinished = true;
        });

        yield return new WaitUntil(() => isCheckFinished);
        // 如果服务器标记已有结算，或者本地倒计时已经归零，说明处于“赛季交替期”
        if (hasSettlement || ZenRankManager.Instance.RemainingSeconds <= 0)
        {
            Debug.Log("[ZenFlow] 检测到赛季已结束或有待领取的奖励。静默拦截：胜利页不弹结算，跳过排名变化动画。");
            // 直接退出流程！玩家点继续后会回到大厅，由大厅统一管理赛季结算弹窗的弹出。
            yield break; 
        }
        // ==========================================
        int addZenCount = ChessStageController.Instance.CurrentTotalScore;
        // 纯净判断：加入了、赛季没结束、得分>0 -> 弹名次变化面板！
        if (userData.isJoinedZenRank && ZenRankManager.Instance.RemainingSeconds > 0 && addZenCount > 0)
        {
            yield return StartCoroutine(CheckAndShowZenRankChange(addZenCount));
        }
    }

    /// <summary>
    /// 结算页会弹更优先的弹窗时，取消本次破冰/复购礼包弹窗（结算页的礼包按钮保留，玩家仍可手动点开）。
    ///
    /// 需求（破冰/复购「弹窗条件 3」）：当前结算页会弹以下任意一个 → 礼包不弹：
    ///   1. 插屏（AdRuleManager.CanShowInterstitial 预判，本页稍后才真正播）
    ///   2. 七日胜利弹窗（SignWinScreen）/ 七日签到（SevenSignScreen）
    ///   3. 灯明心悟页面（LimitTimeScreen，达成进度会自动弹）
    ///   4. 新手奖励活动进度弹窗 → 项目里暂无该活动
    ///   5. 锦鲤竞速活动开始弹窗 → 只有玩家主动点「锦鲤竞速」按钮才会弹，不自动占用结算页，故不拦
    /// </summary>
    private void CancelSettlementGiftIfBlocked()
    {
        if (ShopManager.shopManager == null) return;
        if (!ShopManager.shopManager.IsSettlementGiftPopupPending) return;

        string reason = null;

        // 2. 七日胜利 / 七日签到 / 回归首胜
        if (isShowWinSign || isTriggerFirstWin)
        {
            reason = isShowWinSign ? "七日胜利弹窗" : "回归首胜弹窗";
        }
        else if (SystemManager.Instance != null
                 && (SystemManager.Instance.PanelIsShowing(PanelType.SignWinScreen)
                     || SystemManager.Instance.PanelIsShowing(PanelType.SignAwardScreen)
                     || SystemManager.Instance.PanelIsShowing(PanelType.SevenSignScreen)))
        {
            reason = "七日胜利/签到弹窗";
        }

        // 1. 插屏（本页稍后 Rank 流程之后会尝试播，这里提前预判，避免礼包和插屏叠在一起）
        if (reason == null && AdRuleManager.Instance != null && AdRuleManager.Instance.CanShowInterstitial())
        {
            reason = "插屏";
        }

        // 3. 灯明心悟（限时活动页达成条件会自动弹）
        if (reason == null && WillAutoShowLimitTimePanel())
        {
            reason = "灯明心悟页面";
        }

        if (!string.IsNullOrEmpty(reason))
            ShopManager.shopManager.CancelPreparedSettlementGiftPopup(reason);
    }

    /// <summary>本页是否会自动弹「灯明心悟」限时活动页（与 PlayRewardSequence 里的判定同口径）。</summary>
    private bool WillAutoShowLimitTimePanel()
    {
        if (_limitBtnTable == null || _limitBtnTable._limitTimeEventButton == null) return false;
        if (!_limitBtnTable._limitTimeEventButton.gameObject.activeSelf) return false;

        LimitTimeManager limit = LimitTimeManager.Instance;
        if (limit == null || limit.CurlimitData == null) return false;

        return GetCompletedWordCount() >= limit.CurlimitData.num;
    }

    /// <summary>
    /// 等结算礼包弹窗（破冰 / 复购）弹完并被关闭。
    /// 礼包策略在结算页出现之前就已「预登记」，真正弹窗要等 1.2s，所以这里要同时等两个状态：
    ///   1. 还有礼包等着弹（ShopManager.IsSettlementGiftPopupPending）；
    ///   2. AdsDiscountScreen 正在展示。
    /// 都没有 → 立即返回（没礼包时不影响原流程）；超过 maxWait 兜底放行，避免礼包链路异常卡死结算。
    /// </summary>
    private IEnumerator WaitSettlementGiftPopupClosed()
    {
        const float maxWait = 20f;
        float waited = 0f;

        while (waited < maxWait)
        {
            bool pending = ShopManager.shopManager != null && ShopManager.shopManager.IsSettlementGiftPopupPending;
            bool showing = SystemManager.Instance != null
                           && SystemManager.Instance.PanelIsShowing(PanelType.AdsDiscountScreen);

            if (!pending && !showing) yield break;

            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        Debug.LogWarning("[ChessFinishView] 等待结算礼包弹窗超过 20s，继续后续流程");
    }

    // ==========================================
    // 串行弹窗 1：禅修榜排名变动面板 (已清理净化)
    // ==========================================
    private IEnumerator CheckAndShowZenRankChange(int addZenCount)
    {
        float waitTime = 0f;
        while (ZenRankManager.Instance.IsFetching)
        {
            waitTime += Time.deltaTime;
            if (waitTime > 5f) yield break; // 超时保护
            yield return null;
        }
        var myData = ZenRankManager.Instance.MyCurrentRankData;
        Debug.Log($"【Rank Debug - Flow】是否进入排名变化加载之前 myData: {(myData != null ? "非空" : "空")}");
        
        if (myData != null)
        {
            // 1. 严格使用缓存中的旧数据，如果没缓存(首次)，说明旧分数就是现在分数减去新增
            // int realOldScore = Mathf.Max(0, myData.score - addZenCount);
            int realOldScore = ZenRankManager.Instance.CachedOldScore;
            int realOldRank = ZenRankManager.Instance.CachedOldRank;
            Debug.Log($"【Rank Debug - Flow】[计算前] 从缓存读取到的真实旧分数: {realOldScore}, 旧排名: {realOldRank}, 准备加上的本局得分: {addZenCount}");
            if (realOldScore == 0 && realOldRank == 0 && myData != null)
            {
                // 真实旧分数 = 服务器最新分数 - 本局加分 (最低不小于0，保护雷达入榜底分)
                realOldScore = Mathf.Max(0, myData.score - addZenCount);
    
                // 如果倒推出来的旧分数是0，说明之前完全没分，名次强制给0以触发飞升动画。
                // 如果有底分，暂时借用最新名次进行兜底。
                realOldRank = (realOldScore == 0) ? 0 : Mathf.Max(1, myData.rank);
    
                Debug.Log($"【Rank Debug - Fix】缓存为空，倒推出真实旧数据 -> 旧分数: {realOldScore}, 旧排名: {realOldRank}");
            }else 
            if (realOldRank <= 0)
            {
                realOldScore = 0; // 旧分数强制修正为0
                realOldRank = 0;  // 之前没上榜，旧名次强制修正为0（让UI显示 "-" 并播放飞升动画）
                Debug.Log("【Rank Debug - Flow】检测到之前未上榜(realOldRank <= 0)，将旧分数和排名强行重置为 0。");
            }
            
            // ==========================================
            // 前端“乐观预测” (解决 Laravel 队列未处理完的问题)
            // ==========================================
            // 不管服务器有没有加分，我们自己先把本局得分加上去！
            int expectedNewScore = realOldScore + addZenCount;
            int optimisticNewRank;
            // 如果服务器下发的分数已经 >= 我们预期的分数，
            // 说明后端队列瞬间处理完了！此时直接使用服务器的真实排名，跳过预测！
            if (myData.score >= expectedNewScore)
            {
                optimisticNewRank = myData.rank;
                expectedNewScore = myData.score;
                Debug.Log($"【Rank Debug - Flow】[命中真实数据] 服务器队列已处理完毕，跳过预测，直接使用最新真实排名:{optimisticNewRank}");
            }
            else
            {
                optimisticNewRank = ZenRankManager.Instance.PredictMyRealRank(realOldRank, expectedNewScore);
                Debug.Log($"【Rank Debug - Flow】[预测结果] 服务器数据未同步，执行预测新排名:{optimisticNewRank}");
            }
            Debug.Log($"【Rank Debug - Flow】传入面板的值 -> 旧分数:{realOldScore}, 新分数:{expectedNewScore}, 新增差值:{expectedNewScore - realOldScore}, 最终应用排名:{optimisticNewRank}");
            // 传入智能抓取的“环境玩家”数据，UI 会自动计算谁在上谁在下！
            // List<ZenRankState> contextPlayers = ZenRankManager.Instance.GetContextPlayersForAnimation(realOldRank, myData.rank);
            List<ZenRankState> contextPlayers = ZenRankManager.Instance.GetContextPlayersForAnimation(realOldRank, optimisticNewRank);
            // ⚠️ 删除了强行把 newRank 置为 1 的假逻辑，没上榜就是 <=0，UI层会显示为 "-"
            UIWindow window = SystemManager.Instance.ShowPanel(PanelType.ZenRankChangePanel);
            if (window != null)
            {
                ZenRankChangePanel rankChangePanel = window.GetComponent<ZenRankChangePanel>();
                string levelCode = GameDataManager.Instance.UserData.Zenlevel;
                string levelName = ZenRankManager.Instance.ZenStates.FirstOrDefault(s => s.Code == levelCode)?.Name ?? "";
          
                rankChangePanel.PlayRankChange(
                    realOldRank, optimisticNewRank,
                    realOldScore, expectedNewScore,
                    levelCode, levelName, ZenRankManager.Instance.RemainingSeconds,
                    contextPlayers // 传入真实环境数据
                );

                // ★ 兜底：面板若因异常/卡死一直不关闭，整个结算流程会永远停在这一步
                //   （线上表现就是"结算界面点不了下一关"）。
                //   正常路径：面板自己的看门狗（30s）会把「继续」按钮恢复可点，玩家点了才关闭；
                //   这里的超时只是最后一道保险，取一个足够长的值，避免玩家正在看名次时被强行关掉。
                const float waitLimitSeconds = 180f;
                float waited = 0f;
                while (SystemManager.Instance.PanelIsShowing(PanelType.ZenRankChangePanel)
                       && waited < waitLimitSeconds)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (SystemManager.Instance.PanelIsShowing(PanelType.ZenRankChangePanel))
                {
                    Debug.LogError($"[ZenFlow] 排名变化面板 {waitLimitSeconds}s 仍未关闭 → 强制关闭，继续结算流程");
                    SystemManager.Instance.HidePanel(PanelType.ZenRankChangePanel);
                }

                yield return new WaitForSeconds(FinishFlowTiming.StepGap);
            }

            // 动画播完，同步缓存防止重复播
            ZenRankManager.Instance.CachedOldScore = expectedNewScore;
            ZenRankManager.Instance.CachedOldRank = optimisticNewRank;
            // ZenRankManager.Instance.SyncCachedRank();
            Debug.Log($"【Rank Debug - Cache】动画播放完毕，已将最新预测值存入缓存。当前 Cache 旧分数变更为: {ZenRankManager.Instance.CachedOldScore}");
        }
    }

    public override void Close()
    {
        base.Close();
        AchievementManager.Instance.DisableFinishAchieveTable();
    }

    protected override void OnDisable()
    {
        LimitTimeManager.Instance.OnDailyTimeUpdated -= UpdateTimeDisplay;
        DailyTaskManager.Instance.OnDailyButterflyTaskUI -= UpdateButterflyTime;
        LimitTimeManager.Instance.OnLimitTimeBtnUI -= UpdateSliderProgress;
        FishInfoController.Instance.OnFishTimeUpdated -= _matchFishtable.UpdateFishTime;
        ChessStageController.Instance.LimitPuzzleCount = 0;
        //GameDataManager.Instance?.UserData.ClearPuzzleVocabulary();

        // ======================= 停止限时去广告倒计时协程 =======================
        if (_removeAdsCoroutine != null)
        {
            StopCoroutine(_removeAdsCoroutine);
            _removeAdsCoroutine = null;
        }

        // ======================= 停止禅意分滚动音效 =======================
        AudioManager.Instance.StopLoopSoundEffect(_zenRollSfx);
        _zenRollSfx = null;

        // EventDispatcher.instance.TriggerUpdateLayerCoin(false,false,false);
        EventDispatcher.instance.TriggerChangeGoldUI(AppGameSettings.LevelCompleteBonus, false);
        if (_treasureBoxEffect != null)
            _treasureBoxEffect.gameObject.SetActive(false);
        base.OnDisable();
    }
}