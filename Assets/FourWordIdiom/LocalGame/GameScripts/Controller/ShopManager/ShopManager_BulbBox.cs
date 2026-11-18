using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 商店管理器 - 提示灯（灯泡）内购配置部分类。
/// </summary>
public partial class ShopManager : MonoBehaviour
{
    private bool IsBulbPack(string nameId)
    {
        return nameId.StartsWith("BulbBox")
            || nameId == "ItemBox01"
            || nameId == "GoldBag1"
            || nameId == "GoldBag2"
            || nameId == "LuckyBag1"
            || nameId == "LuckyBag2";
    }

    private void LoadBulbBoxConfig()
    {
        TextAsset rec = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "shop_bulbbox");
        if (rec != null)
        {
            ParseRecommendConfigs(rec.text);
             Debug.Log($"[Shop]LoadBulbBoxConfig: 从 Bundle 加载 shop_bulbbox");
        }
        else
        {
             Debug.Log($"[Shop]LoadBulbBoxConfig: 未找到 shop_bulbbox, 使用默认值");
        }
    }

    /// <summary>
    /// 获取总共提示灯道具数量
    /// </summary>
    /// <returns></returns>
    public int GetBulbBoxCount()
    {
        return 11;
    }

    /// <summary>
    /// 灯泡一号位曝光失败分。
    /// 审核开时内购特殊处理不生效，直接跳过。
    /// </summary>
    /// <param name="shownPack">
    /// 本次界面**实际展示**的 1 号位礼包。由调用方（GetItemScreen.Close）传入最可靠；
    /// 传 null 时依次回退到「本关该推荐位已展示记录」→「候选池第一个」（兜底，会打警告）。
    /// 不能用候选池第一个当默认值：候选顺序 ≠ 实际决策展示的礼包，会记到没曝光的包上。
    /// </param>
    public void AddBulbExposureFail(ShopDataItem shownPack = null)
    {
        if (AuditConfig.IsAuditOpen)
        {
            LogShop("[Audit] AddBulbExposureFail 跳过: 审核开，不累计曝光失败分");
            return;
        }

        // ---------- 1. 确定"实际展示的礼包"：调用方传入 → 本关展示记录 → 候选池兜底 ----------
        string source;
        ShopDataItem bulb = null;

        if (shownPack != null && IsBulbPack(shownPack.produceNameId))
        {
            bulb = shownPack;
            source = "调用方传入";
        }
        else
        {
            ShopDataItem lastShown = GetLastShownPack(RecommendSlot.Bulb, 1);
            if (lastShown != null && IsBulbPack(lastShown.produceNameId))
            {
                bulb = lastShown;
                source = "本关已展示记录";
            }
            else
            {
                List<ShopDataItem> candidates = GetSlotCandidates(RecommendSlot.Bulb, 1);
                if (candidates.Count == 0)
                {
                    LogShop("AddBulbExposureFail 跳过: 灯泡一号位无候选");
                    return;
                }
                bulb = candidates[0];
                source = "候选池兜底";
                 Debug.LogWarning($"[Shop]AddBulbExposureFail 未拿到展示礼包，回退候选池第一个: {bulb.produceNameId}（可能与实际展示不符）");
            }
        }

        int gold = GameDataManager.Instance.UserData.Gold;
        int toolCount = GameDataManager.Instance.UserData.toolInfo[102].count;

        if (gold >= 80 || toolCount > 0)
        {
             Debug.Log($"[Shop]AddBulbExposureFail 跳过: {bulb.produceNameId} 条件不满足(金币={gold}, 道具={toolCount})");
            return;
        }

        ShopLimitData limit = GetOrCreateLimitData(bulb);
        if (limit == null)
        {
             Debug.Log($"[Shop]AddBulbExposureFail 跳过: {bulb.produceNameId} 无法获取跟踪数据");
            return;
        }

        System.DateTime lastFail = limit.GetLastFailScoreTime();
        if (lastFail != System.DateTime.MinValue
            && (System.DateTime.Now - lastFail).TotalSeconds < FailScoreChangeInterval)
        {
             Debug.Log($"[Shop]AddBulbExposureFail 跳过: {bulb.produceNameId} 距上次不足{FailScoreChangeInterval}s");
            return;
        }

        limit.conversionFailScore += 3;
        limit.SetLastFailScoreTime(System.DateTime.Now);

        // ---------- 2. 写后回读：确认这条记录真的在存档表里（不是孤儿对象） ----------
        List<ShopLimitData> table = GameDataManager.Instance != null && GameDataManager.Instance.UserData != null
            ? GameDataManager.Instance.UserData.limitShopItems
            : null;
        bool inList = table != null && table.Contains(limit);
        int listCount = table != null ? table.Count : -1;

         Debug.Log($"[Shop]AddBulbExposureFail: {bulb.produceNameId} 曝光失败分={limit.conversionFailScore:F1} " +
                  $"来源={source} inList={inList} listCount={listCount} cacheCount={shoplimitDatas.Count}, " +
                  $"nameid={limit.nameid}, id={limit.id}");

        if (!inList)
        {
            Debug.LogError($"[Shop]AddBulbExposureFail 写入落在表外(孤儿对象)! {bulb.produceNameId} " +
                           $"listCount={listCount} cacheCount={shoplimitDatas.Count} → 该失败分不会被存档，重进游戏会归零");
        }
    }
}