using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Middleware;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 商店物品基类（无广告逻辑）。
/// 负责：通用显示（图标、价格、折扣、限时、按下缩放）、通用购买流程、
///       去广告判断、无限体力、道具特殊展示（type==1）。
///
/// 派生类：
///  - ShopItem：单一购买商品（含广告领取逻辑）
///  - ShopGiftItem：礼包商品（无广告）
///
/// ★ 购买发放跳过机制：
///   子类可 override <see cref="IsContentSkippedOnPurchase(int)"/> 指定某些内容项在购买时跳过发放。
///   例如 ShopGiftItem 在"已拥有永久去广告"时，会跳过 RemoveAds / RemoveDayAds 的发放，
///   与展示侧（InitGiftItems 跳过）保持一致。
/// </summary>
public class ShopItemBase : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    // ======================= Inspector 字段（protected 便于派生类访问） =======================

    [SerializeField] protected GameObject discountbg;       // 折扣角标
    [SerializeField] protected Image dibg;                  // 礼包底图（单物品可为空）
    [SerializeField] protected GameObject timebg;           // 限时角标
    [SerializeField] protected GameObject circle;           // 价格加载圈
    [SerializeField] protected Image shopIcon;              // 商品图标
    public Image btnAdsicon;                                // 广告图标（礼包可为空）
    [SerializeField] protected Text nameText;               // 商品名
    [SerializeField] protected Text desText;                // 商品描述（type==1 道具展示用）
    [SerializeField] protected Text shopCountText;          // 数量文本（单物品 x count / 礼包金币数）
    public Text shopPriceText;                              // 价格文本
    [SerializeField] protected GameObject tipBtnPrefab;     // 道具说明按钮 Prefab（type==1 用）

    // ======================= 状态字段 =======================

    protected ShopDataItem shopDataItem;                    // 当前绑定的商品数据
    protected ShopLimitData _shopLimitData = null;          // 当前商品的限时记录
    protected Button tipBtn;                                // 道具说明按钮实例
    protected Color _color = Color.white;                   // 描边/内容颜色（子类可覆盖）

    // ======================= 缩放参数 =======================

    [Header("缩放设置")]
    public float pressedScale = 0.9f;
    public float scaleSpeed = 10f;

    // ======================= Transform 缓存（protected 供派生类使用） =======================

    protected RectTransform rectTransform;
    protected Vector3 originalScale;
    protected bool isTransformCached = false;
    protected Vector3 originalIconPosition;
    protected Vector3 originalIconAngles;

    // ======================= 按下状态 =======================

    protected bool isPressed = false;
    protected Vector2 pressPosition;

    // ======================= 日志 =======================

    /// <summary>[Shop] 标签日志</summary>
    protected void LogShopItem(string message)
    {
        string tag = shopDataItem != null ? $"{shopDataItem.produceNameId}(id={shopDataItem.id})" : "null";
        Debug.Log($"[Shop][ShopItem] [{tag}] {message}");
    }

    // ======================= Unity 生命周期 =======================

    protected virtual void Awake()
    {
        InitTransformCache();
    }

    protected virtual void OnEnable()
    {
        isPressed = false;
        InitTransformCache(true);
        RefreshButtonInteractable();
    }

    protected virtual void Update()
    {
        if (!isTransformCached) return;

        // 按下缩放
        Vector3 targetScale = originalScale;
        if (isPressed)
        {
            targetScale = originalScale * pressedScale;
        }
        rectTransform.localScale = Vector3.Lerp(
            rectTransform.localScale,
            targetScale,
            Time.deltaTime * scaleSpeed
        );
    }

    protected virtual void OnDisable()
    {
        if (tipBtn != null)
        {
            var tippanel = tipBtn.transform.GetChild(0)?.gameObject;
            if (tippanel != null)
            {
                tippanel.SetActive(false);
            }
        }
    }

    // ======================= 指针事件 =======================

    public void OnPointerDown(PointerEventData eventData)
    {
        if (IsRemoveAdsOwnedProduct(shopDataItem))
        {
            return;
        }

        isPressed = true;
        pressPosition = eventData.position;

        EventDispatcher.instance.TriggerChangeFreeTipsPanel();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;

        float dragDistance = (eventData.position - pressPosition).magnitude;
        float dragThreshold = EventSystem.current.pixelDragThreshold;

        if (dragDistance <= dragThreshold)
        {
            OnItemClicked();
        }
    }

    // ======================= 点击处理 =======================

    protected virtual void OnItemClicked()
    {
        LogShopItem("条目被点击，执行功能");

        if (IsRemoveAdsOwnedProduct(shopDataItem))
        {
            LogShopItem("永久去广告已购买，拦截点击");
            return;
        }

        // 特殊购买方式（purchaseType == -1），交给派生类处理
        if (shopDataItem.purchaseType == -1)
        {
            HandleSpecialPurchase();
            return;
        }

        // 正常内购
        if (shopDataItem.produceNameId == "SingleGoods")
        {
            if (GameDataManager.Instance.UserData.isDayMoneyBuy)
            {
                MessageSystem.Instance.ShowTip("每日只能购买一次！");
                return;
            }
        }

        OnBuyButtonClicked(shopDataItem);
    }

    /// <summary>
    /// 特殊购买方式（purchaseType == -1）的默认处理：
    /// 基类默认按正常内购处理，派生类可 override 处理"看广告领取"、"金币购买"等。
    /// </summary>
    protected virtual void HandleSpecialPurchase()
    {
        OnBuyButtonClicked(shopDataItem);
    }

    // ======================= 数据绑定 =======================

    /// <summary>绑定商品数据并刷新所有 UI</summary>
    public virtual void SetShopData(ShopDataItem data)
    {
        if (data == null)
        {
            Debug.LogWarning("Shop data is null");
            return;
        }

        shopDataItem = data;

        InitTransformCache();
        SetShopIcon();
        HandleTimeLimitedItems(data);
        HandleDiscountDisplay(data);
        HandleProductContentDisplay(data);
        HandleSpecialTypeItems(data);
        SetProductPrice(data);
        HandleMultiProductContent(data);
        RefreshButtonInteractable();
    }

    /// <summary>刷新 UI（对象池复用时调用）</summary>
    public virtual void UpdateUI()
    {
        HandleTimeLimitedItems(shopDataItem);
        if (tipBtn != null)
        {
            var tippanel = tipBtn.transform.GetChild(0)?.gameObject;
            if (tippanel != null)
            {
                tippanel.SetActive(false);
            }
        }

        RefreshButtonInteractable();
    }

    // ======================= 显示：图标 =======================

    protected virtual void SetShopIcon()
    {
        if (shopIcon == null) return;

        var icon = LoadShopIcon(shopDataItem.showIcon);
        if (icon != null)
        {
            shopIcon.sprite = icon;
        }
    }

    // ======================= 显示：限时 =======================

    protected virtual void HandleTimeLimitedItems(ShopDataItem data)
    {
        if (timebg == null) return;

        _shopLimitData = GameDataManager.Instance.UserData.limitShopItems?
            .Find(item => item.nameid == data.produceNameId);

        bool isTimeActive = _shopLimitData != null
                            && !string.IsNullOrEmpty(_shopLimitData.endtime)
                            && _shopLimitData.isopen;

        if (isTimeActive)
        {
            timebg.gameObject.SetActive(true);
            StartCoroutine(UpdateTime());
        }
        else
        {
            timebg.gameObject.SetActive(false);
        }

        OnTimeLimitedStateChanged(isTimeActive);
    }

    /// <summary>限时状态变化时调用（基类空实现，派生类可 override）</summary>
    protected virtual void OnTimeLimitedStateChanged(bool isActive) { }

    // ======================= 显示：折扣 =======================

    protected virtual void HandleDiscountDisplay(ShopDataItem data)
    {
        if (discountbg == null) return;

        bool hasDiscount = !string.IsNullOrEmpty(data.discount);
        discountbg.SetActive(hasDiscount);

        if (hasDiscount)
        {
            var discountText = discountbg.GetComponentInChildren<Text>();
            if (discountText != null && MultilingualManager.Instance != null)
            {
                discountText.text = $"{data.discount}";
            }
        }
    }

    // ======================= 显示：内容（派生类 override） =======================

    /// <summary>
    /// 显示商品内容（派生类 override）。
    ///  - ShopItem：显示 "x count"；
    ///  - ShopGiftItem：显示礼包名 + 礼包底图 + 金币数量。
    /// </summary>
    protected virtual void HandleProductContentDisplay(ShopDataItem data) { }

    // ======================= 显示：道具特殊展示（type == 1） =======================

    protected virtual void HandleSpecialTypeItems(ShopDataItem data)
    {
        if (data.type != 1) return;

        shopCountText?.gameObject.SetActive(false);

        if (desText != null)
        {
            desText.text = MultilingualManager.Instance?.GetString(data.des) ?? data.des;
        }

        if (nameText != null)
        {
            nameText.text = MultilingualManager.Instance?.GetString(data.name) ?? data.name;
        }

        LoadAndSetupTipButton(data);
    }

    protected virtual void LoadAndSetupTipButton(ShopDataItem data)
    {
        if (AdvancedBundleLoader.SharedInstance == null) return;

        tipBtnPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "Shop_tipbtn");
        if (tipBtnPrefab == null || nameText == null) return;

        if (tipBtn == null)
        {
            tipBtn = Instantiate(tipBtnPrefab, nameText.transform).GetComponent<Button>();
        }

        var tippanel = tipBtn.transform.GetChild(0)?.gameObject;
        if (tippanel != null)
        {
            tippanel.SetActive(false);

            var tipText = tippanel.GetComponentInChildren<Text>();
            if (tipText != null)
            {
                tipText.text = MultilingualManager.Instance?.GetString(data.pointDes) ?? data.pointDes;
            }

            tipBtn.onClick.AddListener(() => ClickShopItemtipBtn(tippanel));
        }
    }

    private void ClickShopItemtipBtn(GameObject tippanel)
    {
        if (!ShopManager.shopManager.shopItemsTipsPanel.ContainsKey(shopDataItem.id))
        {
            ShopManager.shopManager.shopItemsTipsPanel.Add(shopDataItem.id, tippanel);
        }

        if (ShopManager.shopManager.shopItemsTipsPanel.Count > 0)
        {
            foreach (var item in ShopManager.shopManager.shopItemsTipsPanel)
            {
                if (item.Key != shopDataItem.id)
                {
                    item.Value.gameObject.SetActive(false);
                }
            }
        }

        tippanel.SetActive(!tippanel.activeSelf);
    }

    // ======================= 显示：价格 =======================

    protected virtual void SetProductPrice(ShopDataItem data)
    {
        if (shopPriceText == null) return;

        LogShopItem($"获取商品内购名称: {data.GetProduceName()}");

        try
        {
            if (IsRemoveAdsOwnedProduct(data))
            {
                shopPriceText.text = "已购买";
                ShowPriceLoadingState(false);
                LogShopItem("永久去广告已购买，显示'已购买'");
                return;
            }

            float price = data.price;
            CultureInfo culture = UIUtilities.GetCultureForCurrency("");
            if (shopDataItem.produceNameId == "FreeGoods")
            {
                 shopPriceText.text = "免费";
            } else if (shopDataItem.produceNameId == "SingleGoods")
            {
                if (!GameDataManager.Instance.UserData.isDayMoneyBuy)
                    shopPriceText.text = UIUtilities.FormatCurrency(price, culture);
                else
                    shopPriceText.text = "已购买";
            }
            else if (shopDataItem.produceNameId == "GoldGoods")
            {
                if (!GameDataManager.Instance.UserData.isDayGoldBuy)
                    shopPriceText.text = data.price.ToString();
                else
                    shopPriceText.text = "已购买";
            }
            else
            {
                shopPriceText.text = UIUtilities.FormatCurrency(price, culture);
            }

            ShowPriceLoadingState(false);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error setting product price: {ex.Message}");
            ShowPriceLoadingState(true);
        }
    }

    protected void ShowPriceLoadingState(bool isLoading)
    {
        if (circle != null) circle.gameObject.SetActive(isLoading);
        if (shopPriceText != null) shopPriceText.gameObject.SetActive(!isLoading);
    }

    // ======================= 去广告判断 =======================

    protected bool IsRemoveAdsOwnedProduct(ShopDataItem data)
    {
        if (data == null) return false;
        if (data.produceNameId != ShopManager.ProductRemoveAds) return false;
        return ShopManager.shopManager != null && ShopManager.shopManager.IsRemoveAdsOwned();
    }

    protected virtual void RefreshButtonInteractable()
    {
        var button = GetComponent<Button>();
        if (button == null) return;

        if (shopDataItem == null)
        {
            button.interactable = true;
            return;
        }

        bool owned = IsRemoveAdsOwnedProduct(shopDataItem);
        button.interactable = !owned;

        if (owned)
        {
            shopPriceText.text = "已购买";
            ShowPriceLoadingState(false);
            LogShopItem("永久去广告已购买，按钮置灰并显示'已购买'");
        }
    }

    // ======================= Transform 缓存 =======================

    protected void InitTransformCache(bool forceRefresh = false)
    {
        if (!forceRefresh && isTransformCached) return;

        rectTransform = GetComponent<RectTransform>();
        originalScale = transform.localScale;

        if (btnAdsicon != null)
        {
            originalIconPosition = btnAdsicon.transform.localPosition;
            originalIconAngles = btnAdsicon.transform.localEulerAngles;
        }

        isTransformCached = true;
    }

    // ======================= 礼包内容（派生类 override） =======================

    /// <summary>
    /// 礼包内容展示（基类空实现，ShopGiftItem override）。
    /// 用于创建多个 GiftItem 子项。
    /// </summary>
    protected virtual void HandleMultiProductContent(ShopDataItem data) { }

    // ======================= ★ 购买发放跳过（派生类 override） =======================

    /// <summary>
    /// 判断商品内容中的某一项在购买发放时是否需要跳过。
    /// 默认不跳过（false），派生类可 override 用于特殊场景：
    ///  - 例如 ShopGiftItem 在"已拥有永久去广告"时，跳过 RemoveAds / RemoveDayAds 的发放，
    ///    与展示侧（InitGiftItems 跳过）保持一致；
    ///  - 跳过仅影响发放，不影响展示和购买流程本身。
    /// </summary>
    /// <param name="giftType">productContent 中的道具类型（如 RemoveAds / RemoveDayAds / Coins / Butterfly / ...）</param>
    protected virtual bool IsContentSkippedOnPurchase(int giftType)
    {
        return false;
    }

    // ======================= 限时倒计时 =======================

    private string Gettime()
    {
        DateTime startTime = DateTime.Parse(_shopLimitData.endtime);
        TimeSpan timeSpan = startTime.Subtract(DateTime.Now);

        if (timeSpan.TotalMinutes > 0 && timebg != null)
        {
            timebg.GetComponentInChildren<Text>().text = UIUtilities.FormatTimeRemaining(timeSpan);
        }

        return timeSpan.TotalMinutes.ToString();
    }

    private IEnumerator UpdateTime()
    {
        yield return new WaitForSeconds(0.2f);
        string time = Gettime();
        while (true)
        {
            time = Gettime();
            if (string.IsNullOrEmpty(time))
            {
                _shopLimitData.isopen = false;
                transform.gameObject.SetActive(false);
                break;
            }

            yield return new WaitForSeconds(10);
        }
    }

    // ======================= 购买流程 =======================

    protected virtual void OnBuyButtonClicked(ShopDataItem data)
    {
        if (IsRemoveAdsOwnedProduct(data))
        {
            LogShopItem("永久去广告已购买，拦截购买");
            return;
        }

        MessageSystem.Instance.ShowLoadingAnimation();

        if (UIUtilities.isEditMode)
        {
            ProductItem productItem = new ProductItem
            {
                order_id = "",
                IsoCurrencyCode = "",
                ItemName = data.produceNameId,
                ProductId = data.produceNameId,
                LocalizedPrice = data.price * 100,
            };
            OnPurchaseSuccess(productItem);
        }
        else
        {
            AnalyticMgr.PurchaseStart(data.produceNameId);
            Game.self.Shop.Purchase(data.GetProduceName(), OnPurchaseSuccess, OnPurchaseFailed);
        }
    }

    protected virtual void OnPurchaseSuccess(ProductItem item)
    {
        LogShopItem($"购买成功: {item.ProductId}");

        bool hasPermanentRemoveAds = false;

        if (shopDataItem.GetProduceName() == item.ProductId)
        {
            Game.self.Shop.CurrentShopDataItem = shopDataItem.DeepCopy();

            foreach (var dataitem in shopDataItem.productContent)
            {
                int count = int.Parse(dataitem[1]);
                int type = int.Parse(dataitem[0]);

                // ★ 派生类可指定跳过某些项的发放（如已拥有永久去广告时跳过 RemoveAds/RemoveDayAds）
                if (IsContentSkippedOnPurchase(type))
                {
                    LogShopItem($"购买发放跳过 content type={type}（IsContentSkippedOnPurchase=true）");
                    continue;
                }

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
                        hasPermanentRemoveAds = true;
                        HandleRemoveAdsPurchaseInternal((int)LimitRewordType.RemoveAds, 0);
                        break;
                    case (int)LimitRewordType.RemoveDayAds:
                        hasPermanentRemoveAds = false;
                        HandleRemoveAdsPurchaseInternal((int)LimitRewordType.RemoveDayAds, count * 24);
                        break;
                    case (int)LimitRewordType.ReStartTool:
                        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.ReStartTool, count, "商店购买" + item.ItemName);
                        break;
                    case (int)LimitRewordType.InfiniteEnergy:
                        HandleInfiniteEnergyPurchase(count);
                        break;
                    default:
                        Debug.LogWarning($"[Shop] 未处理的商品类型 type={type} count={count}");
                        break;
                }
            }

            if (hasPermanentRemoveAds)
                SystemManager.Instance.ShowPanel(PanelType.RemoveAdsScreen);
            else
                SystemManager.Instance.ShowPanel(PanelType.AwardScreen);
        }

        ShopManager.shopManager.paysuccess = true;
        bool firstPay = GameDataManager.Instance.UserData.TotalPayTimes == 0;
        if (firstPay)
            GameDataManager.Instance.UserData.firstPayTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        GameDataManager.Instance.UserData.lastPayTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        GameDataManager.Instance.UserData.LastPayTimeTicks = DateTime.Now.Ticks;
        GameDataManager.Instance.UserData.TotalPayTimes++;
        GameDataManager.Instance.UserData.TotalRevenue += item.LocalizedPrice;
        DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedShopBuy, 1);

        if (item.LocalizedPrice > GameDataManager.Instance.UserData.MaxSinglePayAmount)
        {
            GameDataManager.Instance.UserData.MaxSinglePayAmount = item.LocalizedPrice;
            LogShopItem($"更新单次付费最大金额: MaxSinglePayAmount={GameDataManager.Instance.UserData.MaxSinglePayAmount}");
        }

        if (!UIUtilities.isEditMode)
        {
            AnalyticMgr.PurchaseFinished(item, firstPay);
#if UNITY_HUAWEI
            item?.OnShipmentCompleted(true);
#endif
        }

        if (shopDataItem.produceNameId == "SingleGoods")
        {
            if (!GameDataManager.Instance.UserData.isDayMoneyBuy)
            {
                GameDataManager.Instance.UserData.isDayMoneyBuy = true;
                shopPriceText.text = "已购买";
            }
        }

        ShopLimitData limitData =
            GameDataManager.Instance.UserData.limitShopItems.Find((x) => x.nameid == shopDataItem.produceNameId);
        if (limitData != null)
        {
            limitData.isget = true;
            limitData.buyCount += 1;
        }

        if (ShopManager.shopManager != null)
        {
            string beforeLayer = ShopManager.shopManager.GetCurrentUserLayer();
            ShopManager.shopManager.RefreshLayer();
            string afterLayer = ShopManager.shopManager.GetCurrentUserLayer();

            LogShopItem($"[UserLayer] 购买成功触发分层刷新: " +
                        $"TotalPayTimes={GameDataManager.Instance.UserData.TotalPayTimes}, " +
                        $"MaxSinglePayAmount={GameDataManager.Instance.UserData.MaxSinglePayAmount}, " +
                        $"分层 {beforeLayer} → {afterLayer}");
        }

        MessageSystem.Instance.HideLoadingAnimation();
        RefreshButtonInteractable();
    }

    protected virtual void OnPurchaseFailed(string error)
    {
        LogShopItem($"购买失败: {error}");
        AnalyticMgr.PurchaseFailed(shopDataItem.GetProduceName(), error);
        MessageSystem.Instance.HideLoadingAnimation();
    }

    // ======================= 去广告处理 =======================

    private void HandleRemoveAdsPurchaseInternal(int adstype, int hours)
    {
        bool permanent = adstype == (int)LimitRewordType.RemoveAds;

        LogShopItem($"处理去广告购买: adstype={adstype}, permanent={permanent}, hours={hours}");

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
                LogShopItem($"永久去广告续期: 记录已更新 (id={exist.id})");
            }
            else
            {
                DateTime baseEnd = DateTime.Now;
                DateTime oldEnd = exist.GetEndTime();
                if (oldEnd != DateTime.MinValue && oldEnd > DateTime.Now)
                    baseEnd = oldEnd;

                DateTime newEnd = baseEnd.AddHours(hours);
                exist.endtime = newEnd.ToString("yyyy-MM-dd HH:mm:ss");
                exist.isget = true;
                exist.isopen = false;
                exist.isoverdate = false;
                exist.gettime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                LogShopItem($"限时去广告续期: 原结束={oldEnd:yyyy-MM-dd HH:mm:ss}, 新结束={newEnd:yyyy-MM-dd HH:mm:ss}");
            }
        }
        else
        {
            DateTime newEnd = permanent ? DateTime.MinValue : DateTime.Now.AddHours(hours);
            ShopLimitData newData = new ShopLimitData()
            {
                id = shopDataItem.id,
                nameid = shopDataItem.produceNameId,
                endtime = permanent ? "" : newEnd.ToString("yyyy-MM-dd HH:mm:ss"),
                isopen = false,
                gettime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                adstype = adstype,
                isget = true,
                isoverdate = false,
            };
            GameDataManager.Instance.UserData.limitShopItems.Add(newData);
            LogShopItem($"新增去广告记录: adstype={adstype}, permanent={permanent}, endtime={(permanent ? "永久" : newEnd.ToString("yyyy-MM-dd HH:mm:ss"))}");
        }

        if (AdRuleManager.Instance != null)
        {
            AdRuleManager.Instance.HideBannerAd();
        }

        if (ShopManager.shopManager != null)
        {
            ShopManager.shopManager.UpdateAdsBtnUIEvent(permanent ? null : DateTime.Now.ToString(), true);
            ShopManager.shopManager.RefreshLimitData();
        }

        var curBtn = transform.GetComponent<Button>();
        if (curBtn != null && permanent)
        {
            curBtn.interactable = false;
        }
    }

    // ======================= 无限体力 =======================

    private void HandleInfiniteEnergyPurchase(int hours)
    {
        if (hours <= 0)
        {
            LogShopItem($"HandleInfiniteEnergyPurchase 跳过: hours={hours}");
            return;
        }

        var userData = GameDataManager.Instance.UserData;

        double beforeRemain = userData.GetUnlimitedEnergyRemainSeconds();
        DateTime newEnd = userData.AddUnlimitedEnergy(hours);
        double afterRemain = userData.GetUnlimitedEnergyRemainSeconds();

        LogShopItem($"无限体力购买: +{hours}h, " +
                    $"原剩余={TimeSpan.FromSeconds(beforeRemain):hh\\:mm\\:ss}, " +
                    $"新剩余={TimeSpan.FromSeconds(afterRemain):hh\\:mm\\:ss}, " +
                    $"新结束={newEnd:yyyy-MM-dd HH:mm:ss}");

        EventDispatcher.instance.TriggerUpdateLayerCoin(false, true);
    }

    // ======================= 工具方法 =======================

    protected Sprite LoadShopIcon(string showIcon)
    {
        Sprite sprite = null;
        sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(showIcon);

        return sprite;
    }
}