using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Mime;
using DG.Tweening;
#if UNITY_HUAWEI
using HuaweiService.CloudStorage;
#endif
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

    /// <summary>[Shop] 标签日志</summary>
    private void LogShop(string message)
    {
        string tag = shopDataItem != null ? $"{shopDataItem.produceNameId}(id={shopDataItem.id})" : "null";
        Debug.Log($"[Shop][GiftTable] [{tag}] {message}");
    }

    private void Start()
    {
        InitEvent();
    }
 

    public void InitUI(LimitRewordType limitReType, ShopDataItem shopItem)
    {
        limitRewordType = limitReType;
        shopDataItem = shopItem;
        
        discount.text = shopItem.discount;
        
        switch (limitRewordType)
        {
            case LimitRewordType.ReStartTool:
                giftNameText.text = shopItem.name;
                break;
            case LimitRewordType.Butterfly:
                //AwardIcon.sprite= AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("UI_Icon_Butterfly");
                break;
            case LimitRewordType.AutoComplete:
                giftNameText.text = shopItem.name;
                break;
            case LimitRewordType.Tipstool:
                giftNameText.text = shopItem.name;
                break;
        }
        
        for (int i = 0; i < _giftItems.Count; i++)
        {
            GiftItem giftItem = _giftItems[i];
            giftItem.gameObject.SetActive(false);
        }

        // ✅ 新代码
        for (int i = 0; i < shopDataItem.productContent.Count; i++)
        {
            if (i >= _giftItems.Count)
            {
                LogShop($"GiftTable.InitUI: _giftItems 数量不足 (i={i}, 需要={shopDataItem.productContent.Count})，跳过");
                break;
            }

            List<string> giftdata = shopDataItem.productContent[i];
            GiftItem giftItem = _giftItems[i];
            giftItem.gameObject.SetActive(true);
            giftItem.SetShopData(giftdata, shopDataItem.id);

            // ★ 设置加赠标识
            int giftType = int.Parse(giftdata[0]);
            bool isBonus = ShopManager.shopManager != null
                           && ShopManager.shopManager.IsBonusGift(shopDataItem.produceNameId, giftType);
            giftItem.SetBonus(isBonus);

            LogShop($"GiftTable.InitUI: 礼包 {shopDataItem.produceNameId} 内容[{i}] type={giftType}, isBonus={isBonus}");
        }
        
        eventDes = giftNameText.text;
        SetProductPrice();

        LogShop($"InitUI 完成: limitRewordType={limitRewordType}, price={shopItem.price}, " +
                $"giftType={shopItem.giftType}, discount={shopItem.discount}, " +
                $"productContent.Count={(shopItem.productContent != null ? shopItem.productContent.Count : 0)}");
    }
    
    private void SetProductPrice()
    {
        if (shopPriceText == null) return;

        Debug.Log($"获取商品内购名称: {shopDataItem.GetProduceName()}");

        try
        {
            float price = shopDataItem.price;
            CultureInfo culture = UIUtilities.GetCultureForCurrency("");
           
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
        LogShop("点击购买");
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
        LogShop($"========== 购买成功开始: ProductId={item?.ProductId}, ItemName={item?.ItemName}, LocalizedPrice={item?.LocalizedPrice} ==========");

        shopDataItem = ShopManager.shopManager.GetProduct(item.ProductId);
        if (shopDataItem == null)
        {
            LogShop($"购买成功但商店未找到该商品: {item.ProductId}");
            return;
        }

        if (shopDataItem.GetProduceName() == item.ProductId)
        {
            SystemManager.Instance.HidePanel(PanelType.GetItemScreen);

            Game.self.Shop.CurrentShopDataItem = shopDataItem.DeepCopy();

            SystemManager.Instance.ShowPanel(PanelType.AwardScreen);

            // =====================================================
            // ★ 统一交给 ShopManager 处理：
            //   1. 发放 productContent 里的资源（金币、道具等）
            //   2. 去广告（永久 RemoveAds / 限时 RemoveDayAds）续期或新建
            //   3. 推荐位跟踪（付费倾向积分 / 探价加分 / 转化失败分归零 / buyCount++ / 冷却 / 上次购买）
            //   4. 打印 [Shop] 日志
            // =====================================================
            if (ShopManager.shopManager != null)
            {
                ShopManager.shopManager.HandleGiftTablePurchaseRewards(shopDataItem, item);
            }
            else
            {
                LogShop("ShopManager.shopManager 为空，无法处理购买后逻辑（回退：仅打印）");
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

        LogShop($"支付统计: firstPay={firstPay}, TotalPayTimes={GameDataManager.Instance.UserData.TotalPayTimes}, " +
                $"TotalRevenue={GameDataManager.Instance.UserData.TotalRevenue}");

        if (!UIUtilities.isEditMode)
        {
            AnalyticMgr.PurchaseFinished(item, firstPay);
            
#if UNITY_HUAWEI
         // 处理购买成功后的逻辑，例如增加游戏内货
            item?.OnShipmentCompleted(true);
#endif
        }
        
        //MessageSystem.Instance.ShowTip("购买成功！");
        MessageSystem.Instance.HideLoadingAnimation();

        LogShop($"========== 购买成功结束: {shopDataItem.produceNameId} ==========");
        
        //EventDispatcher.instance.TriggerChangeGoldUI(0, false);
    }
    
    private void OnPurchaseFailed(string error)
    {
        LogShop($"购买失败: {error}");
        AnalyticMgr.PurchaseFailed(shopDataItem.GetProduceName(), error);
        MessageSystem.Instance.HideLoadingAnimation();
    }

}