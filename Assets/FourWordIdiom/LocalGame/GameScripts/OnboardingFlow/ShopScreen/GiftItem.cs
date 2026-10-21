using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
//using UnityEngine.Purchasing;
using UnityEngine.UI;

public class GiftItem : MonoBehaviour
{
    //private ShopDataItem shopDataItem; // 假设这是一个封装商品数据的类
    [SerializeField] private Image shopIcon;
    [SerializeField] private Text countText;
    [SerializeField] private Button tipBtnPrefab;
    [Header("加赠标识")]
    // <summary>加赠标识节点（Prefab 里加一个 Text "加赠" / 图标，挂到这里）</summary>
    [SerializeField] private GameObject bonusTag;
    
    private Button tipbtn;
    private int shopid;

    public void SetShopData(List<string> data,int itemid,Color color,string des="",string pointdes="")
    {
        string spritename = "";
        shopid = itemid;
        
        if (des != "")
        {
            countText.fontSize = 70;
        }
        
       
        List<Outline> Outlines = countText.GetComponents<Outline>().ToList();
        RectTransform rt = shopIcon.GetComponent<RectTransform>();
        
        countText.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -71f);
        
        // ---------- 礼包名 ----------
        switch (GetItemScreen.limitRewordType)
        {
            case LimitRewordType.ReStartTool:
                break;
            case LimitRewordType.Butterfly:
                break;
            case LimitRewordType.AutoComplete:
                break;
            case LimitRewordType.Tipstool:
                break;
        }

        
        switch (int.Parse(data[0]))
        {
            case (int)LimitRewordType.Coins:
                spritename = "gold2";
                countText.text = "\u00d7"+data[1]; // 假设 productContent 是数量
                break;
            case (int)LimitRewordType.Butterfly:
                spritename = "Butterfly";
                countText.text = "\u00d7"+data[1]; // 假设 productContent 是数量
                rt.sizeDelta = new Vector2(220f, 350f);
                rt.anchoredPosition = new Vector2(0f, -25f);
                break;
            case (int)LimitRewordType.Tipstool:
                spritename = "shop_reset";
                countText.text = "\u00d7"+data[1]; // 假设 productContent 是数量
                rt.sizeDelta = new Vector2(230f, 350f);
                rt.anchoredPosition = new Vector2(0f, -25f);
                countText.GetComponent<RectTransform>().anchoredPosition = new Vector2(8f, -73f);
                break;
            case (int)LimitRewordType.AutoComplete:
                spritename = "rocket";
                countText.text ="\u00d7"+data[1]; // 假设 productContent 是数量
                rt.sizeDelta = new Vector2(245f, 350f);
                rt.anchoredPosition = new Vector2(0f, -20f);
                break;
            case (int)LimitRewordType.RemoveAds:
                spritename = "shopads";
                countText.text = MultilingualManager.Instance.GetString(des);
                countText.fontSize = 60;
                CreateTipsBtn(pointdes);
                break;
            case (int)LimitRewordType.RemoveDayAds:
                spritename = "shopads";
                countText.text = "\u00d7"+data[1]; // 假设 productContent 是数量
                countText.fontSize = 60;
                CreateTipsBtn(pointdes);
                break;
            case (int)LimitRewordType.ReStartTool:
                spritename = "clock";
                countText.text = "\u00d7"+data[1]; // 假设 productContent 是数量
                countText.fontSize = 60;
                rt.anchoredPosition = new Vector2(0f, -5f);
                CreateTipsBtn(pointdes);
                break;
            case (int)LimitRewordType.InfiniteEnergy:
                spritename = "InfiniteEnergy";
                countText.text = "\u00d7"+data[1]; // 假设 productContent 是数量
                countText.fontSize = 60;
              
                CreateTipsBtn(pointdes);
                break;
        }
        
        
        // 假设您有一个方法来加载图标
        Sprite sprite = LoadShopIcon(spritename);
        if (sprite != null)
        {
            shopIcon.sprite = sprite;
            //shopIcon.SetNativeSize();
        }
       

        for (int i = 1; i < Outlines.Count; i++)
        {
            Outlines[i].effectColor = color;
        }
        
     

        // if (buyButton != null)
        // {
        //     buyButton.AddClick(()=>OnBuyButtonClicked(data));
        // }
    }

    /// <summary>设置是否为加赠物品（true=显示加赠标识；false=隐藏）</summary>
    public void SetBonus(bool isBonus)
    {
        if (bonusTag != null)
            bonusTag.SetActive(isBonus);
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