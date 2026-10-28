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

    // [SerializeField] private Button adsButton;
    // [SerializeField] private Button goldButton;
    // [SerializeField] private Button buyButton;
    [SerializeField] private UIShiny adsShiny;

    [Header("广告可用次数显示")]
    [SerializeField] private Transform AdsCanUseTable;   // 广告可以用次数
    [SerializeField] private Text AdsUseCountText;       // 广告剩余可用次数/总可用次数

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
        // 商店金币广告位展示（只在广告位激活时上报，避免每个商品都报一条）
        if (isAdStateActive)
        {
            AnalyticMgr.VideoAdShow("看广告领取商店金币");
        }
    }

    protected override void Update()
    {
        base.Update();

        // 广告 Icon 轻微晃动
        if (isAdStateActive && btnTagicon != null)
        {
            shakeTimer += Time.unscaledDeltaTime;
            float currentCycleTime = shakeTimer % (shakeInterval + shakeDuration);

            if (currentCycleTime < shakeDuration)
            {
                float shakeZ = Mathf.Sin(currentCycleTime * shakeSpeed) * shakeAmplitude;
                btnTagicon.transform.localEulerAngles = new Vector3(
                    originalIconAngles.x,
                    originalIconAngles.y,
                    originalIconAngles.z + shakeZ
                );
            }
            else
            {
                btnTagicon.transform.localEulerAngles = originalIconAngles;
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

    // ======================= 广告可用次数显示 =======================

    /// <summary>
    /// 绑定数据后刷新「广告剩余可用次数 / 总可用次数」显示（仅 FreeGoods 金币视频商品）。
    /// AdsCanUseTable：策略干预（S=1 且 C>=0）时显示；AdsUseCountText：显示 "剩余/总"。
    /// </summary>
    public override void SetShopData(ShopDataItem data)
    {
        base.SetShopData(data);
        RefreshAdsUseCountUI();
    }

    private void RefreshAdsUseCountUI()
    {
        if (AdsCanUseTable == null) return;

        bool show = false;

        if (ShopManager.shopManager != null
            && shopDataItem != null
            && shopDataItem.produceNameId == "FreeGoods")
        {
            AdFailedParams p = ShopManager.shopManager.GetAdFailedParams();
            if (p.enabled && p.shopGoldVideoLimit >= 0)
            {
                int remain = ShopManager.shopManager.GetShopGoldVideoRemainCount();
                if (AdsUseCountText != null)
                    AdsUseCountText.text = $"{remain}/{p.shopGoldVideoLimit}";
                show = true;
            }
        }

        AdsCanUseTable.gameObject.SetActive(show);
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
            if (btnTagicon != null) btnTagicon.gameObject.SetActive(false);
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

        // ★ 广告获取失败策略 C：商店金币视频达到阈值后弹"假的广告失败"引导付费
        if (ShopManager.shopManager != null
            && ShopManager.shopManager.TryHandleShopGoldClickAsFakeFail())
        {
            yield break;
        }

        AdRuleManager.Instance.TryShowRewardVideo(Define.AdKey.RewardAdIdStoreGold, UpdateAdsRewardUI);
    }

    private void UpdateAdsRewardUI(bool isShow)
    {
        MessageSystem.Instance.HideLoadingAnimation();
        if (isShow)
        {
            List<string> giftdata = shopDataItem.productContent[0];

            CustomFlyInManager.Instance.FlyInGold(shopIcon.transform);

            // ★ 商店金币视频发放统一走 OnShopGoldClaimed（单次 100 金币 + 当日次数累计 + C 阈值兜底，C-STRAT-03）
            if (ShopManager.shopManager != null)
                ShopManager.shopManager.OnShopGoldClaimed();
            else
                GameDataManager.Instance.UserData.UpdateGold(int.Parse(giftdata[1]), true, true, "看广告领取商店金币");

            GameDataManager.Instance.UserData.isDayFreeGet = true;
            if (btnTagicon != null) btnTagicon.gameObject.SetActive(true);

            // ★ 刷新「剩余可用次数」显示
            RefreshAdsUseCountUI();

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
            if (btnTagicon != null) btnTagicon.gameObject.SetActive(true);
        }
        else
        {
            isAdStateActive = false;
            if (adsShiny != null) adsShiny.enabled = false;

            if (isTransformCached)
            {
                if (rectTransform != null) rectTransform.localScale = originalScale;
                if (btnTagicon != null)
                {
                    btnTagicon.transform.localPosition = originalIconPosition;
                    btnTagicon.transform.localEulerAngles = originalIconAngles;
                }
            }
        }
    }
}