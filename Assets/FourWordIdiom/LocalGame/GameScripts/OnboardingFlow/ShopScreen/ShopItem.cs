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

    /// <summary>
    /// 审核态「金币视频点」被替换成 Gold6 付费金币商品（AuditStatusSwitch=1 + Gold6ReplaceVideoPos）。
    /// 为 true 时这张卡不再展示任何视频相关 UI（广告角标 / 剩余次数 / 高亮），只当普通内购卡用。
    /// </summary>
    private bool _coinPurchaseMode;

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
        if (!isAdStateActive || btnTagicon == null) return;

        shakeTimer += Time.unscaledDeltaTime;
        float currentCycleTime = shakeTimer % (shakeInterval + shakeDuration);

        if (currentCycleTime < shakeDuration)
        {
            float shakeZ = Mathf.Sin(currentCycleTime * shakeSpeed) * shakeAmplitude;

            // ★ 只有角度真的变了才写 Transform：原来每帧无条件写一次，15 张卡 × 每帧都是白做
            if (Mathf.Abs(shakeZ - _lastShakeZ) < 0.01f) return;

            _lastShakeZ = shakeZ;
            btnTagicon.transform.localEulerAngles = new Vector3(
                originalIconAngles.x,
                originalIconAngles.y,
                originalIconAngles.z + shakeZ
            );
        }
        else if (_lastShakeZ != 0f)
        {
            _lastShakeZ = 0f;
            btnTagicon.transform.localEulerAngles = originalIconAngles;
        }
    }

    /// <summary>上一帧实际写入的抖动角度（避免每帧重复写 Transform）</summary>
    private float _lastShakeZ;

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

        // ★ 商店金币（FreeGoods）：卡面数量必须与「本次点击实际到手」一致
        //   当日首次 = 免广告直发 ShopManager.ShopGoldFreeReward（50）；
        //   领过之后 = 看激励视频领取配置表金额（当前 100）。
        if (data.produceNameId == "FreeGoods")
        {
            int showGold = GameDataManager.Instance.UserData.isDayFreeGet
                ? GetVideoGoldReward()
                : ShopManager.ShopGoldFreeReward;
            shopCountText.text = $"x {showGold}";
            return;
        }

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
    /// 绑定数据后刷新「视频剩余次数 / 每日上限」显示（仅 FreeGoods 金币视频商品），X/a 显示在按钮下方。
    /// a = cypz_Mechanics 表 PropVideoTimes 第 3 位（如 5_3_5 里的 5，**不含当日首次免费**）；
    /// X=0 时视频按钮置灰、点击无效。
    /// </summary>
    public override void SetShopData(ShopDataItem data)
    {
        base.SetShopData(data);
        RefreshAdsUseCountUI();
    }

    private void RefreshAdsUseCountUI()
    {
        // 审核态换成 Gold6 付费金币卡：视频点次数不展示
        if (_coinPurchaseMode)
        {
            if (AdsCanUseTable != null) AdsCanUseTable.gameObject.SetActive(false);
            if (btnTagicon != null) btnTagicon.gameObject.SetActive(false);
            return;
        }

        if (shopDataItem == null || shopDataItem.produceNameId != "FreeGoods")
        {
            if (AdsCanUseTable != null) AdsCanUseTable.gameObject.SetActive(false);
            return;
        }
        if (ShopManager.shopManager == null) return;

        int total = ShopManager.shopManager.GetShopGoldVideoTotalLimit();
        int remain = ShopManager.shopManager.GetShopGoldVideoRemainCount();

        if (AdsCanUseTable != null)
        {
            AdsCanUseTable.gameObject.SetActive(total >= 0);
            if (total >= 0 && AdsUseCountText != null)
                AdsUseCountText.text = $"{remain}/{total}";
        }

        // X=0 → 视频按钮置灰、点击无效
        RefreshButtonInteractable();
    }

    /// <summary>
    /// FreeGoods（商店金币视频）：当日视频点次数用满后按钮置灰、点击无效。
    /// 覆盖基类，避免基类在 OnEnable/UpdateUI 时把按钮重新置为可点。
    /// </summary>
    protected override void RefreshButtonInteractable()
    {
        base.RefreshButtonInteractable();

        // 审核态换成付费金币卡：按普通内购卡处理，不做视频点置灰
        if (_coinPurchaseMode)
        {
            transform.GetComponent<Button>().interactable = true;
            if (shopPriceText != null) shopPriceText.gameObject.SetActive(true);
            return;
        }

        if (shopDataItem == null || shopDataItem.produceNameId != "FreeGoods") return;
        if (ShopManager.shopManager == null) return;

        if (ShopManager.shopManager.IsShopGoldVideoExhausted())
        {
            adsShiny.enabled = false;
            adsShiny.GetComponent<Image>().sprite = LoadShopIcon("NoCanAds");
            transform.GetComponent<Button>().interactable = false;
            btnTagicon.gameObject.SetActive(false);
            shopPriceText.gameObject.SetActive(false);
            
        }
        else
        {
            adsShiny.enabled = true;
            adsShiny.GetComponent<Image>().sprite = LoadShopIcon("UI_button_Bg");
            transform.GetComponent<Button>().interactable = true;
            // ★ 每日免费金币：当日首次是「免费直发」，不挂广告视频角标；
            //   领过之后要播激励视频，才需要角标提示。原来这里无条件打开，
            //   会把「免费」状态下也标上一个视频图标（与 FreeItemTable.InitUI 的口径相反）。
            btnTagicon.gameObject.SetActive(GameDataManager.Instance.UserData.isDayFreeGet);
            shopPriceText.gameObject.SetActive(true);
        }
    }

    /// <summary>FreeGoods：当日首次免广告直发 / 之后看广告领取</summary>
    private void HandleFreeGoodsClick()
    {
        // ★ 当日首次免费：不播广告，也**不占用视频点次数**（2026-11-17 口径变更），
        //   因此不受广告限次/假失败弹窗约束，先于闸门直接直发。
        bool isFreeClaim = !GameDataManager.Instance.UserData.isDayFreeGet;
        if (isFreeClaim)
        {
            GrantFreeGoodsReward();
            return;
        }

        // ★ 广告获取失败策略 C：走真实广告的路径才做次数闸门。
        //   达到当日上限（或处于 T 冷却）时直接弹 AdsFailScreen。
        if (ShopManager.shopManager != null
            && ShopManager.shopManager.TryHandleShopGoldClickAsFakeFail())
        {
            return;
        }

        // 编辑器下不播广告直接结账，但金额仍按「卡面显示的那一档」发，避免测试时对不上
        if (UIUtilities.isEditMode)
        {
            UpdateAdsRewardUI(true);
            return;
        }

        // ★ 之后：看激励视频，发配置表金额（100）
        AnalyticMgr.VideoAdClick("看广告领取商店金币");
        StartCoroutine(ShowAdsRewardUI());
    }

    /// <summary>
    /// 当日首次「免广告」免费领取商店金币（50）。
    /// 与看广告领取区分开：不上报视频观看成功，且**不占用视频点次数**
    /// （2026-11-17 口径变更：PropVideoTimes 第 3 位只统计看广告的次数，免费领取是额外赠送的一次）。
    /// </summary>
    private void GrantFreeGoodsReward()
    {
        int reward = ShopManager.ShopGoldFreeReward;

        // ★ 同上：先到账（不刷 UI），等金币飞入粒子播完再由回调刷新数字
        CustomFlyInManager.Instance.FlyInGold(shopIcon.transform, () => RefreshGoldUIAfterFlyIn(reward));
        GameDataManager.Instance.UserData.UpdateGold(reward, true, false, "商店免费金币","商店免费金币");

        GameDataManager.Instance.UserData.isDayFreeGet = true;

        if (ShopManager.shopManager != null)
            ShopManager.shopManager.OnShopGoldFreeClaimed();

        RefreshFreeGoodsUI();

        //LogShopItem($"免费商品直发（当日首次，免广告）: +{reward} 金币");
    }

    /// <summary>
    /// 金币飞入粒子播完后刷新金币数量（顶部金币栏 + 商店顶部资源栏都会收到 OnChangeGoldUI）。
    /// amount 传本次到账金额 → 数字从「旧值」滚到「新值」，不会先跳变。
    /// </summary>
    private void RefreshGoldUIAfterFlyIn(int amount)
    {
        EventDispatcher.instance.TriggerChangeGoldUI(Mathf.Max(0, amount), amount > 0);
    }

    /// <summary>
    /// 商店金币（FreeGoods）看视频的发放金额：取配置表 shop.csv 的商品内容（当前 100），
    /// 取不到时回退 ShopManager.ShopGoldVideoReward。
    /// </summary>
    private int GetVideoGoldReward()
    {
        try
        {
            List<string> giftdata = shopDataItem.productContent[0];
            if (int.TryParse(giftdata[1], out int gold) && gold > 0) return gold;
        }
        catch (Exception ex)
        {
            LogShopItem($"读取商店金币视频金额失败，回退 {ShopManager.ShopGoldVideoReward}: {ex.Message}");
        }
        return ShopManager.ShopGoldVideoReward;
    }

    /// <summary>领取后刷新本卡：广告角标/动画状态、卡面数量、剩余次数。</summary>
    private void RefreshFreeGoodsUI()
    {
        CheckAdAnimationState();
        if (shopDataItem != null) HandleProductContentDisplay(shopDataItem);
        RefreshAdsUseCountUI();
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
          
            // ★ 商品内容配成金币时：这次买卖=扣金币+发金币，UI 统一等飞入粒子播完再刷一次净额，
            //   免得数字先显示"已扣钱"，粒子飞完又跳一次。
            bool gainCoins = type == (int)LimitRewordType.Coins;

            GameDataManager.Instance.UserData.UpdateGold(-needgold, true, !gainCoins, string.Empty,
                "GoldGoods", ResReason.Iap);

            if (gainCoins)
            {
                GameDataManager.Instance.UserData.UpdateGold(count, true, false, string.Empty,
                    "GoldGoods", ResReason.Iap);
                CustomFlyInManager.Instance.FlyInGold(shopIcon.transform,
                    () => EventDispatcher.instance.TriggerChangeGoldUI(0, false));
            }
            else
            {
                GameDataManager.Instance.UserData.UpdateTool((LimitRewordType)type, count, string.Empty,
                    string.Empty, "GoldGoods", ResReason.Iap);
                EventDispatcher.instance.TriggerChangeGoldUI(0, true);
            }

            GameDataManager.Instance.UserData.isDayGoldBuy = true;
            shopPriceText.text = "已购买";
            if (btnTagicon != null) btnTagicon.gameObject.SetActive(false);
            MessageSystem.Instance.ShowTip("购买成功！");
        }
        else
        {
            //MessageSystem.Instance.ShowTip("每日只能购买一次！");
            shopPriceText.text = "已购买";
        }
    }

    /// <summary>
    /// 商店页「金币购买道具」的 reason_index（需求口径：金币购买道具灯泡 / 火箭 / 复活）。
    /// 与局内 GetItemScreen 的金币买道具保持一致，避免同一行为在商店页和局内落成两条原因。
    /// </summary>
    private static string GetGoldBuyReasonIndex(LimitRewordType type)
    {
        switch (type)
        {
            case LimitRewordType.Tipstool: return ResReason.IdxBuyBulb;
            case LimitRewordType.AutoComplete: return ResReason.IdxBuyRocket;
            case LimitRewordType.ReStartTool: return ResReason.IdxBuyRevive;
            default: return ResReason.IdxUnknown;
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
            int reward = GetVideoGoldReward();

            // ★ 金币飞入粒子：数据先到账，数字等金币落袋（粒子播完）后再刷新。
            //   原来 OnShopGoldClaimed 里是 UpdateGold(..., updateui:false)，发完金币没人刷 UI，
            //   顶部/商店的金币数量一直停在旧值。
            CustomFlyInManager.Instance.FlyInGold(shopIcon.transform, () => RefreshGoldUIAfterFlyIn(reward));

            // ★ 商店金币视频发放统一走 OnShopGoldClaimed（单次 100 金币 + 当日次数累计 + C 阈值兜底，C-STRAT-03）。
            //   注意：这里不能再自己 UpdateGold 一次——原来两处都发，看一次视频实际到手 200。
            if (ShopManager.shopManager != null)
                ShopManager.shopManager.OnShopGoldClaimed(reward);
            else
                GameDataManager.Instance.UserData.UpdateGold(reward, true, false, "看广告领取商店金币");

            GameDataManager.Instance.UserData.isDayFreeGet = true;
            if (btnTagicon != null) btnTagicon.gameObject.SetActive(true);

            RefreshFreeGoodsUI();

            AnalyticMgr.VideoAdSuccess("看广告领取商店金币");
            GameDataManager.Instance.UserData.totalSeeAds++;

            //LogShopItem($"免费商品看视频领取: +{reward} 金币");
        }
        else
        {
            MessageSystem.Instance.ShowTip("广告加载失败，请稍后重试。");
            AnalyticMgr.VideoAdFail("看广告领取商店金币");
        }
    }

    // ======================= 广告动画状态 =======================

    /// <summary>
    /// 审核态：这张「金币视频点」卡是否已被 Gold6 付费金币商品替代。
    /// 商店（FreeItemTable）绑定数据后调用，用来收起广告角标 / 剩余次数 / 高亮。
    /// </summary>
    public void SetCoinPurchaseMode(bool active)
    {
        _coinPurchaseMode = active;

        if (active)
        {
            isAdStateActive = false;
            _lastShakeZ = 0f;
            if (adsShiny != null) adsShiny.enabled = false;
            if (btnTagicon != null) btnTagicon.gameObject.SetActive(false);
            if (AdsCanUseTable != null) AdsCanUseTable.gameObject.SetActive(false);
        }

        RefreshAdsUseCountUI();
        RefreshButtonInteractable();
    }

    public void CheckAdAnimationState()
    {
        if (_coinPurchaseMode)
        {
            isAdStateActive = false;
            _lastShakeZ = 0f;
            if (adsShiny != null) adsShiny.enabled = false;
            if (btnTagicon != null) btnTagicon.gameObject.SetActive(false);
            if (AdsCanUseTable != null) AdsCanUseTable.gameObject.SetActive(false);
            return;
        }

        if (shopDataItem == null) return;

        bool isFreeGoods = shopDataItem.produceNameId == "FreeGoods";
        bool needVideo = isFreeGoods && GameDataManager.Instance.UserData.isDayFreeGet;

        if (needVideo)
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
            _lastShakeZ = 0f;
            if (adsShiny != null) adsShiny.enabled = false;

            // ★ 每日免费金币在「当日首次免费还没领」时是免广告直发 → 不挂广告视频角标。
            //   （非 FreeGoods 商品不动 btnTagicon：那可能是金币图标等其它角标）
            if (isFreeGoods && btnTagicon != null) btnTagicon.gameObject.SetActive(false);

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