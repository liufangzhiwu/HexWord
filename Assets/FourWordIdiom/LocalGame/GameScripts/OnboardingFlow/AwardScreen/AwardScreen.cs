using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AwardScreen : UIWindow
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
    /// 控制 AdsTable 显隐：
    ///  - CurrentShopDataItem 为空 → 隐藏；
    ///  - productContent 数量 > 2 → 隐藏；
    ///  - productContent 中存在 RemoveAds / RemoveDayAds → 显示；
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

            show = (count <= 2) && hasRemoveAds;
            Debug.Log($"[AwardScreen] AdsTable 显隐: count={count}, hasRemoveAds={hasRemoveAds}, show={show}");
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

        // 分享等非商店场景：直接渲染外部传入的奖励列表
        if (OverrideRewards != null)
        {
            int count = Mathf.Min(OverrideRewards.Count, rewardList.Count);
            for (int i = 0; i < count; i++)
            {
                ShowItemUI(OverrideRewards[i], i);
            }
            Debug.Log($"[AwardScreen] 使用 OverrideRewards 展示 {count} 项奖励");
            return;
        }

        //var items = new List<AnalyticMgr.Item>();
        if (Game.self.Shop.CurrentShopDataItem != null)
        {
            for (int i = 0; i < Game.self.Shop.CurrentShopDataItem.productContent.Count; i++)
            {
                var dataitem = Game.self.Shop.CurrentShopDataItem.productContent[i];

                RewardItem rewardItem = new RewardItem()
                {
                    type = int.Parse(dataitem[0]),
                    amount = int.Parse(dataitem[1])
                };
                int reward = i;
                ShowItemUI(rewardItem, reward);
            }
        }
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

    private void ClickClaimBtn()
    {
        // ★ 点领取：资源栏背景变实色，2 秒后渐隐回半透明
        EventDispatcher.instance.TriggerAwardClaimed();

        if (SystemManager.Instance.PanelIsShowing(PanelType.ShopScreen))
        {
            StartCoroutine(ShowRewardAnim());
        }
        else
        {
            EventDispatcher.instance.TriggerChangeGoldUI(0, false);
            Close();
        }
    }

    IEnumerator ShowRewardAnim()
    {
        int pendingFlyCount = 0;
        int completedFlyCount = 0;

        if (Game.self.Shop.CurrentShopDataItem != null)
        {
            int rewardid = 0;
            foreach (var dataitem in Game.self.Shop.CurrentShopDataItem.productContent)
            {
                // 每个奖励单独判定，避免上一项的「不飞」状态污染后续奖励
                bool isneedFly = true;
                Image icon = rewardList[rewardid].GetComponentInChildren<Image>();
                GameObject endposObj = CustomFlyInManager.Instance.ShopGoldObj;
                int count = int.Parse(dataitem[1]);
                int type = int.Parse(dataitem[0]);
                switch (type)
                {
                    case (int)LimitRewordType.Coins:
                        endposObj = CustomFlyInManager.Instance.ShopGoldObj;
                        break;
                    case (int)LimitRewordType.Butterfly:
                        endposObj = CustomFlyInManager.Instance.ShopButterflyObj;
                        break;
                    case (int)LimitRewordType.Tipstool: //放大镜道具，整个词语提示
                        endposObj = CustomFlyInManager.Instance.ShopTipObj;
                        break;
                    case (int)LimitRewordType.AutoComplete: //提示灯道具，单个字符提示
                        endposObj = CustomFlyInManager.Instance.ShopAutoObj;
                        break;
                    case (int)LimitRewordType.ReStartTool:
                        endposObj = CustomFlyInManager.Instance.ShopRestartObj;
                        break;
                    case (int)LimitRewordType.RemoveAds:
                    case (int)LimitRewordType.RemoveDayAds:
                    case (int)LimitRewordType.InfiniteEnergy: //无限体力、去广告没有对应资源栏，不播飞入粒子
                        isneedFly = false;
                        break;
                }

                if (isneedFly)
                {
                    pendingFlyCount++;
                    CustomFlyInManager.Instance.FlyAwardIn(icon.gameObject.transform.position, endposObj, icon.gameObject,
                        () =>
                        {
                            AudioManager.Instance.PlaySoundEffect("BtnUnlock");
                            completedFlyCount++;
                        });
                }

                rewardid++;

                yield return new WaitForSeconds(0.1f);
            }
        }

        // ★ 等所有飞入动画完成后，再刷新资源数值
        while (completedFlyCount < pendingFlyCount)
            yield return null;

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