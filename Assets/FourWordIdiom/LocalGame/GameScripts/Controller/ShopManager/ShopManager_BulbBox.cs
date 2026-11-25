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
    /// <param name="goldSnapshot">曝光那一刻的金币数（弹窗打开时快照）。
    /// 需求口径是「曝光时金币 &lt; 80」，不是「关闭时」——玩家在弹窗内看了广告领金币后关闭，
    /// 关闭时金币已经 ≥80，用实时值会把这次未付费曝光漏记。传 null 才退回实时值。</param>
    /// <param name="bulbSnapshot">曝光那一刻的灯泡（提示灯 toolInfo[102]）持有数。</param>
    /// <param name="rocketSnapshot">曝光那一刻的火箭（toolInfo[104]）持有数。需求要求灯泡和火箭都为 0。</param>
    /// <param name="accompanyCancel">
    /// 是否伴随「点击支付又退出」的 +5 一起结算（同一次 F 变化 = +5 + 3）。
    /// true 时豁免 30 秒节流：+5 刚在取消回调里写完时间戳，不豁免的话这笔 +3 必被自己的冷却拦掉。
    /// 只有本次 +5 真的加上（payfailapplied）才允许传 true，避免重复取消时绕过节流。
    /// </param>
    public void AddBulbExposureFail(ShopDataItem shownPack = null,
                                    int? goldSnapshot = null,
                                    int? bulbSnapshot = null,
                                    int? rocketSnapshot = null,
                                    bool accompanyCancel = false)
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

        // ★ 档位不参与本规则的判定：灯泡一号位出什么礼包（含 0.99 的第 1 档 BulbBox01），
        //   只要满足「金币<80 且 灯泡=0 且 火箭=0 且 本次曝光未支付」就 +3。
        //   （需求确认口径：这条曝光失败分是针对"1 号位推荐位"的，与礼包档位无关。）

        UserData userData = GameDataManager.Instance != null ? GameDataManager.Instance.UserData : null;
        int gold = goldSnapshot ?? (userData != null ? userData.Gold : 0);
        int bulbCount = bulbSnapshot ?? GetToolCount(userData, 102);
        int rocketCount = rocketSnapshot ?? GetToolCount(userData, 104);

        // 需求：金币 < 80，且灯泡、火箭持有数都为 0
        if (gold >= 80 || bulbCount > 0 || rocketCount > 0)
        {
             Debug.Log($"[Shop]AddBulbExposureFail 跳过: {bulb.produceNameId} 条件不满足(金币={gold}, 灯泡={bulbCount}, 火箭={rocketCount})");
            return;
        }

        ShopLimitData limit = GetOrCreateLimitData(bulb);
        if (limit == null)
        {
             Debug.Log($"[Shop]AddBulbExposureFail 跳过: {bulb.produceNameId} 无法获取跟踪数据");
            return;
        }

        // ★ F 每变化一次要间隔 FailScoreChangeInterval(30s) 才能再变（统一节流）
        //   accompanyCancel=true：这笔 +3 是同一次「取消支付」事件的第二个组成部分，豁免节流。
        string tag = accompanyCancel ? "曝光失败+3(随取消同事件)" : "曝光失败+3";
        if (!TryBeginFailScoreChange(limit, tag, ignoreCooldown: accompanyCancel))
            return;

        float failBefore = limit.conversionFailScore;
        limit.conversionFailScore = failBefore + 3;
        ApplyFailScoreChanged(limit, failBefore, limit.conversionFailScore, tag,
                              $"来源={source}{(accompanyCancel ? "，同事件+5已计" : "")}");

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

    /// <summary>取道具持有数（toolInfo 可能在旧存档里缺 key，做一次判空）。</summary>
    private static int GetToolCount(UserData userData, int toolId)
    {
        if (userData == null || userData.toolInfo == null) return 0;
        ToolInfo info;
        return userData.toolInfo.TryGetValue(toolId, out info) && info != null ? info.count : 0;
    }
}