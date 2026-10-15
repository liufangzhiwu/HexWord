using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Middleware;
using UnityEngine;
#if UNITY_IOS
using UnityEngine.Purchasing;
#endif

/// <summary>
/// 内购推荐位权重配置（来源：商店表内 BulbBox / RocketBox / ClockBox / DynamicStore 配置子表）
/// 字段顺序与配置表一致：nameID, GiftType, BaseWeight, MaxWeight, SpecialWeight, PeakWeight, ReturnWeight, Featured
/// </summary>
[Serializable]
public class ShopRecommendConfig
{
    public string nameID;
    public int giftType;        // 礼包类型/档位（火箭、复活配置表无此列时为 0）
    public int baseWeight;      // 基础权重
    public int maxWeight;       // 礼包权重上限（0=使用默认上限）
    public int specialWeight;   // 节日/周末权重（当天在节日/周末表中取该值）
    public int peakWeight;      // 高峰权重（19-23 点）
    public int returnWeight;    // 流失回归（最近7天未登录）
    public int featured;        // 内购推荐位：1=主力 2=价格锚点 3=诱饵（动态商店：1=一号位 2=二号位）
}

/// <summary>
/// 道具折扣机制配置
/// 参数格式：开关_折扣_灯泡道具每日允许出现次数_火箭道具每日允许出现次数_触发的时间间隔(秒)
/// 默认：1_5_3_2_300
/// </summary>
[Serializable]
public class ShopDiscountConfig
{
    public bool enable = true;              // 总开关
    public int bulbDailyLimit = 3;          // 灯泡道具每日允许出现次数
    public int rocketDailyLimit = 2;        // 火箭道具每日允许出现次数
    public int triggerInterval = 300;       // 触发的时间间隔(秒)
    public int unlockLevel = 7;             // 第7关解锁
}

// ======================= 商店模式 =======================

public enum ShopMode
{
    FixedStore,     // 固定商店页（金币栏进入）
    DynamicStore    // 动态商店弹窗（道具金币不足进入）
}

/// <summary>
/// 商店管理器主脚本（核心）。
/// 已按模块拆分为 partial 类，配套脚本：
///  - ShopManager_ShopConfig.cs      商店商品配置（ShopDataItem 数据模型 + CSV 解析 + 商品查询/列表）
///  - ShopManager_DynamicStore.cs    动态商店配置（推荐位列表、商店入口角标）
///  - ShopManager_BulbBox.cs         提示灯（灯泡）内购配置
///  - ShopManager_RocketBox.cs       火箭礼盒配置
///  - ShopManager_ClockBox.cs        （复活礼盒默认配置在主脚本内）
///  - ShopManager_HolidayWeekend.cs  特殊节日&周末配置
///  - ShopManager_UserLayer.cs       用户分层设计（广告失败策略、破冰/复购）
///
/// 购买跟踪数据（buyCount / payTendencyScore / exploreScore / conversionFailScore /
/// lastFailScoreTime / lastBuyTime / limitEndTime / isActivityPack / pushStateIndex）
/// 统一存储在 ShopLimitData 中，并挂在 GameDataManager.UserData.limitShopItems 下持久化。
/// </summary>
public partial class ShopManager : MonoBehaviour
{
    // ======================= 常量（与需求/配置表对齐） =======================

    /// <summary>去广告（终身）商品ID</summary>
    public const string ProductRemoveAds = "RemoveADS";
    /// <summary>灯泡道具单一道具商品</summary>
    public const string ProductSingleGoods = "SingleGoods";
    /// <summary>复活道具单一道具商品</summary>
    public const string ProductSingleClock = "SingleClockS";
    /// <summary>金币视频商品</summary>
    public const string ProductGold6 = "Gold6";

    /// <summary>去广告（限时一天）商品ID（新配置表待新增，预留）</summary>
    public const string ProductRemoveAdsDaily = "RemoveAdsWeekly";
    /// <summary>破冰礼包冷却：7+N 天，N 每次 +3</summary>
    private const int BreakIceCooldownBaseDays = 7;
    private const int BreakIceCooldownStepDays = 3;
    /// <summary>复购/升层礼包冷却：7 天</summary>
    private const int RepurchaseCooldownDays = 7;
    /// <summary>广告失败弹窗冷却 3600 秒（状态 C）</summary>
    private const float AdFailCooldownSeconds = 3600f;
    /// <summary>分享成功判定：切回前台且间隔 > 3 秒</summary>
    private const float ShareSuccessMinSeconds = 3f;
    /// <summary>F 值变化间隔 30 秒</summary>
    private const float FailScoreChangeInterval = 30f;
    /// <summary>高峰时段 19-23 点</summary>
    private const int PeakHourStart = 19;
    private const int PeakHourEnd = 23;
    /// <summary>流失回归：最近 7 天未登录</summary>
    private const int ChurnReturnDays = 7;
    /// <summary>金币视频点每次看完视频的金币数：由 50 调整到 100</summary>
    private const int ShopGoldVideoReward = 100;

    // ======================= 商品/状态（共享字段） =======================

    private List<ShopDataItem> allShopItems = new List<ShopDataItem>();
    private List<ShopDataItem> shopItems = new List<ShopDataItem>();

    /// <summary>当前限时商店物品（原主动弹出逻辑已按需求删除，此字段保留兼容）</summary>
    public ShopDataItem curshopAdsItem;

    public static ShopManager shopManager;

    private Dictionary<int, ShopLimitData> shoplimitDatas = new Dictionary<int, ShopLimitData>();
    private List<ShopDataItem> _limitAdsGifts = new List<ShopDataItem>();

    public Action<string, bool> UpdateAdsBtnUI;

    public Dictionary<int, GameObject> shopItemsTipsPanel = new Dictionary<int, GameObject>();

    [HideInInspector] public bool paysuccess; //支付成功

    // ======================= 推荐位决策共享状态 =======================

    /// <summary>各推荐位权重配置：nameID → 配置（由各推荐位配置脚本加载）</summary>
    private Dictionary<string, ShopRecommendConfig> _recommendConfigs = new Dictionary<string, ShopRecommendConfig>();

    /// <summary>各推荐位当前关卡是否已锁定（购买后当关不刷新新礼包）</summary>
    private Dictionary<string, bool> _slotLockedThisLevel = new Dictionary<string, bool>();

    /// <summary>礼包支付调起次数（成功吊起系统支付弹窗但未完成购买；复购策略选包用）</summary>
    private Dictionary<string, int> _payInvokeCount = new Dictionary<string, int>();

    /// <summary>各礼包档位（GiftType）购买次数（探价加分用）</summary>
    private readonly Dictionary<int, int> _tierBuyCount = new Dictionary<int, int>();

    /// <summary>各推荐位当前展示的新人礼（用于互斥判断）</summary>
    private readonly Dictionary<RecommendSlot, string> _shownNewcomer = new Dictionary<RecommendSlot, string>();

    /// <summary>上次购买的礼包（复购/升层策略的基准礼包 A）</summary>
    private ShopDataItem _lastPurchasedPack;

    // ======================= 道具折扣状态 =======================

    /// <summary>道具折扣配置</summary>
    private ShopDiscountConfig _discountConfig = new ShopDiscountConfig();

    /// <summary>灯泡/火箭道具折扣当天触发次数</summary>
    private int _bulbDiscountToday;
    private int _rocketDiscountToday;
    private string _discountDate = "";

    /// <summary>道具折扣触发时间间隔记录</summary>
    private DateTime _lastBulbDiscountTime;
    private DateTime _lastRocketDiscountTime;

    /// <summary>当前关卡灯泡/火箭折扣是否已消费（折扣使用一次后角标消失、价格恢复原价）</summary>
    private bool _bulbDiscountConsumed;
    private bool _rocketDiscountConsumed;

    // ======================= 激励视频/插屏赠礼状态 =======================

    /// <summary>激励视频赠礼：每天前3个灯泡视频的赠金币（50/30/20）</summary>
    private int _bulbVideoGiftCount;
    private string _bulbVideoGiftDate = "";
    /// <summary>插屏赠礼：每天前5个插屏赠 10 金币</summary>
    private int _interstitialGiftCount;
    private string _interstitialGiftDate = "";

    /// <summary>首个灯泡激励视频/插屏是否已赠送过 50 金币（感谢弹窗）</summary>
    private bool _firstBulbVideoGifted;

    [Header("商店模式")]
    public ShopMode shopMode = ShopMode.FixedStore;

    private void Awake()
    {
        if (shopManager == null)
        {
            shopManager = this;
            DontDestroyOnLoad(gameObject); // 保持商店管理器在场景切换时不销毁
        }
    }

    void Start()
    {
        TextAsset data = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "shop");
        if (data != null)
        {
            ParseShopItems(data.text);
            LogShop($"Start: 商店商品解析完成, 商品总数={allShopItems.Count}, shopItems={shopItems.Count}");
        }
        else
        {
            Debug.LogError("Failed to load CSV data.");
        }

        paysuccess = false;

        // 加载各模块配置（各 partial 脚本各自实现 Load*Config）
        LoadRuntimeConfigs();

        Initialize();

        LogShop($"Start 完成: shopMode={shopMode}, 推荐位配置数={_recommendConfigs.Count}, 限购数据={shoplimitDatas.Count}, 限时礼包={_limitAdsGifts.Count}");
    }

    private void LoadRuntimeConfigs()
    {
        LoadBulbBoxConfig();        // ShopManager_BulbBox.cs
        LoadRocketBoxConfig();      // ShopManager_RocketBox.cs
        LoadClockBoxConfig();       // 主脚本（复活礼盒默认配置）
        LoadDynamicStoreConfig();   // ShopManager_DynamicStore.cs
        LoadLayerConfig();          // ShopManager_UserLayer.cs
        LoadHolidayWeekendTable();  // ShopManager_HolidayWeekend.cs
        LoadDiscountConfig();       // 主脚本（道具折扣）

        LogShop($"LoadRuntimeConfigs 完成: 推荐位配置数={_recommendConfigs.Count}, 折扣开关={_discountConfig.enable}, 灯泡每日={_discountConfig.bulbDailyLimit}, 火箭每日={_discountConfig.rocketDailyLimit}, 间隔={_discountConfig.triggerInterval}s");
    }

    private void LoadClockBoxConfig()
    {
        TextAsset rec = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "shop_clockbox");
        if (rec != null)
        {
            ParseRecommendConfigs(rec.text);
            LogShop($"LoadClockBoxConfig: 从 Bundle 加载 shop_clockbox");
        }
        else
        {
            LogShop($"LoadClockBoxConfig: 未找到 shop_clockbox, 使用默认值");
        }
    }

    public void Initialize()
    {
        shoplimitDatas = GameDataManager.Instance.UserData.limitShopItems
            .Where(x => x != null)
            .GroupBy(x => x.id)
            .ToDictionary(g => g.Key, g => g.First());
        _limitAdsGifts = GetLimitAdsGifts();

        LogShop($"Initialize: 限购数据={shoplimitDatas.Count}, 限时礼包={_limitAdsGifts.Count}");
    }

    public void RefreshLimitData()
    {
        shoplimitDatas = GameDataManager.Instance.UserData.limitShopItems
            .Where(x => x != null)
            .GroupBy(x => x.id)
            .ToDictionary(g => g.Key, g => g.First());
        _limitAdsGifts = GetLimitAdsGifts();

        LogShop($"RefreshLimitData: 限购数据={shoplimitDatas.Count}, 限时礼包={_limitAdsGifts.Count}");
    }

    // ======================= 日志辅助 =======================

    private void LogShop(string message)
    {
        Debug.Log($"[Shop] {message}");
    }

    // ======================= ShopLimitData 读写辅助 =======================

    private ShopLimitData GetOrCreateLimitData(ShopDataItem item)
    {
        if (item == null) return null;

        ShopLimitData data;
        if (shoplimitDatas.TryGetValue(item.id, out data) && data != null)
            return data;

        data = GameDataManager.Instance.UserData.limitShopItems
            .FirstOrDefault(x => x != null && x.id == item.id);

        if (data == null)
        {
            data = new ShopLimitData
            {
                id = item.id,
                nameid = item.produceNameId,
                pushStateIndex = 1f,
            };
            GameDataManager.Instance.UserData.limitShopItems.Add(data);
            LogShop($"GetOrCreateLimitData 新建跟踪数据: id={item.id}, nameid={item.produceNameId}");
        }

        shoplimitDatas[item.id] = data;
        return data;
    }

    private ShopLimitData GetLimitData(ShopDataItem item)
    {
        if (item == null) return null;

        ShopLimitData data;
        if (shoplimitDatas.TryGetValue(item.id, out data) && data != null)
            return data;

        data = GameDataManager.Instance.UserData.limitShopItems
            .FirstOrDefault(x => x != null && x.id == item.id);

        if (data != null)
            shoplimitDatas[item.id] = data;

        return data;
    }

    // ======================= 去广告（非消耗型内购） =======================

    public bool IsRemoveAdsProduct(string produceNameId)
    {
        return produceNameId == ProductRemoveAds || produceNameId == ProductRemoveAdsDaily;
    }

    public bool IsRemoveAdsOwned()
    {
        return GameDataManager.Instance.UserData.limitShopItems
            .Any(item => item.isget && !item.isoverdate && item.adstype == (int)LimitRewordType.RemoveAds);
    }

    public bool IsRemoveAdsActive()
    {
        return GameDataManager.Instance.UserData.limitShopItems
            .Any(item => item.isget && !item.isoverdate
                && (item.adstype == (int)LimitRewordType.RemoveDayAds));
    }

    public double GetRemoveAdsRemainSeconds()
    {
        double remain = 0;
        foreach (var item in GameDataManager.Instance.UserData.limitShopItems)
        {
            if (!item.isget || item.isoverdate) continue;
            if (item.adstype != (int)LimitRewordType.RemoveAds && item.adstype != (int)LimitRewordType.RemoveDayAds)
                continue;

            DateTime end = item.GetEndTime();
            if (end != DateTime.MinValue)
            {
                remain = Math.Max(remain, end.Subtract(DateTime.Now).TotalSeconds);
            }
        }
        return Math.Max(0, remain);
    }

    public bool IsRemoveAdsGift()
    {
        return IsRemoveAdsOwned() || IsRemoveAdsActive();
    }

    // ======================= 内购推荐位推送规则（决策权重，公共逻辑） =======================

    public enum RecommendSlot
    {
        Bulb,       // 灯泡道具弹窗（一号主力/二号锚点/三号诱饵）
        Rocket,     // 火箭道具弹窗
        Clock,      // 复活道具弹窗
        DynamicStore,// 动态商店（一号位/二号位）
        FixedStore  // 固定商店（一号位）
    }

    private void ParseRecommendConfigs(string data)
    {
        var records = SplitCsvLines(data);
        if (records.Count == 0)
        {
            LogShop("ParseRecommendConfigs: CSV 记录为空");
            return;
        }

        // 定位字段名行：行内第0列或第2列为 nameID
        int nameRow = -1;
        for (int i = 0; i < Math.Min(records.Count, 3); i++)
        {
            var row = records[i];
            if (row.Count > 2 && (row[2] == "nameID" || row[0] == "nameID"))
            {
                nameRow = i;
                break;
            }
        }

        int startRow;
        Dictionary<string, int> colMap = null;
        bool hasGiftType = false;

        if (nameRow >= 0)
        {
            colMap = new Dictionary<string, int>();
            var header = records[nameRow];
            for (int j = 0; j < header.Count; j++)
            {
                string h = (header[j] ?? "").Trim();
                if (!string.IsNullOrEmpty(h) && !colMap.ContainsKey(h))
                    colMap[h] = j;
            }
            hasGiftType = colMap.ContainsKey("GiftType");
            startRow = nameRow + 1;
        }
        else
        {
            // 未找到字段名行 → 按 DynamicStore 固定列位置解析
            startRow = 1;
        }

        int parsed = 0;
        for (int i = startRow; i < records.Count; i++)
        {
            var f = records[i];
            if (f.Count < 7) continue;
            if (f.Count > 0 && (f[0] == "这列忽略" || f[0] == "价格" || f[0] == "nameID")) continue;

            string nameID;
            if (colMap != null && colMap.ContainsKey("nameID"))
                nameID = GetField(f, colMap["nameID"]);
            else
                nameID = GetField(f, 2);

            if (string.IsNullOrEmpty(nameID)) continue;

            ShopRecommendConfig cfg = new ShopRecommendConfig
            {
                nameID = nameID,
                giftType = hasGiftType ? GetInt(GetField(f, colMap["GiftType"])) : 3,
                baseWeight = GetInt(GetField(f, colMap != null && colMap.ContainsKey("BaseWeight") ? colMap["BaseWeight"] : 4)),
                maxWeight = GetInt(GetField(f, colMap != null && colMap.ContainsKey("MaxWeight") ? colMap["MaxWeight"] : 5)),
                specialWeight = GetInt(GetField(f, colMap != null && colMap.ContainsKey("SpecialWeight") ? colMap["SpecialWeight"] : 6)),
                peakWeight = GetInt(GetField(f, colMap != null && colMap.ContainsKey("PeakWeight") ? colMap["PeakWeight"] : 7)),
                returnWeight = GetInt(GetField(f, colMap != null && colMap.ContainsKey("ReturnWeight") ? colMap["ReturnWeight"] : 8)),
                featured = GetInt(GetField(f, colMap != null && colMap.ContainsKey("Featured") ? colMap["Featured"] : 9))
            };
            _recommendConfigs[cfg.nameID] = cfg;
            parsed++;
        }

        LogShop($"ParseRecommendConfigs: 解析 {parsed} 条推荐位配置, 累计={_recommendConfigs.Count}");
    }

    private void AddRecommend(string nameID, int giftType, int baseW, int maxW, int specialW, int peakW, int returnW, int featured)
    {
        _recommendConfigs[nameID] = new ShopRecommendConfig
        {
            nameID = nameID,
            giftType = giftType,
            baseWeight = baseW,
            maxWeight = maxW,
            specialWeight = specialW,
            peakWeight = peakW,
            returnWeight = returnW,
            featured = featured
        };
    }

    private List<ShopDataItem> GetSlotCandidates(RecommendSlot slot, int featured)
    {
        List<ShopDataItem> list = new List<ShopDataItem>();
        foreach (var kv in _recommendConfigs)
        {
            if (kv.Value.featured != featured) continue;
            bool inSlot;
            switch (slot)
            {
                case RecommendSlot.Bulb: inSlot = IsBulbPack(kv.Key); break;
                case RecommendSlot.Rocket: inSlot = IsRocketPack(kv.Key); break;
                case RecommendSlot.Clock: inSlot = IsClockPack(kv.Key); break;
                case RecommendSlot.DynamicStore: inSlot = IsDynamicStorePack(kv.Key); break;
                case RecommendSlot.FixedStore: inSlot = IsDynamicStorePack(kv.Key); break;
                default: inSlot = false; break;
            }
            if (!inSlot) continue;

            ShopDataItem item = allShopItems.FirstOrDefault(x => x.produceNameId == kv.Key);
            if (item != null && !IsRemoveAdsProduct(item.produceNameId))
            {
                if (!IsLimitedOut(item)) // 排除限购已满
                    list.Add(item);
            }
        }
        return list;
    }

    private bool IsClockPack(string nameId)
    {
        return nameId.StartsWith("ClockBox");
    }

    private bool IsLimitedOut(ShopDataItem item)
    {
        if (item == null || item.limited <= 0) return false;
        ShopLimitData data = GetLimitData(item);
        int buyCount = data?.buyCount ?? 0;
        return buyCount >= item.limited;
    }

    public bool IsSlotLocked(RecommendSlot slot)
    {
        bool locked;
        return _slotLockedThisLevel.TryGetValue(slot.ToString(), out locked) && locked;
    }

    public void LockSlot(RecommendSlot slot)
    {
        _slotLockedThisLevel[slot.ToString()] = true;
        LogShop($"LockSlot: {slot}");
    }

    public void UnlockAllSlots()
    {
        _slotLockedThisLevel.Clear();
        _bulbDiscountConsumed = false;
        _rocketDiscountConsumed = false;
        _levelVideoUsed.Clear();
        LogShop("UnlockAllSlots: 全部推荐位解锁, 折扣消费标记复位");
    }

    public float CalculateDecisionWeight(ShopDataItem item, ShopRecommendConfig cfg, RecommendSlot slot)
    {
        if (item == null || cfg == null)
        {
            LogShop($"CalculateDecisionWeight 跳过: item={(item == null ? "null" : item.produceNameId)}, cfg={(cfg == null ? "null" : cfg.nameID)}, slot={slot}");
            return 0;
        }

        // 限购已满：不参与决策（决策权重 = 0）
        if (IsLimitedOut(item))
        {
            ShopLimitData ld = GetLimitData(item);
            LogShop($"CalculateDecisionWeight 跳过: {item.produceNameId} 限购已满(limited={item.limited}, buyCount={ld?.buyCount ?? 0})");
            return 0;
        }

        ShopLimitData limit = GetLimitData(item);
        float payTendency = limit?.payTendencyScore ?? 0f;
        float explore = limit?.exploreScore ?? 0f;
        float fail = limit?.conversionFailScore ?? 0f;
        float pushIndex = limit?.pushStateIndex ?? 1f;

        float packWeight = cfg.baseWeight + payTendency;
        if (slot == RecommendSlot.Bulb || slot == RecommendSlot.DynamicStore)
        {
            packWeight += explore - fail;
        }
        packWeight = Mathf.Max(0, packWeight); // 礼包权重最低=0

        float cap = cfg.maxWeight > 0 ? cfg.maxWeight : 100f;
        packWeight = Mathf.Min(packWeight, cap); // 礼包权重上限（配置表）

        float special = IsHolidayOrWeekend() ? cfg.specialWeight : 0;
        float peak = IsPeakHour() ? cfg.peakWeight : 0;
        float ret = IsChurnReturn() ? cfg.returnWeight : 0;

        float cooldown = GetCooldownCoefficient(item);
        float decision = (packWeight + special + peak + ret) * cooldown * pushIndex;

        LogShop($"决策权重 {item.produceNameId} [slot={slot}]: " +
                $"基础权重={cfg.baseWeight}, 付费倾向分={payTendency:F1}, 探价加分={explore:F1}, 失败分={fail:F1} => 礼包权重={packWeight:F2}(cap={cap}), " +
                $"节日/周末权重={special:F2}, 高峰权重={peak:F2}, 流失回归={ret:F2}, 冷却系数={cooldown:F2}, 推送状态={pushIndex:F2} => 最终决策权重={decision:F2}");

        return decision;
    }

    private bool IsPeakHour()
    {
        int hour = DateTime.Now.Hour;
        return hour >= PeakHourStart && hour < PeakHourEnd;
    }

    private bool IsChurnReturn()
    {
        // TODO：接入登录时间记录。GameDataManager 无该字段时恒 false
        DateTime lastLogin = DateTime.Parse(GameDataManager.Instance.UserData.logoutTime);
        return lastLogin != default && (DateTime.Now - lastLogin).TotalDays >= ChurnReturnDays;
    }

    private float GetCooldownCoefficient(ShopDataItem item)
    {
        ShopLimitData limit = GetLimitData(item);
        DateTime limitEndTime = limit?.GetLimitEndTime() ?? DateTime.MinValue;
        DateTime lastBuyTime = limit?.GetLastBuyTime() ?? DateTime.MinValue;

        // 限时结束后冷却优先（权重为0，直接不展示）
        if (limitEndTime != DateTime.MinValue && limitEndTime <= DateTime.Now)
        {
            double hours = DateTime.Now.Subtract(limitEndTime).TotalHours;
            if (hours <= 48) return 0f;
            if (hours <= 120) return 0.5f;
            return 1f;
        }

        if (lastBuyTime != DateTime.MinValue)
        {
            double hours = DateTime.Now.Subtract(lastBuyTime).TotalHours;
            if (hours <= 12) return 0.2f;
            if (hours <= 72) return 0.5f;
        }
        return 1f;
    }

    public List<ShopDataItem> GetRecommendPacks(RecommendSlot slot)
    {
        LogShop($"========== GetRecommendPacks 开始: slot={slot}, shopMode={shopMode}, 时间={DateTime.Now:HH:mm:ss} ==========");

        int maxFeatured = 1;

        switch (slot)
        {
            case RecommendSlot.Bulb:
                maxFeatured = 3;
                break;
            case RecommendSlot.DynamicStore:
                maxFeatured = 2;
                break;
            case RecommendSlot.Rocket:
            case RecommendSlot.Clock:
            case RecommendSlot.FixedStore:
                maxFeatured = 1;
                break;
        }

        List<ShopDataItem> result = new List<ShopDataItem>();
        bool slot1Empty = false;

        for (int featured = 1; featured <= maxFeatured; featured++)
        {
            List<ShopDataItem> candidates = GetSlotCandidates(slot, featured);
            LogShop($"  featured={featured}, 候选数={candidates.Count}, 候选=[{string.Join(",", candidates.Select(x => x.produceNameId))}]");

            if (candidates.Count == 0) continue;

            // 动态商店/固定商店一号位特殊规则
            if ((slot == RecommendSlot.DynamicStore || slot == RecommendSlot.FixedStore) && featured == 1)
            {
                var winners = DecideWinner(candidates, slot);
                if (winners == null || winners.Count == 0)
                {
                    slot1Empty = true;
                    LogShop($"  {slot} 一号位无胜出礼包, 判定去广告补位, IsRemoveAdsOwned={IsRemoveAdsOwned()}");
                    // 一号位不推荐任何礼包：去广告未购买则展示去广告（已购买则二号位放开，见末尾）
                    if (!IsRemoveAdsOwned())
                    {
                        ShopDataItem ad = allShopItems.FirstOrDefault(x => x.produceNameId == ProductRemoveAds);
                        if (ad != null)
                        {
                            result.Add(ad);
                            LogShop($"  {slot} 一号位补位去广告: {ad.produceNameId}");
                        }
                    }
                    continue;
                }
                result.AddRange(winners);
            }
            else
            {
                var winner = DecideWinner(candidates, slot);
                if (winner != null && winner.Count > 0)
                    result.AddRange(winner);
            }
        }

        // 动态商店一号位无礼包可出且"去广告"已购买 → 二号位第1、2权重的礼包都展示
        if (slot == RecommendSlot.DynamicStore && slot1Empty && IsRemoveAdsOwned())
        {
            List<ShopDataItem> second = GetSlotCandidates(slot, 2);
            var secondSorted = second
                .OrderByDescending(x => CalculateDecisionWeight(x, GetCfg(x.produceNameId), slot))
                .ThenBy(x => x.price)
                .ToList();
            LogShop($"  动态商店一号位空且去广告已拥有, 二号位放开: 候选数={secondSorted.Count}, 排序=[{string.Join(",", secondSorted.Select(x => x.produceNameId))}]");
            for (int i = 0; i < Math.Min(2, secondSorted.Count); i++)
            {
                result.Add(secondSorted[i]);
                LogShop($"    二号位追加: {secondSorted[i].produceNameId}");
            }
        }

        var distinctResult = result.Distinct().ToList();
        LogShop($"========== GetRecommendPacks 结果: slot={slot}, 数量={distinctResult.Count}, 商品=[{string.Join(",", distinctResult.Select(x => $"{x.produceNameId}(price={x.price},giftType={x.giftType})"))}] ==========");
        return distinctResult;
    }

    private List<ShopDataItem> DecideWinner(List<ShopDataItem> candidates, RecommendSlot slot)
    {
        LogShop($"    --- DecideWinner 开始: slot={slot}, 初始候选数={candidates.Count} ---");

        // 先过滤限购/限时结束冷却为0的
        List<ShopDataItem> valid = candidates
            .Where(c => !IsLimitedOut(c) && GetCooldownCoefficient(c) > 0f)
            .ToList();

        LogShop($"    过滤后有效候选数={valid.Count}, 有效=[{string.Join(",", valid.Select(x => x.produceNameId))}]");
        if (valid.Count == 0)
        {
            LogShop($"    DecideWinner 结果: 无有效候选");
            return null;
        }

        // 新人礼互斥：若该推荐位要出"新人礼"，检查其他弹窗类型是否已出新人礼
        List<ShopDataItem> newcomers = valid.Where(c => IsNewcomerLabel(c.labelZh)).ToList();
        bool newcomerAllowed = true;
        if (newcomers.Count > 0)
        {
            newcomerAllowed = CanShowNewcomerGift(slot);
            LogShop($"    新人礼候选=[{string.Join(",", newcomers.Select(x => x.produceNameId))}], newcomerAllowed={newcomerAllowed}");
        }

        List<ShopDataItem> pool = newcomerAllowed ? valid : valid.Where(c => !IsNewcomerLabel(c.labelZh)).ToList();
        if (pool.Count == 0)
        {
            LogShop($"    DecideWinner 结果: 新人礼互斥后无候选");
            return null;
        }

        // 计算权重
        List<KeyValuePair<ShopDataItem, float>> scored = pool
            .Select(c => new KeyValuePair<ShopDataItem, float>(c, CalculateDecisionWeight(c, GetCfg(c.produceNameId), slot)))
            .ToList();

        foreach (var kv in scored)
        {
            var item = kv.Key;
            ShopLimitData limit = GetLimitData(item);
            LogShop($"      候选明细 {item.produceNameId}: 权重={kv.Value:F2}, " +
                    $"giftType={item.giftType}, price={item.price}, " +
                    $"payTendency={limit?.payTendencyScore ?? 0f:F1}, explore={limit?.exploreScore ?? 0f:F1}, fail={limit?.conversionFailScore ?? 0f:F1}, " +
                    $"pushIndex={limit?.pushStateIndex ?? 1f:F2}, cooldown={GetCooldownCoefficient(item):F2}, label={item.labelZh}");
        }

        float max = scored.Max(x => x.Value);
        var top = scored.Where(x => Mathf.Approximately(x.Value, max)).ToList();

        // 决策权重相同 → 随机展示
        ShopDataItem winner = top[UnityEngine.Random.Range(0, top.Count)].Key;

        // 活动礼包判定：节日/周末权重≠0 且决策胜出 → 显示限时与活动标签
        ShopRecommendConfig cfg = GetCfg(winner.produceNameId);
        bool isActivity = cfg != null && cfg.specialWeight != 0 && IsHolidayOrWeekend();

        ShopLimitData winnerLimit = GetOrCreateLimitData(winner);
        winnerLimit.isActivityPack = isActivity;
        if (isActivity)
        {
            // 限时时间 = 本次节日/周末的截至时间 - 当前时间（基于预设表连续日期计算）
            winnerLimit.SetLimitEndTime(GetCurrentHolidayEndTime());
        }
        else
        {
            winnerLimit.SetLimitEndTime(DateTime.MinValue);
        }

        LogShop($"    --- DecideWinner 结果: 胜出={winner.produceNameId}, 权重={max:F2}, isActivityPack={isActivity}, isHolidayOrWeekend={IsHolidayOrWeekend()} ---");

        return new List<ShopDataItem> { winner };
    }

    private ShopRecommendConfig GetCfg(string nameId)
    {
        ShopRecommendConfig cfg;
        return _recommendConfigs.TryGetValue(nameId, out cfg) ? cfg : null;
    }

    private bool CanShowNewcomerGift(RecommendSlot slot)
    {
        // 简化实现：按优先级依次检查其他弹窗是否已展示新人礼。
        // 灯泡优先级最高，其次复活，最后火箭。
        // TODO：接入各弹窗当前展示的礼包状态（由各弹窗打开时调用 MarkNewcomerShown 记录）
        return true;
    }

    private static bool IsNewcomerLabel(string label)
    {
        return label == "新人礼" || label == "新手礼";
    }

    public void MarkNewcomerShown(RecommendSlot slot, string nameId)
    {
        if (string.IsNullOrEmpty(nameId)) return;
        _shownNewcomer[slot] = nameId;
        LogShop($"MarkNewcomerShown: slot={slot}, nameId={nameId}");
    }

    public void ClearNewcomerShown()
    {
        _shownNewcomer.Clear();
        LogShop("ClearNewcomerShown");
    }

    public bool IsLastPurchasedPack(ShopDataItem item)
    {
        return item != null && _lastPurchasedPack != null
            && item.produceNameId == _lastPurchasedPack.produceNameId
            && item.type == 2;
    }

    public void RecordPayInvoke(string produceNameId)
    {
        _payInvokeCount[produceNameId] = GetPayInvokeCount(produceNameId) + 1;
        LogShop($"RecordPayInvoke: {produceNameId}, 次数={_payInvokeCount[produceNameId]}");
    }

    public int GetPayInvokeCount(string produceNameId)
    {
        int c;
        return _payInvokeCount.TryGetValue(produceNameId, out c) ? c : 0;
    }

    public void AddConversionFail(ShopDataItem item)
    {
        if (item == null) return;
        if (item.giftType <= 1)
        {
            LogShop($"AddConversionFail 跳过: {item.produceNameId} giftType={item.giftType} <= 1");
            return;
        }

        ShopLimitData limit = GetOrCreateLimitData(item);
        DateTime lastFail = limit.GetLastFailScoreTime();

        if (lastFail != DateTime.MinValue && (DateTime.Now - lastFail).TotalSeconds < FailScoreChangeInterval)
        {
            LogShop($"AddConversionFail 跳过: {item.produceNameId} 距上次不足{FailScoreChangeInterval}s");
            return;
        }

        limit.conversionFailScore += 5;
        limit.SetLastFailScoreTime(DateTime.Now);
        LogShop($"AddConversionFail: {item.produceNameId} 转化失败分={limit.conversionFailScore:F1}");
    }

    public void ResetConversionFail(ShopDataItem item)
    {
        if (item == null) return;
        ShopLimitData limit = GetOrCreateLimitData(item);
        limit.conversionFailScore = 0;
    }

    // ======================= 购买流程 =======================

    /// <summary>
    /// 统一处理礼包购买后的逻辑（GiftTable 和 ShopItem 共用）：
    ///   1. 发放 productContent 里的资源（金币 / 道具 / 去广告 / 无限体力）；
    ///   2. 去广告：永久 RemoveAds / 限时 RemoveDayAds，按已有剩余时间续期；
    ///   3. 推荐位跟踪：付费倾向积分 / 探价加分 / 转化失败分归零 / buyCount++ / 冷却时间 / 上次购买；
    ///   4. 全程 [Shop] 日志。
    /// </summary>
    public void HandleGiftTablePurchaseRewards(ShopDataItem shopDataItem, ProductItem productItem)
    {
        if (shopDataItem == null)
        {
            LogShop("HandleGiftTablePurchaseRewards 跳过: shopDataItem 为空");
            return;
        }

        LogShop($"HandleGiftTablePurchaseRewards 开始: {shopDataItem.produceNameId}, " +
                $"type={shopDataItem.type}, giftType={shopDataItem.giftType}, price={shopDataItem.price}");

        // ---------- 1. 发放奖励 ----------
        if (shopDataItem.productContent != null)
        {
            foreach (var dataitem in shopDataItem.productContent)
            {
                if (dataitem == null || dataitem.Count < 2)
                {
                    LogShop($"  跳过无效 productContent 项 (Count<2)");
                    continue;
                }

                int count = GetInt(dataitem[1]);
                int type = GetInt(dataitem[0]);
                LogShop($"  发放奖励: type={type}, count={count}");

                switch (type)
                {
                    case (int)LimitRewordType.Coins:
                        GameDataManager.Instance.UserData.UpdateGold(count, false, false, shopDataItem.name + "购买金币");
                        break;

                    case (int)LimitRewordType.Butterfly:
                        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Butterfly, count, shopDataItem.name + "商店购买蝴蝶");
                        break;

                    case (int)LimitRewordType.Tipstool:
                        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, count, shopDataItem.name + "商店购买放大镜");
                        break;

                    case (int)LimitRewordType.AutoComplete:
                        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.AutoComplete, count, shopDataItem.name + "商店购买提示灯");
                        break;

                    case (int)LimitRewordType.RemoveAds:
                    case (int)LimitRewordType.RemoveDayAds:
                        // 去广告走下面统一的 HandleRemoveAdsPurchase
                        LogShop($"  去广告商品，交由 HandleRemoveAdsPurchase 处理 (adstype={type})");
                        break;

                    default:
                        LogShop($"  未处理的商品类型: type={type}, count={count}");
                        break;
                }
            }
        }

        // ---------- 2. 去广告（永久 / 限时），走统一续期逻辑 ----------
        if (IsRemoveAdsProduct(shopDataItem.produceNameId))
        {
            LogShop($"  非消耗型: 去广告 {shopDataItem.produceNameId}");
            HandleRemoveAdsPurchase(shopDataItem);
        }

        // ---------- 3. 无限体力 ----------
        if (IsUnlimitedEnergyProduct(shopDataItem.produceNameId))
        {
            LogShop($"  非消耗型: 无限体力 {shopDataItem.produceNameId}");
            HandleUnlimitedEnergyPurchase(shopDataItem);
        }

        // ---------- 4. 推荐位跟踪（付费倾向 / 探价 / 转化失败 / 冷却 / 上次购买） ----------
        OnPackPurchased(shopDataItem);

        LogShop($"HandleGiftTablePurchaseRewards 结束: {shopDataItem.produceNameId}");
    }

    public void OnPurchaseSuccess(ProductItem item)
    {
        //todo 关闭loading界面
        LogShop($"========== OnPurchaseSuccess 开始: ProductId={item?.ProductId}, ItemName={item?.ItemName}, LocalizedPrice={item?.LocalizedPrice} ==========");

        ShopDataItem shopDataItem = GetProduct(item.ProductId);
        if (shopDataItem == null)
        {
            // 修复：原代码在空判断前访问 produceNameId 会崩溃
            LogShop($"购买成功但商店未找到该商品: {item.ProductId}");
            return;
        }

        LogShop($"匹配到商店商品: id={shopDataItem.id}, produceNameId={shopDataItem.produceNameId}, type={shopDataItem.type}, giftType={shopDataItem.giftType}, price={shopDataItem.price}, label={shopDataItem.labelZh}");

        // ---------- 非消耗型商品：去广告 / 无限体力，购买后立即生效 ----------
        if (IsRemoveAdsProduct(shopDataItem.produceNameId))
        {
            LogShop($"非消耗型: 去广告 {shopDataItem.produceNameId}");
            HandleRemoveAdsPurchase(shopDataItem);
        }
        if (IsUnlimitedEnergyProduct(shopDataItem.produceNameId))
        {
            LogShop($"非消耗型: 无限体力 {shopDataItem.produceNameId}");
            HandleUnlimitedEnergyPurchase(shopDataItem);
        }

        // ---------- 消耗型商品：发放奖励 ----------
        Game.self.Shop.CurrentShopDataItem = shopDataItem.DeepCopy();
        SystemManager.Instance.ShowPanel(PanelType.AwardScreen);

        foreach (var dataitem in shopDataItem.productContent)
        {
            if (dataitem == null || dataitem.Count < 2) continue; // 防御：纯描述型商品内容（如去广告）跳过
            int count = GetInt(dataitem[1]);
            int type = GetInt(dataitem[0]);
            LogShop($"发放奖励: type={type}, count={count}");
            switch (type)
            {
                case (int)LimitRewordType.Coins:
                    GameDataManager.Instance.UserData.UpdateGold(count, false, false, "商店购买" + item.ItemName);
                    break;
                case (int)LimitRewordType.Butterfly:
                    GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Butterfly, count, "商店购买" + item.ItemName);
                    break;
                case (int)LimitRewordType.Tipstool: // 放大镜道具，整个词语提示
                    GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, count, "商店购买" + item.ItemName);
                    break;
                case (int)LimitRewordType.AutoComplete: // 提示灯道具，单个字符提示
                    GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.AutoComplete, count, "商店购买" + item.ItemName);
                    break;
                case (int)LimitRewordType.RemoveAds:
                case (int)LimitRewordType.RemoveDayAds:
                    BuyRemoveAdsEvent(type);
                    break;
                case ReviveToolType: // 复活道具（⏰），TODO 与 LimitRewordType 枚举对齐
                    GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, count, "商店购买" + item.ItemName);
                    Debug.LogWarning("[ShopManager] 复活道具类型 ReviveToolType=" + type + " 发放逻辑请与道具系统确认");
                    break;
                default:
                    Debug.LogWarning("购买成功：未处理的商品类型 type=" + type + " count=" + count);
                    break;
            }
        }

        shopManager.paysuccess = true;

        bool firstPay = GameDataManager.Instance.UserData.TotalPayTimes == 0;
        if (firstPay)
            GameDataManager.Instance.UserData.firstPayTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        GameDataManager.Instance.UserData.lastPayTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        GameDataManager.Instance.UserData.LastPayTimeTicks = DateTime.Now.Ticks;
        GameDataManager.Instance.UserData.TotalPayTimes++;
        GameDataManager.Instance.UserData.TotalRevenue += item.LocalizedPrice;
        DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedShopBuy, 1);

        LogShop($"支付统计: firstPay={firstPay}, TotalPayTimes={GameDataManager.Instance.UserData.TotalPayTimes}, TotalRevenue={GameDataManager.Instance.UserData.TotalRevenue}");

        if (!UIUtilities.isEditMode)
        {
            AnalyticMgr.PurchaseFinished(item, firstPay);
#if UNITY_HUAWEI
            // 处理购买成功后的逻辑，例如增加游戏内货
            item?.OnShipmentCompleted(true);
#endif
        }

        // ---------- 推荐位决策状态更新 ----------
        OnPackPurchased(shopDataItem);

        if (shopDataItem.produceNameId == ProductSingleGoods)
        {
            if (!GameDataManager.Instance.UserData.isDayMoneyBuy)
            {
                GameDataManager.Instance.UserData.isDayMoneyBuy = true;
                ///shopPriceText.text = "已购买";
            }
        }

        MessageSystem.Instance.ShowTip($"{shopDataItem.name} 购买成功！");
        MessageSystem.Instance.HideLoadingAnimation();

        LogShop($"========== OnPurchaseSuccess 结束: {shopDataItem.produceNameId} ==========");
    }

    /// <summary>复活道具类型值：TODO 与 GameDataManager.LimitRewordType 枚举对齐后删除该常量</summary>
    public const int ReviveToolType = 1002;

    /// <summary>是否无限体力商品</summary>
    private bool IsUnlimitedEnergyProduct(string produceNameId)
    {
        // 配置表右侧"无限体力（小时）"列：TODO 配置迁移后从 ShopDataItem 字段读取
        return produceNameId != null && produceNameId.Contains("Energy");
    }

    /// <summary>
    /// 去广告购买处理：
    ///  - 终身去广告：写入 limitShopItems（isget=true, adstype=RemoveAds）；
    ///  - 限时去广告（7天/1天）：在之前剩余时间基础上额外增加限时时间（需求：刷新限时）。
    /// </summary>
    private void HandleRemoveAdsPurchase(ShopDataItem shopDataItem)
    {
        int adstype = (int)LimitRewordType.RemoveDayAds;
        bool permanent = shopDataItem.produceNameId == ProductRemoveAds;
        int hours = 0;
        if (permanent)
        {
            adstype = (int)LimitRewordType.RemoveAds;
            hours = 0;
        }
        else if (shopDataItem.produceNameId == ProductRemoveAdsDaily)
        {
            adstype = (int)LimitRewordType.RemoveDayAds;
            hours = 1 * 24;
        }

        LogShop($"HandleRemoveAdsPurchase: {shopDataItem.produceNameId}, permanent={permanent}, hours={hours}");

        ShopLimitData exist = GameDataManager.Instance.UserData.limitShopItems.Find(x => x.adstype == adstype);
        if (exist != null && !exist.isoverdate)
        {
            // 限时去广告：在剩余时间基础上额外增加（永久去广告重复购买直接续期，逻辑一致）
            DateTime baseEnd = DateTime.Now;
            DateTime oldEnd = exist.GetEndTime();
            if (oldEnd != DateTime.MinValue && oldEnd > DateTime.Now)
                baseEnd = oldEnd;

            DateTime newEnd = baseEnd.AddHours(hours);
            exist.SetEndTime(newEnd);
            exist.isget = true;
            exist.isopen = false;
            exist.isoverdate = false;
            LogShop($"  续期: 原结束={oldEnd}, 新结束={newEnd}");
        }
        else
        {
            DateTime newEnd = hours > 0 ? DateTime.Now.AddHours(hours) : DateTime.MinValue;
            GameDataManager.Instance.UserData.limitShopItems.Add(new ShopLimitData()
            {
                id = shopDataItem.id,
                nameid = shopDataItem.produceNameId,
                endtime = hours > 0 ? newEnd.ToString("yyyy-MM-dd HH:mm:ss") : "",
                isopen = false,
                gettime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                adstype = adstype,
                isget = true,
                isoverdate = false,
            });
            LogShop($"  新增记录: adstype={adstype}, endtime={(hours > 0 ? newEnd.ToString("yyyy-MM-dd HH:mm:ss") : "永久")}");
        }

        // 生效广告表现
        OnRemoveAdsApplied(adstype);
        UpdateAdsBtnUI?.Invoke(permanent ? null : DateTime.Now.ToString(), true);

        RefreshLimitData();
    }

    /// <summary>
    /// 无限体力购买处理：
    ///  - 从商品 ID / 配置里解析小时数；
    ///  - 通过 UserData.AddUnlimitedEnergy(hours) 累加时长（多次购买叠加）；
    ///  - 买后刷新 HeaderSection 的体力 UI（若当前显示）。
    /// </summary>
    private void HandleUnlimitedEnergyPurchase(ShopDataItem shopDataItem)
    {
        int hours = ParseUnlimitedEnergyHours(shopDataItem);
        if (hours <= 0)
        {
            LogShop($"HandleUnlimitedEnergyPurchase 跳过: {shopDataItem.produceNameId} 未解析到有效小时数");
            return;
        }

        var userData = GameDataManager.Instance.UserData;

        double beforeRemain = userData.GetUnlimitedEnergyRemainSeconds();
        DateTime newEnd = userData.AddUnlimitedEnergy(hours);
        double afterRemain = userData.GetUnlimitedEnergyRemainSeconds();

        LogShop($"HandleUnlimitedEnergyPurchase: {shopDataItem.produceNameId}, +{hours}h, " +
                $"原剩余={TimeSpan.FromSeconds(beforeRemain):hh\\:mm\\:ss}, " +
                $"新剩余={TimeSpan.FromSeconds(afterRemain):hh\\:mm\\:ss}, " +
                $"新结束={newEnd:yyyy-MM-dd HH:mm:ss}");

        // 通知 HeaderSection 刷新体力 UI（无限体力图标/倒计时）
        // 走 EventDispatcher 或直接让协程下一帧自动刷新均可
        EventDispatcher.instance.TriggerUpdateLayerCoin(false, true);
    }

    /// <summary>
    /// 从商品解析"无限体力（小时）"：
    ///  优先读配置字段（推荐）；无字段时按 produceNameId 命名兜底解析。
    /// </summary>
    private int ParseUnlimitedEnergyHours(ShopDataItem item)
    {
        if (item == null) return 0;

        // ★ 推荐：配置表迁移后从 ShopDataItem 直接读字段
        // return item.unlimitedEnergyHours;

        // 兜底：按 ID 约定命名解析，例如 Energy1 / Energy3 / Energy24
        if (string.IsNullOrEmpty(item.produceNameId)) return 0;
        string s = item.produceNameId;

        int idx = s.Length - 1;
        while (idx >= 0 && char.IsDigit(s[idx])) idx--;
        if (idx == s.Length - 1) return 0; // 末尾没有数字

        int hours;
        return int.TryParse(s.Substring(idx + 1), out hours) ? hours : 0;
    }

    /// <summary>
    /// 礼包购买后的推荐位状态更新（付费倾向、探价加分、冷却、上次购买）。
    /// 所有跟踪数据写入 ShopLimitData（挂在 UserData.limitShopItems 下持久化）。
    /// </summary>
    private void OnPackPurchased(ShopDataItem shopDataItem)
    {
        LogShop($"OnPackPurchased 开始: {shopDataItem.produceNameId}, type={shopDataItem.type}, giftType={shopDataItem.giftType}, price={shopDataItem.price}");

        // 上次购买记录（仅礼包）
        if (shopDataItem.type == 2)
        {
            _lastPurchasedPack = shopDataItem;
            LogShop($"  记录上次购买礼包: {_lastPurchasedPack.produceNameId}");
        }

        // 当前购买商品对应的跟踪数据
        ShopLimitData limit = GetOrCreateLimitData(shopDataItem);

        // 冷却系数：记录购买时间
        limit.SetLastBuyTime(DateTime.Now);

        // 付费倾向积分：基于 GiftType 档位 a，a档+2、a+1档+1、a+2档+0.5（所有该档位礼包）
        int a = shopDataItem.giftType;
        if (a > 0)
        {
            foreach (var cfg in _recommendConfigs)
            {
                ShopDataItem it = allShopItems.FirstOrDefault(x => x.produceNameId == cfg.Key);
                if (it == null) continue;
                ShopLimitData ld = GetOrCreateLimitData(it);
                if (cfg.Value.giftType == a)
                {
                    float before = ld.payTendencyScore;
                    ld.payTendencyScore += 2f;
                    LogShop($"    [付费倾向] {it.produceNameId} (档={cfg.Value.giftType}) {before:F2} → {ld.payTendencyScore:F2} (+2)");
                }
                else if (cfg.Value.giftType == a + 1)
                {
                    float before = ld.payTendencyScore;
                    ld.payTendencyScore += 1f;
                    LogShop($"    [付费倾向] {it.produceNameId} (档={cfg.Value.giftType}) {before:F2} → {ld.payTendencyScore:F2} (+1)");
                }
                else if (cfg.Value.giftType == a + 2)
                {
                    float before = ld.payTendencyScore;
                    ld.payTendencyScore += 0.5f;
                    LogShop($"    [付费倾向] {it.produceNameId} (档={cfg.Value.giftType}) {before:F2} → {ld.payTendencyScore:F2} (+0.5)");
                }
            }
            LogShop($"  付费倾向积分更新: 基准档 a={a}");
        }

        // 探价加分：该礼包 +3；"第X档"每发生3次，下一档 +6（一次性）
        float exploreBefore = limit.exploreScore;
        limit.exploreScore += 3f;
        LogShop($"  [探价加分] {shopDataItem.produceNameId} {exploreBefore:F2} → {limit.exploreScore:F2} (+3)");

        if (a > 0)
        {
            // 记录档位购买次数
            int tierCount = GetTierBuyCount(a) + 1;
            SetTierBuyCount(a, tierCount);
            LogShop($"  档位 a={a} 累计购买次数={tierCount}");
            if (tierCount % 3 == 0)
            {
                foreach (var cfg in _recommendConfigs)
                {
                    if (cfg.Value.giftType == a + 1)
                    {
                        ShopDataItem it = allShopItems.FirstOrDefault(x => x.produceNameId == cfg.Key);
                        if (it != null)
                        {
                            ShopLimitData ld = GetOrCreateLimitData(it);
                            float before = ld.exploreScore;
                            ld.exploreScore += 6f; // 一次性加6分
                            LogShop($"    [探价加分-下一档] {it.produceNameId} {before:F2} → {ld.exploreScore:F2} (+6)");
                        }
                    }
                }
            }
        }

        // 转化失败分归零（购买成功）
        float failBefore = limit.conversionFailScore;
        limit.conversionFailScore = 0;
        LogShop($"  [转化失败分] {shopDataItem.produceNameId} {failBefore:F2} → 0");

        // 购买次数（限购）
        limit.buyCount++;
        LogShop($"  [限购] {shopDataItem.produceNameId} buyCount={limit.buyCount}, limited={shopDataItem.limited}");

        // 限时活动礼包：购买后清除活动状态（12小时内冷却 0.2）
        limit.isActivityPack = false;
        limit.SetLimitEndTime(DateTime.MinValue);

        // 当关推荐位锁定
        LockSlot(RecommendSlot.Bulb);
        LockSlot(RecommendSlot.Rocket);
        LockSlot(RecommendSlot.Clock);
        LockSlot(RecommendSlot.DynamicStore);

        LogShop($"OnPackPurchased 结束: {shopDataItem.produceNameId}, payTendency={limit.payTendencyScore:F1}, explore={limit.exploreScore:F1}, fail={limit.conversionFailScore:F1}, buyCount={limit.buyCount}");
    }

    private int GetTierBuyCount(int tier)
    {
        int c;
        return _tierBuyCount.TryGetValue(tier, out c) ? c : 0;
    }

    private void SetTierBuyCount(int tier, int count)
    {
        _tierBuyCount[tier] = count;
    }

    /// <summary>
    /// 购买失败/取消支付：
    ///  - 调起系统支付弹窗但未成功购买返回游戏 → 弹"取消购买"弹窗（原 toast 不再出现）；
    ///  - 其他失败情况同样弹"取消购买"弹窗；
    ///  - 记录转化失败分 F 与支付调起次数。
    /// </summary>
    public void OnPurchaseCanceled(string productId, bool invokedSystemPay = true)
    {
        LogShop($"OnPurchaseCanceled: productId={productId}, invokedSystemPay={invokedSystemPay}");
        ShopDataItem shopDataItem = GetProduct(productId);
        if (shopDataItem != null)
        {
            RecordPayInvoke(productId);
            if (invokedSystemPay)
                AddConversionFail(shopDataItem);
        }
        else
        {
            LogShop($"  未找到商品: {productId}");
        }

        // 弹"取消购买"弹窗（需求：文案 CancelPurchase/PurchaseCancelled/Continue）
        // TODO：确认 PanelType.CancelPurchase 已注册
        SystemManager.Instance.ShowPanel(PanelType.CancelPurchase);
    }

    public void OnPurchaseFailed(string productId, string error)
    {
        LogShop($"OnPurchaseFailed: productId={productId}, error={error}");
        // 需求：其它失败的情况同样弹"取消购买"弹窗，不再用 toast
        OnPurchaseCanceled(productId, false);
    }

    /// <summary>
    /// 恢复购买（动态商店底部"恢复购买"按钮）。
    /// 成功提示"恢复成功"；失败"恢复失败"；无记录"没有可恢复的内购"。
    /// </summary>
    public void RestorePurchases()
    {
        LogShop("RestorePurchases 调用");
        // TODO：调用 IAP 恢复接口，非消耗型商品（去广告等）通过 OnPurchaseSuccess 重新发放
        // #if UNITY_IOS
        //     IAPManager.Instance.RestorePurchases(OnRestoreSuccess, OnRestoreFailed);
        // #endif
        MessageSystem.Instance.ShowTip("没有可恢复的内购");
    }

    /// <summary>购买记录（动态商店：显示礼包名称、价格、礼包内容、购买时间）</summary>
    public List<string> GetPurchaseRecords()
    {
        // TODO：接入 UserData 购买记录存储（当前记录在 AnalyticMgr/后台，本地需新增字段）
        LogShop("GetPurchaseRecords (TODO 本地记录未实现)");
        return new List<string>();
    }

    // ======================= 道具折扣机制 =======================

    private void ParseDiscountConfig(string param)
    {
        string[] p = param.Split('_');
        if (p.Length < 5) return;
        _discountConfig.enable = p[0] == "1";
        _discountConfig.bulbDailyLimit = GetInt(p[2]);
        _discountConfig.rocketDailyLimit = GetInt(p[3]);
        _discountConfig.triggerInterval = GetInt(p[4]);

        LogShop($"ParseDiscountConfig: enable={_discountConfig.enable}, bulbDaily={_discountConfig.bulbDailyLimit}, rocketDaily={_discountConfig.rocketDailyLimit}, interval={_discountConfig.triggerInterval}");
    }

    private void LoadDiscountConfig()
    {
        // TODO：与拼字玩法配置表统一读取（PropVideoTimes 同理）
        string discountParam = "1_5_3_2_300";
        // TextAsset dc = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "shop_discount");
        // if (dc != null)
        //     discountParam = dc.text.Trim();
        ParseDiscountConfig(discountParam);
    }

    public bool TryTriggerBulbDiscount(int levelId, int wrongAnswerSeconds, int remainTargetWords, int bulbCount)
    {
        if (!_discountConfig.enable) return false;
        RefreshDailyCounters();                              // 跨天重置每日次数
        if (levelId < _discountConfig.unlockLevel) return false;
        if (_bulbDiscountConsumed) return false;                 // 折扣使用一次后消失
        if (wrongAnswerSeconds < 6 || remainTargetWords < 1) return false;
        if (bulbCount > 0) return false;
        if (_bulbDiscountToday >= _discountConfig.bulbDailyLimit) return false;
        if ((DateTime.Now - _lastBulbDiscountTime).TotalSeconds < _discountConfig.triggerInterval) return false;
        if (IsRocketDiscountShowing()) return false;             // 灯泡火箭不同时出现

        _bulbDiscountToday++;
        _lastBulbDiscountTime = DateTime.Now;
        LogShop($"TryTriggerBulbDiscount: 触发成功, 今日次数={_bulbDiscountToday}");
        return true;
    }

    public bool TryTriggerRocketDiscount(int levelId, int cursorSeconds, int blankCount, int rocketCount)
    {
        if (!_discountConfig.enable) return false;
        RefreshDailyCounters();                              // 跨天重置每日次数
        if (levelId < _discountConfig.unlockLevel) return false;
        if (_rocketDiscountConsumed) return false;
        if (cursorSeconds < 3 || blankCount < 3) return false;
        if (rocketCount > 0) return false;
        if (_rocketDiscountToday >= _discountConfig.rocketDailyLimit) return false;
        if ((DateTime.Now - _lastRocketDiscountTime).TotalSeconds < _discountConfig.triggerInterval) return false;

        _rocketDiscountToday++;
        _lastRocketDiscountTime = DateTime.Now;
        LogShop($"TryTriggerRocketDiscount: 触发成功, 今日次数={_rocketDiscountToday}");
        return true;
    }

    private bool IsRocketDiscountShowing()
    {
        // TODO：由 UI 设置当前是否正展示火箭折扣角标
        return false;
    }

    public void OnDiscountUsed(bool isBulb)
    {
        if (isBulb) _bulbDiscountConsumed = true;
        else _rocketDiscountConsumed = true;
        LogShop($"OnDiscountUsed: isBulb={isBulb}");
    }

    private void RefreshDailyCounters()
    {
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        if (_discountDate != today)
        {
            _discountDate = today;
            _bulbDiscountToday = 0;
            _rocketDiscountToday = 0;
            LogShop($"RefreshDailyCounters: 跨天重置, 日期={today}");
        }
    }

    // ======================= 看激励视频赠礼 / 插屏赠礼 =======================

    public void OnBulbRewardVideoFinished()
    {
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        if (_bulbVideoGiftDate != today)
        {
            _bulbVideoGiftDate = today;
            _bulbVideoGiftCount = 0;
        }

        if (!_firstBulbVideoGifted)
        {
            // 首个灯泡道具激励视频/插屏后：弹窗感谢，赠送50金币
            _firstBulbVideoGifted = true;
            GameDataManager.Instance.UserData.UpdateGold(50, false, false, "激励视频赠礼");
            SystemManager.Instance.ShowPanel(PanelType.AwardScreen);
            LogShop($"OnBulbRewardVideoFinished: 首个激励视频, 赠 50 金币");
            return;
        }

        if (_bulbVideoGiftCount >= 3) return;
        _bulbVideoGiftCount++;
        int[] rewards = { 50, 30, 20 };
        int gold = rewards[Mathf.Min(_bulbVideoGiftCount - 1, 2)];
        GameDataManager.Instance.UserData.UpdateGold(gold, false, false, "激励视频赠礼");
        // 复用已有弹窗获取金币的交互（小窗）
        SystemManager.Instance.ShowPanel(PanelType.AwardScreen);
        LogShop($"OnBulbRewardVideoFinished: 第{_bulbVideoGiftCount}个视频, 赠 {gold} 金币");
    }

    public void OnInterstitialClosed(int currentLevel)
    {
        if (currentLevel < 30)
        {
            LogShop($"OnInterstitialClosed: 关卡={currentLevel} < 30, 不赠送");
            return;
        }

        string today = DateTime.Now.ToString("yyyy-MM-dd");
        if (_interstitialGiftDate != today)
        {
            _interstitialGiftDate = today;
            _interstitialGiftCount = 0;
        }
        if (_interstitialGiftCount >= 5)
        {
            LogShop($"OnInterstitialClosed: 今日已达上限(5), 不赠送");
            return;
        }

        _interstitialGiftCount++;
        GameDataManager.Instance.UserData.UpdateGold(10, false, false, "插屏赠礼");
        // 复用激励视频赠礼的小弹窗
        SystemManager.Instance.ShowPanel(PanelType.AwardScreen);
        LogShop($"OnInterstitialClosed: 第{_interstitialGiftCount}个插屏, 赠 10 金币");
    }

    // ======================= 礼包主动弹出策略（替换原 ShowLimitAdsPanel） =======================

    public async void ShowLimitAdsPanel()
    {
        LogShop("[ShopManager] ShowLimitAdsPanel 原主动弹出逻辑已删除（需求 C 章节：删除原逻辑），请接入破冰/复购策略");
        await Task.Delay(1);
    }

    // ======================= 工具方法 =======================

    private static string Localize(string key)
    {
        // TODO：接入本地化文本表（本地化文本表 - 成语消：禅意之境）
        return key;
    }

    public void UpdateAdsBtnUIEvent(string gettime, bool updateui)
    {
        UpdateAdsBtnUI?.Invoke(gettime, updateui);
    }

    private List<ShopDataItem> GetLimitAdsGifts()
    {
        return shopItems.FindAll(item => !string.IsNullOrEmpty(item.limitedTime));
    }

    private void BuyRemoveAdsEvent(int type)
    {
        // 原实现全部被注释且 transform.SetActive(false) 行为异常，已重构：
        // 去广告的持久化/限时刷新统一走 HandleRemoveAdsPurchase；
        // 这里仅保留广告表现与UI回调（隐藏横幅由子类覆写 OnRemoveAdsApplied 接入广告系统）。
        LogShop($"BuyRemoveAdsEvent: type={type}");
        OnRemoveAdsApplied(type);

        if (type == (int)LimitRewordType.RemoveAds)
        {
            ShopManager.shopManager.UpdateAdsBtnUIEvent(null, true);
        }
        else if (type == (int)LimitRewordType.RemoveDayAds)
        {
            ShopManager.shopManager.UpdateAdsBtnUIEvent(DateTime.Now.ToString(), true);
        }
    }

    protected virtual void OnRemoveAdsApplied(int adstype)
    {
        // TODO：接入广告系统
        // AdsManager.Instance.HideBannerAd();
        // AdsManager.Instance.RemoveInterstitialAndSplash();
        LogShop($"OnRemoveAdsApplied: adstype={adstype} (TODO 广告系统)");
    }

    // ======================= 静态打开入口 =======================

    public void OpenFixedStore()
    {
        shopMode = ShopMode.FixedStore;
        LogShop($"OpenFixedStore: 打开固定商店");
        SystemManager.Instance.ShowPanel(PanelType.ShopScreen);
    }

    public void OpenDynamicStore()
    {
        shopMode = ShopMode.DynamicStore;
        LogShop($"OpenDynamicStore: 打开动态商店");
        SystemManager.Instance.ShowPanel(PanelType.ShopScreen);
    }
}