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

    private void Start()
    {
        tipBtn.AddClickAction(ClicktipBtn);
        
        UnityTimer.Loop(3f, TickTime);
        
        ShopDataItem freeDataItem=  ShopManager.shopManager.FormAllItemsGetProduct("FreeGoods");
        ShopDataItem goldDataItem=  ShopManager.shopManager.FormAllItemsGetProduct("GoldGoods");
        ShopDataItem toolDataItem=  ShopManager.shopManager.GetProduct("SingleGoods");

        SetShowIcon(freeDataItem);
        SetShowIcon(goldDataItem);
        SetShowIcon(toolDataItem);
        
        freeitem.SetShopData(freeDataItem);
        golditem.SetShopData(goldDataItem);
        toolitem.SetShopData(toolDataItem);
    }

    private void OnEnable()
    {
        InitUI();

        EventDispatcher.instance.OnChangeFreeTipsPanel += ChangeTipsPanelState;
    }

    private void SetShowIcon(ShopDataItem shopDataItem)
    {
        List<string> giftdata=shopDataItem.productContent[0];

        string spritename="";
        
        switch (int.Parse(giftdata[0]))
        {
            case (int)LimitRewordType.Coins:
                spritename = "gold1";
                break;
            case (int)LimitRewordType.Butterfly:
                spritename = "Butterfly";
                break;
            case (int)LimitRewordType.Tipstool:
                spritename = "tipicon";
                break;
            case (int)LimitRewordType.AutoComplete:
                spritename = "rocket";
                break;
            case (int)LimitRewordType.RemoveAds:
                spritename = "shopads";
                break;
            case (int)LimitRewordType.Remove7DayAds:
                spritename = "shopads";
                break;
        }
        shopDataItem.showIcon = spritename;
    }


    private void InitUI()
    {
        TickTime();
        
        freeitem.btntagicon.gameObject.SetActive(GameDataManager.Instance.UserData.isDayFreeGet);
        golditem.btntagicon.gameObject.SetActive(!GameDataManager.Instance.UserData.isDayGoldBuy);

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
        tipPanel.SetActive(!tipPanel.activeSelf);
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
    }
}