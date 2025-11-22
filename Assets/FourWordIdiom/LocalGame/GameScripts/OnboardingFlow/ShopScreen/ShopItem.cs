using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
    [SerializeField] private Text nameText;
    [SerializeField] private Text desText;
    [SerializeField] private Text shopCountText;
    [SerializeField] private Text shopPriceText;
    //[SerializeField] private Button buyButton;
    [SerializeField] private Transform giftsParent;
    [SerializeField] private GiftItem giftItemPrefab;
    [SerializeField] private GameObject tipBtnPrefab;
    //private ShopLimitData _shopLimitData=null;
    private Button tipBtn;
    
    [Header("缩放设置")]
    public float pressedScale = 0.9f; // 按下时的缩放比例
    public float scaleSpeed = 10f;    // 缩放速度
    
    private Vector3 originalScale;    // 原始大小
    private bool isPressed = false;   // 是否按下
    private bool isDragging = false;  // 是否正在拖动
    private RectTransform rectTransform;
    private Vector2 pressPosition;     // 按下时的屏幕坐标
    
    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        originalScale = transform.localScale;
    }
    
    void Update()
    {
        // 持续更新缩放状态
        var targetScale = isPressed ? originalScale * pressedScale : originalScale;
        rectTransform.localScale = Vector3.Lerp(
            rectTransform.localScale,
            targetScale,
            Time.deltaTime * scaleSpeed
        );
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        pressPosition = eventData.position;
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
        // 在这里实现你的点击功能逻辑
        OnBuyButtonClicked(shopDataItem);
    }
    

    public void SetShopData(ShopDataItem data)
    {
        if (data == null)
        {
            Debug.LogWarning("Shop data is null");
            return;
        }

        shopDataItem = data;       

        try
        {
            SetShopIcon();
            HandleTimeLimitedItems(data);
            HandleDiscountDisplay(data);
            HandleProductContentDisplay(data);
            HandleSpecialTypeItems(data);
            SetProductPrice(data);
            //SetupPurchaseButton(data);
            HandleMultiProductContent(data);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error setting shop data: {ex.Message}");
        }
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
            // _shopLimitData = GameDataManager.Instance.UserData.limitShopItems?
            //     .Find(item => item.id == data.id);
            //
            // if (_shopLimitData != null &&
            //     !string.IsNullOrEmpty(_shopLimitData.endtime) &&
            //     _shopLimitData.isopen)
            // {
            //     StartCoroutine(UpdateTime());
            // }
            
            giftsParent.GetComponent<RectTransform>().anchoredPosition = new Vector2(31.2f, 40);
        }
        else
        {
            giftsParent.GetComponent<RectTransform>().anchoredPosition = new Vector2(31.2f, 65);
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
                discountText.text = $"{data.discount}折扣";
            }
        }
    }

    private void HandleProductContentDisplay(ShopDataItem data)
    {
        if (shopCountText == null) return;

        shopCountText.gameObject.SetActive(data.type != 1);

        if (data.type == 0 && data.productContent != null && data.productContent.Count > 0)
        {
            shopCountText.text = $"x {data.productContent[0][1]}";
        }
        else if (data.type == 2)
        {
            shopCountText.text = MultilingualManager.Instance?.GetString(data.name) ?? data.name;
            dibg.sprite =LoadShopIcon("giftdi"+data.id);
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

        //Product product = ShopManager.shopManager?.GetProduct(data.GetProduceName());
        //if (product == null || product.metadata == null)
        //{
        //    ShowPriceLoadingState(true);
        //    return;
        //}

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
           
            shopPriceText.text = UIUtilities.FormatCurrency(price, culture);

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
        // DateTime startTime = DateTime.Parse(_shopLimitData.endtime);
        // TimeSpan timeSpan = startTime.Subtract(DateTime.Now);
        //   
        // if (timeSpan.TotalMinutes > 0)
        // {
        //     timebg.GetComponentInChildren<Text>().text = UIUtilities.FormatTimeRemaining(timeSpan);
        // }
        //
        // // 输出倒计时
        // return timeSpan.TotalMinutes.ToString();
        return "00:00:00";
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
                //_shopLimitData.isopen = false;
                transform.gameObject.SetActive(false);
                //OnCloseBtn();
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

    private async void OnBuyButtonClicked(ShopDataItem data)
    {
        //ShopManager.shopManager.OnBuyGoldTestButtonClicked(data.GetProduceName()
        //    ,
        //    OnPurchaseSuccess,
        //    OnPurchaseFailed);
        //// 处理购买逻辑
        Debug.Log($"Buying: {data.name}, Price: {data.GetProduceName()}");
        
        //bool isPayable = await UIController.Instance.CheckPayable((int)data.price);
        
        //if (isPayable)
        {
            OnPurchaseSuccess(data);
        }
        //string area = "";
      
        //FirebaseManager.Instance.PayStart(shopDataItem.GetProduceName(),area,SaveSystem.Instance.UserData.CurrentStage);
    }

    private void OnPurchaseSuccess(ShopDataItem product)
    {
        Debug.Log("购买成功: " + product.id);
       
        // if (shopDataItem.GetProduceName() == product.definition.id)
        // {
            foreach (var dataitem in product.productContent)
            {
                int count = int.Parse(dataitem[1]);
                int type = int.Parse(dataitem[0]);
                
                switch (type)
                {
                    case (int)LimitRewordType.Coins:
                        GameDataManager.Instance.UserData.UpdateGold(count,true,true);
                        break;
                    case (int)LimitRewordType.Butterfly:
                        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Butterfly, count);
                        //GameDataManager.Instance.UserData.toolInfo[103].count += count;
                        //EventManager.OnChangGoldUI?.Invoke(0, false);
                        break;
                    case (int)LimitRewordType.Tipstool:
                        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, count);
                        //GameDataManager.Instance.UserData.toolInfo[102].count += count;
                        //EventManager.OnChangGoldUI?.Invoke(0, false);
                        break;
                    case (int)LimitRewordType.Resettool:
                        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Resettool, count);
                        //GameDataManager.Instance.UserData.toolInfo[101].count += count;
                        //EventDispatcher.instance.TriggerChangeGoldUI(0, false);
                        break;
                    case (int)LimitRewordType.RemoveAds:
                    case (int)LimitRewordType.Remove7DayAds:
                        //BuyRemoveAdsEvent(type);
                        break;
                }
            }
        //}
        // 获取商品价格和货币代码
        //string currencyCode = product.metadata.isoCurrencyCode;
        //float localizedPrice = (float)product.metadata.localizedPrice;
        //string area = "";
        //FirebaseManager.Instance.PaySuccess(product,1);
        EventDispatcher.instance.TriggerChangeGoldUI(0, false);
        ShopManager.shopManager.paysuccess = true;
        DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedShopBuy,1);
        MessageSystem.Instance.ShowTip("购买成功！");

        //UIController.Instance.SubmitPayment((int)product.price);

        //AdjustManager.Instance.SendPurchaseEvent();
        // 处理购买成功后的逻辑，例如增加游戏内货币
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

    private void OnPurchaseFailed(string error)
    {
        Debug.Log("购买失败: " + error);
        string area = "";
        //FirebaseManager.Instance.PayFailed(shopDataItem.GetProduceName(),area,SaveSystem.Instance.UserData.CurrentStage,error);
        // 处理购买失败后的逻辑，例如显示错误提示
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