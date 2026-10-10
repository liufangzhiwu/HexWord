using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 商店入口角标数据（需求：隐藏"大减价"，动态商店出活动礼包时展示"节日X折/周末X折"角标，进店消失）
/// </summary>
public class ShopEntryBadgeInfo
{
    public bool showBadge;          // 是否显示角标
    public string text;             // 角标文案："节日X折"/"周末X折"
    public ShopDataItem pack;       // 触发角标的活动礼包（进店后消失的对应商品）
}

/// <summary>
/// 商店管理器 - 动态商店配置部分类。
/// 职责：动态商店推荐位列表（收起/展开、一号位兜底规则）、商店入口角标、活动礼包命名。
/// </summary>
public partial class ShopManager : MonoBehaviour
{
    /// <summary>本次进入商店是否已消费入口角标（进店消失）</summary>
    private bool _shopEntryBadgeConsumed;

    /// <summary>动态商店（DynamicStore 动态商店配置）候选归属判断</summary>
    private bool IsDynamicStorePack(string nameId)
    {
        // 动态商店推荐位配置表内的礼包：NewcomerBox / SpecialPackageA / SpecialPackageB / BoxTime / Box0~Box4
        return nameId.StartsWith("Newcomer")
            || nameId.StartsWith("SpecialPackage")
            || nameId == "BoxTime"
            || (nameId.StartsWith("Box") && nameId.Length == 4 && char.IsDigit(nameId[3]));
    }

    /// <summary>
    /// 加载动态商店推荐位配置（DynamicStore 动态商店配置 200×20）：
    /// 表头：价格/商品名（发布用）/商品名ID/礼包档位类型/基础权重/礼包权重上限/节日周末权重/高峰权重/流失回归/内购推荐位
    /// 内购推荐位：1=一号位 2=二号位（推荐位非0才算候选）
    /// </summary>
    private void LoadDynamicStoreConfig()
    {
        TextAsset rec = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "shop_dynamicstore");
        if (rec != null)
        {
            ParseRecommendConfigs(rec.text);
        }
    }

    /// <summary>
    /// 动态商店 - 收起状态列表（一号位 + 二号位，按价格升序）。
    /// 一号位兜底：若可展示礼包决策权重全为 0 且去广告未购买 → 展示去广告；
    /// 去广告已购买 → 二号位第1、2权重的礼包都展示。
    /// </summary>
    public List<ShopDataItem> GetDynamicStoreCollapsedItems()
    {
        List<ShopDataItem> result = GetRecommendPacks(RecommendSlot.DynamicStore);
        return result
            .OrderBy(item => item.price)
            .ToList();
    }

    /// <summary>
    /// 动态商店 - 展开状态列表：展开后展示全部礼包，按价格升序排列。
    /// </summary>
    public List<ShopDataItem> GetDynamicStoreExpandedItems()
    {
        List<ShopDataItem> result = GetRecommendPacks(RecommendSlot.DynamicStore);
        // 展开后加入二号位剩余候选礼包（价格升序）
        List<ShopDataItem> all = GetSlotCandidates(RecommendSlot.DynamicStore, 2);
        foreach (var item in all)
        {
            if (!result.Contains(item))
                result.Add(item);
        }
        return result
            .OrderBy(item => item.price)
            .ToList();
    }

    /// <summary>
    /// 商店入口角标（需求）：
    ///  - 原"大减价"角标隐藏；
    ///  - 动态商店有活动礼包可出时展示"节日X折/周末X折"；
    ///  - 角标在玩家进入商店后消失（_shopEntryBadgeConsumed）。
    /// </summary>
    public ShopEntryBadgeInfo GetShopEntryBadge()
    {
        if (_shopEntryBadgeConsumed)
            return new ShopEntryBadgeInfo { showBadge = false };

        // 动态商店候选活动礼包（节日/周末权重≠0 且当天是节日/周末）
        ShopDataItem activityPack = null;
        foreach (var kv in _recommendConfigs)
        {
            if (!IsDynamicStorePack(kv.Key)) continue;
            if (kv.Value.specialWeight == 0) continue;
            ShopDataItem item = allShopItems.FirstOrDefault(x => x.produceNameId == kv.Key);
            if (item == null || IsRemoveAdsProduct(item.produceNameId) || IsLimitedOut(item)) continue;
            if (GetCooldownCoefficient(item) <= 0f) continue;
            activityPack = item;
            break;
        }

        if (activityPack == null || !IsHolidayOrWeekend())
            return new ShopEntryBadgeInfo { showBadge = false };

        string text = GetDiscountZheText(activityPack);
        return new ShopEntryBadgeInfo
        {
            showBadge = true,
            text = text,
            pack = activityPack
        };
    }

    /// <summary>玩家进入商店时调用：入口角标消失</summary>
    public void OnShopEntered()
    {
        _shopEntryBadgeConsumed = true;
    }

    /// <summary>
    /// 折扣文案："节日X折/周末X折"（活动礼包在节日/周末时展示）。
    /// 示例：周末2折 → "周末2折"；国庆节 → "国庆节X折"。
    /// </summary>
    public string GetDiscountZheText(ShopDataItem item)
    {
        if (item == null) return "";

        // 折扣列：discount 存折扣（如 "2" 表示2折）
        string zhe = item.discount;
        if (string.IsNullOrEmpty(zhe) || zhe == "0")
        {
            // 用价格反推折扣：原价/售价（新表）
            if (item.originalPrice > 0 && item.price > 0 && item.originalPrice >= item.price)
            {
                float ratio = item.price / item.originalPrice;
                int zheNum = Mathf.CeilToInt(ratio * 10);
                zhe = zheNum.ToString();
            }
            else
            {
                return "特惠";
            }
        }

        string holidayLabel = GetTodayHolidayLabel();
        if (holidayLabel == "周末")
            return "周末" + zhe + "折";
        if (!string.IsNullOrEmpty(holidayLabel))
            return holidayLabel + zhe + "折";
        return zhe + "折";
    }

    /// <summary>
    /// 活动礼包名称规则（需求：{0}特惠礼包 key=SpecialOffer）：
    /// 平时展示"特惠礼包"；节日替换为"XX节特惠礼包"；周末为"周末特惠礼包"。
    /// </summary>
    public string GetActivityPackName(ShopDataItem item)
    {
        if (item == null || !item.isActivityPack)
            return Localize("SpecialOffer");

        string label = GetTodayHolidayLabel();
        if (label == "周末")
            return "周末特惠礼包";
        if (!string.IsNullOrEmpty(label))
            return label + "特惠礼包";
        return "特惠礼包";
    }
}
//（注：内容由AI生成）
