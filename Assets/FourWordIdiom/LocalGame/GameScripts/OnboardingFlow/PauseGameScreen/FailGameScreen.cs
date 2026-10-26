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
    [SerializeField] private Button adsAddTimeBtn;
    [SerializeField] private Button exitBtn;
    [SerializeField] private Button goldAddTimeBtn;
    [SerializeField] private Text reviveAdsText;
    [SerializeField] private GameObject adsIcon;
    [SerializeField] private Text timeText;    // 上局时间

    [Header("内购礼包推荐位（复活礼包）")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Transform pointTransform;
    /// <summary>页面按钮模板（只用 [0] 实例化）</summary>
    [SerializeField] private List<Button> pointBtns;
    /// <summary>
    /// 复活礼包位：场景里挂一个 GiftTable 实例。
    /// 审核关或候选≤1：直接使用该实例显示单礼包；
    /// 审核开且候选>1：作为 content 下第一个礼包容器，其余动态实例化。
    /// </summary>
    [SerializeField] private GiftTable giftTable;

    [SerializeField] private Text addSecondText; // 复活增加秒数
    [SerializeField] private GameObject freeGetButterToolTable; // 免费赠送一个蝴蝶道具
    [SerializeField] private GameObject adsCanUseTable; // 广告可以用次数
    [SerializeField] private Text adsUseCountText;      // 广告剩余可用次数/总可用次数

    [Header("二次确认界面按钮")]
    [SerializeField] private Text conTitleText;
    [SerializeField] private Text conTipText;
    [SerializeField] private Button realExitBtn;
    [SerializeField] private Button adsAddTimeBtn2;
    [SerializeField] private Button goldAddTimeBtn2;
    [SerializeField] private GameObject freeGetButterToolTable2; // 免费赠送一个蝴蝶道具
    [SerializeField] private GameObject confirmPupaIcon;
    
    [SerializeField] private GameObject adsCanUseTable2; // 广告可以用次数
    [SerializeField] private Text adsUseCountText2;      // 广告剩余可用次数/总可用次数

    [Header("翻页设置（审核多礼包模式）")]
    public float snapDuration = 0.2f;
    [Range(0f, 1f)] public float snapSmoothness = 0.6f;
    public float stopVelocityThreshold = 50f;

    private CanvasGroup _canvasGroup;
    private Coroutine _longPressCoroutine;
    private bool _isHiddenByLongPress = false;

    // ======================= 审核多礼包状态 =======================

    private bool _auditMultiMode = false;
    private readonly List<GiftTable> _giftTableInstances = new List<GiftTable>();
    private readonly List<Button> _runtimePointBtns = new List<Button>();

    private float[] _pagePositions;
    private float _targetPosition;
    private bool _isSnapping;
    private bool _wasDragging;
    private int _pageCount;
    private float _pageWidth = 1104f;
    private float _snapStartPos;
    private float _snapTimer;
    private float _dragStartPosition;
    private bool _isDragStartRecorded = false;
    private int _currentPageIndex;

    /// <summary>[Shop] 标签日志</summary>
    private void LogFailGame(string message)
    {
        Debug.Log($"[Shop][FailGameScreen] {message}");
    }

    // ======================= 生命周期 =======================

    private void Start()
    {
        adsAddTimeBtn.AddClickAction(OnReviveClicked);
        goldAddTimeBtn.AddClickAction(OnGoldReviveClicked);
        goldAddTimeBtn2.AddClickAction(OnGoldReviveClicked);
        exitBtn.AddClickAction(() => SwitchPanel(false));
        adsAddTimeBtn2.AddClickAction(OnReviveClicked);
        realExitBtn.AddClickAction(OnRealExitClicked);

        conTitleText.text = MultilingualManager.Instance.GetString("AreYouSure");
        conTipText.text = MultilingualManager.Instance.GetString("YouWillLose");
        realExitBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("ConfirmExit");

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // ★ pointBtns 只作为模板，场景里原有按钮全部隐藏，运行时动态生成
        if (pointBtns != null)
        {
            for (int i = 0; i < pointBtns.Count; i++)
            {
                if (pointBtns[i] != null)
                    pointBtns[i].gameObject.SetActive(false);
            }
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        AudioManager.Instance.PlaySoundEffect("ShowUI");

        SwitchPanel(true); // 每次打开弹窗，强制显示主失败页
        RefreshReviveUI(); // 刷新复活按钮的UI表现 (是否免费)
        RefreshFreeButterToolUI(); // 刷新「免费赠送蝴蝶道具」提示显隐
        RefreshPupaIconVisibility();
        AnalyticMgr.LevelFailed();

        ChessStageController.Instance.CurrStageData.IsPausedOrFailed = true;
        GameDataManager.Instance.CommitGameData();

        EventDispatcher.instance.TriggerUpdateLayerCoin(false, true, false, true);

        // ★ 刷新复活界面内购推荐位
        RefreshGiftTables();
    }

    protected override void OnDisable()
    {
        // 🌟 我关闭了，通知头部恢复原状！
        EventDispatcher.instance.TriggerHighlightHeaderUI(false);

        // ★ 清理本次生成的礼包实例和页面按钮
        ClearGiftTables();
        ClearRuntimePagePoints();

        // 复位状态
        _auditMultiMode = false;
        _isSnapping = false;
        _wasDragging = false;
        _isDragStartRecorded = false;

        base.OnDisable();
    }

    private void Update()
    {
        if (_auditMultiMode)
        {
            HandleAutoSnapping();
        }
    }

    // ======================= 复活界面内购推荐位 =======================

    /// <summary>
    /// 刷新复活界面的内购推荐位。
    ///  - 审核关或候选 ≤ 1：原逻辑，单礼包使用场景里的 giftTable；
    ///  - 审核开且候选 > 1：横滑多礼包模式，场景 giftTable 作为第一个礼包容器，
    ///    其余动态 Instantiate 到 scrollRect.content；同时重建页面按钮。
    /// </summary>
    private void RefreshGiftTables()
    {
        ClearGiftTables();
        ClearRuntimePagePoints();
        _auditMultiMode = false;

        if (giftTable == null)
        {
            LogFailGame("GiftTable 未配置，跳过礼包推荐位");
            return;
        }

        if (ShopManager.shopManager == null)
        {
            LogFailGame("ShopManager 未初始化，隐藏礼包推荐位");
            giftTable.gameObject.SetActive(false);
            if (scrollRect != null) scrollRect.gameObject.SetActive(false);
            if (pointTransform != null) pointTransform.gameObject.SetActive(false);
            return;
        }

        // 审核开 → 全部复活礼包；审核关 → 原权重推荐结果
        List<ShopDataItem> packs = AuditConfig.IsAuditOpen
            ? ShopManager.shopManager.GetAuditPacksForSlot(ShopManager.RecommendSlot.Clock)
            : ShopManager.shopManager.GetRecommendPacks(ShopManager.RecommendSlot.Clock);

        int count = packs != null ? packs.Count : 0;
        LogFailGame($"复活礼包候选数={count}, 审核开={AuditConfig.IsAuditOpen}");

        if (count <= 0)
        {
            giftTable.gameObject.SetActive(false);
            if (scrollRect != null) scrollRect.gameObject.SetActive(false);
            if (pointTransform != null) pointTransform.gameObject.SetActive(false);
            LogFailGame("无候选礼包，隐藏复活礼包位");
            return;
        }

        // ★ 审核关 或 候选 ≤ 1：原单礼包逻辑
        if (!AuditConfig.IsAuditOpen || count <= 1)
        {
            giftTable.gameObject.SetActive(true);
            giftTable.InitUI(LimitRewordType.ReStartTool, packs[0]);
            if (pointTransform != null) pointTransform.gameObject.SetActive(false);
            LogFailGame($"单礼包模式: {packs[0].produceNameId}(id={packs[0].id}, price={packs[0].price})");
            return;
        }

        // ★ 审核开 + 候选 > 1：横滑多礼包
        if (scrollRect == null || scrollRect.content == null)
        {
            LogFailGame("ScrollRect / content 未配置，退回单礼包");
            giftTable.gameObject.SetActive(true);
            giftTable.transform.SetParent(scrollRect.content);
            giftTable.InitUI(LimitRewordType.ReStartTool, packs[0]);
            return;
        }

        if (giftTable.transform.parent != scrollRect.content)
        {
            LogFailGame("提示：giftTable 不在 scrollRect.content 下，横滑布局可能异常");
        }

        _auditMultiMode = true;
        scrollRect.gameObject.SetActive(true);
        scrollRect.horizontal = true;

        // 用场景 giftTable 作视觉最左（packs[count-1]）
        giftTable.gameObject.SetActive(true);
        giftTable.InitUI(LimitRewordType.ReStartTool, packs[count - 1]);
        _giftTableInstances.Add(giftTable);

        // 实例化其余（packs[count-2] ... packs[0]，依次向右）
        for (int i = count - 2; i >= 0; i--)
        {
            GiftTable gt = Instantiate(giftTable, scrollRect.content);
            gt.gameObject.SetActive(true);
            gt.InitUI(LimitRewordType.ReStartTool, packs[i]);
            _giftTableInstances.Add(gt);

            LogFailGame($"生成礼包 [视觉位置={_giftTableInstances.Count - 1}]: {packs[i].produceNameId}(id={packs[i].id}, price={packs[i].price})");
        }

        UpdatePageLayout(count);
        ScrollToPage(0, true);

        // 页面按钮
        RebuildPagePoints(count);
        UpdatePagePointState();

        LogFailGame($"多礼包模式: 共 {count} 个，页面按钮已重建");
    }

    private void ClearGiftTables()
    {
        for (int i = _giftTableInstances.Count - 1; i >= 0; i--)
        {
            GiftTable gt = _giftTableInstances[i];
            if (gt == null) continue;

            if (gt == giftTable)
            {
                // 场景实例，只隐藏不销毁
                giftTable.gameObject.SetActive(false);
            }
            else
            {
                Destroy(gt.gameObject);
            }
        }
        _giftTableInstances.Clear();
    }

    // ======================= 页面按钮 =======================

    private void OnPagePointClicked(int index)
    {
        if (index < 0 || index >= _pageCount)
        {
            LogFailGame($"OnPagePointClicked 忽略: index={index}, pageCount={_pageCount}");
            return;
        }

        if (index == _currentPageIndex && !_isSnapping)
            return;

        ScrollToPage(index);
        UpdatePagePointState();
    }

    private void RebuildPagePoints(int count)
    {
        ClearRuntimePagePoints();

        if (pointBtns == null || pointBtns.Count == 0 || pointBtns[0] == null)
        {
            LogFailGame("RebuildPagePoints 跳过: pointBtns 模板未配置");
            return;
        }
        if (pointTransform == null)
        {
            LogFailGame("RebuildPagePoints 跳过: pointTransform 未配置");
            return;
        }

        Button template = pointBtns[0];

        for (int i = 0; i < count; i++)
        {
            Button btn = Instantiate(template, pointTransform);
            btn.gameObject.SetActive(true);
            int index = i;
            btn.AddClickAction(() => OnPagePointClicked(index));
            _runtimePointBtns.Add(btn);
        }

        bool showPoint = count > 1;
        if (pointTransform.gameObject.activeSelf != showPoint)
            pointTransform.gameObject.SetActive(showPoint);

        LogFailGame($"页面按钮重建: count={count}, pointTransform={(showPoint ? "显示" : "隐藏")}");
    }

    private void ClearRuntimePagePoints()
    {
        for (int i = _runtimePointBtns.Count - 1; i >= 0; i--)
        {
            if (_runtimePointBtns[i] != null)
                Destroy(_runtimePointBtns[i].gameObject);
        }
        _runtimePointBtns.Clear();
    }

    private void UpdatePagePointState()
    {
        for (int i = 0; i < _runtimePointBtns.Count; i++)
        {
            Button btn = _runtimePointBtns[i];
            if (btn == null) continue;

            bool shouldShow = i < _pageCount;
            if (btn.gameObject.activeSelf != shouldShow)
                btn.gameObject.SetActive(shouldShow);

            if (!shouldShow) continue;

            bool isSelected = (i == _currentPageIndex);
            if (btn.transform.childCount > 0)
            {
                Transform selectedChild = btn.transform.GetChild(0);
                if (selectedChild.gameObject.activeSelf != isSelected)
                    selectedChild.gameObject.SetActive(isSelected);
            }

            btn.interactable = true;
        }
    }

    // ======================= 翻页逻辑 =======================

    private void UpdatePageLayout(int count)
    {
        _pageCount = Mathf.Max(0, count);

        if (_pageCount <= 0)
        {
            _pagePositions = new float[0];
            return;
        }

        _pagePositions = new float[_pageCount];
        for (int i = 0; i < _pageCount; i++)
        {
            _pagePositions[i] = (_pageCount == 1) ? 0f : (float)i / (_pageCount - 1);
        }

        _currentPageIndex = Mathf.Clamp(_currentPageIndex, 0, _pageCount - 1);
        LogFailGame($"翻页布局已刷新: pageCount={_pageCount}, pageWidth={_pageWidth:F1}");
    }

    private int GetCurrentPageIndex()
    {
        if (_pagePositions == null || _pagePositions.Length == 0) return 0;

        int nearestPage = 0;
        float minDistance = float.MaxValue;

        for (int i = 0; i < _pageCount; i++)
        {
            float distance = Mathf.Abs(scrollRect.horizontalNormalizedPosition - _pagePositions[i]);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestPage = i;
            }
        }
        return nearestPage;
    }

    private void HandleAutoSnapping()
    {
        if (_pagePositions == null || _pagePositions.Length == 0)
        {
            _wasDragging = false;
            return;
        }

        bool isDraggingNow = Mathf.Abs(scrollRect.velocity.x) > stopVelocityThreshold;

        if (!_wasDragging && isDraggingNow)
        {
            _isSnapping = false;
            _dragStartPosition = scrollRect.horizontalNormalizedPosition;
            _isDragStartRecorded = true;
            _currentPageIndex = GetCurrentPageIndex();
        }

        if (_wasDragging && !isDraggingNow && _isDragStartRecorded)
        {
            float dragEndPosition = scrollRect.horizontalNormalizedPosition;
            float pixelDelta = (dragEndPosition - _dragStartPosition) * _pageWidth;
            float absPixelDelta = Mathf.Abs(pixelDelta);
            const float pixelThreshold = 10f;

            if (absPixelDelta > pixelThreshold)
            {
                if (pixelDelta > 0) SnapToNextPage();
                else SnapToPreviousPage();
            }
            else
            {
                SnapToNearestPage();
            }

            _isDragStartRecorded = false;
            UpdatePagePointState();
        }

        _wasDragging = isDraggingNow;

        if (_isSnapping)
        {
            _snapTimer += Time.deltaTime;

            float t = snapDuration <= 0f
                ? 1f
                : Mathf.Clamp01(_snapTimer / snapDuration);

            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            float easedT = Mathf.Lerp(t, smoothT, snapSmoothness);

            scrollRect.horizontalNormalizedPosition = Mathf.Lerp(_snapStartPos, _targetPosition, easedT);

            if (t >= 1f)
            {
                scrollRect.horizontalNormalizedPosition = _targetPosition;
                _isSnapping = false;
                _currentPageIndex = GetCurrentPageIndex();
                UpdatePagePointState();
            }
        }
    }

    private void SnapToPreviousPage()
    {
        int targetPage = Mathf.Max(0, _currentPageIndex - 1);
        ScrollToPage(targetPage);
    }

    private void SnapToNextPage()
    {
        int targetPage = Mathf.Min(_pageCount - 1, _currentPageIndex + 1);
        ScrollToPage(targetPage);
    }

    private void SnapToNearestPage()
    {
        ScrollToPage(GetCurrentPageIndex());
    }

    private void ScrollToPage(int pageIndex, bool instant = false)
    {
        if (_pagePositions == null || pageIndex < 0 || pageIndex >= _pageCount) return;

        _targetPosition = _pagePositions[pageIndex];
        _currentPageIndex = pageIndex;

        if (instant || snapDuration <= 0f)
        {
            scrollRect.horizontalNormalizedPosition = _targetPosition;
            _isSnapping = false;
            _snapTimer = 0f;
            _snapStartPos = _targetPosition;
            UpdatePagePointState();
            return;
        }

        _snapStartPos = scrollRect.horizontalNormalizedPosition;
        _snapTimer = 0f;
        _isSnapping = true;
    }

    // ======================= 面板切换 =======================

    private void SwitchPanel(bool showFailPanel)
    {
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

    private void RefreshReviveUI()
    {
        bool isFree = !GameDataManager.Instance.UserData.hasUsedFreeRevive;

        if (isFree)
        {
            reviveAdsText.text = "免费复活";
            adsIcon.SetActive(false);
        }
        else
        {
            reviveAdsText.text = "复活";
            adsIcon.SetActive(true);
        }

        // ★ 刷新复活视频「剩余可用次数/总可用次数」显示
        RefreshAdsUseCountUI();
    }

    /// <summary>
    /// 刷新复活视频「广告剩余可用次数 / 总可用次数」显示（主失败页 + 二次确认页）。
    /// </summary>
    private void RefreshAdsUseCountUI()
    {
        RefreshAdsUseCountPanel(adsCanUseTable, adsUseCountText);
        RefreshAdsUseCountPanel(adsCanUseTable2, adsUseCountText2);
    }

    private void RefreshAdsUseCountPanel(GameObject canUseTable, Text useCountText)
    {
        if (canUseTable == null) return;

        bool show = false;

        if (ShopManager.shopManager != null)
        {
            AdFailedParams p = ShopManager.shopManager.GetAdFailedParams();
            if (p.enabled && p.reviveVideoPerLevel >= 0)
            {
                int remain = ShopManager.shopManager.GetToolVideoRemainCount(LimitRewordType.ReStartTool);
                if (useCountText != null)
                    useCountText.text = $"{remain}/{p.reviveVideoPerLevel}";
                show = true;
            }
        }

        canUseTable.SetActive(show);
    }

    // ======================= 免费赠送蝴蝶道具（首次金币复活） =======================

    /// <summary>
    /// 是否显示「免费赠送蝴蝶道具」提示：玩家尚未使用过金币复活 && 玩家没有蝴蝶道具。
    /// </summary>
    private bool ShouldShowFreeButterTool()
    {
        var userData = GameDataManager.Instance != null ? GameDataManager.Instance.UserData : null;
        if (userData == null) return false;
        if (userData.hasUsedGoldRevive) return false;

        // 玩家没有蝴蝶道具（toolId = 103）
        if (userData.toolInfo != null
            && userData.toolInfo.TryGetValue(103, out var butterflyInfo)
            && butterflyInfo != null
            && butterflyInfo.count > 0)
        {
            return false;
        }

        return true;
    }

    /// <summary>刷新「免费赠送蝴蝶道具」提示的显隐（主失败页 + 二次确认页）。</summary>
    private void RefreshFreeButterToolUI()
    {
        bool show = ShouldShowFreeButterTool();
        if (freeGetButterToolTable != null) freeGetButterToolTable.SetActive(show);
        if (freeGetButterToolTable2 != null) freeGetButterToolTable2.SetActive(show);
    }

    private void OnGoldReviveClicked()
    {
        bool isGold = GameDataManager.Instance.UserData.Gold >= 60;

        if (isGold)
        {
            Debug.Log("使用金币复活！");
            GameDataManager.Instance.UserData.UpdateGold(-60, true, true, "复活广告消耗金币");

            // ★ 首次金币复活 + 玩家没有蝴蝶道具 → 赠送一个蝴蝶道具
            if (ShouldShowFreeButterTool())
            {
                GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Butterfly, 1, "金币复活赠送蝴蝶");
                GameDataManager.Instance.UserData.hasUsedGoldRevive = true;
                MessageSystem.Instance.ShowTip("赠送蝴蝶道具 x1");
            }

            ExecuteRevive();
        }
        else
        {
            MessageSystem.Instance.ShowTip("TipGoldInsufficient");
        }
    }

    private void OnReviveClicked()
    {
        bool isFree = !GameDataManager.Instance.UserData.hasUsedFreeRevive;

        if (isFree)
        {
            Debug.Log("使用首次免费复活！");
            GameDataManager.Instance.UserData.hasUsedFreeRevive = true;
            ExecuteRevive();
        }
        else
        {
            // ★ 广告获取失败策略 B：复活道具视频达到阈值后弹"假的广告失败"引导付费
            if (ShopManager.shopManager != null
                && ShopManager.shopManager.TryHandleToolClickAsFakeFail(LimitRewordType.ReStartTool))
            {
                return;
            }

            Debug.Log("免费次数已用完，准备播放激励视频广告...");
            AnalyticMgr.VideoAdClick("复活广告");
            AdRuleManager.Instance.TryShowRewardVideo(Define.AdKey.RewardAdIdStoreGold, success =>
            {
                if (success)
                {
                    AnalyticMgr.VideoAdSuccess("复活广告");

                    // ★ 广告获取失败策略：复活视频成功观看，累计当前关卡次数（B 阈值判断）
                    if (ShopManager.shopManager != null)
                        ShopManager.shopManager.OnToolVideoWatched(LimitRewordType.ReStartTool);

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

    private void ExecuteRevive()
    {
        SystemManager.Instance.HidePanel(PanelType.FailGameScreen);
        ChessStageController.Instance.CurrStageData.IsPausedOrFailed = false;
        GameDataManager.Instance.CommitGameData();
        ChessPlayArea.Instance.ReviveGame(60f);
    }

    private void OnRealExitClicked()
    {
        if (ChessPlayArea.Instance != null)
        {
            if (ChessPlayArea.Instance.chessboardGrid != null)
            {
                ChessPlayArea.Instance.chessboardGrid.IsBlockInput = true;
            }
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
    // 🌟 长按隐藏逻辑
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