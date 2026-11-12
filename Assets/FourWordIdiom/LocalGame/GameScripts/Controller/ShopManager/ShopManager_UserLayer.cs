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
///   2. 活跃周期天数 N（调试宏 Unity_ShowLog 下用首次登录起算的 lifeDays，
///      正式包 Unity_Release 下用累加型 UserData.activeDayCnt，详见 GetActiveDayCount）；
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

    // ======================= 启动日志（分层 + 生效配置） =======================

    /// <summary>启动日志是否已打印（每次进程启动只打一次）</summary>
    private bool _startupLayerLogged;

    /// <summary>
    /// 启动日志：打印当前用户分层与正在执行的配置（每次启动调用一次，内部去重）。
    /// 由 AdTrackingManager 启动流程收尾（FinishStartupFlow / 超时放行）触发。
    /// </summary>
    public void LogStartupLayerAndConfig()
    {
        if (_startupLayerLogged) return;
        _startupLayerLogged = true;

        var userData = GameDataManager.Instance != null ? GameDataManager.Instance.UserData : null;
        string layer = GetCurrentUserLayer();   // 顺带按当前分层刷新 _layerConfig

        string basis = userData == null
            ? "UserData=null（走默认层）"
            : $"adTrackingEnabled={userData.adTrackingEnabled}, 判定天数={GetActiveDayCount(userData)}, " +
              $"isPaid={userData.TotalPayTimes > 0}, MaxSinglePayAmount={userData.MaxSinglePayAmount:F2}, " +
              $"TotalPayTimes={userData.TotalPayTimes}";

        LogShop("[启动] ========== 用户分层与生效配置 ==========");
        LogShop($"[启动] 当前用户分层={layer} | 分层依据: {basis}");
        LogShop($"[启动] 执行的分层配置(shop_layer/{layer}): AdFailed={_layerConfig.adFailed}, " +
                $"BrokenIce={_layerConfig.brokenIce}, InterAdsGroup={_layerConfig.interAdsGroup}, " +
                $"AdTrackingStimulate={_layerConfig.adTrackingStimulate}, AllowSpecialPopup={_layerConfig.allowSpecialPopup}");

        if (AdRuleManager.Instance != null)
        {
            AdRuleManager.Instance.LogCurrentConfig();
        }
        else
        {
            LogShop("[启动] AdRuleManager 未初始化，跳过广告规则配置日志");
        }
    }

    // ======================= 获取当前用户分层（含配置刷新） =======================

    /// <summary>
    /// 取「用于分层判定的天数」。两种口径按编译宏切换：
    ///
    /// 【Unity_ShowLog / UNITY_EDITOR】调试模式 → 用生命周期天数 lifeDays
    ///   lifeDays = (今天 - 首次登录日期).Days + 1，按日期差实时计算。
    ///   好处：改本地系统时间后立刻生效，方便验证各个分层档位。
    ///
    /// 【Unity_Release 等正式包】→ 用累加型活跃天数 activeDayCnt
    ///   只在真正登录（GameStart(true)）时 +1，同一天多次登录不重复计。
    ///   好处：不受用户改系统时间影响，避免刷分层。
    /// </summary>
    private int GetActiveDayCount(UserData userData)
    {
        if (userData == null) return 0;

#if Unity_ShowLog || UNITY_EDITOR
        // 调试：按首次登录日期实时算生命周期天数，改本地时间即可生效
        if (!string.IsNullOrEmpty(userData.firstLoginTime) &&
            DateTime.TryParse(userData.firstLoginTime, out var firstLogin))
        {
            int lifeDays = (DateTime.Now.Date - firstLogin.Date).Days + 1;
            return lifeDays > 0 ? lifeDays : 1;
        }
        // 首次登录时间缺失时回退累加值
        return userData.activeDayCnt;
#else
        return userData.activeDayCnt;
#endif
    }

    /// <summary>
    /// 获取当前用户分层，并同时按分层刷新 _layerConfig。
    ///
    /// 分层依据（需求《用户标记及分层策略规则》）：
    ///   1. 广告追踪权限状态：UserData.adTrackingEnabled；
    ///   2. 活跃周期天数 N：调试模式用 lifeDays，正式包用 UserData.activeDayCnt（见 GetActiveDayCount）；
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
        int activeDayCnt = GetActiveDayCount(userData);
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
                // A ≤ 9：付费累积次数 > 1 → IAP12，= 1 → IAP11
                // （bug35：原来写成 payTimes > 100，导致买了 2 次的用户被判成 IAP11）
                layer = (payTimes > 1) ? "IAP12" : "IAP11";
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
    /// 换关同时清掉「广告失败冷却 T」：冷却只约束"同一关内不要反复弹"，
    /// 否则上一关用完次数弹过 AdsFailScreen 后，T（90~150s）会压到下一关，
    /// 新关卡视频点还有可用次数却一点就弹「广告无法加载」。
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

        // ★ 换关刷新广告失败冷却（T）：A/B 次数已按新关卡重置，冷却不应跨关生效。
        bool hadCooldown = d.adFailBlockedUntilTicks > 0 && DateTime.Now < GetAdFailBlockedUntil();
        d.adFailBlockedUntilTicks = 0;

        PersistAdLimit();
        LogShop($"[AdFailed] 换关 → {key}，提示/复活视频次数已刷新" +
                (hadCooldown ? "，上一关的广告失败冷却已清除" : string.Empty));
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
        // if (p.tipFailCooldown > 0)
        //     SetAdFailBlockedUntil(DateTime.Now.AddSeconds(p.tipFailCooldown));

        _lastAdFailedToolType = LimitRewordType.Coins; // 通用入口 = 商店金币视频路径
        //LogShop($"ShowAdFailedPanel (通用): 冷却={p.tipFailCooldown}s, 截止={GetAdFailBlockedUntil():HH:mm:ss}");
        LogShop($"ShowAdFailedPanel (通用)");
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

        // 奖励改用 AwardScreen 展示（替代原先的 Toast）
        ShowShareAwardScreen();

        LogShop($"OnShareReturned: 分享成功, 今日次数={AdLimitData.shareUsed}");
        return true;
    }

    /// <summary>
    /// 分享成功奖励弹窗：灯泡 1 / 复活 1 / 金币 50。
    /// 通过 AwardScreen.OverrideRewards 展示，不复用商店商品数据。
    /// </summary>
    private void ShowShareAwardScreen()
    {
        AwardScreen.OverrideRewards = new List<RewardItem>
        {
            new RewardItem { type = (int)LimitRewordType.Tipstool,   amount = 1  },  // 灯泡
            new RewardItem { type = (int)LimitRewordType.ReStartTool, amount = 1  }, // 复活
            new RewardItem { type = (int)LimitRewordType.Coins,       amount = 50 },  // 金币
        };

        Debug.Log("[Shop] 分享成功，弹出 AwardScreen 展示奖励");
        SystemManager.Instance.ShowPanel(PanelType.AwardScreen);
    }

    // ======================= 破冰礼包策略 =======================

    private DateTime _lastBreakIcePopupTime = DateTime.MinValue;

    /// <summary>
    /// 破冰弹窗已展示次数（本地持久化，决定冷却天数 7/10/13…）。
    /// 以前只在内存里，杀进程就回到 0 → 冷却天数也跟着退回 7 天。
    /// </summary>
    private int _breakIceCountCache = -1;
    private int BreakIcePopupCount
    {
        get
        {
            if (_breakIceCountCache < 0)
                _breakIceCountCache = PlayerPrefs.GetInt(PrefBreakIceCount, 0);
            return _breakIceCountCache;
        }
        set
        {
            _breakIceCountCache = value;
            PlayerPrefs.SetInt(PrefBreakIceCount, value);
            PlayerPrefs.Save();
        }
    }

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

        // 【弹窗条件 5】当前时间必须 ∈ 周末 或 节假日 —— 硬条件。
        // 旧逻辑是「非周末/节假日时 30% 随机放行」，导致 2026-11-09（周一）也弹了破冰礼包；
        // 策划口径是非周末/节假日不弹（周末/节假日的判定见 ShopManager_HolidayWeekend.IsHolidayOrWeekend）。
        if (!IsHolidayOrWeekend())
        {
            LogShop($"TryShowBreakIcePopup 跳过: 今天({DateTime.Now:yyyy-MM-dd} {DateTime.Now.DayOfWeek})不是周末/节假日");
            return false;
        }

        // 已有互斥弹窗在展示时不叠加（注意：结算页本身是 UpPopPanel，不能直接用 IsPanelTypeShowing）
        if (IsExclusivePopupShowing())
        {
            LogShop("TryShowBreakIcePopup 跳过: 已有互斥弹窗在展示");
            return false;
        }

        // 条件3：结算页其它弹窗（插屏 / 七日胜利 / 灯明心悟 / 新手奖励进度 / 锦鲤竞速开始 等）
        //   ★ 与复购同一口径：这些更优先的弹窗会弹时，破冰礼包不弹
        if (IsSettlementOtherPopupShowing())
        {
            LogShop("TryShowBreakIcePopup 跳过: 结算页已有其它弹窗/插屏在展示");
            return false;
        }

        // 本地与云端均无去广告（永久去广告 / 限时去广告生效中都不弹）
        if (IsRemoveAdsOwned() || IsRemoveAdsActive())
        {
            LogShop($"TryShowBreakIcePopup 跳过: 已拥有去广告(永久={IsRemoveAdsOwned()}, 限时生效={IsRemoveAdsActive()})");
            return false;
        }

        // 破冰冷却：从「上次弹窗倒计时结束」起算（策划口径，与复购/升层一致）
        //   - 倒计时还没结束 → 仍在本次限时展示期内，继续展示；
        //   - 倒计时结束后 N 天内（N = 7/10/13 递增）→ 冷却中；
        //   - 无倒计时（非周末/节假日弹出的情况）→ 退化为从弹窗那一刻算，保持原行为。
        int cooldownDays = BreakIceCooldownBaseDays + BreakIcePopupCount * BreakIceCooldownStepDays;
        DateTime lastCooldownStart = GetBreakIceCooldownStart();
        bool inOfferCountdown = lastCooldownStart != DateTime.MinValue && DateTime.Now < lastCooldownStart;

        if (!inOfferCountdown
            && lastCooldownStart != DateTime.MinValue
            && DateTime.Now.Subtract(lastCooldownStart).TotalDays < cooldownDays)
        {
            LogShop($"TryShowBreakIcePopup 跳过: 冷却中（上次倒计时结束={lastCooldownStart:yyyy-MM-dd HH:mm}，" +
                    $"已过 {DateTime.Now.Subtract(lastCooldownStart).TotalDays:F1} 天，需满 {cooldownDays} 天）");
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

        // 限时 = 本次连续周末或节假日区间；倒计时未结束时沿用上次的截止时间（不重置倒计时）
        DateTime endTime;
        if (inOfferCountdown)
        {
            endTime = lastCooldownStart;
            LogShop($"TryShowBreakIcePopup: 上次弹窗倒计时未结束（截止 {lastCooldownStart:yyyy-MM-dd HH:mm}）→ 继续展示 {pack.produceNameId}");
        }
        else
        {
            // 走到这里已经确认是周末/节假日，限时固定取本次连续周末/节假日的结束时间（不会是 MinValue）
            endTime = GetCurrentHolidayEndTime();

            // 新一轮：累计次数 +1，并记录本轮冷却起点（倒计时结束时间）
            BreakIcePopupCount = BreakIcePopupCount + 1;
            SaveBreakIcePopupState(endTime, BreakIcePopupCount);
            _lastBreakIcePopupTime = DateTime.Now;
        }

        // 登记到「结算页礼包」并弹窗：结算页的破冰礼包按钮据此显示，关掉弹窗也能再打开
        ShowSettlementGift(SettlementGiftKind.BreakIce, pack, endTime);

        LogShop($"TryShowBreakIcePopup: 展示破冰礼包 {pack.produceNameId}, 累计次数={BreakIcePopupCount}, " +
                $"下次冷却={BreakIceCooldownBaseDays + BreakIcePopupCount * BreakIceCooldownStepDays}天" +
                $"（从倒计时结束 {(inOfferCountdown ? lastCooldownStart : endTime):yyyy-MM-dd HH:mm} 起算）, " +
                $"限时截止={(endTime == DateTime.MinValue ? "无" : endTime.ToString("yyyy-MM-dd HH:mm"))}");
        return true;
    }

    // ======================= 复购/升层策略（bug54） =======================

    /// <summary>弹窗门槛：玩家持有金币 &lt; 200 才弹（需求·弹窗条件 6）</summary>
    private const long RepurchaseMaxGold = 200;

    /// <summary>金币 &lt; 100 时才按「支付调起次数最多」选包（需求·弹出礼包 3）</summary>
    private const long RepurchaseLowGoldThreshold = 100;

    /// <summary>基准价：MAX(A, 10)</summary>
    private const float RepurchaseMinBasePrice = 10f;

    /// <summary>
    /// 结算页「复购/升层礼包」弹窗（IAP 层）。
    ///
    /// 弹窗条件（策划）：
    ///   1. 用户属于任意 IAP 层；
    ///   2. 关卡通关、结算时（由 TryShowSettlementGiftPopup 调用）；
    ///   3. 结算页没有其它弹窗（插屏 / 七日胜利 / 灯明心悟（LimitTimeScreen）/ 新手奖励进度（项目暂无）/ 锦鲤竞速开始 等）；
    ///   4. 当前是周末或节假日；
    ///   5. 弹窗冷却：同一个活动期内**只主动弹一次**（弹过之后本次活动的后续结算不再主动弹，
    ///      但结算页的「限时折扣」按钮整个活动期都保留，见 RegisterRepurchaseSettlementButton）；
    ///      活动（倒计时）结束后满 7 天才会有新的弹窗。
    ///   6. 金币 &lt; 200。
    ///
    /// 弹出哪一款（★ 策划口径：A 优先）：
    ///   1. 有「最近一次成功购买的礼包 A」（不含金币商品和两个去广告）→ 直接弹 A 的优惠款（复购）；
    ///   2. 没有 A（从没成功买过礼包）→ 才走「支付调起次数」规则：
    ///      取调起次数最多的礼包，并列取价格最低的；连调起记录都没有则不弹；
    ///   3. 优惠款 = 加量不加价（额外金币 = 取整(礼包金币价值 × 10%)，显示在 GiftAddItem 里）；
    ///   4. 限时 = 本次连续周末 / 节假日结束时间。
    ///
    /// ★ bug54 根因：A / 调起次数 / 上次弹窗时间以前都是运行时字段，杀进程就丢 → 条件永远凑不齐；
    ///   且没有「周末或节假日」这一条、选了「升档礼包（giftType+1）」而不是策划要求的调起次数规则、限时也没设。
    /// </summary>
    public bool TryShowRepurchasePopup(ShopDataItem lastPurchasedPack)
    {
        LogShop("========== 复购礼包判定开始 ==========");

        // 兼容旧调用方：外部传进来的礼包A作为兜底（本地记录优先）
        if (lastPurchasedPack != null) _lastPurchasedPack = lastPurchasedPack;

        // 条件1：只对 IAP 层推（IAA 层走破冰礼包）
        if (IsIaaLayer())
        {
            LogShop($"TryShowRepurchasePopup 跳过: 当前是 IAA 层（{GetCurrentUserLayer()}）");
            return false;
        }

        // 条件4：只在周末 / 节假日弹
        if (!IsHolidayOrWeekend())
        {
            LogShop($"TryShowRepurchasePopup 跳过: 今天既不是周末也不是节假日（{DateTime.Now:yyyy-MM-dd dddd}）");
            return false;
        }

        // 条件5 的「本次展示期」判定提前到这里：Now < 上次倒计时结束 = 本活动期已经主动弹过一次。
        // ★ 新口径（2026-11-08）：同一个活动期内**只主动弹一次**，之后不再主动弹出；
        //   但活动期间结算页的「限时折扣（复购）」按钮要一直保留（玩家想买随时能点开）。
        DateTime lastPopupEndTime = GetRepurchaseLastPopupEndTime();
        bool inOfferCountdown = lastPopupEndTime != DateTime.MinValue && DateTime.Now < lastPopupEndTime;

        if (inOfferCountdown)
        {
            RegisterRepurchaseSettlementButton(lastPopupEndTime);
            return true;
        }

        // 条件6：金币 < 200
        long gold = GetPlayerGold();
        if (gold >= RepurchaseMaxGold)
        {
            LogShop($"TryShowRepurchasePopup 跳过: 金币={gold} >= {RepurchaseMaxGold}");
            return false;
        }

        // 条件3：结算页其它弹窗（插屏 / 七日胜利 / 灯明心悟 / 新手奖励进度 / 锦鲤竞速开始 等）
        if (IsSettlementOtherPopupShowing())
        {
            LogShop("TryShowRepurchasePopup 跳过: 结算页已有其它弹窗/插屏在展示");
            return false;
        }

        // 条件5：弹窗冷却（策划口径：从「上次弹窗倒计时结束」开始算 7 天）
        //   - 倒计时结束后 7 天内 → 冷却中，不弹（这时候也不给按钮，活动已经结束）；
        //   - 倒计时结束后满 7 天 → 可以出新的弹窗。
        if (lastPopupEndTime != DateTime.MinValue
            && DateTime.Now.Subtract(lastPopupEndTime).TotalDays < RepurchaseCooldownDays)
        {
            LogShop($"TryShowRepurchasePopup 跳过: 冷却中（上次倒计时结束={lastPopupEndTime:yyyy-MM-dd HH:mm}，" +
                    $"已过 {DateTime.Now.Subtract(lastPopupEndTime).TotalDays:F1} 天，需满 {RepurchaseCooldownDays} 天）");
            return false;
        }

        // 基准礼包 A：最近一次完成购买的商店礼包（本地记录，排除金币商品/去广告）。
        // ★ 策划口径：优先弹「最近一次成功购买的礼包A」的优惠款；没有A时才走「支付调起次数」规则。
        ShopDataItem a = GetRepurchaseBasePack();

        // A 已经限购售罄 → 再弹它也买不了，退回调起次数规则
        if (a != null && IsLimitedOut(a))
        {
            LogShop($"[复购] 礼包A={a.produceNameId} 已售罄 → 改走支付调起次数规则");
            a = null;
        }

        float basePrice = a != null ? Mathf.Max(a.price, RepurchaseMinBasePrice) : RepurchaseMinBasePrice;

        // 候选：有 A 时按「价格 ≥ MAX(A,10)」；没有 A 时取全部可售礼包（靠调起次数挑）
        List<ShopDataItem> candidates = BuildRepurchaseCandidates(a, basePrice);

        LogShop(a != null
            ? $"[复购] 礼包A={a.produceNameId}(price={a.price})，基准价={basePrice}，" +
              $"候选={candidates.Count} 个：[{string.Join(", ", candidates.Select(x => $"{x.produceNameId}({x.price}/调起{GetPayInvokeCount(x.produceNameId)}次)"))}]"
            : $"[复购] 没有「成功购买的礼包A」→ 走支付调起次数规则，候选={candidates.Count} 个：" +
              $"[{string.Join(", ", candidates.Select(x => $"{x.produceNameId}({x.price}/调起{GetPayInvokeCount(x.produceNameId)}次)"))}]");

        // 走到这里说明本活动期还没弹过（inOfferCountdown 已在上面 return）→ 正常选包 + 主动弹窗
        ShopDataItem chosen = PickRepurchasePack(a, candidates);
        // 限时 = 本次连续周末 / 节假日结束时间
        DateTime endTime = GetCurrentHolidayEndTime();

        if (chosen == null)
        {
            LogShop("TryShowRepurchasePopup 跳过: 选包结果为空");
            return false;
        }

        // 优惠款：加量不加价（额外金币 = 取整(礼包金币价值 V × 10%)）
        int bonusGold = Mathf.RoundToInt(chosen.coinValue * 0.1f);

        ShowRepurchasePopup(chosen, a, bonusGold, endTime);
        return true;
    }

    /// <summary>
    /// 复购候选礼包池：type=2、非去广告、未售罄、非活动礼包、不限购；有 A 时要求价格 ≥ MAX(A,10)。
    /// </summary>
    private List<ShopDataItem> BuildRepurchaseCandidates(ShopDataItem a, float basePrice)
    {
        return shopItems
            .Where(x => x != null && x.type == 2
                        && (a == null || x.price >= basePrice)
                        && !IsRemoveAdsProduct(x.produceNameId)
                        && !IsLimitedOut(x)
                        && !IsActivityPack(x)
                        && x.limited == 0)
            .OrderBy(x => x.price)
            .ToList();
    }

    /// <summary>
    /// 本活动期复购弹窗已经主动弹过一次 → 之后的结算只登记「限时折扣」按钮，<b>不再主动弹窗</b>。
    /// 礼包沿用上次展示的那款；它已经卖完就换一款（只换展示，不动倒计时/冷却）。
    /// </summary>
    private void RegisterRepurchaseSettlementButton(DateTime offerEndTime)
    {
        ShopDataItem offer = GetRepurchaseLastOfferPack();
        if (offer != null && IsLimitedOut(offer))
        {
            LogShop($"[复购] 上次展示的 {offer.produceNameId} 已售罄 → 换一款给结算页按钮（不弹窗）");
            offer = null;
        }

        if (offer == null)
        {
            ShopDataItem a = GetRepurchaseBasePack();
            if (a != null && IsLimitedOut(a)) a = null;
            float basePrice = a != null ? Mathf.Max(a.price, RepurchaseMinBasePrice) : RepurchaseMinBasePrice;
            offer = PickRepurchasePack(a, BuildRepurchaseCandidates(a, basePrice));
            if (offer != null) SaveRepurchaseOfferPack(offer.produceNameId);
        }

        if (offer == null)
        {
            LogShop("[复购] 本活动已展示过，但已无可用礼包 → 结算页不显示复购按钮");
            ClearSettlementGift("复购无可用礼包");
            return;
        }

        // 优惠款：加量不加价（额外金币 = 取整(礼包金币价值 V × 10%)），与主动弹窗口径一致
        int bonusGold = Mathf.RoundToInt(offer.coinValue * 0.1f);
        ShopDataItem display = offer.DeepCopy();

        // ★ 只登记不弹窗（ShowSettlementGift 才会 OpenSettlementGiftPopup）
        RegisterSettlementGift(SettlementGiftKind.Repurchase, display, offerEndTime, bonusGold, offer.produceNameId);

        LogShop($"[复购] 本活动已主动弹过 → 不再弹出，仅保留结算页按钮: {offer.produceNameId}, " +
                $"加赠金币={bonusGold}, 限时截止={offerEndTime:yyyy-MM-dd HH:mm:ss}");
    }

    /// <summary>
    /// 选包（策划口径）：
    ///   1. 优先「最近一次成功购买的礼包A」的优惠款（复购）；
    ///   2. 没有 A（从没成功买过礼包）才走「支付调起次数」：次数最多 → 并列取价格最低；
    ///   3. 连调起记录也没有 → 不弹（返回 null）。
    /// </summary>
    private ShopDataItem PickRepurchasePack(ShopDataItem a, List<ShopDataItem> candidates)
    {
        // 1. 有成功购买的礼包 → 直接弹它的优惠款
        if (a != null)
        {
            LogShop($"[复购] 优先弹最近一次成功购买礼包的优惠款：{a.produceNameId}(price={a.price})");
            return a;
        }

        // 2. 没有 A → 用支付调起次数（玩家点过但没付款的礼包）
        if (candidates == null || candidates.Count == 0)
        {
            LogShop("[复购] 没有成功购买的礼包A，也没有可售礼包 → 不弹");
            return null;
        }

        int maxInvoke = 0;
        for (int i = 0; i < candidates.Count; i++)
            maxInvoke = Mathf.Max(maxInvoke, GetPayInvokeCount(candidates[i].produceNameId));

        if (maxInvoke <= 0)
        {
            LogShop("[复购] 没有成功购买的礼包A，也没有支付调起记录 → 不弹");
            return null;
        }

        ShopDataItem picked = candidates
            .Where(x => GetPayInvokeCount(x.produceNameId) == maxInvoke)
            .OrderBy(x => x.price)
            .FirstOrDefault();

        LogShop($"[复购] 无礼包A → 取调起次数最多({maxInvoke})的礼包：{picked?.produceNameId}");
        return picked;
    }

    /// <summary>展示复购弹窗：展示用副本里补一条加赠金币，真实商品数据不动，加赠只在成交那一单发放。</summary>
    private void ShowRepurchasePopup(ShopDataItem pack, ShopDataItem basePack, int bonusGold, DateTime endTime)
    {
        if (pack == null) return;

        ShopDataItem display = pack.DeepCopy();
        // 展示副本保持礼包原始内容（7 个就是 7 个）；
        // 加赠金币不塞进 productContent，由 AdsDiscountScreen 的 GiftAddItem 单独展示

        // 记录「本次展示」：冷却从倒计时结束开始算（策划口径），并记住这款礼包，
        // 倒计时未结束时下次结算继续展示同一款。
        SaveRepurchaseLastPopupTime(DateTime.Now);
        SaveRepurchaseLastPopupEndTime(endTime, pack.produceNameId);

        // 登记到「结算页礼包」（结算页的限时折扣礼包按钮据此显示），并弹出 AdsDiscountScreen
        // （展示用副本 + 加赠金币都由 ShowSettlementGift/OpenSettlementGiftPopup 写进 pending 数据）
        ShowSettlementGift(SettlementGiftKind.Repurchase, display, endTime, bonusGold, pack.produceNameId);

        LogShop($"TryShowRepurchasePopup: 展示复购礼包 A={basePack?.produceNameId}, chosen={pack.produceNameId}, " +
                $"price={pack.price}, 加赠金币={bonusGold}(coinValue={pack.coinValue}), " +
                $"限时截止={endTime:yyyy-MM-dd HH:mm:ss}, 当前金币={GetPlayerGold()}");
    }

    /// <summary>
    /// 结算页是否有「不能同时弹复购礼包」的其它界面。
    ///
    /// 需求第 3 条（结算页不弹的 5 类）与本项目界面对应关系：
    ///   1. 插屏                      → AdRuleManager.IsInterstitialShowing
    ///   2. 七日胜利弹窗              → PanelType.SignWinScreen / SignAwardScreen / SevenSignScreen
    ///   3. 灯明心悟页面              → PanelType.LimitTimeScreen（限时活动页，策划确认）
    ///   4. 新手奖励活动进度弹窗      → 项目里暂无该活动（预留：以后加了把 PanelType 填进来即可）
    ///   5. 锦鲤竞速活动开始弹窗      → PanelType.CompetitionStart（同族 DashCompetition / MatchSuccess / CompetitionFail / CompetitionHelp）
    ///
    /// 命中会打日志说明被谁挡住，方便按实际表现增删。
    /// </summary>
    private bool IsSettlementOtherPopupShowing()
    {
        if (SystemManager.Instance == null) return false;

        // 1. 插屏广告（不是 Panel，单独取广告规则状态）
        if (AdRuleManager.Instance != null && AdRuleManager.Instance.IsInterstitialShowing)
        {
            LogShop("[复购] 被拦截：插屏正在展示");
            return true;
        }

        // 已有的互斥弹窗（内购成功 / 广告奖励 / 七日签到 / 每日任务 / 破冰复购弹窗本身 ...）
        if (IsExclusivePopupShowing()) return true;

        string[] others =
        {
            // 3. 灯明心悟（限时活动页）
            PanelType.LimitTimeScreen,

            // 2. 七日胜利弹窗 / 七日签到
            PanelType.SignWinScreen,
            PanelType.SignAwardScreen,
            PanelType.SevenSignScreen,

            // 5. 锦鲤竞速（活动开始弹窗 + 同族结算/失败/说明）
            PanelType.CompetitionStart,
            PanelType.DashCompetition,
            PanelType.MatchSuccess,
            PanelType.CompetitionFail,
            PanelType.CompetitionHelp,

            // 其它可能挡在结算页上的弹窗
            PanelType.ReturnFirstWinScreen,   // 回归首胜
            PanelType.RateUsScreen,           // 评分
            PanelType.PolicyView,             // 隐私协议
            PanelType.HardView                // 难度提示
        };

        foreach (var p in others)
        {
            if (SystemManager.Instance.PanelIsShowing(p))
            {
                LogShop($"[复购] 被拦截：{p} 正在展示");
                return true;
            }
        }

        return false;
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

        // 每次结算重新决定：先清掉上一次结算遗留的礼包登记（结算页按钮据此显示）
        ClearSettlementGift("新的结算开始");

        LogShop($"[结算礼包] 开始调度: 本次插屏={interAdsCountThisSession}, 分层={GetCurrentUserLayer()}, " +
                $"金币={GetPlayerGold()}, 周末/节假日={IsHolidayOrWeekend()}, " +
                $"上次复购倒计时结束={GetRepurchaseLastPopupEndTime():yyyy-MM-dd HH:mm}");

        if (TryShowBreakIcePopup(interAdsCountThisSession))
            return true;

        // 礼包A 由 TryShowRepurchasePopup 内部从本地记录取（跨进程保留），这里不用再传
        return TryShowRepurchasePopup(null);
    }

    /// <summary>
    /// 【预登记】只跑策略、只登记结算页礼包（不弹窗），让破冰/复购按钮跟结算页一起出现。
    /// 之后再调 PlayPreparedSettlementGiftPopup() 才真正弹 AdsDiscountScreen。
    /// 用于拼字结算页：以前是结算页出来 1.2s 后才判定，按钮「啪」地晚一步冒出来，很突兀。
    /// </summary>
    public bool PrepareSettlementGift(int interAdsCountThisSession)
    {
        if (ShopManager.shopManager == null) return false;

        _suspendGiftPopup = true;
        try
        {
            TryShowSettlementGiftPopup(interAdsCountThisSession);
        }
        finally
        {
            _suspendGiftPopup = false;
        }

        LogShop($"[结算礼包] 预登记完成: kind={CurrentSettlementGift}, 待弹窗={_settlementGiftPopupPending}, " +
                $"礼包={GetSettlementGiftPackId()}");
        return CurrentSettlementGift != SettlementGiftKind.None;
    }

    /// <summary>结算页展示一小会儿后，把预登记好的礼包弹窗弹出来（没有则为空操作）。</summary>
    public void PlayPreparedSettlementGiftPopup()
    {
        if (!_settlementGiftPopupPending) return;

        // ★ 到点前再确认一次：结算页已经被插屏 / 七日胜利 / 灯明心悟 / 锦鲤竞速等占住 → 礼包不弹（按钮保留）
        if (IsSettlementOtherPopupShowing())
        {
            CancelPreparedSettlementGiftPopup("到点时有更优先的结算页弹窗/插屏在展示");
            return;
        }

        _settlementGiftPopupPending = false;
        LogShop($"[结算礼包] 预登记礼包到点弹窗: kind={CurrentSettlementGift}, 礼包={GetSettlementGiftPackId()}");
        OpenSettlementGiftPopup();
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