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
/// 决策权重的完整拆解（计算与日志共用同一份数据，避免两处算法走偏）。
/// </summary>
public struct DecisionWeightInfo
{
    /// <summary>是否算出有效权重（false 时看 skipReason）</summary>
    public bool valid;
    /// <summary>跳过原因（中文）</summary>
    public string skipReason;

    public float baseWeight;        // 配置基础权重
    public float payTendency;       // 付费倾向分
    public float exploreScore;      // 探价分
    public float failScore;         // 转化失败分
    public float packWeight;        // 礼包分（base+pay(+explore-fail)，已封顶）
    public float cap;               // 封顶值
    public float specialWeight;     // 节日/周末加权
    public float peakWeight;        // 高峰时段加权
    public float returnWeight;      // 流失回归加权
    public float cooldown;          // 冷却系数
    public float pushIndex;         // 推送指数
    public float decision;          // 最终权重
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
    /// <summary>
    /// 1 天去广告。注意：配置表 shop.csv 目前只有 RemoveADS（终身）与 RemoveAdsWeek（一周），
    /// 尚无 1 天商品；此处沿用历史命名，值与周卡相同，渠道补建 1 天商品后需改为对应 ID。
    /// </summary>
    public const string ProductRemoveAdsDaily = "RemoveAdsWeek";
    /// <summary>7 天（一周）去广告，配置表实际 ID 为 RemoveAdsWeek。</summary>
    public const string ProductRemoveAdsWeekly = "RemoveAdsWeek";

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

    // ======================= 礼包弹窗数据源（破冰 / 复购 复用 AdsDiscountScreen） =======================

    /// <summary>礼包弹窗模式：无 / 破冰 / 复购。</summary>
    public enum GiftPopupMode
    {
        None = 0,
        BreakIce = 1,      // 破冰礼包（去广告周卡，引导首次付费）
        Repurchase = 2     // 复购/升层礼包（加量不加价）
    }

    /// <summary>待展示的礼包弹窗商品（AdsDiscountScreen.OnEnable 读取）。</summary>
    public ShopDataItem pendingGiftPopupItem;

    /// <summary>待展示的礼包弹窗模式。</summary>
    public GiftPopupMode pendingGiftPopupMode = GiftPopupMode.None;

    /// <summary>礼包弹窗限时截止时间（无限时为 DateTime.MinValue）。</summary>
    public System.DateTime pendingGiftPopupEndTime = System.DateTime.MinValue;

    /// <summary>设置待展示的礼包弹窗数据。</summary>
    public void SetPendingGiftPopup(ShopDataItem item, GiftPopupMode mode, System.DateTime endTime)
    {
        pendingGiftPopupItem = item;
        pendingGiftPopupMode = mode;
        pendingGiftPopupEndTime = endTime;
        LogShop($"SetPendingGiftPopup: mode={mode}, item={(item != null ? item.produceNameId : "null")}, endTime={(endTime == System.DateTime.MinValue ? "无限时" : endTime.ToString("yyyy-MM-dd HH:mm:ss"))}");
    }

    /// <summary>清空礼包弹窗数据（弹窗关闭时调用）。</summary>
    public void ClearPendingGiftPopup()
    {
        pendingGiftPopupItem = null;
        pendingGiftPopupMode = GiftPopupMode.None;
        pendingGiftPopupEndTime = System.DateTime.MinValue;

        // 加赠金币跟着弹窗走：弹窗关了没买，赠品也要作废
        _pendingGiftBonusPackId = null;
        _pendingGiftBonusGold = 0;
    }

    // ---- 复购礼包「加量不加价」的加赠金币（只对弹窗里卖出的那一单生效）----

    /// <summary>待生效的加赠金币对应礼包 produceNameId</summary>
    private string _pendingGiftBonusPackId;

    /// <summary>待生效的加赠金币数量</summary>
    private int _pendingGiftBonusGold;

    /// <summary>
    /// 登记「这个礼包这一单要额外加赠 N 金币」。
    /// 注意：不去改商店里的真实 ShopDataItem（以前 AddPackBonus 直接往 productContent 里塞，
    /// 弹两次就叠两遍，而且之后从商店正常买也会白送金币）。
    /// </summary>
    public void SetPendingGiftBonus(string packProduceNameId, int bonusGold)
    {
        _pendingGiftBonusPackId = bonusGold > 0 ? packProduceNameId : null;
        _pendingGiftBonusGold = bonusGold > 0 ? bonusGold : 0;
        LogShop($"SetPendingGiftBonus: pack={packProduceNameId}, bonusGold={bonusGold}");
    }

    /// <summary>取出并清空该礼包的加赠金币（购买成功时用；不匹配返回 0）。</summary>
    public int ConsumePendingGiftBonus(string packProduceNameId)
    {
        if (_pendingGiftBonusGold <= 0) return 0;
        if (string.IsNullOrEmpty(packProduceNameId) || _pendingGiftBonusPackId != packProduceNameId) return 0;

        int bonus = _pendingGiftBonusGold;
        _pendingGiftBonusPackId = null;
        _pendingGiftBonusGold = 0;
        LogShop($"ConsumePendingGiftBonus: pack={packProduceNameId}, +{bonus} 金币");
        return bonus;
    }

    /// <summary>
    /// 当前弹窗里这个礼包的加赠金币（不做消耗，给界面展示用；例如 AdsDiscountScreen 的 GiftAddItem）。
    /// </summary>
    public int GetPendingGiftBonus(string packProduceNameId)
    {
        if (_pendingGiftBonusGold <= 0) return 0;
        if (string.IsNullOrEmpty(packProduceNameId) || _pendingGiftBonusPackId != packProduceNameId) return 0;
        return _pendingGiftBonusGold;
    }

    /// <summary>
    /// 把「加赠金币」并进礼包里已有的金币条目（加量不加价）。
    /// ★ 不再新增一条内容：礼包本来 7 个奖励，多加一条会变成 8 个奖励项（初心礼包的问题）。
    /// 礼包里完全没有金币条目时才新增一条。
    /// </summary>
    public static void MergeBonusGoldIntoContent(ShopDataItem item, int bonusGold)
    {
        if (item == null || bonusGold <= 0) return;

        if (item.productContent == null)
            item.productContent = new List<List<string>>();

        foreach (var entry in item.productContent)
        {
            if (entry == null || entry.Count < 2) continue;

            int type;
            if (!int.TryParse(entry[0], out type) || type != (int)LimitRewordType.Coins) continue;

            int count;
            int.TryParse(entry[1], out count);
            entry[1] = (count + bonusGold).ToString();
            return;
        }

        // 礼包里没有金币条目 → 只能单独加一条
        item.productContent.Add(new List<string>
        {
            ((int)LimitRewordType.Coins).ToString(), bonusGold.ToString()
        });
    }

    // ======================= 结算页礼包入口（破冰 / 复购按钮） =======================

    /// <summary>结算页礼包类型。</summary>
    public enum SettlementGiftKind
    {
        None = 0,
        BreakIce = 1,     // 破冰礼包（去广告一周）
        Repurchase = 2    // 限时折扣礼包（复购/升层）
    }

    /// <summary>
    /// 当前结算页可售的礼包类型。
    /// 结算策略（TryShowBreakIcePopup / TryShowRepurchasePopup）决定后登记在这里，
    /// 结算页据此显示「破冰礼包 / 限时折扣礼包」按钮；弹窗被关掉也能用按钮再次打开。
    /// </summary>
    public SettlementGiftKind CurrentSettlementGift { get; private set; } = SettlementGiftKind.None;

    /// <summary>登记的结算页礼包（展示用副本，含加赠金币）</summary>
    private ShopDataItem _settlementGiftItem;

    private System.DateTime _settlementGiftEndTime = System.DateTime.MinValue;
    private int _settlementGiftBonusGold;
    private string _settlementGiftBonusPackId;

    /// <summary>为 true 时 ShowSettlementGift 只登记不弹窗（结算页先出按钮，稍后再弹）</summary>
    private bool _suspendGiftPopup;

    /// <summary>预登记结束后，是否还有一个礼包弹窗等着被弹出来</summary>
    private bool _settlementGiftPopupPending;

    /// <summary>
    /// 结算礼包弹窗是否还在「等待弹出」阶段（预登记完成、等结算页展示一小会儿后才真正弹）。
    /// 结算页的排行榜等后续弹窗用它来让礼包弹窗优先。
    /// </summary>
    public bool IsSettlementGiftPopupPending => _settlementGiftPopupPending;

    /// <summary>
    /// 取消「待弹出」的结算礼包弹窗，但<b>保留</b>结算页按钮的登记（玩家仍可手动点开）。
    /// 用于结算页要弹更优先的弹窗（插屏 / 七日胜利 / 灯明心悟 / 锦鲤竞速…）时。
    /// </summary>
    public void CancelPreparedSettlementGiftPopup(string reason)
    {
        if (!_settlementGiftPopupPending) return;

        _settlementGiftPopupPending = false;
        LogShop($"[结算礼包] 取消本次礼包弹窗（{reason}），结算页按钮保留: " +
                $"kind={CurrentSettlementGift}, 礼包={GetSettlementGiftPackId()}");
    }

    /// <summary>结算页是否指定类型（破冰 / 复购）的礼包可售。</summary>
    public bool HasSettlementGift(SettlementGiftKind kind)
    {
        return kind != SettlementGiftKind.None && CurrentSettlementGift == kind;
    }

    /// <summary>
    /// 登记结算页礼包并立刻弹出 AdsDiscountScreen。
    /// 调用方：TryShowBreakIcePopup（破冰）/ ShowRepurchasePopup（复购）。
    /// 处于「预登记」状态（PrepareSettlementGift）时只登记，弹窗留给 PlayPreparedSettlementGiftPopup。
    /// </summary>
    public void ShowSettlementGift(SettlementGiftKind kind, ShopDataItem displayItem, System.DateTime endTime,
                                   int bonusGold = 0, string bonusPackId = null)
    {
        RegisterSettlementGift(kind, displayItem, endTime, bonusGold, bonusPackId);

        if (CurrentSettlementGift == SettlementGiftKind.None) return;   // 登记失败（礼包为空）

        if (_suspendGiftPopup)
        {
            _settlementGiftPopupPending = true;
            LogShop($"[结算礼包] 预登记（暂不弹窗）kind={kind}，等结算页展示后再弹");
            return;
        }

        OpenSettlementGiftPopup();
    }

    /// <summary>
    /// 只登记结算页礼包、<b>不弹窗</b>（结算页的「破冰礼包 / 限时折扣礼包」按钮据此显示）。
    /// 场景：复购弹窗在本活动期已经主动弹过一次 → 后续结算只保留按钮，不再主动弹出。
    /// </summary>
    public void RegisterSettlementGift(SettlementGiftKind kind, ShopDataItem displayItem, System.DateTime endTime,
                                       int bonusGold = 0, string bonusPackId = null)
    {
        if (displayItem == null)
        {
            LogShop($"RegisterSettlementGift 跳过: 礼包为空 (kind={kind})");
            return;
        }

        CurrentSettlementGift = kind;
        _settlementGiftItem = displayItem;
        _settlementGiftEndTime = endTime;
        _settlementGiftBonusGold = bonusGold;
        _settlementGiftBonusPackId = bonusPackId;

        LogShop($"RegisterSettlementGift: 登记结算页礼包 kind={kind}, item={displayItem.produceNameId}, " +
                $"endTime={(endTime == System.DateTime.MinValue ? "无限时" : endTime.ToString("yyyy-MM-dd HH:mm:ss"))}, 加赠={bonusGold}");
    }

    /// <summary>
    /// 结算页礼包按钮点击：用登记过的数据再次打开弹窗（弹窗关掉也能重开）。
    /// </summary>
    public void OpenSettlementGiftPopup()
    {
        if (CurrentSettlementGift == SettlementGiftKind.None || _settlementGiftItem == null)
        {
            LogShop("OpenSettlementGiftPopup 跳过: 当前结算页没有可售礼包");
            return;
        }

        GiftPopupMode mode = CurrentSettlementGift == SettlementGiftKind.BreakIce
            ? GiftPopupMode.BreakIce
            : GiftPopupMode.Repurchase;

        SetPendingGiftPopup(_settlementGiftItem, mode, _settlementGiftEndTime);
        SetPendingGiftBonus(_settlementGiftBonusPackId, _settlementGiftBonusGold);

        if (SystemManager.Instance != null)
            SystemManager.Instance.ShowPanel(PanelType.AdsDiscountScreen);

        LogShop($"OpenSettlementGiftPopup: kind={CurrentSettlementGift}, item={_settlementGiftItem.produceNameId}");
    }

    /// <summary>清空结算页礼包登记（换结算 / 已购买 / 离开结算页）。</summary>
    public void ClearSettlementGift(string reason)
    {
        _settlementGiftPopupPending = false;

        if (CurrentSettlementGift == SettlementGiftKind.None) return;

        CurrentSettlementGift = SettlementGiftKind.None;
        _settlementGiftItem = null;
        _settlementGiftEndTime = System.DateTime.MinValue;
        _settlementGiftBonusGold = 0;
        _settlementGiftBonusPackId = null;

        LogShop($"ClearSettlementGift: {reason}");
    }

    /// <summary>
    /// 结算页登记礼包的限时截止时间（结算页按钮倒计时用）。
    /// 没有登记 / 该礼包没有倒计时 → DateTime.MinValue。
    /// </summary>
    public System.DateTime GetSettlementGiftEndTime()
    {
        if (CurrentSettlementGift == SettlementGiftKind.None) return System.DateTime.MinValue;
        return _settlementGiftEndTime;
    }

    /// <summary>结算页登记礼包的 ID（日志 / 排查用）。</summary>
    public string GetSettlementGiftPackId()
    {
        return _settlementGiftItem != null ? _settlementGiftItem.produceNameId : string.Empty;
    }

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

    /// <summary>
    /// 本关唯一允许出「新人礼」的推荐位槽（灯泡 / 复活 / 火箭）。
    /// 进入关卡时一次性决出，避免"先点开的弹窗先拿到新人礼"导致三个槽各出一个。
    /// </summary>
    private RecommendSlot? _newcomerOwner;
    private bool _newcomerOwnerResolved;
    /// <summary>上次做新人礼重置的关卡号，用于"同关重进不重置"</summary>
    private int _newcomerResetStage = -1;

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

        // ★ 调试开关（LogManager 上勾选）：启动时先清掉「永久去广告」状态 + 对应购买记录
        //   （= 彻底回到没买过去广告的新号），再 Initialize 重建限购缓存
        if (LogManager.ClearPermanentRemoveAdsOnStart)
            ClearPermanentRemoveAds("LogManager 启动开关", blockRestore: true, clearPurchaseRecords: true);
        else if (IsPermanentRemoveAdsRestoreBlocked())
            LogShop("[去广告] 当前处于「永久去广告补单拦截」状态（之前调试清除留下）：" +
                    "平台已购订单不会补发回来；重新购买永久去广告或 GM 重置存档可解除");

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

    /// <summary>
    /// 商店日志（关键事件：进店、推荐位决策、购买、售罄、限购）。
    /// 开关在 LogManager 上（enableShopLog，默认开启）；落盘策略见 LogManager.ShopLog。
    /// </summary>
    private void LogShop(string message)
    {
        LogManager.ShopLog(message);
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

    /// <summary>
    /// 清除「永久去广告」状态（把 isget 的 RemoveAds 记录整条移除）。
    /// 清除后 IsRemoveAdsOwned() 变 false，广告会恢复正常展示。
    ///
    /// 用途：开发/测试反复验证「去广告购买 → IAA 分层 / 广告链路」时，
    /// 不用清整份存档；入口有两个：
    ///   · `LogManager.clearPermanentRemoveAdsOnStart` 勾选 → 每次启动自动做「彻底回到新号」的清理；
    ///   · 代码里直接调 `ShopManager.shopManager.ClearPermanentRemoveAds("原因")`。
    ///
    /// ★ 只清 limitShopItems 不够：永久去广告在平台侧是**非消耗型商品**，
    ///   每次启动 `Game.self.Shop.Restore()` 都会把这张已完成的订单查回来，
    ///   `DeliverRestoredPurchase` 只按「购买记录 orderId」去重 —— 记录还在就不会重复发，
    ///   但只要记录缺失（老版本购买 / GM 清过档 / 重装换机 / 记录超 100 条被截断 / 平台没回 orderId），
    ///   下次启动就会被当成一笔"待补单"重新发放，永久去广告又回来了。
    ///   所以这里同时写一个**本地拦截标记**（PlayerPrefs，跨进程有效），
    ///   由 `DeliverRestoredPurchase` 拦掉永久去广告的补单；玩家重新购买或 GM 重置存档时自动解除。
    ///
    /// ★ 明确要求「连购买记录一起清、彻底回到新号」（测试去广告相关功能）时，
    ///   传 `clearPurchaseRecords: true` —— 会删掉 RemoveADS 的订单记录（含订单历史列表里的那一条），
    ///   之后只能靠上面的拦截标记来阻止平台重新补发，两者必须同时启用。
    /// </summary>
    /// <param name="reason">调用来源，仅用于日志</param>
    /// <param name="blockRestore">是否同时拦截后续的平台补单（默认 true；调试想验证补单链路时传 false）</param>
    /// <param name="clearPurchaseRecords">是否把「永久去广告」的购买记录一起删掉（默认 false；LogManager 开关会传 true）</param>
    /// <returns>被清除的 limitShopItems 记录条数</returns>
    public int ClearPermanentRemoveAds(string reason = "手动清除", bool blockRestore = true,
        bool clearPurchaseRecords = false)
    {
        UserData user = GameDataManager.Instance != null ? GameDataManager.Instance.UserData : null;
        if (user == null || user.limitShopItems == null)
        {
            LogShop($"[去广告] 清除永久去广告失败：UserData 未就绪（{reason}）");
            return 0;
        }

        // ★ 先置拦截：即使本次没有可清的记录（已经清过 / 上次被 GM 清档清掉了），
        //   也要拦住平台补单，否则下一次 Restore 又会把它发回来。
        if (blockRestore) SetPermanentRemoveAdsRestoreBlock(true, reason);

        int removed = user.limitShopItems.RemoveAll(x => x != null
            && x.isget
            && x.adstype == (int)LimitRewordType.RemoveAds);

        // 连购买记录一起清：删掉的是「永久去广告」这个商品本身的订单（RemoveADS）。
        // 注意不能删普通礼包的记录 —— 那些是消耗型，删了只会让订单历史缺一条，对补单没有意义。
        int removedRecords = 0;
        if (clearPurchaseRecords)
        {
            removedRecords = user.RemovePurchaseRecords(
                r => r != null && r.produceNameId == ProductRemoveAds);
        }

        if (removed <= 0 && removedRecords <= 0)
        {
            LogShop($"[去广告] 清除永久去广告：没有已生效的永久去广告记录 / 购买记录（{reason}）");
            return 0;
        }

        if (GameDataManager.Instance != null) GameDataManager.Instance.CommitGameData();
        RefreshLimitData();

        // 通知外部（结算页/商店的去广告入口、广告规则）按"无去广告"刷新
        UpdateAdsBtnUI?.Invoke(null, false);

        LogShop($"[去广告] 已清除永久去广告：状态记录 {removed} 条" +
                (clearPurchaseRecords ? $"、购买记录 {removedRecords} 条" : "") +
                $"（{reason}），当前 IsRemoveAdsOwned={IsRemoveAdsOwned()}，" +
                $"补单拦截={IsPermanentRemoveAdsRestoreBlocked()}");
        return removed;
    }

    /// <summary>
    /// 是否正在拦截「永久去广告」的平台补单（由 <see cref="ClearPermanentRemoveAds"/> 置位，
    /// 玩家重新购买 / GM 重置存档 / 手动调用 <see cref="SetPermanentRemoveAdsRestoreBlock"/> 解除）。
    /// </summary>
    public bool IsPermanentRemoveAdsRestoreBlocked()
    {
        return PlayerPrefs.GetInt(PrefRemoveAdsRestoreBlock, 0) == 1;
    }

    /// <summary>开启/关闭「永久去广告补单拦截」（本地持久化，杀进程也不丢）。</summary>
    public void SetPermanentRemoveAdsRestoreBlock(bool blocked, string reason = "")
    {
        bool current = IsPermanentRemoveAdsRestoreBlocked();
        if (current == blocked) return;

        if (blocked) PlayerPrefs.SetInt(PrefRemoveAdsRestoreBlock, 1);
        else PlayerPrefs.DeleteKey(PrefRemoveAdsRestoreBlock);

        PlayerPrefs.Save();
        LogShop($"[去广告] 补单拦截 {(blocked ? "已开启" : "已解除")}（{reason}）");
    }

    /// <summary>
    /// 是否「永久去广告」商品：商品名是 RemoveADS，或礼包内容里带 6（RemoveAds）。
    /// 注意不含 7（限时去广告）—— 那个是消耗型，补单本身就靠购买记录去重。
    /// </summary>
    private bool IsPermanentRemoveAdsProduct(ShopDataItem item)
    {
        if (item == null) return false;
        if (item.produceNameId == ProductRemoveAds) return true;

        if (item.productContent != null)
        {
            for (int i = 0; i < item.productContent.Count; i++)
            {
                var content = item.productContent[i];
                if (content != null && content.Count > 0
                    && GetInt(content[0]) == (int)LimitRewordType.RemoveAds)
                    return true;
            }
        }
        return false;
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

    /// <summary>该礼包是否在本次决策的排除列表里（excludeIds 为 null 时一律返回 false）。</summary>
    private static bool IsExcludedPack(ShopDataItem item, ICollection<int> excludeIds)
    {
        return item != null && excludeIds != null && excludeIds.Contains(item.id);
    }

    /// <summary>
    /// 过滤掉本次决策要排除的礼包。
    /// 列表是新建的副本，不会改动 ShopManager 内部的推荐配置集合 / 候选缓存。
    /// </summary>
    private static List<ShopDataItem> FilterExcludedPacks(List<ShopDataItem> packs, ICollection<int> excludeIds)
    {
        if (packs == null || packs.Count == 0) return packs;
        if (excludeIds == null || excludeIds.Count == 0) return packs;

        var filtered = new List<ShopDataItem>(packs.Count);
        for (int i = 0; i < packs.Count; i++)
        {
            ShopDataItem item = packs[i];
            if (IsExcludedPack(item, excludeIds)) continue;
            filtered.Add(item);
        }
        return filtered;
    }

    // ======================= 1 号位共享决策（bug52） =======================

    /// <summary>1 号位决策的上下文 key（关卡 / 审核开关 / 是否已拥有永久去广告），变了就重新决策</summary>
    private string _slot1DecisionKey;

    /// <summary>1 号位已决定的礼包（固定商店 / 动态商店共用）</summary>
    private ShopDataItem _slot1Decision;

    /// <summary>构造 1 号位决策上下文 key</summary>
    private string BuildSlot1DecisionKey()
    {
        var ctrl = ChessStageController.Instance;
        string stage = (ctrl != null && ctrl.CurrStageInfo != null)
            ? $"{GameDataManager.Instance.UserData.levelMode}_{ctrl.CurrStageInfo.StageNumber}"
            : "-";

        return $"{stage}|audit={AuditConfig.IsAuditOpen}|rmads={IsRemoveAdsOwned()}";
    }

    /// <summary>
    /// 固定商店 / 动态商店共用的 1 号位决策。
    /// 缓存仍然有效（同关卡、未被限购、未被排除、仍在候选里）就直接复用，保证两个入口展示同一个礼包；
    /// 否则重新跑一次 DecideWinner 并记下结果。
    /// </summary>
    private List<ShopDataItem> DecideSharedSlot1Winner(List<ShopDataItem> candidates, RecommendSlot slot, ICollection<int> excludeIds)
    {
        // ★ 迎新礼包未购买时固定占 1 号位（固定商店原来是自己 Insert(0)，动态商店没有 → 两边必然不一致）。
        //   现在把「迎新礼包置顶」也纳入共享 1 号位决策，两个商店在迎新礼包卖掉之前都会展示它。
        ShopDataItem newcomer = candidates.FirstOrDefault(c => c != null && c.produceNameId == ProductNewcomerBox);
        if (newcomer != null
            && !IsNewcomerPurchased(newcomer)
            && !IsExcludedPack(newcomer, excludeIds))
        {
            _slot1DecisionKey = BuildSlot1DecisionKey();
            _slot1Decision = newcomer;
            LogShop($"  1号位共享决策: 迎新礼包固定置顶 {newcomer.produceNameId}（本次入口={slot}）");
            return new List<ShopDataItem> { newcomer };
        }

        string key = BuildSlot1DecisionKey();

        if (_slot1Decision != null
            && _slot1DecisionKey == key
            && !IsLimitedOut(_slot1Decision)
            && !IsExcludedPack(_slot1Decision, excludeIds)
            && candidates.Any(c => c != null && c.id == _slot1Decision.id))
        {
            LogShop($"  1号位共享决策: 复用 {_slot1Decision.produceNameId}（本次入口={slot}）");
            return new List<ShopDataItem> { _slot1Decision };
        }

        var winner = DecideWinner(candidates, slot);
        if (winner != null && winner.Count > 0)
        {
            _slot1DecisionKey = key;
            _slot1Decision = winner[0];
            LogShop($"  1号位共享决策: 新决策 {winner[0].produceNameId}（本次入口={slot}），后续两个商店共用");
        }

        return winner;
    }

    /// <summary>清空 1 号位共享决策（换关 / 购买后重新决策）</summary>
    private void ResetSlot1Decision(string reason)
    {
        if (_slot1Decision == null) return;

        _slot1Decision = null;
        _slot1DecisionKey = null;
        LogShop($"  1号位共享决策已清空（{reason}）");
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

    /// <summary>记录该推荐位本次展示的礼包；若是新人礼则登记本关已出（用于互斥）。</summary>
    public void RecordSlotShownPack(RecommendSlot slot, int featured, ShopDataItem pack)
    {
        if (pack == null) return;

        _slotLastShownPack[SlotKey(slot, featured)] = pack;

        if (IsNewcomerLabel(pack.labelZh))
            MarkNewcomerShown(slot, pack.produceNameId);
    }

    /// <summary>
    /// 决策结果变化时，把上一次展示礼包的转化失败分 F 归零（需求 C-RECO-04）。
    /// 换包说明上一次的 F 已不适用于新决策，避免旧分持续压制/抬高候选。
    /// </summary>
    public void ResetFailScoreOnDecisionChange(RecommendSlot slot, int featured, ShopDataItem newPack)
    {
        if (newPack == null) return;

        ShopDataItem last = GetLastShownPack(slot, featured);
        if (last != null && last.produceNameId != newPack.produceNameId)
        {
            ShopLimitData limit = GetLimitData(last);
            if (limit != null && limit.conversionFailScore != 0f)
            {
                float before = limit.conversionFailScore;
                limit.conversionFailScore = 0f;
                limit.SetLastFailScoreTime(DateTime.Now);
                LogShop($"  [决策变化归零] {slot}_{featured}: {last.produceNameId} F {before:F1} → 0，新决策={newPack.produceNameId}");
            }
        }

        RecordSlotShownPack(slot, featured, newPack);
    }

    public void UnlockAllSlots()
    {
        _slotLockedThisLevel.Clear();
        _slotLastShownPack.Clear();
        _bulbDiscountConsumed = false;
        _rocketDiscountConsumed = false;
        // ★ bug52：换关/重进关卡要重新决策 1 号位（固定商店与动态商店共用这一份）
        ResetSlot1Decision("UnlockAllSlots（进关/换关）");
        // 提示/复活视频次数改由 SyncToolVideoLevel 按"关卡变化"刷新（并持久化），
        // 这里不再无条件清空，避免同一关重进/重启游戏时误清零。
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
            LogShop($"计算决策权重(CalculateDecisionWeight) 跳过：礼包={(item == null ? "null" : item.produceNameId)}，" +
                    $"配置={(cfg == null ? "null" : cfg.nameID)}，推荐位={SlotNameCn(slot)}");
            return 0;
        }

        DecisionWeightInfo info = EvaluateDecisionWeight(item, cfg, slot);

        if (!info.valid)
        {
            LogShop($"计算决策权重(CalculateDecisionWeight) 跳过：{item.produceNameId} {info.skipReason}");
            return 0;
        }

        LogShop($"计算决策权重(CalculateDecisionWeight) {item.produceNameId}（推荐位={SlotNameCn(slot)}）：" +
                $"基础={info.baseWeight:F1}，付费倾向={info.payTendency:F1}，探价={info.exploreScore:F1}，失败分={info.failScore:F1}" +
                $" → 礼包分={info.packWeight:F2}(封顶{info.cap:F0})；" +
                $"节日周末={info.specialWeight:F2}，高峰={info.peakWeight:F2}，流失回归={info.returnWeight:F2}，" +
                $"冷却={info.cooldown:F2}，推送指数={info.pushIndex:F2} → 最终权重={info.decision:F2}");

        return info.decision;
    }

    /// <summary>
    /// 计算决策权重并返回完整拆解（算法唯一入口，CalculateDecisionWeight / 购买后日志快照都走这里）。
    /// </summary>
    private DecisionWeightInfo EvaluateDecisionWeight(ShopDataItem item, ShopRecommendConfig cfg, RecommendSlot slot)
    {
        DecisionWeightInfo info = new DecisionWeightInfo();

        if (item == null || cfg == null)
        {
            info.valid = false;
            info.skipReason = $"缺少数据(item={(item == null ? "null" : item.produceNameId)}, cfg={(cfg == null ? "null" : cfg.nameID)})";
            return info;
        }

        if (IsLimitedOut(item))
        {
            ShopLimitData ld = GetLimitData(item);
            info.valid = false;
            info.skipReason = $"限购已满(限购={item.limited}, 已购={ld?.buyCount ?? 0})";
            return info;
        }

        ShopLimitData limit = GetLimitData(item);
        info.baseWeight = cfg.baseWeight;
        info.payTendency = limit?.payTendencyScore ?? 0f;
        info.exploreScore = limit?.exploreScore ?? 0f;
        info.failScore = limit?.conversionFailScore ?? 0f;
        info.pushIndex = limit?.pushStateIndex ?? 1f;

        float packWeight = cfg.baseWeight + info.payTendency;
        // ★ 固定商店 / 动态商店的 1 号位是同一个推荐位（bug52），权重口径必须一致：
        //   之前只有灯泡和动态商店算探价/失败分，固定商店不算 → 同一份候选池算出不同礼包。
        if (slot == RecommendSlot.Bulb
            || slot == RecommendSlot.DynamicStore
            || slot == RecommendSlot.FixedStore)
        {
            packWeight += info.exploreScore - info.failScore;
        }
        packWeight = Mathf.Max(0, packWeight);

        info.cap = cfg.maxWeight > 0 ? cfg.maxWeight : 100f;
        info.packWeight = Mathf.Min(packWeight, info.cap);

        info.specialWeight = IsHolidayOrWeekend() ? cfg.specialWeight : 0;
        info.peakWeight = IsPeakHour() ? cfg.peakWeight : 0;
        info.returnWeight = IsChurnReturn() ? cfg.returnWeight : 0;
        info.cooldown = GetCooldownCoefficient(item);

        info.decision = (info.packWeight + info.specialWeight + info.peakWeight + info.returnWeight)
                        * info.cooldown * info.pushIndex;
        info.valid = true;
        return info;
    }

    private bool IsPeakHour()
    {
        int hour = DateTime.Now.Hour;
        return hour >= PeakHourStart && hour < PeakHourEnd;
    }

    /// <summary>上次打印流失回归日志的结论（只在结论变化时打印，避免每个礼包刷一条）。</summary>
    private bool? _lastChurnReturnLogged;

    /// <summary>
    /// 「流失回归」判定：最近 ChurnReturnDays(7) 天未登录。
    ///
    /// ★ 不能用 UserData.logoutTime 直接判断：`UserData.SaveData()` 每次存档都会把 logoutTime 刷成"现在"，
    ///   玩家回归后只要触发一次自动存档（进关/购买/切后台…），间隔就变成 0，
    ///   回归权重从此变成 0 且整次会话不再恢复 —— 这正是"成功触发一次之后，再次满足条件也没有权重"的原因。
    ///
    /// 现在用两个来源取或：
    ///   ① GameDataManager.SessionLogoutTime：启动/应用服务器存档时抓的「上次离开时间」快照（整次会话不变）；
    ///   ② 当前 logoutTime：覆盖"App 一直挂在后台好几天"的情况（切后台时会存档，此时它=离开时间）。
    /// 只要其中一个间隔 ≥ 7 天就算回归，不会因为后续存档把已生效的权重冲掉。
    /// </summary>
    private bool IsChurnReturn()
    {
        DateTime now = DateTime.Now;

        DateTime snapshotLogout = GameDataManager.SessionLogoutTime;
        DateTime currentLogout;
        if (!DateTime.TryParse(GameDataManager.Instance?.UserData?.logoutTime, out currentLogout))
            currentLogout = DateTime.MinValue;

        double snapshotDays = snapshotLogout == DateTime.MinValue ? 0 : (now - snapshotLogout).TotalDays;
        double currentDays = currentLogout == DateTime.MinValue ? 0 : (now - currentLogout).TotalDays;

        bool isReturn = snapshotDays >= ChurnReturnDays || currentDays >= ChurnReturnDays;

        if (_lastChurnReturnLogged != isReturn)
        {
            _lastChurnReturnLogged = isReturn;
            LogShop($"[权重] 流失回归: 启动前离开={FormatTime(snapshotLogout)}({snapshotDays:F1}天前), " +
                    $"当前 logoutTime={FormatTime(currentLogout)}({currentDays:F1}天前), " +
                    $"阈值={ChurnReturnDays}天 → {isReturn}");
        }

        return isReturn;
    }

    private static string FormatTime(DateTime t)
    {
        return t == DateTime.MinValue ? "无" : t.ToString("yyyy-MM-dd HH:mm");
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
    /// 取某推荐位本次要展示的礼包。
    /// </summary>
    /// <param name="excludeIds">
    /// 需要排除的礼包 id（例如「本关已经买过、本次不想再推」的礼包）。
    /// 传 null 表示不排除。★ 注意：只是本次决策时跳过，不会改动任何积分/冷却数据。
    /// </param>
    public List<ShopDataItem> GetRecommendPacks(RecommendSlot slot, ICollection<int> excludeIds = null)
    {
        
        int maxFeatured = 1;
        
        // ★ 审核开：完全绕过推送权限，返回该类目全部礼包
        if (AuditConfig.IsAuditOpen)
        {
            
            switch (slot)
            {
                case RecommendSlot.Bulb: return FilterExcludedPacks(GetAuditPacksForSlot(slot), excludeIds);
                case RecommendSlot.DynamicStore: maxFeatured = 2; break;
                case RecommendSlot.Rocket:
                case RecommendSlot.Clock:return FilterExcludedPacks(GetAuditPacksForSlot(slot), excludeIds);
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

        LogShop($"========== GetRecommendPacks 开始: slot={slot}, shopMode={shopMode}, 排除数={(excludeIds == null ? 0 : excludeIds.Count)}, 时间={DateTime.Now:HH:mm:ss} ==========");

        List<ShopDataItem> result = new List<ShopDataItem>();
        bool slot1Empty = false;

        for (int featured = 1; featured <= maxFeatured; featured++)
        {
            if (IsSlotLocked(slot, featured))
            {
                ShopDataItem lastShown = GetLastShownPack(slot, featured);
                if (lastShown != null && !IsLimitedOut(lastShown) && !IsExcludedPack(lastShown, excludeIds))
                {
                    result.Add(lastShown);
                    LogShop($"  featured={featured} 已锁定（本关该推荐位购买过），回显礼包: {lastShown.produceNameId}");
                }
                else
                {
                    LogShop($"  featured={featured} 已锁定，但回显礼包为空/已限购/已排除，跳过");
                }
                continue;
            }

            List<ShopDataItem> candidates = FilterExcludedPacks(GetSlotCandidates(slot, featured), excludeIds);
            LogShop($"  featured={featured}, 候选数={candidates.Count}, 候选=[{string.Join(",", candidates.Select(x => x.produceNameId))}]");

            if (candidates.Count == 0) continue;

            if ((slot == RecommendSlot.DynamicStore || slot == RecommendSlot.FixedStore) && featured == 1)
            {
                // ★ bug52：固定商店与动态商店的 1 号位是同一个推荐位，必须展示同一个礼包。
                //   两个入口各自调一次 GetRecommendPacks，之前每次都是「重新决策」（还有随机并列兜底、
                //   以及固定商店把探价/失败分排除在权重外），所以两边经常算出不同的礼包。
                //   现在 1 号位决策按「关卡 + 审核开关 + 是否已拥有永久去广告」缓存一次，两个商店共用。
                var winners = DecideSharedSlot1Winner(candidates, slot, excludeIds);
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

            // ★ 记录本次展示 + 决策变化时把上一次礼包的 F 归零（C-RECO-04）
            if (result.Count > 0)
                ResetFailScoreOnDecisionChange(slot, featured, result[result.Count - 1]);
        }

        if (slot == RecommendSlot.DynamicStore && slot1Empty && IsRemoveAdsOwned()
            && !IsSlotLocked(slot, 2))
        {
            List<ShopDataItem> second = FilterExcludedPacks(GetSlotCandidates(slot, 2), excludeIds);
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

        // 权重并列：先按「低价优先」（需求 C-RECO-09），同价再随机
        float minPrice = top.Min(x => x.Key.price);
        var cheapest = top.Where(x => Mathf.Approximately(x.Key.price, minPrice)).ToList();
        ShopDataItem winner = cheapest[UnityEngine.Random.Range(0, cheapest.Count)].Key;
        if (top.Count > 1)
            LogShop($"    权重并列 {top.Count} 个(max={max:F2})，低价优先取 {winner.produceNameId}(price={winner.price})");

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

    /// <summary>
    /// 礼包档位（策划的 GiftType 字段）。
    /// 优先取商店表 shop.csv 的「礼包档位」列；推荐配置表（shop_clockbox / shop_rocketbox）里没有 GiftType 列，
    /// 解析时全被默认成 3，不能作为档位依据，只作为兜底。
    /// </summary>
    private static int GetPackTier(ShopDataItem item, ShopRecommendConfig cfg = null)
    {
        if (item != null && item.giftType > 0) return item.giftType;
        if (cfg != null && cfg.giftType > 0) return cfg.giftType;
        return 0;
    }

    /// <summary>
    /// 当前推荐位本关是否允许出「新人礼」（需求 C-RECO-09）。
    /// 规则：
    ///   1. 本关任何弹窗已出过新人礼 → 不再并发第二个；
    ///   2. 优先级 灯泡 ＞ 复活 ＞ 火箭：更高优先级的弹窗本关已出新人礼时，低优先级弹窗不允许出。
    /// </summary>
    private bool CanShowNewcomerGift(RecommendSlot slot)
    {
        // 兜底：正常流程在进关时就已决出 owner（ResetNewcomerGiftForNewLevel），
        // 这里再判一次是防止有其它入口在关卡开始前就打开了道具弹窗。
        ResolveNewcomerOwner();

        if (!_newcomerOwner.HasValue)
        {
            LogShop($"CanShowNewcomerGift=false: 本关没有槽位被判定为新人礼归属槽，{slot} 不允许出");
            return false;
        }

        // 本关只有决出的那个槽能出新人礼（全局唯一，避免三个槽同时出现）
        if (_newcomerOwner.Value != slot)
        {
            LogShop($"CanShowNewcomerGift=false: 本关新人礼已归属 {_newcomerOwner.Value}，{slot} 不允许出");
            return false;
        }

        // 同一关内已经出过一次就不再出
        if (_shownNewcomer.ContainsKey(slot))
        {
            LogShop($"CanShowNewcomerGift=false: 本关 {slot} 已出过新人礼({_shownNewcomer[slot]})");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 进入关卡时调用：清空本关新人礼记录，并**提前**决出唯一的归属槽。
    /// 需求：灯泡/复活/火箭的新人礼不能同时出现，优先级 灯泡 ＞ 复活 ＞ 火箭；
    /// 同一槽内多个新人礼候选时，价格低 ＞ 价格高。
    /// </summary>
    /// <param name="stageIndex">关卡号。同一关重复进入时不重置，保留本关已出过新人礼的状态。</param>
    public void ResetNewcomerGiftForNewLevel(int stageIndex)
    {
        if (_newcomerResetStage == stageIndex && _newcomerOwnerResolved)
        {
            LogShop($"[新人礼] 同一关({stageIndex})重进，保留本关新人礼状态");
            return;
        }

        _newcomerResetStage = stageIndex;
        _shownNewcomer.Clear();
        _newcomerOwner = null;
        _newcomerOwnerResolved = false;

        // 进关即判定，而不是等玩家点开某个弹窗时"先到先得"
        ResolveNewcomerOwner();
    }

    /// <summary>决出本关新人礼归属槽（每关只判一次）。</summary>
    private void ResolveNewcomerOwner()
    {
        if (_newcomerOwnerResolved) return;
        _newcomerOwnerResolved = true;

        // 审核态走"全部礼包展示"，不参与推送规则
        if (AuditConfig.IsAuditOpen)
        {
            LogShop("[新人礼] 审核态，跳过归属槽决策");
            return;
        }

        // 优先级：灯泡 3 ＞ 复活 2 ＞ 火箭 1
        RecommendSlot[] order = { RecommendSlot.Bulb, RecommendSlot.Clock, RecommendSlot.Rocket };
        foreach (RecommendSlot slot in order)
        {
            ShopDataItem cand = FindCheapestNewcomerCandidate(slot);
            if (cand == null) continue;

            _newcomerOwner = slot;
            LogShop($"[新人礼] 本关归属槽={slot}, 礼包={cand.produceNameId}(price={cand.price}, giftType={cand.giftType})");
            return;
        }

        LogShop("[新人礼] 本关无槽位可出新人礼");
    }

    /// <summary>
    /// 取该槽当前真正可出的新人礼（同槽多个时取价格最低）。
    /// 过滤口径与 DecideWinner 的 valid 一致：未限购满、冷却系数不为 0。
    /// </summary>
    private ShopDataItem FindCheapestNewcomerCandidate(RecommendSlot slot)
    {
        // 灯泡有三个推荐位，复活/火箭各一个
        int maxFeatured = slot == RecommendSlot.Bulb ? 3 : 1;

        List<ShopDataItem> pool = new List<ShopDataItem>();
        for (int featured = 1; featured <= maxFeatured; featured++)
        {
            pool.AddRange(GetSlotCandidates(slot, featured));
        }

        return pool
            .Where(c => IsNewcomerLabel(c.labelZh))
            .Where(c => !IsLimitedOut(c) && GetCooldownCoefficient(c) > 0f)
            .OrderBy(c => c.price)
            .FirstOrDefault();
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

    // ======================= 复购策略的本地记录（bug54） =======================
    // 说明：以前这几个数据（最近购买礼包A / 支付调起次数 / 上次弹窗时间）都只存在运行时字段里，
    //      玩家杀进程或换一局就全丢 → 结算页永远凑不齐条件，"IAP 层推送礼包不触发"。

    private const string PrefRepurchaseLastPack = "shop_repurchase_last_pack";
    private const string PrefRepurchaseLastPopupTime = "shop_repurchase_last_popup_time";
    private const string PrefRepurchaseLastPopupEnd = "shop_repurchase_last_popup_end";
    private const string PrefRepurchaseLastOfferPack = "shop_repurchase_last_offer_pack";
    private const string PrefPayInvokePrefix = "shop_pay_invoke_";

    // 破冰礼包：冷却起点（倒计时结束时间）+ 已展示次数（决定冷却 7/10/13 天）
    private const string PrefBreakIceCooldownStart = "shop_breakice_cooldown_start";
    private const string PrefBreakIceCount = "shop_breakice_count";

    // 调试用：手动清除「永久去广告」后，阻止平台非消耗型补单把它重新发回来
    private const string PrefRemoveAdsRestoreBlock = "shop_removeads_restore_block";

    /// <summary>
    /// 清空「复购 / 破冰礼包」推送用到的**全部**本地记录与运行时状态（GM 重置存档时调用）。
    ///
    /// 这些数据都存在 PlayerPrefs 里（礼包A、支付调起次数、弹窗倒计时截止时间、破冰冷却与次数），
    /// 而 `GameDataManager.WipeAllGameData()` 只删 persistentDataPath 下的存档文件，删不到 PlayerPrefs，
    /// 所以重置存档后复购/破冰的倒计时、冷却、已弹次数都还会残留。
    /// </summary>
    public void ClearPromotionLocalData(string reason = "清理推广弹窗数据")
    {
        PlayerPrefs.DeleteKey(PrefRepurchaseLastPack);
        PlayerPrefs.DeleteKey(PrefRepurchaseLastPopupTime);
        PlayerPrefs.DeleteKey(PrefRepurchaseLastPopupEnd);
        PlayerPrefs.DeleteKey(PrefRepurchaseLastOfferPack);
        PlayerPrefs.DeleteKey(PrefBreakIceCooldownStart);
        PlayerPrefs.DeleteKey(PrefBreakIceCount);

        // 重置存档 = 回到干净状态：永久去广告的补单拦截也一并解除
        PlayerPrefs.DeleteKey(PrefRemoveAdsRestoreBlock);

        // 支付调起次数是「一个礼包一个 key」，按当前礼包表逐个删
        for (int i = 0; i < allShopItems.Count; i++)
        {
            ShopDataItem item = allShopItems[i];
            if (item == null || string.IsNullOrEmpty(item.produceNameId)) continue;
            PlayerPrefs.DeleteKey(PrefPayInvokePrefix + item.produceNameId);
        }

        PlayerPrefs.Save();

        // 运行时状态
        _lastPurchasedPack = null;
        if (_payInvokeCount != null) _payInvokeCount.Clear();
        _lastBreakIcePopupTime = DateTime.MinValue;
        _breakIceCountCache = -1;          // 下次读会重新从 PlayerPrefs 取（已删 = 0）

        // 结算页礼包登记 / 待弹窗数据 / 共享 1 号位决策
        ClearSettlementGift(reason);
        ClearPendingGiftPopup();
        ResetSlot1Decision(reason);

        LogShop($"[推广] 已清理复购/破冰本地数据与运行时状态: {reason}");
    }

    /// <summary>
    /// 破冰礼包本轮冷却的起点：上次弹窗的「倒计时结束时间」
    /// （非周末/节假日弹出、没有倒计时时，退化为弹窗时刻）。
    /// </summary>
    public DateTime GetBreakIceCooldownStart()
    {
        string s = PlayerPrefs.GetString(PrefBreakIceCooldownStart, "");
        DateTime t;
        return DateTime.TryParse(s, out t) ? t : DateTime.MinValue;
    }

    /// <summary>记录破冰礼包本次展示：冷却起点 + 累计次数（本地持久化）。</summary>
    public void SaveBreakIcePopupState(DateTime cooldownStart, int popupCount)
    {
        PlayerPrefs.SetString(PrefBreakIceCooldownStart, cooldownStart.ToString("yyyy-MM-dd HH:mm:ss"));
        PlayerPrefs.SetInt(PrefBreakIceCount, popupCount);
        PlayerPrefs.Save();

        int cooldownDays = BreakIceCooldownBaseDays + popupCount * BreakIceCooldownStepDays;
        LogShop($"[破冰] 记录本次展示: 次数={popupCount}, 冷却起点（倒计时结束）={cooldownStart:yyyy-MM-dd HH:mm:ss}，" +
                $"下次需间隔 {cooldownDays} 天");
    }

    /// <summary>记录「支付调起次数」（点击礼包、成功拉起系统支付但没完成购买时 +1），本地持久化。</summary>
    public void RecordPayInvoke(string produceNameId)
    {
        if (string.IsNullOrEmpty(produceNameId)) return;

        int c = GetPayInvokeCount(produceNameId) + 1;
        _payInvokeCount[produceNameId] = c;

        PlayerPrefs.SetInt(PrefPayInvokePrefix + produceNameId, c);
        PlayerPrefs.Save();

        LogShop($"[复购] 支付调起次数 {produceNameId} = {c}（已本地记录）");
    }

    public int GetPayInvokeCount(string produceNameId)
    {
        if (string.IsNullOrEmpty(produceNameId)) return 0;

        int c;
        if (_payInvokeCount.TryGetValue(produceNameId, out c)) return c;

        c = PlayerPrefs.GetInt(PrefPayInvokePrefix + produceNameId, 0);
        _payInvokeCount[produceNameId] = c;
        return c;
    }

    /// <summary>
    /// 记录「最近一次完成购买的商店礼包 A」。
    /// 需求：不含金币商品和两个去广告商品 → 只记 type==2 的礼包，且排除去广告 SKU。
    /// </summary>
    private void SaveRepurchaseLastPack(ShopDataItem item)
    {
        if (item == null) return;
        if (item.type != 2)
        {
            LogShop($"[复购] 不记录为礼包A（非礼包商品）: {item.produceNameId}, type={item.type}");
            return;
        }
        if (IsRemoveAdsProduct(item.produceNameId))
        {
            LogShop($"[复购] 不记录为礼包A（去广告商品）: {item.produceNameId}");
            return;
        }

        PlayerPrefs.SetString(PrefRepurchaseLastPack, item.produceNameId);
        PlayerPrefs.Save();
        LogShop($"[复购] 记录礼包A={item.produceNameId}(price={item.price}, giftType={item.giftType})");
    }

    /// <summary>取「最近一次完成购买的礼包 A」（优先本地记录，回退运行时记录 / 购买记录，兼容老存档）。</summary>
    public ShopDataItem GetRepurchaseBasePack()
    {
        string id = PlayerPrefs.GetString(PrefRepurchaseLastPack, "");
        ShopDataItem item = null;
        if (!string.IsNullOrEmpty(id))
            item = GetProduct(id) ?? FormAllItemsGetProduct(id);

        if (item == null && _lastPurchasedPack != null)
            item = _lastPurchasedPack;

        // 兼容升级前的老存档：那时候没有本地记录，只能从购买记录（最新在前）里找最近一笔礼包
        if (item == null)
        {
            var records = GameDataManager.Instance != null && GameDataManager.Instance.UserData != null
                ? GameDataManager.Instance.UserData.purchaseRecords
                : null;

            if (records != null)
            {
                for (int i = 0; i < records.Count; i++)
                {
                    PurchaseRecord r = records[i];
                    if (r == null || string.IsNullOrEmpty(r.produceNameId)) continue;
                    if (IsRemoveAdsProduct(r.produceNameId)) continue;

                    ShopDataItem p = GetProduct(r.produceNameId) ?? FormAllItemsGetProduct(r.produceNameId);
                    if (p == null || p.type != 2) continue;

                    item = p;
                    LogShop($"[复购] 本地无记录，从购买记录回推礼包A={p.produceNameId}（{r.buyTime}）");
                    break;
                }
            }
        }

        if (item == null)
            LogShop($"[复购] 取礼包A失败：本地记录='{id}'，运行时记录={(_lastPurchasedPack != null ? _lastPurchasedPack.produceNameId : "null")}，购买记录也未命中");
        else
            LogShop($"[复购] 礼包A={item.produceNameId}(price={item.price}, giftType={item.giftType})，本地记录='{id}'");

        return item;
    }

    /// <summary>上次复购弹窗时间（本地持久化，仅日志/排查用）。</summary>
    public DateTime GetRepurchaseLastPopupTime()
    {
        string s = PlayerPrefs.GetString(PrefRepurchaseLastPopupTime, "");
        DateTime t;
        return DateTime.TryParse(s, out t) ? t : DateTime.MinValue;
    }

    /// <summary>记录本次复购弹窗时间（本地持久化）。</summary>
    public void SaveRepurchaseLastPopupTime(DateTime time)
    {
        PlayerPrefs.SetString(PrefRepurchaseLastPopupTime, time.ToString("yyyy-MM-dd HH:mm:ss"));
        PlayerPrefs.Save();
        LogShop($"[复购] 记录弹窗时间 {time:yyyy-MM-dd HH:mm:ss}");
    }

    // ---- 冷却口径：从「弹窗倒计时结束」开始算 7 天（策划口径） ----
    // 倒计时期间（Now < 上次倒计时结束）属于本次限时展示期内 → 继续展示；
    // 倒计时结束后再过 7 天，才允许出新的复购弹窗。

    /// <summary>上次复购弹窗的「倒计时结束时间」（本地持久化）。</summary>
    public DateTime GetRepurchaseLastPopupEndTime()
    {
        string s = PlayerPrefs.GetString(PrefRepurchaseLastPopupEnd, "");
        DateTime t;
        return DateTime.TryParse(s, out t) ? t : DateTime.MinValue;
    }

    /// <summary>记录本次复购弹窗的倒计时结束时间 + 本次展示的礼包（本地持久化）。</summary>
    public void SaveRepurchaseLastPopupEndTime(DateTime endTime, string packProduceNameId)
    {
        PlayerPrefs.SetString(PrefRepurchaseLastPopupEnd, endTime.ToString("yyyy-MM-dd HH:mm:ss"));
        PlayerPrefs.SetString(PrefRepurchaseLastOfferPack, packProduceNameId ?? "");
        PlayerPrefs.Save();
        LogShop($"[复购] 记录本次展示: 礼包={packProduceNameId}, 倒计时结束={endTime:yyyy-MM-dd HH:mm:ss}；" +
                $"该时间点之后需间隔 {RepurchaseCooldownDays} 天才有新弹窗");
    }

    /// <summary>上次展示的礼包（倒计时未结束时继续展示同一款，保持限时与加赠一致）。</summary>
    public ShopDataItem GetRepurchaseLastOfferPack()
    {
        string id = PlayerPrefs.GetString(PrefRepurchaseLastOfferPack, "");
        if (string.IsNullOrEmpty(id)) return null;
        return GetProduct(id) ?? FormAllItemsGetProduct(id);
    }

    /// <summary>
    /// 只更新「上次展示的礼包」，不动倒计时/冷却。
    /// 用途：本活动期已弹过复购弹窗后，上次那款售罄需要换个替补礼包给结算页按钮用（但不再弹窗）。
    /// </summary>
    public void SaveRepurchaseOfferPack(string packProduceNameId)
    {
        PlayerPrefs.SetString(PrefRepurchaseLastOfferPack, packProduceNameId ?? "");
        PlayerPrefs.Save();
        LogShop($"[复购] 更新本次展示的礼包(不弹窗) = {packProduceNameId}");
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
    /// 统一发放礼包内容（shop.csv 的 productContent）。购买 / 补发 / 推送礼包都走这里，
    /// 避免各处各写一份 switch 导致「某个入口少了一种类型 → 奖励不到账」。
    ///
    /// 支持的资源类型（LimitRewordType）：
    ///   0  Coins          金币
    ///   1  Butterfly      蝴蝶道具
    ///   2  Tipstool       提示（放大镜）
    ///   3  AutoComplete   火箭（自动完成本关）
    ///   6  RemoveAds      永久去广告
    ///   7  RemoveDayAds   N 天限时去广告（count = 天数）
    ///   8  ReStartTool    复活道具
    ///   9  InfiniteEnergy N 小时无限体力（产品名自带 Energy 时交给 HandleUnlimitedEnergyPurchase，避免重复叠加）
    ///   1002 ReviveToolType 旧的复活类型，按提示灯兜底
    ///
    /// 目前礼包表没用到 4/5(限时双倍)、10(重置)、11(蛹)、12(禅意分)、13(金叶)、14(体力)、15(头像)、16~18(月卡)，
    /// 命中会打 warning，方便以后补。
    /// </summary>
    /// <param name="item">礼包配置</param>
    /// <param name="reason">资源变动原因（日志 / 埋点）</param>
    /// <param name="logPrefix">日志前缀</param>
    /// <param name="skipNonPositive">count ≤ 0 的项跳过（补发路径口径）</param>
    /// <returns>内容里是否发过 6/7（调用方据此决定要不要再按商品名兜底发去广告）</returns>
    private bool GrantProductContent(ShopDataItem item, string reason,
                                     string logPrefix = "  发放奖励", bool skipNonPositive = false)
    {
        bool removeAdsGranted = false;
        if (item == null || item.productContent == null) return false;

        foreach (var dataitem in item.productContent)
        {
            if (dataitem == null || dataitem.Count < 2)
            {
                LogShop($"{logPrefix} 跳过无效 productContent 项 (Count<2)");
                continue;
            }

            int count = GetInt(dataitem[1]);
            int type = GetInt(dataitem[0]);
            if (skipNonPositive && count <= 0) continue;

            LogShop($"{logPrefix}: type={type}({(LimitRewordType)type}), count={count}, item={item.produceNameId}");

            switch (type)
            {
                case (int)LimitRewordType.Coins:
                    GameDataManager.Instance.UserData.UpdateGold(count, false, false, reason);
                    break;

                // 道具类：蝴蝶 / 提示 / 火箭 / 复活
                case (int)LimitRewordType.Butterfly:
                case (int)LimitRewordType.Tipstool:
                case (int)LimitRewordType.AutoComplete:
                case (int)LimitRewordType.ReStartTool:
                    GameDataManager.Instance.UserData.UpdateTool((LimitRewordType)type, count, reason);
                    break;

                case (int)LimitRewordType.RemoveAds:
                    // 内容里的 6;N = 永久去广告（N 无意义，按永久发）
                    GrantRemoveAdsByContent((int)LimitRewordType.RemoveAds, 0, item);
                    removeAdsGranted = true;
                    break;

                case (int)LimitRewordType.RemoveDayAds:
                    // 内容里的 7;N = N 天限时去广告
                    GrantRemoveAdsByContent((int)LimitRewordType.RemoveDayAds, count * 24, item);
                    removeAdsGranted = true;
                    break;

                case (int)LimitRewordType.InfiniteEnergy:
                    // 内容里的 9;N = N 小时无限体力
                    if (!IsUnlimitedEnergyProduct(item.produceNameId))
                        GrantUnlimitedEnergyHours(count, item);
                    break;

                case ReviveToolType:
                    GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, count, reason);
                    Debug.LogWarning("[ShopManager] 旧复活类型 ReviveToolType=1002 按提示灯发放，请与道具系统确认");
                    break;

                default:
                    LogShop($"{logPrefix} 未处理的商品类型: type={type}({(LimitRewordType)type}), count={count}（奖励未发放）");
                    break;
            }
        }

        return removeAdsGranted;
    }

    public void HandleGiftTablePurchaseRewards(ShopDataItem shopDataItem, ProductItem productItem)
    {
        if (shopDataItem == null)
        {
            LogShop("HandleGiftTablePurchaseRewards 跳过: shopDataItem 为空");
            return;
        }

        LogShop($"HandleGiftTablePurchaseRewards 开始: {shopDataItem.produceNameId}, " +
                $"type={shopDataItem.type}, giftType={shopDataItem.giftType}, price={shopDataItem.price}");

        // 内容统一发放（0/1/2/3/6/7/8/9 全类型）
        bool removeAdsGrantedByContent = GrantProductContent(shopDataItem, shopDataItem.name + "礼包购买");

        if (IsRemoveAdsProduct(shopDataItem.produceNameId) && !removeAdsGrantedByContent)
        {
            // 商品本身就是去广告 SKU（RemoveADS / RemoveAdsWeek）：内容里没写 6/7 时按商品名兜底
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

    // ======================= 补发 / 恢复购买（bug49 / bug50 / bug51） =======================

    /// <summary>
    /// 补发（补单 / 恢复购买）统一发货入口。
    ///
    /// 调用方：平台层在「启动补单」「已购查询」「恢复购买」里查到「已支付、但本地还没发过奖」的订单时进来
    /// （鸿蒙：Shop_harmony.GrantRewardIfNeeded → 这里）。
    ///
    /// 与正常购买的区别：不弹 AwardScreen、不报 PurchaseFinished 埋点（这不是一次新的转化）。
    /// 除此之外【必须完全一致】：
    ///   - 以前补发只发金币 / 蝴蝶 / 提示 / 火箭，礼包里的「无限体力(9)」「限时去广告(7)」「复活道具(8)」全丢（bug49）；
    ///   - 以前补发不写购买记录，玩家在「购买记录」里看不到这笔（bug50）。
    ///
    /// 幂等：同一 orderId 已经有购买记录 → 直接跳过（防止重复发货、重复累加支付统计）。
    /// </summary>
    /// <param name="item">平台回调的订单信息（order_id 一定要带上，用于幂等）</param>
    /// <param name="source">日志用来源标记（启动补单 / 恢复购买）</param>
    /// <returns>true=本次真的发了货；false=跳过（找不到商品 / 已发过）</returns>
    public bool DeliverRestoredPurchase(ProductItem item, string source)
    {
        if (item == null) return false;

        string productId = !string.IsNullOrEmpty(item.ProductId) ? item.ProductId : item.ItemName;
        ShopDataItem shopDataItem = GetProduct(productId) ?? FormAllItemsGetProduct(productId);

        if (shopDataItem == null)
        {
            LogShop($"[补发] 未找到商品配置: {productId}（source={source}），跳过");
            return false;
        }

        // ★ 永久去广告是平台侧的非消耗型商品：清掉本地状态后，每次启动的已购查询都会把它查回来。
        //   这里按「手动清除时置的拦截标记」拦掉，避免"清完 → 重启 → 又被补单恢复"。
        //   玩家重新购买 / GM 重置存档 会解除该标记（见 GrantRemoveAdsByContent / ClearPromotionLocalData）。
        if (IsPermanentRemoveAdsProduct(shopDataItem) && IsPermanentRemoveAdsRestoreBlocked())
        {
            LogShop($"[补发] 永久去广告补单已拦截（本地状态被手动清除过）: " +
                    $"order={item.order_id}, source={source}；如需恢复请重新购买或重置存档");
            return false;
        }

        if (HasPurchaseRecordForOrder(item.order_id))
        {
            LogShop($"[补发] 订单 {item.order_id} 已有购买记录，视为已发货，跳过（source={source}）");
            return false;
        }

        LogShop($"[补发] 开始发货: {shopDataItem.produceNameId}(id={shopDataItem.id}), " +
                $"order={item.order_id}, price={item.LocalizedPrice}, currency={item.IsoCurrencyCode}, source={source}");

        if (shopDataItem.productContent != null)
        {
            foreach (var dataitem in shopDataItem.productContent)
            {
                if (dataitem == null || dataitem.Count < 2) continue;

                int type = GetInt(dataitem[0]);
                int count = GetInt(dataitem[1]);
                if (count <= 0) continue;

                LogShop($"[补发] 发放: type={type}, count={count}");
                switch (type)
                {
                    case (int)LimitRewordType.Coins:
                        GameDataManager.Instance.UserData.UpdateGold(count, false, false, $"{source}补发 {item.ItemName}");
                        break;
                    case (int)LimitRewordType.Butterfly:
                        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Butterfly, count, $"{source}补发 {item.ItemName}");
                        break;
                    case (int)LimitRewordType.Tipstool:
                        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, count, $"{source}补发 {item.ItemName}");
                        break;
                    case (int)LimitRewordType.AutoComplete:
                        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.AutoComplete, count, $"{source}补发 {item.ItemName}");
                        break;
                    case (int)LimitRewordType.ReStartTool:
                        // 复活道具（toolId=105）
                        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.ReStartTool, count, $"{source}补发 {item.ItemName}");
                        break;
                    case (int)LimitRewordType.InfiniteEnergy:
                        // 内容里的 9;N = N 小时无限体力
                        GrantUnlimitedEnergyHours(count, shopDataItem);
                        break;
                    case (int)LimitRewordType.RemoveAds:
                        GrantRemoveAdsByContent((int)LimitRewordType.RemoveAds, 0, shopDataItem);
                        break;
                    case (int)LimitRewordType.RemoveDayAds:
                        // 内容里的 7;N = N 天限时去广告
                        GrantRemoveAdsByContent((int)LimitRewordType.RemoveDayAds, count * 24, shopDataItem);
                        break;
                    default:
                        LogShop($"[补发] 未处理的商品类型: type={type}, count={count}");
                        break;
                }
            }
        }

        // ---------- 支付统计（补发说明这次支付第一次成功落盘，按正常购买记账） ----------
        var userData = GameDataManager.Instance.UserData;
        bool firstPay = userData.TotalPayTimes == 0;
        if (firstPay) userData.firstPayTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        userData.lastPayTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        userData.LastPayTimeTicks = DateTime.Now.Ticks;
        userData.TotalPayTimes++;
        userData.TotalRevenue += item.LocalizedPrice;
        if (item.LocalizedPrice > userData.MaxSinglePayAmount)
            userData.MaxSinglePayAmount = item.LocalizedPrice;

        // ---------- 购买记录（bug50） ----------
        AddPurchaseRecordForRestore(shopDataItem, item, source);

        // ---------- 限购计数 / 付费倾向 / 推荐位锁定 ----------
        OnPackPurchased(shopDataItem);

        // ---------- 立刻落盘：下次启动才不会又补一遍 / 又丢一遍（bug51） ----------
        GameDataManager.Instance.CommitGameData();

        LogShop($"[补发] 发货完成并已落盘: {shopDataItem.produceNameId}, order={item.order_id}");
        return true;
    }

    /// <summary>该订单号是否已经写过购买记录（补发幂等判断）。空订单号一律视为没有记录。</summary>
    public bool HasPurchaseRecordForOrder(string orderId)
    {
        if (string.IsNullOrEmpty(orderId)) return false;

        var records = GameDataManager.Instance != null && GameDataManager.Instance.UserData != null
            ? GameDataManager.Instance.UserData.purchaseRecords
            : null;
        if (records == null) return false;

        for (int i = 0; i < records.Count; i++)
        {
            if (records[i] != null && records[i].orderId == orderId) return true;
        }
        return false;
    }

    /// <summary>补发时写一条购买记录（结构 / 快照规则与 GiftTable.RecordPurchase、ShopItemBase.RecordPurchase 一致）。</summary>
    private void AddPurchaseRecordForRestore(ShopDataItem shopDataItem, ProductItem item, string source)
    {
        try
        {
            float price = shopDataItem.price;
            if (item != null && item.LocalizedPrice > 0) price = item.LocalizedPrice;

            string nameText = MultilingualManager.Instance?.GetString(shopDataItem.name)
                              ?? shopDataItem.name
                              ?? shopDataItem.produceNameId
                              ?? "";

            var record = new PurchaseRecord
            {
                orderId = string.IsNullOrEmpty(item?.order_id)
                    ? Guid.NewGuid().ToString("N")
                    : item.order_id,
                produceNameId = shopDataItem.produceNameId ?? "",
                nameKey = shopDataItem.name ?? "",
                nameText = nameText,
                price = price,
                originalPrice = shopDataItem.originalPrice,
                currency = item?.IsoCurrencyCode ?? "",
                showIcon = shopDataItem.showIcon ?? "",
                limitRewordType = shopDataItem.type,
                buyTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                productContent = shopDataItem.DeepCopy()?.productContent ?? new List<List<string>>(),
            };

            GameDataManager.Instance?.UserData?.AddPurchaseRecord(record);

            LogShop($"[补发] 购买记录已写入（{source}）: name={record.nameText}, order={record.orderId}, " +
                    $"price={record.price}, contentCount={record.productContent.Count}");
        }
        catch (Exception ex)
        {
            LogShop($"[补发] 购买记录写入异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 按礼包内容发放去广告（6=永久 / 7=限时 N 天）。
    /// 与 ShopItemBase.HandleRemoveAdsPurchaseInternal 同一套数据结构（UserData.limitShopItems + adstype），
    /// 只是把「读商品名猜类型」改成「读内容类型」，这样补发也能正确发限时去广告。
    /// </summary>
    private void GrantRemoveAdsByContent(int adstype, int hours, ShopDataItem source)
    {
        bool permanent = adstype == (int)LimitRewordType.RemoveAds;
        if (!permanent && hours <= 0) hours = 24;   // 内容里没写时长时兜底 1 天

        // ★ 玩家真的又买到了永久去广告（或礼包里含 6）→ 之前"清除后拦截补单"的标记失效，
        //   否则重装/换机时的正常补单会被误拦。
        if (permanent) SetPermanentRemoveAdsRestoreBlock(false, $"永久去广告重新发放（{source?.produceNameId}）");

        LogShop($"[补发] 去广告发放: adstype={adstype}, permanent={permanent}, hours={hours}, item={source?.produceNameId}");

        ShopLimitData exist = GameDataManager.Instance.UserData.limitShopItems
            .Find(x => x.adstype == adstype);

        if (exist != null && !exist.isoverdate)
        {
            if (permanent)
            {
                exist.isget = true;
                exist.isopen = false;
                exist.isoverdate = false;
                exist.endtime = "";
                exist.gettime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }
            else
            {
                DateTime baseEnd = DateTime.Now;
                DateTime oldEnd = exist.GetEndTime();
                if (oldEnd != DateTime.MinValue && oldEnd > DateTime.Now) baseEnd = oldEnd;

                DateTime newEnd = baseEnd.AddHours(hours);
                exist.endtime = newEnd.ToString("yyyy-MM-dd HH:mm:ss");
                exist.isget = true;
                exist.isopen = false;
                exist.isoverdate = false;
                exist.gettime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                LogShop($"[补发] 限时去广告续期: 原结束={oldEnd:yyyy-MM-dd HH:mm:ss}, 新结束={newEnd:yyyy-MM-dd HH:mm:ss}");
            }
        }
        else
        {
            DateTime newEnd = permanent ? DateTime.MinValue : DateTime.Now.AddHours(hours);
            GameDataManager.Instance.UserData.limitShopItems.Add(new ShopLimitData
            {
                id = source != null ? source.id : 0,
                nameid = source != null ? source.produceNameId : "",
                endtime = permanent ? "" : newEnd.ToString("yyyy-MM-dd HH:mm:ss"),
                isopen = false,
                gettime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                adstype = adstype,
                isget = true,
                isoverdate = false,
            });
            LogShop($"[补发] 新增去广告记录: adstype={adstype}, permanent={permanent}, " +
                    $"endtime={(permanent ? "永久" : newEnd.ToString("yyyy-MM-dd HH:mm:ss"))}");
        }

        OnRemoveAdsApplied(adstype);
        if (AdRuleManager.Instance != null) AdRuleManager.Instance.HideBannerAd();
        UpdateAdsBtnUI?.Invoke(permanent ? null : DateTime.Now.ToString(), true);
        RefreshLimitData();
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

        // ★ bug54：复购弹窗的「加量不加价」加赠金币只对这一单生效（不污染商店里的商品数据）
        int giftBonusGold = ConsumePendingGiftBonus(shopDataItem.produceNameId);

        Game.self.Shop.CurrentShopDataItem = shopDataItem.DeepCopy();
        if (giftBonusGold > 0)
        {
            // 内购成功弹窗里也要看到这份加赠（并进已有金币条目，不新增奖励项）
            MergeBonusGoldIntoContent(Game.self.Shop.CurrentShopDataItem, giftBonusGold);
        }
        SystemManager.Instance.ShowPanel(PanelType.AwardScreen);

        // ★ 推送礼包（破冰 / 复购弹窗、结算页礼包按钮、广告失败页单道具）都走这里。
        //   以前这份 switch 缺 8 复活道具 / 9 无限体力，7 天去广告也只刷了 UI 没写存档 → 礼包奖励没到账；
        //   现在统一走 GrantProductContent（与礼包表购买、补发同一套逻辑）。
        bool removeAdsGrantedByContent = GrantProductContent(shopDataItem, "商店购买" + item.ItemName);

        // 商品本身就是去广告 SKU（RemoveADS / RemoveAdsWeek）：内容里没写 6/7 时才按商品名兜底
        if (IsRemoveAdsProduct(shopDataItem.produceNameId) && !removeAdsGrantedByContent)
        {
            LogShop($"非消耗型: 去广告 {shopDataItem.produceNameId}");
            HandleRemoveAdsPurchase(shopDataItem);
        }
        if (IsUnlimitedEnergyProduct(shopDataItem.produceNameId))
        {
            LogShop($"非消耗型: 无限体力 {shopDataItem.produceNameId}");
            HandleUnlimitedEnergyPurchase(shopDataItem);
        }

        // ★ bug54：复购礼包加赠金币（弹窗里卖出才有的那份）
        if (giftBonusGold > 0)
        {
            GameDataManager.Instance.UserData.UpdateGold(giftBonusGold, false, false, "复购礼包加量不加价");
            LogShop($"[复购] 已发放加赠金币 +{giftBonusGold}（{shopDataItem.produceNameId}）");
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

        // ★ 复购礼包（复购弹窗 / 结算页「限时折扣」按钮）成交后不再弹「xxx 购买成功！」toast：
        //   AwardScreen 已经是成功反馈，这里再弹一次是重复提示。
        if (pendingGiftPopupMode != GiftPopupMode.Repurchase)
            MessageSystem.Instance.ShowTip($"{shopDataItem.name} 购买成功！");
        MessageSystem.Instance.HideLoadingAnimation();

        // ★ bug50/bug51：立刻落盘，避免「买完杀进程」导致奖励丢失且平台已标记发货不再补发
        if (GameDataManager.Instance != null) GameDataManager.Instance.CommitGameData();

        LogShop($"========== OnPurchaseSuccess 结束: {shopDataItem.produceNameId} ==========");
    }

    public const int ReviveToolType = 1002;

    private bool IsUnlimitedEnergyProduct(string produceNameId)
    {
        return produceNameId != null && produceNameId.Contains("Energy");
    }

    /// <summary>
    /// 直接发放 N 小时无限体力（供礼包内容里的 9;N 使用，与产品级的 HandleUnlimitedEnergyPurchase 区分）。
    /// </summary>
    private void GrantUnlimitedEnergyHours(int hours, ShopDataItem item)
    {
        if (hours <= 0) return;

        var userData = GameDataManager.Instance.UserData;

        double beforeRemain = userData.GetUnlimitedEnergyRemainSeconds();
        DateTime newEnd = userData.AddUnlimitedEnergy(hours);
        double afterRemain = userData.GetUnlimitedEnergyRemainSeconds();

        LogShop($"礼包发放无限体力: {item?.produceNameId}, +{hours}h, " +
                $"原剩余={TimeSpan.FromSeconds(beforeRemain):hh\\:mm\\:ss}, " +
                $"新剩余={TimeSpan.FromSeconds(afterRemain):hh\\:mm\\:ss}, " +
                $"新结束={newEnd:yyyy-MM-dd HH:mm:ss}");

        EventDispatcher.instance.TriggerUpdateLayerCoin(false, true);
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

    /// <summary>
    /// 礼包购买后的统一收口：付费倾向 / 探价 / 档位累计 / 购买次数 / 推荐位锁定 + 购买后权重快照。
    /// 调用方：GiftTable（复活、道具弹窗）、ShopManager.OnPurchaseSuccess、ShopItemBase（商店界面 1/2 号位等）。
    /// 注意：这里会 buyCount++，调用方不要再自行累加。
    /// </summary>
    public void OnPackPurchased(ShopDataItem shopDataItem)
    {
        LogShop($"OnPackPurchased 开始: {shopDataItem.produceNameId}, type={shopDataItem.type}, giftType={shopDataItem.giftType}, price={shopDataItem.price}");

        if (shopDataItem.type == 2)
        {
            _lastPurchasedPack = shopDataItem;
            LogShop($"  记录上次购买礼包: {_lastPurchasedPack.produceNameId}");

            // ★ bug54：复购策略要以「最近一次完成购买的礼包A」为基准，必须跨进程保存
            SaveRepurchaseLastPack(shopDataItem);
        }

        // 结算页登记的礼包已成交 → 清掉登记（结算页按钮随之收起，避免重复弹/重复买）
        if (_settlementGiftItem != null && shopDataItem.produceNameId == _settlementGiftItem.produceNameId)
            ClearSettlementGift($"结算页礼包已购买: {shopDataItem.produceNameId}");

        ShopLimitData limit = GetOrCreateLimitData(shopDataItem);

        limit.SetLastBuyTime(DateTime.Now);

        int a = GetPackTier(shopDataItem, GetCfg(shopDataItem.produceNameId));
        if (a > 0)
        {
            // ★ 付费倾向积分规则（策划文档）：
            //   与购买礼包【同档位】的礼包 +2；【档位 a+1】的礼包 +1；【档位 a+2】的礼包 +0.5。
            //   档位统一取 shop.csv 的「礼包档位」列（GetPackTier），低于 a 或高于 a+2 的档位都不加分。
            //   （bug32：原来写成 tier >= a+2，会把 a+3、a+4 也 +0.5，与需求不符）
            int sameTierCount = 0;
            int nextTierCount = 0;
            int aboveTierCount = 0;

            foreach (var cfg in _recommendConfigs)
            {
                ShopDataItem it = allShopItems.FirstOrDefault(x => x.produceNameId == cfg.Key);
                if (it == null) continue;

                ShopLimitData ld = GetOrCreateLimitData(it);
                int tier = GetPackTier(it, cfg.Value);

                if (tier == a)
                {
                    ld.payTendencyScore += 2f;
                    sameTierCount++;
                }
                else if (tier == a + 1)
                {
                    ld.payTendencyScore += 1f;
                    nextTierCount++;
                }
                else if (tier == a + 2)
                {
                    ld.payTendencyScore += 0.5f;
                    aboveTierCount++;
                }
            }

            LogShop($"  付费倾向积分更新（基准档 a={a}）：同档位 +2 → {sameTierCount} 个，" +
                    $"a+1 +1 → {nextTierCount} 个，a+2 +0.5 → {aboveTierCount} 个");
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
                    ShopDataItem it = allShopItems.FirstOrDefault(x => x.produceNameId == cfg.Key);
                    if (it == null) continue;

                    if (GetPackTier(it, cfg.Value) == a + 1)
                    {
                        ShopLimitData ld = GetOrCreateLimitData(it);
                        ld.exploreScore += 6f;
                        LogShop($"    探价加分: 下一档 {it.produceNameId} +6");
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

        // ★ bug52：买过礼包后积分/限购都变了，1 号位共享决策要作废（下个入口重新算，两边仍然一致）
        ResetSlot1Decision($"购买 {shopDataItem.produceNameId} 后");

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

        // ★ 购买后打印权重快照：当前礼包 + 同档位所有礼包（此时各项积分都已更新）
        if (shopDataItem.type == 2 || shopDataItem.giftType > 0)
        {
            LogWeightSnapshotAfterPurchase(shopDataItem);
        }
    }

    // ======================= ★ 购买后权重快照日志 =======================

    /// <summary>
    /// 购买礼包后打印权重快照：**当前礼包** + **同档位（礼包档位 giftType）的所有礼包**。
    /// 数据取的是购买后的最新状态（付费倾向 / 探价 / 转化失败分 / 限购次数都已刷新）。
    /// </summary>
    private void LogWeightSnapshotAfterPurchase(ShopDataItem purchased)
    {
        if (purchased == null) return;

        ShopRecommendConfig purchasedCfg = GetCfg(purchased.produceNameId);
        int tier = GetPackTier(purchased, purchasedCfg);
        RecommendSlot? purchasedSlot = GetSlotOfPack(purchased);

        LogShop("========== 【购买后权重快照】 ==========");
        LogShop($"刚购买的礼包：{purchased.produceNameId}" +
                $"（档位={tier}，推荐位={SlotNameCn(purchasedSlot)}，featured={(purchasedCfg != null ? purchasedCfg.featured : 0)}，价格={purchased.price:0.##}）");

        LogPackWeightLine("当前礼包", purchased, purchasedSlot);

        LogShop($"---------- 同档位（档位={tier}）其它礼包 ----------");
        int sameTierCount = 0;
        foreach (var kv in _recommendConfigs)
        {
            ShopRecommendConfig cfg = kv.Value;
            if (cfg == null) continue;

            ShopDataItem other = allShopItems.FirstOrDefault(x => x != null && x.produceNameId == kv.Key);
            if (other == null) continue;
            if (other.produceNameId == purchased.produceNameId) continue;
            if (GetPackTier(other, cfg) != tier) continue;

            sameTierCount++;
            // 每个礼包按它自己所属推荐位计算（explore/fail 只对灯泡、动态商店生效）
            LogPackWeightLine("同档位", other, GetSlotOfPack(other) ?? purchasedSlot ?? RecommendSlot.DynamicStore);
        }

        if (sameTierCount == 0)
            LogShop("（没有其它同档位礼包）");

        LogShop("======================================");
    }

    /// <summary>打印单个礼包的权重明细（中文标注）。</summary>
    private void LogPackWeightLine(string label, ShopDataItem item, RecommendSlot? slot)
    {
        if (item == null) return;

        ShopRecommendConfig cfg = GetCfg(item.produceNameId);
        ShopLimitData limit = GetLimitData(item);
        int buyCount = limit != null ? limit.buyCount : 0;

        string basic = $"[{label}] {item.produceNameId}" +
                       $"（档位={GetPackTier(item, cfg)}，" +
                       $"推荐位={SlotNameCn(slot)}，" +
                       $"限购={(item.limited > 0 ? item.limited.ToString() : "不限")}，" +
                       $"已购={buyCount}）";

        if (cfg == null)
        {
            LogShop($"{basic} → 无推荐配置（shop_recommend 里没有该礼包），不参与权重计算");
            return;
        }

        DecisionWeightInfo info = EvaluateDecisionWeight(item, cfg, slot ?? RecommendSlot.DynamicStore);
        if (!info.valid)
        {
            LogShop($"{basic} → 不参与推荐：{info.skipReason}");
            return;
        }

        LogShop($"{basic} → 基础权重={info.baseWeight:F1}，付费倾向={info.payTendency:F1}，" +
                $"探价={info.exploreScore:F1}，转化失败分={info.failScore:F1}，" +
                $"礼包分={info.packWeight:F2}(封顶{info.cap:F0})；" +
                $"节日周末={info.specialWeight:F1}，高峰={info.peakWeight:F1}，流失回归={info.returnWeight:F1}；" +
                $"冷却={info.cooldown:F2}，推送指数={info.pushIndex:F2} → 最终权重={info.decision:F2}");
    }

    /// <summary>推荐位中文名（日志用）。</summary>
    private static string SlotNameCn(RecommendSlot? slot)
    {
        if (!slot.HasValue) return "未归属";

        switch (slot.Value)
        {
            case RecommendSlot.Bulb: return "灯泡";
            case RecommendSlot.Rocket: return "火箭";
            case RecommendSlot.Clock: return "复活";
            case RecommendSlot.DynamicStore: return "动态商店";
            case RecommendSlot.FixedStore: return "固定商店";
            default: return slot.Value.ToString();
        }
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

        if (Game.self == null || Game.self.Shop == null)
        {
            LogShop("RestorePurchases: 商店未初始化，跳过");
            MessageSystem.Instance?.ShowTip("商店暂不可用，请稍后再试");
            return;
        }

        MessageSystem.Instance?.ShowLoadingAnimation();

        // 平台回调可能永远不来（安卓实现是空的），用超时兜底，别把 loading 卡死
        _restoreCallbackInvoked = false;
        StartCoroutine(CoRestoreTimeout(4f));

        Game.self.Shop.Restore((success, items) =>
        {
            _restoreCallbackInvoked = true;
            MessageSystem.Instance?.HideLoadingAnimation();

            int count = items != null ? items.Length : 0;
            LogShop($"RestorePurchases 回调: success={success}, 恢复商品数={count}" +
                    (count > 0
                        ? $", 商品=[{string.Join(",", items.Select(x => x != null ? x.ProductId : "null"))}]"
                        : ""));

            if (count > 0)
            {
                MessageSystem.Instance?.ShowTip($"已恢复 {count} 笔内购");
                EventDispatcher.instance?.TriggerChangeGoldUI(0, true);
                RefreshLimitData();
            }
            else if (success)
            {
                MessageSystem.Instance?.ShowTip("没有可恢复的内购");
            }
            else
            {
                MessageSystem.Instance?.ShowTip("没有可恢复的内购");
            }
        });
    }

    /// <summary>恢复购买回调是否已经回来（超时兜底用）</summary>
    private bool _restoreCallbackInvoked;

    private IEnumerator CoRestoreTimeout(float seconds)
    {
        float waited = 0f;
        while (waited < seconds && !_restoreCallbackInvoked)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (_restoreCallbackInvoked) yield break;

        LogShop($"RestorePurchases: {seconds}s 内没有平台回调，按「没有可恢复的内购」处理");
        MessageSystem.Instance?.HideLoadingAnimation();
        MessageSystem.Instance?.ShowTip("没有可恢复的内购");
    }

    /// <summary>
    /// 购买记录（本地 UserData.purchaseRecords，GiftTable 购买成功时写入，最新在前）。
    /// 返回可直接展示的字符串：购买时间 | 礼包名称 | 价格币种。
    /// </summary>
    public List<string> GetPurchaseRecords()
    {
        var result = new List<string>();
        var records = GetPurchaseRecordList();
        foreach (var r in records)
        {
            string name = string.IsNullOrEmpty(r.nameText) ? r.nameKey : r.nameText;
            if (string.IsNullOrEmpty(name)) name = r.produceNameId;
            result.Add($"{r.buyTime} | {name} | {r.price:0.##}{r.currency}");
        }
        LogShop($"GetPurchaseRecords: 共 {result.Count} 条本地记录");
        return result;
    }

    /// <summary>原始购买记录列表（供购买记录弹窗/日志使用），无数据时返回空列表。</summary>
    public List<PurchaseRecord> GetPurchaseRecordList()
    {
        var records = GameDataManager.Instance?.UserData?.purchaseRecords;
        if (records == null || records.Count == 0)
            return new List<PurchaseRecord>();
        return records;
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