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
///
/// ★ 购买记录：
///   OnPurchaseSuccess 成功分支内会调用 RecordPurchase(item) 写入 UserData.purchaseRecords，
///   由 PurchaseRecordScreen（或 BuyListScreen）读取展示：礼包名称、价格、礼包内容、购买时间。
///   与 GiftTable.RecordPurchase 使用同一份数据结构与快照规则。
/// </summary>
public class ShopItemBase : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    // ======================= Inspector 字段（protected 便于派生类访问） =======================

    [SerializeField] protected GameObject discountbg;       // 折扣角标
    [SerializeField] protected Image dibg;                  // 礼包底图（单物品可为空）
    [SerializeField] protected GameObject timebg;           // 限时角标
    [SerializeField] protected GameObject circle;           // 价格加载圈
    [SerializeField] protected Image shopIcon;              // 商品图标
    public Image btnTagicon;                                // 广告图标/金币图标（礼包可为空）
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

    /// <summary>限时倒计时协程句柄（用于去重，避免重复 StartCoroutine 叠出多条常驻协程）</summary>
    private Coroutine _timeCoroutine;

    /// <summary>当前这条倒计时是不是「活动礼包」的限时（到点后卡片保留、只收起限时与特惠标签）</summary>
    private bool _timeCountdownIsActivity;

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

    /// <summary>[Shop] 标签日志（关键事件，默认开启）</summary>
    protected void LogShopItem(string message)
    {
        LogManager.ShopLog(BuildShopItemLog(message));
    }

    /// <summary>
    /// [Shop] 逐卡细节日志（价格布局 / 描边 / 限购次数 / 礼包内容逐项）。
    /// 默认关闭（LogManager.enableShopVerboseLog）：这类日志每次刷新每张卡都会打，量很大。
    /// </summary>
    protected void LogShopItemDetail(string message)
    {
        LogManager.ShopLog(BuildShopItemLog(message));
    }

    private string BuildShopItemLog(string message)
    {
        string tag = shopDataItem != null ? $"{shopDataItem.produceNameId}(id={shopDataItem.id})" : "null";
        return $"[ShopItem] [{tag}] {message}";
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
        if (!isTransformCached || rectTransform == null) return;

        // 按下缩放
        Vector3 targetScale = isPressed ? originalScale * pressedScale : originalScale;
        Vector3 currentScale = rectTransform.localScale;

        // ★ 已经到位就直接返回：原来这里每帧都写一次 localScale，
        //   15 张卡 × 每帧一次 Transform 写入，属于纯浪费（还会持续把层级标脏）。
        if ((targetScale - currentScale).sqrMagnitude <= ScaleEpsilon) return;

        Vector3 nextScale = Vector3.Lerp(currentScale, targetScale, Time.deltaTime * scaleSpeed);
        // 逼近到阈值内直接吸附到目标，避免无限逼近导致「每帧都在写」
        if ((targetScale - nextScale).sqrMagnitude <= ScaleEpsilon) nextScale = targetScale;

        rectTransform.localScale = nextScale;
    }

    /// <summary>缩放收敛阈值（平方比较，避免开根号）</summary>
    private const float ScaleEpsilon = 0.000001f;

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
            LogShopItemDetail("永久去广告已购买，拦截点击");
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

        // ★ 活动礼包（特惠礼包）：限时 = 本次节日/周末结束时间，不走权益 endtime
        bool isActivityTime = ShopManager.shopManager != null
                              && ShopManager.shopManager.IsActivityPackActive(data);

        bool isTimeActive = isActivityTime
                            || (_shopLimitData != null
                                && !string.IsNullOrEmpty(_shopLimitData.endtime)
                                && _shopLimitData.isopen);

        _timeCountdownIsActivity = isActivityTime;

        if (isTimeActive)
        {
            timebg.gameObject.SetActive(true);
            // ★ 去重：刷新（SetShopData / UpdateUI）会被反复调用，
            //   原来每次都会再起一条 while(true) 的倒计时协程，条数越叠越多
            if (_timeCoroutine != null) StopCoroutine(_timeCoroutine);
            _timeCoroutine = StartCoroutine(UpdateTime());
        }
        else
        {
            if (_timeCoroutine != null)
            {
                StopCoroutine(_timeCoroutine);
                _timeCoroutine = null;
            }
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

        LogShopItemDetail($"获取商品内购名称: {data.GetProduceName()}");

        try
        {
            if (IsRemoveAdsOwnedProduct(data))
            {
                shopPriceText.text = "已购买";
                ShowPriceLoadingState(false);
                LogShopItemDetail("永久去广告已购买，显示'已购买'");
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
                {
                    shopPriceText.text = "已购买";
                    discountbg.gameObject.SetActive(false);
                }
            }
            else if (shopDataItem.produceNameId == "GoldGoods")
            {
                if (!GameDataManager.Instance.UserData.isDayGoldBuy)
                    shopPriceText.text = data.price.ToString();
                else
                {
                    shopPriceText.text = "已购买";
                    btnTagicon.gameObject.SetActive(false);
                }
                    
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
            LogShopItemDetail("永久去广告已购买，按钮置灰并显示'已购买'");
        }
    }

    // ======================= Transform 缓存 =======================

    protected void InitTransformCache(bool forceRefresh = false)
    {
        if (!forceRefresh && isTransformCached) return;

        rectTransform = GetComponent<RectTransform>();
        originalScale = transform.localScale;

        if (btnTagicon != null)
        {
            originalIconPosition = btnTagicon.transform.localPosition;
            originalIconAngles = btnTagicon.transform.localEulerAngles;
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

    /// <summary>
    /// 当前限时倒计时的截止时间：
    ///  - 活动礼包 → 本次连续节日/周末的结束时间（需求：限时 = 活动截至时间 - 当前时间）；
    ///  - 其它 → 权益/限时礼包自己的 endtime。
    /// </summary>
    private DateTime GetTimeLimitedEndTime()
    {
        if (shopDataItem != null
            && ShopManager.shopManager != null
            && ShopManager.shopManager.IsActivityPackActive(shopDataItem))
        {
            return ShopManager.shopManager.GetActivityPackEndTime(shopDataItem);
        }

        if (_shopLimitData == null || string.IsNullOrEmpty(_shopLimitData.endtime))
            return DateTime.MinValue;

        DateTime end;
        return DateTime.TryParse(_shopLimitData.endtime, out end) ? end : DateTime.MinValue;
    }

    private string Gettime()
    {
        DateTime end = GetTimeLimitedEndTime();
        if (end == DateTime.MinValue) return "";

        TimeSpan timeSpan = end.Subtract(DateTime.Now);
        if (timeSpan.TotalSeconds <= 0) return "";

        if (timebg != null)
        {
            Text timeText = timebg.GetComponentInChildren<Text>();
            if (timeText != null)
                timeText.text = UIUtilities.FormatTimeRemaining(timeSpan);
        }

        return timeSpan.TotalMinutes.ToString();
    }

    private IEnumerator UpdateTime()
    {
        yield return new WaitForSeconds(0.2f);
        while (true)
        {
            string time = Gettime();
            if (string.IsNullOrEmpty(time))
            {
                // ★ 活动礼包限时结束：卡片继续留在推荐位（退化成普通礼包），只收起限时与特惠标签
                if (_timeCountdownIsActivity)
                {
                    _timeCountdownIsActivity = false;
                    if (timebg != null) timebg.gameObject.SetActive(false);
                    OnTimeLimitedStateChanged(false);
                    if (shopDataItem != null) SetShopData(shopDataItem);
                    LogShopItemDetail("活动礼包限时结束，退回普通礼包展示（名称/标签/限时已刷新）");
                    break;
                }

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

            // ★ 编辑器专用：固定商店购买礼包时按概率模拟支付失败，方便验证「取消购买」等失败流程
            if (ShouldSimulateEditorPurchaseFail(data))
            {
                LogShopItem($"【编辑器模拟】固定商店礼包支付失败({EditorPurchaseFailChance:P0}): {data.produceNameId}");
                OnPurchaseFailed("simulated_editor_purchase_failed");
                return;
            }

            OnPurchaseSuccess(productItem);
        }
        else
        {
            AnalyticMgr.PurchaseStart(data.produceNameId);
            Game.self.Shop.Purchase(data.GetProduceName(), OnPurchaseSuccess, OnPurchaseFailed);
        }
    }

    /// <summary>编辑器模拟支付失败的概率（仅固定商店礼包）</summary>
    private const float EditorPurchaseFailChance = 0.5f;

    /// <summary>
    /// 编辑器模式下是否需要模拟一次支付失败：
    ///  - 只在 Unity 编辑器里生效（<see cref="UIUtilities.isEditMode"/>），真机永远返回 false；
    ///  - 只作用于【固定商店】且【礼包类商品（type == 2）】；
    ///  - 概率 <see cref="EditorPurchaseFailChance"/>（50%）。
    /// </summary>
    private bool ShouldSimulateEditorPurchaseFail(ShopDataItem data)
    {
        if (!UIUtilities.isEditMode) return false;
        if (ShopManager.shopManager == null) return false;
        if (ShopManager.shopManager.shopMode != ShopMode.FixedStore) return false;
        if (data == null || data.type != 2) return false; // 2 = 礼包

        return UnityEngine.Random.value < EditorPurchaseFailChance;
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

            // ★ 写入购买记录（礼包名称、价格、礼包内容快照、购买时间）
            //   与 GiftTable.RecordPurchase 使用同一份 PurchaseRecord 数据结构与快照规则。
            RecordPurchase(item);

            if (hasPermanentRemoveAds)
            {
                
            }
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

        // ★ 限购礼包（迎新 / 萤火 / 飞驰 / 从容礼盒等）可能还没有跟踪数据，
        //   先补建，否则 IsLimitedOut() 永远是 false，买满了也不会隐藏（含下次进店复现）。
        if (limitData == null && shopDataItem.limited > 0)
        {
            limitData = new ShopLimitData
            {
                id = shopDataItem.id,
                nameid = shopDataItem.produceNameId,
                pushStateIndex = 1f,
            };
            GameDataManager.Instance.UserData.limitShopItems.Add(limitData);
            LogShopItem($"补建限购跟踪数据: {shopDataItem.produceNameId}(id={shopDataItem.id}, 限购={shopDataItem.limited})");
        }

        // ★ 这里只置 isget（去广告 / 限时权益判断用）；
        //   购买次数 +1、付费倾向 / 探价 / 推荐位锁定、购买后权重快照统一交给 ShopManager.OnPackPurchased，
        //   避免两处都 +1 造成 buyCount 重复累加。
        if (limitData != null)
        {
            limitData.isget = true;
        }

        // ★ 商店界面（1 号位 / 2 号位 / 动态商店 / 固定商店）购买的礼包也要走推荐权重更新 + 权重快照日志。
        //   原来只有 GiftTable（复活/道具弹窗）和 ShopManager.OnPurchaseSuccess 两条链路会调，商店这边漏了。
        if (ShopManager.shopManager != null)
        {
            ShopManager.shopManager.OnPackPurchased(shopDataItem);

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

        // ★ bug50/bug51：购买成功后立刻落盘。
        //   以前这里完全不存档，玩家在「买完 → 下次自然存档」之间杀进程，
        //   奖励和购买记录会一起丢，而平台侧订单已经标记 granted，重进游戏也不会补发。
        if (GameDataManager.Instance != null) GameDataManager.Instance.CommitGameData();
    }

    protected virtual void OnPurchaseFailed(string error)
    {
        LogShopItem($"购买失败: {error}");
        AnalyticMgr.PurchaseFailed(shopDataItem.GetProduceName(), error);
        MessageSystem.Instance.HideLoadingAnimation();

        // 拉起系统支付后未完成购买（取消/失败），返回游戏时弹出「取消购买」弹窗
        if (shopDataItem != null && ShopManager.shopManager != null)
            ShopManager.shopManager.OnPurchaseCanceled(shopDataItem.produceNameId, true);
    }

    // ======================= ★ 购买记录写入 =======================

    /// <summary>
    /// 写入一条购买记录到 UserData.purchaseRecords。
    /// - 名称：优先 MultilingualManager 翻译 nameKey，回退 produceNameId；
    /// - 价格：优先 ProductItem.LocalizedPrice（>0 时），否则回退 ShopDataItem.price；
    /// - 礼包内容：深拷贝 ShopDataItem.productContent，避免后续引用变化影响历史记录；
    /// - 购买时间：yyyy-MM-dd HH:mm:ss。
    /// 
    /// 与 GiftTable.RecordPurchase 逻辑保持一致，避免两处口径不同。
    /// </summary>
    private void RecordPurchase(ProductItem item)
    {
        try
        {
            if (shopDataItem == null)
            {
                LogShopItem("RecordPurchase: shopDataItem 为空，跳过记录");
                return;
            }

            // 价格：优先取实际支付价格（LocalizedPrice），否则回退商品配置价
            float price = shopDataItem.price;

            // 名称：优先多语言翻译 nameKey，回退 produceNameId
            string nameText = MultilingualManager.Instance?.GetString(shopDataItem.name)
                              ?? shopDataItem.name
                              ?? shopDataItem.produceNameId
                              ?? "";

            var record = new PurchaseRecord
            {
                // 订单号：优先第三方回调 order_id，否则生成唯一 GUID（便于排查/去重）
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

                productContent = DeepCopyContent(shopDataItem.productContent)
            };

            GameDataManager.Instance?.UserData?.AddPurchaseRecord(record);

            LogShopItem($"购买记录已写入: name={record.nameText}, price={record.price}, time={record.buyTime}, contentCount={record.productContent.Count}");
        }
        catch (Exception ex)
        {
            LogShopItem($"RecordPurchase 异常: {ex.Message}");
        }
    }

    /// <summary>深拷贝 productContent（避免后续 ShopDataItem 变化影响历史记录）。</summary>
    private static List<List<string>> DeepCopyContent(List<List<string>> src)
    {
        var dst = new List<List<string>>();
        if (src == null) return dst;

        foreach (var row in src)
        {
            if (row == null) { dst.Add(new List<string>()); continue; }
            dst.Add(new List<string>(row));
        }
        return dst;
    }

    // ======================= 去广告处理 =======================

    private void HandleRemoveAdsPurchaseInternal(int adstype, int hours)
    {
        bool permanent = adstype == (int)LimitRewordType.RemoveAds;

        LogShopItem($"处理去广告购买: adstype={adstype}, permanent={permanent}, hours={hours}");

        ShopLimitData exist = GameDataManager.Instance.UserData.limitShopItems
            .Find(x => x.adstype == adstype);

        // ★ bug64：同 adstype 一律续期（含已标记过期但 endtime 仍有效的记录），不再新建第二条，
        //   否则剩余时长会被 Now + 时长 覆盖，且留下重复记录。
        if (exist != null)
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

    /// <summary>
    /// 已提示过「图标缺失」的名字。
    /// 缺图不会影响功能（有兜底），但原来每张卡每次刷新都会打一条 Warning，
    /// 而 Warning 会立刻落盘 —— 这里做去重，同一个图名一个会话只提示一次。
    /// </summary>
    private static readonly HashSet<string> _reportedMissingIcons = new HashSet<string>();

    protected Sprite LoadShopIcon(string showIcon)
    {
        if (string.IsNullOrEmpty(showIcon)) return null;

        Sprite sprite = AdvancedBundleLoader.SharedInstance.TryGetSpriteFromAtlas(showIcon);
        if (sprite != null) return sprite;

        // 兼容：少数礼包只有主图、没有 "_Icon" 变体（如 shopGifLing_Icon 在 UI_Universal 里并不存在），
        // 取不到就用主图兜底，避免商店列表刷"精灵不存在"的 Error。
        if (showIcon.EndsWith("_Icon"))
        {
            string baseName = showIcon.Substring(0, showIcon.Length - "_Icon".Length);
            sprite = AdvancedBundleLoader.SharedInstance.TryGetSpriteFromAtlas(baseName);
            if (sprite != null)
            {
                LogShopItemDetail($"图标 {showIcon} 缺失，回退使用 {baseName}");
            }
            else
            {
                WarnIconMissingOnce(showIcon, $"{showIcon} / {baseName} 都不存在，保留预制体默认图");
            }
        }
        else
        {
            WarnIconMissingOnce(showIcon, $"{showIcon} 不存在，保留预制体默认图");
        }

        return sprite;
    }

    private static void WarnIconMissingOnce(string key, string message)
    {
        if (!_reportedMissingIcons.Add(key)) return;
        Debug.LogWarning($"[Shop] 图标{message}");
    }
}