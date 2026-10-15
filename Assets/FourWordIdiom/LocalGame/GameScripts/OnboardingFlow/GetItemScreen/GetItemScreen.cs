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
    [SerializeField] private Text title; // 音效文本显示
    [SerializeField] private Text tips; // 语言选择文本显示
    [SerializeField] private Image AwardIcon; // 语言选择文本显示
    [SerializeField] private Image adsloading;
    [SerializeField] private Image adsIcon;
    [SerializeField] private Button ClaimGoldBtn;
    [SerializeField] private Button ClaimAdsBtn;

    /// <summary>礼包 Prefab（在 Inspector 里挂一个 GiftTable 的 Prefab，运行时会被复制到 scrollRect.content 下）</summary>
    [SerializeField] private GiftTable giftTable;

    [SerializeField] private ScrollRect scrollRect;

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

    // —— 新增：吸附动画状态 ——
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

        // 复位吸附状态，防止下次打开时残留
        isSnapping = false;
        wasDragging = false;
        isDragStartRecorded = false;
    }

    protected override void InitializeUIComponents()
    {
        closeBtn.AddClickAction(Close); // 绑定关闭按钮事件
        ClaimAdsBtn.AddClickAction(ClickClaimBtn);
        ClaimGoldBtn.AddClickAction(ClickClaimGoldBtn);
    }

    // ======================= 初始化 UI =======================

    private void InitUI()
    {
        ClaimAdsBtn.GetComponentInChildren<Text>().text = "免费";
        adsloading.gameObject.SetActive(false);
        adsIcon.gameObject.SetActive(true);

        shopDataItem = null;

        switch (limitRewordType)
        {
            case LimitRewordType.Coins:
                // if(max)
                //     AwardIcon.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("Coin2");
                // AwardIcon.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("Coin1");
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
                break;

            case LimitRewordType.AutoComplete: // 火箭道具
                UpdateCliamBtn(false);
                title.text = MultilingualManager.Instance.GetString("ItemName02", "pingzi");
                tips.text = MultilingualManager.Instance.GetString("ItemDes02", "pingzi");
                ClaimGoldBtn.GetComponentInChildren<Text>().text = GameDataManager.Instance.UserData.toolInfo[104].cost.ToString();
                AwardIcon.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("rocket");
                eventDes = title.text + "弹窗广告";
                break;
        }

        // 刷新礼包表（提示灯 3 个，其他 1 个）
        RefreshGiftTables();
    }

    // ======================= 礼包表生成 =======================

    /// <summary>
    /// 根据当前 limitRewordType 返回需要展示的礼包数量：
    ///  - 提示灯道具（Tipstool）：固定 3 个；
    ///  - 其他道具：固定 1 个。
    /// </summary>
    private int GetGiftTableCountForType(LimitRewordType type)
    {
        switch (type)
        {
            case LimitRewordType.Tipstool: // 提示灯
                return 3;
            default:
                return 1;
        }
    }

    /// <summary>
    /// 根据当前 limitRewordType 从 ShopManager 获取推荐位礼包列表。
    ///  - 提示灯（Tipstool）→ RecommendSlot.Bulb（灯泡弹窗一号/二号/三号位）；
    ///  - 火箭（AutoComplete）→ RecommendSlot.Rocket（火箭弹窗）。
    /// 如后续其他道具需要走别的推荐位（Clock / DynamicStore），在此处扩展即可。
    /// </summary>
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
    /// 清空并重新生成礼包表。
    /// 数量规则：提示灯（Tipstool）3 个礼包；其他道具 1 个礼包。
    /// 每个礼包用 GiftTable Prefab 在 scrollRect.content 下实例化。
    /// </summary>
    private void RefreshGiftTables()
    {
        ClearGiftTables();

        if (giftTable == null)
        {
            LogGetItem("GiftTable Prefab 未配置，无法生成礼包表");
            if (scrollRect != null) scrollRect.gameObject.SetActive(false);
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
            return;
        }

        scrollRect.gameObject.SetActive(true);

        // 主礼包兼容字段（取第一个）
        shopDataItem = packs[0];

        for (int i = 0; i < actualCount; i++)
        {
            GiftTable gt = Instantiate(giftTable, scrollRect.content);
            gt.gameObject.SetActive(true);

            ShopDataItem item = packs[i];
            gt.InitUI(limitRewordType, item);
            _giftTableInstances.Add(gt);

            LogGetItem($"生成礼包 [{i}]: {item.produceNameId}(id={item.id}, price={item.price}, giftType={item.giftType})");
        }

        // 根据实际数量刷新翻页布局
        UpdatePageLayout(actualCount);

        // 回到第一页
        ScrollToPage(0, true);
    }

    /// <summary>清理所有已生成的 GiftTable</summary>
    private void ClearGiftTables()
    {
        for (int i = _giftTableInstances.Count - 1; i >= 0; i--)
        {
            if (_giftTableInstances[i] != null)
                Destroy(_giftTableInstances[i].gameObject);
        }
        _giftTableInstances.Clear();
    }

    /// <summary>
    /// 根据礼包数量重新计算翻页位置。
    /// 归一化位置：pageCount=1 → [0]；pageCount=N → [0, 1/(N-1), ..., 1]
    /// </summary>
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

    // ======================= 翻页逻辑（核心修复） =======================

    private void Update()
    {
        HandleAutoSnapping();
    }

    /// <summary>获取当前最近页面的索引</summary>
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

        // ============ 1. 检测拖动状态变化 ============
        bool isDraggingNow = Mathf.Abs(scrollRect.velocity.x) > stopVelocityThreshold;

        // 拖动开始 → 打断正在进行的吸附
        if (!wasDragging && isDraggingNow)
        {
            isSnapping = false; // ★ 关键：打断吸附，避免与用户拖动打架
            dragStartPosition = scrollRect.horizontalNormalizedPosition;
            isDragStartRecorded = true;
            currentPageIndex = GetCurrentPageIndex();
        }

        // 拖动结束 → 判断方向，决定吸附目标页
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
        }

        wasDragging = isDraggingNow;

        // ============ 2. 吸附动画：基于时间的插值（匀速/可控时长） ============
        if (isSnapping)
        {
            snapTimer += Time.deltaTime;

            float t = snapDuration <= 0f
                ? 1f
                : Mathf.Clamp01(snapTimer / snapDuration);

            // ★ 匀速 & 平滑：
            //   snapSmoothness = 0 → 纯匀速（easedT = t）
            //   snapSmoothness = 1 → 强烈缓入缓出（SmoothStep 满值）
            //   中间值按比例混插，兼顾"匀速感"与"自然度"
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            float easedT = Mathf.Lerp(t, smoothT, snapSmoothness);

            scrollRect.horizontalNormalizedPosition = Mathf.Lerp(snapStartPos, targetPosition, easedT);

            if (t >= 1f)
            {
                // 到达目标位置，结束吸附
                scrollRect.horizontalNormalizedPosition = targetPosition;
                isSnapping = false;
                currentPageIndex = GetCurrentPageIndex();
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

    /// <summary>
    /// 触发翻页吸附。
    /// </summary>
    /// <param name="pageIndex">目标页</param>
    /// <param name="instant">是否立即跳转（打开面板初始化第一页时用 true）</param>
    public void ScrollToPage(int pageIndex, bool instant = false)
    {
        if (pagePositions == null || pageIndex < 0 || pageIndex >= pageCount) return;

        targetPosition = pagePositions[pageIndex];
        currentPageIndex = pageIndex;

        if (instant || snapDuration <= 0f)
        {
            // 立即到位（打开面板时使用，避免第一页有滑入动画）
            scrollRect.horizontalNormalizedPosition = targetPosition;
            isSnapping = false;
            snapTimer = 0f;
            snapStartPos = targetPosition;
            return;
        }

        // 记录吸附起点，重置计时器
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
            //MessageSystem.Instance.ShowTip("TipGoldInsufficient");
            // 直接跳转到商店
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
            AnalyticMgr.VideoAdSuccess(eventDes);

            //EventDispatcher.instance.TriggerChangeGoldUI(0, false);
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
        base.Close(); // 隐藏面板
    }

    public override void OnHideAnimationEnd()
    {
        base.OnHideAnimationEnd();
    }
}