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
            LogShop($"LoadBulbBoxConfig: 从 Bundle 加载 shop_bulbbox");
        }
        else
        {
            LogShop($"LoadBulbBoxConfig: 未找到 shop_bulbbox, 使用默认值");
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
    public void AddBulbExposureFail()
    {
        if (AuditConfig.IsAuditOpen)
        {
            LogShop("[Audit] AddBulbExposureFail 跳过: 审核开，不累计曝光失败分");
            return;
        }

        List<ShopDataItem> candidates = GetSlotCandidates(RecommendSlot.Bulb, 1);
        if (candidates.Count == 0)
        {
            LogShop("AddBulbExposureFail 跳过: 灯泡一号位无候选");
            return;
        }
        ShopDataItem bulb = candidates[0];

        int gold = GameDataManager.Instance.UserData.Gold;
        int toolCount = GameDataManager.Instance.UserData.toolInfo[102].count;

        if (gold >= 80 || toolCount > 0)
        {
            LogShop($"AddBulbExposureFail 跳过: {bulb.produceNameId} 条件不满足(金币={gold}, 道具={toolCount})");
            return;
        }

        ShopLimitData limit = GetOrCreateLimitData(bulb);
        if (limit == null)
        {
            LogShop($"AddBulbExposureFail 跳过: {bulb.produceNameId} 无法获取跟踪数据");
            return;
        }

        System.DateTime lastFail = limit.GetLastFailScoreTime();
        if (lastFail != System.DateTime.MinValue
            && (System.DateTime.Now - lastFail).TotalSeconds < FailScoreChangeInterval)
        {
            LogShop($"AddBulbExposureFail 跳过: {bulb.produceNameId} 距上次不足{FailScoreChangeInterval}s");
            return;
        }

        limit.conversionFailScore += 3;
        limit.SetLastFailScoreTime(System.DateTime.Now);

        LogShop($"AddBulbExposureFail: {bulb.produceNameId} 曝光失败分={limit.conversionFailScore:F1}");
    }
}