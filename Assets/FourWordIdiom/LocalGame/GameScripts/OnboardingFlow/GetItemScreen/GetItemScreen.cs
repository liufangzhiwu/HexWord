using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GetItemScreen : UIWindow
{
    [SerializeField] private Button closeBtn; // 关闭按钮
    [SerializeField] private List<Button> pointBtns; // 页面按钮模板（只用 [0] 实例化）
    [SerializeField] private Text title; // 音效文本显示
    [SerializeField] private Text tips; // 语言选择文本显示
    [SerializeField] private Transform AdsCanUseTable; 
    [SerializeField] private Text AdsUseCountText; 
    [SerializeField] private Image AwardIcon; // 语言选择文本显示
    [SerializeField] private Image adsloading;
    [SerializeField] private Image adsIcon;
    [SerializeField] private Button ClaimGoldBtn;
    [SerializeField] private Button ClaimAdsBtn;

    /// <summary>礼包 Prefab（普通礼包用；运行时会被复制到 scrollRect.content 下）</summary>
    [SerializeField] private GiftTable giftTable;
    /// <summary>左侧礼包 Prefab（LuckyBag1 / LuckyBag2 用；样式与普通礼包不同）</summary>
    [SerializeField] private GiftTable gftLeftTable;

    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Transform pointTransform;

    public static LimitRewordType limitRewordType;

    [Header("翻页设置")]
    /// <summary>吸附动画时长（秒）。越短越快，推荐 0.15 ~ 0.3</summary>
    public float snapDuration = 0.2f;
    /// <summary>吸附曲线的平滑度：0=纯匀速（线性），1=强烈缓入缓出</summary>
    [Range(0f, 1f)] public float snapSmoothness = 0.6f;
    /// <summary>拖动速度阈值：低于该值时认为"拖动结束"</summary>
    public float stopVelocityThreshold = 50f;

    private float[] pagePositions;
    private float targetPosition;
    private bool isSnapping;
    private bool wasDragging;
    private int pageCount;
    private float pageWidth;

    // —— 吸附动画状态 ——
    private float snapStartPos;   // 吸附起点（归一化）
    private float snapTimer;      // 吸附已进行时长

    private float dragStartPosition; // 记录拖动开始时的位置
    private bool isDragStartRecorded = false; // 是否已记录拖动起始点
    private int currentPageIndex; // 当前页面索引

    private string eventDes;
    public static string targetWord; // 🌟 新增：用于存储当前目标词组

    /// <summary>当前道具的主礼包（取推荐位第一个，供其他逻辑兼容使用）</summary>
    private ShopDataItem shopDataItem;

    /// <summary>本次实例化的 GiftTable 列表（每次打开重新生成）</summary>
    private readonly List<GiftTable> _giftTableInstances = new List<GiftTable>();

    /// <summary>运行时动态生成的页面按钮（每次刷新礼包时重建）</summary>
    private readonly List<Button> _runtimePointBtns = new List<Button>();

    /// <summary>[Shop] 标签日志</summary>
    private void LogGetItem(string message)
    {
        Debug.Log($"[Shop][GetItemScreen] {message}");
    }

    // ======================= 生命周期 =======================

    protected override void OnEnable()
    {
        base.OnEnable();
        InitUI();
        AudioManager.Instance.PlaySoundEffect("ShowUI");

        if (SystemManager.Instance.PanelIsShowing(PanelType.ChessPlayArea))
        {
            SystemManager.Instance.ShowPanel(PanelType.HeaderSection);
        }

        EventDispatcher.instance.TriggerUpdateLayerCoin(true, true, false, true);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        targetWord = ""; // 🌟 每次关闭弹窗清空目标词
        ClaimGoldBtn.interactable = true;
        closeBtn.interactable = true;
        EventDispatcher.instance.TriggerChangeGoldUI(0, true);
        EventDispatcher.instance.TriggerUpdateLayerCoin(false, true, false);

        if (SystemManager.Instance.PanelIsShowing(PanelType.ChessPlayArea))
        {
            SystemManager.Instance.HidePanel(PanelType.HeaderSection);
        }

        // 清理本次生成的礼包表
        ClearGiftTables();

        // 清理运行时页面按钮
        ClearRuntimePagePoints();

        // 复位吸附状态，防止下次打开时残留
        isSnapping = false;
        wasDragging = false;
        isDragStartRecorded = false;
    }

    protected override void InitializeUIComponents()
    {
        closeBtn.AddClickAction(Close);
        ClaimAdsBtn.AddClickAction(ClickClaimBtn);
        ClaimGoldBtn.AddClickAction(ClickClaimGoldBtn);

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

    // ======================= 页面按钮 =======================

    /// <summary>
    /// 点击页面按钮：滚动到对应的礼包页。
    /// </summary>
    private void OnPagePointClicked(int index)
    {
        if (index < 0 || index >= pageCount)
        {
            LogGetItem($"OnPagePointClicked 忽略: index={index}, pageCount={pageCount}");
            return;
        }

        LogGetItem($"点击页面按钮 index={index}, 当前页={currentPageIndex}");

        if (index == currentPageIndex && !isSnapping)
        {
            // 已在目标页，无需再滚
            return;
        }

        ScrollToPage(index);
        UpdatePagePointState();
    }

    /// <summary>
    /// 根据礼包数量重建页面按钮：
    ///  - 用 pointBtns[0] 作为模板，实例化 count 个到 pointTransform 下；
    ///  - 每个按钮绑定 OnPagePointClicked(index)；
    ///  - count > 1 时显示 pointTransform，否则隐藏。
    /// </summary>
    private void RebuildPagePoints(int count)
    {
        ClearRuntimePagePoints();

        if (pointBtns == null || pointBtns.Count == 0 || pointBtns[0] == null)
        {
            LogGetItem("RebuildPagePoints 跳过: pointBtns 模板未配置");
            return;
        }
        if (pointTransform == null)
        {
            LogGetItem("RebuildPagePoints 跳过: pointTransform 未配置");
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

        LogGetItem($"页面按钮重建: count={count}, pointTransform={(showPoint ? "显示" : "隐藏")}");
    }

    /// <summary>清理运行时动态生成的页面按钮。</summary>
    private void ClearRuntimePagePoints()
    {
        for (int i = _runtimePointBtns.Count - 1; i >= 0; i--)
        {
            if (_runtimePointBtns[i] != null)
                Destroy(_runtimePointBtns[i].gameObject);
        }
        _runtimePointBtns.Clear();
    }

    /// <summary>
    /// 刷新页面按钮的显示与选中状态：
    ///  - 超出礼包数量的按钮 → 整体隐藏；
    ///  - 当前页按钮 → 第 0 个子对象显示（选中态）；
    ///  - 其他按钮 → 第 0 个子对象隐藏（未选中态）。
    /// </summary>
    private void UpdatePagePointState()
    {
        for (int i = 0; i < _runtimePointBtns.Count; i++)
        {
            Button btn = _runtimePointBtns[i];
            if (btn == null) continue;

            bool shouldShow = i < pageCount;
            if (btn.gameObject.activeSelf != shouldShow)
                btn.gameObject.SetActive(shouldShow);

            if (!shouldShow) continue;

            bool isSelected = (i == currentPageIndex);
            if (btn.transform.childCount > 0)
            {
                Transform selectedChild = btn.transform.GetChild(0);
                if (selectedChild.gameObject.activeSelf != isSelected)
                    selectedChild.gameObject.SetActive(isSelected);
            }

            btn.interactable = true;
        }
    }

    // ======================= 初始化 UI =======================

    private void InitUI()
    {
        ClaimAdsBtn.GetComponentInChildren<Text>().text = "免费";
        adsloading.gameObject.SetActive(false);
        adsIcon.gameObject.SetActive(true);
        scrollRect.horizontal = false;

        shopDataItem = null;

        RectTransform rectTransform = AwardIcon.GetComponent<RectTransform>();

        switch (limitRewordType)
        {
            case LimitRewordType.Coins:
                break;
            case LimitRewordType.Butterfly:
                AwardIcon.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("UI_Icon_Butterfly");
                break;
            case LimitRewordType.Tipstool: // 提示灯道具
                title.text = MultilingualManager.Instance.GetString("ItemName01", "pingzi");
                tips.text = MultilingualManager.Instance.GetString("ItemDes01", "pingzi");
                UpdateCliamBtn(true);
                ClaimGoldBtn.GetComponentInChildren<Text>().text = GameDataManager.Instance.UserData.toolInfo[102].cost.ToString();
                AwardIcon.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("tipicon");
                eventDes = title.text + "弹窗广告";
                rectTransform.sizeDelta = new Vector2(310, 310);
                scrollRect.horizontal = true;
                break;
            case LimitRewordType.AutoComplete: // 火箭道具
                UpdateCliamBtn(false);
                title.text = MultilingualManager.Instance.GetString("ItemName02", "pingzi");
                tips.text = MultilingualManager.Instance.GetString("ItemDes02", "pingzi");
                ClaimGoldBtn.GetComponentInChildren<Text>().text = GameDataManager.Instance.UserData.toolInfo[104].cost.ToString();
                AwardIcon.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("rocket");
                rectTransform.sizeDelta = new Vector2(460, 460);
                eventDes = title.text + "弹窗广告";
                if (AuditConfig.IsAuditOpen)
                {
                    scrollRect.horizontal = true;
                }
                break;
        }

        // 刷新礼包表（提示灯/火箭按配置数量生成）
        RefreshGiftTables();

        // 刷新「广告剩余可用次数」显示（仅提示灯道具）
        RefreshAdsUseCountUI();
    }

    // ======================= 广告剩余可用次数显示 =======================

    /// <summary>
    /// 刷新「广告剩余可用次数 / 总可用次数」显示（仅提示灯/灯泡道具）。
    /// - AdsCanUseTable：策略干预（S=1 且 A>=0）时显示，否则隐藏；
    /// - AdsUseCountText：显示 "剩余/总"（如 2/4）。
    /// </summary>
    private void RefreshAdsUseCountUI()
    {
        if (AdsCanUseTable == null) return;

        bool show = false;

        if (limitRewordType == LimitRewordType.Tipstool && ShopManager.shopManager != null)
        {
            AdFailedParams p = ShopManager.shopManager.GetAdFailedParams();
            if (p.enabled && p.bulbVideoPerLevel >= 0)
            {
                int remain = ShopManager.shopManager.GetToolVideoRemainCount(LimitRewordType.Tipstool);
                if (AdsUseCountText != null)
                    AdsUseCountText.text = $"{remain}/{p.bulbVideoPerLevel}";
                show = true;
            }
        }

        AdsCanUseTable.gameObject.SetActive(show);
    }

    // ======================= 礼包表生成 =======================

    private int GetGiftTableCountForType(LimitRewordType type)
    {
        switch (type)
        {
            case LimitRewordType.Tipstool: // 提示灯
                if (AuditConfig.IsAuditOpen)
                {
                    return ShopManager.shopManager.GetBulbBoxCount();
                }
                return 3;
            default:
                if (AuditConfig.IsAuditOpen)
                {
                    return ShopManager.shopManager.GetBulbBoxCount();
                }
                return 1;
        }
    }

    private List<ShopDataItem> GetRecommendPacksForCurrentTool()
    {
        if (ShopManager.shopManager == null)
        {
            LogGetItem("ShopManager 未初始化，跳过礼包获取");
            return new List<ShopDataItem>();
        }

        switch (limitRewordType)
        {
            case LimitRewordType.Tipstool:
                return ShopManager.shopManager.GetRecommendPacks(ShopManager.RecommendSlot.Bulb);
            case LimitRewordType.AutoComplete:
                return ShopManager.shopManager.GetRecommendPacks(ShopManager.RecommendSlot.Rocket);
            default:
                return new List<ShopDataItem>();
        }
    }

    /// <summary>
    /// 特殊礼包：LuckyBag1 / LuckyBag2 使用 gftLeftTable 进行实例化。
    /// </summary>
    private static bool IsLeftTablePack(string produceNameId)
    {
        return produceNameId == "LuckyBag1" || produceNameId == "LuckyBag2";
    }

    /// <summary>
    /// 根据礼包 ID 选择实例化用的 Prefab：
    ///  - LuckyBag1 / LuckyBag2 → gftLeftTable；
    ///  - 其他 → giftTable；
    ///  - gftLeftTable 为空时回退到 giftTable。
    /// </summary>
    private GiftTable GetPrefabForPack(ShopDataItem item)
    {
        if (item != null && IsLeftTablePack(item.produceNameId))
        {
            if (gftLeftTable != null) return gftLeftTable;
            LogGetItem($"礼包 {item.produceNameId} 应使用 gftLeftTable，但未配置，回退 giftTable");
        }
        return giftTable;
    }

    private void RefreshGiftTables()
    {
        ClearGiftTables();

        if (giftTable == null && gftLeftTable == null)
        {
            LogGetItem("GiftTable / gftLeftTable 均未配置，无法生成礼包表");
            if (scrollRect != null) scrollRect.gameObject.SetActive(false);
            RebuildPagePoints(0);
            UpdatePagePointState();
            return;
        }

        if (scrollRect == null || scrollRect.content == null)
        {
            LogGetItem("ScrollRect 或 content 未配置，无法生成礼包表");
            return;
        }

        List<ShopDataItem> packs = GetRecommendPacksForCurrentTool();
        int wantCount = GetGiftTableCountForType(limitRewordType);
        int actualCount = Mathf.Min(wantCount, packs.Count);

        LogGetItem($"准备生成礼包表: limitRewordType={limitRewordType}, 需求数量={wantCount}, 推荐位候选={packs.Count}, 实际生成={actualCount}");

        if (actualCount <= 0)
        {
            scrollRect.gameObject.SetActive(false);
            RebuildPagePoints(0);
            UpdatePagePointState();
            return;
        }

        scrollRect.gameObject.SetActive(true);

        // 主礼包兼容字段（取推荐位第一个，即 1 号位）
        shopDataItem = packs[0];

        // ★ 从后往前遍历：packs[last]（3 号位）先创建 → 物理最左；packs[0]（1 号位）最后创建 → 物理最右
        for (int i = actualCount - 1; i >= 0; i--)
        {
            ShopDataItem item = packs[i];

            // ★ 按礼包 ID 选择 Prefab（LuckyBag1/LuckyBag2 → gftLeftTable，其余 → giftTable）
            GiftTable prefab = GetPrefabForPack(item);
            if (prefab == null)
            {
                LogGetItem($"礼包 {item.produceNameId} 无可用 Prefab，跳过");
                continue;
            }

            GiftTable gt = Instantiate(prefab, scrollRect.content);
            gt.gameObject.SetActive(true);
            gt.InitUI(limitRewordType, item);
            _giftTableInstances.Add(gt);

            int visualIndex = _giftTableInstances.Count - 1;
            string prefabName = prefab == gftLeftTable ? "gftLeftTable" : "giftTable";
            LogGetItem($"生成礼包 [视觉位置={visualIndex}, Prefab={prefabName}]: {item.produceNameId}(id={item.id}, price={item.price}, giftType={item.giftType}) ← packs[{i}]");
        }

        UpdatePageLayout(actualCount);
        ScrollToPage(0, true);

        // ★ 按实际礼包数量重建页面按钮，再刷新选中态
        RebuildPagePoints(actualCount);
        UpdatePagePointState();
    }

    private void ClearGiftTables()
    {
        for (int i = _giftTableInstances.Count - 1; i >= 0; i--)
        {
            if (_giftTableInstances[i] != null)
                Destroy(_giftTableInstances[i].gameObject);
        }
        _giftTableInstances.Clear();
    }

    private void UpdatePageLayout(int count)
    {
        pageCount = Mathf.Max(0, count);
        pageWidth = 1104;

        if (pageCount <= 0)
        {
            pagePositions = new float[0];
            return;
        }

        pagePositions = new float[pageCount];
        for (int i = 0; i < pageCount; i++)
        {
            pagePositions[i] = (pageCount == 1) ? 0f : (float)i / (pageCount - 1);
        }

        currentPageIndex = Mathf.Clamp(currentPageIndex, 0, pageCount - 1);
        LogGetItem($"翻页布局已刷新: pageCount={pageCount}, pageWidth={pageWidth:F1}");
    }

    // ======================= 翻页逻辑 =======================

    private void Update()
    {
        HandleAutoSnapping();
    }

    private int GetCurrentPageIndex()
    {
        if (pagePositions == null || pagePositions.Length == 0) return 0;

        int nearestPage = 0;
        float minDistance = float.MaxValue;

        for (int i = 0; i < pageCount; i++)
        {
            float distance = Mathf.Abs(scrollRect.horizontalNormalizedPosition - pagePositions[i]);
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
        if (pagePositions == null || pagePositions.Length == 0)
        {
            wasDragging = false;
            return;
        }

        bool isDraggingNow = Mathf.Abs(scrollRect.velocity.x) > stopVelocityThreshold;

        if (!wasDragging && isDraggingNow)
        {
            isSnapping = false;
            dragStartPosition = scrollRect.horizontalNormalizedPosition;
            isDragStartRecorded = true;
            currentPageIndex = GetCurrentPageIndex();
        }

        if (wasDragging && !isDraggingNow && isDragStartRecorded)
        {
            float dragEndPosition = scrollRect.horizontalNormalizedPosition;
            float pixelDelta = (dragEndPosition - dragStartPosition) * pageWidth;
            float absPixelDelta = Mathf.Abs(pixelDelta);
            const float pixelThreshold = 10f;

            if (absPixelDelta > pixelThreshold)
            {
                if (pixelDelta > 0)
                {
                    SnapToNextPage();
                    Debug.Log($"右滑翻页 位移:{pixelDelta:F2}px");
                }
                else
                {
                    SnapToPreviousPage();
                    Debug.Log($"左滑翻页 位移:{pixelDelta:F2}px");
                }
            }
            else
            {
                SnapToNearestPage();
                Debug.Log($"微调吸附 位移:{pixelDelta:F2}px");
            }

            isDragStartRecorded = false;
            UpdatePagePointState();
        }

        wasDragging = isDraggingNow;

        if (isSnapping)
        {
            snapTimer += Time.deltaTime;

            float t = snapDuration <= 0f
                ? 1f
                : Mathf.Clamp01(snapTimer / snapDuration);

            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            float easedT = Mathf.Lerp(t, smoothT, snapSmoothness);

            scrollRect.horizontalNormalizedPosition = Mathf.Lerp(snapStartPos, targetPosition, easedT);

            if (t >= 1f)
            {
                scrollRect.horizontalNormalizedPosition = targetPosition;
                isSnapping = false;
                currentPageIndex = GetCurrentPageIndex();

                // ★ 吸附完成后刷新页面按钮状态
                UpdatePagePointState();
            }
        }
    }

    private void SnapToPreviousPage()
    {
        int targetPage = Mathf.Max(0, currentPageIndex - 1);
        ScrollToPage(targetPage);
    }

    private void SnapToNextPage()
    {
        int targetPage = Mathf.Min(pageCount - 1, currentPageIndex + 1);
        ScrollToPage(targetPage);
    }

    private void SnapToNearestPage()
    {
        ScrollToPage(GetCurrentPageIndex());
    }

    public void ScrollToPage(int pageIndex, bool instant = false)
    {
        if (pagePositions == null || pageIndex < 0 || pageIndex >= pageCount) return;

        targetPosition = pagePositions[pageIndex];
        currentPageIndex = pageIndex;

        if (instant || snapDuration <= 0f)
        {
            scrollRect.horizontalNormalizedPosition = targetPosition;
            isSnapping = false;
            snapTimer = 0f;
            snapStartPos = targetPosition;
            UpdatePagePointState();
            return;
        }

        snapStartPos = scrollRect.horizontalNormalizedPosition;
        snapTimer = 0f;
        isSnapping = true;
    }

    // ======================= 按钮回调 =======================

    private void ClickClaimBtn()
    {
        AnalyticMgr.VideoAdClick(eventDes);
        StartCoroutine(ShowAdsRewardUI());
    }

    IEnumerator ShowAdsRewardUI()
    {
        yield return new WaitForSeconds(0.05f);

        // ★ 广告获取失败策略 A：灯泡道具视频达到阈值后弹"假的广告失败"引导付费
        if (limitRewordType == LimitRewordType.Tipstool
            && ShopManager.shopManager != null
            && ShopManager.shopManager.TryHandleToolClickAsFakeFail(LimitRewordType.Tipstool))
        {
            yield break;
        }

        AdRuleManager.Instance.TryShowTipToolRewardVideo(Define.AdKey.RewardAdIdStoreGold, UpdateAdsRewardUI);
    }

    private void ClickClaimGoldBtn()
    {
        ToolInfo toolInfo = GameDataManager.Instance.UserData.toolInfo[102];
        if (limitRewordType == LimitRewordType.Tipstool)
        {
            toolInfo = GameDataManager.Instance.UserData.toolInfo[102];
        }
        else if (limitRewordType == LimitRewordType.AutoComplete)
        {
            toolInfo = GameDataManager.Instance.UserData.toolInfo[104];
        }

        if (toolInfo.cost > GameDataManager.Instance.UserData.Gold)
        {
            SystemManager.Instance.ShowPanel(PanelType.ShopScreen);
            Close();
            return;
        }

        GameDataManager.Instance.UserData.UpdateTool(limitRewordType, 1, "金币购买道具");
        GameDataManager.Instance.UserData.UpdateGold(-toolInfo.cost, true, true, "金币购买道具");

        if (limitRewordType == LimitRewordType.Tipstool)
        {
            if (SystemManager.Instance.PanelIsShowing(PanelType.HexGamePlayArea))
            {
                SystemManager.Instance.GetPanel(PanelType.HexGamePlayArea)?.GetComponent<HexGamePlayArea>()
                    ?.UseTips();
            }

            if (SystemManager.Instance.PanelIsShowing(PanelType.ChessPlayArea))
            {
                SystemManager.Instance.GetPanel(PanelType.ChessPlayArea)?.GetComponent<ChessPlayArea>()
                    ?.UseTips();
            }
        }
        else if (limitRewordType == LimitRewordType.AutoComplete)
        {
            if (SystemManager.Instance.PanelIsShowing(PanelType.HexGamePlayArea))
            {
                SystemManager.Instance.GetPanel(PanelType.HexGamePlayArea)?.GetComponent<HexGamePlayArea>()
                    ?.UseAutoTips();
            }

            if (SystemManager.Instance.PanelIsShowing(PanelType.ChessPlayArea))
            {
                SystemManager.Instance.GetPanel(PanelType.ChessPlayArea)?.GetComponent<ChessPlayArea>()
                    ?.UseComplete();
            }
        }

        Close();
    }

    private void UpdateAdsRewardUI(bool isShow)
    {
        MessageSystem.Instance.HideLoadingAnimation();
        if (isShow)
        {
            Debug.Log("上报的词语2" + targetWord);

            if (SystemManager.Instance.PanelIsShowing(PanelType.HexGamePlayArea))
            {
                GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, 1, "看广告获取" + title.text + "道具");

                SystemManager.Instance.GetPanel(PanelType.HexGamePlayArea)?.GetComponent<HexGamePlayArea>()
                    ?.UseTips();
            }

            if (SystemManager.Instance.PanelIsShowing(PanelType.ChessPlayArea))
            {
                GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, 1, "看广告获取" + title.text + "道具");

                SystemManager.Instance.GetPanel(PanelType.ChessPlayArea)?.GetComponent<ChessPlayArea>()
                    ?.UseTips();
            }

            // ★ 广告获取失败策略：灯泡视频成功观看，累计当前关卡次数（A 阈值判断）
            if (limitRewordType == LimitRewordType.Tipstool && ShopManager.shopManager != null)
                ShopManager.shopManager.OnToolVideoWatched(LimitRewordType.Tipstool);

            AnalyticMgr.VideoAdSuccess(eventDes);
            Close();
        }
        else
        {
            MessageSystem.Instance.ShowTip("广告加载失败，请稍后重试。");
            AnalyticMgr.VideoAdFail(eventDes);
        }
    }

    private void UpdateCliamBtn(bool canClaimed)
    {
        ClaimAdsBtn.gameObject.SetActive(canClaimed);
        ClaimGoldBtn.gameObject.SetActive(true);
    }

    // ======================= 关闭 =======================

    public void Close()
    {
        AudioManager.Instance.TriggerVibration(10, 200);
        base.Close();
    }

    public override void OnHideAnimationEnd()
    {
        base.OnHideAnimationEnd();
    }
}