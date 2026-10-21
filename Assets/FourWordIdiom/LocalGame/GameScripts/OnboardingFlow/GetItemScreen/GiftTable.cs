using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
    [SerializeField] private Text shopOldPriceText;
    [SerializeField] private Text shopPriceText;
    [SerializeField] private Text giftNameText;
    [SerializeField] private Text discount; // 折扣
    [SerializeField] private List<GiftItem> _giftItems;
    [SerializeField] private GiftItem _AddgiftItem; // 加赠物品（独立样式，数据源：productContent 里 IsBonusGift==true 的项）
    [SerializeField] private Text limintCountText; // 限购次数
    [SerializeField] private Text tagText; // 促销标签

    public static LimitRewordType limitRewordType;

    private string eventDes;

    private ShopDataItem shopDataItem;

    private Color color;

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
        if (shopItem == null)
        {
            LogShop("InitUI 跳过: shopItem 为空");
            return;
        }

        transform.GetComponent<Image>().sprite = LoadShopIcon(shopItem.showIcon);
        
        limitRewordType = limitReType;
        shopDataItem = shopItem;

        // ---------- 折扣 ----------
        if (discount != null)
        {
            if (string.IsNullOrEmpty(shopItem.discount))
            {
                discount.transform.parent.gameObject.SetActive(false);
            }
            else
            {
                discount.transform.parent.gameObject.SetActive(true);
                discount.text = shopItem.discount;
            }
        }

        List<Outline> Outlines = giftNameText.GetComponents<Outline>().ToList();
        List<Outline> Outlinelimits = limintCountText.GetComponents<Outline>().ToList();
        color = new Color(0f, 0.1529412f, 0.4745098f, 1f);
        
        
        // ---------- 礼包名 ----------
        switch (limitRewordType)
        {
            case LimitRewordType.ReStartTool:
                giftNameText.text = shopItem.name;
                limintCountText.transform.parent.GetComponent<Image>().sprite= LoadShopIcon("faillimitbg");
                color= new Color(0f, 0.1529412f, 0.4745098f, 1f);
                break;
            case LimitRewordType.Butterfly:
                break;
            case LimitRewordType.AutoComplete:
                giftNameText.text = shopItem.name;
                limintCountText.transform.parent.GetComponent<Image>().sprite= LoadShopIcon("autolimitbg");
                break;
            case LimitRewordType.Tipstool:
                giftNameText.text = shopItem.name;
                limintCountText.transform.parent.GetComponent<Image>().sprite= LoadShopIcon("tiplimitbg");
                color= new Color(0.4941177f, 0f, 0.03529412f, 1f);
                break;
        }

        for (int i = 1; i < Outlines.Count; i++)
        {
            Outlines[i].effectColor = color;
        }
        
        for (int i = 0; i < Outlinelimits.Count; i++)
        {
            Outlinelimits[i].effectColor = color;
        }
            
        // ---------- 礼包内容（跳过加赠项，加赠走 _AddgiftItem 单独显示） ----------
        RefreshGiftContent();

        // ---------- 加赠物品（独立样式，从 productContent 中识别） ----------
        RefreshBonusItem();

        // ---------- 限购次数 ----------
        RefreshLimitCount();

        // ---------- 促销标签 ----------
        RefreshTag();

        eventDes = giftNameText.text;
        SetProductPrice();

        LogShop($"InitUI 完成: limitRewordType={limitRewordType}, price={shopItem.price}, " +
                $"giftType={shopItem.giftType}, discount={shopItem.discount}, " +
                $"productContent.Count={(shopItem.productContent != null ? shopItem.productContent.Count : 0)}");
    }

    // ======================= 礼包内容（排除加赠项） =======================

    private void RefreshGiftContent()
    {
        for (int i = 0; i < _giftItems.Count; i++)
        {
            _giftItems[i].gameObject.SetActive(false);
        }

        if (shopDataItem.productContent == null) return;

        int giftIndex = 0;
        for (int i = 0; i < shopDataItem.productContent.Count; i++)
        {
            List<string> giftdata = shopDataItem.productContent[i];
            if (giftdata == null || giftdata.Count < 2)
            {
                LogShop($"RefreshGiftContent: productContent[{i}] 数据不合法，跳过");
                continue;
            }

            int giftType = int.Parse(giftdata[0]);

            if (ShopManager.shopManager != null
                && ShopManager.shopManager.IsBonusGift(shopDataItem.produceNameId, giftType))
            {
                LogShop($"RefreshGiftContent: 内容[{i}] type={giftType} 是加赠物品，跳过（交给 _AddgiftItem）");
                continue;
            }

            if (giftIndex >= _giftItems.Count)
            {
                LogShop($"RefreshGiftContent: _giftItems 数量不足 (giftIndex={giftIndex})，剩余内容不展示");
                break;
            }

            GiftItem giftItem = _giftItems[giftIndex];
            giftItem.gameObject.SetActive(true);
            giftItem.SetShopData(giftdata, shopDataItem.id,color);
            giftIndex++;

            LogShop($"RefreshGiftContent: 礼包 {shopDataItem.produceNameId} 内容[{giftIndex - 1}] type={giftType}");
        }
    }

    // ======================= 加赠物品（独立样式） =======================

    private void RefreshBonusItem()
    {
        if (_AddgiftItem == null) return;

        List<string> bonusData = FindBonusItemInProductContent();
        if (bonusData == null || bonusData.Count < 2)
        {
            _AddgiftItem.gameObject.SetActive(false);
            LogShop("未找到加赠项，隐藏 _AddgiftItem");
            return;
        }

        _AddgiftItem.gameObject.SetActive(true);
        _AddgiftItem.SetShopData(bonusData, shopDataItem.id,color);
        _AddgiftItem.SetBonus(true);

        LogShop($"加赠物品已显示: type={bonusData[0]}, count={bonusData[1]}");
    }

    private List<string> FindBonusItemInProductContent()
    {
        if (shopDataItem == null || shopDataItem.productContent == null) return null;
        if (ShopManager.shopManager == null) return null;

        for (int i = 0; i < shopDataItem.productContent.Count; i++)
        {
            List<string> giftdata = shopDataItem.productContent[i];
            if (giftdata == null || giftdata.Count < 2) continue;

            int giftType;
            if (!int.TryParse(giftdata[0], out giftType)) continue;

            if (ShopManager.shopManager.IsBonusGift(shopDataItem.produceNameId, giftType))
                return giftdata;
        }
        return null;
    }
    
    private Sprite LoadShopIcon(string showIcon)
    {
        Sprite sprite = null;
        sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(showIcon);

        return sprite;
    }

    // ======================= 限购次数 =======================

    /// <summary>
    /// 刷新限购次数。
    ///  - limited == 0（不限购）→ 隐藏；
    ///  - limited > 0 → 显示 "仅限 X 次"，X 用中文数字（一~十），超过 10 用阿拉伯数字兜底；
    ///  - 已买满 → "已售空"。
    /// ★ 中文数字转换统一走 UIUtilities.GetChineseNumber。
    /// </summary>
    private void RefreshLimitCount()
    {
        if (limintCountText == null) return;

        if (shopDataItem == null || shopDataItem.limited <= 0)
        {
            limintCountText.transform.parent.gameObject.SetActive(false);
            return;
        }

        int buyCount = 0;
        ShopLimitData limitData = GameDataManager.Instance?.UserData?.limitShopItems?
            .Find(x => x != null && x.nameid == shopDataItem.produceNameId);
        if (limitData != null)
            buyCount = limitData.buyCount;

        int remain = Mathf.Max(0, shopDataItem.limited - buyCount);

        if (remain <= 0)
            limintCountText.text = "已售空";
        else
            limintCountText.text = $"仅限{UIUtilities.GetChineseNumber(remain)}次";

        limintCountText.transform.parent.gameObject.SetActive(true);
        LogShop($"限购次数: limited={shopDataItem.limited}, buyCount={buyCount}, remain={remain}, 显示='{limintCountText.text}'");
    }

    // ======================= 促销标签 =======================

    private void RefreshTag()
    {
        if (tagText == null) return;

        if (shopDataItem == null)
        {
            tagText.transform.parent.gameObject.SetActive(false);
            return;
        }

        string tag = "";

        if (ShopManager.shopManager != null && ShopManager.shopManager.IsLastPurchasedPack(shopDataItem))
        {
            tag = MultilingualManager.Instance?.GetString("LastPurchased") ?? "上次购买";
        }
        else if (!string.IsNullOrEmpty(shopDataItem.labelZh))
        {
            tag = shopDataItem.labelZh;
        }
        else if (IsActivityPackProduct(shopDataItem))
        {
            tag = GetActivityPackTag();
        }

        if (string.IsNullOrEmpty(tag))
        {
            tagText.transform.parent.gameObject.SetActive(false);
        }
        else
        {
            tagText.text = tag;
            tagText.transform.parent.gameObject.SetActive(true);
            LogShop($"促销标签: {tag}");
        }
    }

    private bool IsActivityPackProduct(ShopDataItem item)
    {
        if (item == null) return false;
        ShopLimitData limitData = GameDataManager.Instance?.UserData?.limitShopItems?
            .Find(x => x != null && x.nameid == item.produceNameId);
        return limitData != null && limitData.isActivityPack;
    }

    private string GetActivityPackTag()
    {
        DayOfWeek dayOfWeek = DateTime.Now.DayOfWeek;
        if (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday)
            return MultilingualManager.Instance?.GetString("WeekendSpecialOffer") ?? "周末特惠";

        return MultilingualManager.Instance?.GetString("HolidaySpecialOffer") ?? "节日特惠";
    }

    // ======================= 价格 =======================

    private void SetProductPrice()
    {
        if (shopPriceText == null) return;

        Debug.Log($"获取商品内购名称: {shopDataItem.GetProduceName()}");

        try
        {
            float price = shopDataItem.price;
            float oldprice = shopDataItem.originalPrice;
            CultureInfo culture = UIUtilities.GetCultureForCurrency("");

            shopPriceText.text = UIUtilities.FormatCurrency(price, culture);

            if (shopOldPriceText != null)
            {
                if (oldprice <= 0)
                {
                    shopOldPriceText.gameObject.SetActive(false);
                    shopPriceText.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 0);
                }
                else
                {
                    shopOldPriceText.gameObject.SetActive(true);
                    shopPriceText.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -19);
                    shopOldPriceText.text = UIUtilities.FormatCurrency(oldprice, culture);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error setting product price: {ex.Message}");
        }
    }

    private void InitEvent()
    {
        BuyBtn.AddClickAction(OnBuyButtonClicked);
    }

    private void OnBuyButtonClicked()
    {
        LogShop("点击购买");
        MessageSystem.Instance.ShowLoadingAnimation();

        if (UIUtilities.isEditMode)
        {
            ProductItem productItem = new ProductItem
            {
                order_id = "",
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
            item?.OnShipmentCompleted(true);
#endif
        }

        MessageSystem.Instance.HideLoadingAnimation();

        LogShop($"========== 购买成功结束: {shopDataItem.produceNameId} ==========");
    }

    private void OnPurchaseFailed(string error)
    {
        LogShop($"购买失败: {error}");
        AnalyticMgr.PurchaseFailed(shopDataItem.GetProduceName(), error);
        MessageSystem.Instance.HideLoadingAnimation();
    }
}