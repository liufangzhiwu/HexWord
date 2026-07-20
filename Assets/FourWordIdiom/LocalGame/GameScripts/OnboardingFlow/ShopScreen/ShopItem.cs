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

public class ShopItem : MonoBehaviour,IPointerDownHandler, IPointerUpHandler
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
    
    private ShopLimitData _shopLimitData=null;
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
    private Vector3 originalIconAngles; // 广告Icon原始旋转（防止角度漂移）
    
    private Vector3 originalScale;    // 原始大小
    private bool isPressed = false;   // 是否按下
    private bool isDragging = false;  // 是否正在拖动
    private bool isAdStateActive = false;    // 是否处于看广告领取的动画状态
    private RectTransform rectTransform;
    private Vector2 pressPosition;     // 按下时的屏幕坐标
    
    private bool isTransformCached = false; // 标记是否已缓存初始坐标
    
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
            // 如果在“抖动持续时间”内，执行正弦波晃动
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
                // 在“非抖动的间隔时间”内，强行复位角度，保持绝对静止
                btntagicon.transform.localEulerAngles = originalIconAngles;
            }
            // 强行锁死原始坐标，确保在动画过程中任何层级刷新都不会导致位移
            // btntagicon.transform.localPosition = originalIconPosition; 
        }
    }


    public void OnPointerDown(PointerEventData eventData)
    {
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
        Debug.Log("条目被点击，执行功能");

        if (shopDataItem.purchaseType == -1)
        {
            if (shopDataItem.produceNameId == "FreeGoods")
            {
                if (UIUtilities.isEditMode||!GameDataManager.Instance.UserData.isDayFreeGet)
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
                    
                    if (GameDataManager.Instance.UserData.Gold < 400)
                    {
                        MessageSystem.Instance.ShowTip("TipGoldInsufficient");
                        return;
                    }
                    
                    List<string> giftdata=shopDataItem.productContent[0];
                    int count = int.Parse(giftdata[1]);
                    int type = int.Parse(giftdata[0]);
                    
                    GameDataManager.Instance.UserData.UpdateGold(-400, true,true,"看广告领取商店金币");
                    GameDataManager.Instance.UserData.UpdateTool((LimitRewordType)type, count,"看广告领取商店金币");
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
        AdRuleManager.Instance.TryShowRewardVideo(Define.AdKey.RewardAdIdStoreGold,UpdateAdsRewardUI);
    }
    
    private void UpdateAdsRewardUI(bool isShow)
    {
        MessageSystem.Instance.HideLoadingAnimation();
        if (isShow)
        {
            List<string> giftdata=shopDataItem.productContent[0];
            int count = int.Parse(giftdata[1]);
            
            CustomFlyInManager.Instance.FlyInGold(shopIcon.transform);
            GameDataManager.Instance.UserData.UpdateGold(count, true,true,"看广告领取商店金币");
            GameDataManager.Instance.UserData.isDayFreeGet=true;
            btntagicon.gameObject.SetActive(true);
            AnalyticMgr.VideoAdSuccess("看广告领取商店金币");
            MessageSystem.Instance.ShowTip("购买成功！");
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

        bool shouldShowTimeBg = !string.IsNullOrEmpty(data.unlocked?[0]);
        timebg.SetActive(shouldShowTimeBg);

        if (shouldShowTimeBg)
        {
            _shopLimitData = GameDataManager.Instance.UserData.limitShopItems?
                .Find(item => item.nameid == data.produceNameId);
            
            if (_shopLimitData != null &&
                !string.IsNullOrEmpty(_shopLimitData.endtime) &&
                _shopLimitData.isopen)
            {
                StartCoroutine(UpdateTime());
            }
            
            giftsParent.GetComponent<RectTransform>().anchoredPosition = new Vector2(50f, 40);
        }
        else
        {
            giftsParent.GetComponent<RectTransform>().anchoredPosition = new Vector2(50f, 65);
        }
        
        giftsParent.GetComponent<Image>().sprite = LoadShopIcon("itemdi"+data.id);
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

        if (data.type == 0||data.type == 5|| data.purchaseType == -1&& data.productContent != null && data.productContent.Count > 0)
        {
            shopCountText.text = $"x {data.productContent[0][1]}";
        }
        else if (data.type == 2)
        {
            shopCountText.text = MultilingualManager.Instance?.GetString(data.name) ?? data.name;
            dibg.sprite =LoadShopIcon("giftdi"+data.id);
            
            Color color = new Color(40.0f/255,144.0f/255,1);

            switch (data.id)
            {
                case 6:
                    color = new Color(25.0f/255f,193.0f/255,134.0f/255);
                    break;
                case 7:
                    color =new Color(251.0f/255,91.0f/255,168.0f/255);
                    break;
                case 8:
                    color = new Color(254.0f/255,141.0f/255,50.0f/255);
                    break;
                case 9:
                    color =new Color(140.0f/255,89.0f/255,246.0f/255);
                    break;
            }
            
            shopCountText.color = color;
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

            tipBtn.onClick.AddListener(()=>ClickShopItemtipBtn(tippanel));
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

        Debug.Log($"获取商品内购名称: {data.GetProduceName()}");

        // Product product = ShopManager.shopManager?.GetProduct(data.GetProduceName());
        // if (product == null || product.metadata == null)
        // {
        //     ShowPriceLoadingState(true);
        //     return;
        // }

        try
        {

#if UNITY_IOS
            decimal price = product.metadata.localizedPrice;
            string currencyCode = product.metadata.isoCurrencyCode;

            Debug.Log($"商品价格: {price} ({currencyCode})");

            CultureInfo culture = UIExtension.GetCultureForCurrency(currencyCode);
#else
            float price = data.price;
            // 获取合适的文化信息
            CultureInfo culture = UIUtilities.GetCultureForCurrency("");
#endif
           
            
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
                    shopPriceText.text = "400";
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

    private void InitGiftItems()
    {
        foreach (List<string> giftdata in shopDataItem.productContent)
        {
            // 从对象池获取 ShopItem 对象
            GiftItem giftItem = Instantiate(giftItemPrefab, giftsParent).GetComponent<GiftItem>();
            if (int.Parse(giftdata[0]) == (int)LimitRewordType.RemoveAds || int.Parse(giftdata[0]) == (int)LimitRewordType.Remove7DayAds)
            {
                if (shopDataItem.type == 2)
                {
                    giftsParent.GetComponent<HorizontalLayoutGroup>().spacing = 200;
                    // 赋值 shopItem 的数据
                    giftItem.SetShopData(giftdata,shopDataItem.id,shopDataItem.des,shopDataItem.pointDes);
                }
                else
                {
                    // 赋值 shopItem 的数据
                    giftItem.SetShopData(giftdata, shopDataItem.id);
                }
            }
            else
            {
                // 赋值 shopItem 的数据
                giftItem.SetShopData(giftdata, shopDataItem.id);
            }
           
        }
    }

    private void OnBuyButtonClicked(ShopDataItem data)
    {
        MessageSystem.Instance.ShowLoadingAnimation();

        if (UIUtilities.isEditMode)
        {
            ProductItem productItem = new ProductItem
            {
                order_id  = "",
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
        Debug.Log("购买成功: " + item.ProductId);
        //var items = new List<AnalyticMgr.Item>();
        if (shopDataItem.GetProduceName() == item.ProductId)
        {
            foreach (var dataitem in shopDataItem.productContent)
            {
                int count = int.Parse(dataitem[1]);
                int type = int.Parse(dataitem[0]);
                //items.Add(new AnalyticMgr.Item { item_name = type.ToString(), quantity = count });
                switch (type)
                {
                    case (int)LimitRewordType.Coins:
                        GameDataManager.Instance.UserData.UpdateGold(count, true, true,"商店购买"+item.ItemName);
                        break;
                    case (int)LimitRewordType.Butterfly:
                        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Butterfly,count,"商店购买"+item.ItemName);
                        break;
                    case (int)LimitRewordType.Tipstool://放大镜道具，整个词语提示
                        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool,count,"商店购买"+item.ItemName);
                        break;
                    case (int)LimitRewordType.AutoComplete://提示灯道具，单个字符提示
                        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.AutoComplete,count,"商店购买"+item.ItemName);
                        break;
                    case (int)LimitRewordType.RemoveAds:
                    case (int)LimitRewordType.Remove7DayAds:
                        BuyRemoveAdsEvent(type);
                        break;
                }
            }
        }
        ShopManager.shopManager.paysuccess = true;
        bool firstPay = GameDataManager.Instance.UserData.TotalPayTimes == 0;
        if (firstPay)
            GameDataManager.Instance.UserData.firstPayTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        GameDataManager.Instance.UserData.lastPayTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        GameDataManager.Instance.UserData.TotalPayTimes++;
        GameDataManager.Instance.UserData.TotalRevenue += item.LocalizedPrice;
        DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedShopBuy,1);
        
        if (!UIUtilities.isEditMode)
        {
            AnalyticMgr.PurchaseFinished(item, firstPay);
#if UNITY_huawei
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
            transform.gameObject.SetActive(false);
        }
        
        
        MessageSystem.Instance.ShowTip("购买成功！");
        MessageSystem.Instance.HideLoadingAnimation();
    }
    
    private void OnPurchaseFailed(string error)
    {
        //todo 关闭loading界面
        Debug.Log("购买失败: " + error);
        AnalyticMgr.PurchaseFailed(shopDataItem.GetProduceName(),error);
        MessageSystem.Instance.HideLoadingAnimation();
    }
    
    private void BuyRemoveAdsEvent(int type)
    {
        // ShopLimitData reshopLimitData= GameDataManager.Instance.UserData.limitShopItems.Find(item =>item.id == shopDataItem.id);
        // if (reshopLimitData != null)
        // {
        //     reshopLimitData.isoverdate = false;
        //     reshopLimitData.isget = true;
        //     reshopLimitData.gettime=DateTime.Now.ToString();
        //     reshopLimitData.adstype = type;
        // }
        // else
        // {
        //     GameDataManager.Instance.UserData.limitShopItems.Add(new ShopLimitData()
        //     {
        //         id = shopDataItem.id,
        //         endtime = null,
        //         isopen = false,
        //         gettime = DateTime.Now.ToString(),
        //         adstype = type,
        //         isget = true,
        //         isoverdate = false,
        //     });
        // }

        //AdsManager.Instance.HideBannerAd();
        transform.gameObject.SetActive(false);

        if (type == (int)LimitRewordType.Remove7DayAds)
        {
            //ShopManager.shopManager.UpdateAdsBtnUIEvent(reshopLimitData.gettime,true);
        }

        if (type == (int)LimitRewordType.RemoveAds)
        {
            ShopManager.shopManager.UpdateAdsBtnUIEvent(null,true);
        }
    }

    private Sprite LoadShopIcon(string showIcon)
    {
        return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(showIcon);
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