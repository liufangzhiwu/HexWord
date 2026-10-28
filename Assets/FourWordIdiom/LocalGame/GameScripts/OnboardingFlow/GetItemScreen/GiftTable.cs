using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Mime;
using DG.Tweening;
#if UNITY_HUAWEI
using HuaweiService.CloudStorage;
#endif
using Middleware;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GiftTable : MonoBehaviour
{
    [SerializeField] private Button BuyBtn;
    [SerializeField] private Text shopOldPriceText;
    [SerializeField] private Text shopPriceText;
    [SerializeField] private Text giftNameText;
    [SerializeField] private Text discount; // 折扣
    [SerializeField] private List<GiftItem> _giftItems;
    [SerializeField] private GiftItem _AddgiftItem; // 加赠物品（独立样式）
    [SerializeField] private Text limintCountText; // 限购次数
    [SerializeField] private Text tagText; // 促销标签

    [Header("购买记录")]
    [SerializeField] private Button purchaseRecordBtn; // 可选：打开购买记录弹窗的按钮

    public static LimitRewordType limitRewordType;

    private string eventDes;

    private ShopDataItem shopDataItem;

    /// <summary>本礼包位是否已上报过 show_shopActivity（避免同一页重复上报）</summary>
    private bool _activityPackReported;

    private Color color;

    /// <summary>[Shop] 标签日志</summary>
    private void LogShop(string message)
    {
        string tag = shopDataItem != null ? $"{shopDataItem.produceNameId}(id={shopDataItem.id})" : "null";
        Debug.Log($"[Shop][GiftTable] [{tag}] {message}");
    }

    // ======================= 描边颜色配置（与 ShopGiftItem 保持一致） =======================

    /// <summary>
    /// 各礼包 produceNameId 对应的描边颜色（优先于 limitRewordType 的基础色）。
    /// 色值按 hsla(h, s%, l%, a) 给定，代码中用 HslToColor 转换。
    /// </summary>
    private static readonly Dictionary<string, Color> _bonusOutlineColors = new Dictionary<string, Color>
    {
        { "NewcomerBox",     HslToColor(31f,  1.00f, 0.35f) },   // hsla(31, 100%, 35%, 1)
        { "SpecialPackageA", HslToColor(225f, 1.00f, 0.35f) },   // hsla(225, 100%, 35%, 1)
        { "BoxTime",         HslToColor(31f,  1.00f, 0.35f) },   // hsla(31, 100%, 35%, 1)
        { "SpecialPackageB", HslToColor(272f, 0.95f, 0.34f) },   // hsla(272, 95%, 34%, 1)
        { "Box0",            HslToColor(294f, 0.97f, 0.23f) },   // hsla(294, 97%, 23%, 1)
        { "Box1",            HslToColor(270f, 0.99f, 0.27f) },   // hsla(270, 99%, 27%, 1)
        { "Box2",            HslToColor(16f,  1.00f, 0.32f) },   // hsla(16, 100%, 32%, 1)
        { "Box3",            HslToColor(225f, 0.95f, 0.30f) },   // hsla(225, 95%, 30%, 1)
        { "Box4",            HslToColor(9f,   1.00f, 0.35f) },   // hsla(9, 100%, 35%, 1)
        { "RemoveADS",       HslToColor(189f, 1.00f, 0.11f) },   // hsla(189, 100%, 11%, 1)
        { "RemoveAdsWeek", HslToColor(189f, 1.00f, 0.11f) },   // hsla(189, 100%, 11%, 1)
    };

    /// <summary>HSL(A) → Unity Color。</summary>
    private static Color HslToColor(float h, float s, float l, float a = 1f)
    {
        h %= 360f;
        if (h < 0f) h += 360f;
        s = Mathf.Clamp01(s);
        l = Mathf.Clamp01(l);

        float c = (1f - Mathf.Abs(2f * l - 1f)) * s;
        float hPrime = h / 60f;
        float x = c * (1f - Mathf.Abs(hPrime % 2f - 1f));
        float m = l - c / 2f;

        float r, g, b;
        if (hPrime < 1f) { r = c; g = x; b = 0f; }
        else if (hPrime < 2f) { r = x; g = c; b = 0f; }
        else if (hPrime < 3f) { r = 0f; g = c; b = x; }
        else if (hPrime < 4f) { r = 0f; g = x; b = c; }
        else if (hPrime < 5f) { r = x; g = 0f; b = c; }
        else { r = c; g = 0f; b = x; }

        return new Color(r + m, g + m, b + m, a);
    }

    /// <summary>尝试按 produceNameId 获取描边色（无配置返回 false，由调用方保持已有颜色）。</summary>
    private static bool TryGetBonusOutlineColor(string produceNameId, out Color color)
    {
        if (!string.IsNullOrEmpty(produceNameId) && _bonusOutlineColors.TryGetValue(produceNameId, out color))
            return true;
        color = Color.white;
        return false;
    }

    // ======================= 特殊礼包配置 =======================

    /// <summary>
    /// 特殊礼包 A：productContent 最后一项应显示在加赠位（隐藏加赠标签）。
    /// 目前配置：GoldBag1 / GoldBag2
    /// </summary>
    private static bool IsLastItemAsBonusPosition(string produceNameId)
    {
        return produceNameId == "GoldBag1" || produceNameId == "GoldBag2";
    }

    /// <summary>
    /// 特殊礼包 B：productContent 中的金币项应显示在加赠位（隐藏加赠标签）。
    /// 目前配置：LuckyBag1 / LuckyBag2
    /// </summary>
    private static bool IsGoldToBonusPosition(string produceNameId)
    {
        return produceNameId == "LuckyBag1" || produceNameId == "LuckyBag2";
    }

    /// <summary>是否为去广告相关的内容类型（永久 / 限时）</summary>
    private static bool IsRemoveAdsRelatedType(int type)
    {
        return type == (int)LimitRewordType.RemoveAds
               || type == (int)LimitRewordType.RemoveDayAds;
    }

    // ======================= 生命周期 =======================

    private void Start()
    {
        InitEvent();
    }

    public void InitUI(LimitRewordType limitReType, ShopDataItem shopItem)
    {
        if (shopItem == null)
        {
            LogShop("InitUI 跳过: shopItem 为空");
            return;
        }

        transform.GetComponent<Image>().sprite = LoadShopIcon(shopItem.showIcon);

        limitRewordType = limitReType;
        shopDataItem = shopItem;

        // ---------- 折扣 ----------
        if (discount != null)
        {
            if (string.IsNullOrEmpty(shopItem.discount))
            {
                discount.transform.parent.gameObject.SetActive(false);
            }
            else
            {
                discount.transform.parent.gameObject.SetActive(true);
                discount.text = shopItem.discount;
            }
        }

        // ---------- 描边颜色（优先级：produceNameId 配置 > limitRewordType 基础 > LuckyBag 强制） ----------
        List<Outline> Outlines = giftNameText.GetComponents<Outline>().ToList();
        List<Outline> Outlinelimits = limintCountText.GetComponents<Outline>().ToList();
        color = new Color(0f, 0.1529412f, 0.4745098f, 1f);

        // 1. 基础色：按 limitRewordType
        switch (limitRewordType)
        {
            case LimitRewordType.ReStartTool:
                giftNameText.text = shopItem.name;
                limintCountText.transform.parent.GetComponent<Image>().sprite = LoadShopIcon("faillimitbg");
                color = new Color(0f, 0.1529412f, 0.4745098f, 1f);
                break;
            case LimitRewordType.Butterfly:
                break;
            case LimitRewordType.AutoComplete:
                giftNameText.text = shopItem.name;
                limintCountText.transform.parent.GetComponent<Image>().sprite = LoadShopIcon("autolimitbg");
                break;
            case LimitRewordType.Tipstool:
                giftNameText.text = shopItem.name;
                limintCountText.transform.parent.GetComponent<Image>().sprite = LoadShopIcon("tiplimitbg");
                color = new Color(0.4941177f, 0f, 0.03529412f, 1f);
                break;
        }

        // 2. LuckyBag1/2 强制色
        if (shopItem.produceNameId == "LuckyBag1" || shopItem.produceNameId == "LuckyBag2")
        {
            color = new Color(0f, 0.1529412f, 0.4745098f, 1f);
        }

        // 3. ★ 按 produceNameId 配置覆盖（优先级最高，与 ShopGiftItem 一致）
        if (TryGetBonusOutlineColor(shopItem.produceNameId, out Color outlineColor))
        {
            color = outlineColor;
            LogShop($"描边颜色命中 produceNameId 配置: {shopItem.produceNameId} → RGB({outlineColor.r:F3},{outlineColor.g:F3},{outlineColor.b:F3},{outlineColor.a:F2})");
        }

        // 4. 应用到 Outline
        for (int i = 1; i < Outlines.Count; i++)
        {
            Outlines[i].effectColor = color;
        }

        for (int i = 0; i < Outlinelimits.Count; i++)
        {
            Outlinelimits[i].effectColor = color;
        }

        // ---------- 礼包内容（跳过加赠项 / 跳过 GoldBag 最后一项 / 跳过 LuckyBag 金币项 / 跳过已拥有去广告） ----------
        RefreshGiftContent();

        // ---------- 加赠物品（独立样式） ----------
        RefreshBonusItem();

        // ---------- 限购次数 ----------
        RefreshLimitCount();

        // ---------- 促销标签 ----------
        RefreshTag();

        eventDes = giftNameText.text;
        SetProductPrice();

        LogShop($"InitUI 完成: limitRewordType={limitRewordType}, price={shopItem.price}, " +
                $"giftType={shopItem.giftType}, discount={shopItem.discount}, " +
                $"productContent.Count={(shopItem.productContent != null ? shopItem.productContent.Count : 0)}, " +
                $"lastItemAsBonus={IsLastItemAsBonusPosition(shopItem.produceNameId)}, " +
                $"goldToBonus={IsGoldToBonusPosition(shopItem.produceNameId)}, " +
                $"ownsPermanentRemoveAds={IsRemoveAdsOwned()}");
    }

    // ======================= 礼包内容（排除加赠项 / GoldBag 最后一项 / LuckyBag 金币项 / 已拥有去广告） =======================

    /// <summary>
    /// 刷新礼包内容（_giftItems）。
    /// - 普通礼包：productContent 中 IsBonusGift == true 的项跳过（交给 _AddgiftItem 单独显示）；
    /// - GoldBag1 / GoldBag2：productContent 最后一项跳过（交给 _AddgiftItem，隐藏标签）；
    /// - LuckyBag1 / LuckyBag2：productContent 中金币项跳过（交给 _AddgiftItem，隐藏标签）；
    /// - ★ 已购买永久去广告：跳过 RemoveAds / RemoveDayAds 项（不展示）；
    /// - 其余项按顺序填入 _giftItems。
    /// ★ 发放时由 ShopManager.HandleGiftTablePurchaseRewards 处理（发放侧也需做同样的跳过）。
    /// </summary>
    private void RefreshGiftContent()
    {
        for (int i = 0; i < _giftItems.Count; i++)
        {
            _giftItems[i].gameObject.SetActive(false);
        }

        if (shopDataItem.productContent == null) return;

        bool lastItemAsBonus = IsLastItemAsBonusPosition(shopDataItem.produceNameId);
        bool goldToBonus = IsGoldToBonusPosition(shopDataItem.produceNameId);
        bool ownsPermanentRemoveAds = IsRemoveAdsOwned();

        int normalItemCount = shopDataItem.productContent.Count;
        if (lastItemAsBonus && normalItemCount > 0)
        {
            // 最后一项留给 _AddgiftItem，_giftItems 只显示前面的
            normalItemCount -= 1;
        }

        int giftIndex = 0;
        for (int i = 0; i < normalItemCount; i++)
        {
            List<string> giftdata = shopDataItem.productContent[i];
            if (giftdata == null || giftdata.Count < 2)
            {
                LogShop($"RefreshGiftContent: productContent[{i}] 数据不合法，跳过");
                continue;
            }

            int giftType = int.Parse(giftdata[0]);

            // ★ 已购买永久去广告：跳过礼包中的去广告物品（不展示）
            if (ownsPermanentRemoveAds && IsRemoveAdsRelatedType(giftType))
            {
                LogShop($"RefreshGiftContent: 内容[{i}] type={giftType} 已拥有永久去广告，跳过展示");
                continue;
            }

            // ★ LuckyBag1 / LuckyBag2：金币项跳过（交给 _AddgiftItem，隐藏加赠标签）
            if (goldToBonus && giftType == (int)LimitRewordType.Coins)
            {
                LogShop($"RefreshGiftContent: 内容[{i}] type={giftType} 是 LuckyBag 金币项，跳过（交给 _AddgiftItem）");
                continue;
            }

            // 普通礼包：IsBonusGift 识别出的加赠项跳过
            if (!lastItemAsBonus
                && !goldToBonus
                && ShopManager.shopManager != null
                && ShopManager.shopManager.IsBonusGift(shopDataItem.produceNameId, giftType))
            {
                LogShop($"RefreshGiftContent: 内容[{i}] type={giftType} 是加赠物品，跳过（交给 _AddgiftItem）");
                continue;
            }

            if (giftIndex >= _giftItems.Count)
            {
                LogShop($"RefreshGiftContent: _giftItems 数量不足 (giftIndex={giftIndex})，剩余内容不展示");
                break;
            }

            GiftItem giftItem = _giftItems[giftIndex];
            giftItem.gameObject.SetActive(true);
            giftItem.SetShopData(giftdata, shopDataItem.id, color);
            giftIndex++;

            LogShop($"RefreshGiftContent: 礼包 {shopDataItem.produceNameId} 内容[{giftIndex - 1}] type={giftType}");
        }
    }

    // ======================= 加赠物品（独立样式） =======================

    /// <summary>
    /// 刷新加赠物品（_AddgiftItem）。
    /// 优先级：
    ///   1. LuckyBag1 / LuckyBag2：金币项显示在加赠位，隐藏加赠标签（SetBonus(false)）；
    ///   2. GoldBag1 / GoldBag2：productContent 最后一项显示在加赠位，隐藏加赠标签（SetBonus(false)）；
    ///   3. 其他礼包：IsBonusGift == true 的项显示在加赠位，并显示加赠标签（SetBonus(true)）；
    ///   4. 都没有 / 加赠项为去广告且已拥有 → 隐藏 _AddgiftItem。
    /// </summary>
    private void RefreshBonusItem()
    {
        if (_AddgiftItem == null) return;

        // ---------- 情况 A：LuckyBag1 / LuckyBag2 → 金币项显示在加赠位，隐藏加赠标签 ----------
        if (IsGoldToBonusPosition(shopDataItem.produceNameId))
        {
            List<string> goldItem = FindGoldItemInProductContent();
            if (goldItem == null || goldItem.Count < 2)
            {
                _AddgiftItem.gameObject.SetActive(false);
                LogShop($"{shopDataItem.produceNameId}: 无金币项，隐藏 _AddgiftItem");
                return;
            }

            _AddgiftItem.gameObject.SetActive(true);
            _AddgiftItem.SetShopData(goldItem, shopDataItem.id, color);
            _AddgiftItem.SetBonus(false); // 隐藏加赠标签

            LogShop($"{shopDataItem.produceNameId}: 金币项显示在加赠位（隐藏加赠标签）: type={goldItem[0]}, count={goldItem[1]}");
            return;
        }

        // ---------- 情况 B：GoldBag1 / GoldBag2 → 最后一项显示在加赠位，隐藏加赠标签 ----------
        if (IsLastItemAsBonusPosition(shopDataItem.produceNameId))
        {
            List<string> lastItem = GetLastProductContent();
            if (lastItem == null || lastItem.Count < 2)
            {
                _AddgiftItem.gameObject.SetActive(false);
                LogShop($"{shopDataItem.produceNameId}: 无最后一项，隐藏 _AddgiftItem");
                return;
            }

            _AddgiftItem.gameObject.SetActive(true);
            _AddgiftItem.SetShopData(lastItem, shopDataItem.id, color);
            _AddgiftItem.SetBonus(false); // 隐藏加赠标签

            LogShop($"{shopDataItem.produceNameId}: 最后一项显示在加赠位（隐藏加赠标签）: type={lastItem[0]}, count={lastItem[1]}");
            return;
        }

        // ---------- 情况 C：普通礼包 → IsBonusGift 识别出的加赠项 ----------
        List<string> bonusData = FindBonusItemInProductContent();
        if (bonusData == null || bonusData.Count < 2)
        {
            _AddgiftItem.gameObject.SetActive(false);
            LogShop("未找到加赠项，隐藏 _AddgiftItem");
            return;
        }

        // ★ 加赠项若为去广告且已拥有永久去广告 → 不展示加赠位
        if (int.TryParse(bonusData[0], out int bonusType)
            && IsRemoveAdsOwned()
            && IsRemoveAdsRelatedType(bonusType))
        {
            _AddgiftItem.gameObject.SetActive(false);
            LogShop($"加赠项为去广告(type={bonusType})且已拥有永久去广告，隐藏 _AddgiftItem");
            return;
        }

        _AddgiftItem.gameObject.SetActive(true);
        _AddgiftItem.SetShopData(bonusData, shopDataItem.id, color);
        _AddgiftItem.SetBonus(true);

        LogShop($"加赠物品已显示: type={bonusData[0]}, count={bonusData[1]}");
    }

    /// <summary>取 productContent 的最后一项。</summary>
    private List<string> GetLastProductContent()
    {
        if (shopDataItem == null || shopDataItem.productContent == null) return null;
        if (shopDataItem.productContent.Count == 0) return null;
        return shopDataItem.productContent[shopDataItem.productContent.Count - 1];
    }

    /// <summary>从 productContent 里找出金币项（type == Coins 的第一项）。未找到返回 null。</summary>
    private List<string> FindGoldItemInProductContent()
    {
        if (shopDataItem == null || shopDataItem.productContent == null) return null;

        for (int i = 0; i < shopDataItem.productContent.Count; i++)
        {
            List<string> giftdata = shopDataItem.productContent[i];
            if (giftdata == null || giftdata.Count < 2) continue;

            int giftType;
            if (!int.TryParse(giftdata[0], out giftType)) continue;

            if (giftType == (int)LimitRewordType.Coins)
                return giftdata;
        }
        return null;
    }

    private List<string> FindBonusItemInProductContent()
    {
        if (shopDataItem == null || shopDataItem.productContent == null) return null;
        if (ShopManager.shopManager == null) return null;

        for (int i = 0; i < shopDataItem.productContent.Count; i++)
        {
            List<string> giftdata = shopDataItem.productContent[i];
            if (giftdata == null || giftdata.Count < 2) continue;

            int giftType;
            if (!int.TryParse(giftdata[0], out giftType)) continue;

            if (ShopManager.shopManager.IsBonusGift(shopDataItem.produceNameId, giftType))
                return giftdata;
        }
        return null;
    }

    private Sprite LoadShopIcon(string showIcon)
    {
        Sprite sprite = null;
        sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(showIcon);

        return sprite;
    }

    /// <summary>是否已拥有永久去广告</summary>
    private bool IsRemoveAdsOwned()
    {
        return ShopManager.shopManager != null && ShopManager.shopManager.IsRemoveAdsOwned();
    }

    // ======================= 限购次数 =======================

    private void RefreshLimitCount()
    {
        if (limintCountText == null) return;

        if (shopDataItem == null || shopDataItem.limited <= 0)
        {
            limintCountText.transform.parent.gameObject.SetActive(false);
            return;
        }

        int buyCount = 0;
        ShopLimitData limitData = GameDataManager.Instance?.UserData?.limitShopItems?
            .Find(x => x != null && x.nameid == shopDataItem.produceNameId);
        if (limitData != null)
            buyCount = limitData.buyCount;

        int remain = Mathf.Max(0, shopDataItem.limited - buyCount);

        if (remain <= 0)
            limintCountText.text = "已售空";
        else
            limintCountText.text = $"仅限{UIUtilities.GetChineseNumber(remain)}次";

        limintCountText.transform.parent.gameObject.SetActive(true);
        LogShop($"限购次数: limited={shopDataItem.limited}, buyCount={buyCount}, remain={remain}, 显示='{limintCountText.text}'");
    }

    // ======================= 促销标签 =======================

    private void RefreshTag()
    {
        if (tagText == null) return;

        if (shopDataItem == null)
        {
            tagText.transform.parent.gameObject.SetActive(false);
            return;
        }

        string tag = "";

        if (ShopManager.shopManager != null && ShopManager.shopManager.IsLastPurchasedPack(shopDataItem))
        {
            tag = "上次购买";
        }
        else if (!string.IsNullOrEmpty(shopDataItem.labelZh))
        {
            tag = shopDataItem.labelZh;
        }
        else if (IsActivityPackProduct(shopDataItem))
        {
            tag = GetActivityPackTag();
        }

        // 商店活动礼包：本页首次渲染到活动礼包时上报一次弹出
        if (!_activityPackReported && IsActivityPackProduct(shopDataItem))
        {
            _activityPackReported = true;
            AnalyticMgr.ShopActivityShow(shopDataItem.produceNameId);
        }

        if (string.IsNullOrEmpty(tag))
        {
            tagText.transform.parent.gameObject.SetActive(false);
        }
        else
        {
            tagText.text = tag;
            tagText.transform.parent.gameObject.SetActive(true);
            LogShop($"促销标签: {tag}");
        }
    }

    private bool IsActivityPackProduct(ShopDataItem item)
    {
        if (item == null) return false;
        ShopLimitData limitData = GameDataManager.Instance?.UserData?.limitShopItems?
            .Find(x => x != null && x.nameid == item.produceNameId);
        return limitData != null && limitData.isActivityPack;
    }

    private string GetActivityPackTag()
    {
        DayOfWeek dayOfWeek = DateTime.Now.DayOfWeek;
        if (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday)
            return MultilingualManager.Instance?.GetString("WeekendSpecialOffer") ?? "周末特惠";

        return MultilingualManager.Instance?.GetString("HolidaySpecialOffer") ?? "节日特惠";
    }

    // ======================= 价格 =======================

    private void SetProductPrice()
    {
        if (shopPriceText == null) return;

        Debug.Log($"获取商品内购名称: {shopDataItem.GetProduceName()}");

        try
        {
            float price = shopDataItem.price;
            float oldprice = shopDataItem.originalPrice;
            CultureInfo culture = UIUtilities.GetCultureForCurrency("");

            shopPriceText.text = UIUtilities.FormatCurrency(price, culture);

            if (shopOldPriceText != null)
            {
                if (oldprice <= 0)
                {
                    shopOldPriceText.gameObject.SetActive(false);
                    shopPriceText.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 0);
                }
                else
                {
                    shopOldPriceText.gameObject.SetActive(true);
                    shopPriceText.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -19);
                    shopOldPriceText.text = UIUtilities.FormatCurrency(oldprice, culture);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error setting product price: {ex.Message}");
        }
    }

    private void InitEvent()
    {
        BuyBtn.AddClickAction(OnBuyButtonClicked);
    }

    private void OnBuyButtonClicked()
    {
        LogShop("点击购买");
        MessageSystem.Instance.ShowLoadingAnimation();

        if (UIUtilities.isEditMode)
        {
            ProductItem productItem = new ProductItem
            {
                order_id = "",
                IsoCurrencyCode = "",
                ItemName = shopDataItem.produceNameId,
                ProductId = shopDataItem.produceNameId,
                LocalizedPrice = 0,
            };
            OnPurchaseSuccess(productItem);
        }
        else
        {
            AnalyticMgr.PurchaseStart(shopDataItem.produceNameId);
            // 商店活动礼包：点击
            if (IsActivityPackProduct(shopDataItem))
            {
                AnalyticMgr.ShopActivityTap(shopDataItem.produceNameId);
            }
            Game.self.Shop.Purchase(shopDataItem.GetProduceName(), OnPurchaseSuccess, OnPurchaseFailed);
        }
    }

    private void OnPurchaseSuccess(ProductItem item)
    {
        LogShop($"========== 购买成功开始: ProductId={item?.ProductId}, ItemName={item?.ItemName}, LocalizedPrice={item?.LocalizedPrice} ==========");

        shopDataItem = ShopManager.shopManager.GetProduct(item.ProductId);
        if (shopDataItem == null)
        {
            LogShop($"购买成功但商店未找到该商品: {item.ProductId}");
            return;
        }

        if (shopDataItem.GetProduceName() == item.ProductId)
        {
            SystemManager.Instance.HidePanel(PanelType.GetItemScreen);

            Game.self.Shop.CurrentShopDataItem = shopDataItem.DeepCopy();

            SystemManager.Instance.ShowPanel(PanelType.AwardScreen);

            if (ShopManager.shopManager != null)
            {
                // ★ 发放侧跳过（已拥有永久去广告时跳过 RemoveAds / RemoveDayAds 的发放）
                //   需在 ShopManager.HandleGiftTablePurchaseRewards 内部实现，见文末说明。
                ShopManager.shopManager.HandleGiftTablePurchaseRewards(shopDataItem, item);
            }
            else
            {
                LogShop("ShopManager.shopManager 为空，无法处理购买后逻辑（回退：仅打印）");
            }

            // ★ 限购一次礼包购买后立即隐藏（下一关卡由推荐权重重新选出新礼包）
            if (ShopManager.shopManager != null && ShopManager.shopManager.IsLimitedOut(shopDataItem))
            {
                LogShop($"限购已满，立即隐藏礼包: {shopDataItem.produceNameId}");
                gameObject.SetActive(false);
            }

            // ★ 写入购买记录（礼包名称、价格、礼包内容快照、购买时间）
            RecordPurchase(item);
        }

        ShopManager.shopManager.paysuccess = true;
        bool firstPay = GameDataManager.Instance.UserData.TotalPayTimes == 0;
        if (firstPay)
            GameDataManager.Instance.UserData.firstPayTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        GameDataManager.Instance.UserData.lastPayTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        GameDataManager.Instance.UserData.LastPayTimeTicks = DateTime.Now.Ticks;
        GameDataManager.Instance.UserData.TotalPayTimes++;
        GameDataManager.Instance.UserData.TotalRevenue += item.LocalizedPrice;
        DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedShopBuy, 1);

        LogShop($"支付统计: firstPay={firstPay}, TotalPayTimes={GameDataManager.Instance.UserData.TotalPayTimes}, " +
                $"TotalRevenue={GameDataManager.Instance.UserData.TotalRevenue}");

        if (!UIUtilities.isEditMode)
        {
            AnalyticMgr.PurchaseFinished(item, firstPay);

            // 商店活动礼包：购买成功
            if (IsActivityPackProduct(shopDataItem))
            {
                AnalyticMgr.ShopActivityBuy(shopDataItem.produceNameId);
            }

#if UNITY_HUAWEI
            item?.OnShipmentCompleted(true);
#endif
        }

        MessageSystem.Instance.HideLoadingAnimation();

        LogShop($"========== 购买成功结束: {shopDataItem.produceNameId} ==========");
    }

    // ======================= 购买记录写入 =======================

    /// <summary>
    /// 写入一条购买记录到 UserData.purchaseRecords。
    /// - 名称：优先 MultilingualManager 翻译 nameKey，回退 produceNameId；
    /// - 价格：优先 ProductItem.LocalizedPrice（>0 时），否则回退 ShopDataItem.price；
    /// - 礼包内容：深拷贝 ShopDataItem.productContent，避免后续引用变化影响历史记录；
    /// - 购买时间：yyyy-MM-dd HH:mm:ss。
    /// </summary>
    private void RecordPurchase(ProductItem item)
    {
        try
        {
            if (shopDataItem == null)
            {
                LogShop("RecordPurchase: shopDataItem 为空，跳过记录");
                return;
            }

            float price = shopDataItem.price;
            if (item != null && item.LocalizedPrice > 0)
                price = item.LocalizedPrice;

            string nameText = MultilingualManager.Instance?.GetString(shopDataItem.name)
                              ?? shopDataItem.name
                              ?? shopDataItem.produceNameId
                              ?? "";

            var record = new PurchaseRecord
            {
                orderId = string.IsNullOrEmpty(item?.order_id)
                    ? System.Guid.NewGuid().ToString("N")
                    : item.order_id,
                produceNameId = shopDataItem.produceNameId ?? "",
                nameKey = shopDataItem.name ?? "",
                nameText = nameText,
                price = price,
                originalPrice = shopDataItem.originalPrice,
                currency = item?.IsoCurrencyCode ?? "",
                showIcon = shopDataItem.showIcon ?? "",
                limitRewordType = shopDataItem.type,
                buyTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                productContent = DeepCopyContent(shopDataItem.productContent)
            };

            GameDataManager.Instance?.UserData?.AddPurchaseRecord(record);

            LogShop($"购买记录已写入: name={record.nameText}, price={record.price}, time={record.buyTime}, contentCount={record.productContent.Count}");
        }
        catch (Exception ex)
        {
            LogShop($"RecordPurchase 异常: {ex.Message}");
        }
    }

    /// <summary>深拷贝 productContent（避免后续 ShopDataItem 变化影响历史记录）。</summary>
    private static List<List<string>> DeepCopyContent(List<List<string>> src)
    {
        var dst = new List<List<string>>();
        if (src == null) return dst;

        foreach (var row in src)
        {
            if (row == null) { dst.Add(new List<string>()); continue; }
            dst.Add(new List<string>(row));
        }
        return dst;
    }

    private void OnPurchaseFailed(string error)
    {
        LogShop($"购买失败: {error}");
        AnalyticMgr.PurchaseFailed(shopDataItem.GetProduceName(), error);
        MessageSystem.Instance.HideLoadingAnimation();

        // 拉起系统支付后未完成购买（取消/失败），返回游戏时弹出「取消购买」弹窗
        if (shopDataItem != null && ShopManager.shopManager != null)
            ShopManager.shopManager.OnPurchaseCanceled(shopDataItem.produceNameId, true);
    }
}