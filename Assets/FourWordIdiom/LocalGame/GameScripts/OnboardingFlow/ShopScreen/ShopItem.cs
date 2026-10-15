using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Coffee.UIEffects;
using Middleware;
using UnityEngine;
using UnityEngine.EventSystems;
//using UnityEngine.Purchasing;
using UnityEngine.UI;

public class ShopItem : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private ShopDataItem shopDataItem; // 假设这是一个封装商品数据的类
    [SerializeField] private GameObject discountbg;
    [SerializeField] private Image dibg;
    [SerializeField] private GameObject timebg;
    [SerializeField] private GameObject circle;
    [SerializeField] private Image shopIcon;
    public Image btntagicon;
    [SerializeField] private Text nameText;
    [SerializeField] private Text desText;
    [SerializeField] private Text shopCountText;
    public Text shopPriceText;
    //[SerializeField] private Button buyButton;
    [SerializeField] private Transform giftsParent;
    [SerializeField] private GiftItem giftItemPrefab;
    [SerializeField] private GameObject tipBtnPrefab;

    [SerializeField] private UIShiny adsShiny;

    private ShopLimitData _shopLimitData = null;
    private Button tipBtn;

    [Header("缩放设置")]
    public float pressedScale = 0.9f; // 按下时的缩放比例
    public float scaleSpeed = 10f;    // 缩放速度

    [Header("广告状态动画设置")]
    public float breathSpeed = 3f;           // 按钮呼吸速度
    public float breathAmplitude = 0.03f;    // 按钮呼吸幅度
    public float shakeSpeed = 15f;           // 抖动频率（频率调高，抖动更剧烈） 广告Icon晃动速度
    public float shakeAmplitude = 8f;        // 抖动幅度（角度）广告Icon晃动角度
    public float shakeInterval = 2.0f;       // 每次抖动的间隔时间（比如每2秒抖动一次）
    public float shakeDuration = 0.5f;       // 每次抖动的持续时间（比如持续抖动0.5秒）

    private float shakeTimer = 0f;           // 内部计时器
    private Vector3 originalIconPosition;    // 广告Icon原始坐标（防止位移）
    private Vector3 originalIconAngles;      // 广告Icon原始旋转（防止角度漂移）

    private Vector3 originalScale;    // 原始大小
    private bool isPressed = false;   // 是否按下
    private bool isDragging = false;  // 是否正在拖动
    private bool isAdStateActive = false;    // 是否处于看广告领取的动画状态
    private RectTransform rectTransform;
    private Vector2 pressPosition;     // 按下时的屏幕坐标

    private bool isTransformCached = false; // 标记是否已缓存初始坐标

    /// <summary>[Shop] 标签日志（与本类的 shopDataItem 信息一起输出）</summary>
    private void LogShopItem(string message)
    {
        string tag = shopDataItem != null ? $"{shopDataItem.produceNameId}(id={shopDataItem.id})" : "null";
        Debug.Log($"[Shop][ShopItem] [{tag}] {message}");
    }

    private void Awake()
    {
        InitTransformCache();
    }

    private void OnEnable()
    {
        isPressed = false;
        InitTransformCache(true);
        // 每次打开界面时，检查是否需要播放广告动画
        CheckAdAnimationState();
        // 每次打开时刷新按钮可点击状态（永久去广告已购买时置灰）
        RefreshButtonInteractable();
    }

    /// <summary>
    /// 缓存原始坐标和缩放，确保在对象池或频繁刷新下绝对不会偏移
    /// </summary>
    private void InitTransformCache(bool forceRefresh = false)
    {
        if (!forceRefresh && isTransformCached) return;

        rectTransform = GetComponent<RectTransform>();
        originalScale = transform.localScale;

        if (btntagicon != null)
        {
            originalIconPosition = btntagicon.transform.localPosition;
            originalIconAngles = btntagicon.transform.localEulerAngles;
        }

        isTransformCached = true;
    }

    /// <summary>
    /// 核心动画状态机：检查当前按钮是否处于广告状态，并控制组件启停
    /// </summary>
    public void CheckAdAnimationState()
    {
        if (shopDataItem == null) return;

        // 如果商品是"免费/广告"商品，并且今天的免费次数已经用过了，说明进入了广告模式
        if (shopDataItem.produceNameId == "FreeGoods" && GameDataManager.Instance.UserData.isDayFreeGet)
        {
            if (!isAdStateActive)
            {
                shakeTimer = 0f; // 刚切进入广告状态时，重置计时器
            }
            isAdStateActive = true;
            if (adsShiny != null) adsShiny.enabled = true; // 开启流光
            if (btntagicon != null) btntagicon.gameObject.SetActive(true); // 确保广告图标显示
        }
        else
        {
            // 免费模式或付费模式：关闭动画和特效，并强行复位坐标
            isAdStateActive = false;
            if (adsShiny != null) adsShiny.enabled = false;

            if (isTransformCached)
            {
                if (rectTransform != null) rectTransform.localScale = originalScale;
                if (btntagicon != null)
                {
                    btntagicon.transform.localPosition = originalIconPosition;
                    btntagicon.transform.localEulerAngles = originalIconAngles;
                }
            }
        }
    }

    void Update()
    {
        if (!isTransformCached) return;

        // 1. 处理按钮整体的呼吸与缩放
        Vector3 targetScale = originalScale;

        if (isPressed)
        {
            // 如果被按下，目标缩放为按下状态
            targetScale = originalScale * pressedScale;
        }
        // else if (isAdStateActive)
        // {
        //     // 如果处于广告状态且未被按下，执行基于正弦波的呼吸效果
        //     float breath = Mathf.Sin(Time.time * breathSpeed) * breathAmplitude;
        //     targetScale = originalScale * (1f + breath); 
        //     // 注意：这里永远基于 originalScale 计算，绝对不会产生逐渐变大或变小的漂移
        // }

        // 平滑过渡到目标缩放
        rectTransform.localScale = Vector3.Lerp(
            rectTransform.localScale,
            targetScale,
            Time.deltaTime * scaleSpeed
        );

        // 2. 处理广告Icon的轻微晃动
        if (isAdStateActive && btntagicon != null)
        {
            // 累加计时器，使用 unscaledTime 防止受游戏暂停影响
            shakeTimer += Time.unscaledDeltaTime;
            // 计算当前处于哪个周期（通过取余操作）
            float currentCycleTime = shakeTimer % (shakeInterval + shakeDuration);
            // 如果在"抖动持续时间"内，执行正弦波晃动
            if (currentCycleTime < shakeDuration)
            {
                // 计算Z轴的旋转晃动量
                float shakeZ = Mathf.Sin(currentCycleTime * shakeSpeed) * shakeAmplitude;

                // 基于原始旋转进行赋值，绝对不会产生角度偏移积累
                btntagicon.transform.localEulerAngles = new Vector3(
                    originalIconAngles.x,
                    originalIconAngles.y,
                    originalIconAngles.z + shakeZ
                );
            }
            else
            {
                // 在"非抖动的间隔时间"内，强行复位角度，保持绝对静止
                btntagicon.transform.localEulerAngles = originalIconAngles;
            }
            // 强行锁死原始坐标，确保在动画过程中任何层级刷新都不会导致位移
            // btntagicon.transform.localPosition = originalIconPosition; 
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // ★ 永久去广告已购买：拦截按下反馈
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

        // 计算拖拽距离（使用Unity的事件系统阈值）
        float dragDistance = (eventData.position - pressPosition).magnitude;
        float dragThreshold = EventSystem.current.pixelDragThreshold;

        // 当拖拽距离小于阈值时视为有效点击
        if (dragDistance <= dragThreshold)
        {
            OnItemClicked();
        }
    }

    private void OnItemClicked()
    {
        LogShopItem("条目被点击，执行功能");

        // ★ 永久去广告已购买：拦截点击（既不购买，也不触发其他流程）
        if (IsRemoveAdsOwnedProduct(shopDataItem))
        {
            LogShopItem("永久去广告已购买，拦截点击");
            return;
        }

        if (shopDataItem.purchaseType == -1)
        {
            if (shopDataItem.produceNameId == "FreeGoods")
            {
                if (UIUtilities.isEditMode || !GameDataManager.Instance.UserData.isDayFreeGet)
                {
                    UpdateAdsRewardUI(true);
                }
                else
                {
                    AnalyticMgr.VideoAdClick("看广告领取商店金币");
                    StartCoroutine(ShowAdsRewardUI());
                }
            }

            if (shopDataItem.produceNameId == "GoldGoods")
            {
                if (!GameDataManager.Instance.UserData.isDayGoldBuy)
                {
                    int needgold = (int)shopDataItem.price;

                    if (GameDataManager.Instance.UserData.Gold < needgold)
                    {
                        MessageSystem.Instance.ShowTip("TipGoldInsufficient");
                        return;
                    }

                    List<string> giftdata = shopDataItem.productContent[0];
                    int count = int.Parse(giftdata[1]);
                    int type = int.Parse(giftdata[0]);

                    GameDataManager.Instance.UserData.UpdateGold(-needgold, true, true, "看广告领取商店金币");
                    GameDataManager.Instance.UserData.UpdateTool((LimitRewordType)type, count, "看广告领取商店金币");

                    EventDispatcher.instance.TriggerChangeGoldUI(0, true);
                    GameDataManager.Instance.UserData.isDayGoldBuy = true;
                    shopPriceText.text = "已购买";
                    btntagicon.gameObject.SetActive(false);
                    MessageSystem.Instance.ShowTip("购买成功！");
                }
                else
                {
                    MessageSystem.Instance.ShowTip("每日只能购买一次！");
                    shopPriceText.text = "已购买";
                }
            }
        }
        else
        {
            if (shopDataItem.produceNameId == "SingleGoods")
            {
                if (GameDataManager.Instance.UserData.isDayMoneyBuy)
                {
                    MessageSystem.Instance.ShowTip("每日只能购买一次！");
                    return;
                }
            }

            // 在这里实现你的点击功能逻辑
            OnBuyButtonClicked(shopDataItem);
        }
    }

    IEnumerator ShowAdsRewardUI()
    {
        yield return new WaitForSeconds(0.05f);
        AdRuleManager.Instance.TryShowRewardVideo(Define.AdKey.RewardAdIdStoreGold, UpdateAdsRewardUI);
    }

    private void UpdateAdsRewardUI(bool isShow)
    {
        MessageSystem.Instance.HideLoadingAnimation();
        if (isShow)
        {
            List<string> giftdata = shopDataItem.productContent[0];
            int count = int.Parse(giftdata[1]);

            CustomFlyInManager.Instance.FlyInGold(shopIcon.transform);
            GameDataManager.Instance.UserData.UpdateGold(count, true, true, "看广告领取商店金币");
            GameDataManager.Instance.UserData.isDayFreeGet = true;
            btntagicon.gameObject.SetActive(true);
            AnalyticMgr.VideoAdSuccess("看广告领取商店金币");
            //MessageSystem.Instance.ShowTip("购买成功！");
            GameDataManager.Instance.UserData.totalSeeAds++;
        }
        else
        {
            MessageSystem.Instance.ShowTip("广告加载失败，请稍后重试。");
            AnalyticMgr.VideoAdFail("看广告领取商店金币");
        }
    }

    public void SetShopData(ShopDataItem data)
    {
        if (data == null)
        {
            Debug.LogWarning("Shop data is null");
            return;
        }

        shopDataItem = data;

        InitTransformCache(); // 确保数据填充前已缓存，防止对象池报错
        SetShopIcon();
        HandleTimeLimitedItems(data);
        HandleDiscountDisplay(data);
        HandleProductContentDisplay(data);
        HandleSpecialTypeItems(data);
        SetProductPrice(data);
        //SetupPurchaseButton(data);
        HandleMultiProductContent(data);

        // ★ 数据绑定完成后，刷新按钮可点击状态
        RefreshButtonInteractable();
    }

    public void UpdateUI()
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

        // ★ 刷新按钮可点击状态（去广告购买状态可能已变化）
        RefreshButtonInteractable();
    }

    #region Helper Methods

    private void SetShopIcon()
    {
        if (shopIcon == null) return;

        var icon = LoadShopIcon(shopDataItem.showIcon);
        if (icon != null)
        {
            shopIcon.sprite = icon;
            // shopIcon.SetNativeSize(); // 根据需要取消注释
        }
    }

    private void HandleTimeLimitedItems(ShopDataItem data)
    {
        if (timebg == null) return;

        _shopLimitData = GameDataManager.Instance.UserData.limitShopItems?
            .Find(item => item.nameid == data.produceNameId);

        if (_shopLimitData != null &&
            !string.IsNullOrEmpty(_shopLimitData.endtime) &&
            _shopLimitData.isopen)
        {
            timebg.gameObject.SetActive(true);
            StartCoroutine(UpdateTime());
            giftsParent.GetComponent<RectTransform>().anchoredPosition = new Vector2(50f, 40);
        }
        else
        {
            timebg.gameObject.SetActive(false);
            giftsParent.GetComponent<RectTransform>().anchoredPosition = new Vector2(50f, 65);
        }

        var icon = LoadShopIcon(shopDataItem.showIcon);
        if (icon != null)
        {
            //giftsParent.GetComponent<Image>().sprite = icon;
        }
    }

    private void HandleDiscountDisplay(ShopDataItem data)
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

    private void HandleProductContentDisplay(ShopDataItem data)
    {
        if (shopCountText == null) return;

        shopCountText.gameObject.SetActive(data.type != 1);

        if (data.type == 0 || data.type == 5 || data.purchaseType == -1 && data.productContent != null && data.productContent.Count > 0)
        {
            shopCountText.text = $"x {data.productContent[0][1]}";
        }
        else if (data.type == 2)
        {
            shopCountText.text = MultilingualManager.Instance?.GetString(data.name) ?? data.name;
            Sprite sprite = LoadShopIcon("giftdi" + data.id);
            if (sprite != null)
                dibg.sprite = sprite;
        }
    }

    private void HandleSpecialTypeItems(ShopDataItem data)
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

    private void LoadAndSetupTipButton(ShopDataItem data)
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

    private void SetProductPrice(ShopDataItem data)
    {
        if (shopPriceText == null) return;

        LogShopItem($"获取商品内购名称: {data.GetProduceName()}");

        try
        {
            // ★ 永久去广告已购买：不显示价格，显示"已购买"
            if (IsRemoveAdsOwnedProduct(data))
            {
                shopPriceText.text = "已购买";
                ShowPriceLoadingState(false);
                LogShopItem("永久去广告已购买，显示'已购买'");
                return;
            }

            float price = data.price;
            // 获取合适的文化信息
            CultureInfo culture = UIUtilities.GetCultureForCurrency("");

            if (shopDataItem.produceNameId == "SingleGoods")
            {
                if (!GameDataManager.Instance.UserData.isDayMoneyBuy)
                {
                    shopPriceText.text = UIUtilities.FormatCurrency(price, culture);
                }
                else
                {
                    shopPriceText.text = "已购买";
                }
            }
            else if (shopDataItem.produceNameId == "GoldGoods")
            {
                if (!GameDataManager.Instance.UserData.isDayGoldBuy)
                {
                    //shopPriceText.text = "400";
                    shopPriceText.text = data.price.ToString();
                }
                else
                {
                    shopPriceText.text = "已购买";
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

    private void ShowPriceLoadingState(bool isLoading)
    {
        if (circle != null) circle.gameObject.SetActive(isLoading);
        if (shopPriceText != null) shopPriceText.gameObject.SetActive(!isLoading);
    }

    /// <summary>
    /// 是否是"永久去广告"商品且已购买：
    ///  - 商品 produceNameId 等于 ShopManager.ProductRemoveAds（"RemoveADS"）；
    ///  - 玩家已拥有永久去广告（UserData.limitShopItems 里存在 adstype == RemoveAds 且未失效）。
    /// </summary>
    private bool IsRemoveAdsOwnedProduct(ShopDataItem data)
    {
        if (data == null) return false;
        if (data.produceNameId != ShopManager.ProductRemoveAds) return false;
        return ShopManager.shopManager != null && ShopManager.shopManager.IsRemoveAdsOwned();
    }

    /// <summary>
    /// 刷新按钮的可点击状态：
    ///  - 永久去广告已购买时，按钮置灰不可点，价格显示"已购买"；
    ///  - 其他商品保持可点击。
    /// </summary>
    private void RefreshButtonInteractable()
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

    private void SetupPurchaseButton(ShopDataItem data)
    {
        //if (buyButton == null) return;

        transform.GetComponent<Button>().onClick.RemoveAllListeners();
        transform.GetComponent<Button>().onClick.AddListener(() => OnBuyButtonClicked(data));
    }

    private void HandleMultiProductContent(ShopDataItem data)
    {
        if (data.productContent == null || data.productContent.Count <= 1) return;

        if (AdvancedBundleLoader.SharedInstance == null) return;

        var prefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "GiftItem");
        if (prefab == null) return;

        giftItemPrefab = prefab.GetComponent<GiftItem>();
        InitGiftItems();
    }

    #endregion

    private string Gettime()
    {
        DateTime startTime = DateTime.Parse(_shopLimitData.endtime);
        TimeSpan timeSpan = startTime.Subtract(DateTime.Now);

        if (timeSpan.TotalMinutes > 0)
        {
            timebg.GetComponentInChildren<Text>().text = UIUtilities.FormatTimeRemaining(timeSpan);
        }

        // 输出倒计时
        return timeSpan.TotalMinutes.ToString();
        //return "00:00:00";
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
                break; // 如果时间为空，退出循环
            }

            yield return new WaitForSeconds(10); // 等待 60 秒
        }
    }

    /// <summary>
    /// 初始化礼包内容展示。
    /// 需求："永久去广告"商品，玩家购买成功后，包含此物品的礼包不再显示"去广告"（无论是永久还是显示）物品。
    /// 因此当已拥有永久去广告时，跳过所有含 RemoveAds / Remove7DayAds / RemoveAds1Day 的内容。
    /// </summary>
    private void InitGiftItems()
    {
        bool ownsPermanentRemoveAds =
            ShopManager.shopManager != null && ShopManager.shopManager.IsRemoveAdsOwned();

        foreach (List<string> giftdata in shopDataItem.productContent)
        {
            int giftType = int.Parse(giftdata[0]);

            // 已购买永久去广告：礼包中不再展示任何去广告物品
            if (ownsPermanentRemoveAds && IsRemoveAdsRelatedType(giftType))
            {
                LogShopItem($"已拥有永久去广告，跳过礼包中的去广告物品 (type={giftType})");
                continue;
            }

            // 从对象池获取 ShopItem 对象
            GiftItem giftItem = Instantiate(giftItemPrefab, giftsParent).GetComponent<GiftItem>();
            if (giftType == (int)LimitRewordType.RemoveAds || giftType == (int)LimitRewordType.RemoveDayAds)
            {
                if (shopDataItem.type == 2)
                {
                    //giftsParent.GetComponent<HorizontalLayoutGroup>().spacing = 200;
                    giftItem.SetShopData(giftdata, shopDataItem.id, shopDataItem.des, shopDataItem.pointDes);
                }
                else
                {
                    giftItem.SetShopData(giftdata, shopDataItem.id);
                }
            }
            else
            {
                giftItem.SetShopData(giftdata, shopDataItem.id);
            }
        }
    }

    /// <summary>是否为去广告相关的内容类型（永久 / 7天 / 1天）</summary>
    private bool IsRemoveAdsRelatedType(int type)
    {
        return type == (int)LimitRewordType.RemoveAds
               || type == (int)LimitRewordType.RemoveDayAds;
    }

    private void OnBuyButtonClicked(ShopDataItem data)
    {
        // 双保险：点击入口再做一次拦截
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
                LocalizedPrice = 0,
            };
            OnPurchaseSuccess(productItem);
        }
        else
        {
            AnalyticMgr.PurchaseStart(data.produceNameId);
            //todo 打开loading界面
            Game.self.Shop.Purchase(data.GetProduceName(), OnPurchaseSuccess, OnPurchaseFailed);
        }
    }

    private void OnPurchaseSuccess(ProductItem item)
    {
        //todo 关闭loading界面
        LogShopItem($"购买成功: {item.ProductId}");

        // ★ 本次购买涉及的去广告类型（礼包可能同时包含多种）
        bool hasPermanentRemoveAds = false;       // 永久去广告

        if (shopDataItem.GetProduceName() == item.ProductId)
        {
            Game.self.Shop.CurrentShopDataItem = shopDataItem.DeepCopy();

            foreach (var dataitem in shopDataItem.productContent)
            {
                int count = int.Parse(dataitem[1]);
                int type = int.Parse(dataitem[0]);
                switch (type)
                {
                    case (int)LimitRewordType.Coins:
                        GameDataManager.Instance.UserData.UpdateGold(count, false, false, "商店购买" + item.ItemName);
                        break;
                    case (int)LimitRewordType.Butterfly:
                        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Butterfly, count, "商店购买" + item.ItemName);
                        break;
                    case (int)LimitRewordType.Tipstool://放大镜道具，整个词语提示
                        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, count, "商店购买" + item.ItemName);
                        break;
                    case (int)LimitRewordType.AutoComplete://提示灯道具，单个字符提示
                        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.AutoComplete, count, "商店购买" + item.ItemName);
                        break;
                    case (int)LimitRewordType.RemoveAds:
                        hasPermanentRemoveAds = true;
                        HandleRemoveAdsPurchaseInternal((int)LimitRewordType.RemoveAds, 0);
                        break;
                    case (int)LimitRewordType.RemoveDayAds:
                        hasPermanentRemoveAds = false;
                        HandleRemoveAdsPurchaseInternal((int)LimitRewordType.RemoveDayAds, count*24);
                        break;
                    case (int)LimitRewordType.ReStartTool:
                        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.ReStartTool, count, "商店购买" + item.ItemName);
                        break;
                    case (int)LimitRewordType.InfiniteEnergy:
                        // ★ 无限体力：count = 小时数，累加到 UserData.unlimitedEnergyEndTime
                        HandleInfiniteEnergyPurchase(count);
                        break;
                    default:
                        Debug.LogWarning($"[Shop] 未处理的商品类型 type={type} count={count}");
                        break;
                }
            }
            
            // 决定弹哪个面板：包含任一去广告 → RemoveAdsScreen；否则 → AwardScreen
            bool anyRemoveAds = hasPermanentRemoveAds;
            if (anyRemoveAds)
            {
                SystemManager.Instance.ShowPanel(PanelType.RemoveAdsScreen);
            }
            else
            {
                SystemManager.Instance.ShowPanel(PanelType.AwardScreen);
            }
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

        if (!UIUtilities.isEditMode)
        {
            AnalyticMgr.PurchaseFinished(item, firstPay);
#if UNITY_HUAWEI
            // 处理购买成功后的逻辑，例如增加游戏内货
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
            transform.gameObject.SetActive(false);
        }

        //MessageSystem.Instance.ShowTip("购买成功！");
        MessageSystem.Instance.HideLoadingAnimation();

        // ★ 购买成功后刷新按钮状态（永久去广告购买后立即置灰）
        RefreshButtonInteractable();
    }

    // ======================= 去广告核心处理 =======================

    /// <summary>
    /// 处理去广告购买（永久 + 限时）：
    ///   - 永久去广告（RemoveAds）：写入/更新永久记录（endtime 为空），isoverdate 重置为 false；
    ///   - 限时7天（Remove7DayAds）：在现有未过期时间基础上 +7*24 小时（需求：限时续期叠加）；
    ///   - 限时1天（RemoveAds1Day）：在现有未过期时间基础上 +24 小时；
    ///   - 已过期或从未购买：从当前时间起算。
    /// 需求："限时去广告商品，玩家购买后，在限时期间，玩家再次购买包含'限时去广告'礼包的，
    ///       刷新去广告的限时；在之前剩余时间的基础上，额外增加限时时间"。
    /// </summary>
    private void HandleRemoveAdsPurchaseInternal(int adstype, int hours)
    {
        bool permanent = adstype == (int)LimitRewordType.RemoveAds;

        LogShopItem($"处理去广告购买: adstype={adstype}, permanent={permanent}, hours={hours}");

        // 1. 查找现有记录
        ShopLimitData exist = GameDataManager.Instance.UserData.limitShopItems
            .Find(x => x.adstype == adstype);

        if (exist != null && !exist.isoverdate)
        {
            // 已有未失效记录 → 续期
            if (permanent)
            {
                // 永久去广告：无需 endtime，仅刷新状态
                exist.isget = true;
                exist.isopen = false;
                exist.isoverdate = false;
                exist.endtime = "";
                exist.gettime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                LogShopItem($"永久去广告续期: 记录已更新 (id={exist.id})");
            }
            else
            {
                // 限时去广告：在剩余时间基础上叠加
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
            // 2. 无记录或已失效 → 新建 / 重激活
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

        // 3. 生效广告表现：隐藏横幅（插屏/开屏拦截由 AdRuleManager 判断）
        if (AdRuleManager.Instance != null)
        {
            AdRuleManager.Instance.HideBannerAd();
        }

        // 4. 通知刷新
        if (ShopManager.shopManager != null)
        {
            ShopManager.shopManager.UpdateAdsBtnUIEvent(permanent ? null : DateTime.Now.ToString(), true);
            ShopManager.shopManager.RefreshLimitData();
        }

        // 5. 当前按钮置灰（当前商品即为该去广告商品时）
        var curBtn = transform.GetComponent<Button>();
        if (curBtn != null && permanent)
        {
            curBtn.interactable = false;
        }
    }

    /// <summary>
    /// 无限体力购买处理：
    ///  - 通过 UserData.AddUnlimitedEnergy(hours) 累加时长（多次购买叠加）；
    ///  - 若当前商品同时包含其它奖励，不影响本方法；
    ///  - 通知顶部体力栏立即刷新（无限体力图标/倒计时）。
    /// </summary>
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

        // 刷新顶部体力栏 UI（无限体力图标 ∞ + 剩余倒计时）
        EventDispatcher.instance.TriggerUpdateLayerCoin(false, true);
    }

    private void OnPurchaseFailed(string error)
    {
        //todo 关闭loading界面
        LogShopItem($"购买失败: {error}");
        AnalyticMgr.PurchaseFailed(shopDataItem.GetProduceName(), error);
        MessageSystem.Instance.HideLoadingAnimation();
    }

    private Sprite LoadShopIcon(string showIcon)
    {
        Sprite sprite = null;
        sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(showIcon);

        return sprite;
    }

    private void OnDisable()
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
}