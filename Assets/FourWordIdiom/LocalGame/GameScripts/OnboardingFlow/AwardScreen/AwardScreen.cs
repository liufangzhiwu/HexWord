using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AwardScreen : UIWindow, IBackGestureClosePanel
{
    [SerializeField] private Button closeBtn; // 关闭按钮
    [SerializeField] private Text title; // 音效文本显示
    [SerializeField] private List<GameObject> rewardList; // 语言选择文本显示

    [SerializeField] private Button ClaimBtn;
    [SerializeField] private Transform AdsTable;

    public List<RewardItem> rewardItems;

    /// <summary>
    /// 非商店场景（如分享奖励）临时指定的奖励列表。
    /// 非空时优先展示它，面板关闭后自动清空；为空则回退到 CurrentShopDataItem。
    /// </summary>
    public static List<RewardItem> OverrideRewards;

    protected override void OnEnable()
    {
        base.OnEnable();
        InitUI();
        AudioManager.Instance.PlaySoundEffect("hudieReward");
    }

    private void InitUI()
    {
        ClaimBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("ADPopReceive");
        //title.text = MultilingualManager.Instance.GetString("FirstWin");
        //tips.text = MultilingualManager.Instance.GetString("FirstWinRewards");

        // ★ 刷新 AdsTable 显隐：内购商品 ≤ 2 且存在去广告/去广告几天 → 显示
        RefreshAdsTableVisibility();

        InitAwardUI();
    }

    /// <summary>
    /// 控制 AdsTable（去广告权益说明栏）显隐：
    ///  - CurrentShopDataItem 为空 → 隐藏；
    ///  - productContent 数量 > 2 → 隐藏；
    ///  - productContent 中存在 RemoveAds / RemoveDayAds → 显示；
    ///  - 已拥有永久去广告（去广告项被隐藏）→ 隐藏；
    ///  - 其余情况 → 隐藏。
    /// </summary>
    private void RefreshAdsTableVisibility()
    {
        if (AdsTable == null) return;

        // 非商店场景（分享奖励等）不展示推荐位
        if (OverrideRewards != null)
        {
            if (AdsTable.gameObject.activeSelf)
                AdsTable.gameObject.SetActive(false);
            return;
        }

        bool show = false;

        var shopItem = Game.self != null ? Game.self.Shop.CurrentShopDataItem : null;
        if (shopItem != null && shopItem.productContent != null)
        {
            int count = shopItem.productContent.Count;

            // ★ 已拥有永久去广告 → 奖励里的去广告项不展示，配套的「去广告权益说明」栏也一起收起来
            bool hideRemoveAds = ShopManager.ShouldHideRemoveAdsReward(shopItem.produceNameId, shopItem.productContent);
            bool hasRemoveAds = false;

            for (int i = 0; i < count; i++)
            {
                var dataitem = shopItem.productContent[i];
                if (dataitem == null || dataitem.Count < 2) continue;

                int type;
                if (!int.TryParse(dataitem[0], out type)) continue;

                if (type == (int)LimitRewordType.RemoveAds
                    || type == (int)LimitRewordType.RemoveDayAds)
                {
                    hasRemoveAds = true;
                    break;
                }
            }

            show = (count <= 2) && hasRemoveAds && !hideRemoveAds;
            Debug.Log($"[AwardScreen] AdsTable 显隐: count={count}, hasRemoveAds={hasRemoveAds}, hideRemoveAds={hideRemoveAds}, show={show}");
        }
        else
        {
            Debug.Log("[AwardScreen] AdsTable 显隐: CurrentShopDataItem 为空，隐藏");
        }

        if (AdsTable.gameObject.activeSelf != show)
            AdsTable.gameObject.SetActive(show);
    }

    private void InitAwardUI()
    {
        foreach (GameObject rGameObject in rewardList)
        {
            rGameObject.gameObject.SetActive(false);
        }

        // 奖励列表统一由 CollectRewards 产出（内部已按"永久去广告已拥有"过滤去广告项），
        // 保证展示顺序 / 数量与点击领取时的飞入动画完全一致。
        List<RewardItem> rewards = CollectRewards();
        int count = Mathf.Min(rewards.Count, rewardList.Count);
        for (int i = 0; i < count; i++)
        {
            ShowItemUI(rewards[i], i);
        }

        if (OverrideRewards != null)
            Debug.Log($"[AwardScreen] 使用 OverrideRewards 展示 {count} 项奖励");
        else
            Debug.Log($"[AwardScreen] 展示 {count} 项奖励（已按去广告口径过滤）");
    }

    private void ShowItemUI(RewardItem rlist, int rewardid)
    {
        LimitRewordType type = (LimitRewordType)rlist.type;
        Image icon = rewardList[rewardid].GetComponentInChildren<Image>();
        Text count = rewardList[rewardid].GetComponentInChildren<Text>();
        rewardList[rewardid].SetActive(true);

        icon.preserveAspect = true;
        //string message = "回归首胜奖励";

        switch (type)
        {
            case LimitRewordType.Coins:
                count.text = "\u00d7" + rlist.amount;
                icon.sprite = GetSprite(type);
                //GameDataManager.Instance.UserData.UpdateGold(rlist.amount,true,true,message);
                break;
            case LimitRewordType.Butterfly:
                count.text = "\u00d7" + rlist.amount;
                icon.sprite = GetSprite(type);
                //GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Butterfly, rlist.amount,message);
                break;
            case LimitRewordType.AutoComplete:
                count.text = "\u00d7" + rlist.amount;
                icon.sprite = GetSprite(type);
                //GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, rlist.amount,message);
                break;
            case LimitRewordType.Tipstool:
                count.text = "\u00d7" + rlist.amount;
                icon.sprite = GetSprite(type);
                //GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, rlist.amount,message);
                break;
            case LimitRewordType.ReStartTool:
                icon.sprite = GetSprite(type);
                count.text = "\u00d7" + rlist.amount; // 假设 productContent 是数量
                break;
            case LimitRewordType.InfiniteEnergy:
                icon.sprite = GetSprite(type);
                count.text = rlist.amount + "时"; // 假设 productContent 是数量
                break;
            case LimitRewordType.RemoveDayAds:
                icon.sprite = GetSprite(type);
                count.text = rlist.amount + "天"; // 假设 productContent 是数量
                break;
            case LimitRewordType.RemoveAds:
                icon.sprite = GetSprite(type);
                count.text = "永久"; // 假设 productContent 是数量
                break;
        }
    }

    private Sprite GetSprite(LimitRewordType type)
    {
        switch (type)
        {
            case LimitRewordType.Coins:
                return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("gold0");
            case LimitRewordType.Butterfly:
                return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("UI_Icon_Butterfly");
            case LimitRewordType.Tipstool:
                return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("tipicon");
            case LimitRewordType.AutoComplete:
                return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("rocket");
            case LimitRewordType.Pupas:
                return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Pupas");
            case LimitRewordType.ReStartTool:
                return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("clock");
            case LimitRewordType.InfiniteEnergy:
                return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("InfiniteEnergy");
            case LimitRewordType.RemoveDayAds:
            case LimitRewordType.RemoveAds:
                return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("shopads");
        }
        return null;
    }

    protected override void InitializeUIComponents()
    {
        closeBtn.AddVibraClickAction(ClickClaimBtn); // 绑定关闭按钮事件
        ClaimBtn.AddVibraClickAction(ClickClaimBtn); // 绑定关闭按钮事件
    }

    /// <summary>
    /// 侧滑 = 「领取」按钮（本界面的关闭按钮本来就绑的是领取）。
    ///
    /// 走 base.Close() 会跳过 TriggerAwardClaimed（资源栏背景恢复）与飞入动画，
    /// 侧滑和点按钮的表现会不一致；这里统一到领取流程。_claiming 已做重复保护。
    /// </summary>
    public void CloseFromBackGesture()
    {
        ClickClaimBtn();
    }

    /// <summary>领取中标记：防止连点导致两组飞入动画同时播、面板被关两次。</summary>
    private bool _claiming;

    private void ClickClaimBtn()
    {
        if (_claiming) return;
        _claiming = true;

        // ★ 点领取：资源栏背景变实色，2 秒后渐隐回半透明
        EventDispatcher.instance.TriggerAwardClaimed();

        // ★ 2026-11-17：不再只在商店里播飞入。
        //   落点交给 CustomFlyInManager 的落点注册表解析——
        //     商店场景 → 商店顶部那排资源图标；
        //     分享奖励等非商店场景 → 顶栏金币 / 玩法内的提示灯、火箭按钮。
        //   解析不到落点时只刷新数值并关闭（不留"点完什么都没发生"的空档）。
        StartCoroutine(ShowRewardAnim());
    }

    /// <summary>
    /// 收集本次要展示/飞入的奖励，统一成 RewardItem：
    ///   OverrideRewards（分享奖励等外部传入）优先，否则取当前商店商品内容。
    ///
    /// ★ 已拥有永久去广告时，商店商品内容里的去广告项（6 永久 / 7 限时 N 天）不再展示：
    ///   否则玩家买了永久去广告后再买含「限时去广告」的礼包，领取界面还会列一条"×N 天"，
    ///   看起来像又买了一份广告权益。发放逻辑不受影响，只隐藏展示。
    /// </summary>
    private List<RewardItem> CollectRewards()
    {
        List<RewardItem> list = new List<RewardItem>();

        if (OverrideRewards != null)
        {
            list.AddRange(OverrideRewards);
            return list;
        }

        var shopItem = Game.self != null ? Game.self.Shop.CurrentShopDataItem : null;
        if (shopItem != null && shopItem.productContent != null)
        {
            bool hideRemoveAds = ShopManager.ShouldHideRemoveAdsReward(shopItem.produceNameId, shopItem.productContent);

            foreach (var dataitem in shopItem.productContent)
            {
                if (dataitem == null || dataitem.Count < 2) continue;
                if (!int.TryParse(dataitem[0], out int type)) continue;
                if (!int.TryParse(dataitem[1], out int amount)) continue;

                if (hideRemoveAds && IsRemoveAdsRewardType(type))
                {
                    Debug.Log($"[AwardScreen] 已拥有永久去广告 → 领取界面隐藏去广告项 type={type}({(LimitRewordType)type})");
                    continue;
                }

                list.Add(new RewardItem { type = type, amount = amount });
            }
        }

        return list;
    }

    /// <summary>是否为去广告类奖励（6 永久去广告 / 7 限时去广告）。</summary>
    private static bool IsRemoveAdsRewardType(int type)
    {
        return type == (int)LimitRewordType.RemoveAds
            || type == (int)LimitRewordType.RemoveDayAds;
    }

    /// <summary>没有对应资源栏的奖励（去广告 / 无限体力）不播飞入。</summary>
    private static bool IsFlyableReward(LimitRewordType type)
    {
        return type != LimitRewordType.RemoveAds
            && type != LimitRewordType.RemoveDayAds
            && type != LimitRewordType.InfiniteEnergy;
    }

    IEnumerator ShowRewardAnim()
    {
        List<RewardItem> rewards = CollectRewards();
        int pendingFlyCount = 0;
        int completedFlyCount = 0;
        bool anyFlyLaunched = false;

        Action onFlyArrived = () =>
        {
            AudioManager.Instance.PlaySoundEffect("BtnUnlock");
            completedFlyCount++;
        };

        for (int i = 0; i < rewards.Count && i < rewardList.Count; i++)
        {
            LimitRewordType type = (LimitRewordType)rewards[i].type;
            if (!IsFlyableReward(type)) continue;

            if (rewardList[i] == null || !rewardList[i].activeSelf) continue;
            Image icon = rewardList[i].GetComponentInChildren<Image>();
            if (icon == null) continue;

            // ★ 落点解析放在 FlyAwardToResource 内部：注册表 → 商店图标 → 顶栏金币，
            //   全都不可见时返回 false，这一项就不播（避免飞向隐藏对象后"凭空消失"）
            if (CustomFlyInManager.Instance == null) continue;

            pendingFlyCount++;
            if (!CustomFlyInManager.Instance.FlyAwardToResource(type, icon.transform.position, icon.gameObject, onFlyArrived))
            {
                pendingFlyCount--;   // 没有落点，不计入等待
                continue;
            }

            anyFlyLaunched = true;
            yield return new WaitForSeconds(0.1f);
        }

        // ★ 等所有飞入动画完成后，再刷新资源数值（加超时兜底，避免回调丢失导致面板永不关闭）
        float waited = 0f;
        while (completedFlyCount < pendingFlyCount && waited < 3f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!anyFlyLaunched)
             Debug.Log("[AwardScreen] 本次奖励没有可用的飞入落点，直接刷新数值并关闭");

        EventDispatcher.instance.TriggerChangeGoldUI(0, false);

        // 短暂延迟让数值更新可见，再关闭面板
        yield return new WaitForSeconds(0.15f);

        Close();
    }

    private void Close()
    {
        base.Close(); // 隐藏面板
    }

    public override void OnHideAnimationEnd()
    {
        base.OnHideAnimationEnd();
    }

    protected override void OnDisable()
    {
        // ★ 复位领取中标记，保证下次弹出还能正常点领取
        _claiming = false;

        // ★ 复位 AdsTable 显隐，避免残留
        if (AdsTable != null && AdsTable.gameObject.activeSelf)
            AdsTable.gameObject.SetActive(false);

        // ★ 清空临时奖励，避免影响下一次商店场景
        OverrideRewards = null;

        base.OnDisable();

        // ★ 界面隐藏时：资源栏背景渐隐回 120（半透明）
        EventDispatcher.instance.TriggerAwardScreenClosed();
    }
}