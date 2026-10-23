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

    [Header("是否为 giftHengItem（横向布局）")]
    [Tooltip("勾选后不修改 countText 的 fontSize / anchoredPosition；图标使用下方 hengIconSize / hengIconAnchoredPosition 独立布局")]
    [SerializeField] private bool isGiftHengItem;

    [Header("giftHengItem 专用图标布局（仅 isGiftHengItem = true 时生效）")]
    [Tooltip("giftHengItem 下 shopIcon 的 sizeDelta")]
    private Vector2 hengIconSize = new Vector2(120f, 130f);
    [Tooltip("giftHengItem 下 shopIcon 的 anchoredPosition")]
    private Vector2 hengIconAnchoredPosition = Vector2.zero;

    private Button tipbtn;
    private int shopid;

    public void SetShopData(List<string> data, int itemid, Color color, string des = "", string pointdes = "")
    {
        string spritename = "";
        shopid = itemid;

        if (des != "")
        {
            SetCountFontSize(70);
        }

        List<Outline> Outlines = countText.GetComponents<Outline>().ToList();

        SetCountAnchoredPosition(new Vector2(0f, -71f));

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
                countText.text = data[1];
                SetIconSize(new Vector2(280f, 350f));
                SetIconAnchoredPosition(new Vector2(0f, -10f));
                break;
            case (int)LimitRewordType.Butterfly:
                spritename = "Butterfly";
                countText.text = "\u00d7" + data[1];
                hengIconSize = new Vector2(110f, 100f);
                SetIconSize(new Vector2(220f, 350f));
                SetIconAnchoredPosition(new Vector2(0f, -25f));
                break;
            case (int)LimitRewordType.Tipstool:
                spritename = "shop_reset";
                countText.text = "\u00d7" + data[1];
                SetIconSize(new Vector2(230f, 350f));
                SetIconAnchoredPosition(new Vector2(0f, -25f));
                SetCountAnchoredPosition(new Vector2(8f, -73f));
                break;
            case (int)LimitRewordType.AutoComplete:
                spritename = "rocket";
                countText.text = "\u00d7" + data[1];
                SetIconSize(new Vector2(245f, 350f));
                SetIconAnchoredPosition(new Vector2(0f, -20f));
                break;
            case (int)LimitRewordType.RemoveAds:
                spritename = "shopads";
                hengIconSize = new Vector2(110f, 100f);
                SetIconSize(new Vector2(250f, 250f));
                SetIconAnchoredPosition(new Vector2(0f, -10f));
                countText.text = "永久";
                SetCountFontSize(60);
                CreateTipsBtn(pointdes);
                break;
            case (int)LimitRewordType.RemoveDayAds:
                spritename = "shopads";
                countText.text = data[1]+"天";
                SetIconSize(new Vector2(230f, 350f));
                SetIconSize(Vector2.zero);
                hengIconAnchoredPosition = new Vector2(-14f, -14f);
                SetIconAnchoredPosition(new Vector2(0f, -25f));
                SetCountFontSize(60);
                CreateTipsBtn(pointdes);
                break;
            case (int)LimitRewordType.ReStartTool:
                spritename = "clock";
                countText.text = "\u00d7" + data[1];
                SetCountFontSize(60);
                hengIconSize = new Vector2(110f, 100f);
                SetIconSize(Vector2.zero);
                SetIconAnchoredPosition(new Vector2(0f, -5f));
                CreateTipsBtn(pointdes);
                break;
            case (int)LimitRewordType.InfiniteEnergy:
                spritename = "InfiniteEnergy";
                countText.text = data[1]+"时";
                hengIconSize = new Vector2(108f, 100f);
                hengIconAnchoredPosition = new Vector2(-14f, -14f);
                SetIconSize(Vector2.zero);
                SetIconAnchoredPosition(new Vector2(0f, -5f));
                SetCountFontSize(60);
                CreateTipsBtn(pointdes);
                break;
        }

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
    }

    public void SetBonus(bool isBonus)
    {
        if (bonusTag != null)
            bonusTag.SetActive(isBonus);
    }

    // ======================= countText：giftHengItem 时不修改 =======================

    private void SetCountFontSize(int size)
    {
        if (isGiftHengItem) return;
        if (countText == null) return;
        countText.fontSize = size;
    }

    private void SetCountAnchoredPosition(Vector2 pos)
    {
        if (isGiftHengItem) return;
        if (countText == null) return;
        countText.GetComponent<RectTransform>().anchoredPosition = pos;
    }

    // ======================= shopIcon：giftHengItem 用专用布局 =======================

    /// <summary>
    /// 设置图标节点 sizeDelta。
    /// isGiftHengItem = true → 使用 hengIconSize（单独配置）；
    /// isGiftHengItem = false → 使用调用处传入的普通 size。
    /// </summary>
    private void SetIconSize(Vector2 size)
    {
        if (shopIcon == null) return;
        if (!isGiftHengItem&&size==Vector2.zero) return;
        
        shopIcon.rectTransform.sizeDelta = isGiftHengItem ? hengIconSize : size;
    }

    /// <summary>
    /// 设置图标节点 anchoredPosition。
    /// isGiftHengItem = true → 使用 hengIconAnchoredPosition（单独配置）；
    /// isGiftHengItem = false → 使用调用处传入的普通 pos。
    /// </summary>
    private void SetIconAnchoredPosition(Vector2 pos)
    {
        if (shopIcon == null) return;
        shopIcon.rectTransform.anchoredPosition = isGiftHengItem ? hengIconAnchoredPosition : pos;
    }

    private void CreateTipsBtn(string tips)
    {
        if (string.IsNullOrEmpty(tips)) return;

        SetCountFontSize(45);
        if (tipBtnPrefab == null)
            tipBtnPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "Shop_tipbtn").GetComponent<Button>();
        tipbtn = Instantiate(tipBtnPrefab, countText.transform).GetComponent<Button>();
        GameObject tippanel = tipbtn.transform.GetChild(0).gameObject;
        tippanel.gameObject.SetActive(false);
        tippanel.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString(tips);
        tipbtn.onClick.AddListener(() => ClickShopItemtipBtn(tippanel));
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