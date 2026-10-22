using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 商店礼包物品（type 1 / 2）。
/// - type=2：普通礼包（金币由 shopCountText 展示，GiftItem 跳过金币）；
/// - type=1：广告礼包（移除广告 + 金币资源，金币由 GiftItem 展示）。
///
/// 继承 ShopItemBase（无广告），override：
///  - SetShopIcon：图标 = showIcon + "_Icon"；
///  - HandleSpecialTypeItems：空实现，阻止基类把 type=1 按"单物品道具"展示；
///  - HandleProductContentDisplay：礼包名 + 礼包底图 + （type=2）金币数量；
///  - HandleMultiProductContent / InitGiftItems：创建多个 GiftItem（type=2 跳过金币；type=1 金币也走 GiftItem）；
///  - IsContentSkippedOnPurchase：已拥有永久去广告 → 购买时跳过 RemoveAds / RemoveDayAds 的发放；
///  - SetProductPrice：原价文本（oldPriceText）+ 价格位置（有折扣下移）；
///  - SetShopData：按礼包 produceNameId 应用描边色 + 刷新限购次数 + 礼包标签显隐；
///  - OnTimeLimitedStateChanged：限时状态变化钩子（当前保留空实现）。
///
/// 描边颜色规则（与道具弹窗内购礼包一致）：
///   NewcomerBox      hsla(31, 100%, 35%, 1)
///   SpecialPackageA  hsla(225, 100%, 35%, 1)
///   BoxTime          hsla(31, 100%, 35%, 1)
///   SpecialPackageB  hsla(272, 95%, 34%, 1)
///   Box0             hsla(294, 97%, 23%, 1)
///   Box1             hsla(270, 99%, 27%, 1)
///   Box2             hsla(16, 100%, 32%, 1)
///   Box3             hsla(225, 95%, 30%, 1)
///   Box4             hsla(9, 100%, 35%, 1)
///
/// 注意：giftsParent / giftItemPrefab 由本类自己声明（基类已移除）。
/// </summary>
public class ShopGiftItem : ShopItemBase
{
    [Header("礼包专用")]
    [SerializeField] protected Transform giftsParent;            // 礼包内容父节点
    [SerializeField] protected GiftItem giftItemPrefab;          // 礼包内容项 Prefab
    [SerializeField] protected Text oldPriceText;                // 原价文本（有折扣时显示 originalPrice）
    [SerializeField] protected Text limitCountText;              // 限购次数文本
    [SerializeField] protected Text tagText;                     // 礼包标签

    /// <summary>礼包内容描边色（传给 GiftItem 使用，默认白色）</summary>
    protected Color _color = Color.white;

    // ======================= 描边颜色配置 =======================

    private static readonly Dictionary<string, Color> _bonusOutlineColors = new Dictionary<string, Color>
    {
        { "NewcomerBox",     HslToColor(31f,  1.00f, 0.35f) },
        { "SpecialPackageA", HslToColor(225f, 1.00f, 0.35f) },
        { "BoxTime",         HslToColor(31f,  1.00f, 0.35f) },
        { "SpecialPackageB", HslToColor(272f, 0.95f, 0.34f) },
        { "Box0",            HslToColor(294f, 0.97f, 0.23f) },
        { "Box1",            HslToColor(270f, 0.99f, 0.27f) },
        { "Box2",            HslToColor(16f,  1.00f, 0.32f) },
        { "Box3",            HslToColor(225f, 0.95f, 0.30f) },
        { "Box4",            HslToColor(9f,   1.00f, 0.35f) },
        { "RemoveADS",       HslToColor(189f, 1.00f, 0.11f) },
        { "RemoveAdsWeekly", HslToColor(189f, 1.00f, 0.11f) },
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

    private static bool TryGetBonusOutlineColor(string produceNameId, out Color color)
    {
        if (!string.IsNullOrEmpty(produceNameId) && _bonusOutlineColors.TryGetValue(produceNameId, out color))
            return true;
        color = Color.white;
        return false;
    }

    /// <summary>
    /// 给 nameText / limitCountText / shopCountText 的 Outline 设置描边色。
    ///  - nameText：从第 2 个 Outline 开始设置（保留第 1 个作为高光/上层描边）；
    ///  - limitCountText / shopCountText：全部 Outline 设置。
    /// </summary>
    private void ApplyOutlineColor(Color color)
    {
        if (nameText != null)
        {
            var outlines = nameText.GetComponents<Outline>();
            for (int i = 1; i < outlines.Length; i++)
                outlines[i].effectColor = color;
        }

        if (limitCountText != null)
        {
            var outlines = limitCountText.GetComponents<Outline>();
            for (int i = 0; i < outlines.Length; i++)
                outlines[i].effectColor = color;
        }

        if (shopCountText != null)
        {
            var outlines = shopCountText.GetComponents<Outline>();
            for (int i = 0; i < outlines.Length; i++)
                outlines[i].effectColor = color;
        }
    }

    // ======================= 限时状态变化钩子（保留空实现） =======================

    protected override void OnTimeLimitedStateChanged(bool isActive)
    {
        // 如需在限时角标显隐时调整礼包内容布局，可在此处理
    }

    // ======================= 图标 =======================

    protected override void SetShopIcon()
    {
        if (shopIcon == null || shopDataItem.type == 1) return;

        var icon = LoadShopIcon(shopDataItem.showIcon + "_Icon");
        if (icon != null)
            shopIcon.sprite = icon;
    }

    // ======================= 阻止基类把 type=1 按"单物品道具"展示 =======================

    /// <summary>
    /// type=1 在 ShopGiftItem 中按"广告礼包"处理，不走基类的单物品道具展示逻辑。
    /// </summary>
    protected override void HandleSpecialTypeItems(ShopDataItem data)
    {
        // 空实现
    }

    // ======================= ★ 购买发放跳过：已拥有永久去广告 → 跳过 RemoveAds / RemoveDayAds =======================

    /// <summary>
    /// 已拥有永久去广告时，礼包中的 RemoveAds / RemoveDayAds 项在购买发放时跳过。
    /// 与展示侧（InitGiftItems 跳过）保持一致：不展示也不发放。
    /// </summary>
    protected override bool IsContentSkippedOnPurchase(int giftType)
    {
        if (!IsRemoveAdsRelatedType(giftType)) return false;

        bool ownsPermanentRemoveAds =
            ShopManager.shopManager != null && ShopManager.shopManager.IsRemoveAdsOwned();

        if (ownsPermanentRemoveAds)
        {
            LogShopItem($"已拥有永久去广告，购买时跳过 content type={giftType} 的发放");
        }

        return ownsPermanentRemoveAds;
    }

    // ======================= 显示：礼包名 + 底图 + （type=2）金币数量 =======================

    protected override void HandleProductContentDisplay(ShopDataItem data)
    {
        if (nameText != null)
        {
            nameText.gameObject.SetActive(true);
            nameText.text = MultilingualManager.Instance?.GetString(data.name) ?? data.name;
        }

        if (dibg != null)
        {
            Sprite sprite = LoadShopIcon(data.showIcon);
            if (sprite != null)
                dibg.sprite = sprite;
        }

        // shopCountText 金币数量显示规则：
        //  - type=1 广告礼包：金币由 GiftItem 展示 → shopCountText 隐藏；
        //  - type=2 普通礼包：金币由 shopCountText 展示。
        if (shopCountText != null)
        {
            if (data.type == 1)
            {
                shopCountText.gameObject.SetActive(false);
            }
            else
            {
                int coins = GetCoinsFromProductContent(data);
                if (coins > 0)
                {
                    shopCountText.gameObject.SetActive(true);
                    shopCountText.text = $"{coins}";
                }
                else
                {
                    shopCountText.gameObject.SetActive(false);
                }
            }
        }
    }

    private int GetCoinsFromProductContent(ShopDataItem data)
    {
        if (data == null || data.productContent == null) return 0;

        foreach (var content in data.productContent)
        {
            if (content == null || content.Count < 2) continue;
            int type;
            if (!int.TryParse(content[0], out type)) continue;
            if (type != (int)LimitRewordType.Coins) continue;
            int count;
            return int.TryParse(content[1], out count) ? count : 0;
        }
        return 0;
    }

    // ======================= 礼包内容（type=2 跳过金币；type=1 金币也走 GiftItem） =======================

    protected override void HandleMultiProductContent(ShopDataItem data)
    {
        if (data == null) return;
        if (data.productContent == null || data.productContent.Count == 0) return;

        if (giftItemPrefab == null && AdvancedBundleLoader.SharedInstance != null)
        {
            var prefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "GiftItem");
            if (prefab != null)
                giftItemPrefab = prefab.GetComponent<GiftItem>();
        }

        if (giftItemPrefab == null)
        {
            LogShopItem("HandleMultiProductContent: giftItemPrefab 为空，跳过礼包内容初始化");
            return;
        }

        InitGiftItems();
    }

    private void InitGiftItems()
    {
        if (giftsParent == null || giftItemPrefab == null)
        {
            LogShopItem("InitGiftItems: giftsParent 或 giftItemPrefab 为空，跳过");
            return;
        }

        bool ownsPermanentRemoveAds =
            ShopManager.shopManager != null && ShopManager.shopManager.IsRemoveAdsOwned();

        foreach (List<string> giftdata in shopDataItem.productContent)
        {
            if (giftdata == null || giftdata.Count < 2)
            {
                LogShopItem("InitGiftItems: productContent 项数据不合法，跳过");
                continue;
            }

            int giftType = int.Parse(giftdata[0]);

            // 金币处理：
            //  - type=2 普通礼包：金币由 shopCountText 显示 → GiftItem 跳过金币；
            //  - type=1 广告礼包：金币用 GiftItem 显示 → 不跳过。
            if (giftType == (int)LimitRewordType.Coins && shopDataItem.type != 1)
            {
                LogShopItem($"InitGiftItems: 金币项由 shopCountText 展示，跳过 GiftItem (count={giftdata[1]})");
                continue;
            }

            // 已购买永久去广告 → 跳过礼包中的去广告物品
            if (ownsPermanentRemoveAds && IsRemoveAdsRelatedType(giftType))
            {
                LogShopItem($"已拥有永久去广告，跳过礼包中的去广告物品 (type={giftType})");
                continue;
            }

            GiftItem giftItem = Instantiate(giftItemPrefab, giftsParent).GetComponent<GiftItem>();

            if (giftType == (int)LimitRewordType.RemoveAds || giftType == (int)LimitRewordType.RemoveDayAds)
            {
                if (shopDataItem.type == 1 || shopDataItem.type == 2)
                    giftItem.SetShopData(giftdata, shopDataItem.id, _color, shopDataItem.des, shopDataItem.pointDes);
                else
                    giftItem.SetShopData(giftdata, shopDataItem.id, _color);
            }
            else
            {
                giftItem.SetShopData(giftdata, shopDataItem.id, _color);
            }

            bool isBonus = ShopManager.shopManager != null
                           && ShopManager.shopManager.IsBonusGift(shopDataItem.produceNameId, giftType);
            giftItem.SetBonus(isBonus);

            LogShopItem($"InitGiftItems: 礼包 {shopDataItem.produceNameId} (type={shopDataItem.type}) 内容 type={giftType}, isBonus={isBonus}");
        }
    }

    private bool IsRemoveAdsRelatedType(int type)
    {
        return type == (int)LimitRewordType.RemoveAds
               || type == (int)LimitRewordType.RemoveDayAds;
    }

    // ======================= 价格：原价 + 价格位置 =======================

    protected override void SetProductPrice(ShopDataItem data)
    {
        base.SetProductPrice(data);

        if (shopPriceText == null) return;

        bool hasDiscount = data != null
                           && data.originalPrice > 0
                           && data.originalPrice > data.price;

        if (oldPriceText != null)
        {
            if (hasDiscount)
            {
                CultureInfo culture = UIUtilities.GetCultureForCurrency("");
                oldPriceText.text = UIUtilities.FormatCurrency(data.originalPrice, culture);
                oldPriceText.gameObject.SetActive(true);
            }
            else
            {
                oldPriceText.gameObject.SetActive(false);
            }
        }

        var rt = shopPriceText.rectTransform;
        Vector2 pos = rt.anchoredPosition;
        pos.y = hasDiscount ? -20f : 0f;
        rt.anchoredPosition = pos;

        LogShopItem($"价格布局: originalPrice={data?.originalPrice}, price={data?.price}, hasDiscount={hasDiscount}, priceY={pos.y}");
    }

    // ======================= 限购次数 =======================

    private void RefreshLimitCount()
    {
        if (limitCountText == null) return;

        GameObject root = limitCountText.transform.parent != null
            ? limitCountText.transform.parent.gameObject
            : limitCountText.gameObject;

        if (shopDataItem == null || shopDataItem.limited <= 0)
        {
            root.SetActive(false);
            return;
        }

        int buyCount = 0;
        ShopLimitData limitData = GameDataManager.Instance?.UserData?.limitShopItems?
            .Find(x => x != null && x.nameid == shopDataItem.produceNameId);
        if (limitData != null)
            buyCount = limitData.buyCount;

        int remain = Mathf.Max(0, shopDataItem.limited - buyCount);

        if (remain <= 0)
            limitCountText.text = "已售空";
        else
            limitCountText.text = $"仅限{UIUtilities.GetChineseNumber(remain)}次";

        root.SetActive(true);
        LogShopItem($"限购次数: limited={shopDataItem.limited}, buyCount={buyCount}, remain={remain}, 显示='{limitCountText.text}'");
    }

    // ======================= 礼包标签 =======================

    private void RefreshTag()
    {
        if (tagText == null) return;

        GameObject root = tagText.transform.parent != null
            ? tagText.transform.parent.gameObject
            : tagText.gameObject;

        if (shopDataItem == null || string.IsNullOrEmpty(shopDataItem.labelZh))
        {
            root.SetActive(false);
            return;
        }

        tagText.text = shopDataItem.labelZh;
        root.SetActive(true);
        LogShopItem($"礼包标签: {shopDataItem.labelZh}");
    }

    // ======================= SetShopData：应用描边色 + 刷新限购 + 刷新标签 =======================

    public override void SetShopData(ShopDataItem data)
    {
        Color outlineColor = Color.white;
        bool hasConfig = data != null && TryGetBonusOutlineColor(data.produceNameId, out outlineColor);

        _color = hasConfig ? outlineColor : Color.white;

        base.SetShopData(data);

        if (hasConfig)
        {
            ApplyOutlineColor(outlineColor);
            LogShopItem($"描边颜色已应用: {data.produceNameId} → RGB({outlineColor.r:F3},{outlineColor.g:F3},{outlineColor.b:F3},{outlineColor.a:F2})");
        }

        RefreshLimitCount();
        RefreshTag();
    }
}