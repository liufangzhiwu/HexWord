using System;
using System.Collections.Generic;
using System.Globalization;
using Middleware;
using UnityEngine;
//using UnityEngine.Purchasing;
using UnityEngine.UI;

public class FreeItemTable : MonoBehaviour
{
    [SerializeField] private GameObject items;
    [SerializeField] private ShopItem freeitem;
    [SerializeField] private ShopItem golditem;
    [SerializeField] private ShopItem toolitem;
    [SerializeField] private Text timetText;
    [SerializeField] private Button tipBtn;
    [SerializeField] private GameObject tipPanel;

    private ShopDataItem freeDataItem;
    private ShopDataItem goldDataItem;
    private ShopDataItem toolDataItem;

    /// <summary>
    /// 审核态：免费/视频金币这一格（freeitem）已被 Gold6 付费金币商品替代。
    /// 对应需求：审核开（AuditStatusSwitch=1）时，分享链路里的 Gold6 商品展示在商店中，
    /// 顶掉「金币视频点」（FreeGoods）的位置，商店不再出现广告领取链路。
    /// </summary>
    private bool _auditGold6Replace;

    private void Start()
    {
        tipBtn.AddClickAction(()=>ClicktipBtn(),"Button",false);
        
        UnityTimer.Loop(3f, TickTime);

        ResolveItems();
    }

    private void OnEnable()
    {
        InitUI();

        EventDispatcher.instance.OnChangeFreeTipsPanel += ChangeTipsPanelState;

        if (ShopManager.shopManager != null)
            ShopManager.shopManager.OnAuditConfigLoaded += OnAuditConfigLoaded;

        // 审核配置可能是异步回来的，这里每次显示都重新按配置取一次商品数据
        ResolveItems();
    }

    /// <summary>审核配置就绪（异步）→ 重新判一次「金币视频点」是否要换成 Gold6。</summary>
    private void OnAuditConfigLoaded()
    {
        ResolveItems();
    }

    /// <summary>
    /// 解析并绑定三张固定卡的商品数据。
    /// 审核态下 freeitem 由 FreeGoods（免费 / 看视频领金币）换成 Gold6（600 金币，付费）。
    /// </summary>
    private void ResolveItems()
    {
        ShopManager sm = ShopManager.shopManager;
        if (sm == null) return;

        _auditGold6Replace = sm.ShouldReplaceCoinVideoSlotWithGold6();
        ShopDataItem gold6Item = _auditGold6Replace ? sm.GetAuditGold6Item() : null;

        freeDataItem = gold6Item != null ? gold6Item : sm.FormAllItemsGetProduct("FreeGoods");
        goldDataItem = sm.FormAllItemsGetProduct("GoldGoods");
        toolDataItem = sm.GetProduct("SingleGoods");

        SetShowIcon(freeDataItem);
        SetShowIcon(goldDataItem);
        SetShowIcon(toolDataItem);

        if (freeitem != null) freeitem.SetShopData(freeDataItem);
        if (golditem != null) golditem.SetShopData(goldDataItem);
        if (toolitem != null) toolitem.SetShopData(toolDataItem);

        ApplyFreeItemMode();

        if (_auditGold6Replace)
            Debug.Log($"[Shop][AuditStatus] 商店「金币视频点」已替换为 Gold6 付费金币商品: {freeDataItem?.produceNameId}");
    }

    /// <summary>把「视频金币卡 / 付费金币卡」两种展示态落到 freeitem 上。</summary>
    private void ApplyFreeItemMode()
    {
        if (freeitem == null) return;

        bool replaced = _auditGold6Replace
                        && freeDataItem != null
                        && freeDataItem.produceNameId == ShopManager.ProductGold6;

        // 视频角标 / 剩余次数 / 高亮由 ShopItem 统一收口
        freeitem.SetCoinPurchaseMode(replaced);

        if (replaced && freeitem.btnTagicon != null)
            freeitem.btnTagicon.gameObject.SetActive(false);
    }

    private void SetShowIcon(ShopDataItem shopDataItem)
    {
        if (shopDataItem == null || shopDataItem.productContent == null
            || shopDataItem.productContent.Count == 0 || shopDataItem.productContent[0] == null
            || shopDataItem.productContent[0].Count == 0)
            return;

        List<string> giftdata = shopDataItem.productContent[0];

        string spritename = "";

        if (!int.TryParse(giftdata[0], out int contentType)) return;

        switch (contentType)
        {
            case (int)LimitRewordType.Coins:
                spritename = "gold1";
                break;
            case (int)LimitRewordType.Butterfly:
                spritename = "Butterfly";
                break;
            case (int)LimitRewordType.Tipstool:
                spritename = "shop_reset";
                break;
            case (int)LimitRewordType.AutoComplete:
                spritename = "rocket";
                break;
            case (int)LimitRewordType.RemoveAds:
                spritename = "shopads";
                break;
            case (int)LimitRewordType.RemoveDayAds:
                spritename = "shopads";
                break;
        }
        shopDataItem.showIcon = spritename;
    }


    private void InitUI()
    {
        TickTime();

        // 审核态这一格已换成付费金币卡：不挂视频角标
        if (!_auditGold6Replace && freeitem != null && freeitem.btnTagicon != null)
            freeitem.btnTagicon.gameObject.SetActive(GameDataManager.Instance.UserData.isDayFreeGet);

        if (golditem != null && golditem.btnTagicon != null)
            golditem.btnTagicon.gameObject.SetActive(!GameDataManager.Instance.UserData.isDayGoldBuy);

        // if (GameDataManager.Instance.UserData.isDayFreeGet)
        // {
        //     Sprite sprite=LoadShopIcon("freemaxitembg");
        //
        //     freeitem.GetComponent<Image>().sprite = sprite;
        //     golditem.GetComponent<Image>().sprite = sprite;
        //     toolitem.GetComponent<Image>().sprite = sprite;
        // }
        // else
        // {
        //     Sprite sprite=LoadShopIcon("freeitembg");
        //     freeitem.GetComponent<Image>().sprite = sprite;
        //     golditem.GetComponent<Image>().sprite = sprite;
        //     toolitem.GetComponent<Image>().sprite = sprite;
        // }
    }
    
    private void TickTime()
    {
        // 假设 logoutTime 是用户的登出时间
        DateTime logoutTime = DateTime.Now; // 将字符串转换为 DateTime
        DateTime midnight = logoutTime.Date.AddDays(1); // 获取当天的 00:00

        // 计算剩余时间
        TimeSpan timeRemaining = midnight - logoutTime;
        if (timeRemaining.TotalMinutes > 0)
        {
            if (timeRemaining.Hours == 24)
            {
                GameDataManager.Instance.UserData.CheckResetDailyTime();
            }
            string time = UIUtilities.FormatTimeRemaining(timeRemaining);
            timetText.text = time;
        }
       
        // 假设您有一个方法来加载图标
        //shopIcon.sprite = LoadShopIcon(spritename);
    }
    

    private Sprite LoadShopIcon(string showIcon)
    {
        return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(showIcon);
    }

    private void ClicktipBtn()
    {
        bool isshow=!tipPanel.activeSelf;
        tipPanel.SetActive(isshow);
    }
    
    private void ChangeTipsPanelState()
    {
        if (tipPanel.activeSelf)
        {
            tipPanel.SetActive(false);
        }
    }

    private void OnDisable()
    {
        EventDispatcher.instance.OnChangeFreeTipsPanel -= ChangeTipsPanelState;

        if (ShopManager.shopManager != null)
            ShopManager.shopManager.OnAuditConfigLoaded -= OnAuditConfigLoaded;
    }
}