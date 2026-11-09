using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通用礼包弹窗（原「限时折扣」界面改造）。
/// 现在承载两类主动弹窗（需求 C-STRAT-08 破冰 / C-STRAT-09 复购升层）：
///   1. 破冰礼包：去广告周卡，引导首次付费，限时 = 本次周末/节假日区间；
///   2. 复购/升层礼包：加量不加价，额外金币 = 取整(金币价值 × 10%)。
///
/// 数据来源：ShopManager.SetPendingGiftPopup（pendingGiftPopupItem / Mode / EndTime）。
/// 原有「限时宝箱倒计时必填」逻辑已移除：仅在存在有效限时截止时才显示倒计时。
/// 购买统一走 Game.self.Shop.Purchase → ShopManager.OnPurchaseSuccess（发奖 + AwardScreen + 购买记录）。
/// </summary>
public class AdsDiscountScreen : UIWindow
{
    [SerializeField] private Button closeBtn;      // 关闭按钮
    [SerializeField] private Text titleText;       // 标题（未在 prefab 绑定时留空即可）
    [SerializeField] private Text timeText;        // 倒计时（无限时时隐藏）
    [SerializeField] private Text priceText;       // 价格
    [SerializeField] private Text discountText;    // 划线原价
    [SerializeField] private Transform parent;     // 礼包内容容器
    [SerializeField] private GiftItem giftItempPefab;
    [SerializeField] private GameObject discountObj;  // 折扣角标
    [SerializeField] private GameObject circle;        // 加载态
    [SerializeField] private Button ClaimBtn;          // 购买按钮

    [Header("礼包图（图集 ShopGifts）")]
    [SerializeField] private Image giftIcon;           // 礼包大图（UIBase/Bg/Icons），未绑定时按路径自动查找
    [SerializeField] private Image giftBgImage;        // 背景图（UIBase/Bg），未绑定时按路径自动查找

    [Header("布局微调")]
    [SerializeField] private RectTransform rewardBg;   // 奖励区（UIBase/RewardBg），按奖励数量改高度
    [SerializeField] private GiftItem giftAddItem;     // 加赠奖励项（UIBase/GiftAddItem），未绑定时按路径自动查找
    [SerializeField] private GiftItem goldItem;     // 奖励资源=7个时，金币资源用这个显示；

    /// <summary>礼包图所在图集名</summary>
    private const string GiftArtAtlas = "ShopGifts";

    /// <summary>加赠奖励项节点路径（GiftItem 预制体实例）</summary>
    private const string GiftAddItemPath = "UIBase/GiftAddItem";

    /// <summary>金币奖励项节点路径（7 个奖励时金币用这个格子显示）</summary>
    private const string GoldItemPath = "UIBase/GoldItem";

    /// <summary>走「7 个奖励」专属排版的奖励数量</summary>
    private const int SevenRewardCount = 7;
    /// <summary>7 个奖励时奖励容器的 x</summary>
    private const float ParentXSevenRewards = 200f;
    /// <summary>7 个奖励时网格的横向间距</summary>
    private const float GridSpacingXSevenRewards = -70f;

    /// <summary>初心礼包（标题描边单独配色）</summary>
    private const string ChuxinPackId = "Box0";
    /// <summary>初心礼包标题描边色：hsla(227, 100%, 33%, 1)</summary>
    private static readonly Color ChuxinTitleOutline = new Color(0f, 0.143f, 0.66f, 1f);

    /// <summary>有折扣时价格文本的 y（相对原位置的上移量）</summary>
    private const float PriceYWithDiscount = -30f;
    /// <summary>无折扣时价格文本的 y</summary>
    private const float PriceYWithoutDiscount = 0f;
    /// <summary>奖励数量 &gt; 3 时奖励区高度</summary>
    private const float RewardBgHeightLarge = 540f;
    /// <summary>奖励数量 ≤ 3 时奖励区高度</summary>
    private const float RewardBgHeightSmall = 280f;
    /// <summary>奖励数量 ≤ 3 时礼包大图的 y</summary>
    private const float GiftIconYSmall = 35f;
    /// <summary>奖励数量 &gt; 3 时礼包大图的 y</summary>
    private const float GiftIconYLarge = 120f;

    /// <summary>Prefab 上的默认礼包图（映射表里没有对应礼包时用它回填）</summary>
    private Sprite _defaultGiftIcon;
    private Sprite _defaultGiftBg;
    private bool _defaultGiftArtCached;

    /// <summary>奖励容器的默认排版（7 个奖励以外的数量用默认值）</summary>
    private bool _defaultRewardLayoutCached;
    private float _defaultParentX;
    private Vector2 _defaultGridSpacing;

    /// <summary>标题的描边组件与默认颜色（换成礼包专属描边色后可还原）</summary>
    private Outline[] _titleOutlines;
    private Color[] _titleOutlineDefaults;

    /// <summary>礼包大图：produceNameId → 图集里的 sprite 名</summary>
    private static readonly Dictionary<string, string> GiftIconSprites = new Dictionary<string, string>
    {
        { "Box0", "chuxingGift" },
        { "Box1", "xiwuGift" },
        { "Box2", "mingxingGift" },
        { "Box3", "dajueGift" },
        { "Box4", "yuanmanGift" },
        { "NewcomerBox", "newgift" },
        { "SpecialPackageA", "tehuiGiftA" },
        { "SpecialPackageB", "tehuiGiftB" },
        { "RemoveAdsWeek", "removeAds" },
        { "RemoveADS", "removeAds" },
    };

    /// <summary>礼包背景图：produceNameId → 图集里的 sprite 名（按价格档位分组）</summary>
    private static readonly Dictionary<string, string> GiftBgSprites = new Dictionary<string, string>
    {
        // 低价档
        { "Box0", "lowPricebg" },
        { "Box1", "lowPricebg" },
        { "Box2", "lowPricebg" },
        // 特惠档
        { "NewcomerBox", "tehuibg" },
        { "SpecialPackageA", "tehuibg" },
        { "SpecialPackageB", "tehuibg" },
        // 高价档
        { "Box3", "highPricebg" },
        { "Box4", "highPricebg" },
        // 去广告
        { "RemoveAdsWeek", "removeAdsbg" },
        { "RemoveADS", "removeAdsbg" },
    };

    private ObjectPool objectPool;
    private ShopDataItem currentShopItem;
    private readonly List<GiftItem> GiftItems = new List<GiftItem>();

    private ShopManager.GiftPopupMode _mode = ShopManager.GiftPopupMode.None;
    private DateTime _endTime = DateTime.MinValue;
    private bool _purchasing;

    protected void Start()
    {
        EnsureGiftPool();
    }

    /// <summary>
    /// 创建 GiftItem 对象池。
    /// ★ 首次打开弹窗时 OnEnable 早于 Start，池还是 null → InitGiftItems 直接空引用、
    ///   奖励资源一个都不显示（结算页首次弹这个弹窗的 bug）。所以改成按需创建。
    /// </summary>
    private void EnsureGiftPool()
    {
        if (objectPool != null) return;

        if (giftItempPefab == null)
        {
            GameObject prefabGo = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "DiscountItem");
            if (prefabGo != null) giftItempPefab = prefabGo.GetComponent<GiftItem>();
        }

        if (giftItempPefab == null)
        {
            Debug.LogError("[AdsDiscountScreen] DiscountItem 预制体加载失败，奖励资源无法显示");
            return;
        }

        objectPool = new ObjectPool(giftItempPefab.gameObject, ObjectPool.CreatePoolContainer(transform, "GiftItemPool"));
    }

    protected override void InitializeUIComponents()
    {
        if (closeBtn != null) closeBtn.AddVibraClickAction(OnCloseBtn);
        if (ClaimBtn != null) ClaimBtn.AddClickAction(OnBuyButtonClicked);
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        // 优先取主动弹窗数据（破冰/复购）；无则回退旧的 curshopAdsItem
        if (ShopManager.shopManager != null)
        {
            _mode = ShopManager.shopManager.pendingGiftPopupMode;
            _endTime = ShopManager.shopManager.pendingGiftPopupEndTime;
            currentShopItem = ShopManager.shopManager.pendingGiftPopupItem;

            if (currentShopItem == null && _mode == ShopManager.GiftPopupMode.None)
            {
                currentShopItem = ShopManager.shopManager.curshopAdsItem;
                _mode = ShopManager.GiftPopupMode.None;
                _endTime = DateTime.MinValue;
            }
        }

        if (currentShopItem == null)
        {
            Debug.LogError("[AdsDiscountScreen] 未取到礼包数据（pendingGiftPopupItem 与 curshopAdsItem 均为空），关闭弹窗");
            Close();
            return;
        }

        _purchasing = false;

        InitUI();
        EventDispatcher.instance.TriggerUpdateLayerCoin(true, true, false,true);
        InitGiftItems();
        if (HasCountdown())
            StartCoroutine(UpdateTime());
        AudioManager.Instance.PlaySoundEffect("ShowUI");
    }

    private bool HasCountdown()
    {
        return _endTime != DateTime.MinValue && _endTime > DateTime.Now && timeText != null;
    }

    private void InitUI()
    {
        if (currentShopItem == null) return;

        ApplyGiftArt(currentShopItem.produceNameId);

        if (titleText != null)
            titleText.text = GetTitleText();

        ApplyTitleOutline(currentShopItem.produceNameId);

        bool hasDiscount = !string.IsNullOrEmpty(currentShopItem.discount);
        if (discountObj != null) discountObj.SetActive(hasDiscount);
        if (discountText != null) discountText.gameObject.SetActive(hasDiscount);

        InitPriceText(hasDiscount);
        ApplyPriceTextAnchor(hasDiscount);

        if (timeText != null)
            timeText.gameObject.SetActive(HasCountdown());
    }

    /// <summary>
    /// 按礼包 ID 换掉弹窗的「礼包大图」和「背景图」（都取自图集 ShopGifts）。
    /// 映射表里没有的礼包 / 取图失败时，回填 Prefab 上的默认图，不会留空。
    /// </summary>
    private void ApplyGiftArt(string produceNameId)
    {
        ResolveGiftArtRefs();

        if (giftIcon == null)
            Debug.LogWarning("[AdsDiscountScreen] 没找到礼包大图节点（UIBase/Bg/Icons），跳过换图");
        if (giftBgImage == null)
            Debug.LogWarning("[AdsDiscountScreen] 没找到礼包背景节点（UIBase/Bg），跳过换图");

        string iconName = null;
        string bgName = null;
        if (!string.IsNullOrEmpty(produceNameId))
        {
            GiftIconSprites.TryGetValue(produceNameId, out iconName);
            GiftBgSprites.TryGetValue(produceNameId, out bgName);
        }

        if (giftIcon != null)
        {
            Sprite icon = string.IsNullOrEmpty(iconName)
                ? null
                : AdvancedBundleLoader.SharedInstance.TryGetSpriteFromAtlas(iconName, GiftArtAtlas);
            if (icon == null && !string.IsNullOrEmpty(iconName))
                Debug.LogWarning($"[AdsDiscountScreen] 礼包大图缺失: {iconName} @ {GiftArtAtlas}（{produceNameId}）");
            giftIcon.sprite = icon != null ? icon : _defaultGiftIcon;
        }

        if (giftBgImage != null)
        {
            Sprite bg = string.IsNullOrEmpty(bgName)
                ? null
                : AdvancedBundleLoader.SharedInstance.TryGetSpriteFromAtlas(bgName, GiftArtAtlas);
            if (bg == null && !string.IsNullOrEmpty(bgName))
                Debug.LogWarning($"[AdsDiscountScreen] 礼包背景图缺失: {bgName} @ {GiftArtAtlas}（{produceNameId}）");
            giftBgImage.sprite = bg != null ? bg : _defaultGiftBg;
        }

        Debug.Log($"[AdsDiscountScreen] 礼包图: {produceNameId} → icon={iconName ?? "默认"}, bg={bgName ?? "默认"}");
    }

    /// <summary>
    /// 未在 Prefab 上绑定 Image 时，按固定的节点路径兜底查找：
    ///   礼包大图 UIBase/Bg/Icons，背景图 UIBase/Bg。
    /// 同时把 Prefab 上的原始图记下来，作为没有映射时的默认图。
    /// </summary>
    private void ResolveGiftArtRefs()
    {
        if (giftIcon == null)
        {
            Transform iconNode = transform.Find("UIBase/Bg/Icons");
            if (iconNode != null) giftIcon = iconNode.GetComponent<Image>();
        }

        if (giftBgImage == null)
        {
            Transform bgNode = transform.Find("UIBase/Bg");
            if (bgNode != null) giftBgImage = bgNode.GetComponent<Image>();
        }

        if (!_defaultGiftArtCached && (giftIcon != null || giftBgImage != null))
        {
            _defaultGiftIcon = giftIcon != null ? giftIcon.sprite : null;
            _defaultGiftBg = giftBgImage != null ? giftBgImage.sprite : null;
            _defaultGiftArtCached = true;
        }
    }

    /// <summary>
    /// 标题描边配色：初心礼包（Box0）用 hsla(227,100%,33%)，其它礼包还原 Prefab 上的默认描边。
    /// 标题上有多个 Outline（深色描边 + 白色高光），这里只换深色那几层，白色高光保留。
    /// </summary>
    private void ApplyTitleOutline(string produceNameId)
    {
        if (titleText == null) return;

        if (_titleOutlines == null)
        {
            _titleOutlines = titleText.GetComponents<Outline>();
            _titleOutlineDefaults = new Color[_titleOutlines.Length];
            for (int i = 0; i < _titleOutlines.Length; i++)
                _titleOutlineDefaults[i] = _titleOutlines[i] != null ? _titleOutlines[i].effectColor : Color.clear;
        }

        if (_titleOutlines.Length == 0)
        {
            Debug.LogWarning("[AdsDiscountScreen] 标题上没有 Outline 组件，无法改描边色");
            return;
        }

        bool chuxin = produceNameId == ChuxinPackId;

        for (int i = 0; i < _titleOutlines.Length; i++)
        {
            Outline outline = _titleOutlines[i];
            if (outline == null) continue;

            Color original = _titleOutlineDefaults[i];
            // 白色那层是高光，不参与换色
            bool highlight = original.r > 0.9f && original.g > 0.9f && original.b > 0.9f;

            outline.effectColor = (chuxin && !highlight) ? ChuxinTitleOutline : original;
        }

        if (chuxin)
            Debug.Log("[AdsDiscountScreen] 初心礼包标题描边: hsla(227,100%,33%,1)");
    }

    private string GetTitleText()
    {
        switch (_mode)
        {
            // case ShopManager.GiftPopupMode.BreakIce:
            //     return GetLocalized("BreakIceTitle", "限时特惠");
            // case ShopManager.GiftPopupMode.Repurchase:
            //     return GetLocalized("RepurchaseTitle", "加量不加价");
            default:
                return string.IsNullOrEmpty(currentShopItem.name)
                    ? GetLocalized("GiftTitle", "限时特惠")
                    : currentShopItem.name;
        }
    }

    private void InitPriceText(bool needDiscount)
    {
        if (currentShopItem == null)
        {
            Debug.LogWarning("[AdsDiscountScreen] 当前商店项为空");
            ShowLoadingState(true);
            return;
        }

        try
        {
            float price = currentShopItem.price;
            CultureInfo culture = UIUtilities.GetCultureForCurrency("");

            if (priceText != null)
                priceText.text = UIUtilities.FormatCurrency(price, culture);

            if (needDiscount)
            {
                if (float.TryParse(currentShopItem.discount.TrimEnd('%'), out float discountPercent))
                {
                    discountPercent = Mathf.Abs(discountPercent);
                    decimal discountRate = (decimal)(discountPercent / 100f);
                    decimal originalPrice = (decimal)price / discountRate;
                    if (discountText != null)
                        discountText.text = UIUtilities.FormatCurrency(originalPrice, culture);

                    Text badgeText = discountObj != null ? discountObj.GetComponentInChildren<Text>() : null;
                    if (badgeText != null)
                        badgeText.text = $"{currentShopItem.discount}";
                }
                else
                {
                    Debug.LogWarning($"[AdsDiscountScreen] 折扣格式无效: {currentShopItem.discount}");
                    if (discountText != null) discountText.text = "N/A";
                }
            }

            ShowLoadingState(false);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AdsDiscountScreen] 初始化价格文本出错: {ex.Message}");
            ShowLoadingState(true);
        }
    }

    private void ShowLoadingState(bool isLoading)
    {
        if (circle != null) circle.SetActive(isLoading);
        if (priceText != null) priceText.gameObject.SetActive(!isLoading);
        if (discountText != null && !string.IsNullOrEmpty(currentShopItem.discount))
            discountText.gameObject.SetActive(!isLoading);
    }

    private IEnumerator UpdateTime()
    {
        while (HasCountdown())
        {
            TimeSpan span = _endTime.Subtract(DateTime.Now);
            if (timeText != null)
                timeText.text = UIUtilities.FormatTimeRemaining(span);
            yield return new WaitForSeconds(1f);
        }

        // 限时结束：关闭弹窗并清理数据
        if (timeText != null) timeText.text = "";
        OnCloseBtn();
    }

    private void InitGiftItems()
    {
        if (currentShopItem == null || currentShopItem.productContent == null) return;

        // 首次打开时 Start 还没跑，池可能还没建（否则奖励一个都显示不出来）
        EnsureGiftPool();
        if (objectPool == null) return;

        List<List<string>> contents = currentShopItem.productContent;

        // ★ 奖励 = 7 个时，金币项用单独的 GoldItem 显示，不占网格里的一格
        bool sevenRewards = contents.Count == SevenRewardCount;
        int goldIndex = sevenRewards ? FindCoinsIndex(contents) : -1;
        bool useGoldItem = ApplyGoldItem(contents, goldIndex);
        if (!useGoldItem) goldIndex = -1;

        int shown = 0;
        for (int i = 0; i < contents.Count; i++)
        {
            if (i == goldIndex) continue;   // 金币项交给 GoldItem

            List<string> itemdata = contents[i];
            if (GiftItems.Count > shown)
            {
                GiftItem giftItem = GiftItems[shown];
                giftItem.SetShopData(itemdata, currentShopItem.id, Color.white, currentShopItem.pointDes,
                    applyStyle: false);
                giftItem.gameObject.SetActive(true);
            }
            else
            {
                GiftItem giftItem = objectPool.GetObject<GiftItem>(parent);
                giftItem.SetShopData(itemdata, currentShopItem.id, Color.white, currentShopItem.pointDes,
                    applyStyle: false);
                GiftItems.Add(giftItem);
            }
            shown++;
        }

        // 多余的历史项隐藏（上一次礼包内容更多时）
        for (int i = shown; i < GiftItems.Count; i++)
            GiftItems[i].gameObject.SetActive(false);

        // 奖励区布局：> 3 个奖励要两排（矮高度会裁掉第二排），同时大图往上让位
        ApplyRewardLayout(contents.Count);

        // 7 个奖励时的专属排版（parent x / 网格间距），其它数量恢复 Prefab 默认值
        ApplySevenRewardGridLayout(sevenRewards);

        // 加赠金币单独占一个「加赠」格（不进 productContent，奖励项数量保持礼包原始数量）
        InitGiftAddItem();

        Debug.Log($"[AdsDiscountScreen] 奖励项显示: {contents.Count} 个（网格 {shown} 个" +
                  $"{(useGoldItem ? " + 金币 GoldItem" : "")}）, 礼包={currentShopItem.produceNameId}");
    }

    /// <summary>找金币（type=0）在奖励内容里的下标，没有返回 -1。</summary>
    private static int FindCoinsIndex(List<List<string>> contents)
    {
        if (contents == null) return -1;

        for (int i = 0; i < contents.Count; i++)
        {
            List<string> item = contents[i];
            if (item == null || item.Count < 1) continue;

            int type;
            if (int.TryParse(item[0], out type) && type == (int)LimitRewordType.Coins)
                return i;
        }

        return -1;
    }

    /// <summary>
    /// 金币项用 GoldItem（UIBase/GoldItem）显示；返回是否真的用上了（节点缺失时回退到网格里显示）。
    /// </summary>
    private bool ApplyGoldItem(List<List<string>> contents, int goldIndex)
    {
        if (goldItem == null)
        {
            Transform node = transform.Find(GoldItemPath);
            if (node != null) goldItem = node.GetComponent<GiftItem>();
        }

        bool show = goldIndex >= 0 && goldIndex < contents.Count && goldItem != null;

        if (goldItem != null && goldItem.gameObject.activeSelf != show)
            goldItem.gameObject.SetActive(show);

        if (!show)
        {
            if (goldIndex >= 0 && goldItem == null)
                Debug.LogWarning($"[AdsDiscountScreen] 没找到金币奖励节点（{GoldItemPath}），金币仍在网格里显示");
            return false;
        }

        goldItem.SetShopData(contents[goldIndex], currentShopItem.id, Color.white, string.Empty, applyStyle: false);
        return true;
    }

    /// <summary>
    /// 奖励 = 7 个时的专属排版：
    ///   parent（奖励容器）的 x = 130，GridLayoutGroup 的横向间距 = -70；
    ///   其它数量恢复 Prefab 上的默认值。
    /// </summary>
    private void ApplySevenRewardGridLayout(bool sevenRewards)
    {
        if (parent == null) return;

        RectTransform parentRt = parent as RectTransform;
        GridLayoutGroup grid = parent.GetComponent<GridLayoutGroup>();

        // 首次调用时把 Prefab 上的默认值记下来（此时还没被改过）
        if (!_defaultRewardLayoutCached)
        {
            _defaultParentX = parentRt != null ? parentRt.anchoredPosition.x : 0f;
            _defaultGridSpacing = grid != null ? grid.spacing : Vector2.zero;
            _defaultRewardLayoutCached = true;
        }

        if (parentRt != null)
        {
            Vector2 pos = parentRt.anchoredPosition;
            pos.x = sevenRewards ? ParentXSevenRewards : _defaultParentX;
            parentRt.anchoredPosition = pos;
        }

        if (grid != null)
        {
            grid.spacing = sevenRewards
                ? new Vector2(GridSpacingXSevenRewards, _defaultGridSpacing.y)
                : _defaultGridSpacing;
        }

        // 子物体数量/间距都变了，立刻重排一次，避免第一帧叠在一起
        if (parentRt != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRt);

        Debug.Log($"[AdsDiscountScreen] 7 奖励排版: {(sevenRewards ? "启用" : "恢复默认")}, " +
                  $"parentX={(parentRt != null ? parentRt.anchoredPosition.x : 0)}, " +
                  $"spacing={(grid != null ? grid.spacing.ToString() : "无网格")}");
    }

    /// <summary>
    /// 加赠奖励项（GiftAddItem）：复购/升层优惠款的「加赠 N 金币」显示在这里，
    /// 礼包本身的奖励项数量不变（7 个还是 7 个）。
    /// </summary>
    private void InitGiftAddItem()
    {
        if (giftAddItem == null)
        {
            Transform node = transform.Find(GiftAddItemPath);
            if (node != null) giftAddItem = node.GetComponent<GiftItem>();
        }

        if (giftAddItem == null)
        {
            Debug.LogWarning($"[AdsDiscountScreen] 没找到加赠奖励节点（{GiftAddItemPath}）");
            return;
        }

        int bonusGold = ShopManager.shopManager != null
            ? ShopManager.shopManager.GetPendingGiftBonus(currentShopItem.produceNameId)
            : 0;

        bool show = bonusGold > 0;
        if (giftAddItem.gameObject.activeSelf != show)
            giftAddItem.gameObject.SetActive(show);

        if (!show)
        {
            Debug.Log("[AdsDiscountScreen] 加赠奖励项隐藏（本单没有加赠金币）");
            return;
        }

        giftAddItem.SetShopData(
            new List<string> { ((int)LimitRewordType.Coins).ToString(), bonusGold.ToString() },
            currentShopItem.id, Color.white, string.Empty, applyStyle: false);
        giftAddItem.SetBonus(true);

        Debug.Log($"[AdsDiscountScreen] 加赠奖励项显示: 金币 {bonusGold}, 礼包={currentShopItem.produceNameId}");
    }

    /// <summary>
    /// 价格文本的 y：有折扣（多出一行划线原价）时下移，没有折扣时回到 0。
    /// </summary>
    private void ApplyPriceTextAnchor(bool hasDiscount)
    {
        if (priceText == null) return;

        RectTransform rt = priceText.rectTransform;
        Vector2 pos = rt.anchoredPosition;
        pos.y = hasDiscount ? PriceYWithDiscount : PriceYWithoutDiscount;
        rt.anchoredPosition = pos;
    }

    /// <summary>
    /// 奖励区布局（按奖励数量）：
    ///   ≤ 3 个 → RewardBg / 奖励容器(parent) 高 280、礼包大图 y=35；
    ///   &gt; 3 个 → RewardBg / 奖励容器(parent) 高 540、礼包大图 y=120（两排奖励，大图往上让位）。
    /// </summary>
    private void ApplyRewardLayout(int rewardCount)
    {
        bool large = rewardCount > 3;
        float height = large ? RewardBgHeightLarge : RewardBgHeightSmall;

        if (rewardBg == null)
        {
            Transform node = transform.Find("UIBase/RewardBg");
            if (node != null) rewardBg = node as RectTransform;
        }

        if (rewardBg == null)
        {
            Debug.LogWarning("[AdsDiscountScreen] 没找到奖励区节点（UIBase/RewardBg），跳过高度调整");
        }
        else
        {
            Vector2 size = rewardBg.sizeDelta;
            size.y = height;
            rewardBg.sizeDelta = size;
        }

        // 奖励容器（GridLayoutGroup 的父节点）高度也要跟着变，否则两排奖励会溢出容器
        RectTransform parentRt = parent as RectTransform;
        if (parentRt == null)
        {
            Debug.LogWarning("[AdsDiscountScreen] 奖励容器(parent)未绑定，跳过高度调整");
        }
        else
        {
            Vector2 parentSize = parentRt.sizeDelta;
            parentSize.y = height;
            parentRt.sizeDelta = parentSize;
        }

        ResolveGiftArtRefs();
        if (giftIcon != null)
        {
            RectTransform iconRt = giftIcon.rectTransform;
            Vector2 iconPos = iconRt.anchoredPosition;
            iconPos.y = large ? GiftIconYLarge : GiftIconYSmall;
            iconRt.anchoredPosition = iconPos;
        }

        Debug.Log($"[AdsDiscountScreen] 奖励区布局: 奖励数={rewardCount} → RewardBg/奖励容器高度={height}, " +
                  $"大图 y={(large ? GiftIconYLarge : GiftIconYSmall)}");
    }

    private void OnBuyButtonClicked()
    {
        if (currentShopItem == null || _purchasing) return;
        _purchasing = true;

        MessageSystem.Instance.ShowLoadingAnimation();

        if (UIUtilities.isEditMode)
        {
            ProductItem productItem = new ProductItem
            {
                order_id = "",
                IsoCurrencyCode = "",
                ItemName = currentShopItem.produceNameId,
                ProductId = currentShopItem.produceNameId,
                LocalizedPrice = 0,
            };
            OnPurchaseSuccess(productItem);
            return;
        }

        AnalyticMgr.PurchaseStart(currentShopItem.produceNameId);
        Game.self.Shop.Purchase(currentShopItem.GetProduceName(), OnPurchaseSuccess, OnPurchaseFailed);
    }

    private void OnPurchaseSuccess(ProductItem item)
    {
        _purchasing = false;
        Debug.Log($"[AdsDiscountScreen] 购买成功: {item?.ProductId}, mode={_mode}");

        // 统一走商店的购买成功处理：发放内容 + 内购成功弹窗 + 购买记录 + 统计 + 失败分归零
        if (ShopManager.shopManager != null)
            ShopManager.shopManager.OnPurchaseSuccess(item);

        MessageSystem.Instance.HideLoadingAnimation();
        Close();
    }

    private void OnPurchaseFailed(string error)
    {
        _purchasing = false;
        Debug.Log($"[AdsDiscountScreen] 购买失败: {error}");
        AnalyticMgr.PurchaseFailed(currentShopItem?.GetProduceName(), error);
        MessageSystem.Instance.HideLoadingAnimation();

        // 拉起系统支付后未完成购买（取消/失败），由商店统一弹「取消购买」弹窗
        if (currentShopItem != null && ShopManager.shopManager != null)
            ShopManager.shopManager.OnPurchaseCanceled(currentShopItem.produceNameId, true);
    }

    private void OnCloseBtn()
    {
        base.Close();
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (ClaimBtn != null) ClaimBtn.interactable = true;
        if (closeBtn != null) closeBtn.interactable = true;
        EventDispatcher.instance.TriggerUpdateLayerCoin(false, true);

        // 清理主动弹窗数据，避免下次打开误用旧礼包
        if (ShopManager.shopManager != null)
            ShopManager.shopManager.ClearPendingGiftPopup();

        // 弹窗关闭后刷新结算页的礼包入口按钮（买过 → 按钮收起；没买 → 按钮保留，可再次打开）
        RefreshFinishViewGiftButtons();
    }

    /// <summary>
    /// 通知结算页刷新「破冰礼包 / 限时折扣礼包」按钮显隐。
    /// 结算页没在展示（例如从道具弹窗里打开的礼包）时什么都不做。
    /// </summary>
    private void RefreshFinishViewGiftButtons()
    {
        if (SystemManager.Instance == null) return;
        if (!SystemManager.Instance.PanelIsShowing(PanelType.ChessFinishView)) return;

        var finish = SystemManager.Instance.GetPanel(PanelType.ChessFinishView);
        if (finish == null) return;

        var view = finish.GetComponent<ChessFinishView>();
        if (view != null) view.RefreshSettlementGiftButtons();
    }

    private string GetLocalized(string key, string fallback)
    {
        if (MultilingualManager.Instance == null) return fallback;
        string value = MultilingualManager.Instance.GetString(key);
        if (string.IsNullOrEmpty(value) || value == key) return fallback;
        return value;
    }
}
