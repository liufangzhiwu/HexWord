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

    /// <summary>
    /// 本次 SetShopData 是否需要改图标 / 文字的表现（尺寸、位置、字号、描边色）。
    /// AdsDiscountScreen 的 DiscountItem 预制体已经排好版（icon 与 countText 的大小、位置、描边都在 Prefab 里定死），
    /// 传 false 就只填内容（图标 sprite、数量文字），不再覆盖任何表现参数。
    /// </summary>
    private bool _applyStyle = true;

    /// <summary>是否为横向布局 Prefab（GiftHengItem）。横向项的图标位置由 hengIconAnchoredPosition 决定，不做外部覆盖。</summary>
    public bool IsHengItem => isGiftHengItem;

    /// <summary>
    /// 直接覆盖图标节点的 anchoredPosition.y 和尺寸（宽 / 高），x 位置保持不变。
    /// 用于 itemEnergyList 内只有 1 个子对象时的排版微调：
    /// 在 SetShopData 设置完之后调用，作为最终覆盖。
    /// </summary>
    public void SetIconAnchoredYAndSize(float y, float width, float height)
    {
        if (shopIcon == null) return;

        RectTransform rt = shopIcon.rectTransform;

        Vector2 pos = rt.anchoredPosition;
        pos.y = y;
        rt.anchoredPosition = pos;

        rt.sizeDelta = new Vector2(width, height);
    }

    
     public void SetBuyListData(List<string> data, int itemid, Color color, string des = "", string pointdes = "")
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
                spritename = "gold";
                countText.text = "\u00d7" + data[1];
                hengIconSize = new Vector2(100f, 100f);
                SetIconSize(new Vector2(180f, 250f));
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
                hengIconSize = new Vector2(98f, 100f);
                hengIconAnchoredPosition = new Vector2(8f, -14f);
                SetIconSize(Vector2.zero);
                SetIconAnchoredPosition(new Vector2(0f, 50f));
                SetCountFontSize(60);
                CreateTipsBtn(pointdes);

                
                SetCountAnchoredPosition(new Vector2(0f, -89f));
               
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
    
    /// <param name="applyStyle">
    /// true（默认）：按道具类型覆盖图标 / 数量的尺寸、位置、字号与描边色；
    /// false：只填图标 sprite 与数量文字，其它表现全部交给 Prefab（AdsDiscountScreen 用）。
    /// </param>
    public void SetShopData(List<string> data, int itemid, Color color, string des = "", string pointdes = "",
        bool applyStyle = true)
    {
        string spritename = "";
        shopid = itemid;
        _applyStyle = applyStyle;

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
                SetIconAnchoredPosition(new Vector2(0f, 50f));
                SetCountFontSize(60);
                CreateTipsBtn(pointdes);

                
                SetCountAnchoredPosition(new Vector2(0f, -89f));
               
                break;
        }

        Sprite sprite = LoadShopIcon(spritename);
        if (sprite != null)
        {
            shopIcon.sprite = sprite;
            //shopIcon.SetNativeSize();
        }

        // 描边色也归「表现」：AdsDiscountScreen 的 DiscountItem 在 Prefab 里定好描边，脚本不再改
        if (_applyStyle)
        {
            for (int i = 1; i < Outlines.Count; i++)
            {
                Outlines[i].effectColor = color;
            }
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
        if (!_applyStyle) return;
        if (isGiftHengItem) return;
        if (countText == null) return;
        countText.fontSize = size;
    }

    private void SetCountAnchoredPosition(Vector2 pos)
    {
        if (!_applyStyle) return;
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
        if (!_applyStyle) return;
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
        if (!_applyStyle) return;
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