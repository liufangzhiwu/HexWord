using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
/// 动态商店与固定商店共用：ShopItem 预制体、对象池、GiftItem、购买流程、
/// 去广告状态、无限体力、购买记录/恢复购买/客服、埋点。
/// </summary>
public class ShopScreen : UIWindow
{


    // ======================= 固定商店序列化字段 =======================

    [Header("固定商店 - 基础")]
    [SerializeField] private Button closeBtn;           // 关闭按钮
    [SerializeField] private Button adsbtn;             // （旧）广告按钮
    [SerializeField] private Image CoinIcon;
    [SerializeField] private Image TipIcon;
    [SerializeField] private Image AutoIcon;
    [SerializeField] private Image ButteryIcon;
    [SerializeField] private Text HeaderText;
    [SerializeField] private Text GoldText;
    [SerializeField] private Text TipText;
    [SerializeField] private Text AutoText;
    [SerializeField] private Text ButteryText;
    [SerializeField] private ShopItem ShopGiftItemPrefab;
    [SerializeField] private ShopItem ShopItemPrefab;
    [SerializeField] private ShopItem ShopAdsItemPrefab;
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

    // 固定商店商品（保持原有字段名，兼容外部引用）
    Dictionary<int, ShopItem> shopallItems = new Dictionary<int, ShopItem>();
    List<ShopDataItem> shopallDataItems = new List<ShopDataItem>();

    // 动态商店商品
    private readonly Dictionary<int, ShopItem> _dynamicItems = new Dictionary<int, ShopItem>();
    private readonly List<ShopItem> _dynamicActiveItems = new List<ShopItem>();
    private bool _isExpanded = false;

    // ======================= 生命周期 =======================

    protected void Start()
    {
        // 加载预制体（若 Inspector 未赋值，则从 Bundle 加载）
        if (ShopItemPrefab == null)
        {
            ShopItemPrefab = AdvancedBundleLoader.SharedInstance
                .LoadGameObject("commonitem", "ShopItem").GetComponent<ShopItem>();
        }

        if (ShopGiftItemPrefab == null)
        {
            ShopGiftItemPrefab = AdvancedBundleLoader.SharedInstance
                .LoadGameObject("commonitem", "ShopGiftItem").GetComponent<ShopItem>();
        }

        if (ShopAdsItemPrefab == null)
        {
            ShopAdsItemPrefab = AdvancedBundleLoader.SharedInstance
                .LoadGameObject("commonitem", "ShopAdsItem").GetComponent<ShopItem>();
        }

        // 初始化对象池
        objectPool = new ObjectPool(ShopItemPrefab.gameObject,
            ObjectPool.CreatePoolContainer(transform, "ShopItemPool"));
        giftobjectPool = new ObjectPool(ShopGiftItemPrefab.gameObject,
            ObjectPool.CreatePoolContainer(transform, "ShopGiftItemPool"));
        adsObjectPool = new ObjectPool(ShopAdsItemPrefab.gameObject,
            ObjectPool.CreatePoolContainer(transform, "ShopAdsItemPool"));

        // 固定商店：Start 阶段直接创建商品列表
        shopallDataItems = ShopManager.shopManager.GetShopItems();
        if (ShopManager.shopManager.shopMode == ShopMode.FixedStore)
            CrateShopItem(shopallDataItems);
       

        CustomFlyInManager.Instance.ShopAutoObj = AutoIcon.gameObject;
        CustomFlyInManager.Instance.ShopGoldObj = CoinIcon.gameObject;
        CustomFlyInManager.Instance.ShopTipObj = TipIcon.gameObject;
        CustomFlyInManager.Instance.ShopButterflyObj = ButteryIcon.gameObject;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        AudioManager.Instance.PlaySoundEffect("ShowUI");
        InitUI();
        EventDispatcher.instance.OnChangeGoldUI += InitUI;

        HeaderText.text = MultilingualManager.Instance.GetString("Shop");
        ShopManager.shopManager.paysuccess = false;

        if (adsbtn != null) adsbtn.gameObject.SetActive(false);

        BuyRemoveAdsEvent();
        CustomFlyInManager.Instance.GoldObj = CoinIcon.gameObject;

        GameDataManager.Instance.UserData.CheckShopBuyData();
        GameDataManager.Instance.UserData.isHideShopRedPoint = true;
        GameDataManager.Instance.UserData.isShowDiscountGift = false;

        EventDispatcher.instance.TriggerUpdateLayerCoin(false, true);

        if (ShopManager.shopManager.shopMode == ShopMode.DynamicStore)
        {
            OnPanelClosed();
            ShowCollapsedState();
        }
        else
        {
            CrateShopItem(shopallDataItems);
        }
        
        moreChoiceBtn.gameObject.SetActive(ShopManager.shopManager.shopMode == ShopMode.DynamicStore);
        shopScrollView.enabled =ShopManager.shopManager.shopMode != ShopMode.DynamicStore;
        
        // 进入商店消费入口角标（需求：进店消失）
        ShopManager.shopManager.OnShopEntered();
    }

    protected override void OnDisable()
    {
        EventDispatcher.instance.OnChangeGoldUI -= InitUI;

        ShowBanner();

        if (SystemManager.Instance.PanelIsShowing(PanelType.ChessPlayArea) ||
            SystemManager.Instance.PanelIsShowing(PanelType.HexGamePlayArea))
        {
            EventDispatcher.instance.TriggerUpdateLayerCoin(false, true, false);
        }
    }

    protected override void InitializeUIComponents()
    {
        if (closeBtn != null) closeBtn.AddVibraClickAction(OnCloseBtn);

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

    // ======================= 固定商店：商品创建 =======================

    private void CrateShopItem(List<ShopDataItem> shopDataItems)
    {
        var targetDict = shopallItems;

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

                shopItem = pool.GetObject<ShopItem>(itemparent);
                shopItem.SetShopData(shopDataItem);
                shopItem.transform.SetSiblingIndex(i);
                targetDict.TryAdd(shopDataItem.id, shopItem);

                if (!shopallItems.ContainsKey(shopDataItem.id))
                {
                    shopallItems.TryAdd(shopDataItem.id, shopItem);
                }
            }
        }

        if (freeItemTable != null) freeItemTable.transform.SetAsLastSibling();
        if (goldItemTable != null) goldItemTable.transform.SetAsLastSibling();
        if (goldItemTable2 != null) goldItemTable2.transform.SetAsLastSibling();
    }

    // ======================= 动态商店：收起 / 展开 =======================

    /// <summary>
    /// 收起状态：一号推荐位 + 二号推荐位 + 金币档 + “更多选择”
    /// </summary>
    private void ShowCollapsedState()
    {
        if (ShopManager.shopManager.shopMode != ShopMode.DynamicStore) return;

        _isExpanded = false;

        ClearDynamicItems();

        // 1. 动态商店收起状态商品（内部处理一号位兜底：去广告/二号位前2权重）
        List<ShopDataItem> collapsedItems = ShopManager.shopManager.GetDynamicStoreCollapsedItems();

        // 2. 按顺序填充一号位、二号位
        int slotIndex = 0;
        for (int i = 0; i < collapsedItems.Count; i++)
        {
            ShopDataItem data = collapsedItems[i];
            if (data == null) continue;

            // 金币档：以 Gold 开头且非礼包
            bool isGold = data.produceNameId != null
                && data.produceNameId.StartsWith("Gold")
                && data.type != 2;
         
           
            CreateDynamicItem(data, parent, -1);
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
                Transform transform= goldItemTable.transform;
                
                if (gold.id >= 5 && gold.type == 0)
                {
                    transform = goldItemTable2.transform;
                }
                
                CreateDynamicItem(gold, transform, -1);
            }
        }
    }

    /// <summary>
    /// 展开“更多选择”：所有礼包 + 去广告 + 免费金币 + 金币商品 + 单一道具 + 六个金币，按价格从低到高
    /// </summary>
    private void ExpandMoreChoices()
    {
        if (ShopManager.shopManager.shopMode != ShopMode.DynamicStore) return;

        _isExpanded = true;

        ClearDynamicItems();

        List<ShopDataItem> expandedItems = ShopManager.shopManager.GetDynamicStoreExpandedItems();
        if (expandedItems == null) expandedItems = new List<ShopDataItem>();

        // // 去广告：若未购买且未在列表中，追加
        // ShopDataItem removeAds = ShopManager.shopManager.GetProduct(ShopManager.ProductRemoveAds);
        // if (removeAds != null
        //     && !ShopManager.shopManager.IsRemoveAdsOwned()
        //     && !expandedItems.Contains(removeAds))
        // {
        //     expandedItems.Add(removeAds);
        // }
        //
        // // 按价格升序、去重
        // expandedItems = expandedItems
        //     .Where(x => x != null)
        //     .Distinct()
        //     .OrderBy(x => x.price)
        //     .ToList();
        //
        // for (int i = 0; i < expandedItems.Count; i++)
        // {
        //     CreateDynamicItem(expandedItems[i], parent, i);
        // }
        
        CrateShopItem(shopallDataItems);
        
        moreChoiceBtn.gameObject.SetActive(false);
        shopScrollView.enabled =true;
    }

    private void CreateDynamicItem(ShopDataItem data, Transform parent, int siblingIndex)
    {
        if (data == null || parent == null) return;

        ObjectPool pool = objectPool;
        if (data.type == 2) pool = giftobjectPool;
        else if (data.type == 1) pool = adsObjectPool;

        ShopItem item = pool.GetObject<ShopItem>(parent);
        item.SetShopData(data);

        if (siblingIndex >= 0) item.transform.SetSiblingIndex(siblingIndex);
        item.gameObject.SetActive(true);

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

        // 固定商店：原有逻辑
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

    private void OnAdsBtn()
    {
        // 原逻辑保留，如未来需要接入恢复购买可在此扩展
        // ShopManager.shopManager.iapManager.UserInitiatedRestore(true);
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
            item.Value.gameObject.SetActive(false);
        }

        ClearDynamicItems();
    }
}