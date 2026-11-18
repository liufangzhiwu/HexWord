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
    /// 本次**灯泡界面**实际展示的 1 号位礼包。必须由调用方（GetItemScreen.Close）传入；
    /// 传 null / 非灯泡包时直接跳过，不做任何兜底猜测。
    /// 原因：GetItemScreen 是灯泡/火箭/复活等道具界面共用的，用兜底会在非灯泡界面
    /// 给灯泡礼包误记曝光失败分（曾复现：火箭界面关闭 → BulbBox01 F+3）。
    /// </param>
    public void AddBulbExposureFail(ShopDataItem shownPack = null)
    {
        if (AuditConfig.IsAuditOpen)
        {
            LogShop("[Audit] AddBulbExposureFail 跳过: 审核开，不累计曝光失败分");
            return;
        }

        // ---------- 1. 必须由调用方传入"本次灯泡界面实际展示的礼包" ----------
        //   ★ 不再回退「本关展示记录 / 候选池第一个」：
        //     火箭（AutoComplete）、复活（ReStartTool）等界面用的是同一个 GetItemScreen，
        //     关闭时如果传 null 再让这里兜底，就会把 F+3 记到灯泡一号位身上（已复现：
        //     火箭界面关闭 → BulbBox01 曝光失败分=9/12）。
        //     宁可跳过也不要在没曝光灯泡礼包时乱记分。
        if (shownPack == null || !IsBulbPack(shownPack.produceNameId))
        {
            LogShop($"AddBulbExposureFail 跳过: 未传入灯泡一号位展示礼包(shown={(shownPack == null ? "null" : shownPack.produceNameId)})，" +
                    "非灯泡界面不累计曝光失败分");
            return;
        }

        string source = "调用方传入";
        ShopDataItem bulb = shownPack;

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

        // ★ F 每变化一次要间隔 FailScoreChangeInterval(30s) 才能再变（统一节流）
        if (!TryBeginFailScoreChange(limit, "曝光失败+3"))
            return;

        float failBefore = limit.conversionFailScore;
        limit.conversionFailScore = failBefore + 3;
        ApplyFailScoreChanged(limit, failBefore, limit.conversionFailScore, "曝光失败+3", $"来源={source}");

        // ★ F 是决策权重输入，必须真正落盘：否则玩家直接杀进程 / 断电重启就白记了。
        //   用延迟提交合并写盘（1.5s），不占这一帧的 IO。
        if (GameDataManager.Instance != null)
            GameDataManager.Instance.CommitGameDataDelayed();

        // ---------- 2. 写后回读：确认这条记录真的在存档表里（不是孤儿对象） ----------
        List<ShopLimitData> table = GameDataManager.Instance != null && GameDataManager.Instance.UserData != null
            ? GameDataManager.Instance.UserData.limitShopItems
            : null;
        bool inList = table != null && table.Contains(limit);
        int listCount = table != null ? table.Count : -1;

         Debug.Log($"[Shop]AddBulbExposureFail: {bulb.produceNameId} 曝光失败分={limit.conversionFailScore:F1} " +
                  $"下次可变化倒计时={GetFailScoreCooldownRemain(limit):F1}s, 来源={source} " +
                  $"inList={inList} listCount={listCount} cacheCount={shoplimitDatas.Count}, " +
                  $"nameid={limit.nameid}, id={limit.id}");

        if (!inList)
        {
            Debug.LogError($"[Shop]AddBulbExposureFail 写入落在表外(孤儿对象)! {bulb.produceNameId} " +
                           $"listCount={listCount} cacheCount={shoplimitDatas.Count} → 该失败分不会被存档，重进游戏会归零");
        }
    }
}