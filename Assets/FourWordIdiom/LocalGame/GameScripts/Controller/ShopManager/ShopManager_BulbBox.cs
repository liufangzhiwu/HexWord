using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 商店管理器 - 提示灯（灯泡）内购配置部分类。
/// 职责：灯泡道具推荐位权重配置（BulbBox 提示灯内购配置）、灯泡曝光失败分。
/// 灯泡道具弹窗推荐位（一号主力/二号锚点/三号诱饵）的候选归属与公共决策逻辑在主脚本。
///
/// 购买跟踪数据（payTendencyScore / exploreScore / conversionFailScore /
/// lastFailScoreTime / lastBuyTime / limitEndTime / isActivityPack / pushStateIndex）
/// 统一存储在 ShopLimitData 中，并挂在 GameDataManager.UserData.limitShopItems 下持久化。
/// </summary>
public partial class ShopManager : MonoBehaviour
{
    /// <summary>灯泡道具弹窗（BulbBox 提示灯内购配置）候选归属判断</summary>
    private bool IsBulbPack(string nameId)
    {
        // BulbBox 提示灯内购配置表：BulbBox01~06、ItemBox01（灯泡+金币）、GoldBag1/2（金币）、LuckyBag1（福袋）
        return nameId.StartsWith("BulbBox")
            || nameId == "ItemBox01"
            || nameId == "GoldBag1"
            || nameId == "GoldBag2"
            || nameId == "LuckyBag1";
    }

    /// <summary>
    /// 加载灯泡道具推荐位配置（BulbBox 提示灯内购配置 202×23）：
    /// 第2行字段名：这列忽略,这列忽略,nameID,GiftType,BaseWeight,MaxWeight,SpecialWeight,PeakWeight,ReturnWeight,Featured
    /// 优先读 Bundle，失败回退内置默认（与配置表 20260915 一致）。
    /// </summary>
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
    /// 灯泡一号位曝光失败分：灯泡推荐位曝光且用户未支付
    /// （金币&lt;80 且无道具时，一次性加 3 分，间隔 30 秒）。
    /// 跟踪数据统一写入 ShopLimitData。
    /// </summary>
    public void AddBulbExposureFail()
    {
        List<ShopDataItem> candidates = GetSlotCandidates(RecommendSlot.Bulb, 1);
        if (candidates.Count == 0)
        {
            LogShop("AddBulbExposureFail 跳过: 灯泡一号位无候选");
            return;
        }
        ShopDataItem bulb = candidates[0];
        
        int gold=GameDataManager.Instance.UserData.Gold;
        int toolCount=GameDataManager.Instance.UserData.toolInfo[102].count;

        // 条件：金币<80 且 灯泡道具持有=0
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