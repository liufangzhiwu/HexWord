using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ShopEntryBadgeInfo
{
    public bool showBadge;
    public string text;
    public ShopDataItem pack;
}

/// <summary>
/// 商店管理器 - 动态商店配置部分类。
/// 动态商店（收起/展开）不走审核配置，始终走原权重推荐逻辑；
/// 固定商店展开仍保留审核判断。
/// </summary>
public partial class ShopManager : MonoBehaviour
{
    private bool IsDynamicStorePack(string nameId)
    {
        bool isPack = nameId.StartsWith("Newcomer")
                      || nameId.StartsWith("SpecialPackage")
                      || nameId == "BoxTime"
                      || (nameId.StartsWith("Box") && nameId.Length == 4 && char.IsDigit(nameId[3]));

        return isPack;
    }

    private void LoadDynamicStoreConfig()
    {
        TextAsset rec = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "shop_dynamicstore");
        if (rec != null)
        {
            ParseRecommendConfigs(rec.text);
            LogShop($"LoadDynamicStoreConfig: 从 Bundle 加载 shop_dynamicstore");
        }
        else
        {
            LogShop($"LoadDynamicStoreConfig: 未找到 shop_dynamicstore, 使用默认值");
        }
    }

    /// <summary>
    /// 固定商店 - 1号推荐位。
    /// 审核开：复用审核固定商店列表；
    /// 审核关：原推送逻辑。
    /// </summary>
    public List<ShopDataItem> GetFixedStoreExpandedItems()
    {
        // if (AuditConfig.IsAuditOpen)
        // {
        //     List<ShopDataItem> auditItems = GetAuditFixedStoreItems();
        //     if (auditItems.Count > 0)
        //     {
        //         LogShop($"[Audit] GetFixedStoreExpandedItems 审核开，全部展示 {auditItems.Count} 个");
        //         return auditItems.OrderBy(item=>item.price).ToList();
        //     }
        //     LogShop("[Audit] GetFixedStoreExpandedItems 审核开但无礼包，回退常规");
        // }

        List<ShopDataItem> result = GetRecommendPacks(RecommendSlot.FixedStore);
        return result
            .OrderBy(item => item.price)
            .ToList();
    }

    /// <summary>
    /// 动态商店 - 收起状态列表。
    /// ★ 不走审核配置，始终走原权重推荐逻辑。
    /// </summary>
    public List<ShopDataItem> GetDynamicStoreCollapsedItems()
    {
        List<ShopDataItem> result = GetRecommendPacks(RecommendSlot.DynamicStore);
        return result
            .OrderBy(item => item.price)
            .ToList();
    }

    /// <summary>
    /// 动态商店 - 展开状态列表。
    /// ★ 不走审核配置，始终走原权重推荐逻辑。
    /// </summary>
    public List<ShopDataItem> GetDynamicStoreExpandedItems()
    {
        List<ShopDataItem> result = GetRecommendPacks(RecommendSlot.DynamicStore);
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

    // ======================= 消费标记读写 =======================

    private void EnsureBadgeConsumedResetIfHolidayEnded()
    {
        var userData = GameDataManager.Instance != null ? GameDataManager.Instance.UserData : null;
        if (userData == null) return;

        string until = userData.shopEntryBadgeConsumedUntil;
        if (string.IsNullOrEmpty(until)) return;

        DateTime endTime;
        if (!DateTime.TryParse(until, out endTime))
        {
            LogShop($"EnsureBadgeConsumedResetIfHolidayEnded: 结束时间格式错误('{until}')，清空标记");
            ClearBadgeConsumedUntil();
            return;
        }

        if (DateTime.Now >= endTime)
        {
            LogShop($"EnsureBadgeConsumedResetIfHolidayEnded: 当前时间已过结束时间({endTime})，清空消费标记");
            ClearBadgeConsumedUntil();
        }
    }

    private void ClearBadgeConsumedUntil()
    {
        var userData = GameDataManager.Instance != null ? GameDataManager.Instance.UserData : null;
        if (userData == null) return;

        if (string.IsNullOrEmpty(userData.shopEntryBadgeConsumedUntil)) return;

        userData.shopEntryBadgeConsumedUntil = "";
        GameDataManager.Instance.CommitGameData();
    }

    private bool IsBadgeConsumed()
    {
        var userData = GameDataManager.Instance != null ? GameDataManager.Instance.UserData : null;
        if (userData == null) return false;

        string until = userData.shopEntryBadgeConsumedUntil;
        if (string.IsNullOrEmpty(until)) return false;

        DateTime endTime;
        if (!DateTime.TryParse(until, out endTime)) return false;

        return DateTime.Now < endTime;
    }

    // ======================= 促销角标入口（审核开禁用） =======================

    public ShopEntryBadgeInfo GetShopEntryBadge()
    {
        if (IsActivityBadgeSuppressedByAudit())
            return new ShopEntryBadgeInfo { showBadge = false };

        EnsureBadgeConsumedResetIfHolidayEnded();

        if (IsBadgeConsumed())
        {
            var userData = GameDataManager.Instance?.UserData;
            LogShop($"GetShopEntryBadge: 本次节日/周末已消费(截止={userData?.shopEntryBadgeConsumedUntil}), 不展示");
            return new ShopEntryBadgeInfo { showBadge = false };
        }

        return BuildActivityBadgeInfo("GetShopEntryBadge");
    }

    public ShopEntryBadgeInfo GetHeaderActivityBadge()
    {
        if (IsActivityBadgeSuppressedByAudit())
            return new ShopEntryBadgeInfo { showBadge = false };

        EnsureBadgeConsumedResetIfHolidayEnded();

        if (IsBadgeConsumed())
        {
            var userData = GameDataManager.Instance?.UserData;
            LogShop($"GetHeaderActivityBadge: 本次节日/周末已消费(截止={userData?.shopEntryBadgeConsumedUntil}), 不展示");
            return new ShopEntryBadgeInfo { showBadge = false };
        }

        return BuildActivityBadgeInfo("GetHeaderActivityBadge");
    }

    private ShopEntryBadgeInfo BuildActivityBadgeInfo(string caller)
    {
        if (!IsHolidayOrWeekend())
        {
            LogShop($"{caller}: 非节日/周末, 不展示");
            return new ShopEntryBadgeInfo { showBadge = false };
        }

        ShopDataItem activityPack = null;
        float bestWeight = -1f;

        foreach (var kv in _recommendConfigs)
        {
            if (!IsDynamicStorePack(kv.Key)) continue;
            if (kv.Value.specialWeight == 0) continue;

            ShopDataItem item = allShopItems.FirstOrDefault(x => x.produceNameId == kv.Key);
            if (item == null || IsRemoveAdsProduct(item.produceNameId) || IsLimitedOut(item)) continue;
            if (GetCooldownCoefficient(item) <= 0f) continue;

            float weight = CalculateDecisionWeight(item, kv.Value, RecommendSlot.DynamicStore);
            if (weight > bestWeight)
            {
                bestWeight = weight;
                activityPack = item;
            }
        }

        if (activityPack == null)
        {
            LogShop($"{caller}: 无可用活动礼包, 不展示");
            return new ShopEntryBadgeInfo { showBadge = false };
        }

        ShopLimitData limit = GetOrCreateLimitData(activityPack);
        if (limit != null)
        {
            limit.isActivityPack = true;
            limit.SetLimitEndTime(GetCurrentHolidayEndTime());
        }

        string text = GetActivityBadgeText(activityPack);
        LogShop($"{caller}: 展示, pack={activityPack.produceNameId}, weight={bestWeight:F2}, text={text}");

        return new ShopEntryBadgeInfo
        {
            showBadge = true,
            text = text,
            pack = activityPack
        };
    }

    private string GetActivityBadgeText(ShopDataItem item)
    {
        if (item == null) return "";

        string zhe = item.discount;
        if (string.IsNullOrEmpty(zhe) || zhe == "0")
        {
            if (item.originalPrice > 0 && item.price > 0 && item.originalPrice >= item.price)
            {
                float ratio = (float)item.price / item.originalPrice;
                int zheNum = Mathf.CeilToInt(ratio * 10);
                zhe = zheNum.ToString();
            }
        }

        if (string.IsNullOrEmpty(zhe) || zhe == "0")
        {
            return GetTodayHolidayLabel() == "周末" ? "周末特惠" : "节日特惠";
        }

        if (GetTodayHolidayLabel() == "周末")
            return "周末" + zhe + "折";

        return "节日" + zhe;
    }

    // ======================= 进店消费（审核开不写标记） =======================

    public void OnShopEntered()
    {
        if (AuditConfig.IsAuditOpen)
        {
            LogShop("[Audit] OnShopEntered: 审核开，不写促销消费标记");
            return;
        }

        EnsureBadgeConsumedResetIfHolidayEnded();

        if (!IsHolidayOrWeekend())
        {
            LogShop("OnShopEntered: 非节日/周末, 不记录消费标记");
            return;
        }

        DateTime holidayEnd = GetCurrentHolidayEndTime();
        if (holidayEnd == DateTime.MinValue)
        {
            LogShop("OnShopEntered: 本次节日/周末结束时间无效, 跳过记录");
            return;
        }

        var userData = GameDataManager.Instance != null ? GameDataManager.Instance.UserData : null;
        if (userData == null)
        {
            LogShop("OnShopEntered: UserData 为空, 跳过记录");
            return;
        }

        userData.shopEntryBadgeConsumedUntil = holidayEnd.ToString("yyyy-MM-dd HH:mm:ss");
        GameDataManager.Instance.CommitGameData();

        LogShop($"OnShopEntered: 促销角标已消费, 本次节日/周末结束={userData.shopEntryBadgeConsumedUntil}");
    }

    private bool IsActivityPack(ShopDataItem item)
    {
        if (item == null) return false;
        ShopLimitData limit = GetLimitData(item);
        return limit != null && limit.isActivityPack;
    }
}