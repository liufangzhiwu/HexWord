using System;
using System.Collections;
using System.Collections.Generic;
using FourWordIdiom.LocalGame.GameScripts.Controller;
using Middleware;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FailGameScreen : UIWindow, IPointerDownHandler, IPointerUpHandler
{
    [Header("面板节点")]
    [SerializeField] private GameObject failPanel;
    [SerializeField] private GameObject continuePanel;

    [Header("主失败界面按钮")]
    [SerializeField] private Button addTimeBtn;
    [SerializeField] private Button exitBtn;
    [SerializeField] private Text reviveBtnText;
    [SerializeField] private GameObject adIcon;
    [SerializeField] private Text timeText;    // 上局时间

    [Header("内购礼包推荐位（复活礼包只有 1 个购买位）")]
    /// <summary>复活礼包位：场景里直接挂一个 GiftTable 实例，打开时按当前决策结果刷新数据</summary>
    [SerializeField] private GiftTable giftTable;

    [Header("二次确认界面按钮")]
    [SerializeField] private Text conTitleText;
    [SerializeField] private Text conTipText;
    [SerializeField] private Button realExitBtn;
    [SerializeField] private Button returnGame;
    [SerializeField] private GameObject confirmPupaIcon;

    private CanvasGroup _canvasGroup;
    private Coroutine _longPressCoroutine;
    private bool _isHiddenByLongPress = false;

    /// <summary>[Shop] 标签日志</summary>
    private void LogFailGame(string message)
    {
        Debug.Log($"[Shop][FailGameScreen] {message}");
    }

    // ======================= 生命周期 =======================

    private void Start()
    {
        addTimeBtn.AddClickAction(OnReviveClicked);
        exitBtn.AddClickAction(() => SwitchPanel(false));
        returnGame.AddClickAction(() => SwitchPanel(true));
        realExitBtn.AddClickAction(OnRealExitClicked);

        conTitleText.text = MultilingualManager.Instance.GetString("AreYouSure");
        conTipText.text = MultilingualManager.Instance.GetString("YouWillLose");
        realExitBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("ConfirmExit");
        returnGame.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("LetMeThink");

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        AudioManager.Instance.PlaySoundEffect("ShowUI");

        SwitchPanel(true); // 每次打开弹窗，强制显示主失败页
        RefreshReviveUI(); // 刷新复活按钮的UI表现 (是否免费)
        RefreshPupaIconVisibility();
        AnalyticMgr.LevelFailed();

        ChessStageController.Instance.CurrStageData.IsPausedOrFailed = true;
        GameDataManager.Instance.CommitGameData();

        EventDispatcher.instance.TriggerUpdateLayerCoin(false, true, false, true);

        // ★ 刷新复活界面内购推荐位（只有 1 个购买位）
        RefreshGiftTables();
    }

    protected override void OnDisable()
    {
        // 🌟 我关闭了，通知头部恢复原状！
        EventDispatcher.instance.TriggerHighlightHeaderUI(false);
        base.OnDisable();
    }

    // ======================= 复活界面内购推荐位（只有 1 个位） =======================

    /// <summary>
    /// 刷新复活界面的内购推荐位。
    /// 复活购买位只有一个：RecommendSlot.Clock（maxFeatured = 1），永远最多 1 个礼包。
    /// 直接操作场景里挂的 giftTable 实例：
    ///   - 有候选礼包 → 显示并 InitUI；
    ///   - 无候选礼包 → 隐藏。
    /// </summary>
    private void RefreshGiftTables()
    {
        if (giftTable == null)
        {
            LogFailGame("GiftTable 未配置，跳过礼包推荐位");
            return;
        }

        if (ShopManager.shopManager == null)
        {
            LogFailGame("ShopManager 未初始化，隐藏礼包推荐位");
            giftTable.gameObject.SetActive(false);
            return;
        }

        // 复活推荐位：RecommendSlot.Clock，maxFeatured=1，永远只有 1 个购买位
        List<ShopDataItem> packs = ShopManager.shopManager.GetRecommendPacks(ShopManager.RecommendSlot.Clock);

        const int wantCount = 1; // ★ 复活购买位只有一个
        int candidateCount = packs != null ? packs.Count : 0;

        LogFailGame($"准备刷新复活礼包推荐位: 购买位数={wantCount}, 推荐位候选={candidateCount}");

        if (candidateCount <= 0)
        {
            // 无礼包可出（全部冷却 / 全部限购 / 权重都是 0）→ 隐藏礼包位
            giftTable.gameObject.SetActive(false);
            LogFailGame("无候选礼包，隐藏复活礼包位");
            return;
        }

        // 只取第一个（一号位）作为复活购买位
        ShopDataItem item = packs[0];
        giftTable.gameObject.SetActive(true);
        giftTable.InitUI(LimitRewordType.ReStartTool, item); // 复活道具类型（如项目枚举名不同，改成对应的即可）

        LogFailGame($"刷新复活礼包: {item.produceNameId}(id={item.id}, price={item.price}, giftType={item.giftType})");
    }

    // ======================= 面板切换 =======================

    /// <summary>
    /// 面板切换助手
    /// </summary>
    private void SwitchPanel(bool showFailPanel)
    {
        //是否处于无限体力中
        if (GameDataManager.Instance.UserData.HasUnlimitedEnergy() && !showFailPanel)
        {
            OnRealExitClicked();
            return;
        }

        float rawTime = ChessStageController.Instance.CurrStageData.RemainingTime;
        float safeTime = Mathf.Max(0, rawTime);
        int minutes = Mathf.FloorToInt(safeTime / 60F);
        int seconds = Mathf.FloorToInt(safeTime % 60F);
        timeText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        timeText.gameObject.SetActive(showFailPanel);

        if (failPanel != null) failPanel.SetActive(showFailPanel);
        if (continuePanel != null) continuePanel.SetActive(!showFailPanel);

        if (showFailPanel)
            SystemManager.Instance.ShowPanel(PanelType.HeaderSection);

        EventDispatcher.instance.TriggerHighlightHeaderUI(!showFailPanel);
    }

    /// <summary>
    /// 刷新复活按钮表现（免费还是看广告）
    /// </summary>
    private void RefreshReviveUI()
    {
        bool isFree = !GameDataManager.Instance.UserData.hasUsedFreeRevive;

        if (isFree)
        {
            reviveBtnText.text = "免费复活";
            adIcon.SetActive(false);
        }
        else
        {
            reviveBtnText.text = "复活";
            adIcon.SetActive(true);
        }
    }

    /// <summary>
    /// 🌟 点击复活按钮：判断是免费还是看视频
    /// </summary>
    private void OnReviveClicked()
    {
        bool isFree = !GameDataManager.Instance.UserData.hasUsedFreeRevive;

        if (isFree)
        {
            Debug.Log("使用首次免费复活！");
            // 标记已使用并保存存档
            GameDataManager.Instance.UserData.hasUsedFreeRevive = true;

            // 执行复活
            ExecuteRevive();
        }
        else
        {
            Debug.Log("免费次数已用完，准备播放激励视频广告...");
            AnalyticMgr.VideoAdClick("复活广告");
            // 播放激励视频
            AdRuleManager.Instance.TryShowRewardVideo(Define.AdKey.RewardAdIdStoreGold, success =>
            {
                if (success)
                {
                    AnalyticMgr.VideoAdSuccess("复活广告");
                    ExecuteRevive();
                    GameDataManager.Instance.UserData.totalSeeAds++;
                }
                else
                {
                    MessageSystem.Instance.ShowTip("广告未播放完成，无法复活！");
                    AnalyticMgr.VideoAdFail("复活广告");
                }
            });
        }
    }

    /// <summary>
    /// 执行真正的复活并返回游戏
    /// </summary>
    private void ExecuteRevive()
    {
        SystemManager.Instance.HidePanel(PanelType.FailGameScreen);
        ChessStageController.Instance.CurrStageData.IsPausedOrFailed = false;
        GameDataManager.Instance.CommitGameData();
        ChessPlayArea.Instance.ReviveGame(60f);
    }

    /// <summary>
    /// 🌟 点击真正的退出按钮：扣分、扣体力、删存档
    /// </summary>
    private void OnRealExitClicked()
    {
        if (ChessPlayArea.Instance != null)
        {
            if (ChessPlayArea.Instance.chessboardGrid != null)
            {
                ChessPlayArea.Instance.chessboardGrid.IsBlockInput = true;
            }
            // 锁死下方的待填字盘
            ChessBowlGrid.IsTutorialBlocking = true;
        }
        EventDispatcher.instance.TriggerHighlightHeaderUI(false);
        SystemManager.Instance.HidePanel(PanelType.HeaderSection, false);
        ChessPlayArea.Instance.QuitGameAndDeductEnergy();

        if (ChessStageController.Instance.CurrStageData != null &&
            ChessStageController.Instance.CurrStageData.FoundTargetPuzzles.Count > 0)
        {
            AnalyticMgr.LevelExit();
        }

        SystemManager.Instance.HidePanel(PanelType.FailGameScreen);
        ChessGuideSystem.Instance.CloseGuide();
    }

    /// <summary>
    /// 根据当前关卡能力，刷新蝶蛹图标的显示
    /// </summary>
    private void RefreshPupaIconVisibility()
    {
        if (confirmPupaIcon == null) return;

        int currentScore = ChessStageController.Instance.CurrentTotalScore;
        int threshold = ButterfliesManager.Instance.GetScoreThresholdForPupa();
        bool currentlyHasPupa = currentScore >= threshold;

        int optimalScore = ChessStageController.Instance.OptimalTotalScore;
        bool canLevelProduce = ButterfliesManager.Instance.CanShowPupaProgressBarThisLevel(optimalScore);

        bool shouldShow = canLevelProduce && currentlyHasPupa;
        confirmPupaIcon.SetActive(shouldShow);
    }

    // ==========================================
    // 🌟 4. 长按隐藏逻辑核心实现
    // ==========================================
    public new void OnPointerDown(PointerEventData eventData)
    {
        if (_longPressCoroutine != null) StopCoroutine(_longPressCoroutine);
        _longPressCoroutine = StartCoroutine(LongPressRoutine());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_longPressCoroutine != null)
        {
            StopCoroutine(_longPressCoroutine);
            _longPressCoroutine = null;
        }

        if (_isHiddenByLongPress)
        {
            _isHiddenByLongPress = false;
            _canvasGroup.alpha = 1f;

            if (continuePanel.activeSelf)
            {
                EventDispatcher.instance.TriggerHighlightHeaderUI(true);
            }
        }
    }

    private IEnumerator LongPressRoutine()
    {
        yield return new WaitForSeconds(0.25f);

        _isHiddenByLongPress = true;
        _canvasGroup.alpha = 0f;

        EventDispatcher.instance.TriggerHighlightHeaderUI(false);
    }
}