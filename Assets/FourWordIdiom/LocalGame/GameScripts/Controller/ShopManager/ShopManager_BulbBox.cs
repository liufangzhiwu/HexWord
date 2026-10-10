using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 商店管理器 - 提示灯（灯泡）内购配置部分类。
/// 职责：灯泡道具推荐位权重配置（BulbBox 提示灯内购配置）、灯泡曝光失败分。
/// 灯泡道具弹窗推荐位（一号主力/二号锚点/三号诱饵）的候选归属与公共决策逻辑在主脚本。
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
        }
    }

    /// <summary>
    /// 灯泡一号位曝光失败分：灯泡推荐位曝光且用户未支付
    /// （金币&lt;80 且无道具时，一次性加 3 分，间隔 30 秒）。
    /// </summary>
    public void AddBulbExposureFail()
    {
        List<ShopDataItem> candidates = GetSlotCandidates(RecommendSlot.Bulb, 1);
        if (candidates.Count == 0) return;
        ShopDataItem bulb = candidates[0];

        // 条件：金币<80 且 灯泡道具持有=0
        if (GetPlayerGold() >= 80 || GetPlayerToolCount() > 0) return;

        if ((System.DateTime.Now - bulb.lastFailScoreTime).TotalSeconds < FailScoreChangeInterval) return;
        bulb.conversionFailScore += 3;
        bulb.lastFailScoreTime = System.DateTime.Now;
    }
}
//（注：内容由AI生成）
