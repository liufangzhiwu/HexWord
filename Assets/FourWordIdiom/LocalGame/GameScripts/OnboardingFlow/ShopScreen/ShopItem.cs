using System;
using System.Collections;
using System.Collections.Generic;
using Coffee.UIEffects;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 商店单个物品（type 0/5/purchaseType==-1）。
/// 继承 ShopItemBase，扩展：
///  - 广告领取逻辑（FreeGoods）与金币购买逻辑（GoldGoods）；
///  - 广告 Icon 晃动动画（CheckAdAnimationState + Update）；
///  - 单物品内容展示（"x count"）。
/// </summary>
public class ShopItem : ShopItemBase
{
    // ======================= 广告专属字段 =======================

    [SerializeField] private UIShiny adsShiny;

    [Header("广告状态动画设置")]
    public float breathSpeed = 3f;
    public float breathAmplitude = 0.03f;
    public float shakeSpeed = 15f;
    public float shakeAmplitude = 8f;
    public float shakeInterval = 2.0f;
    public float shakeDuration = 0.5f;

    private float shakeTimer = 0f;
    private bool isAdStateActive = false;

    // ======================= 生命周期 =======================

    protected override void OnEnable()
    {
        base.OnEnable();
        CheckAdAnimationState();
    }

    protected override void Update()
    {
        base.Update();

        // 广告 Icon 轻微晃动
        if (isAdStateActive && btnAdsicon != null)
        {
            shakeTimer += Time.unscaledDeltaTime;
            float currentCycleTime = shakeTimer % (shakeInterval + shakeDuration);

            if (currentCycleTime < shakeDuration)
            {
                float shakeZ = Mathf.Sin(currentCycleTime * shakeSpeed) * shakeAmplitude;
                btnAdsicon.transform.localEulerAngles = new Vector3(
                    originalIconAngles.x,
                    originalIconAngles.y,
                    originalIconAngles.z + shakeZ
                );
            }
            else
            {
                btnAdsicon.transform.localEulerAngles = originalIconAngles;
            }
        }
    }

    // ======================= 内容展示（单物品：x count） =======================

    protected override void HandleProductContentDisplay(ShopDataItem data)
    {
        if (shopCountText == null) return;

        // type == 1 道具：交给基类 HandleSpecialTypeItems 处理
        if (data.type == 1)
        {
            shopCountText.gameObject.SetActive(false);
            return;
        }

        shopCountText.gameObject.SetActive(true);

        // 单物品：显示 "x count"
        if ((data.type == 0 || data.type == 5
             || (data.purchaseType == -1 && data.productContent != null && data.productContent.Count > 0))
            && data.productContent != null && data.productContent.Count > 0
            && data.productContent[0] != null && data.productContent[0].Count >= 2)
        {
            shopCountText.text = $"x {data.productContent[0][1]}";
        }
        else
        {
            shopCountText.gameObject.SetActive(false);
        }
    }

    // ======================= 广告 + 金币购买（purchaseType == -1） =======================

    protected override void HandleSpecialPurchase()
    {
        if (shopDataItem.produceNameId == "FreeGoods")
        {
            HandleFreeGoodsClick();
            return;
        }

        if (shopDataItem.produceNameId == "GoldGoods")
        {
            HandleGoldGoodsClick();
            return;
        }

        base.HandleSpecialPurchase();
    }

    /// <summary>FreeGoods：看广告领取金币</summary>
    private void HandleFreeGoodsClick()
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

    /// <summary>GoldGoods：金币购买</summary>
    private void HandleGoldGoodsClick()
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
            if (btnAdsicon != null) btnAdsicon.gameObject.SetActive(false);
            MessageSystem.Instance.ShowTip("购买成功！");
        }
        else
        {
            MessageSystem.Instance.ShowTip("每日只能购买一次！");
            shopPriceText.text = "已购买";
        }
    }

    // ======================= 广告领取 =======================

    private IEnumerator ShowAdsRewardUI()
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
            if (btnAdsicon != null) btnAdsicon.gameObject.SetActive(true);
            AnalyticMgr.VideoAdSuccess("看广告领取商店金币");
            GameDataManager.Instance.UserData.totalSeeAds++;
        }
        else
        {
            MessageSystem.Instance.ShowTip("广告加载失败，请稍后重试。");
            AnalyticMgr.VideoAdFail("看广告领取商店金币");
        }
    }

    // ======================= 广告动画状态 =======================

    public void CheckAdAnimationState()
    {
        if (shopDataItem == null) return;

        if (shopDataItem.produceNameId == "FreeGoods" && GameDataManager.Instance.UserData.isDayFreeGet)
        {
            if (!isAdStateActive)
            {
                shakeTimer = 0f;
            }
            isAdStateActive = true;
            if (adsShiny != null) adsShiny.enabled = true;
            if (btnAdsicon != null) btnAdsicon.gameObject.SetActive(true);
        }
        else
        {
            isAdStateActive = false;
            if (adsShiny != null) adsShiny.enabled = false;

            if (isTransformCached)
            {
                if (rectTransform != null) rectTransform.localScale = originalScale;
                if (btnAdsicon != null)
                {
                    btnAdsicon.transform.localPosition = originalIconPosition;
                    btnAdsicon.transform.localEulerAngles = originalIconAngles;
                }
            }
        }
    }
}