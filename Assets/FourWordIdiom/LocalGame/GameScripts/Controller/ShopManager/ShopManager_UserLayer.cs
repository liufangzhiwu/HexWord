using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 用户分层配置（来源：商店表"用户分层付费设计20260906（禅意之境）/（成语消消闯关）"）
/// 相关字段：用户标记 AdTracking（开关_活跃天数）与 AdTrackingStimulate（S_G）、
///          AdFailed（S_F_T_A_B_C 广告失败策略）、BrokenIce（破冰礼包冷却 P）、
///          InterAdsGroup（插屏分组）、特惠礼包主动弹出（开/关，第11列）。
/// </summary>
[Serializable]
public class ShopLayerConfig
{
    public string userLayer = "IAA11";   // 当前用户分层
    public string adFailed = "1_1_60_4_-1_3";   // 广告失败策略 S_F_T_A_B_C
    public string brokenIce = "1_1";      // 破冰礼包策略（开关_P冷却参数）
    public string interAdsGroup = "0";    // 插屏分组（插屏>3 次数判断用）
    public string adTrackingStimulate = "0_0"; // 广告追踪刺激 S_G（活跃天数/金币数）
    public bool allowSpecialPopup = true; // 特惠礼包主动弹出（开/关）
}

/// <summary>
/// 广告失败策略参数（解析自 S_F_T_A_B_C）：
/// S=商店金币视频日上限  F=分享日上限  T=提示道具广告失败弹窗冷却(秒)
/// A=单关道具视频次数(灯泡/复活)  B=金币视频间隔秒  C=3600s冷却后状态(1=可再弹 0=不可)
/// </summary>
public struct AdFailedParams
{
    public int shopGoldVideoDailyLimit; // S
    public int shareDailyLimit;         // F
    public float tipFailCooldown;       // T
    public int toolVideoPerLevel;       // A（灯泡/复活每关次数 X/a）
    public int goldVideoInterval;       // B（金币视频间隔秒）
    public int cooldownState;           // C（3600s冷却后的状态：1 可再弹）
    public bool isCooldown;             // 当前是否处于冷却状态 C
}

/// <summary>
/// 商店管理器 - 用户分层设计部分类。
/// 职责：用户分层配置加载与查询、广告获取失败策略（提示道具视频/金币视频/分享）、
///      破冰礼包策略、复购/升层策略。分层与分层跳转（L层升级）由外部分层模块驱动。
/// </summary>
public partial class ShopManager : MonoBehaviour
{
    /// <summary>当前用户分层配置</summary>
    private ShopLayerConfig _layerConfig = new ShopLayerConfig();

    // ======================= 广告失败策略运行状态 =======================

    /// <summary>提示道具（灯泡/复活）每关已观看视频次数：type → count</summary>
    private Dictionary<LimitRewordType, int> _levelVideoUsed = new Dictionary<LimitRewordType, int>();

    /// <summary>商店金币视频当日已领取次数</summary>
    private int _shopGoldUsedToday;
    private string _shopGoldUsedDate = "";

    /// <summary>分享当日成功次数</summary>
    private int _shareSuccessToday;
    private string _shareSuccessDate = "";

    /// <summary>广告失败弹窗冷却截止时间（3600s 状态 C）</summary>
    private DateTime _adFailBlockedUntil;

    /// <summary>道具视频类型（灯泡提示灯 / 复活）</summary>
    public enum ToolVideoType
    {
        Bulb = (int)LimitRewordType.AutoComplete,
        Clock = 0 // TODO：对齐复活道具在 LimitRewordType 中的枚举值
    }

    /// <summary>金币视频状态（商店金币视频：每次看完 100 金币）</summary>
    public struct ShopGoldVideoState
    {
        public bool used;         // 当天是否已看
        public int total;         // 当天总次数（S 上限）
        public bool freeReady;    // 是否满足观看条件
    }

    // ======================= 分层加载 =======================

    /// <summary>
    /// 加载用户分层配置（用户分层付费设计20260906）：
    /// 第2行字段名：用户分层 | … | AdFailed | BrokenIce | InterAdsGroup | AdTrackingStimulate | 特惠礼包主动弹出
    /// 优先读 Bundle，失败回退内置默认（IAA11 档）。
    /// </summary>
    private void LoadLayerConfig()
    {
        TextAsset layer = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "shop_layer");
        if (layer != null)
        {
            ParseLayerConfigs(layer.text);
            return;
        }
        LoadDefaultLayerConfig();
    }

    private void ParseLayerConfigs(string data)
    {
        var records = SplitCsvLines(data);
        // 定位字段名行
        int nameRow = -1;
        for (int i = 0; i < Math.Min(records.Count, 4); i++)
        {
            if (records[i].Count > 0 && records[i][0] == "用户分层")
            {
                nameRow = i;
                break;
            }
        }
        if (nameRow < 0) return;

        var header = records[nameRow];
        Dictionary<string, int> colMap = new Dictionary<string, int>();
        for (int j = 0; j < header.Count; j++)
        {
            string h = (header[j] ?? "").Trim();
            if (!string.IsNullOrEmpty(h) && !colMap.ContainsKey(h))
                colMap[h] = j;
        }

        string currentLayer = GetCurrentUserLayer();

        for (int i = nameRow + 1; i < records.Count; i++)
        {
            var f = records[i];
            if (f.Count < 6) continue;
            if (GetField(f, 0) != currentLayer) continue;

            _layerConfig.userLayer = GetField(f, 0);
            _layerConfig.adFailed = GetField(f, colMap.ContainsKey("AdFailed") ? colMap["AdFailed"] : 6);
            if (string.IsNullOrEmpty(_layerConfig.adFailed))
                _layerConfig.adFailed = "0_1_60_-1_-1_-1";
            _layerConfig.brokenIce = GetField(f, colMap.ContainsKey("BrokenIce") ? colMap["BrokenIce"] : 7);
            if (string.IsNullOrEmpty(_layerConfig.brokenIce))
                _layerConfig.brokenIce = "1_1";
            _layerConfig.interAdsGroup = GetField(f, colMap.ContainsKey("InterAdsGroup") ? colMap["InterAdsGroup"] : 8);
            _layerConfig.adTrackingStimulate = GetField(f, colMap.ContainsKey("AdTrackingStimulate") ? colMap["AdTrackingStimulate"] : 9);
            int specialPopupCol = colMap.ContainsKey("特惠礼包主动弹出") ? colMap["特惠礼包主动弹出"] : 10;
            _layerConfig.allowSpecialPopup = GetField(f, specialPopupCol) == "开";
            return;
        }
    }

    private void LoadDefaultLayerConfig()
    {
        // 默认 IAA11（分层表 20260906 禅意之境 IAA11 行）：
        _layerConfig.userLayer = "IAA11";
        _layerConfig.adFailed = "1_1_120_4_2_2";   // S=1 F=1 T=120 A=4 B=2 C=2
        _layerConfig.brokenIce = "1_1";
        _layerConfig.interAdsGroup = "0";
        _layerConfig.adTrackingStimulate = "1_500";
        _layerConfig.allowSpecialPopup = true;
    }

    /// <summary>
    /// 获取当前用户分层（TODO：接入分层模块；当前返回配置的默认层）。
    /// 分层跳转（升级/降级）由外部用户分层模块驱动后调用 RefreshLayer()。
    /// </summary>
    public string GetCurrentUserLayer()
    {
        // TODO：从分层模块读取实际分层（IAA00/IAA01/IAA02/IAA10/IAA11/IAA12/IAP11~IAP14…）
        // 当前按配置表默认层返回，接入后删除该注释
        return _layerConfig.userLayer;
    }

    /// <summary>分层变更后重新加载配置</summary>
    public void RefreshLayer()
    {
        LoadLayerConfig();
    }

    /// <summary>是否 IAA 层（纯广告变现层；破冰礼包只对 IAA 层生效）</summary>
    private bool IsIaaLayer()
    {
        string layer = GetCurrentUserLayer();
        return layer != null && layer.StartsWith("IAA");
    }

    // ======================= 广告获取失败策略（提示道具） =======================

    /// <summary>
    /// 解析广告失败策略参数（S_F_T_A_B_C），来源：用户分层表 AdFailed 列。
    /// 需求：提示道具广告获取失败策略（仅适用禅意之境）
    ///  - 每关可观看次数 A（灯泡/复活），单次失败后 T 秒内不再弹窗；
    ///  - 失败后累计 3600 秒进入冷却状态 C（1=冷却后可再弹，-1=不弹）。
    /// </summary>
    public AdFailedParams GetAdFailedParams()
    {
        AdFailedParams p = new AdFailedParams();
        string[] parts = _layerConfig.adFailed.Split('_');
        p.shopGoldVideoDailyLimit = parts.Length > 0 ? GetInt(parts[0]) : 0;
        p.shareDailyLimit = parts.Length > 1 ? GetInt(parts[1]) : 1;
        p.tipFailCooldown = parts.Length > 2 ? GetInt(parts[2]) : 60;
        p.toolVideoPerLevel = parts.Length > 3 ? GetInt(parts[3]) : 4;
        p.goldVideoInterval = parts.Length > 4 ? GetInt(parts[4]) : 2;
        p.cooldownState = parts.Length > 5 ? GetInt(parts[5]) : 3;
        p.isCooldown = DateTime.Now < _adFailBlockedUntil;
        return p;
    }

    /// <summary>灯泡/复活每关可观看视频次数（A 参数；-1 表示该层不提供）</summary>
    private int GetToolVideoLevelLimit()
    {
        return GetAdFailedParams().toolVideoPerLevel;
    }

    private int GetToolVideoUsedCount(LimitRewordType type)
    {
        int c;
        return _levelVideoUsed.TryGetValue(type, out c) ? c : 0;
    }

    private void IncreaseToolVideoUsed(LimitRewordType type)
    {
        _levelVideoUsed[type] = GetToolVideoUsedCount(type) + 1;
    }

    /// <summary>玩家是否可以继续观看提示道具广告（每关次数限制 + 冷却状态）</summary>
    public bool CanPlayToolVideo(LimitRewordType toolType)
    {
        AdFailedParams p = GetAdFailedParams();
        if (p.toolVideoPerLevel < 0) return false;   // -1 不提供
        if (GetToolVideoUsedCount(toolType) >= p.toolVideoPerLevel) return false;
        if (p.isCooldown) return false;              // 3600s 冷却中
        return true;
    }

    /// <summary>提示道具广告失败（未看完）：按 T 秒冷却 + 3600s 状态 C 控制弹窗</summary>
    public void ShowAdFailedPanel(LimitRewordType toolType)
    {
        AdFailedParams p = GetAdFailedParams();
        // 每关次数已满则不弹
        if (p.toolVideoPerLevel >= 0 && GetToolVideoUsedCount(toolType) >= p.toolVideoPerLevel)
            return;

        DateTime now = DateTime.Now;
        // 状态 C：累计失败 3600 秒后若 C 允许则继续弹，否则不再弹
        if (p.cooldownState < 0 && now < _adFailBlockedUntil)
            return;

        _adFailBlockedUntil = now.AddSeconds(p.tipFailCooldown);
        SystemManager.Instance.ShowPanel(PanelType.AdFailed);
    }

    /// <summary>提示道具激励视频看完成功：增加次数并发放道具（每关 X/a 次）</summary>
    public void OnToolVideoWatched(LimitRewordType toolType)
    {
        IncreaseToolVideoUsed(toolType);
        // 发放道具由调用方（弹窗关闭回调）完成，此处仅维护次数
    }

    // ======================= 商店金币视频（50 → 100） =======================

    /// <summary>
    /// 商店金币视频状态：每天总次数 = S 参数；每次看完视频获得 100 金币（需求：由 50 调整到 100）。
    /// </summary>
    public ShopGoldVideoState GetShopGoldVideoState()
    {
        ShopGoldVideoState s = new ShopGoldVideoState();
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        if (_shopGoldUsedDate != today)
        {
            _shopGoldUsedDate = today;
            _shopGoldUsedToday = 0;
        }
        s.used = _shopGoldUsedToday >= GetAdFailedParams().shopGoldVideoDailyLimit;
        s.total = _shopGoldUsedToday;
        s.freeReady = !s.used && GetAdFailedParams().shopGoldVideoDailyLimit > 0;
        return s;
    }

    /// <summary>金币视频看完后调用：加 100 金币</summary>
    public void OnShopGoldClaimed()
    {
        ShopGoldVideoState s = GetShopGoldVideoState();
        if (s.used) return;
        _shopGoldUsedToday++;
        GameDataManager.Instance.UserData.UpdateGold(ShopGoldVideoReward, false, false, "商店金币视频");
        MessageSystem.Instance.ShowTip("+100 金币");
    }

    // ======================= 分享奖励 =======================

    /// <summary>分享点击：记录点击时间，等待切回前台判定</summary>
    public void OnShareClicked()
    {
        _shareClickedTime = DateTime.Now;
    }

    private DateTime _shareClickedTime;

    /// <summary>
    /// 分享成功判定：切回前台且距离点击超过 3 秒 → 分享成功
    /// 奖励：灯泡1 / 复活1 / 金币50，每日限 F 次（需求：分享奖励）。
    /// </summary>
    public void OnShareReturned(bool fromBackground)
    {
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        if (_shareSuccessDate != today)
        {
            _shareSuccessDate = today;
            _shareSuccessToday = 0;
        }
        if (_shareSuccessToday >= GetAdFailedParams().shareDailyLimit) return;

        if (!fromBackground) return;
        if ((DateTime.Now - _shareClickedTime).TotalSeconds < ShareSuccessMinSeconds) return;

        _shareSuccessToday++;
        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.AutoComplete, 1, "分享奖励");
        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, 1, "分享奖励");
        GameDataManager.Instance.UserData.UpdateGold(50, false, false, "分享奖励");
        MessageSystem.Instance.ShowTip("分享成功 +1灯泡 +1复活 +50金币");
    }

    // ======================= 破冰礼包策略 =======================

    /// <summary>破冰礼包弹窗记录（冷却 7+N 天，N 每次 +3）</summary>
    private DateTime _lastBreakIcePopupTime = DateTime.MinValue;
    private int _breakIcePopupCount;

    /// <summary>
    /// 破冰礼包策略（需求：破冰策略）：
    ///  - 仅 IAA 层触发；触发条件：插屏广告次数 &gt; 3；
    ///  - 周末/节假日触发概率更高（此处按是否节日/周末放大概率）；
    ///  - 冷却时间：7+N 天，N 为历史弹窗次数每次 +3；
    ///  - 商品：RemoveAdsWeekly（去广告一周礼包）。
    /// </summary>
    public bool TryShowBreakIcePopup(int interAdsCountThisSession)
    {
        // 分层表 BrokenIce 开关（1=开）
        string[] bi = _layerConfig.brokenIce.Split('_');
        if (bi.Length < 1 || bi[0] != "1") return false;
        if (!IsIaaLayer()) return false;              // 仅 IAA 层
        if (interAdsCountThisSession <= 3) return false; // 插屏>3

        // 周末/节假日触发概率更高
        if (!IsHolidayOrWeekend() && UnityEngine.Random.value > 0.3f) return false;

        // 冷却：7+N 天（N = 弹窗次数 × 3）
        int cooldownDays = BreakIceCooldownBaseDays + _breakIcePopupCount * BreakIceCooldownStepDays;
        if (_lastBreakIcePopupTime != DateTime.MinValue
            && DateTime.Now.Subtract(_lastBreakIcePopupTime).TotalDays < cooldownDays)
            return false;

        ShopDataItem pack = allShopItems.FirstOrDefault(x => x.produceNameId == ProductRemoveAdsWeekly);
        if (pack == null) return false;

        _breakIcePopupCount++;
        _lastBreakIcePopupTime = DateTime.Now;
        SaveNonConsumableState();

        // TODO：确认 PanelType.SpecialOfferPopup 已注册，并透传 pack 到弹窗
        SystemManager.Instance.ShowPanel(PanelType.SpecialOfferPopup);
        return true;
    }

    // ======================= 复购/升层策略 =======================

    /// <summary>复购/升层弹窗记录（冷却 7 天）</summary>
    private DateTime _lastRepurchasePopupTime = DateTime.MinValue;

    /// <summary>
    /// 复购/升层策略（需求：复购策略）：
    ///  - IAP 层触发；金币 &lt; 100；
    ///  - 从上次购买的礼包 A 的下一个更高档位礼包中，选择"支付调起次数最多"的礼包 B；
    ///  - 若 A 不在候选列表，则从基准价 ≥ MAX(A,10) 的礼包中选择；
    ///  - 加量不加价：B 的礼包内容 = A 的内容 + C，C = 取整(B价格×10%)对应的等值内容；
    ///  - 7 天冷却。
    /// </summary>
    public bool TryShowRepurchasePopup(ShopDataItem lastPurchasedPack)
    {
        if (lastPurchasedPack == null) return false;
        if (IsIaaLayer()) return false;               // 仅 IAP 层
        if (GetPlayerGold() >= 100) return false;     // 金币<100
        if (_lastRepurchasePopupTime != DateTime.MinValue
            && DateTime.Now.Subtract(_lastRepurchasePopupTime).TotalDays < RepurchaseCooldownDays)
            return false;

        ShopDataItem a = lastPurchasedPack;
        float basePrice = Math.Max(a.price, 10);

        // 候选：价格更高档的礼包（type=2 礼包；排除去广告、限时礼包、活动礼包、限购商品）
        List<ShopDataItem> candidates = shopItems
            .Where(x => x.type == 2 && x.price >= basePrice && !IsRemoveAdsProduct(x.produceNameId)
                && !IsLimitedOut(x) && !x.isActivityPack && x.limited == 0)
            .OrderBy(x => x.price)
            .ToList();

        if (candidates.Count == 0) return false;

        // 首选：A 的下一个更高档位礼包（giftType = a.giftType + 1）
        ShopDataItem chosen = candidates.FirstOrDefault(x => x.giftType == a.giftType + 1);
        if (chosen == null)
        {
            // 次选：支付调起次数最多的礼包
            int maxInvoke = candidates.Max(x => GetPayInvokeCount(x.produceNameId));
            var mostInvoked = candidates.Where(x => GetPayInvokeCount(x.produceNameId) == maxInvoke && maxInvoke > 0).ToList();
            if (mostInvoked.Count > 0)
                chosen = mostInvoked.OrderBy(x => x.price).First();
            else
                chosen = candidates[0]; // 兜底：最低价
        }

        // 加量不加价：C = 取整(chosen.price × 10%)，按 A 的内容单价换算成 B 的额外内容
        AddPackBonus(chosen, a, Mathf.RoundToInt(chosen.price * 0.1f));

        _lastRepurchasePopupTime = DateTime.Now;
        SaveNonConsumableState();

        // TODO：确认 PanelType.SpecialOfferPopup 已注册，并透传 chosen 到弹窗（加量不加价展示）
        SystemManager.Instance.ShowPanel(PanelType.SpecialOfferPopup);
        return true;
    }

    /// <summary>
    /// 加量不加价：在 B 的礼包内容基础上按 C 价值追加内容（C = 取整(V×10%)）。
    /// 此处按"每1元≈100金币"的等价换算追加金币；具体兑换比例 TODO 与数值策划确认。
    /// </summary>
    private void AddPackBonus(ShopDataItem target, ShopDataItem basePack, int bonusValue)
    {
        if (target.productContent == null)
            target.productContent = new List<List<string>>();

        // 追加：金币类型，数量 = bonusValue × 100（1元≈100金币）
        target.productContent.Add(new List<string>
        {
            ((int)LimitRewordType.Coins).ToString(),
            (bonusValue * 100).ToString()
        });

        // 同时记录 basePack 的原始内容用于 UI 展示"加量不加价"对比
        // TODO：UI 展示时对比 basePack 内容与 target 内容
        Debug.Log($"[ShopManager] 加量不加价: {target.produceNameId} 追加价值 {bonusValue}");
    }

    // ======================= 数据访问桩（TODO 接入 UserData 后替换） =======================

    /// <summary>获取玩家金币数（TODO：按 GameDataManager.UserData 实际金币字段实现）</summary>
    protected virtual long GetPlayerGold()
    {
        // TODO：return GameDataManager.Instance.UserData.goldCount;
        return 0;
    }

    /// <summary>获取玩家道具持有数（TODO：按 UserData 实际道具字段实现）</summary>
    protected virtual int GetPlayerToolCount()
    {
        // TODO：return GameDataManager.Instance.UserData.toolCount;
        return 0;
    }
}
//（注：内容由AI生成）
