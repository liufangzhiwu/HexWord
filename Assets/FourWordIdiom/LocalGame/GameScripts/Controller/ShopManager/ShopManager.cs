using System;
using System.Collections;
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
/// 内购推荐位权重配置
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

// ======================= 审核配置（内嵌，不新增脚本） =======================

/// <summary>道具内购位类型：灯泡 / 火箭 / 复活（时钟）</summary>
public enum PropShopType
{
    Lamp = 0,
    Rocket = 1,
    Revive = 2
}

/// <summary>
/// 审核配置静态数据。
/// 默认全部关闭：AuditStatusSwitch=0 时所有内购特殊处理不生效，走原推送逻辑。
/// 审核开时：礼包不走推送权限，全部展示；白名单为空即自动全部展示。
/// </summary>
public static class AuditConfig
{
    public static bool AuditStatusSwitch;
    public static bool IsAuditOpen => AuditStatusSwitch;

    public static bool ZenSwitch;
    public static int LotusHeartAppearCondition;
    public static int LotusHeartUnlockCondition;

    public static bool Gold6ReplaceVideoPos;
    public static bool RemoveAdsWeeklyUnlockHome;

    public static readonly List<string> AuditFixedShopItemIds = new List<string>();
    public static readonly Dictionary<PropShopType, List<string>> AuditPropShopItemIds
        = new Dictionary<PropShopType, List<string>>
        {
            { PropShopType.Lamp,   new List<string>() },
            { PropShopType.Rocket, new List<string>() },
            { PropShopType.Revive, new List<string>() }
        };

    public static bool HasAuditFixedShopWhitelist => AuditFixedShopItemIds.Count > 0;

    public static bool HasAuditPropShopWhitelist(PropShopType type)
        => GetPropShopItems(type).Count > 0;

    public static List<string> GetPropShopItems(PropShopType type)
    {
        if (AuditPropShopItemIds.TryGetValue(type, out var list) && list != null)
            return list;
        return new List<string>();
    }

    public static void ResetToDefault()
    {
        AuditStatusSwitch = false;
        ZenSwitch = false;
        LotusHeartAppearCondition = 0;
        LotusHeartUnlockCondition = 0;
        Gold6ReplaceVideoPos = false;
        RemoveAdsWeeklyUnlockHome = false;

        AuditFixedShopItemIds.Clear();
        foreach (var kv in AuditPropShopItemIds)
            if (kv.Value != null) kv.Value.Clear();
    }
}

/// <summary>
/// 商店管理器主脚本（核心）。
/// </summary>
public partial class ShopManager : MonoBehaviour
{
    // ======================= 常量 =======================

    public const string ProductRemoveAds = "RemoveADS";
    public const string ProductSingleGoods = "SingleGoods";
    public const string ProductSingleClock = "SingleClockS";
    public const string ProductGold6 = "Gold6";
    public const string ProductRemoveAdsDaily = "RemoveAdsWeek";
    public const string ProductRemoveAdsWeekly = "RemoveAdsWeekly";

    /// <summary>审核开时不展示的商品（固定商店）</summary>
    private const string ProductGold3ExcludedInAudit = "Gold3";
    /// <summary>审核开时不展示的礼包（固定商店/动态商店）</summary>
    private const string ProductBoxTimeExcludedInAudit = "BoxTime";

    /// <summary>审核开时补入固定商店的特惠礼包</summary>
    public string ProductSpecialPackageA = "SpecialPackageA";
    public string ProductSpecialPackageB = "SpecialPackageB";

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

    // ======================= 审核配置加载状态 =======================

    public bool IsAuditConfigReady { get; private set; }
    public event Action OnAuditConfigLoaded;

    private string AuditConfigKey = "Zen_AuditStatus_AZ";
    private const float AuditRequestTimeout = 5f;

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

    private Dictionary<string, bool> _slotLockedThisLevel = new Dictionary<string, bool>();
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

        // ★ 启动审核配置拉取
        StartAuditStatusLoad();

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

    // ======================= 日志 =======================

    private void LogShop(string message)
    {
        Debug.Log($"[Shop] {message}");
    }

    // ======================= 审核配置加载与解析 =======================

    public void StartAuditStatusLoad()
    {
        StartCoroutine(LoadAuditStatusConfigRoutine());
    }

    private IEnumerator LoadAuditStatusConfigRoutine()
    {
        AuditConfig.ResetToDefault();
        IsAuditConfigReady = false;

        bool isDone = false;
        GameConfigResponse serverResponse = null;

        yield return new WaitForSeconds(2f);

#if UNITY_OPENHARMONY
        AuditConfigKey = "Zen_AuditStatus_HM";
#else
        AuditConfigKey = "Zen_AuditStatus_AZ";
#endif

        yield return APIGateway.Instance.GameConfigApi.GetGameConfig(
            AuditConfigKey,
            onSuccess: (response) => { serverResponse = response; isDone = true; },
            onError: (err) =>
            {
                Debug.LogWarning($"[Zen_AuditStatus_AZ] 在线拉取失败，准备使用本地兜底: {err}");
                TextAsset localAuditAsset = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", AuditConfigKey);
                if (localAuditAsset != null && !string.IsNullOrEmpty(localAuditAsset.text))
                {
                    ParseAuditStatusConfig(localAuditAsset.text, "本地预载(防闪烁)");
                }
                else
                {
                    AuditConfig.AuditStatusSwitch = true;
                    AuditConfig.ZenSwitch = false;
                }
                isDone = true;
            }
        );

        float timeout = AuditRequestTimeout;
        while (!isDone && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (serverResponse != null && !string.IsNullOrEmpty(serverResponse.CsvString))
        {
            string localVersion = Application.version.Trim();
            string configVersion = serverResponse.Version != null
                ? serverResponse.Version.Trim() : string.Empty;

            if (string.Equals(localVersion, configVersion, StringComparison.OrdinalIgnoreCase))
                ParseAuditStatusConfig(serverResponse.CsvString,
                    $"服务端下发 (匹配版本: {localVersion})");
            else
            {
                AuditConfig.ResetToDefault();
                Debug.Log($"[Zen_AuditStatus_AZ] 版本号不一致 (本地:{localVersion} vs 服务器配置:{configVersion})，忽略配置，审核关。");
            }
        }
        else
        {
            AuditConfig.ResetToDefault();
            Debug.LogWarning("[Zen_AuditStatus_AZ] 无有效服务端配置，使用默认审核关。");
        }

        IsAuditConfigReady = true;
        OnAuditConfigLoaded?.Invoke();

        LogShop($"[Zen_AuditStatus_AZ] 审核配置就绪: IsAuditOpen={AuditConfig.IsAuditOpen}, " +
                $"FixedWhitelist={AuditConfig.AuditFixedShopItemIds.Count}, " +
                $"LampWhitelist={AuditConfig.GetPropShopItems(PropShopType.Lamp).Count}, " +
                $"RocketWhitelist={AuditConfig.GetPropShopItems(PropShopType.Rocket).Count}, " +
                $"ReviveWhitelist={AuditConfig.GetPropShopItems(PropShopType.Revive).Count}");
    }

    private void ParseAuditStatusConfig(string csvText, string source)
    {
        try
        {
            AuditConfig.ResetToDefault();

            List<List<string>> records = SplitCsvLines(csvText);
            if (records == null || records.Count <= 2)
            {
                Debug.LogWarning($"[Zen_AuditStatus_AZ] CSV 行数不足，使用默认审核关 (来源: {source})");
                return;
            }

            List<string> fields = records[2];
            if (fields == null || fields.Count < 1)
            {
                Debug.LogWarning($"[Zen_AuditStatus_AZ] CSV 字段不足，使用默认审核关 (来源: {source})");
                return;
            }

            AuditConfig.AuditStatusSwitch = GetAuditBool(fields, 0);
            AuditConfig.ZenSwitch = GetAuditBool(fields, 1);

            string lotusRaw = GetAuditField(fields, 2);
            if (!string.IsNullOrEmpty(lotusRaw))
            {
                string[] lotus = lotusRaw.Split('_');
                int appear = 0, unlock = 0;
                if (lotus.Length >= 1) int.TryParse(lotus[0], out appear);
                if (lotus.Length >= 2) int.TryParse(lotus[1], out unlock);
                AuditConfig.LotusHeartAppearCondition = appear;
                AuditConfig.LotusHeartUnlockCondition = unlock;
            }

            AuditConfig.Gold6ReplaceVideoPos = GetAuditBool(fields, 3);
            AuditConfig.RemoveAdsWeeklyUnlockHome = GetAuditBool(fields, 4);

            AuditConfig.AuditFixedShopItemIds.Clear();
            AuditConfig.AuditFixedShopItemIds.AddRange(
                ParseAuditIdList(GetAuditField(fields, 5)));

            FillAuditPropShopItems(fields, 6, PropShopType.Lamp);
            FillAuditPropShopItems(fields, 7, PropShopType.Rocket);
            FillAuditPropShopItems(fields, 8, PropShopType.Revive);

            LogShop($"[Zen_AuditStatus_AZ] 解析完成，来源【{source}】 | " +
                    $"AuditStatusSwitch={AuditConfig.IsAuditOpen}, " +
                    $"Gold6ReplaceVideoPos={AuditConfig.Gold6ReplaceVideoPos}, " +
                    $"RemoveAdsWeeklyUnlockHome={AuditConfig.RemoveAdsWeeklyUnlockHome}");
        }
        catch (Exception ex)
        {
            AuditConfig.ResetToDefault();
            Debug.LogError($"[Zen_AuditStatus_AZ] 解析异常，回默认审核关 (来源: {source}): {ex.Message}");
        }
    }

    private static void FillAuditPropShopItems(List<string> fields, int index, PropShopType type)
    {
        List<string> target = AuditConfig.GetPropShopItems(type);
        target.Clear();
        target.AddRange(ParseAuditIdList(GetAuditField(fields, index)));
    }

    private static string GetAuditField(List<string> fields, int index)
    {
        if (fields == null || index < 0 || index >= fields.Count) return string.Empty;
        return fields[index] != null ? fields[index].Trim().Trim('"') : string.Empty;
    }

    private static bool GetAuditBool(List<string> fields, int index)
        => GetAuditField(fields, index) == "1";

    private static List<string> ParseAuditIdList(string raw)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(raw)) return result;

        string[] parts = raw.Split(new[] { '|', ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string p in parts)
        {
            string id = p.Trim();
            if (!string.IsNullOrEmpty(id)) result.Add(id);
        }
        return result;
    }

    // ======================= 审核业务 =======================

    /// <summary>
    /// 按 produceNameId 获取商店商品（供广告失败弹窗等外部模块读取单道具商品的价格/折扣/文案）。
    /// </summary>
    public ShopDataItem GetShopItemByProduceName(string produceNameId)
    {
        if (string.IsNullOrEmpty(produceNameId)) return null;
        return allShopItems.FirstOrDefault(x => x.produceNameId == produceNameId);
    }

    private List<ShopDataItem> GetItemsByIds(List<string> ids)
    {
        var result = new List<ShopDataItem>();
        foreach (string id in ids)
        {
            if (string.IsNullOrEmpty(id)) continue;
            ShopDataItem item = allShopItems.FirstOrDefault(x => x.produceNameId == id);
            if (item != null) result.Add(item);
            else LogShop($"[Audit] 商品ID无效或未配置: {id}");
        }
        return result;
    }

    // 审核开时按分类函数收集 allShopItems 中所有匹配礼包（去重、只保留可售商品）
    private List<ShopDataItem> CollectAuditPacks(Predicate<string> matchId)
    {
        var result = new List<ShopDataItem>();
        var seen = new HashSet<string>();

        foreach (var item in allShopItems)
        {
            if (item == null || string.IsNullOrEmpty(item.produceNameId)) continue;
            if (seen.Contains(item.produceNameId)) continue;
            if (!matchId(item.produceNameId)) continue;
            if (item.purchaseType < 0) continue; // 仅数据，不售卖

            seen.Add(item.produceNameId);
            result.Add(item);
        }
        return result;
    }

    /// <summary>
    /// 审核固定商店商品列表。
    /// 审核关返回空列表；审核开：
    ///  - 白名单非空 → 按白名单顺序；
    ///  - 白名单为空 → 所有动态商店礼包（NewcomerBox / SpecialPackageA/B / BoxTime / Box0~9），
    ///    排除 Gold3，补入特惠礼包A/B，排序：新手礼包 → 特惠A → 特惠B → 其余按 sort。
    /// Gold6 若配置替代金币视频点，则不在固定商店网格重复展示。
    /// </summary>
    public List<ShopDataItem> GetAuditFixedStoreItems()
    {
        if (!AuditConfig.IsAuditOpen) return new List<ShopDataItem>();

        List<ShopDataItem> result;

        if (AuditConfig.HasAuditFixedShopWhitelist)
        {
            result = GetItemsByIds(AuditConfig.AuditFixedShopItemIds);
        }
        else
        {
            result = BuildAuditFixedStoreBaseList();

            LogShop($"[Audit] 商店审核固定礼包 {result.Count} 个: " +
                    $"[{string.Join(",", result.Select(x => x.produceNameId))}]");
        }

        if (ShouldReplaceCoinVideoSlotWithGold6())
            result = result.Where(x => x.produceNameId != ProductGold6).ToList();

        return result;
    }

    /// <summary>
    /// 审核开时固定商店基础列表：
    ///  - 基础：所有动态商店礼包（NewcomerBox / SpecialPackageA/B / BoxTime / Box0~9），
    ///    只匹配礼包前缀，天然排除金币/道具等普通商品；
    ///  - 排除 Gold3、BoxTime（审核开不展示）；
    ///  - 补入特惠礼包A（SpecialPackageA）、特惠礼包B（SpecialPackageB）兜底；
    ///  - 排序：新手礼包 → 特惠A → 特惠B → 其余按 sort。
    /// </summary>
    private List<ShopDataItem> BuildAuditFixedStoreBaseList()
    {
        // 动态商店礼包全集（NewcomerBox 一定在内，金币商品一定不在内）
        var baseList = CollectAuditPacks(IsDynamicStorePack);

        // 审核开不展示 Gold3、BoxTime（玲珑礼包）
        baseList = baseList
            .Where(x => x.produceNameId != ProductGold3ExcludedInAudit)
            .Where(x => x.produceNameId != ProductBoxTimeExcludedInAudit)
            .ToList();

        // 补入特惠礼包A / 特惠礼包B（若前缀未匹配上，用此兜底）
        EnsureAuditExtraPack(baseList, ProductSpecialPackageA);
        EnsureAuditExtraPack(baseList, ProductSpecialPackageB);

        return SortAuditFixedStoreItems(baseList);
    }

    private void EnsureAuditExtraPack(List<ShopDataItem> list, string nameId)
    {
        if (list == null || string.IsNullOrEmpty(nameId)) return;
        if (list.Any(x => x.produceNameId == nameId)) return;

        ShopDataItem item = allShopItems.FirstOrDefault(x => x.produceNameId == nameId);
        if (item != null)
        {
            list.Add(item);
            LogShop($"[Audit] 补入审核固定商店礼包: {nameId}");
        }
        else
        {
            LogShop($"[Audit] 特惠礼包未在商店表找到，跳过: {nameId}");
        }
    }

    /// <summary>
    /// 审核固定商店排序：
    ///  - NewcomerBox（新手礼包）最前；
    ///  - 紧随其后 SpecialPackageA（特惠礼包A）、SpecialPackageB（特惠礼包B）；
    ///  - 其余按原 sort 升序（sort=0 排末尾），再按 id。
    /// </summary>
    private List<ShopDataItem> SortAuditFixedStoreItems(List<ShopDataItem> items)
    {
        if (items == null) return new List<ShopDataItem>();

        return items
            .OrderBy(GetAuditFixedStoreGroup)
            .ThenBy(x =>
            {
                if (GetAuditFixedStoreGroup(x) < 3) return 0;
                return x.sort == 0 ? int.MaxValue : x.sort;
            })
            .ThenBy(x => x.id)
            .ToList();
    }

    private int GetAuditFixedStoreGroup(ShopDataItem item)
    {
        if (item == null || string.IsNullOrEmpty(item.produceNameId)) return 3;

        string id = item.produceNameId;
        if (id == "NewcomerBox") return 0;          // 新手礼包
        if (id == ProductSpecialPackageA) return 1;  // 特惠礼包A
        if (id == ProductSpecialPackageB) return 2;  // 特惠礼包B
        return 3;                                    // 其余
    }
    
    /// <summary>
    /// 审核开时返回某 slot 全部礼包（不走推送权限）。
    /// 白名单非空时按白名单；否则从 allShopItems 全量按分类匹配：
    ///   Bulb → 所有提示灯礼包；Rocket → 所有火箭礼包；Clock → 所有复活礼包；
    ///   DynamicStore/FixedStore → 所有动态商店礼包（排除 Gold3、BoxTime）。
    /// </summary>
    public List<ShopDataItem> GetAuditPacksForSlot(RecommendSlot slot)
    {
        if (!AuditConfig.IsAuditOpen) return new List<ShopDataItem>();

        PropShopType? propType = MapRecommendSlotToPropShopType(slot);
        if (propType.HasValue && AuditConfig.HasAuditPropShopWhitelist(propType.Value))
        {
            return GetItemsByIds(AuditConfig.GetPropShopItems(propType.Value)).ToList();
        }

        Predicate<string> matcher = null;
        bool isSort = false;
        switch (slot)
        {
            case RecommendSlot.Bulb:         matcher = IsBulbPack; break;
            case RecommendSlot.Rocket:       matcher = IsRocketPack; break;
            case RecommendSlot.Clock:        matcher = IsClockPack; break;
            case RecommendSlot.DynamicStore:
            case RecommendSlot.FixedStore:
            {
                isSort = true; 
                matcher = IsDynamicStorePack; 
                break;
            }
        }
        if (matcher == null) return new List<ShopDataItem>();

        var result = CollectAuditPacks(matcher);

        // 动态商店 / 固定商店审核开排除 Gold3、BoxTime
        if (slot == RecommendSlot.DynamicStore || slot == RecommendSlot.FixedStore)
        {
            result = result
                .Where(x => x.produceNameId != ProductGold3ExcludedInAudit)
                .Where(x => x.produceNameId != ProductBoxTimeExcludedInAudit)
                .ToList();
        }

        if(isSort)  
            result = result.OrderBy(x => x.price).ToList();

        LogShop($"[Audit] slot={slot} 审核全量礼包 {result.Count} 个: [{string.Join(",", result.Select(x => x.produceNameId))}]");
        return result;
    }

    private PropShopType? MapRecommendSlotToPropShopType(RecommendSlot slot)
    {
        switch (slot)
        {
            case RecommendSlot.Bulb:   return PropShopType.Lamp;
            case RecommendSlot.Rocket: return PropShopType.Rocket;
            case RecommendSlot.Clock:  return PropShopType.Revive;
            default: return null;
        }
    }

    public bool ShouldReplaceCoinVideoSlotWithGold6()
        => AuditConfig.IsAuditOpen && AuditConfig.Gold6ReplaceVideoPos;

    public ShopDataItem GetAuditGold6Item()
    {
        if (!ShouldReplaceCoinVideoSlotWithGold6()) return null;
        ShopDataItem item = allShopItems.FirstOrDefault(x => x.produceNameId == ProductGold6);
        if (item == null)
            LogShop($"[Audit] Gold6 商品未在商店表中找到: {ProductGold6}");
        return item;
    }

    public bool IsRemoveAdsWeeklyHomeEntryUnlocked()
    {
        if (AuditConfig.IsAuditOpen && AuditConfig.RemoveAdsWeeklyUnlockHome)
            return true;
        return IsRemoveAdsWeeklyOwned();
    }

    private bool IsRemoveAdsWeeklyOwned()
    {
        ShopDataItem item = allShopItems.FirstOrDefault(
            x => x.produceNameId == ProductRemoveAdsDaily
              || x.produceNameId == ProductRemoveAdsWeekly);
        if (item == null) return false;

        ShopLimitData limit = GetLimitData(item);
        return limit != null && limit.isget && !limit.isoverdate;
    }

    private bool IsActivityBadgeSuppressedByAudit()
    {
        if (!AuditConfig.IsAuditOpen) return false;
        LogShop("[Audit] 审核开，促销角标禁用");
        return true;
    }

    // ======================= ShopLimitData 读写 =======================

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
        return produceNameId == ProductRemoveAds
            || produceNameId == ProductRemoveAdsDaily
            || produceNameId == ProductRemoveAdsWeekly;
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

    // ======================= 推荐位推送规则 =======================

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

    public bool IsLimitedOut(ShopDataItem item)
    {
        if (item == null || item.limited <= 0) return false;
        ShopLimitData data = GetLimitData(item);
        int buyCount = data?.buyCount ?? 0;
        return buyCount >= item.limited;
    }

    // ======================= 推荐位锁定 =======================

    private string SlotKey(RecommendSlot slot, int featured)
    {
        return $"{slot}_{featured}";
    }

    public bool IsSlotLocked(RecommendSlot slot, int featured)
    {
        bool locked;
        return _slotLockedThisLevel.TryGetValue(SlotKey(slot, featured), out locked) && locked;
    }

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

    public ShopDataItem GetLastShownPack(RecommendSlot slot, int featured)
    {
        ShopDataItem pack;
        if (_slotLastShownPack.TryGetValue(SlotKey(slot, featured), out pack))
            return pack;
        return null;
    }

    public void UnlockAllSlots()
    {
        _slotLockedThisLevel.Clear();
        _slotLastShownPack.Clear();
        _bulbDiscountConsumed = false;
        _rocketDiscountConsumed = false;
        _levelVideoUsed.Clear();
        LogShop("UnlockAllSlots: 全部推荐位解锁, 折扣消费标记复位, 回显记录已清空");
    }

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

    public List<ShopDataItem> GetRecommendPacks(RecommendSlot slot)
    {
        
        int maxFeatured = 1;
        
        // ★ 审核开：完全绕过推送权限，返回该类目全部礼包
        if (AuditConfig.IsAuditOpen)
        {
            
            switch (slot)
            {
                case RecommendSlot.Bulb: return GetAuditPacksForSlot(slot);
                case RecommendSlot.DynamicStore: maxFeatured = 2; break;
                case RecommendSlot.Rocket:
                case RecommendSlot.Clock:return GetAuditPacksForSlot(slot);
                case RecommendSlot.FixedStore: maxFeatured = 1;break;
            }
        }
        else
        {
            switch (slot)
            {
                case RecommendSlot.Bulb: maxFeatured=3;break;
                case RecommendSlot.DynamicStore: maxFeatured = 2; break;
                case RecommendSlot.Rocket:
                case RecommendSlot.Clock:
                case RecommendSlot.FixedStore: maxFeatured = 1;break;
            }
        }

        LogShop($"========== GetRecommendPacks 开始: slot={slot}, shopMode={shopMode}, 时间={DateTime.Now:HH:mm:ss} ==========");

        List<ShopDataItem> result = new List<ShopDataItem>();
        bool slot1Empty = false;

        for (int featured = 1; featured <= maxFeatured; featured++)
        {
            if (IsSlotLocked(slot, featured))
            {
                ShopDataItem lastShown = GetLastShownPack(slot, featured);
                if (lastShown != null && !IsLimitedOut(lastShown))
                {
                    result.Add(lastShown);
                    LogShop($"  featured={featured} 已锁定（本关该推荐位购买过），回显礼包: {lastShown.produceNameId}");
                }
                else
                {
                    LogShop($"  featured={featured} 已锁定，但回显礼包为空或已限购，跳过");
                }
                continue;
            }

            List<ShopDataItem> candidates = GetSlotCandidates(slot, featured);
            LogShop($"  featured={featured}, 候选数={candidates.Count}, 候选=[{string.Join(",", candidates.Select(x => x.produceNameId))}]");

            if (candidates.Count == 0) continue;

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
        else if (shopDataItem.produceNameId == ProductRemoveAdsDaily
              || shopDataItem.produceNameId == ProductRemoveAdsWeekly)
        {
            adstype = (int)LimitRewordType.RemoveDayAds;
            hours = shopDataItem.produceNameId == ProductRemoveAdsWeekly ? 7 * 24 : 1 * 24;
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

        SystemManager.Instance.ShowPanel(PanelType.BuyFailScreen);
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
        if (AuditConfig.IsAuditOpen) return false;
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
        if (AuditConfig.IsAuditOpen) return false;
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
        if (AuditConfig.IsAuditOpen)
        {
            LogShop("[Audit] OnBulbRewardVideoFinished 跳过: 审核开，内购特殊处理不生效");
            return;
        }

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
        if (AuditConfig.IsAuditOpen)
        {
            LogShop("[Audit] OnInterstitialClosed 跳过: 审核开，内购特殊处理不生效");
            return;
        }

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
        LogShop("[ShopManager] ShowLimitAdsPanel 原主动弹出逻辑已删除，请接入破冰/复购策略");
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