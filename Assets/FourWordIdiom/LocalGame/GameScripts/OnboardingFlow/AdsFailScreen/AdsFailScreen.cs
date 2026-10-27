using System;
using System.Collections;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 广告播放失败弹窗（AdsFailScreen / PanelType.AdFailed）。
/// 当激励视频达到「假失败」阈值（分层策略 S_F_T_A_B_C）时由
/// ShopManager.ShowAdFailedPanel 弹出，提供两条转化路径：
///
///   1. 分享换奖励（需求 C-STRAT-06）：分享后切回前台且间隔 > 3 秒判成功，
///      发灯泡1/复活1/金币50（ShopManager.OnShareReturned），
///      每天限 F 次，达限后隐藏分享按钮；
///   2. 单道具商品按钮（需求 C-STRAT-07）：按触发道具类型映射商品
///      （灯泡→SingleGoods、复活→SingleClockS、金币→Gold6），
///      点击直接调起系统支付，购买成功复用 ShopManager.OnPurchaseSuccess（发奖 + AwardScreen）。
///
/// 弹出时由 ShopManager 记录触发道具类型（LastAdFailedToolType），本面板据此切换内容。
/// </summary>
public class AdsFailScreen : UIWindow
{
    [Header("基础")]
    [SerializeField] private Button closeBtn;       // CloseBtn：关闭
    [SerializeField] private Text title;            // HeaderText：标题
    [SerializeField] private Text descText;         // 描述文案
    [SerializeField] private Image toolIcon;        // 道具主图标（Icon）
    [SerializeField] private Image toolIcon2;       // 道具次图标（icon，可选）

    [Header("路径1：分享换奖励")]
    [SerializeField] private Button ShareBtn;       // 分享按钮（每天 F 次，达限隐藏）
    [SerializeField] private Text shareBtnText;     // 分享按钮文案（shareText）
    [SerializeField] private Text countText;        // 剩余分享次数 / 商品数量（countText）

    [Header("路径2：单道具商品")]
    [SerializeField] private Button BuyBtn;         // 单道具商品按钮
    [SerializeField] private Text priceText;        // 商品价格（price）
    [SerializeField] private Text discountText;     // 折扣标签（discountText，取内购配置表）
    [SerializeField] private Transform discountTable; // 折扣标签容器（discountbg）

    // ======================= 分享状态（仅本面板生命周期内） =======================

    private bool _sharePending;        // 已点击分享、等待切回前台判定
    private bool _shareFocusLost;      // 分享后是否真的切出过前台
    private float _shareClickRealtime; // 点击时刻（realtimeSinceStartup）

    /// <summary>分享成功判定最小间隔（秒），与 ShopManager.ShareSuccessMinSeconds 保持一致</summary>
    private const float ShareSuccessMinSeconds = 3f;

    protected override void OnEnable()
    {
        base.OnEnable();
        ResetShareState();
        InitUI();
    }

    protected override void InitializeUIComponents()
    {
        if (closeBtn != null) closeBtn.AddVibraClickAction(OnCloseClick);
        if (BuyBtn != null) BuyBtn.AddVibraClickAction(OnBuyClick);
        if (ShareBtn != null) ShareBtn.AddVibraClickAction(OnShareClick);
    }

    /// <summary>Android 分享面板切走前台时记录（供切回判定「真的离开过」）。</summary>
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            if (_sharePending) _shareFocusLost = true;
            return;
        }

        // 切回前台：判定分享是否成功
        if (!_sharePending) return;
        _sharePending = false;

        bool success = _shareFocusLost
                       && (Time.realtimeSinceStartup - _shareClickRealtime) >= ShareSuccessMinSeconds;

        if (ShopManager.shopManager != null)
            ShopManager.shopManager.OnShareReturned(success); // 内部校验每日上限/间隔并发奖

        if (!success)
            MessageSystem.Instance.ShowTip(GetLocalized("ShareFailTip", "分享失败，再试试吧"));

        RefreshShareUI(); // 成功后刷新剩余次数/按钮显隐
    }

    private void OnDisable()
    {
        ResetShareState();
    }

    // ======================= UI 初始化 =======================

    private void InitUI()
    {
        LimitRewordType toolType = ShopManager.shopManager != null
            ? ShopManager.shopManager.LastAdFailedToolType
            : LimitRewordType.Tipstool;

        if (title != null)
            title.text = GetLocalized("AdFailedTitle", "换种方式");

        if (descText != null)
            descText.text = GetLocalized("AdFailedDes", "视频暂不可用，换种方式获取吧");

        // 道具图标按触发类型切换
        Sprite iconSprite = GetSprite(toolType);
        if (iconSprite != null)
        {
            if (toolIcon != null) toolIcon.sprite = iconSprite;
            if (toolIcon2 != null) toolIcon2.sprite = iconSprite;
        }

        RefreshGoodsUI(toolType);
        RefreshShareUI();

        if (closeBtn != null)
        {
            var btnText = closeBtn.GetComponentInChildren<Text>();
            if (btnText != null)
                btnText.text = GetLocalized("Close", "关闭");
        }
    }

    /// <summary>分享按钮：每天 F 次，达限（或策略关闭）后隐藏。</summary>
    private void RefreshShareUI()
    {
        if (ShareBtn == null) return;

        bool available = ShopManager.shopManager != null
                         && ShopManager.shopManager.IsShareRewardAvailable();
        ShareBtn.gameObject.SetActive(available);

        if (available && countText != null && ShopManager.shopManager != null)
        {
            var p = ShopManager.shopManager.GetAdFailedParams();
            countText.gameObject.SetActive(true);
            countText.text = string.Format(GetLocalized("ShareRemainFormat", "今日剩余 {0} 次"), p.shareDailyLimit);
        }
        else if (countText != null && !available)
        {
            countText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 单道具商品按钮：按触发道具类型映射商品并显示价格/折扣/图标。
    /// 灯泡→SingleGoods，复活→SingleClockS，商店金币→Gold6（需求 C-STRAT-07）。
    /// 商品未在商店表配置时隐藏按钮。
    /// </summary>
    private void RefreshGoodsUI(LimitRewordType toolType)
    {
        ShopDataItem item = GetContextGoodsItem(toolType);
        if (item == null)
        {
            if (BuyBtn != null) BuyBtn.gameObject.SetActive(false);
            if (discountText != null) discountText.gameObject.SetActive(false);
            if (discountTable != null) discountTable.gameObject.SetActive(false);
            if (priceText != null) priceText.gameObject.SetActive(false);
            return;
        }

        if (BuyBtn != null) BuyBtn.gameObject.SetActive(true);

        if (priceText != null)
        {
            priceText.gameObject.SetActive(item.price > 0);
            priceText.text = item.price > 0 ? string.Format("¥{0:0.##}", item.price) : "";
        }

        // 折扣标签文案取内购配置表（discountZh 优先）
        if (discountText != null)
        {
            string discount = !string.IsNullOrEmpty(item.discountZh) ? item.discountZh : item.discount;
            bool hasDiscount = !string.IsNullOrEmpty(discount);
            discountText.gameObject.SetActive(hasDiscount);
            if (hasDiscount) discountText.text = discount;
        }
        if (discountTable != null && discountText == null)
            discountTable.gameObject.SetActive(!string.IsNullOrEmpty(item.discountZh) || !string.IsNullOrEmpty(item.discount));

        // 商品内容数量（如「灯泡 x1」），无内容时隐藏
        if (countText != null && ShareBtn != null && !ShareBtn.gameObject.activeSelf)
        {
            int count = GetGoodsMainCount(item, toolType);
            countText.gameObject.SetActive(count > 0);
            if (count > 0)
                countText.text = string.Format("x{0}", count);
        }
    }

    /// <summary>根据 ShopManager 记录的道具类型取对应单道具商品配置。</summary>
    private ShopDataItem GetContextGoodsItem(LimitRewordType toolType)
    {
        if (ShopManager.shopManager == null) return null;

        string productId;
        switch (toolType)
        {
            case LimitRewordType.ReStartTool: productId = ShopManager.ProductSingleClock; break; // 复活
            case LimitRewordType.Coins:       productId = ShopManager.ProductGold6;      break; // 商店金币
            default:                          productId = ShopManager.ProductSingleGoods; break; // 灯泡（默认）
        }

        return ShopManager.shopManager.GetShopItemByProduceName(productId);
    }

    /// <summary>取商品内容中与触发道具类型对应的主道具数量（展示用）。</summary>
    private int GetGoodsMainCount(ShopDataItem item, LimitRewordType toolType)
    {
        if (item == null || item.productContent == null) return 0;
        foreach (var row in item.productContent)
        {
            if (row == null || row.Count < 2) continue;
            int type;
            int count;
            if (int.TryParse(row[0], out type) && int.TryParse(row[1], out count))
            {
                if (type == (int)toolType) return count;
            }
        }
        return 0;
    }

    // ======================= 事件 =======================

    private void OnCloseClick()
    {
        base.Close();
    }

    /// <summary>点击单道具商品：直接调起系统支付；成功走 ShopManager.OnPurchaseSuccess（发奖+AwardScreen）。</summary>
    private void OnBuyClick()
    {
        LimitRewordType toolType = ShopManager.shopManager != null
            ? ShopManager.shopManager.LastAdFailedToolType
            : LimitRewordType.Tipstool;

        ShopDataItem item = GetContextGoodsItem(toolType);
        if (item == null || Game.self == null || Game.self.Shop == null)
        {
            MessageSystem.Instance.ShowTip(GetLocalized("GoodsNotFoundTip", "商品暂不可用"));
            return;
        }

        Debug.Log($"[AdsFail] 发起单道具购买: {item.produceNameId}, price={item.price}");
        Game.self.Shop.Purchase(
            item.GetProduceName(),
            productItem =>
            {
                // 复用商店统一的购买成功处理（发放内容 + 内购成功弹窗 AwardScreen + 购买记录）
                if (ShopManager.shopManager != null)
                    ShopManager.shopManager.OnPurchaseSuccess(productItem);
                Close();
            },
            error =>
            {
                Debug.Log($"[AdsFail] 单道具购买失败/取消: {item.produceNameId}, error={error}");
                // 取消/失败不弹额外窗：取消购买弹窗由 ShopManager.OnPurchaseCanceled 统一调度
            });
    }

    /// <summary>点击分享：记录时刻 → 调起系统分享 → 等切回前台判定（OnApplicationFocus）。</summary>
    private void OnShareClick()
    {
        _sharePending = true;
        _shareFocusLost = false;
        _shareClickRealtime = Time.realtimeSinceStartup;

        if (ShopManager.shopManager != null)
            ShopManager.shopManager.OnShareClicked();

        DoNativeShare();
    }

    // ======================= 系统分享（无插件，Android Intent） =======================

    private void DoNativeShare()
    {
        string shareText = GetLocalized("ShareText", "我在玩《成语消：禅意之境》，一起来消成语吧！");

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent"))
            {
                intent.Call<AndroidJavaObject>("setAction", "android.intent.action.SEND");
                intent.Call<AndroidJavaObject>("setType", "text/plain");
                intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", shareText);
                using (AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent"))
                using (AndroidJavaObject chooser = intentClass.CallStatic<AndroidJavaObject>(
                    "createChooser", intent, GetLocalized("ShareTitle", "分享到")))
                {
                    activity.Call("startActivity", chooser);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[AdsFail] 调起系统分享失败: {e.Message}");
            MessageSystem.Instance.ShowTip(GetLocalized("ShareFailTip", "分享失败，再试试吧"));
            _sharePending = false;
        }
#elif UNITY_IOS && !UNITY_EDITOR
        // iOS 分享待接入（Social API / ShareSheet）；先按失败提示，避免卡判定
        Debug.Log("[AdsFail] iOS 分享未接入，走失败分支");
        MessageSystem.Instance.ShowTip(GetLocalized("ShareFailTip", "分享失败，再试试吧"));
        _sharePending = false;
#else
#if UNITY_EDITOR
        // 编辑器：无法真实分享，3.5 秒后自动按成功判定，方便验证发奖链路
        Debug.Log("[AdsFail] 编辑器环境模拟分享成功（3.5秒后）");
#else
        // 其他平台（Windows/鸿蒙/WebGL 等）无原生分享实现：直接按成功判定，避免卡分享流程
        Debug.Log("[AdsFail] 当前平台无原生分享实现，直接按分享成功判定");
        if (_sharePending)
        {
            _sharePending = false;
            if (ShopManager.shopManager != null)
                ShopManager.shopManager.OnShareReturned(true);
            RefreshShareUI();
        }
#endif
#endif
    }

    private void ResetShareState()
    {
        _sharePending = false;
        _shareFocusLost = false;
    }

    // ======================= 工具 =======================

    /// <summary>按道具类型取图集图标（沿用工程图集名）。</summary>
    private Sprite GetSprite(LimitRewordType type)
    {
        switch (type)
        {
            case LimitRewordType.Coins: return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("gold0");
            case LimitRewordType.Butterfly: return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("UI_Icon_Butterfly");
            case LimitRewordType.Tipstool: return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("tipicon");
            case LimitRewordType.AutoComplete: return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("rocket");
            case LimitRewordType.ReStartTool: return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("give_3");
            case LimitRewordType.InfiniteEnergy: return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("InfiniteEnergy");
            case LimitRewordType.RemoveDayAds: return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("shopads");
            default: return null;
        }
    }

    /// <summary>多语言取值，缺失时回退中文兜底。</summary>
    private string GetLocalized(string key, string fallback)
    {
        if (MultilingualManager.Instance == null) return fallback;
        string value = MultilingualManager.Instance.GetString(key);
        if (string.IsNullOrEmpty(value) || value == key) return fallback;
        return value;
    }
}
