using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using DG.Tweening;
using HuaweiService.CloudStorage;
using Middleware;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GiftTable : MonoBehaviour
{
    [SerializeField] private Button BuyBtn;
    [SerializeField] private Text shopPriceText; // 
    [SerializeField] private Text giftNameText; // 
    [SerializeField] private Text discount; // 语言选择文本显示
    [SerializeField] private List<GiftItem> _giftItems; // 语言选择文本显示
 
    public static LimitRewordType limitRewordType;

    private string eventDes;

    private ShopDataItem shopDataItem;

    private void Start()
    {
        InitEvent();
    }
 

    public void InitUI(LimitRewordType limitReType,ShopDataItem shopItem)
    {
        limitRewordType=limitReType;
        shopDataItem=shopItem;
        
        discount.text=shopItem.discount;
        
        switch (limitRewordType)
        {
            case LimitRewordType.Coins:
                // if(max)
                //     AwardIcon.sprite= AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("Coin2");
                //AwardIcon.sprite= AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("Coin1");
                break;
            case LimitRewordType.Butterfly:
                //AwardIcon.sprite= AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("UI_Icon_Butterfly");
                break;
            case LimitRewordType.Tipstool:
                giftNameText.text ="放大镜礼包";
                break;
            case LimitRewordType.SingleTipsttool:
                giftNameText.text ="提示灯礼包";
                break;
        }

        for (int i = 0; i < shopDataItem.productContent.Count; i++)
        {
            List<string> giftdata=shopDataItem.productContent[i];
            GiftItem giftItem = _giftItems[i];
            giftItem.SetShopData(giftdata, shopDataItem.id);
        }
        
        eventDes=giftNameText.text;
        SetProductPrice();
        //AnalyticMgr.VideoAdShow(eventDes);
    }
    
    private void SetProductPrice()
    {
        if (shopPriceText == null) return;

        Debug.Log($"获取商品内购名称: {shopDataItem.GetProduceName()}");

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
            float price = shopDataItem.price;
            // 获取合适的文化信息
            CultureInfo culture = UIUtilities.GetCultureForCurrency("");
#endif
           
            shopPriceText.text = UIUtilities.FormatCurrency(price, culture);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error setting product price: {ex.Message}");
        }
    }

    private void InitEvent()
    {
        BuyBtn.AddClickAction(OnBuyButtonClicked); // 绑定关闭按钮事件
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
                ItemName = shopDataItem.produceNameId,
                ProductId = shopDataItem.produceNameId,
                LocalizedPrice = 0,
            };
            OnPurchaseSuccess(productItem);
        }
        else
        {
            AnalyticMgr.PurchaseStart(shopDataItem.produceNameId);
            //todo 打开loading界面
            Game.self.Shop.Purchase(shopDataItem.GetProduceName(), OnPurchaseSuccess, OnPurchaseFailed);
        }
    }

    private void OnPurchaseSuccess(ProductItem item)
    {
        //todo 关闭loading界面
        Debug.Log("购买成功: " + item.ProductId);
        shopDataItem=ShopManager.shopManager.GetProduct(item.ProductId);
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
                        GameDataManager.Instance.UserData.UpdateGold(count, true, true,giftNameText+"购买金币");
                        break;
                    case (int)LimitRewordType.Butterfly:
                        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Butterfly,count,giftNameText+"商店购买蝴蝶");
                        break;
                    case (int)LimitRewordType.Tipstool://放大镜道具，整个词语提示
                        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool,count,giftNameText+"商店购买放大镜");
                        break;
                    case (int)LimitRewordType.SingleTipsttool://提示灯道具，单个字符提示
                        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.SingleTipsttool,count,giftNameText+"商店购买提示灯");
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
            
#if UNITY_huawei
         // 处理购买成功后的逻辑，例如增加游戏内货
            item?.OnShipmentCompleted(true);
#endif
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

}
