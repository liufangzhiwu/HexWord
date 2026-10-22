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
/// 购买跟踪数据（isActivityPack / limitEndTime / buyCount / lastBuyTime 等）统一从 ShopLimitData 读取。
///
/// ★ 促销角标消费标记统一存储在 GameDataManager.Instance.UserData.shopEntryBadgeConsumedUntil：
///   - 随 UserData 一起序列化到服务器，卸载重装可恢复；
///   - 语义：记录"本次节日/周末的结束时间"作为消费截止时间；
///   - 当前时间 < 截止时间 → 本次节日/周末内不再显示；
///   - 当前时间 >= 截止时间 → 自动清空，进入下一个节日/周末可重新显示。
/// </summary>
public partial class ShopManager : MonoBehaviour
{
    // ======================= 促销角标（进店消失 + 持久化 + 节日/周末结束自动重置） =======================

    /// <summary>动态商店/固定商店1号推荐位（DynamicStore 动态商店配置）候选归属判断</summary>
    private bool IsDynamicStorePack(string nameId)
    {
        // 动态商店推荐位配置表内的礼包：NewcomerBox / SpecialPackageA / SpecialPackageB / BoxTime / Box0~Box4
        bool isPack = nameId.StartsWith("Newcomer")
                      || nameId.StartsWith("SpecialPackage")
                      || nameId == "BoxTime"
                      || (nameId.StartsWith("Box") && nameId.Length == 4 && char.IsDigit(nameId[3]));

        return isPack;
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
            LogShop($"LoadDynamicStoreConfig: 从 Bundle 加载 shop_dynamicstore");
        }
        else
        {
            LogShop($"LoadDynamicStoreConfig: 未找到 shop_dynamicstore, 使用默认值");
        }
    }

    /// <summary>固定商店 - 1号推荐位。</summary>
    public List<ShopDataItem> GetFixedStoreExpandedItems()
    {
        List<ShopDataItem> result = GetRecommendPacks(RecommendSlot.FixedStore);
        return result
            .OrderBy(item => item.price)
            .ToList();
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

    /// <summary>动态商店 - 展开状态列表：展开后展示全部礼包，按价格升序排列。</summary>
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

    // ======================= 消费标记读写（统一走 UserData） =======================

    /// <summary>
    /// 检查并处理"本次节日/周末已结束"的自动清空。
    /// 每次读取角标前调用一次；若当前时间 >= UserData.shopEntryBadgeConsumedUntil，则清空该字段。
    /// 数据来自 GameDataManager.Instance.UserData（随 UserData 同步到服务器）。
    /// </summary>
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
            LogShop($"EnsureBadgeConsumedResetIfHolidayEnded: 当前时间已过结束时间({endTime})，清空消费标记（进入新节日/周末可重新显示）");
            ClearBadgeConsumedUntil();
        }
    }

    /// <summary>清空持久化的消费标记（写回 UserData 并保存）</summary>
    private void ClearBadgeConsumedUntil()
    {
        var userData = GameDataManager.Instance != null ? GameDataManager.Instance.UserData : null;
        if (userData == null) return;

        if (string.IsNullOrEmpty(userData.shopEntryBadgeConsumedUntil)) return;

        userData.shopEntryBadgeConsumedUntil = "";
        GameDataManager.Instance.CommitGameData();
    }

    /// <summary>当前是否处于"本次节日/周末已消费"状态（消费截止时间 > 当前时间）</summary>
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

    // ======================= 促销角标入口 =======================

    /// <summary>
    /// 商店入口按钮角标（需求）：
    ///  - 原"大减价"角标隐藏；
    ///  - 动态商店有活动礼包可出时展示"节日X折/周末X折"；
    ///  - 角标在玩家进入商店后消失（进店消失），本次节日/周末内不再显示；
    ///  - 持久化：重启游戏/卸载重装仍保持消失；只有进入下一个节日/周末才重新显示。
    /// </summary>
    public ShopEntryBadgeInfo GetShopEntryBadge()
    {
        EnsureBadgeConsumedResetIfHolidayEnded();

        if (IsBadgeConsumed())
        {
            var userData = GameDataManager.Instance?.UserData;
            LogShop($"GetShopEntryBadge: 本次节日/周末已消费(截止={userData?.shopEntryBadgeConsumedUntil}), 不展示");
            return new ShopEntryBadgeInfo { showBadge = false };
        }

        return BuildActivityBadgeInfo("GetShopEntryBadge");
    }

    /// <summary>
    /// HeaderSection 顶部金币栏旁的促销角标（需求）：
    ///  - 出现节日/周末礼包时 sale 显示；
    ///  - 文案为"节日X折"或"周末X折"；
    ///  - 进店后消失（与商店入口角标共用消费标记），本次节日/周末内不再显示；
    ///  - 持久化：重启游戏/卸载重装仍保持消失；只有进入下一个节日/周末才重新显示。
    /// </summary>
    public ShopEntryBadgeInfo GetHeaderActivityBadge()
    {
        EnsureBadgeConsumedResetIfHolidayEnded();

        if (IsBadgeConsumed())
        {
            var userData = GameDataManager.Instance?.UserData;
            LogShop($"GetHeaderActivityBadge: 本次节日/周末已消费(截止={userData?.shopEntryBadgeConsumedUntil}), 不展示");
            return new ShopEntryBadgeInfo { showBadge = false };
        }

        return BuildActivityBadgeInfo("GetHeaderActivityBadge");
    }

    /// <summary>
    /// 构造活动礼包角标信息（选最优礼包 + 写入限时 + 生成文案）。
    /// 商店入口角标与 HeaderSection 角标共用此方法。
    /// </summary>
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

        // 标记该礼包为活动礼包，并写入限时截至时间（与 DecideWinner 保持一致）
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

    /// <summary>
    /// 促销角标文案："节日X折"或"周末X折"。
    /// 折扣数字 X 优先取礼包 discount，为空时用价格反推（原价/售价）。
    /// 无有效折扣时退回类型文案（周末特惠/节日特惠）。
    /// </summary>
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
            return "周末" + zhe;

        return "节日" + zhe;
    }

    // ======================= 进店消费（持久化到 UserData） =======================

    /// <summary>
    /// 玩家进入商店时调用：促销角标消失（商店入口按钮角标 + HeaderSection 角标同时生效）。
    ///
    /// ★ 持久化规则：
    ///   - 记录"本次节日/周末的结束时间"到 UserData.shopEntryBadgeConsumedUntil；
    ///   - 当前时间 < 截止时间 → 本次节日/周末内不再显示（重启游戏/卸载重装仍保持）；
    ///   - 当前时间 >= 截止时间 → 自动清空标记（进入下一个节日/周末可重新显示）。
    /// </summary>
    public void OnShopEntered()
    {
        // 先检查是否已过旧的节日/周末结束时间（清空旧标记）
        EnsureBadgeConsumedResetIfHolidayEnded();

        // 非节日/周末进店 → 不记录消费标记（避免影响下一个节日/周末的显示）
        if (!IsHolidayOrWeekend())
        {
            LogShop("OnShopEntered: 非节日/周末, 不记录消费标记");
            return;
        }

        // 记录本次节日/周末的结束时间
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

        LogShop($"OnShopEntered: 促销角标已消费, 本次节日/周末结束={userData.shopEntryBadgeConsumedUntil}（重启游戏/卸载重装仍保持消失，直到节日/周末结束）");
    }

    /// <summary>读取指定商品是否为活动礼包（统一从 ShopLimitData 读取）。</summary>
    private bool IsActivityPack(ShopDataItem item)
    {
        if (item == null) return false;
        ShopLimitData limit = GetLimitData(item);
        return limit != null && limit.isActivityPack;
    }
}