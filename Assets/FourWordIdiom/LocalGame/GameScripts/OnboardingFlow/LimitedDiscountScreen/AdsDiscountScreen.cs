using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DG.Tweening;
using Middleware;
using UnityEngine;
//using UnityEngine.Purchasing;
using UnityEngine.UI;

public class AdsDiscountScreen : UIWindow
{
    [SerializeField] private Button closeBtn; // 关闭按钮
    //[SerializeField] private Text title; // 音效文本显示
    [SerializeField] private Text timeText; // 语言选择文本显示
    [SerializeField] private Text priceText; // 价格
    [SerializeField] private Text discountText; // 折扣前价格
    [SerializeField] private Transform parent;
    [SerializeField] private GiftItem giftItempPefab;
    [SerializeField] private GameObject discountObj; 
    [SerializeField] private GameObject circle; 
    [SerializeField] private Button ClaimBtn;
    private ObjectPool objectPool; // 对象池实例
    private ShopDataItem currentShopItem;
    private ShopLimitData shopLimitData;
    private List<GiftItem> GiftItems=new List<GiftItem>();

    protected void Start()
    {
        
        if (giftItempPefab == null)
        {
            giftItempPefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "DiscountItem").GetComponent<GiftItem>();
        }
        objectPool = new ObjectPool(giftItempPefab.gameObject, ObjectPool.CreatePoolContainer(transform, "GiftItemPool"));
        
    }
    
    protected override void InitializeUIComponents()
    {
        closeBtn.AddVibraClickAction(OnCloseBtn); // 绑定关闭按钮事件
        ClaimBtn.AddClickAction(OnBuyButtonClicked);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        currentShopItem = ShopManager.shopManager.curshopAdsItem;
        shopLimitData=GameDataManager.Instance.UserData.limitShopItems.Find(item => item.id == currentShopItem.id);
        
        InitUI();
        EventDispatcher.instance.TriggerUpdateLayerCoin(true,false);
        
        InitGiftItems();
        
        StartCoroutine(UpdateTime());
        AudioManager.Instance.PlaySoundEffect("ShowUI");
    }

    private void InitUI()
    {
        if (currentShopItem == null) return;

        //title.text = MultilingualManager.Instance?.GetString(currentShopItem.name) ?? currentShopItem.name;

        bool hasDiscount = !string.IsNullOrEmpty(currentShopItem.discount);
        discountObj.SetActive(hasDiscount);
        discountText.gameObject.SetActive(hasDiscount);

        // 调整价格文本位置
        // priceText.GetComponent<RectTransform>().anchoredPosition =
        //     hasDiscount ? new Vector2(93, 0) : Vector2.zero;

        InitPriceText(hasDiscount);
    }

    private void InitPriceText(bool needDiscount)
    {
        if (currentShopItem == null)
        {
            Debug.LogWarning("当前商店项为空");
            ShowLoadingState(true);
            return;
        }

        Debug.Log($"礼包弹窗界面获取商品内购名称: {currentShopItem.GetProduceName()}");

        //Product product = ShopManager.shopManager?.GetProduct(currentShopItem.GetProduceName());
        //if (product == null || product.metadata == null)
        //{
        //    Debug.LogWarning($"无法获取商品信息: {currentShopItem.GetProduceName()}");
        //    ShowLoadingState(true);
        //    return;
        //}

        try
        {

#if UNITY_IOS
            decimal price = product.metadata.localizedPrice;
            string currencyCode = product.metadata.isoCurrencyCode;

            Debug.Log($"商品价格: {price} ({currencyCode})");

            // 获取合适的文化信息
            CultureInfo culture = UIUtilities.GetCultureForCurrency(currencyCode);
#else
            float price = currentShopItem.price;
            // 获取合适的文化信息
            CultureInfo culture = UIUtilities.GetCultureForCurrency("");
#endif
            
            // 格式化价格
            priceText.text = UIUtilities.FormatCurrency(price,culture );

            // 处理折扣
            if (needDiscount)
            {
                if (float.TryParse(currentShopItem.discount.TrimEnd('%'), out float discountPercent))
                {
                    decimal discountRate = (decimal)(discountPercent / 100f);
                    decimal originalPrice = (decimal) price / discountRate;
                    discountText.text = UIUtilities.FormatCurrency(originalPrice, culture);
                    discountObj.GetComponentInChildren<Text>().text = $"{currentShopItem.discount}";
                   
                }
                else
                {
                    Debug.LogWarning($"折扣格式无效: {currentShopItem.discount}");
                    discountText.text = "N/A";
                }
            }

            ShowLoadingState(false);
        }
        catch (Exception ex)
        {
            Debug.LogError($"初始化价格文本时出错: {ex.Message}");
            ShowLoadingState(true);
        }
    }

    private void ShowLoadingState(bool isLoading)
    {
        circle.gameObject.SetActive(isLoading);
        priceText.gameObject.SetActive(!isLoading);
       
        if (!string.IsNullOrEmpty(currentShopItem.discount))
        {
            discountText.gameObject.SetActive(!isLoading);
        }
    }

    private string Gettime()
    {
        DateTime endtime = DateTime.Parse(shopLimitData.endtime);
        TimeSpan timeSpan = endtime.Subtract(DateTime.Now);
      
        if (timeSpan.TotalMinutes > 0)
        {
            timeText.text = UIUtilities.FormatTimeRemaining(timeSpan);
        }

        // 输出倒计时
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
                shopLimitData.isopen = false;
                shopLimitData.endtime=null;
                OnCloseBtn();
                break; // 如果时间为空，退出循环
            }
          
            yield return new WaitForSeconds(1); // 等待 60 秒
        }
    }

    private async void InitGiftItems()
    {
        await Task.Delay(10);
        
        for (int i = 0; i < currentShopItem.productContent.Count; i++)
        {
            List<string> itemdata=currentShopItem.productContent[i];
            if (GiftItems.Count > i)
            {
                GiftItem giftItem =GiftItems[i];
                giftItem.SetShopData(itemdata, currentShopItem.id, currentShopItem.pointDes);
            }
            else
            {
                GiftItem giftItem = objectPool.GetObject<GiftItem>(parent);
                giftItem.SetShopData(itemdata, currentShopItem.id, currentShopItem.pointDes);
                GiftItems.Add(giftItem);
            }
        }
    }
    
    private void OnBuyButtonClicked()
    {
        MessageSystem.Instance.ShowLoadingAnimation();

        if (UIUtilities.isEditMode)
        {
            ProductItem productItem = new ProductItem
            {
                order_id  = "",
                IsoCurrencyCode = "",
                ItemName = currentShopItem.produceNameId,
                ProductId = currentShopItem.produceNameId,
                LocalizedPrice = 0,
            };
            OnPurchaseSuccess(productItem);
        }
        else
        {
            AnalyticMgr.PurchaseStart(currentShopItem.produceNameId);
            //todo 打开loading界面
            Game.self.Shop.Purchase(currentShopItem.GetProduceName(), OnPurchaseSuccess, OnPurchaseFailed);
        }
    }
    
     private void OnPurchaseSuccess(ProductItem item)
    {
        //todo 关闭loading界面
        Debug.Log("购买成功: " + item.ProductId);
        //var items = new List<AnalyticMgr.Item>();
        if (currentShopItem.GetProduceName() == item.ProductId)
        {
            Game.self.Shop.CurrentShopDataItem=currentShopItem.DeepCopy();
            
            SystemManager.Instance.ShowPanel(PanelType.AwardScreen);
            
            foreach (var dataitem in currentShopItem.productContent)
            {
                int count = int.Parse(dataitem[1]);
                int type = int.Parse(dataitem[0]);
                //items.Add(new AnalyticMgr.Item { item_name = type.ToString(), quantity = count });
                switch (type)
                {
                    case (int)LimitRewordType.Coins:
                        GameDataManager.Instance.UserData.UpdateGold(count, false, false,"限时宝箱商店购买"+item.ItemName);
                        break;
                    case (int)LimitRewordType.Butterfly:
                        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Butterfly,count,"限时宝箱商店购买"+item.ItemName);
                        break;
                    case (int)LimitRewordType.Tipstool://放大镜道具，整个词语提示
                        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool,count,"限时宝箱商店购买"+item.ItemName);
                        break;
                    case (int)LimitRewordType.AutoComplete://提示灯道具，单个字符提示
                        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.AutoComplete,count,"限时宝箱商店购买"+item.ItemName);
                        break;
                    case (int)LimitRewordType.RemoveAds:
                    case (int)LimitRewordType.Remove7DayAds:
                        //BuyRemoveAdsEvent(type);
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
            // 处理购买成功后的逻辑，例如增加游戏内货
#if UNITY_huawei
            // 处理购买成功后的逻辑，例如增加游戏内货
            item?.OnShipmentCompleted(true);
#endif
        }
        
        //MessageSystem.Instance.ShowTip("购买成功！");
        MessageSystem.Instance.HideLoadingAnimation();
        
        ShopLimitData limitData =
            GameDataManager.Instance.UserData.limitShopItems.Find((x) => x.nameid == currentShopItem.produceNameId);
        if (limitData != null)
        {
            limitData.isget = true;
            Close();
        }
    }

    private void OnPurchaseFailed(string error)
    {
        //todo 关闭loading界面
        Debug.Log("购买失败: " + error);
        AnalyticMgr.PurchaseFailed(currentShopItem.GetProduceName(),error);
        MessageSystem.Instance.HideLoadingAnimation();
    }

   
    private void OnCloseBtn()
    {
        base.Close(); // 隐藏面板
    }
    
    public override void OnHideAnimationEnd()
    {
        base.OnHideAnimationEnd();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        ClaimBtn.interactable = true;
        closeBtn.interactable = true;
        EventDispatcher.instance.TriggerUpdateLayerCoin(false,true);
    }
}
