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
    public int giftType;
    public int baseWeight;
    public int maxWeight;
    public int specialWeight;
    public int peakWeight;
    public int returnWeight;
    public int featured;
}

/// <summary>
/// 道具折扣机制配置
/// </summary>
[Serializable]
public class ShopDiscountConfig
{
    public bool enable = true;
    public int bulbDailyLimit = 3;
    public int rocketDailyLimit = 2;
    public int triggerInterval = 300;
    public int unlockLevel = 7;
}

// ======================= 商店模式 =======================

public enum ShopMode
{
    FixedStore,
    DynamicStore
}

/// <summary>
/// 商店管理器主脚本（核心）。
///
/// 购买后推荐位锁定规则（需求："当任意推送位，被购买后，当前关的该推荐位不会刷新新的礼包"）：
///  - 锁定粒度 (slot, featured)：灯泡一号位/二号位/三号位、动态商店一号位/二号位均可独立锁定；
///  - 锁定时记录"该位置最后展示的礼包"，GetRecommendPacks 遇到锁定 featured 时**回显**该礼包（不消失）；
///  - 每次进入新关卡时应调用 UnlockAllSlots() 清空所有锁定和回显记录。
/// </summary>
public partial class ShopManager : MonoBehaviour
{
    // ======================= 常量 =======================

    public const string ProductRemoveAds = "RemoveADS";
    public const string ProductSingleGoods = "SingleGoods";
    public const string ProductSingleClock = "SingleClockS";
    public const string ProductGold6 = "Gold6";
    public const string ProductRemoveAdsDaily = "RemoveAdsWeekly";

    private const int BreakIceCooldownBaseDays = 7;
    private const int BreakIceCooldownStepDays = 3;
    private const int RepurchaseCooldownDays = 7;
    private const float AdFailCooldownSeconds = 3600f;
    private const float ShareSuccessMinSeconds = 3f;
    private const float FailScoreChangeInterval = 30f;
    private const int PeakHourStart = 19;
    private const int PeakHourEnd = 23;
    private const int ChurnReturnDays = 7;
    private const int ShopGoldVideoReward = 100;

    // ======================= 商品/状态 =======================

    private List<ShopDataItem> allShopItems = new List<ShopDataItem>();
    private List<ShopDataItem> shopItems = new List<ShopDataItem>();

    public ShopDataItem curshopAdsItem;
    public static ShopManager shopManager;

    private Dictionary<int, ShopLimitData> shoplimitDatas = new Dictionary<int, ShopLimitData>();
    private List<ShopDataItem> _limitAdsGifts = new List<ShopDataItem>();

    public Action<string, bool> UpdateAdsBtnUI;
    public Dictionary<int, GameObject> shopItemsTipsPanel = new Dictionary<int, GameObject>();
    [HideInInspector] public bool paysuccess;

    // ======================= 推荐位决策共享状态 =======================

    private Dictionary<string, ShopRecommendConfig> _recommendConfigs = new Dictionary<string, ShopRecommendConfig>();

    /// <summary>各推荐位当前关卡是否已锁定（Key = "slot_featured"，如 "Bulb_1"）</summary>
    private Dictionary<string, bool> _slotLockedThisLevel = new Dictionary<string, bool>();

    /// <summary>
    /// 各推荐位当前关卡"锁定前最后展示的礼包"（Key = "slot_featured"）。
    /// 用于在锁定时回显该礼包（不消失、不刷新），下一关 UnlockAllSlots 时清空。
    /// </summary>
    private Dictionary<string, ShopDataItem> _slotLastShownPack = new Dictionary<string, ShopDataItem>();

    private Dictionary<string, int> _payInvokeCount = new Dictionary<string, int>();
    private readonly Dictionary<int, int> _tierBuyCount = new Dictionary<int, int>();
    private readonly Dictionary<RecommendSlot, string> _shownNewcomer = new Dictionary<RecommendSlot, string>();
    private ShopDataItem _lastPurchasedPack;

    // ======================= 道具折扣状态 =======================

    private ShopDiscountConfig _discountConfig = new ShopDiscountConfig();
    private int _bulbDiscountToday;
    private int _rocketDiscountToday;
    private string _discountDate = "";
    private DateTime _lastBulbDiscountTime;
    private DateTime _lastRocketDiscountTime;
    private bool _bulbDiscountConsumed;
    private bool _rocketDiscountConsumed;

    // ======================= 激励视频/插屏赠礼状态 =======================

    private int _bulbVideoGiftCount;
    private string _bulbVideoGiftDate = "";
    private int _interstitialGiftCount;
    private string _interstitialGiftDate = "";
    private bool _firstBulbVideoGifted;

    [Header("商店模式")]
    public ShopMode shopMode = ShopMode.FixedStore;

    private void Awake()
    {
        if (shopManager == null)
        {
            shopManager = this;
            DontDestroyOnLoad(gameObject);
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

        LoadRuntimeConfigs();
        Initialize();

        LogShop($"Start 完成: shopMode={shopMode}, 推荐位配置数={_recommendConfigs.Count}, 限购数据={shoplimitDatas.Count}, 限时礼包={_limitAdsGifts.Count}");
    }

    private void LoadRuntimeConfigs()
    {
        LoadBulbBoxConfig();
        LoadRocketBoxConfig();
        LoadClockBoxConfig();
        LoadDynamicStoreConfig();
        LoadLayerConfig();
        LoadHolidayWeekendTable();
        LoadDiscountConfig();

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

    // ======================= 去广告 =======================

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

    // ======================= 内购推荐位推送规则 =======================

    public enum RecommendSlot
    {
        Bulb,
        Rocket,
        Clock,
        DynamicStore,
        FixedStore
    }

    private void ParseRecommendConfigs(string data)
    {
        var records = SplitCsvLines(data);
        if (records.Count == 0)
        {
            LogShop("ParseRecommendConfigs: CSV 记录为空");
            return;
        }

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
                if (!IsLimitedOut(item))
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

    // ======================= 推荐位锁定（当前关） =======================

    private string SlotKey(RecommendSlot slot, int featured)
    {
        return $"{slot}_{featured}";
    }

    /// <summary>该推荐位（slot + featured）当前关是否已锁定（被购买过）。</summary>
    public bool IsSlotLocked(RecommendSlot slot, int featured)
    {
        bool locked;
        return _slotLockedThisLevel.TryGetValue(SlotKey(slot, featured), out locked) && locked;
    }

    /// <summary>
    /// 锁定指定的推荐位（slot + featured），当前关内不再刷新新礼包。
    /// ★ 同时记录"该位置最后展示的礼包"，GetRecommendPacks 遇到锁定时回显该礼包（不消失）。
    /// </summary>
    /// <param name="slot">推荐位类型</param>
    /// <param name="featured">推荐位格子（1/2/3）</param>
    /// <param name="shownPack">锁定时该位置展示的礼包（被购买的那个）</param>
    public void LockSlot(RecommendSlot slot, int featured, ShopDataItem shownPack)
    {
        string key = SlotKey(slot, featured);
        _slotLockedThisLevel[key] = true;
        if (shownPack != null)
        {
            _slotLastShownPack[key] = shownPack;
        }
        LogShop($"LockSlot: {slot}, featured={featured}, shownPack={(shownPack != null ? shownPack.produceNameId : "null")}");
    }

    /// <summary>取该推荐位锁定时记录的"最后展示的礼包"（无记录返回 null）。</summary>
    public ShopDataItem GetLastShownPack(RecommendSlot slot, int featured)
    {
        ShopDataItem pack;
        if (_slotLastShownPack.TryGetValue(SlotKey(slot, featured), out pack))
            return pack;
        return null;
    }

    /// <summary>
    /// 解锁所有推荐位（应在每次进入新关卡时调用）。
    /// 同时复位折扣消费标记、道具视频每关次数，并清空"上次展示礼包"记录。
    /// </summary>
    public void UnlockAllSlots()
    {
        _slotLockedThisLevel.Clear();
        _slotLastShownPack.Clear();
        _bulbDiscountConsumed = false;
        _rocketDiscountConsumed = false;
        _levelVideoUsed.Clear();
        LogShop("UnlockAllSlots: 全部推荐位解锁, 折扣消费标记复位, 回显记录已清空");
    }

    /// <summary>反查商品属于哪个推荐位 slot；不属于任何推荐位则返回 null。</summary>
    private RecommendSlot? GetSlotOfPack(ShopDataItem item)
    {
        if (item == null) return null;
        string id = item.produceNameId;
        if (string.IsNullOrEmpty(id)) return null;

        if (IsBulbPack(id)) return RecommendSlot.Bulb;
        if (IsRocketPack(id)) return RecommendSlot.Rocket;
        if (IsClockPack(id)) return RecommendSlot.Clock;
        if (IsDynamicStorePack(id)) return RecommendSlot.DynamicStore;

        return null;
    }

    // ======================= 决策权重 =======================

    public float CalculateDecisionWeight(ShopDataItem item, ShopRecommendConfig cfg, RecommendSlot slot)
    {
        if (item == null || cfg == null)
        {
            LogShop($"CalculateDecisionWeight 跳过: item={(item == null ? "null" : item.produceNameId)}, cfg={(cfg == null ? "null" : cfg.nameID)}, slot={slot}");
            return 0;
        }

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
        packWeight = Mathf.Max(0, packWeight);

        float cap = cfg.maxWeight > 0 ? cfg.maxWeight : 100f;
        packWeight = Mathf.Min(packWeight, cap);

        float special = IsHolidayOrWeekend() ? cfg.specialWeight : 0;
        float peak = IsPeakHour() ? cfg.peakWeight : 0;
        float ret = IsChurnReturn() ? cfg.returnWeight : 0;

        float cooldown = GetCooldownCoefficient(item);
        float decision = (packWeight + special + peak + ret) * cooldown * pushIndex;

        LogShop($"CalculateDecisionWeight {item.produceNameId} [slot={slot}]: " +
                $"base={cfg.baseWeight}, pay={payTendency:F1}, explore={explore:F1}, fail={fail:F1} => pack={packWeight:F2}(cap={cap}), " +
                $"special={special:F2}, peak={peak:F2}, ret={ret:F2}, cooldown={cooldown:F2}, pushIndex={pushIndex:F2} => decision={decision:F2}");

        return decision;
    }

    private bool IsPeakHour()
    {
        int hour = DateTime.Now.Hour;
        return hour >= PeakHourStart && hour < PeakHourEnd;
    }

    private bool IsChurnReturn()
    {
        DateTime lastLogin = DateTime.Parse(GameDataManager.Instance.UserData.logoutTime);
        return lastLogin != default && (DateTime.Now - lastLogin).TotalDays >= ChurnReturnDays;
    }

    private float GetCooldownCoefficient(ShopDataItem item)
    {
        ShopLimitData limit = GetLimitData(item);
        DateTime limitEndTime = limit?.GetLimitEndTime() ?? DateTime.MinValue;
        DateTime lastBuyTime = limit?.GetLastBuyTime() ?? DateTime.MinValue;

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

    // ======================= 推荐位决策入口 =======================

    /// <summary>
    /// 决出当前推荐位礼包。
    /// 关键逻辑：
    ///  - 若某 (slot, featured) 在当前关已被购买过（IsSlotLocked 返回 true），
    ///    **回显锁定时记录的最后展示礼包**（不消失、不刷新），而不是跳过；
    ///  - 动态商店/固定商店一号位：若所有可展示礼包决策权重都为 0 → 不推荐礼包；
    ///    此时若"去广告"未购买，一号位展示"去广告"；若已购买，二号位第1、2权重的礼包都展示；
    ///  - 新人礼礼包不会同时出现（灯泡＞复活＞火箭，同类型价格低＞高）。
    /// </summary>
    public List<ShopDataItem> GetRecommendPacks(RecommendSlot slot)
    {
        LogShop($"========== GetRecommendPacks 开始: slot={slot}, shopMode={shopMode}, 时间={DateTime.Now:HH:mm:ss} ==========");

        int maxFeatured = 1;
        switch (slot)
        {
            case RecommendSlot.Bulb: maxFeatured = 3; break;
            case RecommendSlot.DynamicStore: maxFeatured = 2; break;
            case RecommendSlot.Rocket:
            case RecommendSlot.Clock:
            case RecommendSlot.FixedStore: maxFeatured = 1; break;
        }

        List<ShopDataItem> result = new List<ShopDataItem>();
        bool slot1Empty = false;

        for (int featured = 1; featured <= maxFeatured; featured++)
        {
            // ★ 核心：该推荐位本关已锁定（被购买过）
            //    处理策略：回显"锁定时记录的最后展示礼包"，不消失、不刷新。
            if (IsSlotLocked(slot, featured))
            {
                ShopDataItem lastShown = GetLastShownPack(slot, featured);
                if (lastShown != null)
                {
                    result.Add(lastShown);
                    LogShop($"  featured={featured} 已锁定（本关该推荐位购买过），回显礼包: {lastShown.produceNameId}");
                }
                else
                {
                    LogShop($"  featured={featured} 已锁定，但无回显记录，跳过");
                }
                continue;
            }

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
        // 注意：若二号位本关已被锁定，则不再放开
        if (slot == RecommendSlot.DynamicStore && slot1Empty && IsRemoveAdsOwned()
            && !IsSlotLocked(slot, 2))
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

        List<ShopDataItem> valid = candidates
            .Where(c => !IsLimitedOut(c) && GetCooldownCoefficient(c) > 0f)
            .ToList();

        LogShop($"    过滤后有效候选数={valid.Count}, 有效=[{string.Join(",", valid.Select(x => x.produceNameId))}]");
        if (valid.Count == 0)
        {
            LogShop($"    DecideWinner 结果: 无有效候选");
            return null;
        }

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

        ShopDataItem winner = top[UnityEngine.Random.Range(0, top.Count)].Key;

        ShopRecommendConfig cfg = GetCfg(winner.produceNameId);
        bool isActivity = cfg != null && cfg.specialWeight != 0 && IsHolidayOrWeekend();

        ShopLimitData winnerLimit = GetOrCreateLimitData(winner);
        winnerLimit.isActivityPack = isActivity;
        if (isActivity)
        {
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
    /// 统一处理礼包购买后的逻辑（GiftTable 和 ShopItem 共用）。
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
                        LogShop($"  去广告商品，交由 HandleRemoveAdsPurchase 处理 (adstype={type})");
                        break;
                    default:
                        LogShop($"  未处理的商品类型: type={type}, count={count}");
                        break;
                }
            }
        }

        if (IsRemoveAdsProduct(shopDataItem.produceNameId))
        {
            LogShop($"  非消耗型: 去广告 {shopDataItem.produceNameId}");
            HandleRemoveAdsPurchase(shopDataItem);
        }

        if (IsUnlimitedEnergyProduct(shopDataItem.produceNameId))
        {
            LogShop($"  非消耗型: 无限体力 {shopDataItem.produceNameId}");
            HandleUnlimitedEnergyPurchase(shopDataItem);
        }

        OnPackPurchased(shopDataItem);

        LogShop($"HandleGiftTablePurchaseRewards 结束: {shopDataItem.produceNameId}");
    }

    public void OnPurchaseSuccess(ProductItem item)
    {
        LogShop($"========== OnPurchaseSuccess 开始: ProductId={item?.ProductId}, ItemName={item?.ItemName}, LocalizedPrice={item?.LocalizedPrice} ==========");

        ShopDataItem shopDataItem = GetProduct(item.ProductId);
        if (shopDataItem == null)
        {
            LogShop($"购买成功但商店未找到该商品: {item.ProductId}");
            return;
        }

        LogShop($"匹配到商店商品: id={shopDataItem.id}, produceNameId={shopDataItem.produceNameId}, type={shopDataItem.type}, giftType={shopDataItem.giftType}, price={shopDataItem.price}, label={shopDataItem.labelZh}");

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

        Game.self.Shop.CurrentShopDataItem = shopDataItem.DeepCopy();
        SystemManager.Instance.ShowPanel(PanelType.AwardScreen);

        foreach (var dataitem in shopDataItem.productContent)
        {
            if (dataitem == null || dataitem.Count < 2) continue;
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
                case (int)LimitRewordType.Tipstool:
                    GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, count, "商店购买" + item.ItemName);
                    break;
                case (int)LimitRewordType.AutoComplete:
                    GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.AutoComplete, count, "商店购买" + item.ItemName);
                    break;
                case (int)LimitRewordType.RemoveAds:
                case (int)LimitRewordType.RemoveDayAds:
                    BuyRemoveAdsEvent(type);
                    break;
                case ReviveToolType:
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
            item?.OnShipmentCompleted(true);
#endif
        }

        OnPackPurchased(shopDataItem);

        if (shopDataItem.produceNameId == ProductSingleGoods)
        {
            if (!GameDataManager.Instance.UserData.isDayMoneyBuy)
            {
                GameDataManager.Instance.UserData.isDayMoneyBuy = true;
            }
        }

        MessageSystem.Instance.ShowTip($"{shopDataItem.name} 购买成功！");
        MessageSystem.Instance.HideLoadingAnimation();

        LogShop($"========== OnPurchaseSuccess 结束: {shopDataItem.produceNameId} ==========");
    }

    public const int ReviveToolType = 1002;

    private bool IsUnlimitedEnergyProduct(string produceNameId)
    {
        return produceNameId != null && produceNameId.Contains("Energy");
    }

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

        OnRemoveAdsApplied(adstype);
        UpdateAdsBtnUI?.Invoke(permanent ? null : DateTime.Now.ToString(), true);

        RefreshLimitData();
    }

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

        EventDispatcher.instance.TriggerUpdateLayerCoin(false, true);
    }

    private int ParseUnlimitedEnergyHours(ShopDataItem item)
    {
        if (item == null) return 0;
        if (string.IsNullOrEmpty(item.produceNameId)) return 0;
        string s = item.produceNameId;

        int idx = s.Length - 1;
        while (idx >= 0 && char.IsDigit(s[idx])) idx--;
        if (idx == s.Length - 1) return 0;

        int hours;
        return int.TryParse(s.Substring(idx + 1), out hours) ? hours : 0;
    }

    /// <summary>
    /// 礼包购买后的推荐位状态更新（付费倾向、探价加分、冷却、上次购买、锁定推荐位）。
    /// </summary>
    private void OnPackPurchased(ShopDataItem shopDataItem)
    {
        LogShop($"OnPackPurchased 开始: {shopDataItem.produceNameId}, type={shopDataItem.type}, giftType={shopDataItem.giftType}, price={shopDataItem.price}");

        if (shopDataItem.type == 2)
        {
            _lastPurchasedPack = shopDataItem;
            LogShop($"  记录上次购买礼包: {_lastPurchasedPack.produceNameId}");
        }

        ShopLimitData limit = GetOrCreateLimitData(shopDataItem);

        limit.SetLastBuyTime(DateTime.Now);

        int a = shopDataItem.giftType;
        if (a > 0)
        {
            foreach (var cfg in _recommendConfigs)
            {
                ShopDataItem it = allShopItems.FirstOrDefault(x => x.produceNameId == cfg.Key);
                if (it == null) continue;
                ShopLimitData ld = GetOrCreateLimitData(it);
                if (cfg.Value.giftType == a) ld.payTendencyScore += 2f;
                else if (cfg.Value.giftType == a + 1) ld.payTendencyScore += 1f;
                else if (cfg.Value.giftType == a + 2) ld.payTendencyScore += 0.5f;
            }
            LogShop($"  付费倾向积分更新: 基准档 a={a}");
        }

        limit.exploreScore += 3f;
        LogShop($"  探价加分: {shopDataItem.produceNameId} +3, 当前={limit.exploreScore:F1}");
        if (a > 0)
        {
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
                            ld.exploreScore += 6f;
                            LogShop($"    探价加分: 下一档 {it.produceNameId} +6");
                        }
                    }
                }
            }
        }

        float failBefore = limit.conversionFailScore;
        limit.conversionFailScore = 0;
        LogShop($"  [转化失败分] {shopDataItem.produceNameId} {failBefore:F1} → 0");

        limit.buyCount++;

        limit.isActivityPack = false;
        limit.SetLimitEndTime(DateTime.MinValue);

        // ============================================================
        // ★ 锁定推荐位：只锁"被购买礼包所在的具体推荐位（slot + featured）"
        //   同时把被购买的礼包作为"锁定时最后展示的礼包"记录下来，
        //   GetRecommendPacks 遇到锁定的 featured 时会回显它，不会消失。
        // ============================================================
        RecommendSlot? slot = GetSlotOfPack(shopDataItem);
        ShopRecommendConfig packCfg = GetCfg(shopDataItem.produceNameId);
        int featured = packCfg != null ? packCfg.featured : 0;

        if (slot.HasValue && featured > 0)
        {
            LockSlot(slot.Value, featured, shopDataItem);
            LogShop($"  锁定推荐位: slot={slot.Value}, featured={featured}, 回显礼包={shopDataItem.produceNameId}");
        }
        else
        {
            LogShop($"  无法定位 {shopDataItem.produceNameId} 所属 slot/featured，跳过锁定 (slot={(slot.HasValue ? slot.Value.ToString() : "null")}, featured={featured})");
        }

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

        SystemManager.Instance.ShowPanel(PanelType.CancelPurchase);
    }

    public void OnPurchaseFailed(string productId, string error)
    {
        LogShop($"OnPurchaseFailed: productId={productId}, error={error}");
        OnPurchaseCanceled(productId, false);
    }

    public void RestorePurchases()
    {
        LogShop("RestorePurchases 调用");
        MessageSystem.Instance.ShowTip("没有可恢复的内购");
    }

    public List<string> GetPurchaseRecords()
    {
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
        string discountParam = "1_5_3_2_300";
        ParseDiscountConfig(discountParam);
    }

    public bool TryTriggerBulbDiscount(int levelId, int wrongAnswerSeconds, int remainTargetWords, int bulbCount)
    {
        if (!_discountConfig.enable) return false;
        RefreshDailyCounters();
        if (levelId < _discountConfig.unlockLevel) return false;
        if (_bulbDiscountConsumed) return false;
        if (wrongAnswerSeconds < 6 || remainTargetWords < 1) return false;
        if (bulbCount > 0) return false;
        if (_bulbDiscountToday >= _discountConfig.bulbDailyLimit) return false;
        if ((DateTime.Now - _lastBulbDiscountTime).TotalSeconds < _discountConfig.triggerInterval) return false;
        if (IsRocketDiscountShowing()) return false;

        _bulbDiscountToday++;
        _lastBulbDiscountTime = DateTime.Now;
        LogShop($"TryTriggerBulbDiscount: 触发成功, 今日次数={_bulbDiscountToday}");
        return true;
    }

    public bool TryTriggerRocketDiscount(int levelId, int cursorSeconds, int blankCount, int rocketCount)
    {
        if (!_discountConfig.enable) return false;
        RefreshDailyCounters();
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

    // ======================= 激励视频赠礼 / 插屏赠礼 =======================

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
        SystemManager.Instance.ShowPanel(PanelType.AwardScreen);
        LogShop($"OnInterstitialClosed: 第{_interstitialGiftCount}个插屏, 赠 10 金币");
    }

    // ======================= 礼包主动弹出策略 =======================

    public async void ShowLimitAdsPanel()
    {
        LogShop("[ShopManager] ShowLimitAdsPanel 原主动弹出逻辑已删除（需求 C 章节：删除原逻辑），请接入破冰/复购策略");
        await Task.Delay(1);
    }

    // ======================= 工具方法 =======================

    private static string Localize(string key)
    {
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