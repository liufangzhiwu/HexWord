using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 商店礼包物品（type 1 / 2）。
/// - type=2：普通礼包（金币由 shopCountText 展示，GiftItem 跳过金币）；
/// - type=1：广告礼包（移除广告 + 金币资源，金币由 GiftItem 展示）。
///
/// 继承 ShopItemBase（无广告），override：
///  - SetShopIcon：图标 = showIcon + "_Icon"；
///  - HandleSpecialTypeItems：空实现，阻止基类把 type=1 按"单物品道具"展示；
///  - HandleProductContentDisplay：礼包名 + 礼包底图 + （type=2）金币数量；
///      ★ 礼包底图（dibg）：特惠礼包B 且【已购买永久去广告】→ shopGiftTeB2，其它情况 → showIcon；
///  - HandleMultiProductContent / InitGiftItems：创建多个 GiftItem
///      ★ 实际需要创建的数量 > 4 时用 giftHengItemPrefab（横向布局，父节点 giftsHengParent），否则用 giftItemPrefab（父节点 giftsParent）；
///      ★ 广告 / 体力类资源（RemoveAds / RemoveDayAds / InfiniteEnergy）优先挂到 itemEnergyList（若配置）；
///      ★ itemEnergyList 内的 Prefab 独立判断：
///         - 只有 1 个子对象 → giftItemPrefab（单个用横向 Prefab 会变形/留白）；
///         - 全部是体力资源（InfiniteEnergy）→ giftItemPrefab（普通 GiftItem）；
///         - 含广告资源或其他资源且多于 1 项 → giftHengItemPrefab（横向 GiftHengItem）；
///         - 目标 Prefab 不可用时回退到本次传入的 prefab；
///      ★ itemEnergyList 的排版微调（对象池复用，每次刷新都重算）：
///         - GridLayoutGroup.padding.left：特惠礼包B → -70，其它礼包 → 0；
///         - 只有 1 个 GiftItem 时：图标 anchoredPosition.y = 40、宽 = 186.3、高 = 230；
///      ★ itemHengList（ItemHengList）的排版微调（对象池复用，每次刷新都重算）：
///         - 特惠礼包B 且【未购买永久去广告】→ GridLayoutGroup.padding.left = -60、spacing.x = 30；
///         - 其它情况（其它礼包 / 已购买永久去广告）→ 复位为 Prefab 默认值（left = -5、spacing.x = 10）；
///      ★ InitGiftItems 开头清理 giftsParent / giftsHengParent / itemEnergyList 下的旧 GiftItem 子物体（对象池复用必须清理）；
///      ★ type=2 跳过金币；type=1 金币也走 GiftItem；
///  - IsContentSkippedOnPurchase：已拥有永久去广告 → 购买时跳过 RemoveAds / RemoveDayAds 的发放；
///  - SetProductPrice：原价文本（oldPriceText）+ 价格位置（有折扣下移）；
///  - SetShopData：按礼包 produceNameId 应用描边色 + 刷新限购次数 + 礼包标签显隐；
///  - OnTimeLimitedStateChanged：限时状态变化钩子（当前保留空实现）；
///  - ★ OnPurchaseSuccess：购买成功后刷新礼包内容（已拥有永久去广告时 RemoveAds / RemoveDayAds 会被跳过）。
///
/// 描边颜色规则（与道具弹窗内购礼包一致）：
///   NewcomerBox      hsla(31, 100%, 35%, 1)
///   SpecialPackageA  hsla(225, 100%, 35%, 1)
///   BoxTime          hsla(31, 100%, 35%, 1)
///   SpecialPackageB  hsla(272, 95%, 34%, 1)
///   Box0             hsla(294, 97%, 23%, 1)
///   Box1             hsla(270, 99%, 27%, 1)
///   Box2             hsla(16, 100%, 32%, 1)
///   Box3             hsla(225, 95%, 30%, 1)
///   Box4             hsla(9, 100%, 35%, 1)
///
/// 注意：itemEnergyList / itemHengList / giftsParent / giftsHengParent / giftItemPrefab / giftHengItemPrefab 由本类自己声明（基类已移除）。
/// </summary>
public class ShopGiftItem : ShopItemBase
{
    [Header("礼包专用")]
    [SerializeField] protected Transform itemEnergyList;          // 广告 and 体力资源的父节点（RemoveAds / RemoveDayAds / InfiniteEnergy 等）
    [SerializeField] protected Transform itemHengList;           // 横向礼包子项的父节点（Prefab 中的 ItemHengList），排版按礼包单独微调
    [SerializeField] protected Transform giftsParent;            // 礼包内容父节点（普通 GiftItem，实际数量 <= 4）
    [SerializeField] protected Transform giftsHengParent;        // 礼包内容父节点（横向 GiftHengItem，实际数量 > 4）
    [SerializeField] protected GiftItem giftItemPrefab;          // 礼包内容项 Prefab（实际数量 <= 4 时用；itemEnergyList 内只有 1 项或只有体力时也用它）
    [SerializeField] protected GiftItem giftHengItemPrefab;      // 礼包内容项 Prefab（实际数量 > 4 时用，横向布局；itemEnergyList 内含多项广告/其他资源时也用它）
    [SerializeField] protected Text oldPriceText;                // 原价文本（有折扣时显示 originalPrice）
    [SerializeField] protected Text limitCountText;              // 限购次数文本
    [SerializeField] protected Text tagText;                     // 礼包标签

    // 说明：礼包内容描边色复用基类的 _color（原实现在这里重复声明同名字段，会隐藏基类字段）

    /// <summary>阈值：礼包实际展示资源超过该数量时，切换为横向布局 Prefab（giftHengItemPrefab）</summary>
    private const int HorizontalLayoutThreshold = 4;

    // ======================= ★ itemEnergyList 细节微调（按礼包区分） =======================

    /// <summary>特惠礼包B：itemEnergyList 上 GridLayoutGroup 的 padding.left</summary>
    private const int SpecialPackageBPaddingLeft = -70;
    /// <summary>其它礼包：itemEnergyList 上 GridLayoutGroup 的 padding.left（复位用）</summary>
    private const int CommonPaddingLeft = 0;
    /// <summary>itemEnergyList 下只有 1 个子对象时：GiftItem 图标的 anchoredPosition.y</summary>
    private const float SingleEnergyItemIconY = 40f;
    /// <summary>itemEnergyList 下只有 1 个子对象时：GiftItem 图标的宽度（sizeDelta.x）</summary>
    private const float SingleEnergyItemIconWidth = 186.3f;
    /// <summary>itemEnergyList 下只有 1 个子对象时：GiftItem 图标的高度（sizeDelta.y）</summary>
    private const float SingleEnergyItemIconHeight = 230f;

    // ======================= ★ itemHengList 细节微调（按礼包 + 去广告状态区分） =======================

    /// <summary>特惠礼包B（且未购买永久去广告）：itemHengList 上 GridLayoutGroup 的 padding.left</summary>
    private const int SpecialPackageBHengPaddingLeft = -60;
    /// <summary>特惠礼包B（且未购买永久去广告）：itemHengList 上 GridLayoutGroup 的 spacing.x</summary>
    private const float SpecialPackageBHengSpacingX = 30f;
    /// <summary>其它情况：itemHengList 上 GridLayoutGroup 的 padding.left（复位值，与 Prefab 默认一致）</summary>
    private const int CommonHengPaddingLeft = -5;
    /// <summary>其它情况：itemHengList 上 GridLayoutGroup 的 spacing.x（复位值，与 Prefab 默认一致）</summary>
    private const float CommonHengSpacingX = 10f;

    // ======================= ★ 礼包底图（按礼包 + 去广告状态区分） =======================

    /// <summary>
    /// 特惠礼包B 且【已购买永久去广告】时使用的礼包底图名（UI_Universal 图集里的精灵名）。
    /// 未购买永久去广告时仍用 shop.csv 里配置的 showIcon（shopGiftTeB）。
    /// </summary>
    private const string SpecialPackageBOwnedAdsBackground = "shopGiftTeB2";

    /// <summary>本次已生成的 GiftItem 列表（用于回收，避免对象池复用时旧子物体残留）</summary>
    private readonly List<GiftItem> _spawnedGiftItems = new List<GiftItem>();

    // ======================= ★ 关店时立即隐藏：粒子 + 数量文本 =======================

    /// <summary>礼包卡上的粒子（Particle System (2) / FX_LightSpin 实例），首次用到时缓存。</summary>
    private ParticleSystem[] _giftParticles;

    /// <summary>粒子缓存是否已初始化（含"确实没有粒子"的情况，避免重复查找）。</summary>
    private bool _giftParticlesCached;

    /// <summary>
    /// 关店时立即隐藏礼包卡上的「粒子 + 数量文本」。
    /// 只做隐藏，不改任何层级。
    /// </summary>
    public void HideDecorationImmediately()
    {
        if (!_giftParticlesCached)
        {
            _giftParticlesCached = true;
            // includeInactive: true —— 卡片可能刚 SetActive、子节点尚未完全激活，
            // 用 false 会缓存成空数组
            _giftParticles = GetComponentsInChildren<ParticleSystem>(true);
        }

        if (_giftParticles != null)
        {
            for (int i = 0; i < _giftParticles.Length; i++)
            {
                var ps = _giftParticles[i];
                if (ps == null) continue;

                // StopEmittingAndClear：清掉已在飞的粒子，不留残影
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.gameObject.SetActive(false);
            }
        }

        if (shopCountText != null) shopCountText.gameObject.SetActive(false);
    }

    /// <summary>
    /// 下次进店时恢复「粒子 + 数量文本」。
    /// 数量文本交给 HandleProductContentDisplay，按商品数据重新决定显示/隐藏和内容。
    /// </summary>
    public void RestoreDecoration()
    {
        if (_giftParticles != null)
        {
            for (int i = 0; i < _giftParticles.Length; i++)
            {
                var ps = _giftParticles[i];
                if (ps == null) continue;

                if (!ps.gameObject.activeSelf) ps.gameObject.SetActive(true);
                ps.Play();
            }
        }

        if (shopDataItem != null) HandleProductContentDisplay(shopDataItem);
    }

    // ======================= ★ 礼包子项对象池 =======================

    /// <summary>
    /// 礼包内容项的对象池（按 Prefab 区分：普通 GiftItem / 横向 GiftHengItem）。
    /// 原来每次刷新都是 Destroy 旧项 + Instantiate 新项，进店和购买后都会产生一批 GC 与层级变更；
    /// 改成池化后只在第一次创建，后续复用。
    /// </summary>
    private readonly Dictionary<GiftItem, ObjectPool> _giftPools = new Dictionary<GiftItem, ObjectPool>();

    /// <summary>池容器：挂在卡片下一个「不激活」的空节点，LayoutGroup 不会统计到它，也不影响卡片排版。</summary>
    private Transform _giftPoolRoot;

    private Transform GetGiftPoolRoot()
    {
        if (_giftPoolRoot != null) return _giftPoolRoot;

        var go = new GameObject("GiftItemPool");
        go.transform.SetParent(transform, false);
        // ★ 容器常驻非激活：池对象本身就 SetActive(false)，容器不激活可以完全不参与布局与渲染
        go.SetActive(false);
        _giftPoolRoot = go.transform;
        return _giftPoolRoot;
    }

    /// <summary>从池里取一个礼包内容项（没有就实例化并纳管）。</summary>
    private GiftItem GetPooledGiftItem(GiftItem prefab, Transform parent)
    {
        if (prefab == null || parent == null) return null;

        ObjectPool pool;
        if (!_giftPools.TryGetValue(prefab, out pool) || pool == null)
        {
            // initcount 传 0：按需创建。预生成会在铺列表时反而制造一批无谓的实例化。
            pool = new ObjectPool(prefab.gameObject, GetGiftPoolRoot(), 0);
            _giftPools[prefab] = pool;
        }

        return pool.GetObject<GiftItem>(parent);
    }

    /// <summary>回收礼包内容项：池对象直接还池，非池对象（美术直接摆在 Prefab 上的）才销毁。</summary>
    private void ReturnGiftItem(GiftItem item)
    {
        if (item == null) return;

        var poolObject = item.GetComponent<PoolObject>();
        if (poolObject != null && poolObject.pool != null)
        {
            ObjectPool.ReturnObjectToPool(item.gameObject);
            return;
        }

        item.gameObject.SetActive(false);
        Destroy(item.gameObject);
    }

    // ======================= 描边颜色配置 =======================

    private static readonly Dictionary<string, Color> _bonusOutlineColors = new Dictionary<string, Color>
    {
        { "NewcomerBox",     HslToColor(31f,  1.00f, 0.35f) },
        { "SpecialPackageA", HslToColor(225f, 1.00f, 0.35f) },
        { "BoxTime",         HslToColor(31f,  1.00f, 0.35f) },
        { "SpecialPackageB", HslToColor(272f, 0.95f, 0.34f) },
        { "Box0",            HslToColor(294f, 0.97f, 0.23f) },
        { "Box1",            HslToColor(270f, 0.99f, 0.27f) },
        { "Box2",            HslToColor(16f,  1.00f, 0.32f) },
        { "Box3",            HslToColor(225f, 0.95f, 0.30f) },
        { "Box4",            HslToColor(9f,   1.00f, 0.35f) },
        { "RemoveADS",       HslToColor(189f, 1.00f, 0.11f) },
        { "RemoveAdsWeek",   HslToColor(189f, 1.00f, 0.11f) },
    };

    /// <summary>HSL(A) → Unity Color。</summary>
    private static Color HslToColor(float h, float s, float l, float a = 1f)
    {
        h %= 360f;
        if (h < 0f) h += 360f;
        s = Mathf.Clamp01(s);
        l = Mathf.Clamp01(l);

        float c = (1f - Mathf.Abs(2f * l - 1f)) * s;
        float hPrime = h / 60f;
        float x = c * (1f - Mathf.Abs(hPrime % 2f - 1f));
        float m = l - c / 2f;

        float r, g, b;
        if (hPrime < 1f) { r = c; g = x; b = 0f; }
        else if (hPrime < 2f) { r = x; g = c; b = 0f; }
        else if (hPrime < 3f) { r = 0f; g = c; b = x; }
        else if (hPrime < 4f) { r = 0f; g = x; b = c; }
        else if (hPrime < 5f) { r = x; g = 0f; b = c; }
        else { r = c; g = 0f; b = x; }

        return new Color(r + m, g + m, b + m, a);
    }

    private static bool TryGetBonusOutlineColor(string produceNameId, out Color color)
    {
        if (!string.IsNullOrEmpty(produceNameId) && _bonusOutlineColors.TryGetValue(produceNameId, out color))
            return true;
        color = Color.white;
        return false;
    }

    /// <summary>
    /// 给 nameText / limitCountText / shopCountText 的 Outline 设置描边色。
    ///  - nameText：从第 2 个 Outline 开始设置（保留第 1 个作为高光/上层描边）；
    ///  - limitCountText / shopCountText：全部 Outline 设置。
    /// </summary>
    private void ApplyOutlineColor(Color color)
    {
        if (nameText != null)
        {
            var outlines = nameText.GetComponents<Outline>();
            for (int i = 1; i < outlines.Length; i++)
                outlines[i].effectColor = color;
        }

        if (limitCountText != null)
        {
            var outlines = limitCountText.GetComponents<Outline>();
            for (int i = 0; i < outlines.Length; i++)
                outlines[i].effectColor = color;
        }

        if (shopCountText != null)
        {
            var outlines = shopCountText.GetComponents<Outline>();
            for (int i = 0; i < outlines.Length; i++)
                outlines[i].effectColor = color;
        }
    }

    // ======================= 限时状态变化钩子（保留空实现） =======================

    protected override void OnTimeLimitedStateChanged(bool isActive)
    {
        // 如需在限时角标显隐时调整礼包内容布局，可在此处理
    }

    // ======================= 图标 =======================

    protected override void SetShopIcon()
    {
        if (shopIcon == null || shopDataItem.type == 1) return;

        var icon = LoadShopIcon(shopDataItem.showIcon + "_Icon");
        if (icon != null)
            shopIcon.sprite = icon;
    }

    // ======================= 阻止基类把 type=1 按"单物品道具"展示 =======================

    /// <summary>
    /// type=1 在 ShopGiftItem 中按"广告礼包"处理，不走基类的单物品道具展示逻辑。
    /// </summary>
    protected override void HandleSpecialTypeItems(ShopDataItem data)
    {
        // 空实现
    }

    // ======================= ★ 购买发放跳过：已拥有永久去广告 → 跳过 RemoveAds / RemoveDayAds =======================

    /// <summary>
    /// 已拥有永久去广告时，礼包中的 RemoveAds / RemoveDayAds 项在购买发放时跳过。
    /// 与展示侧（InitGiftItems 跳过）保持一致：不展示也不发放。
    /// </summary>
    protected override bool IsContentSkippedOnPurchase(int giftType)
    {
        if (!IsRemoveAdsRelatedType(giftType)) return false;

        bool ownsPermanentRemoveAds = IsPermanentRemoveAdsOwned();

        if (ownsPermanentRemoveAds)
        {
            LogShopItem($"已拥有永久去广告，购买时跳过 content type={giftType} 的发放");
        }

        return ownsPermanentRemoveAds;
    }

    // ======================= 显示：礼包名 + 底图 + （type=2）金币数量 =======================

    /// <summary>
    /// 卡片刷新（进店 / 购买后）时恢复关店隐藏的粒子与数量文本。
    /// 基类 UpdateUI 不会重跑 HandleProductContentDisplay，所以这里显式恢复一次。
    /// </summary>
    public override void UpdateUI()
    {
        base.UpdateUI();
        RestoreDecoration();
    }

    protected override void HandleProductContentDisplay(ShopDataItem data)
    {
        if (nameText != null)
        {
            nameText.gameObject.SetActive(true);
            nameText.text = MultilingualManager.Instance?.GetString(data.name) ?? data.name;
        }

        if (dibg != null)
        {
            // ★ 特惠礼包B 且【已购买永久去广告】→ 底图换为 shopGiftTeB2（去掉去广告项后的专属底图）；
            //   其它情况（含未购买永久去广告的特惠礼包B）→ 仍用商品配置里的 showIcon。
            //   注意：礼包卡走对象池复用，这里必须每次刷新都按当前状态重设，
            //   否则「购买永久去广告后」或「复用给别的礼包」时会残留 shopGiftTeB2。
            string bgIconName = ResolveGiftBackgroundIconName(data);
            Sprite sprite = LoadShopIcon(bgIconName);
            if (sprite != null)
                dibg.sprite = sprite;
        }

        // shopCountText 金币数量显示规则：
        //  - type=1 广告礼包：金币由 GiftItem 展示 → shopCountText 隐藏；
        //  - type=2 普通礼包：金币由 shopCountText 展示。
        if (shopCountText != null)
        {
            if (data.type == 1)
            {
                shopCountText.gameObject.SetActive(false);
            }
            else
            {
                int coins = GetCoinsFromProductContent(data);
                if (coins > 0)
                {
                    shopCountText.gameObject.SetActive(true);
                    shopCountText.text = $"{coins}";
                }
                else
                {
                    shopCountText.gameObject.SetActive(false);
                }
            }
        }
    }

    private int GetCoinsFromProductContent(ShopDataItem data)
    {
        if (data == null || data.productContent == null) return 0;

        foreach (var content in data.productContent)
        {
            if (content == null || content.Count < 2) continue;
            int type;
            if (!int.TryParse(content[0], out type)) continue;
            if (type != (int)LimitRewordType.Coins) continue;
            int count;
            return int.TryParse(content[1], out count) ? count : 0;
        }
        return 0;
    }

    // ======================= 礼包内容（type=2 跳过金币；type=1 金币也走 GiftItem） =======================

    protected override void HandleMultiProductContent(ShopDataItem data)
    {
        if (data == null) return;

        // ★ 是否为特惠礼包B（itemEnergyList 的排版对它有单独一套参数）
        bool isSpecialPackageB = IsSpecialPackageB(data);

        // ★ itemEnergyList 的 GridLayoutGroup.left 按礼包区分；对象池复用，必须每次刷新都重置
        ApplyItemEnergyListPadding(isSpecialPackageB);

        // ★ itemHengList 的 GridLayoutGroup.left / spacing.x 按「礼包 + 是否已购买永久去广告」区分；
        //   同样受对象池复用影响，每次刷新都重算（不能只在特惠礼包B 时设置，否则会残留到其它礼包）
        ApplyItemHengListLayout(isSpecialPackageB);

        if (data.productContent == null || data.productContent.Count == 0) return;

        // ★ 计算实际需要创建的 GiftItem 数量（按 InitGiftItems 的跳过规则：跳过金币 / 跳过去广告）
        int actualCount = CountActualGiftItems(data);

        if (actualCount <= 0)
        {
            // ★ 修复：全部项都被跳过时，也必须清理旧子物体，避免购买后残留旧的 RemoveAds 展示
            LogShopItemDetail($"HandleMultiProductContent: 实际需要创建的 GiftItem 数量为 0，清理旧项（productContent.Count={data.productContent.Count}）");
            ClearGiftItems();
            return;
        }

        // ★ 根据实际数量选择 Prefab：> 4 用 giftHengItemPrefab（横向布局），否则用 giftItemPrefab
        bool useHorizontal = actualCount > HorizontalLayoutThreshold;

        // Inspector 未配置时，从 Bundle 加载
        if (useHorizontal && giftHengItemPrefab == null)
        {
            if (AdvancedBundleLoader.SharedInstance != null)
            {
                var prefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "GiftHengItem");
                if (prefab != null)
                {
                    giftHengItemPrefab = prefab.GetComponent<GiftItem>();
                    LogShopItemDetail($"HandleMultiProductContent: 从 Bundle 加载 GiftHengItem 成功 (actualCount={actualCount})");
                }
                else
                {
                    LogShopItemDetail($"HandleMultiProductContent: Bundle 未找到 commonitem/GiftHengItem (actualCount={actualCount})");
                }
            }
            else
            {
                LogShopItemDetail("HandleMultiProductContent: AdvancedBundleLoader.SharedInstance 为空，无法加载 GiftHengItem");
            }
        }

        if (!useHorizontal && giftItemPrefab == null)
        {
            if (AdvancedBundleLoader.SharedInstance != null)
            {
                var prefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "GiftItem");
                if (prefab != null)
                {
                    giftItemPrefab = prefab.GetComponent<GiftItem>();
                    LogShopItemDetail($"HandleMultiProductContent: 从 Bundle 加载 GiftItem 成功 (actualCount={actualCount})");
                }
                else
                {
                    LogShopItemDetail($"HandleMultiProductContent: Bundle 未找到 commonitem/GiftItem (actualCount={actualCount})");
                }
            }
            else
            {
                LogShopItemDetail("HandleMultiProductContent: AdvancedBundleLoader.SharedInstance 为空，无法加载 GiftItem");
            }
        }

        // 最终选择 Prefab
        GiftItem selectedPrefab = useHorizontal ? giftHengItemPrefab : giftItemPrefab;

        // 横向 Prefab 不可用 → 回退普通 GiftItem，保证内容至少能展示
        if (selectedPrefab == null && useHorizontal)
        {
            LogShopItemDetail($"HandleMultiProductContent: 警告! giftHengItemPrefab 未配置且加载失败，回退 giftItemPrefab (actualCount={actualCount})。请检查 Inspector 或 Bundle 中是否存在 GiftHengItem Prefab。");
            selectedPrefab = giftItemPrefab;
        }

        if (selectedPrefab == null)
        {
            LogShopItemDetail("HandleMultiProductContent: 无可用的 GiftItem Prefab，跳过礼包内容初始化");
            return;
        }

        LogShopItemDetail($"HandleMultiProductContent: 实际数量={actualCount}, 使用={(selectedPrefab == giftHengItemPrefab ? "giftHengItemPrefab" : "giftItemPrefab")}");
        InitGiftItems(selectedPrefab);
    }

    // ======================= ★ itemEnergyList 细节微调 =======================

    /// <summary>是否为特惠礼包B（SpecialPackageB）。</summary>
    private bool IsSpecialPackageB(ShopDataItem data)
    {
        if (data == null || string.IsNullOrEmpty(data.produceNameId)) return false;

        string specialB = ShopManager.shopManager != null
            ? ShopManager.shopManager.ProductSpecialPackageB
            : "SpecialPackageB";

        return data.produceNameId == specialB;
    }

    /// <summary>
    /// ★ 设置 itemEnergyList 上 GridLayoutGroup 的 padding.left：
    ///  - 特惠礼包B → -70；
    ///  - 其它礼包 → 0（对象池复用，必须显式复位，否则会残留上一个礼包的 -70）。
    /// 找不到 GridLayoutGroup 时只打日志，不影响其它逻辑。
    /// </summary>
    private void ApplyItemEnergyListPadding(bool isSpecialPackageB)
    {
        if (itemEnergyList == null) return;

        GridLayoutGroup grid = itemEnergyList.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            LogShopItemDetail("ApplyItemEnergyListPadding: itemEnergyList 上未找到 GridLayoutGroup，跳过 padding.left 调整");
            return;
        }

        int targetLeft = isSpecialPackageB ? SpecialPackageBPaddingLeft : CommonPaddingLeft;
        RectOffset padding = grid.padding;
        if (padding != null && padding.left == targetLeft) return;

        int right = padding != null ? padding.right : 0;
        int top = padding != null ? padding.top : 0;
        int bottom = padding != null ? padding.bottom : 0;
        grid.padding = new RectOffset(targetLeft, right, top, bottom);

        LogShopItemDetail($"ApplyItemEnergyListPadding: padding.left → {targetLeft} (isSpecialPackageB={isSpecialPackageB})");
    }

    /// <summary>
    /// ★ 设置 itemHengList（Prefab 中的 ItemHengList）上 GridLayoutGroup 的排版：
    ///  - 特惠礼包B 且【未购买永久去广告】→ padding.left = -60、spacing.x = 30；
    ///  - 其它情况（其它礼包 / 已购买永久去广告）→ 复位为 Prefab 默认值（left = -5、spacing.x = 10）。
    ///
    /// 为什么每次刷新都要重算：礼包卡走对象池复用，同一个 ShopGiftItem 实例会先后展示不同礼包，
    /// 也会在「购买永久去广告前 / 后」被复用；只设不复位会把 -60 / 30 残留到别的礼包上。
    /// 找不到 GridLayoutGroup 时只打日志，不影响其它逻辑。
    /// </summary>
    private void ApplyItemHengListLayout(bool isSpecialPackageB)
    {
        if (itemHengList == null) return;

        GridLayoutGroup grid = itemHengList.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            LogShopItemDetail("ApplyItemHengListLayout: itemHengList 上未找到 GridLayoutGroup，跳过 padding.left / spacing.x 调整");
            return;
        }

        // ★ 需求：只有「特惠礼包B」且「没有购买永久去广告」时才用 -60 / 30；
        //   买了永久去广告后礼包里的去广告项会被跳过，排版回到默认值。
        bool ownsPermanentRemoveAds = IsPermanentRemoveAdsOwned();

        bool useSpecialLayout = isSpecialPackageB && !ownsPermanentRemoveAds;

        int targetLeft = useSpecialLayout ? SpecialPackageBHengPaddingLeft : CommonHengPaddingLeft;
        float targetSpacingX = useSpecialLayout ? SpecialPackageBHengSpacingX : CommonHengSpacingX;

        // 1. padding.left（其余边保持 Prefab 上的值）
        RectOffset padding = grid.padding;
        if (padding == null || padding.left != targetLeft)
        {
            int right = padding != null ? padding.right : 0;
            int top = padding != null ? padding.top : 0;
            int bottom = padding != null ? padding.bottom : 0;
            grid.padding = new RectOffset(targetLeft, right, top, bottom);
        }

        // 2. spacing.x（spacing.y 保持 Prefab 上的值）
        Vector2 spacing = grid.spacing;
        if (!Mathf.Approximately(spacing.x, targetSpacingX))
        {
            grid.spacing = new Vector2(targetSpacingX, spacing.y);
        }

        LogShopItemDetail($"ApplyItemHengListLayout: padding.left → {targetLeft}, spacing.x → {targetSpacingX} " +
                          $"(isSpecialPackageB={isSpecialPackageB}, ownsPermanentRemoveAds={ownsPermanentRemoveAds})");
    }

    /// <summary>
    /// ★ 解析礼包卡底图（dibg）应使用的精灵名：
    ///  - 特惠礼包B 且【已购买永久去广告】→ shopGiftTeB2；
    ///  - 其它情况 → 商品配置里的 showIcon（shop.csv 的「展示图」列）。
    ///
    /// 为什么按「已购买永久去广告」区分：买了永久去广告后，礼包里的去广告项会被跳过、
    /// 内容排版也随之变化，底图需要用去掉去广告角标的版本。
    /// </summary>
    private string ResolveGiftBackgroundIconName(ShopDataItem data)
    {
        if (data == null) return null;

        if (IsSpecialPackageB(data) && IsPermanentRemoveAdsOwned())
        {
            LogShopItemDetail($"礼包底图: 特惠礼包B 且已购买永久去广告 → {SpecialPackageBOwnedAdsBackground}");
            return SpecialPackageBOwnedAdsBackground;
        }

        return data.showIcon;
    }

    /// <summary>是否已购买永久去广告（统一入口，避免各处重复判空）。</summary>
    private bool IsPermanentRemoveAdsOwned()
    {
        return ShopManager.shopManager != null && ShopManager.shopManager.IsRemoveAdsOwned();
    }

    /// <summary>
    /// 计算实际需要创建的 GiftItem 数量（与 InitGiftItems 的跳过规则一致）：
    ///  - 跳过无效项（null 或 Count < 2）；
    ///  - type=2 跳过金币项（金币由 shopCountText 展示）；
    ///  - 已拥有永久去广告 → 跳过 RemoveAds / RemoveDayAds 项。
    /// </summary>
    private int CountActualGiftItems(ShopDataItem data)
    {
        if (data == null || data.productContent == null) return 0;

        bool ownsPermanentRemoveAds = IsPermanentRemoveAdsOwned();

        int count = 0;
        foreach (var giftdata in data.productContent)
        {
            if (giftdata == null || giftdata.Count < 2) continue;

            int giftType;
            if (!int.TryParse(giftdata[0], out giftType)) continue;

            // type=2 普通礼包：金币由 shopCountText 展示，不计入 GiftItem 数量
            if (giftType == (int)LimitRewordType.Coins && data.type != 1) continue;

            // 已拥有永久去广告 → 去广告项不展示，不计入 GiftItem 数量
            if (ownsPermanentRemoveAds && IsRemoveAdsRelatedType(giftType)) continue;

            count++;
        }
        return count;
    }

    // ======================= ★ 根据 Prefab / 资源类型选择父节点 =======================

    /// <summary>
    /// 判断是否为「广告 / 体力类资源」：这类项优先挂到 itemEnergyList。
    /// 如需扩展（例如更多体力相关类型），在此追加即可。
    /// </summary>
    private static bool IsAdOrEnergyType(int giftType)
    {
        return giftType == (int)LimitRewordType.RemoveAds
               || giftType == (int)LimitRewordType.RemoveDayAds
               || giftType == (int)LimitRewordType.InfiniteEnergy;
    }

    /// <summary>
    /// 根据实际使用的 Prefab 返回对应的父节点：
    ///  - giftHengItemPrefab → giftsHengParent（横向布局父节点）
    ///  - 其它（含回退到 giftItemPrefab）→ giftsParent
    /// giftsHengParent 未配置时回退到 giftsParent（避免空父节点导致内容丢失）。
    /// </summary>
    private Transform GetParentForPrefab(GiftItem prefab)
    {
        bool isHeng = prefab != null && giftHengItemPrefab != null && prefab == giftHengItemPrefab;
        if (isHeng)
        {
            if (giftsHengParent != null) return giftsHengParent;
            LogShopItemDetail("GetParentForPrefab: 使用 giftHengItemPrefab 但 giftsHengParent 未配置，回退 giftsParent");
            return giftsParent;
        }
        return giftsParent;
    }

    /// <summary>
    /// ★ 按「资源类型 + Prefab」选择父节点：
    ///  - 广告 / 体力类（RemoveAds / RemoveDayAds / InfiniteEnergy）且 itemEnergyList 已配置 → itemEnergyList
    ///  - 其它 → GetParentForPrefab（giftHengItemPrefab → giftsHengParent，其余 → giftsParent）
    /// </summary>
    private Transform GetParentForGiftType(int giftType, GiftItem prefab)
    {
        if (itemEnergyList != null && IsAdOrEnergyType(giftType))
            return itemEnergyList;

        return GetParentForPrefab(prefab);
    }

    // ======================= ★ itemEnergyList 内 Prefab 规则 =======================

    /// <summary>
    /// ★ itemEnergyList 内 Prefab 规则（只影响挂到 itemEnergyList 的项）：
    ///  - itemEnergyList 内【只有 1 项】→ 返回 false，使用 giftItemPrefab；
    ///    （横向 GiftHengItem 是按「并排多个」设计的，只放 1 个会撑得很宽/留白，
    ///      例：特惠礼包B 只有「去广告1天」会挂到 itemEnergyList）
    ///  - itemEnergyList 内【实际展示的项全部是体力资源（InfiniteEnergy）】→ 返回 false，使用 giftItemPrefab；
    ///  - itemEnergyList 内【含广告资源或其他资源，且多于 1 项】→ 返回 true，使用 giftHengItemPrefab。
    /// 跳过规则与 InitGiftItems 保持一致（type=2 跳过金币、已拥有永久去广告跳过 RemoveAds/RemoveDayAds），
    /// 以保证判断依据与实际展示的项一致。
    /// </summary>
    private bool ShouldUseHengForItemEnergyList(ShopDataItem data)
    {
        if (itemEnergyList == null || data?.productContent == null) return false;

        bool ownsPermanentRemoveAds = IsPermanentRemoveAdsOwned();

        bool allEnergy = true;
        int energyItemCount = 0; // ★ 实际会挂到 itemEnergyList 的项数

        foreach (var giftdata in data.productContent)
        {
            if (giftdata == null || giftdata.Count < 2) continue;

            int giftType;
            if (!int.TryParse(giftdata[0], out giftType)) continue;

            // 只看会挂到 itemEnergyList 的项
            if (!IsAdOrEnergyType(giftType)) continue;

            // 与 InitGiftItems 相同的跳过规则（保证与实际展示一致）
            if (giftType == (int)LimitRewordType.Coins && data.type != 1) continue;
            if (ownsPermanentRemoveAds && IsRemoveAdsRelatedType(giftType)) continue;

            energyItemCount++;
            if (giftType != (int)LimitRewordType.InfiniteEnergy)
                allEnergy = false;
        }

        // ★ 只有 1 个子对象 → 一律用 GiftItem（横向 Prefab 单个摆放会变形）
        if (energyItemCount <= 1) return false;

        // 全部是体力资源 → GiftItem；含广告/其他资源且多于 1 项 → GiftHengItem
        return !allEnergy;
    }

    /// <summary>
    /// 确保 giftItemPrefab 可用（itemEnergyList 内使用）；为空时尝试从 Bundle 加载。
    /// </summary>
    private GiftItem EnsureGiftItemPrefab()
    {
        if (giftItemPrefab != null) return giftItemPrefab;

        if (AdvancedBundleLoader.SharedInstance != null)
        {
            var go = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "GiftItem");
            if (go != null)
            {
                giftItemPrefab = go.GetComponent<GiftItem>();
                if (giftItemPrefab != null)
                    LogShopItemDetail("EnsureGiftItemPrefab: 从 Bundle 加载 GiftItem 成功（itemEnergyList 专用）");
            }
        }

        return giftItemPrefab;
    }

    /// <summary>
    /// 确保 giftHengItemPrefab 可用（itemEnergyList 内使用）；为空时尝试从 Bundle 加载。
    /// </summary>
    private GiftItem EnsureGiftHengItemPrefab()
    {
        if (giftHengItemPrefab != null) return giftHengItemPrefab;

        if (AdvancedBundleLoader.SharedInstance != null)
        {
            var go = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "GiftHengItem");
            if (go != null)
            {
                giftHengItemPrefab = go.GetComponent<GiftItem>();
                if (giftHengItemPrefab != null)
                    LogShopItemDetail("EnsureGiftHengItemPrefab: 从 Bundle 加载 GiftHengItem 成功（itemEnergyList 专用）");
            }
        }

        return giftHengItemPrefab;
    }

    // ======================= 清理 & 创建礼包内容 =======================

    /// <summary>
    /// 回收 itemEnergyList / giftsParent / giftsHengParent 下的旧 GiftItem 子物体。
    /// ★ 走对象池：SetActive(false) 后还池复用，不再 Destroy/Instantiate。
    ///   （Destroy 是延迟到帧末才真正发生，同一帧既有销毁又有新建会造成 GC 与 Canvas 重建叠加）
    /// </summary>
    private void ClearGiftItems()
    {
        // 1. 回收自己创建的项
        for (int i = _spawnedGiftItems.Count - 1; i >= 0; i--)
        {
            ReturnGiftItem(_spawnedGiftItems[i]);
        }
        _spawnedGiftItems.Clear();

        // 2. 兜底：清理三个父节点下所有带 GiftItem 组件的子物体（同一节点只清一次）
        ClearGiftItemsUnder(itemEnergyList);
        if (giftsParent != null && giftsParent != itemEnergyList)
            ClearGiftItemsUnder(giftsParent);
        if (giftsHengParent != null
            && giftsHengParent != giftsParent
            && giftsHengParent != itemEnergyList)
            ClearGiftItemsUnder(giftsHengParent);
    }

    private void ClearGiftItemsUnder(Transform parent)
    {
        if (parent == null) return;

        int count = parent.childCount;
        for (int i = count - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            if (child == null) continue;

            var gift = child.GetComponent<GiftItem>();
            if (gift != null)
            {
                ReturnGiftItem(gift);
            }
        }
    }

    private void InitGiftItems(GiftItem prefab)
    {
        if (prefab == null)
        {
            LogShopItemDetail("InitGiftItems: prefab 为空，跳过");
            return;
        }

        // ★ 先清理旧子物体（对象池复用必须清理，三个父节点都要清）
        ClearGiftItems();

        bool ownsPermanentRemoveAds = IsPermanentRemoveAdsOwned();

        bool isHengPrefab = giftHengItemPrefab != null && prefab == giftHengItemPrefab;
        LogShopItemDetail($"InitGiftItems: prefab={prefab.name}, isHengPrefab={isHengPrefab}");

        // ★ itemEnergyList 内 Prefab 规则（仅影响挂到 itemEnergyList 的项，不影响其他项）：
        //    - itemEnergyList 内【只有 1 项】→ 使用 giftItemPrefab（单个用横向 Prefab 会变形/留白）；
        //    - itemEnergyList 内实际展示的项【全部是体力资源（InfiniteEnergy）】→ 使用 giftItemPrefab；
        //    - itemEnergyList 内实际展示的项【含广告资源或其他资源且多于 1 项】→ 使用 giftHengItemPrefab；
        //    - 目标 Prefab 不可用时回退到本次传入的 prefab。
        bool energyUseHeng = ShouldUseHengForItemEnergyList(shopDataItem);
        GiftItem energyPrefab;
        if (energyUseHeng)
        {
            energyPrefab = EnsureGiftHengItemPrefab();
            if (energyPrefab == null)
            {
                LogShopItemDetail("InitGiftItems: itemEnergyList 内应使用 GiftHengItem，但 giftHengItemPrefab 不可用，回退传入 prefab");
                energyPrefab = prefab;
            }
        }
        else
        {
            energyPrefab = EnsureGiftItemPrefab();
            if (energyPrefab == null)
            {
                LogShopItemDetail("InitGiftItems: itemEnergyList 内应使用 GiftItem，但 giftItemPrefab 不可用，回退传入 prefab");
                energyPrefab = prefab;
            }
        }

        if (itemEnergyList != null)
        {
            LogShopItemDetail($"InitGiftItems: itemEnergyList 内 Prefab 规则 → energyUseHeng={energyUseHeng}, energyPrefab={energyPrefab.name}");
        }

        foreach (List<string> giftdata in shopDataItem.productContent)
        {
            if (giftdata == null || giftdata.Count < 2)
            {
                LogShopItemDetail("InitGiftItems: productContent 项数据不合法，跳过");
                continue;
            }

            int giftType = int.Parse(giftdata[0]);

            // 金币处理：
            //  - type=2 普通礼包：金币由 shopCountText 显示 → GiftItem 跳过金币；
            //  - type=1 广告礼包：金币用 GiftItem 显示 → 不跳过。
            if (giftType == (int)LimitRewordType.Coins && shopDataItem.type != 1)
            {
                LogShopItemDetail($"InitGiftItems: 金币项由 shopCountText 展示，跳过 GiftItem (count={giftdata[1]})");
                continue;
            }

            // 已购买永久去广告 → 跳过礼包中的去广告物品
            if (ownsPermanentRemoveAds && IsRemoveAdsRelatedType(giftType))
            {
                LogShopItemDetail($"已拥有永久去广告，跳过礼包中的去广告物品 (type={giftType})");
                continue;
            }

            // ★ 按「资源类型 + Prefab」选择父节点：
            //    广告 / 体力类（且 itemEnergyList 已配置）→ itemEnergyList；
            //    其它 → giftsParent / giftsHengParent。
            Transform parent = GetParentForGiftType(giftType, prefab);
            if (parent == null)
            {
                LogShopItemDetail($"InitGiftItems: 目标父节点为空（giftType={giftType}），跳过本项。请检查 itemEnergyList / giftsParent / giftsHengParent 配置");
                continue;
            }

            // ★ 仅对挂到 itemEnergyList 的项改用 energyPrefab；其他项仍使用传入的 prefab。
            bool isEnergyItem = itemEnergyList != null && IsAdOrEnergyType(giftType);
            GiftItem itemPrefab = isEnergyItem ? energyPrefab : prefab;

            // ★ 从池里取（没有才实例化），不再每次 Instantiate
            GiftItem giftItem = GetPooledGiftItem(itemPrefab, parent);
            if (giftItem == null)
            {
                LogShopItemDetail($"InitGiftItems: prefab 上未挂 GiftItem 组件，跳过 (prefab={itemPrefab.name})");
                continue;
            }

            if (giftType == (int)LimitRewordType.RemoveAds || giftType == (int)LimitRewordType.RemoveDayAds)
            {
                if (shopDataItem.type == 1 || shopDataItem.type == 2)
                    giftItem.SetShopData(giftdata, shopDataItem.id, _color, shopDataItem.des, shopDataItem.pointDes);
                else
                    giftItem.SetShopData(giftdata, shopDataItem.id, _color);
            }
            else
            {
                giftItem.SetShopData(giftdata, shopDataItem.id, _color);
            }

            bool isBonus = ShopManager.shopManager != null
                           && ShopManager.shopManager.IsBonusGift(shopDataItem.produceNameId, giftType);
            giftItem.SetBonus(isBonus);

            _spawnedGiftItems.Add(giftItem);

            LogShopItemDetail($"InitGiftItems: 礼包 {shopDataItem.produceNameId} (type={shopDataItem.type}) 内容 type={giftType}, isBonus={isBonus}, prefab={itemPrefab.name}, parent={parent.name}");
        }

        // ★ 全部创建完之后再统一处理「itemEnergyList 只有 1 个子对象」的图标微调（依赖真实子节点数量）
        ApplySingleEnergyItemIconLayout();
    }

    /// <summary>
    /// ★ itemEnergyList 下只有 1 个 GiftItem 时，图标统一改为 y = 40、宽 = 186.3、高 = 230。
    /// （横向 GiftHengItem 或子对象多于 1 个时不动，避免破坏已有的排列）
    ///
    /// 注意：这里必须用「本次新生成的 _spawnedGiftItems」来数，不能遍历 itemEnergyList.childCount ——
    /// 购买后刷新时 ClearGiftItems() 只是 Destroy（延迟到帧末），旧子物体这一帧仍挂在父节点下，
    /// 直接数子节点会得到「旧的 + 新的」= 2，导致规则不生效、图标保持 SetShopData 的原始尺寸。
    /// </summary>
    private void ApplySingleEnergyItemIconLayout()
    {
        if (itemEnergyList == null) return;

        GiftItem singleItem = null;
        int giftItemCount = 0;

        for (int i = 0; i < _spawnedGiftItems.Count; i++)
        {
            GiftItem item = _spawnedGiftItems[i];
            if (item == null) continue;

            // 只看本次生成、且挂在 itemEnergyList 下的项
            if (item.transform.parent != itemEnergyList) continue;

            giftItemCount++;
            singleItem = item;

            if (giftItemCount > 1) break; // 多于 1 个就不用继续找了
        }

        if (giftItemCount != 1 || singleItem == null) return;
        if (singleItem.IsHengItem) return;

        singleItem.SetIconAnchoredYAndSize(SingleEnergyItemIconY, SingleEnergyItemIconWidth, SingleEnergyItemIconHeight);

        LogShopItemDetail($"ApplySingleEnergyItemIconLayout: itemEnergyList 只有 1 个子对象 → icon y={SingleEnergyItemIconY}, w={SingleEnergyItemIconWidth}, h={SingleEnergyItemIconHeight}");
    }

    private bool IsRemoveAdsRelatedType(int type)
    {
        return type == (int)LimitRewordType.RemoveAds
               || type == (int)LimitRewordType.RemoveDayAds;
    }

    // ======================= 价格：原价 + 价格位置 =======================

    protected override void SetProductPrice(ShopDataItem data)
    {
        base.SetProductPrice(data);

        if (shopPriceText == null) return;

        bool hasDiscount = data != null
                           && data.originalPrice > 0
                           && data.originalPrice > data.price;

        if (oldPriceText != null)
        {
            if (hasDiscount)
            {
                CultureInfo culture = UIUtilities.GetCultureForCurrency("");
                oldPriceText.text = UIUtilities.FormatCurrency(data.originalPrice, culture);
                oldPriceText.gameObject.SetActive(true);
            }
            else
            {
                oldPriceText.gameObject.SetActive(false);
            }
        }

        var rt = shopPriceText.rectTransform;
        Vector2 pos = rt.anchoredPosition;
        pos.y = hasDiscount ? -20f : 0f;
        rt.anchoredPosition = pos;

        LogShopItemDetail($"价格布局: originalPrice={data?.originalPrice}, price={data?.price}, hasDiscount={hasDiscount}, priceY={pos.y}");
    }

    // ======================= 限购次数 =======================

    private void RefreshLimitCount()
    {
        if (limitCountText == null) return;

        GameObject root = limitCountText.transform.parent != null
            ? limitCountText.transform.parent.gameObject
            : limitCountText.gameObject;

        if (shopDataItem == null || shopDataItem.limited <= 0)
        {
            root.SetActive(false);
            return;
        }

        int buyCount = 0;
        ShopLimitData limitData = GameDataManager.Instance?.UserData?.limitShopItems?
            .Find(x => x != null && x.nameid == shopDataItem.produceNameId);
        if (limitData != null)
            buyCount = limitData.buyCount;

        int remain = Mathf.Max(0, shopDataItem.limited - buyCount);

        if (remain <= 0)
            limitCountText.text = "已售空";
        else
            limitCountText.text = $"仅限{UIUtilities.GetChineseNumber(remain)}次";

        root.SetActive(true);
        LogShopItemDetail($"限购次数: limited={shopDataItem.limited}, buyCount={buyCount}, remain={remain}, 显示='{limitCountText.text}'");
    }

    // ======================= 礼包标签 =======================

    private void RefreshTag()
    {
        if (tagText == null) return;

        GameObject root = tagText.transform.parent != null
            ? tagText.transform.parent.gameObject
            : tagText.gameObject;

        if (shopDataItem == null || string.IsNullOrEmpty(shopDataItem.labelZh))
        {
            root.SetActive(false);
            return;
        }

        tagText.text = shopDataItem.labelZh;
        root.SetActive(true);
        LogShopItemDetail($"礼包标签: {shopDataItem.labelZh}");
    }

    // ======================= SetShopData：应用描边色 + 刷新限购 + 刷新标签 =======================

    public override void SetShopData(ShopDataItem data)
    {
        Color outlineColor = Color.white;
        bool hasConfig = data != null && TryGetBonusOutlineColor(data.produceNameId, out outlineColor);

        _color = hasConfig ? outlineColor : Color.white;

        base.SetShopData(data);

        if (hasConfig)
        {
            ApplyOutlineColor(outlineColor);
            LogShopItemDetail($"描边颜色已应用: {data.produceNameId} → RGB({outlineColor.r:F3},{outlineColor.g:F3},{outlineColor.b:F3},{outlineColor.a:F2})");
        }

        RefreshLimitCount();
        RefreshTag();
    }

    // ======================= ★ 购买成功后刷新礼包 =======================

    /// <summary>
    /// ★ 购买成功后的礼包刷新：
    /// base.OnPurchaseSuccess 完成发放/统计后，按"创建礼包的逻辑"重新生成礼包内容。
    ///  - 若本次购买让用户获得永久去广告（或此前已拥有）→ IsRemoveAdsOwned() 为 true，
    ///    礼包中的 RemoveAds / RemoveDayAds 会在 CountActualGiftItems / InitGiftItems 中被跳过；
    ///  - 金币数（type=2 由 shopCountText 展示）与限购次数会一并刷新。
    /// HandleMultiProductContent → InitGiftItems 会先 ClearGiftItems 再重新创建，调用是幂等的。
    /// </summary>
    protected override void OnPurchaseSuccess(ProductItem item)
    {
        base.OnPurchaseSuccess(item);

        RefreshGiftContentAfterPurchase();

        // ★ 去广告礼包购买完成后，刷新商店内所有包含去广告的礼包（其他礼包里的「去掉广告」项应一并隐藏）
        if (ContainsRemoveAdsContent())
            RefreshAllRemoveAdsGiftPacksInShop();

        // ★ 限购次数用完（含迎新礼包）→ 隐藏自己并重排商店
        TryHideSelfIfSoldOut();
    }

    /// <summary>
    /// ★ 限购次数用完（limited > 0 且已买满）后隐藏自己，并重排商店。
    /// 原来只处理迎新礼包（NewcomerBox），现在萤火 / 飞驰 / 从容礼盒等所有限购礼包都适用；
    /// 下次进店时 ShopScreen 的条目创建逻辑也会过滤掉售罄礼包，不会重新冒出来。
    /// </summary>
    private void TryHideSelfIfSoldOut()
    {
        if (shopDataItem == null || ShopManager.shopManager == null) return;
        if (!ShopManager.shopManager.IsLimitedOut(shopDataItem)) return;

        LogShopItem($"限购次数已用完，隐藏礼包: {shopDataItem.produceNameId}(id={shopDataItem.id}, 限购={shopDataItem.limited})");

        gameObject.SetActive(false);

        if (SystemManager.Instance != null && SystemManager.Instance.PanelIsShowing(PanelType.ShopScreen))
        {
            ShopScreen shopScreen = SystemManager.Instance.GetPanel(PanelType.ShopScreen) as ShopScreen;
            if (shopScreen != null) shopScreen.RefreshAfterPackSoldOut();
        }
    }

    /// <summary>
    /// 购买成功后按"创建礼包的逻辑"刷新礼包展示：
    ///  - 刷新金币数（type=2 由 shopCountText 展示）；
    ///  - 按跳过规则重新生成 GiftItem 内容（已拥有永久去广告时 RemoveAds / RemoveDayAds 会被跳过）；
    ///  - 刷新限购次数与礼包标签。
    /// </summary>
    public void RefreshGiftContentAfterPurchase()
    {
        if (shopDataItem == null)
        {
            LogShopItemDetail("RefreshGiftContentAfterPurchase: shopDataItem 为空，跳过礼包刷新");
            return;
        }

        bool ownsPermanentRemoveAds = IsPermanentRemoveAdsOwned();

        LogShopItem($"购买成功刷新礼包: 已拥有永久去广告={ownsPermanentRemoveAds}, " +
                    $"productContent.Count={(shopDataItem.productContent != null ? shopDataItem.productContent.Count : 0)}");

        // 1. 金币显示（type=2 由 shopCountText 展示，type=1 由 GiftItem 展示，这里会同步刷新）
        HandleProductContentDisplay(shopDataItem);

        // 2. 礼包内容：按跳过规则重新生成 GiftItem（已拥有永久去广告 → RemoveAds/RemoveDayAds 会被跳过）
        HandleMultiProductContent(shopDataItem);

        // 3. 限购次数 / 礼包标签
        RefreshLimitCount();
        RefreshTag();
    }

    // ======================= ★ 全量刷新：去广告礼包购买后 =======================

    /// <summary>
    /// 判断当前礼包是否包含去广告内容（RemoveAds / RemoveDayAds）。
    /// </summary>
    public bool ContainsRemoveAdsContent()
    {
        if (shopDataItem == null || shopDataItem.productContent == null) return false;

        foreach (var content in shopDataItem.productContent)
        {
            if (content == null || content.Count < 2) continue;
            int type;
            if (int.TryParse(content[0], out type) && IsRemoveAdsRelatedType(type))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 刷新商店内所有包含去广告内容的礼包。
    /// 去广告礼包购买完成后，其他礼包里的「去掉广告」项也应一并隐藏（此时 IsRemoveAdsOwned() 已为 true）。
    /// </summary>
    private void RefreshAllRemoveAdsGiftPacksInShop()
    {
        if (SystemManager.Instance == null) return;

        // 商店未显示时无需刷新（下次打开商店会按新状态重建礼包内容）
        if (!SystemManager.Instance.PanelIsShowing(PanelType.ShopScreen)) return;

        ShopScreen shopScreen = SystemManager.Instance.GetPanel(PanelType.ShopScreen) as ShopScreen;
        if (shopScreen == null)
        {
            LogShopItem("RefreshAllRemoveAdsGiftPacksInShop: 未找到 ShopScreen 面板，跳过全量刷新");
            return;
        }

        shopScreen.RefreshAllRemoveAdsGiftPacks();
    }
}