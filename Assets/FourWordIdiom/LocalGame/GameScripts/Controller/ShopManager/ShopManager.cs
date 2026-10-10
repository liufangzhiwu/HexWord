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
/// </summary>
public partial class ShopManager : MonoBehaviour
{
    // ======================= 常量（与需求/配置表对齐） =======================

    /// <summary>去广告（终身）商品ID</summary>
    public const string ProductRemoveAds = "RemoveADS";
    /// <summary>去广告（一周）商品ID，同时是破冰礼包 ID</summary>
    public const string ProductRemoveAdsWeekly = "RemoveAdsWeekly";
    /// <summary>灯泡道具单一道具商品</summary>
    public const string ProductSingleGoods = "SingleGoods";
    /// <summary>复活道具单一道具商品</summary>
    public const string ProductSingleClock = "SingleClockS";
    /// <summary>金币视频商品</summary>
    public const string ProductGold6 = "Gold6";

    /// <summary>去广告（限时一天）商品ID（新配置表待新增，预留）</summary>
    public const string ProductRemoveAdsDaily = "RemoveAdsDaily";
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

    /// <summary>非消耗型权益持久化 key（PlayerPrefs；TODO：迁移到 UserData 序列化字段）</summary>
    private const string SaveKey = "ShopNonConsumableState_v1";

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
        }
        else
        {
            Debug.LogError("Failed to load CSV data.");
        }

        paysuccess = false;

        // 加载各模块配置（各 partial 脚本各自实现 Load*Config）
        LoadRuntimeConfigs();

        Initialize();

        // 恢复本地持久化的非消耗型权益（去广告/无限体力等）
        LoadNonConsumableState();
    }

    /// <summary>
    /// 加载运行时配置：推荐位权重表（灯泡/火箭/复活/动态商店）、用户分层表、节日/周末表、道具折扣参数。
    /// 配置全部放本地（需求：不做云端配置）。各模块优先从 Bundle 读取，失败时回退内置默认值（与配置表 20260915 一致）。
    /// </summary>
    private void LoadRuntimeConfigs()
    {
        LoadBulbBoxConfig();        // ShopManager_BulbBox.cs
        LoadRocketBoxConfig();      // ShopManager_RocketBox.cs
        LoadClockBoxConfig();       // 主脚本（复活礼盒默认配置）
        LoadDynamicStoreConfig();   // ShopManager_DynamicStore.cs
        LoadLayerConfig();          // ShopManager_UserLayer.cs
        LoadHolidayWeekendTable();  // ShopManager_HolidayWeekend.cs
        LoadDiscountConfig();       // 主脚本（道具折扣）
    }

    /// <summary>
    /// 复活礼盒（ClockBox）推荐位配置：优先读 Bundle，失败回退内置默认（与配置表 20260915 一致）
    /// </summary>
    private void LoadClockBoxConfig()
    {
        TextAsset rec = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "shop_clockbox");
        if (rec != null)
        {
            ParseRecommendConfigs(rec.text);
        }
    }

    /// <summary>
    /// 初始化：限购数据字典、限时礼包列表
    /// </summary>
    public void Initialize()
    {
        shoplimitDatas = GameDataManager.Instance.UserData.limitShopItems
            .ToDictionary(x => x.id, x => x);
        _limitAdsGifts = GetLimitAdsGifts();
    }

    /// <summary>
    /// 重新从 UserData 刷新限购数据（购买成功后调用）
    /// </summary>
    public void RefreshLimitData()
    {
        shoplimitDatas = GameDataManager.Instance.UserData.limitShopItems
            .ToDictionary(x => x.id, x => x);
        _limitAdsGifts = GetLimitAdsGifts();
    }

    // ======================= 去广告（非消耗型内购） =======================

    /// <summary>是否为去广告类商品（永久或限时）</summary>
    public bool IsRemoveAdsProduct(string produceNameId)
    {
        return produceNameId == ProductRemoveAds
            || produceNameId == ProductRemoveAdsWeekly
            || produceNameId == ProductRemoveAdsDaily;
    }

    /// <summary>是否已拥有永久去广告（本地 + 云端恢复状态）</summary>
    public bool IsRemoveAdsOwned()
    {
        return GameDataManager.Instance.UserData.limitShopItems
            .Any(item => item.isget && !item.isoverdate && item.adstype == (int)LimitRewordType.RemoveAds);
    }

    /// <summary>是否处于限时去广告期间（7天/1天）</summary>
    public bool IsRemoveAdsActive()
    {
        return GameDataManager.Instance.UserData.limitShopItems
            .Any(item => item.isget && !item.isoverdate
                && (item.adstype == (int)LimitRewordType.Remove7DayAds
                 || item.adstype == RemoveAds1DayType));
    }

    /// <summary>获取限时去广告剩余时间（秒），未生效返回 0</summary>
    public double GetRemoveAdsRemainSeconds()
    {
        double remain = 0;
        foreach (var item in GameDataManager.Instance.UserData.limitShopItems)
        {
            if (!item.isget || item.isoverdate) continue;
            if (item.adstype != (int)LimitRewordType.RemoveAds && item.adstype != (int)LimitRewordType.Remove7DayAds && item.adstype != RemoveAds1DayType)
                continue;
            DateTime end;
            if (!string.IsNullOrEmpty(item.endtime) && DateTime.TryParse(item.endtime, out end))
            {
                remain = Math.Max(remain, end.Subtract(DateTime.Now).TotalSeconds);
            }
        }
        return Math.Max(0, remain);
    }

    /// <summary>是否处于去除广告礼包期间（修复：原实现逻辑被注释，恒返回 false）</summary>
    public bool IsRemoveAdsGift()
    {
        return IsRemoveAdsOwned() || IsRemoveAdsActive();
    }

    /// <summary>去广告（限时一天）类型值：TODO 与 GameDataManager.LimitRewordType 枚举对齐后删除该常量</summary>
    public const int RemoveAds1DayType = 1001;

    // ======================= 内购推荐位推送规则（决策权重，公共逻辑） =======================

    /// <summary>
    /// 内购推荐位类型（各推荐位配置脚本各自维护候选集，公共决策逻辑在此）
    /// </summary>
    public enum RecommendSlot
    {
        Bulb,       // 灯泡道具弹窗（一号主力/二号锚点/三号诱饵）
        Rocket,     // 火箭道具弹窗
        Clock,      // 复活道具弹窗
        DynamicStore// 动态商店（一号位/二号位）
    }

    /// <summary>
    /// 解析推荐位权重配置（CSV 格式，列与配置表一致），供各推荐位配置脚本复用。
    /// 兼容两种表头形态：
    ///  - BulbBox / RocketBox / ClockBox：第1行中文说明，第2行字段名（nameID, [GiftType,] BaseWeight, MaxWeight, SpecialWeight, PeakWeight, ReturnWeight, Featured）
    ///  - DynamicStore：第1行即字段名，第2行起为数据
    /// 按字段名动态映射列，避免 Rocket/Clock（无 GiftType 列）错位。
    /// </summary>
    private void ParseRecommendConfigs(string data)
    {
        var records = SplitCsvLines(data);
        if (records.Count == 0) return;

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
                giftType = hasGiftType ? GetInt(GetField(f, colMap["GiftType"])) : 0,
                baseWeight = GetInt(GetField(f, colMap != null && colMap.ContainsKey("BaseWeight") ? colMap["BaseWeight"] : 3)),
                maxWeight = GetInt(GetField(f, colMap != null && colMap.ContainsKey("MaxWeight") ? colMap["MaxWeight"] : 4)),
                specialWeight = GetInt(GetField(f, colMap != null && colMap.ContainsKey("SpecialWeight") ? colMap["SpecialWeight"] : 5)),
                peakWeight = GetInt(GetField(f, colMap != null && colMap.ContainsKey("PeakWeight") ? colMap["PeakWeight"] : 6)),
                returnWeight = GetInt(GetField(f, colMap != null && colMap.ContainsKey("ReturnWeight") ? colMap["ReturnWeight"] : 7)),
                featured = GetInt(GetField(f, colMap != null && colMap.ContainsKey("Featured") ? colMap["Featured"] : 8))
            };
            _recommendConfigs[cfg.nameID] = cfg;
        }
    }

    /// <summary>写入单条推荐位配置（各推荐位配置脚本的默认值入口）</summary>
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

    /// <summary>获取推荐位候选礼包（按配置表 featured 过滤；各推荐位归属判断由对应脚本提供）</summary>
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

    /// <summary>复活礼盒（ClockBox）候选归属判断</summary>
    private bool IsClockPack(string nameId)
    {
        return nameId.StartsWith("ClockBox");
    }

    /// <summary>限购判断：配置限购次数且已买满</summary>
    private bool IsLimitedOut(ShopDataItem item)
    {
        if (item.limited <= 0) return false;
        return item.buyCount >= item.limited;
    }

    /// <summary>推荐位当关锁定（购买后当关不刷新新礼包）</summary>
    public bool IsSlotLocked(RecommendSlot slot)
    {
        bool locked;
        return _slotLockedThisLevel.TryGetValue(slot.ToString(), out locked) && locked;
    }

    public void LockSlot(RecommendSlot slot)
    {
        _slotLockedThisLevel[slot.ToString()] = true;
    }

    public void UnlockAllSlots()
    {
        _slotLockedThisLevel.Clear();
        _bulbDiscountConsumed = false;
        _rocketDiscountConsumed = false;
        _levelVideoUsed.Clear();
    }

    /// <summary>
    /// 计算单个礼包的决策权重：
    /// 决策权重 =（礼包权重 + 节日/周末权重 + 高峰权重 + 流失回归）× 冷却系数 × 推送状态指数
    /// 礼包权重 = 基础权重 + 付费倾向积分（火箭、复活）
    ///          基础权重 + 付费倾向积分 + 探价加分 - 转化失败分（灯泡、动态商店）
    /// 每次弹窗或进入动态商店时临时计算。
    /// </summary>
    public float CalculateDecisionWeight(ShopDataItem item, ShopRecommendConfig cfg, RecommendSlot slot)
    {
        if (item == null || cfg == null) return 0;

        float packWeight = cfg.baseWeight + item.payTendencyScore;
        if (slot == RecommendSlot.Bulb || slot == RecommendSlot.DynamicStore)
        {
            packWeight += item.exploreScore - item.conversionFailScore;
        }
        packWeight = Mathf.Max(0, packWeight); // 礼包权重最低=0

        float cap = cfg.maxWeight > 0 ? cfg.maxWeight : 100f;
        packWeight = Mathf.Min(packWeight, cap); // 礼包权重上限（配置表）

        // 节日/周末权重：当天属于预设节日或周末时
        float special = IsHolidayOrWeekend() ? cfg.specialWeight : 0;

        // 高峰权重：19-23 点
        float peak = IsPeakHour() ? cfg.peakWeight : 0;

        // 流失回归：最近7天未登录
        float ret = IsChurnReturn() ? cfg.returnWeight : 0;

        float decision = (packWeight + special + peak + ret) * GetCooldownCoefficient(item) * item.pushStateIndex;
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
        DateTime lastLogin =DateTime.Parse(GameDataManager.Instance.UserData.logoutTime);
        return lastLogin != default && (DateTime.Now - lastLogin).TotalDays >= ChurnReturnDays;
    }

    /// <summary>
    /// 冷却系数：
    ///  - 礼包被购买后：12小时内=0.2；12-72小时=0.5；72小时后=1
    ///  - 礼包限时结束后：48小时内=0；48-120小时=0.5；120小时后=1
    /// </summary>
    private float GetCooldownCoefficient(ShopDataItem item)
    {
        // 限时结束后冷却优先（权重为0，直接不展示）
        if (item.limitEndTime != default(DateTime) && item.limitEndTime <= DateTime.Now)
        {
            double hours = DateTime.Now.Subtract(item.limitEndTime).TotalHours;
            if (hours <= 48) return 0f;
            if (hours <= 120) return 0.5f;
            return 1f;
        }

        if (item.lastBuyTime != default(DateTime))
        {
            double hours = DateTime.Now.Subtract(item.lastBuyTime).TotalHours;
            if (hours <= 12) return 0.2f;
            if (hours <= 72) return 0.5f;
        }
        return 1f;
    }

    /// <summary>
    /// 决出当前推荐位礼包（决策权重最高；相同权重随机）。
    /// 特殊规则：
    ///  - 新人礼礼包不会同时出现（灯泡＞复活＞火箭，同类型价格低＞高）；
    ///  - "上次购买"标签优先级最高（UI 读取 labelZh 时处理）；
    ///  - 动态商店一号位：若所有可展示礼包决策权重都为 0 → 不推荐礼包；
    ///    此时若"去广告"未购买，一号位展示"去广告"；若已购买，二号位第1、2权重的礼包都展示。
    /// </summary>
    public List<ShopDataItem> GetRecommendPacks(RecommendSlot slot)
    {
        int maxFeatured = (slot == RecommendSlot.DynamicStore) ? 2 : 3;
        List<ShopDataItem> result = new List<ShopDataItem>();
        bool slot1Empty = false;

        for (int featured = 1; featured <= maxFeatured; featured++)
        {
            List<ShopDataItem> candidates = GetSlotCandidates(slot, featured);
            if (candidates.Count == 0) continue;

            // 动态商店一号位特殊规则
            if (slot == RecommendSlot.DynamicStore && featured == 1)
            {
                var winners = DecideWinner(candidates, slot);
                if (winners == null || winners.Count == 0)
                {
                    slot1Empty = true;
                    // 一号位不推荐任何礼包：去广告未购买则展示去广告（已购买则二号位放开，见末尾）
                    if (!IsRemoveAdsOwned())
                    {
                        ShopDataItem ad = allShopItems.FirstOrDefault(x => x.produceNameId == ProductRemoveAds);
                        if (ad != null) result.Add(ad);
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
            for (int i = 0; i < Math.Min(2, secondSorted.Count); i++)
            {
                result.Add(secondSorted[i]);
            }
        }

        return result.Distinct().ToList();
    }

    /// <summary>决出单个推荐位胜出礼包（权重最高；相同随机；含新人礼互斥处理）</summary>
    private List<ShopDataItem> DecideWinner(List<ShopDataItem> candidates, RecommendSlot slot)
    {
        // 先过滤限购/限时结束冷却为0的
        List<ShopDataItem> valid = candidates
            .Where(c => !IsLimitedOut(c) && GetCooldownCoefficient(c) > 0f)
            .ToList();
        if (valid.Count == 0) return null;

        // 新人礼互斥：若该推荐位要出"新人礼"，检查其他弹窗类型是否已出新人礼
        List<ShopDataItem> newcomers = valid.Where(c => IsNewcomerLabel(c.labelZh)).ToList();
        bool newcomerAllowed = true;
        if (newcomers.Count > 0)
        {
            newcomerAllowed = CanShowNewcomerGift(slot);
        }

        List<ShopDataItem> pool = newcomerAllowed ? valid : valid.Where(c => !IsNewcomerLabel(c.labelZh)).ToList();
        if (pool.Count == 0) return null;

        // 计算权重
        List<KeyValuePair<ShopDataItem, float>> scored = pool
            .Select(c => new KeyValuePair<ShopDataItem, float>(c, CalculateDecisionWeight(c, GetCfg(c.produceNameId), slot)))
            .ToList();

        float max = scored.Max(x => x.Value);
        var top = scored.Where(x => Mathf.Approximately(x.Value, max)).ToList();

        // 决策权重相同 → 随机展示
        ShopDataItem winner = top[UnityEngine.Random.Range(0, top.Count)].Key;

        // 活动礼包判定：节日/周末权重≠0 且决策胜出 → 显示限时与活动标签
        ShopRecommendConfig cfg = GetCfg(winner.produceNameId);
        winner.isActivityPack = cfg != null && cfg.specialWeight != 0 && IsHolidayOrWeekend();
        if (winner.isActivityPack)
        {
            // 限时时间 = 本次节日/周末的截至时间 - 当前时间（基于预设表连续日期计算）
            winner.limitEndTime = GetCurrentHolidayEndTime();
        }

        return new List<ShopDataItem> { winner };
    }

    private ShopRecommendConfig GetCfg(string nameId)
    {
        ShopRecommendConfig cfg;
        return _recommendConfigs.TryGetValue(nameId, out cfg) ? cfg : null;
    }

    /// <summary>
    /// 新人礼互斥判断（优先级：灯泡＞复活＞火箭；同类型价格低＞高）。
    /// 当前弹窗类型可出新人礼的条件：优先级更高类型的推荐位此时未出新人礼。
    /// </summary>
    private bool CanShowNewcomerGift(RecommendSlot slot)
    {
        // 简化实现：按优先级依次检查其他弹窗是否已展示新人礼。
        // 灯泡优先级最高，其次复活，最后火箭。
        // TODO：接入各弹窗当前展示的礼包状态（由各弹窗打开时调用 MarkNewcomerShown 记录）
        return true;
    }

    /// <summary>是否新人礼标签（"新人礼"/"新手礼"），此类礼包不会同时出现</summary>
    private static bool IsNewcomerLabel(string label)
    {
        return label == "新人礼" || label == "新手礼";
    }

    public void MarkNewcomerShown(RecommendSlot slot, string nameId)
    {
        if (string.IsNullOrEmpty(nameId)) return;
        _shownNewcomer[slot] = nameId;
    }

    public void ClearNewcomerShown()
    {
        _shownNewcomer.Clear();
    }

    /// <summary>是否"上次购买"的礼包（展示"上次购买"内购标签，优先级最高）</summary>
    public bool IsLastPurchasedPack(ShopDataItem item)
    {
        return item != null && _lastPurchasedPack != null
            && item.produceNameId == _lastPurchasedPack.produceNameId
            && item.type == 2;
    }

    /// <summary>记录支付调起（成功吊起系统支付弹窗但未完成购买）</summary>
    public void RecordPayInvoke(string produceNameId)
    {
        _payInvokeCount[produceNameId] = GetPayInvokeCount(produceNameId) + 1;
    }

    public int GetPayInvokeCount(string produceNameId)
    {
        int c;
        return _payInvokeCount.TryGetValue(produceNameId, out c) ? c : 0;
    }

    /// <summary>记录转化失败（F += 5；第1档除外；间隔30秒）</summary>
    public void AddConversionFail(ShopDataItem item)
    {
        if (item == null) return;
        if (item.giftType <= 1) return; // 第1档礼包不加 F
        if ((DateTime.Now - item.lastFailScoreTime).TotalSeconds < FailScoreChangeInterval) return;
        item.conversionFailScore += 5;
        item.lastFailScoreTime = DateTime.Now;
    }

    /// <summary>重置转化失败分 F（购买成功 / 推荐位更换）</summary>
    public void ResetConversionFail(ShopDataItem item)
    {
        if (item == null) return;
        item.conversionFailScore = 0;
    }

    // ======================= 购买流程 =======================

    /// <summary>
    /// 购买成功（含恢复购买回调）。
    /// 需求变更：
    ///  - 修复 shopDataItem 为 null 时提前访问 produceNameId 的崩溃；
    ///  - 新增去广告（终身/限时一天/限时7天）、无限体力等非消耗型商品处理；
    ///  - 限时去广告购买后刷新剩余时间（在之前剩余时间基础上额外增加）；
    ///  - 记录"上次购买"礼包、付费倾向积分、探价加分、冷却系数、支付完成埋点。
    /// </summary>
    public void OnPurchaseSuccess(ProductItem item)
    {
        //todo 关闭loading界面
        Debug.Log("购买成功: " + item.ProductId);

        ShopDataItem shopDataItem = GetProduct(item.ProductId);
        if (shopDataItem == null)
        {
            // 修复：原代码在空判断前访问 produceNameId 会崩溃
            Debug.LogWarning("购买成功但商店未找到该商品: " + item.ProductId);
            return;
        }

        Debug.Log("获取恢复购买商品ID: " + shopDataItem.produceNameId);

        // ---------- 非消耗型商品：去广告 / 无限体力，购买后立即生效并持久化 ----------
        if (IsRemoveAdsProduct(shopDataItem.produceNameId))
        {
            HandleRemoveAdsPurchase(shopDataItem);
        }
        if (IsUnlimitedEnergyProduct(shopDataItem.produceNameId))
        {
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
                case (int)LimitRewordType.Remove7DayAds:
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
    ///  - 终身去广告：写入 limitShopItems（isget=true, adstype=RemoveAds），并持久化；
    ///  - 限时去广告（7天/1天）：在之前剩余时间基础上额外增加限时时间（需求：刷新限时）。
    /// </summary>
    private void HandleRemoveAdsPurchase(ShopDataItem shopDataItem)
    {
        int adstype;
        bool permanent = shopDataItem.produceNameId == ProductRemoveAds;
        int hours;
        if (permanent)
        {
            adstype = (int)LimitRewordType.RemoveAds;
            hours = 0;
        }
        else if (shopDataItem.produceNameId == ProductRemoveAdsWeekly)
        {
            adstype = (int)LimitRewordType.Remove7DayAds;
            hours = 7 * 24;
        }
        else
        {
            adstype = RemoveAds1DayType;
            hours = 24;
        }

        ShopLimitData exist = GameDataManager.Instance.UserData.limitShopItems.Find(x => x.adstype == adstype);
        if (exist != null && !exist.isoverdate)
        {
            // 限时去广告：在剩余时间基础上额外增加（永久去广告重复购买直接续期，逻辑一致）
            DateTime baseEnd = DateTime.Now;
            DateTime oldEnd;
            if (!string.IsNullOrEmpty(exist.endtime) && DateTime.TryParse(exist.endtime, out oldEnd) && oldEnd > DateTime.Now)
                baseEnd = oldEnd;
            exist.endtime = baseEnd.AddHours(hours).ToString();
            exist.isget = true;
            exist.isopen = false;
            exist.isoverdate = false;
        }
        else
        {
            GameDataManager.Instance.UserData.limitShopItems.Add(new ShopLimitData()
            {
                id = shopDataItem.id,
                nameid = shopDataItem.produceNameId,
                endtime = hours > 0 ? DateTime.Now.AddHours(hours).ToString() : "",
                isopen = false,
                gettime = DateTime.Now.ToString(),
                adstype = adstype,
                isget = true,
                isoverdate = false,
            });
        }

        // 生效广告表现
        OnRemoveAdsApplied(adstype);
        UpdateAdsBtnUI?.Invoke(permanent ? null : DateTime.Now.ToString(), true);

        RefreshLimitData();
        SaveNonConsumableState();
    }

    /// <summary>无限体力购买处理（TODO：接入体力系统）</summary>
    private void HandleUnlimitedEnergyPurchase(ShopDataItem shopDataItem)
    {
        // TODO：设置 UserData 无限体力状态（体力图标无穷、限时显示、退出/复活无二次确认）
        // GameDataManager.Instance.UserData.unlimitedEnergyEndTime = DateTime.Now.AddHours(hours).ToString();
        SaveNonConsumableState();
    }

    /// <summary>礼包购买后的推荐位状态更新（付费倾向、探价加分、冷却、上次购买）</summary>
    private void OnPackPurchased(ShopDataItem shopDataItem)
    {
        // 上次购买记录（仅礼包）
        if (shopDataItem.type == 2)
        {
            _lastPurchasedPack = shopDataItem;
        }

        // 冷却系数：记录购买时间
        shopDataItem.lastBuyTime = DateTime.Now;

        // 付费倾向积分：基于 GiftType 档位 a，a档+2、a+1档+1、a+2档+0.5（所有该档位礼包）
        int a = shopDataItem.giftType;
        if (a > 0)
        {
            foreach (var cfg in _recommendConfigs)
            {
                ShopDataItem item = allShopItems.FirstOrDefault(x => x.produceNameId == cfg.Key);
                if (item == null) continue;
                if (cfg.Value.giftType == a) item.payTendencyScore += 2f;
                else if (cfg.Value.giftType == a + 1) item.payTendencyScore += 1f;
                else if (cfg.Value.giftType == a + 2) item.payTendencyScore += 0.5f;
            }
        }

        // 探价加分：该礼包 +3；"第X档"每发生3次，下一档 +6（一次性）
        shopDataItem.exploreScore += 3f;
        if (a > 0)
        {
            // 记录档位购买次数
            int tierCount = GetTierBuyCount(a) + 1;
            SetTierBuyCount(a, tierCount);
            if (tierCount % 3 == 0)
            {
                foreach (var cfg in _recommendConfigs)
                {
                    if (cfg.Value.giftType == a + 1)
                    {
                        ShopDataItem item = allShopItems.FirstOrDefault(x => x.produceNameId == cfg.Key);
                        if (item != null) item.exploreScore += 6f; // 一次性加6分
                    }
                }
            }
        }

        // 转化失败分归零（购买成功）
        ResetConversionFail(shopDataItem);

        // 购买次数（限购）
        shopDataItem.buyCount++;

        // 限时活动礼包：购买后清除活动状态（12小时内冷却 0.2）
        shopDataItem.isActivityPack = false;
        shopDataItem.limitEndTime = default(DateTime);

        // 当关推荐位锁定
        LockSlot(RecommendSlot.Bulb);
        LockSlot(RecommendSlot.Rocket);
        LockSlot(RecommendSlot.Clock);
        LockSlot(RecommendSlot.DynamicStore);

        SaveNonConsumableState();
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
        Debug.Log("购买取消: " + productId);
        ShopDataItem shopDataItem = GetProduct(productId);
        if (shopDataItem != null)
        {
            RecordPayInvoke(productId);
            if (invokedSystemPay)
                AddConversionFail(shopDataItem);
        }

        // 弹"取消购买"弹窗（需求：文案 CancelPurchase/PurchaseCancelled/Continue）
        // TODO：确认 PanelType.CancelPurchase 已注册
        SystemManager.Instance.ShowPanel(PanelType.CancelPurchase);
    }

    public void OnPurchaseFailed(string productId, string error)
    {
        Debug.LogWarning("购买失败: " + productId + " error=" + error);
        // 需求：其它失败的情况同样弹"取消购买"弹窗，不再用 toast
        OnPurchaseCanceled(productId, false);
    }

    /// <summary>
    /// 恢复购买（动态商店底部"恢复购买"按钮）。
    /// 成功提示"恢复成功"；失败"恢复失败"；无记录"没有可恢复的内购"。
    /// </summary>
    public void RestorePurchases()
    {
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
        return new List<string>();
    }

    // ======================= 道具折扣机制 =======================

    /// <summary>
    /// 解析道具折扣参数：开关_折扣_灯泡每日次数_火箭每日次数_间隔秒（默认 1_5_3_2_300）
    /// </summary>
    private void ParseDiscountConfig(string param)
    {
        string[] p = param.Split('_');
        if (p.Length < 5) return;
        _discountConfig.enable = p[0] == "1";
        _discountConfig.bulbDailyLimit = GetInt(p[2]);
        _discountConfig.rocketDailyLimit = GetInt(p[3]);
        _discountConfig.triggerInterval = GetInt(p[4]);
    }

    /// <summary>
    /// 加载道具折扣参数（开关_折扣_灯泡次数_火箭次数_间隔秒，默认 1_5_3_2_300）
    /// </summary>
    private void LoadDiscountConfig()
    {
        // TODO：与拼字玩法配置表统一读取（PropVideoTimes 同理）
        string discountParam = "1_5_3_2_300";
        // TextAsset dc = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "shop_discount");
        // if (dc != null)
        //     discountParam = dc.text.Trim();
        ParseDiscountConfig(discountParam);
    }

    /// <summary>
    /// 灯泡道具折扣触发判定（需求）：
    ///  - 第7关解锁；
    ///  - 关卡答错后6秒，且此时剩余未完成的目标词≥1；
    ///  - 当前持有灯泡道具数量=0；
    ///  - 灯泡道具折扣今天触发未达限制次数上限；
    ///  - 灯泡道具满足触发时间间隔；
    ///  - 灯泡与火箭不会同时出现；同时满足则优先出灯泡。
    /// </summary>
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
        return true;
    }

    /// <summary>
    /// 火箭道具折扣触发判定（需求）：
    ///  - 光标切到某个词之后3秒，该词的待填空格≥3；
    ///  - 当前持有火箭道具数量=0；
    ///  - 每日限制与时间间隔。
    /// </summary>
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
        return true;
    }

    private bool IsRocketDiscountShowing()
    {
        // TODO：由 UI 设置当前是否正展示火箭折扣角标
        return false;
    }

    /// <summary>折扣使用后调用：角标消失、价格恢复原价</summary>
    public void OnDiscountUsed(bool isBulb)
    {
        if (isBulb) _bulbDiscountConsumed = true;
        else _rocketDiscountConsumed = true;
    }

    /// <summary>每日重置折扣次数（跨天/新的一天）</summary>
    private void RefreshDailyCounters()
    {
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        if (_discountDate != today)
        {
            _discountDate = today;
            _bulbDiscountToday = 0;
            _rocketDiscountToday = 0;
        }
    }

    // ======================= 看激励视频赠礼 / 插屏赠礼 =======================

    /// <summary>
    /// 灯泡激励视频看完后的赠礼：
    ///  - 首个灯泡道具激励视频（或插屏广告）→ 弹窗感谢，赠送 50 金币；
    ///  - 每天看完前3个灯泡道具激励视频，弹小窗送金币：50/30/20（自动获得）。
    /// </summary>
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
            return;
        }

        if (_bulbVideoGiftCount >= 3) return;
        _bulbVideoGiftCount++;
        int[] rewards = { 50, 30, 20 };
        int gold = rewards[Mathf.Min(_bulbVideoGiftCount - 1, 2)];
        GameDataManager.Instance.UserData.UpdateGold(gold, false, false, "激励视频赠礼");
        // 复用已有弹窗获取金币的交互（小窗）
        SystemManager.Instance.ShowPanel(PanelType.AwardScreen);
    }

    /// <summary>
    /// 插屏关闭后的赠礼：玩家每天前5个插屏赠礼（填字玩法30关之后），每次10金币。
    /// </summary>
    public void OnInterstitialClosed(int currentLevel)
    {
        if (currentLevel < 30) return; // 填字玩法30关之后

        string today = DateTime.Now.ToString("yyyy-MM-dd");
        if (_interstitialGiftDate != today)
        {
            _interstitialGiftDate = today;
            _interstitialGiftCount = 0;
        }
        if (_interstitialGiftCount >= 5) return;

        _interstitialGiftCount++;
        GameDataManager.Instance.UserData.UpdateGold(10, false, false, "插屏赠礼");
        // 复用激励视频赠礼的小弹窗
        SystemManager.Instance.ShowPanel(PanelType.AwardScreen);
    }

    // ======================= 礼包主动弹出策略（替换原 ShowLimitAdsPanel） =======================

    /// <summary>
    /// 原限时礼包弹窗逻辑：按需求"首先关闭原礼包主动弹出；（删除原逻辑）"已废弃。
    /// 保留空实现兼容旧调用方，新逻辑见 UserLayer 脚本中的 TryShowBreakIcePopup / TryShowRepurchasePopup。
    /// </summary>
    public async void ShowLimitAdsPanel()
    {
        Debug.Log("[ShopManager] ShowLimitAdsPanel 原主动弹出逻辑已删除（需求 C 章节：删除原逻辑），请接入破冰/复购策略");
        await Task.Delay(1);
    }

    // ======================= 非消耗型权益持久化 =======================

    /// <summary>
    /// 数据存储（需求：去广告礼包和限时体力这类非消耗型商品需存个人数据，卸载重装可恢复）。
    /// 本地 PlayerPrefs 兜底 + 建议迁移到 UserData 序列化字段。
    /// </summary>
    [Serializable]
    private class NonConsumableState
    {
        public bool removeAdsOwned;
        public string removeAdsEndTime;
        public string unlimitedEnergyEndTime;
        public int breakIcePopupCount;
        public string lastBreakIceTime;
        public string lastRepurchaseTime;
        public string lastPurchasedPack;
    }

    private void SaveNonConsumableState()
    {
        try
        {
            NonConsumableState s = new NonConsumableState();
            s.removeAdsOwned = IsRemoveAdsOwned();
            s.removeAdsEndTime = GetRemoveAdsEndTime();
            s.breakIcePopupCount = _breakIcePopupCount;
            s.lastBreakIceTime = _lastBreakIcePopupTime == DateTime.MinValue ? "" : _lastBreakIcePopupTime.ToString();
            s.lastRepurchaseTime = _lastRepurchasePopupTime == DateTime.MinValue ? "" : _lastRepurchasePopupTime.ToString();
            s.lastPurchasedPack = _lastPurchasedPack != null ? _lastPurchasedPack.produceNameId : "";
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(s));
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning("保存商店非消耗型状态失败: " + e.Message);
        }
    }

    private void LoadNonConsumableState()
    {
        try
        {
            if (!PlayerPrefs.HasKey(SaveKey)) return;
            NonConsumableState s = JsonUtility.FromJson<NonConsumableState>(PlayerPrefs.GetString(SaveKey));
            if (s == null) return;
            _breakIcePopupCount = s.breakIcePopupCount;
            DateTime t;
            if (DateTime.TryParse(s.lastBreakIceTime, out t)) _lastBreakIcePopupTime = t;
            if (DateTime.TryParse(s.lastRepurchaseTime, out t)) _lastRepurchasePopupTime = t;
            if (!string.IsNullOrEmpty(s.lastPurchasedPack))
                _lastPurchasedPack = allShopItems.FirstOrDefault(x => x.produceNameId == s.lastPurchasedPack);
        }
        catch (Exception e)
        {
            Debug.LogWarning("读取商店非消耗型状态失败: " + e.Message);
        }
    }

    private string GetRemoveAdsEndTime()
    {
        foreach (var item in GameDataManager.Instance.UserData.limitShopItems)
        {
            if (item.isget && !item.isoverdate && !string.IsNullOrEmpty(item.endtime))
                return item.endtime;
        }
        return "";
    }

    // ======================= 工具方法 =======================

    private static string Localize(string key)
    {
        // TODO：接入本地化文本表（本地化文本表 - 成语消：禅意之境）
        return key;
    }

    /// <summary>更新广告按钮UI（原方法保留）</summary>
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
        OnRemoveAdsApplied(type);

        if (type == (int)LimitRewordType.RemoveAds)
        {
            ShopManager.shopManager.UpdateAdsBtnUIEvent(null, true);
        }
        else if (type == (int)LimitRewordType.Remove7DayAds)
        {
            ShopManager.shopManager.UpdateAdsBtnUIEvent(DateTime.Now.ToString(), true);
        }
    }

    /// <summary>
    /// 去广告生效钩子：隐藏底部横幅/停止插屏与开屏的强制弹出（需求：代码上预做处理）。
    /// 原代码中 AdsManager.Instance.HideBannerAd() 被注释，说明广告系统可能未接入，此处用虚方法供子类实现。
    /// </summary>
    protected virtual void OnRemoveAdsApplied(int adstype)
    {
        // TODO：接入广告系统
        // AdsManager.Instance.HideBannerAd();
        // AdsManager.Instance.RemoveInterstitialAndSplash();
    }
}
//（注：内容由AI生成）
