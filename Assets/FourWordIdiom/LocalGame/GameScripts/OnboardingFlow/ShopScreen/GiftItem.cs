using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
//using UnityEngine.Purchasing;
using UnityEngine.UI;

public class GiftItem : MonoBehaviour
{
    //private ShopDataItem shopDataItem; // 假设这是一个封装商品数据的类
    [SerializeField] private Image shopIcon;
    [SerializeField] private Text countText;
    [SerializeField] private Button tipBtnPrefab;
    private Button tipbtn;
    private int shopid;

    public void SetShopData(List<string> data,int itemid,string des="",string pointdes="")
    {
        string spritename = "";
        shopid = itemid;
        
        if (des != "")
        {
            countText.fontSize = 70;
        }
        
        switch (int.Parse(data[0]))
        {
            case (int)LimitRewordType.Coins:
                spritename = "gold2";
                countText.text = "x "+data[1]; // 假设 productContent 是数量
                break;
            case (int)LimitRewordType.Butterfly:
                spritename = "Butterfly";
                countText.text = "x "+data[1]; // 假设 productContent 是数量
                break;
            case (int)LimitRewordType.Tipstool:
                spritename = "tipicon";
                countText.text = "x "+data[1]; // 假设 productContent 是数量
                break;
            case (int)LimitRewordType.Resettool:
                spritename = "shop_reset";
                countText.text = "x "+data[1]; // 假设 productContent 是数量
                break;
            case (int)LimitRewordType.RemoveAds:
                spritename = "shopads";
                countText.text = MultilingualManager.Instance.GetString(des);
                countText.fontSize = 60;
                CreateTipsBtn(pointdes);
                break;
            case (int)LimitRewordType.Remove7DayAds:
                spritename = "shopads";
                countText.text = MultilingualManager.Instance.GetString(des);
                countText.fontSize = 60;
                CreateTipsBtn(pointdes);
                break;
        }
        
        
        // 假设您有一个方法来加载图标
        shopIcon.sprite = LoadShopIcon(spritename);
        //shopIcon.SetNativeSize();
       
       
       
        // if (buyButton != null)
        // {
        //     buyButton.AddClick(()=>OnBuyButtonClicked(data));
        // }
    }

    private void CreateTipsBtn(string tips)
    {
        if(string.IsNullOrEmpty(tips)) return;
        countText.fontSize = 45;
        if(tipBtnPrefab==null)
            tipBtnPrefab=AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "Shop_tipbtn").GetComponent<Button>();
        // 从对象池获取 ShopItem 对象
        tipbtn = Instantiate(tipBtnPrefab, countText.transform).GetComponent<Button>();
        GameObject tippanel = tipbtn.transform.GetChild(0).gameObject;
        tippanel.gameObject.SetActive(false);
        tippanel.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString(tips);
        tipbtn.onClick.AddListener(()=> ClickShopItemtipBtn(tippanel));
    }

    private Sprite LoadShopIcon(string showIcon)
    {
        return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(showIcon);
    }

    private void ClickShopItemtipBtn(GameObject tippanel)
    {
        if (!ShopManager.shopManager.shopItemsTipsPanel.ContainsKey(shopid))
        {
            ShopManager.shopManager.shopItemsTipsPanel.Add(shopid, tippanel);
        }

        if (ShopManager.shopManager.shopItemsTipsPanel.Count > 0)
        {
            foreach (var item in ShopManager.shopManager.shopItemsTipsPanel)
            {
                if (item.Key != shopid)
                {
                    item.Value.gameObject.SetActive(false);
                }
            }
        }

        tippanel.SetActive(!tippanel.activeSelf);
    }

    private void OnDisable()
    {
        if (tipbtn != null)
        {
            var tippanel = tipbtn.transform.GetChild(0)?.gameObject;
            if (tippanel != null)
            {
                tippanel.SetActive(false);
            }
        }
    }
}