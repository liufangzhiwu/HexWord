using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 商店管理器 - 火箭礼盒配置部分类。
/// 职责：火箭道具推荐位权重配置（RocketBox 火箭礼盒配置表）、火箭礼盒候选归属。
/// 注意：RocketBox 表无 GiftType 列，解析时 giftType=0（付费倾向积分不作用于火箭候选）。
/// </summary>
public partial class ShopManager : MonoBehaviour
{
    /// <summary>火箭道具弹窗（RocketBox 火箭礼盒配置表）候选归属判断</summary>
    private bool IsRocketPack(string nameId)
    {
        // RocketBox 火箭礼盒配置表：RocketBox010/011/012、ItemBox02 等
        return nameId.StartsWith("RocketBox")
            || nameId == "ItemBox02";
    }

    /// <summary>
    /// 加载火箭礼盒推荐位配置（RocketBox 火箭礼盒配置表 201×20）：
    /// 第2行字段名：这列忽略,这列忽略,nameID,BaseWeight,MaxWeight,SpecialWeight,PeakWeight,ReturnWeight,Featured
    /// 无 GiftType 列（giftType=0）。优先读 Bundle，失败回退内置默认（与配置表 20260915 一致）。
    /// </summary>
    private void LoadRocketBoxConfig()
    {
        TextAsset rec = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "shop_rocketbox");
        if (rec != null)
        {
            ParseRecommendConfigs(rec.text);
        }
    }
}
//（注：内容由AI生成）
