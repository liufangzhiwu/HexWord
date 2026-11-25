using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 购买记录单条 UI：礼包名称、价格、原价、购买时间、礼包内容。
/// - 礼包内容优先用 GiftItem 实例化展示（需要配置 contentParent + giftItemPrefab）；
/// - 未配置 GiftItem 时回退到 contentText 纯文本展示。
/// </summary>
public class PurchaseRecordItem : MonoBehaviour
{
    [Header("基础信息")]
    [SerializeField] private Text nameText;         // 礼包名称
    [SerializeField] private Text priceText;        // 实际价格
    //[SerializeField] private Text oldPriceText;     // 原价（划线价，无折扣时隐藏）
    [SerializeField] private Text timeText;         // 购买时间
    //[SerializeField] private Image iconImage;       // 可选：礼包底图

    [Header("礼包内容（GiftItem 方式）")]
    [SerializeField] private Transform contentParent;   // 内容父节点（Grid/VerticalLayout）
    [SerializeField] private GiftItem giftItemPrefab;   // 单个礼包内容项 Prefab
   

    private readonly List<GiftItem> _spawned = new List<GiftItem>();

    public void SetData(PurchaseRecord record)
    {
        if (record == null) return;

        // ---------- 名称 ----------
        if (nameText != null)
        {
            nameText.text = string.IsNullOrEmpty(record.nameText)
                ? (MultilingualManager.Instance?.GetString(record.nameKey) ?? record.nameKey ?? "")
                : record.nameText;
        }

        // ---------- 实际价格 ----------
        if (priceText != null)
        {
            var culture = UIUtilities.GetCultureForCurrency("");
            priceText.text = UIUtilities.FormatCurrency(record.price, culture);
        }

        // ---------- 原价（有折扣才显示） ----------
        // if (oldPriceText != null)
        // {
        //     bool hasOld = record.originalPrice > 0 && record.originalPrice > record.price;
        //     if (hasOld)
        //     {
        //         var culture = UIUtilities.GetCultureForCurrency("");
        //         oldPriceText.text = UIUtilities.FormatCurrency(record.originalPrice, culture);
        //         oldPriceText.gameObject.SetActive(true);
        //     }
        //     else
        //     {
        //         oldPriceText.gameObject.SetActive(false);
        //     }
        // }

        // ---------- 购买时间 ----------
        // ★ 只显示日期（yyyy-MM-dd），不显示具体时分秒
        if (timeText != null)
            timeText.text = FormatBuyDate(record.buyTime);

        // ---------- 礼包内容 ----------
        RefreshContent(record);
    }

    /// <summary>
    /// 购买时间只取日期部分展示。
    /// 存档里 buyTime 是 "yyyy-MM-dd HH:mm:ss"（旧记录也是这个格式），
    /// 这里解析后统一输出 "yyyy-MM-dd"，解析失败则退化为「取空格前的部分」。
    /// </summary>
    private static string FormatBuyDate(string buyTime)
    {
        if (string.IsNullOrEmpty(buyTime)) return "";

        if (DateTime.TryParseExact(buyTime, "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
        {
            return exact.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (DateTime.TryParse(buyTime, out var dt))
            return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        int spaceIndex = buyTime.IndexOf(' ');
        return spaceIndex > 0 ? buyTime.Substring(0, spaceIndex) : buyTime;
    }

    private void RefreshContent(PurchaseRecord record)
    {
        // 清理旧项（对象池/复用时防止叠加）
        for (int i = _spawned.Count - 1; i >= 0; i--)
        {
            if (_spawned[i] != null) Destroy(_spawned[i].gameObject);
        }
        _spawned.Clear();

        // ★ 展示口径：按「本单购买前是否已拥有永久去广告」判定：
        //   - 买之前没有（第一次买到永久去广告，含明心/大觉/圆满礼包里的 6;1）→ 展示；
        //   - 买之前已拥有（第二次及以后再买含去广告的礼包）→ 隐藏，
        //     否则玩家每买一次这几个大礼包，记录里都会重复列一条「去广告」。
        //   新记录用购买时写入的快照；旧记录没有快照字段 → 用「更早的订单里是否已出现过永久去广告」推断。
        bool hideRemoveAds = ShopManager.ShouldHideRemoveAdsReward(
            record.produceNameId, ResolveOwnedRemoveAdsBeforePurchase(record));

        // ---------- GiftItem 方式 ----------
        Color outlineColor = Color.gray; // 记录页不强制按 produceNameId 上色，统一白色描边
        foreach (var row in record.productContent)
        {
            if (row == null || row.Count < 2) continue;

            if (hideRemoveAds && IsRemoveAdsRewardType(row[0]))
            {
                Debug.Log($"[PurchaseRecord] 购买前已拥有永久去广告 → 记录内隐藏去广告项 type={row[0]}（{record.produceNameId}）");
                continue;
            }

            if (!hideRemoveAds && IsRemoveAdsRewardType(row[0]))
            {
                Debug.Log($"[PurchaseRecord] 购买前未拥有永久去广告 → 记录内展示去广告项 type={row[0]}（{record.produceNameId}）");
            }

            var item = Instantiate(giftItemPrefab, contentParent);
            // itemid 传 0：记录页不创建 tips，避免 shopItemsTipsPanel 冲突
            item.SetBuyListData(row, 0, outlineColor);
            item.SetBonus(false);
            item.gameObject.SetActive(true);
            _spawned.Add(item);
        }
    }

    /// <summary>
    /// 「本单购买前」是否已拥有永久去广告。
    ///  - 新记录：直接用购买时写入的快照 ownedRemoveAdsBeforePurchase；
    ///  - 旧记录（字段为 null）：按「更早的订单里是否已经出现过永久去广告」推断 ——
    ///    purchaseRecords 是「最近的在前」，所以下标更大的才是更早的订单。
    ///    更早的订单里已出现过 6（或独立去广告商品）→ 本单购买前就已拥有；都没有 → 本单是第一次拿到。
    ///  - 推断不了（拿不到列表）→ 退回「当前是否已拥有」。
    /// </summary>
    private static bool ResolveOwnedRemoveAdsBeforePurchase(PurchaseRecord record)
    {
        if (record == null) return false;
        if (record.ownedRemoveAdsBeforePurchase.HasValue)
            return record.ownedRemoveAdsBeforePurchase.Value;

        var records = GameDataManager.Instance != null && GameDataManager.Instance.UserData != null
            ? GameDataManager.Instance.UserData.purchaseRecords
            : null;

        if (records != null)
        {
            int index = records.IndexOf(record);
            if (index >= 0)
            {
                for (int i = index + 1; i < records.Count; i++)
                {
                    if (ContainsPermanentRemoveAds(records[i]))
                        return true;
                }
                return false;
            }
        }

        return ShopManager.shopManager != null && ShopManager.shopManager.IsRemoveAdsOwned();
    }

    /// <summary>该订单里是否含「永久去广告」：内容里的 6，或本身就是独立去广告商品。</summary>
    private static bool ContainsPermanentRemoveAds(PurchaseRecord record)
    {
        if (record == null) return false;

        if (record.produceNameId == ShopManager.ProductRemoveAds) return true;

        if (record.productContent == null) return false;
        for (int i = 0; i < record.productContent.Count; i++)
        {
            var row = record.productContent[i];
            if (row == null || row.Count < 1) continue;

            int t;
            if (int.TryParse(row[0], out t) && t == (int)LimitRewordType.RemoveAds)
                return true;
        }
        return false;
    }

    /// <summary>是否为去广告类奖励（6 永久去广告 / 7 限时去广告）。</summary>
    private static bool IsRemoveAdsRewardType(string typeStr)
    {
        int t;
        if (!int.TryParse(typeStr, out t)) return false;
        return t == (int)LimitRewordType.RemoveAds
            || t == (int)LimitRewordType.RemoveDayAds;
    }

    private string GetTypeName(string typeStr)
    {
        if (!int.TryParse(typeStr, out int t)) return typeStr;
        switch (t)
        {
            case (int)LimitRewordType.Coins:         return "金币";
            case (int)LimitRewordType.Butterfly:     return "蝴蝶";
            case (int)LimitRewordType.Tipstool:      return "提示";
            case (int)LimitRewordType.AutoComplete:  return "火箭";
            case (int)LimitRewordType.RemoveAds:     return "去广告";
            case (int)LimitRewordType.RemoveDayAds:  return "限时去广告";
            case (int)LimitRewordType.ReStartTool:   return "复活";
            case (int)LimitRewordType.InfiniteEnergy:return "无限体力";
            default:                                 return typeStr;
        }
    }
}