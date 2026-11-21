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

    [Header("促销标签背景（图集 ShopGifts）")]
    [SerializeField] private Image tagBg; // 标签背景（Tagbg），未绑定时按子节点名自动查找

    [Header("购买记录")]
    [SerializeField] private Button purchaseRecordBtn; // 可选：打开购买记录弹窗的按钮

    public static LimitRewordType limitRewordType;

    private string eventDes;

    private ShopDataItem shopDataItem;

    /// <summary>本礼包位是否已上报过 show_shopActivity（避免同一页重复上报）</summary>
    private bool _activityPackReported;

    private Color color;

    /// <summary>[Shop] 标签日志（走 LogManager，正式包也会写进手机日志文件）</summary>
    private void LogShop(string message)
    {
        string tag = shopDataItem != null ? $"{shopDataItem.produceNameId}(id={shopDataItem.id})" : "null";
        LogManager.ShopLog($"[GiftTable] [{tag}] {message}");
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

    // ======================= 促销标签背景（图集 ShopGifts） =======================

    /// <summary>标签背景图所在图集（图片实际文件在 Game_UI/ShopGifts/ShopGifts 下，bundle 名 shopgifts）</summary>
    private const string TagBgAtlas = "ShopGifts";

    /// <summary>标签背景节点名（GiftTable 根节点下的直接子节点，TagText 是它的子节点）</summary>
    private const string TagBgNodeName = "Tagbg";

    /// <summary>提示灯礼包 1、2 号位的标签背景</summary>
    private const string TagBgTip = "discountTip";
    /// <summary>提示灯礼包 3 号位的标签背景</summary>
    private const string TagBgTip3 = "discountTip1";
    /// <summary>火箭礼包的标签背景</summary>
    private const string TagBgRocket = "discountRocket";
    /// <summary>复活礼包的标签背景</summary>
    private const string TagBgRestart = "discountRestart";

    /// <summary>Prefab 上的原始标签背景（没有映射 / 图集缺图时回填）</summary>
    private Sprite _defaultTagBgSprite;
    private bool _tagBgResolved;

    /// <summary>当前礼包在本页的号位（0 基：0=1 号位）。提示灯礼包 3 号位要用 discountTip1</summary>
    private int _slotIndex;

    // ======================= 生命周期 =======================

    private void Start()
    {
        InitEvent();
    }

    public void InitUI(LimitRewordType limitReType, ShopDataItem shopItem, int slotIndex = 0)
    {
        if (shopItem == null)
        {
            LogShop("InitUI 跳过: shopItem 为空");
            return;
        }

        transform.GetComponent<Image>().sprite = LoadShopIcon(shopItem.showIcon);

        limitRewordType = limitReType;
        shopDataItem = shopItem;
        _slotIndex = Mathf.Max(0, slotIndex);   // 0 基号位，提示灯礼包 3 号位（index=2）单独换图

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

        // ★ 标签背景图（按礼包类型 + 号位取图集里对应的 discount* 图），
        //   放在文字判定之前：即使本次没有标签文案（Tagbg 整块隐藏），也要把 sprite 复位/换好，
        //   避免礼包位复用（复活弹窗横滑、GetItemScreen 反复打开）时残留上一个礼包的背景。
        RefreshTagBg();

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

    // ======================= 促销标签背景（换图） =======================

    /// <summary>
    /// 解析标签背景节点：优先用 Prefab 上绑定的 tagBg，未绑定时按固定子节点名 "Tagbg" 兜底查找
    /// （GetItemScreen / FailGameScreen 的 GiftTable 下都是 GiftTable/Tagbg/TagText 结构）。
    /// 同时记住 Prefab 上的原始图，作为没有映射 / 图集缺图时的回填图。
    /// </summary>
    private void ResolveTagBgRefs()
    {
        if (_tagBgResolved) return;
        _tagBgResolved = true;

        if (tagBg == null)
        {
            Transform node = transform.Find(TagBgNodeName);
            if (node != null) tagBg = node.GetComponent<Image>();
        }

        if (tagBg == null)
        {
            LogShop("未找到促销标签背景节点 Tagbg，标签背景不做换图");
            return;
        }

        _defaultTagBgSprite = tagBg.sprite;
    }

    /// <summary>
    /// 按「礼包类型 + 号位」从图集 ShopGifts 加载标签背景图：
    ///   提示灯礼包 1、2 号位 → discountTip；3 号位 → discountTip1；
    ///   火箭礼包（AutoComplete）→ discountRocket；复活礼包（ReStartTool）→ discountRestart；
    ///   其余礼包（金币包 / 去广告 / LuckyBag 等）保持 Prefab 默认图。
    /// 图集缺图时回填默认图，不隐藏标签（文案仍然可读）。
    /// </summary>
    private void RefreshTagBg()
    {
        ResolveTagBgRefs();
        if (tagBg == null) return;

        string spriteName = GetTagBgSpriteName();

        if (string.IsNullOrEmpty(spriteName))
        {
            tagBg.sprite = _defaultTagBgSprite;
            return;
        }

        // 用 Try 版本：图集里没有这张图时返回 null（不刷 Error），下面回填默认图
        Sprite sprite = AdvancedBundleLoader.SharedInstance.TryGetSpriteFromAtlas(spriteName, TagBgAtlas);

        if (sprite == null)
        {
            LogShop($"标签背景图缺失: {spriteName} @ {TagBgAtlas}，回填 Prefab 默认图");
            tagBg.sprite = _defaultTagBgSprite;
            return;
        }

        tagBg.sprite = sprite;
        LogShop($"标签背景: {spriteName} @ {TagBgAtlas}（limitRewordType={limitRewordType}, 号位={_slotIndex + 1}）");
    }

    /// <summary>当前礼包应使用的标签背景图名（返回 null = 不改图，保持 Prefab 默认）。</summary>
    private string GetTagBgSpriteName()
    {
        switch (limitRewordType)
        {
            case LimitRewordType.Tipstool:      // 提示灯礼包：1、2 号位 / 3 号位不同图
                return _slotIndex >= 2 ? TagBgTip3 : TagBgTip;
            case LimitRewordType.AutoComplete:  // 火箭礼包
                return TagBgRocket;
            case LimitRewordType.ReStartTool:   // 复活礼包
                return TagBgRestart;
            default:
                return null;
        }
    }

    private bool IsActivityPackProduct(ShopDataItem item)
    {
        // 统一口径：活动礼包标记 + 本次节日/周末限时未结束 + 未售罄
        return ShopManager.shopManager != null && ShopManager.shopManager.IsActivityPackActive(item);
    }

    private string GetActivityPackTag()
    {
        // 固定文案（礼包 LabelZh ＞ 节日/周末表固定标签）优先，兜底「周末特惠 / 节日特惠」
        return ShopManager.shopManager != null
            ? ShopManager.shopManager.GetActivityPackTag(shopDataItem)
            : "";
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

            // ★ 复活弹窗推荐位：先拿到弹窗引用（下面可能把自己隐藏，隐藏后再取就取不到了）
            FailGameScreen failScreen = GetComponentInParent<FailGameScreen>();

            // ★ 限购一次礼包购买后立即隐藏（下一关卡由推荐权重重新选出新礼包）
            if (ShopManager.shopManager != null && ShopManager.shopManager.IsLimitedOut(shopDataItem))
            {
                LogShop($"限购已满，立即隐藏礼包: {shopDataItem.produceNameId}");
                gameObject.SetActive(false);
            }

            // ★ 复活弹窗里买的礼包：购买后立刻隐藏（本次弹窗内不再展示），
            //   下次打开弹窗（进入下一关 / 退出游戏后再进）会由 RefreshGiftTables 重建并恢复显示。
            if (failScreen != null)
            {
                LogShop($"通知复活弹窗隐藏已购买礼包: {shopDataItem.produceNameId}");
                failScreen.HideGiftTableAfterPurchase(this, shopDataItem);
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

        // ★ 付费保护（cypz_payProtect）：礼包（复活 / 道具弹窗）购买成功同样续保护
        ChessDynamicHardManager.RecordPayProtect("礼包购买");

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

        // ★ bug50/bug51：购买成功后立刻落盘（奖励 + 购买记录），
        //   否则买完马上杀进程会全部丢失，而平台订单已被标记发货，重进也不会补。
        if (GameDataManager.Instance != null) GameDataManager.Instance.CommitGameData();

        LogShop($"========== 购买成功结束: {shopDataItem.produceNameId} ==========");
    }

    // ======================= 购买记录写入 =======================

    /// <summary>
    /// 写入一条购买记录到 UserData.purchaseRecords。
    /// - 名称：优先 MultilingualManager 翻译 nameKey，回退 produceNameId；
    /// - 价格：优先 ShopDataItem.price（配置价，元，与商店展示 / 正常购买口径一致）；
    ///         配置价缺失才用 ProductItem.LocalizedPrice 兜底，并做「分→元」归一
    ///         （华为 / 鸿蒙的平台价单位是分，直接入库会变 100 倍）；
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

            // ★ 价格：配置价优先，缺失时用平台价兜底并归一（分→元）
            float rawPlatformPrice = item?.LocalizedPrice ?? 0f;
            float price = shopDataItem.price > 0f
                ? shopDataItem.price
                : ShopManager.ToYuanPrice(rawPlatformPrice, shopDataItem.originalPrice);

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
                originalPrice = shopDataItem.originalPrice > 0f ? shopDataItem.originalPrice : price,
                currency = item?.IsoCurrencyCode ?? "",
                showIcon = shopDataItem.showIcon ?? "",
                limitRewordType = shopDataItem.type,
                buyTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                productContent = DeepCopyContent(shopDataItem.productContent)
            };

            GameDataManager.Instance?.UserData?.AddPurchaseRecord(record);

            LogShop($"购买记录已写入: name={record.nameText}, price={record.price}, " +
                    $"配置价={shopDataItem.price}, 平台价={rawPlatformPrice}, " +
                    $"time={record.buyTime}, contentCount={record.productContent.Count}");
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

        // 拉起系统支付后未完成购买（取消/失败），返回游戏时弹出购买未完成弹窗
        if (shopDataItem != null && ShopManager.shopManager != null)
            ShopManager.shopManager.OnPurchaseCanceled(shopDataItem.produceNameId, true, error);
    }
}