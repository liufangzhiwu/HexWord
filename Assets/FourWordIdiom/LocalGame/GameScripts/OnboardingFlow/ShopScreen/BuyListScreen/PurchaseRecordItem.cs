using System.Collections.Generic;
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
    [SerializeField] private Image iconImage;       // 可选：礼包底图

    [Header("礼包内容（GiftItem 方式）")]
    [SerializeField] private Transform contentParent;   // 内容父节点（Grid/VerticalLayout）
    [SerializeField] private GiftItem giftItemPrefab;   // 单个礼包内容项 Prefab

    [Header("礼包内容（纯文本方式，可选）")]
    [SerializeField] private Text contentText;      // 回退文本（如 "金币 ×1000  蝴蝶 ×2"）

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
        if (timeText != null)
            timeText.text = record.buyTime ?? "";

        // ---------- 底图（可选） ----------
        if (iconImage != null && !string.IsNullOrEmpty(record.showIcon))
        {
            var sprite = AdvancedBundleLoader.SharedInstance?.GetSpriteFromAtlas(record.showIcon+"_Icon");
            if (sprite != null) iconImage.sprite = sprite;
        }

        // ---------- 礼包内容 ----------
        RefreshContent(record);
    }

    private void RefreshContent(PurchaseRecord record)
    {
        // 清理旧项（对象池/复用时防止叠加）
        for (int i = _spawned.Count - 1; i >= 0; i--)
        {
            if (_spawned[i] != null) Destroy(_spawned[i].gameObject);
        }
        _spawned.Clear();

        if (record.productContent == null || record.productContent.Count == 0)
        {
            if (contentText != null) contentText.text = "（无内容数据）";
            return;
        }

        bool useGiftItems = contentParent != null && giftItemPrefab != null;

        // ---------- 回退：纯文本 ----------
        if (!useGiftItems)
        {
            if (contentText != null)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var row in record.productContent)
                {
                    if (row == null || row.Count < 2) continue;
                    sb.Append(GetTypeName(row[0])).Append(" ×").Append(row[1]).Append("  ");
                }
                contentText.text = sb.ToString().TrimEnd();
            }
            return;
        }

        // ---------- GiftItem 方式 ----------
        Color outlineColor = Color.gray; // 记录页不强制按 produceNameId 上色，统一白色描边
        foreach (var row in record.productContent)
        {
            if (row == null || row.Count < 2) continue;

            var item = Instantiate(giftItemPrefab, contentParent);
            // itemid 传 0：记录页不创建 tips，避免 shopItemsTipsPanel 冲突
            item.SetShopData(row, 0, outlineColor);
            item.SetBonus(false);
            item.gameObject.SetActive(true);
            _spawned.Add(item);
        }
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