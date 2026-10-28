using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通用礼包弹窗（原「限时折扣」界面改造）。
/// 现在承载两类主动弹窗（需求 C-STRAT-08 破冰 / C-STRAT-09 复购升层）：
///   1. 破冰礼包：去广告周卡，引导首次付费，限时 = 本次周末/节假日区间；
///   2. 复购/升层礼包：加量不加价，额外金币 = 取整(金币价值 × 10%)。
///
/// 数据来源：ShopManager.SetPendingGiftPopup（pendingGiftPopupItem / Mode / EndTime）。
/// 原有「限时宝箱倒计时必填」逻辑已移除：仅在存在有效限时截止时才显示倒计时。
/// 购买统一走 Game.self.Shop.Purchase → ShopManager.OnPurchaseSuccess（发奖 + AwardScreen + 购买记录）。
/// </summary>
public class AdsDiscountScreen : UIWindow
{
    [SerializeField] private Button closeBtn;      // 关闭按钮
    [SerializeField] private Text titleText;       // 标题（未在 prefab 绑定时留空即可）
    [SerializeField] private Text timeText;        // 倒计时（无限时时隐藏）
    [SerializeField] private Text priceText;       // 价格
    [SerializeField] private Text discountText;    // 划线原价
    [SerializeField] private Transform parent;     // 礼包内容容器
    [SerializeField] private GiftItem giftItempPefab;
    [SerializeField] private GameObject discountObj;  // 折扣角标
    [SerializeField] private GameObject circle;        // 加载态
    [SerializeField] private Button ClaimBtn;          // 购买按钮

    private ObjectPool objectPool;
    private ShopDataItem currentShopItem;
    private readonly List<GiftItem> GiftItems = new List<GiftItem>();

    private ShopManager.GiftPopupMode _mode = ShopManager.GiftPopupMode.None;
    private DateTime _endTime = DateTime.MinValue;
    private bool _purchasing;

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
        if (closeBtn != null) closeBtn.AddVibraClickAction(OnCloseBtn);
        if (ClaimBtn != null) ClaimBtn.AddClickAction(OnBuyButtonClicked);
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        // 优先取主动弹窗数据（破冰/复购）；无则回退旧的 curshopAdsItem
        if (ShopManager.shopManager != null)
        {
            _mode = ShopManager.shopManager.pendingGiftPopupMode;
            _endTime = ShopManager.shopManager.pendingGiftPopupEndTime;
            currentShopItem = ShopManager.shopManager.pendingGiftPopupItem;

            if (currentShopItem == null && _mode == ShopManager.GiftPopupMode.None)
            {
                currentShopItem = ShopManager.shopManager.curshopAdsItem;
                _mode = ShopManager.GiftPopupMode.None;
                _endTime = DateTime.MinValue;
            }
        }

        if (currentShopItem == null)
        {
            Debug.LogError("[AdsDiscountScreen] 未取到礼包数据（pendingGiftPopupItem 与 curshopAdsItem 均为空），关闭弹窗");
            Close();
            return;
        }

        _purchasing = false;

        InitUI();
        EventDispatcher.instance.TriggerUpdateLayerCoin(true, false);
        InitGiftItems();
        if (HasCountdown())
            StartCoroutine(UpdateTime());
        AudioManager.Instance.PlaySoundEffect("ShowUI");
    }

    private bool HasCountdown()
    {
        return _endTime != DateTime.MinValue && _endTime > DateTime.Now && timeText != null;
    }

    private void InitUI()
    {
        if (currentShopItem == null) return;

        if (titleText != null)
            titleText.text = GetTitleText();

        bool hasDiscount = !string.IsNullOrEmpty(currentShopItem.discount);
        if (discountObj != null) discountObj.SetActive(hasDiscount);
        if (discountText != null) discountText.gameObject.SetActive(hasDiscount);

        InitPriceText(hasDiscount);

        if (timeText != null)
            timeText.gameObject.SetActive(HasCountdown());
    }

    private string GetTitleText()
    {
        switch (_mode)
        {
            case ShopManager.GiftPopupMode.BreakIce:
                return GetLocalized("BreakIceTitle", "限时特惠");
            case ShopManager.GiftPopupMode.Repurchase:
                return GetLocalized("RepurchaseTitle", "加量不加价");
            default:
                return string.IsNullOrEmpty(currentShopItem.name)
                    ? GetLocalized("GiftTitle", "限时特惠")
                    : currentShopItem.name;
        }
    }

    private void InitPriceText(bool needDiscount)
    {
        if (currentShopItem == null)
        {
            Debug.LogWarning("[AdsDiscountScreen] 当前商店项为空");
            ShowLoadingState(true);
            return;
        }

        try
        {
            float price = currentShopItem.price;
            CultureInfo culture = UIUtilities.GetCultureForCurrency("");

            if (priceText != null)
                priceText.text = UIUtilities.FormatCurrency(price, culture);

            if (needDiscount)
            {
                if (float.TryParse(currentShopItem.discount.TrimEnd('%'), out float discountPercent))
                {
                    discountPercent = Mathf.Abs(discountPercent);
                    decimal discountRate = (decimal)(discountPercent / 100f);
                    decimal originalPrice = (decimal)price / discountRate;
                    if (discountText != null)
                        discountText.text = UIUtilities.FormatCurrency(originalPrice, culture);

                    Text badgeText = discountObj != null ? discountObj.GetComponentInChildren<Text>() : null;
                    if (badgeText != null)
                        badgeText.text = $"{currentShopItem.discount}";
                }
                else
                {
                    Debug.LogWarning($"[AdsDiscountScreen] 折扣格式无效: {currentShopItem.discount}");
                    if (discountText != null) discountText.text = "N/A";
                }
            }

            ShowLoadingState(false);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AdsDiscountScreen] 初始化价格文本出错: {ex.Message}");
            ShowLoadingState(true);
        }
    }

    private void ShowLoadingState(bool isLoading)
    {
        if (circle != null) circle.SetActive(isLoading);
        if (priceText != null) priceText.gameObject.SetActive(!isLoading);
        if (discountText != null && !string.IsNullOrEmpty(currentShopItem.discount))
            discountText.gameObject.SetActive(!isLoading);
    }

    private IEnumerator UpdateTime()
    {
        while (HasCountdown())
        {
            TimeSpan span = _endTime.Subtract(DateTime.Now);
            if (timeText != null)
                timeText.text = UIUtilities.FormatTimeRemaining(span);
            yield return new WaitForSeconds(1f);
        }

        // 限时结束：关闭弹窗并清理数据
        if (timeText != null) timeText.text = "";
        OnCloseBtn();
    }

    private void InitGiftItems()
    {
        if (currentShopItem == null || currentShopItem.productContent == null) return;

        for (int i = 0; i < currentShopItem.productContent.Count; i++)
        {
            List<string> itemdata = currentShopItem.productContent[i];
            if (GiftItems.Count > i)
            {
                GiftItem giftItem = GiftItems[i];
                giftItem.SetShopData(itemdata, currentShopItem.id, Color.white, currentShopItem.pointDes);
                giftItem.gameObject.SetActive(true);
            }
            else
            {
                GiftItem giftItem = objectPool.GetObject<GiftItem>(parent);
                giftItem.SetShopData(itemdata, currentShopItem.id, Color.white, currentShopItem.pointDes);
                GiftItems.Add(giftItem);
            }
        }

        // 多余的历史项隐藏（上一次礼包内容更多时）
        for (int i = currentShopItem.productContent.Count; i < GiftItems.Count; i++)
            GiftItems[i].gameObject.SetActive(false);
    }

    private void OnBuyButtonClicked()
    {
        if (currentShopItem == null || _purchasing) return;
        _purchasing = true;

        MessageSystem.Instance.ShowLoadingAnimation();

        if (UIUtilities.isEditMode)
        {
            ProductItem productItem = new ProductItem
            {
                order_id = "",
                IsoCurrencyCode = "",
                ItemName = currentShopItem.produceNameId,
                ProductId = currentShopItem.produceNameId,
                LocalizedPrice = 0,
            };
            OnPurchaseSuccess(productItem);
            return;
        }

        AnalyticMgr.PurchaseStart(currentShopItem.produceNameId);
        Game.self.Shop.Purchase(currentShopItem.GetProduceName(), OnPurchaseSuccess, OnPurchaseFailed);
    }

    private void OnPurchaseSuccess(ProductItem item)
    {
        _purchasing = false;
        Debug.Log($"[AdsDiscountScreen] 购买成功: {item?.ProductId}, mode={_mode}");

        // 统一走商店的购买成功处理：发放内容 + 内购成功弹窗 + 购买记录 + 统计 + 失败分归零
        if (ShopManager.shopManager != null)
            ShopManager.shopManager.OnPurchaseSuccess(item);

        MessageSystem.Instance.HideLoadingAnimation();
        Close();
    }

    private void OnPurchaseFailed(string error)
    {
        _purchasing = false;
        Debug.Log($"[AdsDiscountScreen] 购买失败: {error}");
        AnalyticMgr.PurchaseFailed(currentShopItem?.GetProduceName(), error);
        MessageSystem.Instance.HideLoadingAnimation();

        // 拉起系统支付后未完成购买（取消/失败），由商店统一弹「取消购买」弹窗
        if (currentShopItem != null && ShopManager.shopManager != null)
            ShopManager.shopManager.OnPurchaseCanceled(currentShopItem.produceNameId, true);
    }

    private void OnCloseBtn()
    {
        base.Close();
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (ClaimBtn != null) ClaimBtn.interactable = true;
        if (closeBtn != null) closeBtn.interactable = true;
        EventDispatcher.instance.TriggerUpdateLayerCoin(false, true);

        // 清理主动弹窗数据，避免下次打开误用旧礼包
        if (ShopManager.shopManager != null)
            ShopManager.shopManager.ClearPendingGiftPopup();
    }

    private string GetLocalized(string key, string fallback)
    {
        if (MultilingualManager.Instance == null) return fallback;
        string value = MultilingualManager.Instance.GetString(key);
        if (string.IsNullOrEmpty(value) || value == key) return fallback;
        return value;
    }
}
