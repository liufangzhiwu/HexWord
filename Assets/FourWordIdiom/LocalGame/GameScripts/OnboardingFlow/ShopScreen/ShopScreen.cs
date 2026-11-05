using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;

/// <summary>
/// 商店界面（固定商店页 + 动态商店弹窗）
///
/// 使用方式：
/// - 固定商店：金币栏进入，ShopMode = FixedStore，直接展开全部商品，按 sort 排序；
/// - 动态商店：灯泡/火箭/复活金币不足进入，ShopMode = DynamicStore，默认收起，
///            展示一号/二号推荐位 + 金币档，点“更多选择”按价格从低到高展开全部内购。
///
/// 商品统一使用 ShopItemBase 及派生类：
/// - ShopItem：单物品（含广告逻辑）
/// - ShopGiftItem：礼包（无广告）
///
/// 顶部资源栏展示：金币 / 提示灯 / 火箭 / 蝴蝶 / 复活道具（105）
/// </summary>
public class ShopScreen : UIWindow
{
    // ======================= 固定商店序列化字段 =======================

    [Header("固定商店 - 基础")]
    [SerializeField] private Button closeBtn;           // 关闭按钮
    [SerializeField] private Button restartbtn;         // 复活道具按钮（展示数量，点击可查看说明/跳转）

    [Header("顶部资源栏图标")]
    [SerializeField] private Image CoinIcon;
    [SerializeField] private Image TipIcon;
    [SerializeField] private Image AutoIcon;
    [SerializeField] private Image ButteryIcon;
    [SerializeField] private Image RestartIcon;         // ★ 复活道具图标

    [Header("顶部资源栏文本")]
    [SerializeField] private Text HeaderText;
    [SerializeField] private Text GoldText;
    [SerializeField] private Text TipText;
    [SerializeField] private Text AutoText;
    [SerializeField] private Text ButteryText;
    [SerializeField] private Text RestartText;          // ★ 复活道具数量

    [Header("商品 Prefab")]
    [SerializeField] private ShopItemBase ShopGiftItemPrefab;   // 礼包 Prefab（挂 ShopGiftItem）
    [SerializeField] private ShopItemBase ShopItemPrefab;       // 单物品 Prefab（挂 ShopItem）
    [SerializeField] private ShopItemBase ShopAdsItemPrefab;    // 广告类 Prefab（挂 ShopItem）

    [SerializeField] private Transform parent;
    [SerializeField] private ScrollRect shopScrollView;
    [SerializeField] private FreeItemTable freeItemTable;
    [SerializeField] private GameObject goldItemTable;
    [SerializeField] private GameObject goldItemTable2;

    // ======================= 动态商店序列化字段 =======================

    [SerializeField] private Button moreChoiceBtn;         // “更多选择”按钮

    [Header("动态商店 - 底部功能栏")]
    [SerializeField] private Button purchaseRecordBtn;     // 购买记录
    [SerializeField] private Button restoreBtn;            // 恢复购买
    [SerializeField] private Button contactBtn;            // 联系客服

    // ======================= 对象池 =======================

    private ObjectPool objectPool;      // 普通商品
    private ObjectPool giftobjectPool;  // 礼包
    private ObjectPool adsObjectPool;   // 广告/去广告类

    // ======================= 商品缓存 =======================

    // 固定商店 + 动态商店共用商品缓存（统一用 ShopItemBase）
    Dictionary<int, ShopItemBase> shopallItems = new Dictionary<int, ShopItemBase>();
    List<ShopDataItem> shopallDataItems = new List<ShopDataItem>();

    // 动态商店商品
    private readonly Dictionary<int, ShopItemBase> _dynamicItems = new Dictionary<int, ShopItemBase>();
    private readonly List<ShopItemBase> _dynamicActiveItems = new List<ShopItemBase>();
    private bool _isExpanded = false;

    // ======================= 生命周期 =======================

    protected void Start()
    {
        InitPool();

        // 固定商店：Start 阶段直接创建商品列表
        shopallDataItems = ShopManager.shopManager.GetShopItems();
        if (ShopManager.shopManager.shopMode == ShopMode.FixedStore)
            ShowFixedStore();

        // ★ 顶部资源飞入目标（金币 / 提示灯 / 火箭 / 蝴蝶 / 复活道具）
        CustomFlyInManager.Instance.ShopAutoObj = AutoIcon.gameObject;
        CustomFlyInManager.Instance.ShopGoldObj = CoinIcon.gameObject;
        CustomFlyInManager.Instance.ShopTipObj = TipIcon.gameObject;
        CustomFlyInManager.Instance.ShopButterflyObj = ButteryIcon.gameObject;
        CustomFlyInManager.Instance.ShopRestartObj = RestartIcon != null ? RestartIcon.gameObject : null;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        AudioManager.Instance.PlaySoundEffect("ShowUI");
        InitPool();
        InitUI();
        EventDispatcher.instance.OnChangeGoldUI += InitUI;

        // ★ 资源栏背景渐变：订阅奖励面板领取/关闭事件 + 复位默认透明度 120
        EventDispatcher.instance.OnAwardClaimed += OnAwardClaimed;
        EventDispatcher.instance.OnAwardScreenClosed += OnAwardScreenClosed;
        SetResourceBarBackgroundAlpha(ResourceBarBgDefaultAlpha, 0f);

        HeaderText.text = MultilingualManager.Instance.GetString("Shop");
        ShopManager.shopManager.paysuccess = false;

        BuyRemoveAdsEvent();
        CustomFlyInManager.Instance.GoldObj = CoinIcon.gameObject;

        GameDataManager.Instance.UserData.CheckShopBuyData();
        GameDataManager.Instance.UserData.isHideShopRedPoint = true;
        GameDataManager.Instance.UserData.isShowDiscountGift = false;
        
        EventDispatcher.instance.TriggerUpdateLayerCoin(false, true, false);

        if (ShopManager.shopManager.shopMode == ShopMode.DynamicStore)
        {
            OnPanelClosed();
            ShowCollapsedState();

            freeItemTable.gameObject.SetActive(false);
        }
        else
        {
            ShowFixedStore();
            freeItemTable.gameObject.SetActive(true);
        }

        moreChoiceBtn.gameObject.SetActive(ShopManager.shopManager.shopMode == ShopMode.DynamicStore);
        shopScrollView.enabled = ShopManager.shopManager.shopMode != ShopMode.DynamicStore;

        // ★ 复活道具按钮始终显示（展示数量 + 点击可查看说明）
        if (restartbtn != null)
        {
            restartbtn.gameObject.SetActive(true);
        }

        // 进入商店消费入口角标（需求：进店消失）
        ShopManager.shopManager.OnShopEntered();

        // 重置滚动位置到顶部
        shopScrollView.normalizedPosition = new Vector2(0, 1);
    }

    protected override void OnDisable()
    {
        EventDispatcher.instance.OnChangeGoldUI -= InitUI;
        EventDispatcher.instance.OnAwardClaimed -= OnAwardClaimed;
        EventDispatcher.instance.OnAwardScreenClosed -= OnAwardScreenClosed;

        ShowBanner();

        if (SystemManager.Instance.PanelIsShowing(PanelType.ChessPlayArea) ||
            SystemManager.Instance.PanelIsShowing(PanelType.HexGamePlayArea))
        {

            if (ShopManager.shopManager.shopMode == ShopMode.DynamicStore)
            {
                EventDispatcher.instance.TriggerUpdateLayerCoin(true, true,false,true);
            }
            else
            {
                EventDispatcher.instance.TriggerUpdateLayerCoin(false, true, false);
            }
        }
    }

    private void InitPool()
    {
        
        // 加载预制体（若 Inspector 未赋值，则从 Bundle 加载）
        if (ShopItemPrefab == null)
        {
            ShopItemPrefab = AdvancedBundleLoader.SharedInstance
                .LoadGameObject("commonitem", "ShopItem").GetComponent<ShopItemBase>();
        }

        if (ShopGiftItemPrefab == null)
        {
            ShopGiftItemPrefab = AdvancedBundleLoader.SharedInstance
                .LoadGameObject("commonitem", "ShopGiftItem").GetComponent<ShopItemBase>();
        }

        if (ShopAdsItemPrefab == null)
        {
            ShopAdsItemPrefab = AdvancedBundleLoader.SharedInstance
                .LoadGameObject("commonitem", "ShopAdsItem").GetComponent<ShopItemBase>();
        }
        
        // 初始化对象池
        if (objectPool == null && ShopItemPrefab != null)
        {
            objectPool = new ObjectPool(ShopItemPrefab.gameObject,
                ObjectPool.CreatePoolContainer(transform, "ShopItemPool"), 6);
        }

        if (giftobjectPool == null && ShopGiftItemPrefab != null)
        {
            giftobjectPool = new ObjectPool(ShopGiftItemPrefab.gameObject,
                ObjectPool.CreatePoolContainer(transform, "ShopGiftItemPool"));
        }

        if (adsObjectPool == null && ShopAdsItemPrefab != null)
        {
            adsObjectPool = new ObjectPool(ShopAdsItemPrefab.gameObject,
                ObjectPool.CreatePoolContainer(transform, "ShopAdsItemPool"));
        }
    }

    protected override void InitializeUIComponents()
    {
        if (closeBtn != null) closeBtn.AddVibraClickAction(OnCloseBtn);

        // ★ 复活道具按钮（替代原广告按钮）
        if (restartbtn != null) restartbtn.AddVibraClickAction(OnRestartBtn);

        // 动态商店按钮
        if (moreChoiceBtn != null) moreChoiceBtn.AddVibraClickAction(ExpandMoreChoices);
        if (purchaseRecordBtn != null) purchaseRecordBtn.AddVibraClickAction(OnPurchaseRecordBtn);
        if (restoreBtn != null) restoreBtn.AddVibraClickAction(OnRestoreBtn);
        if (contactBtn != null) contactBtn.AddVibraClickAction(OnContactBtn);
    }

    // ======================= 顶部UI刷新 =======================

    private void InitUI(int value = 0, bool isanim = false)
    {
        if (value > 0 && isanim)
        {
            StartCoroutine(AnimateCoinAddition(value));
        }
        else
        {
            GoldText.text = GameDataManager.Instance.UserData.Gold.ToString();
        }

        AutoText.text = GameDataManager.Instance.UserData.toolInfo[104].count.ToString();
        ButteryText.text = GameDataManager.Instance.UserData.toolInfo[103].count.ToString();
        TipText.text = GameDataManager.Instance.UserData.toolInfo[102].count.ToString();

        // ★ 复活道具数量（105），兼容旧存档缺少 105 的情况
        if (RestartText != null)
        {
            int restartCount = 0;
            if (GameDataManager.Instance.UserData.toolInfo.TryGetValue(105, out var restartInfo) && restartInfo != null)
            {
                restartCount = restartInfo.count;
            }
            RestartText.text = restartCount.ToString();
        }
    }

    private IEnumerator AnimateCoinAddition(int amount)
    {
        int startValue = GameDataManager.Instance.UserData.Gold - amount;
        int targetValue = GameDataManager.Instance.UserData.Gold;
        float duration = 0.2f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            int currentValue = Mathf.RoundToInt(Mathf.Lerp(startValue, targetValue, t));
            GoldText.text = currentValue.ToString();
            yield return null;
        }
        GoldText.text = targetValue.ToString();
    }

    // ======================= ★ 资源栏背景渐变 =======================

    /// <summary>
    /// 资源栏（顶部 金币/提示灯/火箭/蝴蝶/复活道具）每个条目的背景 Image。
    /// 默认透明度 120/255；AwardScreen 显示时渐显到 255，关闭时渐隐回 120。
    /// 背景取各图标父节点（Coins/TipTool/AutoTool/Butterfly/ReStore）上的 Image。
    /// </summary>
    private readonly List<Image> _resourceBarBackgrounds = new List<Image>();
    private Coroutine _bgFadeCoroutine;

    private const float ResourceBarBgDefaultAlpha = 120f / 255f; // 默认 120
    private const float ResourceBarBgShownAlpha  = 255f / 255f; // 显示奖励面板时 255（完全不透明）
    private const float ResourceBarBgFadeDuration = 0.3f;
    private const int   ResourceBarBgDefaultSortingOrder = 52; // 默认层级
    private const int   ResourceBarBgShownSortingOrder   = 70; // 领取时层级（置顶）

    private Canvas _resourceBarContainerCanvas; // resourses 父对象的 Canvas

    /// <summary>缓存资源栏 5 个条目的背景 Image + resourses 父对象 Canvas。</summary>
    private void CacheResourceBarBackgrounds()
    {
        _resourceBarBackgrounds.Clear();
        var icons = new[] { CoinIcon, TipIcon, AutoIcon, ButteryIcon, RestartIcon };

        // 移除各资源条目上残留的层级 Canvas 组件（如果之前有加过）
        foreach (var icon in icons)
        {
            if (icon == null) continue;
            var itemCanvas = icon.transform.parent.GetComponent<Canvas>();
            if (itemCanvas != null) Destroy(itemCanvas);
        }

        foreach (var icon in icons)
        {
            if (icon == null) continue;
            var bg = icon.transform.parent.GetComponent<Image>();
            if (bg != null) _resourceBarBackgrounds.Add(bg);
        }

        // 获取 resourses 父对象（图标父节点的父节点）的 Canvas
        if (CoinIcon != null)
        {
            var resoursesParent = CoinIcon.transform.parent.parent;
            _resourceBarContainerCanvas = resoursesParent.GetComponent<Canvas>();
            if (_resourceBarContainerCanvas == null)
                _resourceBarContainerCanvas = resoursesParent.gameObject.AddComponent<Canvas>();
            _resourceBarContainerCanvas.overrideSorting = true;
            _resourceBarContainerCanvas.sortingOrder = ResourceBarBgDefaultSortingOrder;
        }
    }

    /// <summary>设置资源栏背景透明度（0~1）。duration&lt;=0 时立即设置。</summary>
    public void SetResourceBarBackgroundAlpha(float targetAlpha, float duration = ResourceBarBgFadeDuration)
    {
        if (_resourceBarBackgrounds.Count == 0) CacheResourceBarBackgrounds();
        if (_resourceBarBackgrounds.Count == 0) return;

        if (_bgFadeCoroutine != null) StopCoroutine(_bgFadeCoroutine);
        _bgFadeCoroutine = StartCoroutine(FadeResourceBarBackgroundCoroutine(targetAlpha, duration));
    }

    private IEnumerator FadeResourceBarBackgroundCoroutine(float targetAlpha, float duration)
    {
        float startAlpha = _resourceBarBackgrounds[0].color.a;
        if (duration <= 0f)
        {
            ApplyResourceBarBgAlpha(targetAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t); // smoothstep 缓动
            ApplyResourceBarBgAlpha(Mathf.Lerp(startAlpha, targetAlpha, eased));
            yield return null;
        }
        ApplyResourceBarBgAlpha(targetAlpha);
    }

    private void ApplyResourceBarBgAlpha(float a)
    {
        foreach (var bg in _resourceBarBackgrounds)
        {
            if (bg == null) continue;
            var c = bg.color;
            c.a = a;
            bg.color = c;
        }
    }

    /// <summary>设置 resourses 父对象 Canvas 层级（sortingOrder）。</summary>
    private void SetResourceBarBgSortingOrder(int sortingOrder)
    {
        if (_resourceBarContainerCanvas == null) return;
        _resourceBarContainerCanvas.overrideSorting = true;
        _resourceBarContainerCanvas.sortingOrder = sortingOrder;
    }

    /// <summary>AwardScreen 领取回调：资源栏背景立即变实色（255）+ 层级改为 70（置顶）。</summary>
    private void OnAwardClaimed()
    {
        SetResourceBarBackgroundAlpha(ResourceBarBgShownAlpha, 0f);
        SetResourceBarBgSortingOrder(ResourceBarBgShownSortingOrder);
    }

    /// <summary>AwardScreen 关闭回调：资源栏背景渐隐回半透明（120）+ 层级改回 52。</summary>
    private void OnAwardScreenClosed()
    {
        SetResourceBarBackgroundAlpha(ResourceBarBgDefaultAlpha, ResourceBarBgFadeDuration);
        SetResourceBarBgSortingOrder(ResourceBarBgDefaultSortingOrder);
    }

    // ======================= 固定商店：商品创建 =======================

    private void CrateShopItem(List<ShopDataItem> shopDataItems)
    {
        var targetDict = shopallItems;
        ShopItemBase adsShopItem = null;

        for (int i = 0; i < shopDataItems.Count; i++)
        {
            ShopDataItem shopDataItem = shopDataItems[i];

            if (shopDataItem.type != 0 && shopDataItem.type != 1 && shopDataItem.type != 2)
                continue;

            var pool = shopDataItem.type == 2 ? giftobjectPool : objectPool;
            Transform itemparent = shopDataItem.type == 2 ? parent : goldItemTable.transform;

            if (shopDataItem.type == 1)
            {
                pool = adsObjectPool;
                itemparent = parent;
            }

            if (targetDict.TryGetValue(shopDataItem.id, out var shopItem))
            {
                if (shopDataItem.type == 1)
                {
                    adsShopItem = shopItem;
                }

                shopItem.transform.SetSiblingIndex(i);
                shopItem.gameObject.SetActive(true);
                shopItem.UpdateUI();
            }
            else
            {
                if (shopDataItem.id >= 5 && shopDataItem.type == 0)
                {
                    itemparent = goldItemTable2.transform;
                }

                shopItem = pool.GetObject<ShopItemBase>(itemparent);
                shopItem.SetShopData(shopDataItem);

                if (shopDataItem.type == 1)
                {
                    adsShopItem = shopItem;
                }
                shopItem.transform.SetSiblingIndex(i);

                targetDict.TryAdd(shopDataItem.id, shopItem);

                if (!shopallItems.ContainsKey(shopDataItem.id))
                {
                    shopallItems.TryAdd(shopDataItem.id, shopItem);
                }
            }
        }

        if (adsShopItem != null) adsShopItem.transform.SetAsLastSibling();
        if (freeItemTable != null) freeItemTable.transform.SetAsLastSibling();
        if (goldItemTable != null) goldItemTable.transform.SetAsLastSibling();
        if (goldItemTable2 != null) goldItemTable2.transform.SetAsLastSibling();
    }

    /// <summary>固定商店：展开全部商品 + 一号位推荐</summary>
    private void ShowFixedStore()
    {
        if (ShopManager.shopManager.shopMode != ShopMode.FixedStore) return;

        // 1. 固定商店收起状态商品（内部处理一号位兜底）
        List<ShopDataItem> collapsedItems = ShopManager.shopManager.GetFixedStoreExpandedItems();

        // 2. 按列表顺序填充一号位（siblingIndex = i，保证列表第 0 个显示在最前，如迎新礼包）
        for (int i = 0; i < collapsedItems.Count; i++)
        {
            ShopDataItem data = collapsedItems[i];
            if (data == null) continue;
            CreateDynamicItem(data, parent, i);
        }

        CrateShopItem(shopallDataItems);
    }

    /// <summary>刷新固定商店：购买后重新排版（已购买的迎新礼包不再展示）。</summary>
    public void RefreshFixedStore()
    {
        if (ShopManager.shopManager == null) return;
        if (ShopManager.shopManager.shopMode != ShopMode.FixedStore) return;
        ShowFixedStore();
    }

    // ======================= 动态商店：收起 / 展开 =======================

    /// <summary>收起状态：一号推荐位 + 二号推荐位 + 金币档 + “更多选择”</summary>
    private void ShowCollapsedState()
    {
        if (ShopManager.shopManager.shopMode != ShopMode.DynamicStore) return;

        _isExpanded = false;

        // 1. 动态商店收起状态商品（内部处理一号位兜底：去广告/二号位前2权重）
        List<ShopDataItem> collapsedItems = ShopManager.shopManager.GetDynamicStoreCollapsedItems();

        // 2. 按顺序填充一号位、二号位
        int slotIndex = 0;
        for (int i = 0; i < collapsedItems.Count; i++)
        {
            ShopDataItem data = collapsedItems[i];
            if (data == null) continue;
            slotIndex = i;
            CreateDynamicItem(data, parent, slotIndex);
        }

        // 3. 兜底：即使推荐位为空，也展示配置中的金币档
        List<ShopDataItem> goldItems = ShopManager.shopManager.GetShopItems()
            .Where(x => x.produceNameId != null
                && x.produceNameId.StartsWith("Gold")
                && x.type != 2)
            .OrderBy(x => x.price)
            .ToList();

        foreach (var gold in goldItems)
        {
            if (gold == null) continue;
            if (!_dynamicItems.ContainsKey(gold.id))
            {
                Transform transform = goldItemTable.transform;

                if (gold.id >= 5 && gold.type == 0)
                {
                    if (slotIndex >= 1) break;

                    transform = goldItemTable2.transform;
                }

                CreateDynamicItem(gold, transform, -1);
            }
        }
    }

    /// <summary>展开“更多选择”：所有礼包 + 去广告 + 免费金币 + 金币商品 + 单一道具 + 六个金币，按价格从低到高</summary>
    private void ExpandMoreChoices()
    {
        if (ShopManager.shopManager.shopMode != ShopMode.DynamicStore) return;

        _isExpanded = true;

        // 1. 动态商店收起状态商品 + 全部商店商品
        List<ShopDataItem> collapsedItems = ShopManager.shopManager.GetDynamicStoreCollapsedItems();
        List<ShopDataItem> allDataItems = new List<ShopDataItem>();
        List<ShopDataItem> allTempDataItems = new List<ShopDataItem>();
        
        if (AuditConfig.IsAuditOpen)
        {
            allTempDataItems.AddRange(shopallDataItems.FindAll(item => 
                item.produceNameId != ShopManager.shopManager.ProductSpecialPackageA&&item.produceNameId != ShopManager.shopManager.ProductSpecialPackageB));
        }
        else
        {
            allTempDataItems.AddRange(shopallDataItems);
        }
        
        allDataItems.AddRange(allTempDataItems);
        allDataItems.AddRange(collapsedItems);

        // 按价格升序、去重
        allDataItems = allDataItems
            .Distinct()
            .OrderBy(x => x.price)
            .ToList();

        CrateShopItem(allDataItems);

        freeItemTable.gameObject.SetActive(true);
        moreChoiceBtn.gameObject.SetActive(false);
        shopScrollView.enabled = true;
    }

    private void CreateDynamicItem(ShopDataItem data, Transform parent, int siblingIndex)
    {
        if (data == null || parent == null) return;
        var targetDict = shopallItems;

        if (targetDict.TryGetValue(data.id, out var shopItem))
        {
            if (shopItem != null)
            {
                if (siblingIndex >= 0) shopItem.transform.SetSiblingIndex(siblingIndex);
                shopItem.gameObject.SetActive(true);
                return;
            }
        }

        ObjectPool pool = objectPool;
        if (data.type == 2) pool = giftobjectPool;
        else if (data.type == 1) pool = adsObjectPool;

        ShopItemBase item = pool.GetObject<ShopItemBase>(parent);
        item.SetShopData(data);

        if (siblingIndex >= 0) item.transform.SetSiblingIndex(siblingIndex);
        item.gameObject.SetActive(true);

        targetDict.TryAdd(data.id, item);

        if (!_dynamicItems.ContainsKey(data.id))
            _dynamicItems.Add(data.id, item);

        _dynamicActiveItems.Add(item);
    }

    private void ClearDynamicItems()
    {
        for (int i = 0; i < _dynamicActiveItems.Count; i++)
        {
            var item = _dynamicActiveItems[i];
            if (item != null) item.gameObject.SetActive(false);
        }
        _dynamicActiveItems.Clear();
        _dynamicItems.Clear();
    }

    // ======================= ★ 去广告礼包购买后全量刷新 =======================

    /// <summary>
    /// 刷新所有包含去广告内容的礼包。
    /// 去广告礼包购买完成后调用：其他礼包里的「去掉广告」项应一并隐藏。
    /// </summary>
    public void RefreshAllRemoveAdsGiftPacks()
    {
        foreach (var kv in shopallItems)
        {
            var gift = kv.Value as ShopGiftItem;
            if (gift == null) continue;
            if (gift.ContainsRemoveAdsContent())
            {
                gift.RefreshGiftContentAfterPurchase();
            }
        }
    }

    // ======================= 复活道具按钮 =======================

    /// <summary>
    /// 复活道具按钮点击：
    ///  - 展示复活道具数量（右上角按钮已显示数量）；
    ///  - 可打开道具说明/购买界面，目前复用道具弹窗说明。
    /// </summary>
    private void OnRestartBtn()
    {
        int restartCount = 0;
        if (GameDataManager.Instance.UserData.toolInfo.TryGetValue(105, out var restartInfo) && restartInfo != null)
        {
            restartCount = restartInfo.count;
        }

        Debug.Log($"[Shop] 复活道具按钮点击, 当前数量={restartCount}");

        // TODO: 根据产品需求跳转到复活道具说明/购买/GetItemScreen 等
        // 例如：
        // GetItemScreen.limitRewordType = LimitRewordType.ReStartTool;
        // SystemManager.Instance.ShowPanel(PanelType.GetItemScreen);
        MessageSystem.Instance.ShowTip($"复活道具：{restartCount}");
    }

    // ======================= 动态商店：底部按钮 =======================

    private void OnPurchaseRecordBtn()
    {
        SystemManager.Instance.ShowPanel(PanelType.BuyListScreen);
        var records = ShopManager.shopManager.GetPurchaseRecords();
        // TODO: 打开购买记录弹窗（礼包名称、价格、礼包内容、购买时间）
        Debug.Log("[DynamicShop] 购买记录：" + (records != null ? string.Join("\n", records) : "空"));
    }

    private void OnRestoreBtn()
    {
        ShopManager.shopManager.RestorePurchases();
    }

    private void OnContactBtn()
    {
        Application.OpenURL(ConfigManager.Instance.GetString("OpinionUrl"));
    }

    // ======================= 关闭 =======================

    private void OnCloseBtn()
    {
        EventDispatcher.instance.TriggerChangeGoldUI(0, false);

        base.Close();

        if (GameCoreManager.Instance.PanelState == PanelState.MainMenuPanel)
        {
            SystemManager.Instance.ShowPanel(PanelType.PrimaryInterface);
        }
        else if (GameCoreManager.Instance.PanelState == PanelState.FinishHexPanel)
        {
            SystemManager.Instance.ShowPanel(PanelType.StageFinishView);
        }

        SystemManager.Instance.ShowPanel(PanelType.HeaderSection);
    }

    // ======================= 去广告UI回调 =======================

    private async void BuyRemoveAdsEvent()
    {
        await Task.Delay(100);
        // 原注释逻辑保留：根据 limitShopItems 中 adstype 更新广告按钮UI
    }

    // ======================= 广告横幅 =======================

    private void ShowBanner()
    {
        if (GameDataManager.Instance?.UserData.CurrentHexStage >= 7)
        {
            if (SystemManager.Instance.PanelIsShowing(PanelType.HexGamePlayArea))
            {
                // if (StageController.Instance.CurStageInfo.Puzzles.Count <= 9)
                //     AdsManager.Instance.ShowBannerAd();
            }
        }
    }

    // ======================= 面板关闭清理 =======================

    public void OnPanelClosed()
    {
        foreach (var item in shopallItems)
        {
            if (item.Value != null)
                item.Value.gameObject.SetActive(false);
        }

        ClearDynamicItems();
    }
}