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
    public string userLayer = "";   // 当前用户分层（初始为空，第一次 GetCurrentUserLayer 时被填充）
    public string adFailed = "1_1_60_4_-1_3";   // 广告失败策略 S_F_T_A_B_C
    public string brokenIce = "1_1";      // 破冰礼包策略（开关_P冷却参数）
    public string interAdsGroup = "0";    // 插屏分组（插屏>3 次数判断用）
    public string adTrackingStimulate = "0_0"; // 广告追踪刺激 S_G（活跃天数/金币数）
    public bool allowSpecialPopup = true; // 特惠礼包主动弹出（开/关）
}

/// <summary>
/// 广告失败策略参数（解析自 S_F_T_A_B_C）：
///   S = 开关（1 开 / 0 关）——关闭时全部"假广告失败弹窗"逻辑停用，正常请求广告；
///   F = 每天分享次数限制；
///   T = 限制时间（秒）——任意一次弹"广告失败弹窗"后，T 秒内点任意视频点都直接弹该弹窗；
///   A = 当前关卡灯泡道具视频按钮，使用 A 次后，弹假的广告获取失败弹窗（引导付费）；-1 表示不干预；
///   B = 当前关卡复活道具视频按钮，使用 B 次后，弹假的广告获取失败弹窗（引导付费）；-1 表示不干预；
///   C = 商店金币视频按钮，使用 C 次后，弹假的广告获取失败弹窗（引导付费）；-1 表示不干预。
/// 注：
///   - A/B 的计数按"当前关卡"统计（进入关卡时由 SyncToolVideoLevel 按关卡标识刷新）；
///   - C 与 F 的计数按"自然日"统计；
///   - 所有计数持久化在 UserData.adLimit，关闭游戏后仍继承；
///   - T 冷却跨所有视频入口共享（灯泡/复活/商店金币视频），同样持久化。
/// </summary>
public struct AdFailedParams
{
    public bool enabled;                 // S：总开关（1 开 / 0 关）
    public int shareDailyLimit;          // F：每天分享次数上限
    public float tipFailCooldown;        // T：广告失败弹窗冷却（秒）
    public int bulbVideoPerLevel;        // A：灯泡每关可正常看视频次数（-1 不干预）
    public int reviveVideoPerLevel;      // B：复活每关可正常看视频次数（-1 不干预）
    public int shopGoldVideoLimit;       // C：商店金币视频可正常看次数（-1 不干预）
    public bool isCooldown;              // 运行状态：当前是否处于 T 秒冷却内
}

/// <summary>
/// 商店管理器 - 用户分层设计部分类。
/// 职责：用户分层配置加载与查询、广告获取失败策略（提示道具视频/金币视频/分享）、
///      破冰礼包策略、复购/升层策略。分层与分层跳转（L层升级）由外部分层模块驱动。
///
/// 分层依据（需求《用户标记及分层策略规则》）：
///   1. 广告追踪权限状态（PushMessageReceiver.OnOAIDClosedSignal 回调写入 UserData.adTrackingEnabled）；
///   2. 活跃周期天数 N（UserData.activeDayCnt）；
///   3. 付费状态（UserData.TotalPayTimes > 0）+ 单次付费最大金额 A + 付费累积次数。
///
/// ★ IAA 层（未付费）分 6 层：
///   ┌──────────────┬──────────────┬───────────────────┐
///   │ 广告追踪权限 │ 活跃天数 N   │ 分层              │
///   ├──────────────┼──────────────┼───────────────────┤
///   │ 未通过       │ N <= 3       │ IAA00（默认）     │
///   │ 通过         │ N <= 3       │ IAA10             │
///   │ 未通过       │ 3 < N < 14   │ IAA01             │
///   │ 通过         │ 3 < N < 14   │ IAA11             │
///   │ 未通过       │ N >= 14      │ IAA02             │
///   │ 通过         │ N >= 14      │ IAA12             │
///   └──────────────┴──────────────┴───────────────────┘
///
/// ★ IAP 层（已付费）分 4 层：
///   ┌──────────┬──────────────────────────┬──────────────────────┐
///   │ 分层     │ 单次付费最大金额 A       │ 付费累积次数         │
///   ├──────────┼──────────────────────────┼──────────────────────┤
///   │ IAP14    │ A >= 25                  │ —                    │
///   │ IAP13    │ 25 > A > 9               │ —                    │
///   │ IAP12    │ A <= 9                   │ 付费累积次数 > 1     │
///   │ IAP11    │ A <= 9                   │ 付费累积次数 = 1     │
///   └──────────┴──────────────────────────┴──────────────────────┘
///
/// ★ 每次调用 GetCurrentUserLayer() 都会：
///   1. 根据"广告追踪权限 × 活跃天数 × 付费状态"计算当前分层 layer；
///   2. 从缓存的配置表中按 layer 查询对应行，刷新 _layerConfig；
///   3. 若分层与上次不同，打印切换日志；若相同则静默返回。
/// </summary>
public partial class ShopManager : MonoBehaviour
{
    /// <summary>当前用户分层配置（每次 GetCurrentUserLayer 都会根据当前分层刷新）</summary>
    private ShopLayerConfig _layerConfig = new ShopLayerConfig();

    /// <summary>标记 _layerConfig 是否已经从配置表应用过一次（避免首次误跳过）</summary>
    private bool _layerConfigApplied = false;

    // ======================= 分层配置表缓存 =======================

    /// <summary>分层配置表的 CSV 记录缓存（首次 LoadLayerConfig 时填充）</summary>
    private List<List<string>> _layerRecords;
    /// <summary>分层配置表的列名 → 列号映射</summary>
    private Dictionary<string, int> _layerColMap;
    /// <summary>分层配置表的数据起始行（跳过说明行和字段名行）</summary>
    private int _layerDataStartRow = -1;

    // ======================= 广告失败策略运行状态（持久化） =======================
    //
    // 所有计数统一落在 UserData.adLimit（随存档序列化），保证"关闭游戏后数据继承"：
    //   - 商店金币视频（C）/ 分享（F）：按自然日刷新（跨天清零）；
    //   - 提示/复活视频（A/B）：按关卡刷新（换关清零，同关重进保留）。
    // 内存字段仅作 UserData 不可用时的兜底。

    /// <summary>UserData 尚不可用时的兜底容器（正常流程不会走到）。</summary>
    private readonly AdLimitSaveData _fallbackAdLimit = new AdLimitSaveData();

    /// <summary>分享点击时间（切回前台成功判定用，纯运行时状态，无需持久化）</summary>
    private DateTime _shareClickedTime;

    /// <summary>广告限次持久化数据的统一访问入口。</summary>
    private AdLimitSaveData AdLimitData
    {
        get
        {
            var user = GameDataManager.Instance != null ? GameDataManager.Instance.UserData : null;
            if (user == null) return _fallbackAdLimit;
            if (user.adLimit == null) user.adLimit = new AdLimitSaveData();
            return user.adLimit;
        }
    }

    private static string TodayString() => DateTime.Now.ToString("yyyy-MM-dd");

    /// <summary>把广告限次数据立即落盘（避免进程被直接杀掉时丢失计数）。</summary>
    private void PersistAdLimit()
    {
        var user = GameDataManager.Instance != null ? GameDataManager.Instance.UserData : null;
        if (user == null) return;
        user.SaveData();
    }

    /// <summary>商店金币视频（C）：按自然日归一化，跨天清零。</summary>
    private void EnsureShopGoldDay()
    {
        AdLimitSaveData d = AdLimitData;
        string today = TodayString();
        if (d.shopGoldDate != today)
        {
            d.shopGoldDate = today;
            d.shopGoldUsed = 0;
        }
    }

    /// <summary>分享（F）：按自然日归一化，跨天清零。</summary>
    private void EnsureShareDay()
    {
        AdLimitSaveData d = AdLimitData;
        string today = TodayString();
        if (d.shareDate != today)
        {
            d.shareDate = today;
            d.shareUsed = 0;
        }
    }

    private static int GetToolUsed(AdLimitSaveData d, LimitRewordType type)
    {
        return type == LimitRewordType.ReStartTool ? d.reviveUsed : d.bulbUsed;
    }

    private static void SetToolUsed(AdLimitSaveData d, LimitRewordType type, int value)
    {
        if (type == LimitRewordType.ReStartTool) d.reviveUsed = value;
        else d.bulbUsed = value;
    }

    /// <summary>构造关卡标识：isChess=true 用拼字关卡，否则用六边形/方块关卡。</summary>
    private static string BuildToolLevelKey(int stageIndex, bool isChess)
    {
        return (isChess ? "C:" : "H:") + stageIndex;
    }

    private DateTime GetAdFailBlockedUntil()
    {
        long ticks = AdLimitData.adFailBlockedUntilTicks;
        if (ticks <= 0) return DateTime.MinValue;
        try { return new DateTime(ticks); }
        catch { return DateTime.MinValue; }
    }

    private void SetAdFailBlockedUntil(DateTime value)
    {
        AdLimitSaveData d = AdLimitData;
        d.adFailBlockedUntilTicks = value == DateTime.MinValue ? 0 : value.Ticks;
        PersistAdLimit();
    }

    /// <summary>最近一次广告失败弹窗对应的道具类型（灯泡/复活/商店金币），供弹窗读取以决定单道具商品按钮</summary>
    private LimitRewordType _lastAdFailedToolType = LimitRewordType.Tipstool;

    /// <summary>道具视频类型（灯泡提示灯 / 复活）</summary>
    public enum ToolVideoType
    {
        Bulb = (int)LimitRewordType.Tipstool,      // 灯泡（提示道具，弹窗按钮）
        Revive = (int)LimitRewordType.ReStartTool  // 复活（Restart 道具）
    }

    /// <summary>金币视频状态（商店金币视频：每次看完 100 金币）</summary>
    public struct ShopGoldVideoState
    {
        public bool used;         // 当日是否已达到 C 阈值（再点将弹假的广告获取失败面板）
        public int total;         // 当日已正常观看次数
        public bool freeReady;    // 是否可以正常观看（= !used 或 S=0/C=-1 时始终为 true）
    }

    // ======================= 分层加载（缓存 CSV） =======================

    /// <summary>
    /// 加载用户分层配置 CSV 并缓存（只做一次）。
    /// 真正的"按分层查询并填充 _layerConfig"由 ApplyLayerConfig(layer) 完成。
    /// </summary>
    private void LoadLayerConfig()
    {
        TextAsset layer = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "shop_layer");
        if (layer == null)
        {
            LogShop("LoadLayerConfig: 未找到 shop_layer 配置表，后续走默认值兜底");
            return;
        }

        CacheLayerCsv(layer.text);
    }

    /// <summary>
    /// 解析并缓存 CSV：定位字段名行、构建列映射、记录数据起始行。
    /// </summary>
    private void CacheLayerCsv(string data)
    {
        _layerRecords = SplitCsvLines(data);
        if (_layerRecords == null || _layerRecords.Count == 0)
        {
            LogShop("CacheLayerCsv: CSV 记录为空");
            return;
        }

        // 定位字段名行：前 4 行内第 0 列 == "分层标记"
        int nameRow = -1;
        for (int i = 0; i < Math.Min(_layerRecords.Count, 4); i++)
        {
            if (_layerRecords[i].Count > 0 && _layerRecords[i][0] == "分层标记")
            {
                nameRow = i;
                break;
            }
        }
        if (nameRow < 0)
        {
            LogShop("CacheLayerCsv: 未找到字段名行（第0列=分层标记）");
            return;
        }

        // 构建列映射
        _layerColMap = new Dictionary<string, int>();
        var header = _layerRecords[nameRow];
        for (int j = 0; j < header.Count; j++)
        {
            string h = (header[j] ?? "").Trim();
            if (!string.IsNullOrEmpty(h) && !_layerColMap.ContainsKey(h))
                _layerColMap[h] = j;
        }

        _layerDataStartRow = nameRow + 1;
        LogShop($"CacheLayerCsv: CSV 已缓存, 记录数={_layerRecords.Count}, 数据起始行={_layerDataStartRow}, 列数={_layerColMap.Count}");
    }

    // ======================= 分层应用（按 layer 查表并填充 _layerConfig） =======================

    /// <summary>
    /// 按给定分层名从缓存的 CSV 中查到对应行并填充 _layerConfig。
    /// 若缓存缺失或未找到该分层行，走默认值兜底。
    /// 分层与 _layerConfig.userLayer 相同时静默跳过（避免重复日志和重复赋值）。
    /// </summary>
    private void ApplyLayerConfig(string layer)
    {
        if (string.IsNullOrEmpty(layer)) return;

        string previousLayer = _layerConfig != null ? _layerConfig.userLayer : "";

        // ---------- 从缓存 CSV 查表 ----------
        if (_layerRecords != null && _layerColMap != null && _layerDataStartRow > 0)
        {
            for (int i = _layerDataStartRow; i < _layerRecords.Count; i++)
            {
                var f = _layerRecords[i];
                if (f.Count < 6) continue;
                if (GetField(f, 0) != layer) continue;

                // 命中：按字段填充
                _layerConfig = new ShopLayerConfig();
                _layerConfig.userLayer = layer;

                _layerConfig.adFailed = GetField(f, _layerColMap.ContainsKey("AdFailed") ? _layerColMap["AdFailed"] : 6);
                if (string.IsNullOrEmpty(_layerConfig.adFailed))
                    _layerConfig.adFailed = "0_1_60_-1_-1_-1";

                _layerConfig.brokenIce = GetField(f, _layerColMap.ContainsKey("BrokenIce") ? _layerColMap["BrokenIce"] : 7);
                if (string.IsNullOrEmpty(_layerConfig.brokenIce))
                    _layerConfig.brokenIce = "1_1";

                _layerConfig.interAdsGroup = GetField(f, _layerColMap.ContainsKey("InterAdsGroup") ? _layerColMap["InterAdsGroup"] : 8);

                _layerConfig.adTrackingStimulate = GetField(f, _layerColMap.ContainsKey("AdTrackingStimulate") ? _layerColMap["AdTrackingStimulate"] : 9);

                int specialPopupCol = _layerColMap.ContainsKey("特惠礼包主动弹出") ? _layerColMap["特惠礼包主动弹出"] : 10;
                _layerConfig.allowSpecialPopup = GetField(f, specialPopupCol) == "开";

                _layerConfigApplied = true;

                if (previousLayer != layer)
                {
                    LogShop($"[UserLayer] 分层切换: {previousLayer} → {layer} | " +
                            $"AdFailed={_layerConfig.adFailed}, BrokenIce={_layerConfig.brokenIce}, " +
                            $"InterAdsGroup={_layerConfig.interAdsGroup}, AdTrackingStimulate={_layerConfig.adTrackingStimulate}, " +
                            $"AllowSpecialPopup={_layerConfig.allowSpecialPopup}");
                }
                else
                {
                    LogShop($"[UserLayer] 应用分层配置: {layer} (首次) | " +
                            $"AdFailed={_layerConfig.adFailed}, BrokenIce={_layerConfig.brokenIce}, " +
                            $"InterAdsGroup={_layerConfig.interAdsGroup}, AdTrackingStimulate={_layerConfig.adTrackingStimulate}, " +
                            $"AllowSpecialPopup={_layerConfig.allowSpecialPopup}");
                }
                return;
            }
        }
    }

    // ======================= 获取当前用户分层（含配置刷新） =======================

    /// <summary>
    /// 获取当前用户分层，并同时按分层刷新 _layerConfig。
    ///
    /// 分层依据（需求《用户标记及分层策略规则》）：
    ///   1. 广告追踪权限状态：UserData.adTrackingEnabled；
    ///   2. 活跃周期天数 N：UserData.activeDayCnt；
    ///   3. 付费状态：UserData.TotalPayTimes > 0 → IAP 层；
    ///      付费金额 / 次数细分：
    ///        - 单次付费最大金额 A ≥ 25         → IAP14
    ///        - 25 > A > 9                     → IAP13
    ///        - A ≤ 9 且 付费累积次数 > 1       → IAP12
    ///        - A ≤ 9 且 付费累积次数 = 1       → IAP11
    /// </summary>
    public string GetCurrentUserLayer()
    {
        var userData = GameDataManager.Instance != null ? GameDataManager.Instance.UserData : null;
        if (userData == null)
        {
            LogShop("GetCurrentUserLayer: UserData 为空，回退默认层 IAA00");
            ApplyLayerConfig("IAA00");
            return "IAA00";
        }

        bool hasAdTracking = userData.adTrackingEnabled == "1";   // 若为 string 请改为 == "1"
        int activeDayCnt = userData.activeDayCnt;
        bool isPaid = userData.TotalPayTimes > 0;

        string layer;

        // ---------- 1. 已付费玩家 → IAP 层（按单次付费最大金额 A + 付费累积次数细分） ----------
        if (isPaid)
        {
            float maxSinglePayAmount = userData.MaxSinglePayAmount;
            float totalPayAmount = userData.TotalRevenue;
            int payTimes = userData.TotalPayTimes;

            if (maxSinglePayAmount >= 2500)
            {
                layer = "IAP14";
            }
            else if (maxSinglePayAmount > 900)
            {
                layer = "IAP13";
            }
            else
            {
                // A ≤ 9
                layer = (payTimes > 100) ? "IAP12" : "IAP11";
            }

            LogShop($"GetCurrentUserLayer 计算: isPaid=True, MaxSinglePayAmount={maxSinglePayAmount:F2},TotalPayAmount={totalPayAmount}, TotalPayTimes={payTimes} => layer={layer}");
        }
        else
        {
            // ---------- 2. 未付费玩家 → IAA 层（按广告追踪权限 × 活跃天数分 6 层） ----------
            if (hasAdTracking)
            {
                if (activeDayCnt <= 3) layer = "IAA10";
                else if (activeDayCnt < 14) layer = "IAA11";
                else layer = "IAA12";
            }
            else
            {
                if (activeDayCnt <= 3) layer = "IAA00";
                else if (activeDayCnt < 14) layer = "IAA01";
                else layer = "IAA02";
            }

            LogShop($"GetCurrentUserLayer 计算: isPaid=False, hasAdTracking={hasAdTracking}, activeDayCnt={activeDayCnt} => layer={layer}");
        }

        // ★ 计算完分层后立刻按分层刷新 _layerConfig
        ApplyLayerConfig(layer);

        return layer;
    }

    /// <summary>
    /// 分层变更后重新加载配置并打印（外部主动调用的入口）。
    /// 一般由 PushMessageReceiver.OnOAIDClosedSignal / 切回前台 / 日切 / 购买成功等触发。
    /// </summary>
    public void RefreshLayer()
    {
        // 重新加载 CSV 缓存（防止热更配置）
        LoadLayerConfig();

        // 强制重算一次（ApplyLayerConfig 内部分层没变会静默跳过，这里先清空标记强制刷）
        _layerConfigApplied = false;
        string layer = GetCurrentUserLayer();

        LogShop($"[UserLayer] RefreshLayer 完成: 当前分层={layer}, " +
                $"AdFailed={_layerConfig.adFailed}, BrokenIce={_layerConfig.brokenIce}, " +
                $"InterAdsGroup={_layerConfig.interAdsGroup}, AdTrackingStimulate={_layerConfig.adTrackingStimulate}, " +
                $"AllowSpecialPopup={_layerConfig.allowSpecialPopup}");
    }

    /// <summary>
    /// 获取当前分层对应的"插屏控制参数"分组 Key。
    /// 该值对应 adv_general_config 表的 UserLayer 列：
    ///   1=标准 / 2=低 / 3=高 / 0=不弹插屏和Banner
    /// </summary>
    public string GetInterAdsGroup()
    {
        if (_layerConfig == null) return "1";
        string v = _layerConfig.interAdsGroup;
        return string.IsNullOrEmpty(v) ? "1" : v.Trim();
    }

    /// <summary>
    /// 解析当前分层的"广告追踪激励策略" AdTrackingStimulate（S_G）：
    ///   S = 0 关闭 / 1 开启（关闭时设置里不出"广告关联"开关，也不发奖励）
    ///   G = 激励金币数量
    /// 数据来源：shop_layer.csv 的 AdTrackingStimulate 列（如 IAA01 = 1_200）。
    /// </summary>
    public bool TryGetAdTrackingStimulate(out bool enabled, out int gold)
    {
        enabled = false;
        gold = 0;

        // 保证 _layerConfig 是当前分层的最新值（授权状态/活跃天数可能刚变化）
        GetCurrentUserLayer();

        string raw = _layerConfig != null ? _layerConfig.adTrackingStimulate : null;
        if (string.IsNullOrEmpty(raw)) return false;

        string[] parts = raw.Split('_');
        if (parts.Length < 2) return false;

        enabled = GetInt(parts[0]) == 1;
        gold = Mathf.Max(0, GetInt(parts[1]));
        return true;
    }

    /// <summary>是否 IAA 层（纯广告变现层；破冰礼包只对 IAA 层生效）</summary>
    private bool IsIaaLayer()
    {
        string layer = GetCurrentUserLayer();
        return layer != null && layer.StartsWith("IAA");
    }

    // ======================= 广告获取失败策略（S_F_T_A_B_C 解析） =======================

    /// <summary>
    /// 解析广告失败策略参数（S_F_T_A_B_C），来源：用户分层表 AdFailed 列。
    /// 解析规则（新语义）：
    ///   S = 开关（1 开 / 0 关）
    ///   F = 每天分享次数上限
    ///   T = 冷却秒数（任意一次弹"广告失败弹窗"后 T 秒内，点任意视频点都直接弹该弹窗）
    ///   A = 灯泡每关可正常看视频次数（-1 不干预）
    ///   B = 复活每关可正常看视频次数（-1 不干预）
    ///   C = 商店金币视频可正常观看次数（-1 不干预）
    /// </summary>
    public AdFailedParams GetAdFailedParams()
    {
        AdFailedParams p = new AdFailedParams();
        string[] parts = _layerConfig.adFailed.Split('_');
        p.enabled             = parts.Length > 0 && GetInt(parts[0]) == 1;
        p.shareDailyLimit     = parts.Length > 1 ? GetInt(parts[1]) : 1;
        p.tipFailCooldown     = parts.Length > 2 ? GetInt(parts[2]) : 60;
        p.bulbVideoPerLevel   = parts.Length > 3 ? GetInt(parts[3]) : -1;
        p.reviveVideoPerLevel = parts.Length > 4 ? GetInt(parts[4]) : -1;
        p.shopGoldVideoLimit  = parts.Length > 5 ? GetInt(parts[5]) : -1;
        p.isCooldown          = DateTime.Now < GetAdFailBlockedUntil();
        return p;
    }

    // ======================= 灯泡 / 复活 视频次数与假失败判定 =======================

    /// <summary>
    /// 获取指定道具类型在"当前关卡"的假失败阈值（A/B）；未识别类型返回 -1（不干预）。
    /// </summary>
    private int GetToolVideoLevelLimit(LimitRewordType toolType)
    {
        AdFailedParams p = GetAdFailedParams();
        if (toolType == LimitRewordType.Tipstool)    return p.bulbVideoPerLevel;
        if (toolType == LimitRewordType.ReStartTool) return p.reviveVideoPerLevel;
        return -1;
    }

    private int GetToolVideoUsedCount(LimitRewordType type)
    {
        return GetToolUsed(AdLimitData, type);
    }

    private void IncreaseToolVideoUsed(LimitRewordType type)
    {
        AdLimitSaveData d = AdLimitData;
        SetToolUsed(d, type, GetToolUsed(d, type) + 1);
        PersistAdLimit();
    }

    /// <summary>
    /// 关卡进入时调用：只有"关卡标识变化"才清零提示/复活视频次数。
    ///   - 换关（关卡号或模式变化）→ 清零，满足"每关刷新"；
    ///   - 同一关重进 / 关掉游戏再打开继续本关 → 保留已用次数，满足"数据继承"。
    /// </summary>
    /// <param name="stageIndex">即将进入的关卡号</param>
    /// <param name="isChess">是否拼字关卡（false = 六边形/方块关卡）</param>
    public void SyncToolVideoLevel(int stageIndex, bool isChess)
    {
        AdLimitSaveData d = AdLimitData;
        string key = BuildToolLevelKey(stageIndex, isChess);
        if (d.toolLevelKey == key) return;

        d.toolLevelKey = key;
        d.bulbUsed = 0;
        d.reviveUsed = 0;
        PersistAdLimit();
        LogShop($"[AdFailed] 换关 → {key}，提示/复活视频次数已刷新");
    }

    /// <summary>
    /// 强制清空当前关卡的灯泡/复活使用计数（调试或特殊流程使用）。
    /// 常规关卡刷新请走 <see cref="SyncToolVideoLevel"/>，避免同关重进误清。
    /// </summary>
    public void ResetLevelVideoUsed()
    {
        AdLimitSaveData d = AdLimitData;
        d.bulbUsed = 0;
        d.reviveUsed = 0;
        PersistAdLimit();
        LogShop("[AdFailed] 提示/复活视频使用次数已强制清零");
    }

    /// <summary>
    /// 判断针对指定道具类型是否应该直接弹"假的广告获取失败弹窗"（不请求真实广告）。
    /// 判定顺序：
    ///   1. S=0 → 不弹（关闭整个假失败逻辑）；
    ///   2. 处于 T 秒冷却中 → 弹；
    ///   3. A/B = -1 → 不弹；
    ///   4. 当前关卡已用次数 >= A/B → 弹。
    /// </summary>
    public bool ShouldShowFakeAdFailed(LimitRewordType toolType)
    {
        AdFailedParams p = GetAdFailedParams();
        if (!p.enabled) return false;
        if (p.isCooldown) return true;

        int limit = GetToolVideoLevelLimit(toolType);
        if (limit < 0) return false;

        return GetToolVideoUsedCount(toolType) >= limit;
    }

    /// <summary>
    /// 灯泡/复活点击的便捷入口：
    ///   - 返回 true  → 已弹出"假的广告获取失败面板"，调用方不要再去请求真实广告；
    ///   - 返回 false → 调用方正常请求广告。
    /// </summary>
    public bool TryHandleToolClickAsFakeFail(LimitRewordType toolType)
    {
        if (ShouldShowFakeAdFailed(toolType))
        {
            ShowAdFailedPanel(toolType);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 能否请求真实广告（= !ShouldShowFakeAdFailed）。
    /// 保留原方法名与签名，语义调整为"是否可以请求真实广告"。
    /// </summary>
    public bool CanPlayToolVideo(LimitRewordType toolType)
    {
        return !ShouldShowFakeAdFailed(toolType);
    }

    /// <summary>
    /// 展示"广告失败弹窗"（真实失败与假失败共用），并刷新 T 秒冷却。
    /// 冷却内任意视频点都会走 ShouldShowFakeAdFailed 返回 true 的分支。
    /// </summary>
    public void ShowAdFailedPanel(LimitRewordType toolType)
    {
        AdFailedParams p = GetAdFailedParams();
        if (p.tipFailCooldown > 0)
            SetAdFailBlockedUntil(DateTime.Now.AddSeconds(p.tipFailCooldown));

        _lastAdFailedToolType = toolType;
        LogShop($"ShowAdFailedPanel: toolType={toolType}, 冷却={p.tipFailCooldown}s, 截止={GetAdFailBlockedUntil():HH:mm:ss}");
        SystemManager.Instance.ShowPanel(PanelType.AdsFailScreen);
    }

    /// <summary>
    /// 通用"广告失败弹窗"（无具体道具类型时使用，例如商店金币视频的假失败）。
    /// </summary>
    public void ShowAdFailedPanel()
    {
        AdFailedParams p = GetAdFailedParams();
        if (p.tipFailCooldown > 0)
            SetAdFailBlockedUntil(DateTime.Now.AddSeconds(p.tipFailCooldown));

        _lastAdFailedToolType = LimitRewordType.Coins; // 通用入口 = 商店金币视频路径
        LogShop($"ShowAdFailedPanel (通用): 冷却={p.tipFailCooldown}s, 截止={GetAdFailBlockedUntil():HH:mm:ss}");
        SystemManager.Instance.ShowPanel(PanelType.AdsFailScreen);
    }

    /// <summary>最近一次广告失败弹窗对应的道具类型（灯泡 Tipstool / 复活 ReStartTool / 商店金币 Coins）。</summary>
    public LimitRewordType LastAdFailedToolType
    {
        get { return _lastAdFailedToolType; }
    }

    /// <summary>
    /// 今日分享换奖励是否还可用（未达 F 次上限）。
    /// 供广告失败弹窗决定是否显示「分享」按钮；F<=0 或策略关闭时不可用。
    /// </summary>
    public bool IsShareRewardAvailable()
    {
        AdFailedParams p = GetAdFailedParams();
        if (!p.enabled || p.shareDailyLimit <= 0) return false;

        EnsureShareDay();
        return AdLimitData.shareUsed < p.shareDailyLimit;
    }

    /// <summary>
    /// 今日分享换奖励的剩余次数（= F - 当日已成功次数，最小 0）。
    /// 供 AdsFailScreen 显示「剩余次数/上限次数」。
    /// 策略关闭或 F&lt;=0 时返回 0。
    /// </summary>
    public int GetShareRemainCount()
    {
        AdFailedParams p = GetAdFailedParams();
        if (!p.enabled || p.shareDailyLimit <= 0) return 0;

        EnsureShareDay();
        return Mathf.Max(0, p.shareDailyLimit - AdLimitData.shareUsed);
    }

    /// <summary>
    /// 灯泡/复活视频成功观看后调用：累计当前关卡已看次数（用于 A/B 阈值判断）。
    /// </summary>
    public void OnToolVideoWatched(LimitRewordType toolType)
    {
        IncreaseToolVideoUsed(toolType);
        LogShop($"OnToolVideoWatched: toolType={toolType}, 本次已看={GetToolVideoUsedCount(toolType)}");
    }

    // ======================= 剩余可看次数（供 UI 显示） =======================

    /// <summary>
    /// 获取指定道具视频在"当前关卡"的剩余可正常观看次数（A/B）。
    /// 返回 -1 表示不干预（无限制）；S=0 关闭策略时同样返回 -1。
    /// </summary>
    public int GetToolVideoRemainCount(LimitRewordType toolType)
    {
        AdFailedParams p = GetAdFailedParams();
        if (!p.enabled) return -1;

        int limit = GetToolVideoLevelLimit(toolType);
        if (limit < 0) return -1;

        return Mathf.Max(0, limit - GetToolVideoUsedCount(toolType));
    }

    /// <summary>
    /// 商店金币视频当日剩余可正常观看次数（C）。
    /// 返回 -1 表示不干预（无限制）。
    /// </summary>
    public int GetShopGoldVideoRemainCount()
    {
        AdFailedParams p = GetAdFailedParams();
        if (!p.enabled) return -1;

        int limit = p.shopGoldVideoLimit;
        if (limit < 0) return -1;

        EnsureShopGoldDay();
        return Mathf.Max(0, limit - AdLimitData.shopGoldUsed);
    }

    // ======================= 商店金币视频（S 开关 / C 次数阈值） =======================

    /// <summary>
    /// 商店金币视频当前状态：
    ///   - used       = 当日是否已达到 C 阈值（再点将弹假的广告获取失败面板）；
    ///   - total      = 当日已正常观看次数；
    ///   - freeReady  = 是否可以正常观看（S=0 或 C=-1 时始终为 true）。
    /// </summary>
    public ShopGoldVideoState GetShopGoldVideoState()
    {
        ShopGoldVideoState s = new ShopGoldVideoState();

        EnsureShopGoldDay();

        AdFailedParams p = GetAdFailedParams();
        s.total = AdLimitData.shopGoldUsed;

        // S 关闭或 C=-1 → 不干预，始终可正常观看
        if (!p.enabled || p.shopGoldVideoLimit < 0)
        {
            s.used = false;
            s.freeReady = true;
        }
        else
        {
            s.used = AdLimitData.shopGoldUsed >= p.shopGoldVideoLimit;
            s.freeReady = !s.used;
        }

        return s;
    }

    /// <summary>
    /// 判断商店金币视频当前是否应该弹"假的广告获取失败弹窗"（不请求真实广告）。
    /// 判定顺序：
    ///   1. S=0 → 不弹；
    ///   2. 处于 T 秒冷却中 → 弹；
    ///   3. C=-1 → 不弹；
    ///   4. 当日已用次数 >= C → 弹。
    /// </summary>
    public bool ShouldShowFakeAdFailedForShopGold()
    {
        AdFailedParams p = GetAdFailedParams();
        if (!p.enabled) return false;
        if (p.isCooldown) return true;

        int limit = p.shopGoldVideoLimit;
        if (limit < 0) return false;

        EnsureShopGoldDay();
        return AdLimitData.shopGoldUsed >= limit;
    }

    /// <summary>
    /// 商店金币视频点击便捷入口：
    ///   - 返回 true  → 已弹出"假的广告获取失败面板"，调用方不要再去请求真实广告；
    ///   - 返回 false → 调用方正常请求广告。
    /// </summary>
    public bool TryHandleShopGoldClickAsFakeFail()
    {
        if (ShouldShowFakeAdFailedForShopGold())
        {
            ShowAdFailedPanel();
            return true;
        }
        return false;
    }

    /// <summary>
    /// 商店金币视频正常观看成功后的发放入口。
    /// 若当前已超过 C 阈值，则拒绝发放（兜底：外部若遗漏了点击前的假失败拦截）。
    /// </summary>
    public void OnShopGoldClaimed()
    {
        // ★ 关键修复：必须先按自然日归一化，再累加/判断阈值。
        //   原实现直接 _shopGoldUsedToday++，但从未写入日期；首次领取时日期为空，
        //   剩余次数计算里 used 恒为 0，表现为"每次看完广告，商店广告次数没有减"。
        EnsureShopGoldDay();

        // 兜底只看"次数阈值"，不要看 T 冷却：广告已经播完，不能因为冷却把奖励吞掉。
        AdFailedParams p = GetAdFailedParams();
        if (p.enabled && p.shopGoldVideoLimit >= 0 && AdLimitData.shopGoldUsed >= p.shopGoldVideoLimit)
        {
            LogShop("OnShopGoldClaimed 跳过: 已达 C 阈值（不应发放金币）");
            return;
        }

        AdLimitSaveData d = AdLimitData;
        d.shopGoldUsed++;
        PersistAdLimit();

        GameDataManager.Instance.UserData.UpdateGold(ShopGoldVideoReward, false, false, "商店金币视频");
        MessageSystem.Instance.ShowTip("+100 金币");
        LogShop($"OnShopGoldClaimed: 今日已领={d.shopGoldUsed}, 阈值={p.shopGoldVideoLimit}");
    }

    /// <summary>
    /// 商店金币视频正常观看成功后仅累计次数（不发放金币）。
    /// 用于 FreeGoods 商品等"由商品自身发放奖励"的视频路径，避免重复发奖。
    /// </summary>
    public void RecordShopGoldVideoWatched()
    {
        EnsureShopGoldDay();

        AdLimitSaveData d = AdLimitData;
        d.shopGoldUsed++;
        PersistAdLimit();
        LogShop($"RecordShopGoldVideoWatched: 今日已看={d.shopGoldUsed}, 阈值={GetAdFailedParams().shopGoldVideoLimit}");
    }

    // ======================= 分享奖励（F：每天分享次数上限） =======================

    public void OnShareClicked()
    {
        _shareClickedTime = DateTime.Now;
        LogShop($"OnShareClicked: {_shareClickedTime:HH:mm:ss}");
    }

    /// <summary>分享返回结算：校验每日上限/间隔并发奖+toast「分享成功 +1灯泡 +1复活 +50金币」。返回是否真正发奖。</summary>
    public bool OnShareReturned(bool fromBackground)
    {
        EnsureShareDay();
        if (AdLimitData.shareUsed >= GetAdFailedParams().shareDailyLimit)
        {
            LogShop("OnShareReturned 跳过: 今日分享已达上限");
            return false;
        }

        if (!fromBackground)
        {
            LogShop("OnShareReturned 跳过: 非切回前台");
            return false;
        }
        if ((DateTime.Now - _shareClickedTime).TotalSeconds < ShareSuccessMinSeconds)
        {
            LogShop("OnShareReturned 跳过: 切回间隔不足3秒");
            return false;
        }

        AdLimitData.shareUsed++;
        PersistAdLimit();
        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, 1, "分享奖励");     // 灯泡 +1
        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.ReStartTool, 1, "分享奖励");  // 复活 +1（需求 C-STRAT-06：灯泡1/复活1/金币50）
        GameDataManager.Instance.UserData.UpdateGold(50, false, false, "分享奖励");
        MessageSystem.Instance.ShowTip("分享成功 +1灯泡 +1复活 +50金币");
        LogShop($"OnShareReturned: 分享成功, 今日次数={AdLimitData.shareUsed}");
        return true;
    }

    // ======================= 破冰礼包策略 =======================

    private DateTime _lastBreakIcePopupTime = DateTime.MinValue;
    private int _breakIcePopupCount;

    public bool TryShowBreakIcePopup(int interAdsCountThisSession)
    {
        string[] bi = _layerConfig.brokenIce.Split('_');
        if (bi.Length < 1 || bi[0] != "1")
        {
            LogShop("TryShowBreakIcePopup 跳过: BrokenIce 开关关闭");
            return false;
        }
        if (!IsIaaLayer())
        {
            LogShop("TryShowBreakIcePopup 跳过: 非 IAA 层");
            return false;
        }
        if (interAdsCountThisSession <= 3)
        {
            LogShop($"TryShowBreakIcePopup 跳过: 本次插屏={interAdsCountThisSession} <= 3");
            return false;
        }

        if (!IsHolidayOrWeekend() && UnityEngine.Random.value > 0.3f)
        {
            LogShop("TryShowBreakIcePopup 跳过: 非周末/节日且随机未命中");
            return false;
        }

        // 已有互斥弹窗在展示时不叠加（注意：结算页本身是 UpPopPanel，不能直接用 IsPanelTypeShowing）
        if (IsExclusivePopupShowing())
        {
            LogShop("TryShowBreakIcePopup 跳过: 已有互斥弹窗在展示");
            return false;
        }

        // 本地与云端均无去广告（永久去广告 / 限时去广告生效中都不弹）
        if (IsRemoveAdsOwned() || IsRemoveAdsActive())
        {
            LogShop($"TryShowBreakIcePopup 跳过: 已拥有去广告(永久={IsRemoveAdsOwned()}, 限时生效={IsRemoveAdsActive()})");
            return false;
        }

        int cooldownDays = BreakIceCooldownBaseDays + _breakIcePopupCount * BreakIceCooldownStepDays;
        if (_lastBreakIcePopupTime != DateTime.MinValue
            && DateTime.Now.Subtract(_lastBreakIcePopupTime).TotalDays < cooldownDays)
        {
            LogShop($"TryShowBreakIcePopup 跳过: 冷却中, 需要{cooldownDays}天");
            return false;
        }

        // 破冰主力商品：一周去广告（配置表 RemoveAdsWeek）
        ShopDataItem pack = allShopItems.FirstOrDefault(x => x.produceNameId == ProductRemoveAdsWeekly);

        if (pack == null)
        {
            // 渠道未建周卡时，回退到定价最低的礼包，保证破冰链路可用（不去用终身去广告，价格过高不适合破冰）
            pack = allShopItems
                .Where(x => x.price > 0 && !IsRemoveAdsProduct(x.produceNameId))
                .OrderBy(x => x.price)
                .FirstOrDefault();
            if (pack != null)
                LogShop($"TryShowBreakIcePopup: 未配置 {ProductRemoveAdsWeekly}，回退最低价礼包 {pack.produceNameId}");
        }

        if (pack == null)
        {
            LogShop($"TryShowBreakIcePopup 跳过: 未找到 {ProductRemoveAdsWeekly} 且无可用回退礼包");
            return false;
        }

        _breakIcePopupCount++;
        _lastBreakIcePopupTime = DateTime.Now;

        // 限时 = 本次连续周末或节假日区间
        DateTime endTime = IsHolidayOrWeekend() ? GetCurrentHolidayEndTime() : DateTime.MinValue;
        SetPendingGiftPopup(pack, GiftPopupMode.BreakIce, endTime);
        SystemManager.Instance.ShowPanel(PanelType.AdsDiscountScreen);

        LogShop($"TryShowBreakIcePopup: 展示破冰礼包 {pack.produceNameId}, 累计次数={_breakIcePopupCount}, 下次冷却={BreakIceCooldownBaseDays + _breakIcePopupCount * BreakIceCooldownStepDays}天, 限时截止={(endTime == DateTime.MinValue ? "无" : endTime.ToString("yyyy-MM-dd HH:mm"))}");
        return true;
    }

    // ======================= 复购/升层策略 =======================

    private DateTime _lastRepurchasePopupTime = DateTime.MinValue;

    public bool TryShowRepurchasePopup(ShopDataItem lastPurchasedPack)
    {
        if (lastPurchasedPack == null)
        {
            LogShop("TryShowRepurchasePopup 跳过: lastPurchasedPack 为空");
            return false;
        }
        if (IsIaaLayer())
        {
            LogShop("TryShowRepurchasePopup 跳过: 非 IAP 层");
            return false;
        }
        if (GetPlayerGold() >= 200)
        {
            LogShop($"TryShowRepurchasePopup 跳过: 金币={GetPlayerGold()} >= 200");
            return false;
        }

        // 已有互斥弹窗在展示时不叠加
        if (IsExclusivePopupShowing())
        {
            LogShop("TryShowRepurchasePopup 跳过: 已有互斥弹窗在展示");
            return false;
        }
        if (_lastRepurchasePopupTime != DateTime.MinValue
            && DateTime.Now.Subtract(_lastRepurchasePopupTime).TotalDays < RepurchaseCooldownDays)
        {
            LogShop($"TryShowRepurchasePopup 跳过: 冷却中, 需要{RepurchaseCooldownDays}天");
            return false;
        }

        ShopDataItem a = lastPurchasedPack;
        float basePrice = Math.Max(a.price, 10);

        List<ShopDataItem> candidates = shopItems
            .Where(x => x.type == 2 && x.price >= basePrice && !IsRemoveAdsProduct(x.produceNameId)
                && !IsLimitedOut(x) && !IsActivityPack(x) && x.limited == 0)
            .OrderBy(x => x.price)
            .ToList();

        if (candidates.Count == 0)
        {
            LogShop("TryShowRepurchasePopup 跳过: 无候选礼包");
            return false;
        }

        ShopDataItem chosen = candidates.FirstOrDefault(x => x.giftType == a.giftType + 1);
        if (chosen == null)
        {
            int maxInvoke = candidates.Max(x => GetPayInvokeCount(x.produceNameId));
            var mostInvoked = candidates.Where(x => GetPayInvokeCount(x.produceNameId) == maxInvoke && maxInvoke > 0).ToList();
            if (mostInvoked.Count > 0)
                chosen = mostInvoked.OrderBy(x => x.price).First();
            else
                chosen = candidates[0];
        }

        // 加量不加价：额外金币 = 取整(礼包金币价值 V × 10%)（需求 C-STRAT-10）
        int bonusGold = Mathf.RoundToInt(chosen.coinValue * 0.1f);
        AddPackBonus(chosen, a, bonusGold);

        _lastRepurchasePopupTime = DateTime.Now;

        SetPendingGiftPopup(chosen, GiftPopupMode.Repurchase, DateTime.MinValue);
        SystemManager.Instance.ShowPanel(PanelType.AdsDiscountScreen);
        LogShop($"TryShowRepurchasePopup: 展示复购礼包, a={a.produceNameId}, chosen={chosen.produceNameId}, price={chosen.price}, 加赠金币={bonusGold}(coinValue={chosen.coinValue})");
        return true;
    }

    /// <summary>
    /// 给目标礼包追加额外金币（加量不加价，需求 C-STRAT-10）。
    /// </summary>
    /// <param name="bonusGold">额外金币数量（= 取整(金币价值 V × 10%)）</param>
    private void AddPackBonus(ShopDataItem target, ShopDataItem basePack, int bonusGold)
    {
        if (bonusGold <= 0)
        {
            LogShop($"AddPackBonus 跳过: 加赠金币={bonusGold} <= 0 (target={target?.produceNameId})");
            return;
        }

        if (target.productContent == null)
            target.productContent = new List<List<string>>();

        target.productContent.Add(new List<string>
        {
            ((int)LimitRewordType.Coins).ToString(),
            bonusGold.ToString()
        });

        LogShop($"AddPackBonus: {target.produceNameId} 加赠金币 {bonusGold}, 基准礼包={basePack?.produceNameId}");
    }

    // ======================= 弹窗互斥 =======================

    /// <summary>
    /// 是否有「互斥弹窗」正在展示（用于主动弹窗的并发控制）。
    /// 不能用 SystemManager.IsPanelTypeShowing：结算页 ChessFinishView / StageFinishView
    /// 本身是 UpPopPanel，会把主动弹窗永远挡住。
    /// </summary>
    private bool IsExclusivePopupShowing()
    {
        if (SystemManager.Instance == null) return false;

        string[] exclusive =
        {
            PanelType.SpecialOfferPopup,   // 特惠礼包（旧）
            PanelType.AdsDiscountScreen,   // 礼包弹窗（破冰/复购）
            PanelType.AdsFailScreen,            // 广告获取失败
            PanelType.BuyFailScreen,       // 取消购买
            PanelType.AwardScreen,         // 内购成功
            PanelType.AdsAwardScreen,      // 广告奖励
            PanelType.BuyListScreen,       // 购买记录
            PanelType.SevenSignScreen,     // 七日签到
            PanelType.DailyTasksScreen     // 每日任务
        };

        foreach (var p in exclusive)
        {
            if (SystemManager.Instance.PanelIsShowing(p))
            {
                LogShop($"IsExclusivePopupShowing=true: {p} 正在展示");
                return true;
            }
        }
        return false;
    }

    // ======================= 结算页弹窗调度（破冰 → 复购） =======================

    /// <summary>
    /// 结算页统一入口：按 破冰 → 复购 顺序尝试弹出礼包弹窗（需求 C-STRAT-12）。
    /// 两者互斥，任一弹出即返回 true；调用方无需关心内部策略。
    /// </summary>
    /// <param name="interAdsCountThisSession">本次启动已播放插屏次数（破冰要求 > 3）</param>
    public bool TryShowSettlementGiftPopup(int interAdsCountThisSession)
    {
        if (ShopManager.shopManager == null) return false;

        if (TryShowBreakIcePopup(interAdsCountThisSession))
            return true;

        return TryShowRepurchasePopup(_lastPurchasedPack);
    }

    // ======================= 数据访问桩 =======================

    protected virtual long GetPlayerGold()
    {
        if (GameDataManager.Instance == null || GameDataManager.Instance.UserData == null) return 0;
        return GameDataManager.Instance.UserData.Gold;
    }

    protected virtual int GetPlayerToolCount()
    {
        if (GameDataManager.Instance == null || GameDataManager.Instance.UserData == null) return 0;
        return GameDataManager.Instance.UserData.toolInfo[102].count;
    }
}