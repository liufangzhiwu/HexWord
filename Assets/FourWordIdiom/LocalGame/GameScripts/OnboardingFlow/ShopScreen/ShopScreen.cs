using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;

public class ShopScreen : UIWindow
{
    [SerializeField] private Button closeBtn; // 关闭按钮
    //[SerializeField] private Button pageBtn; // 关闭按钮
    [SerializeField] private Button adsbtn; // 关闭按钮
    [SerializeField] private Image CoinIcon;
    [SerializeField] private Image TipIcon;
    [SerializeField] private Image AutoIcon;
    [SerializeField] private Image ButteryIcon;
    [SerializeField] private Text HeaderText;
    [SerializeField] private Text GoldText;
    [SerializeField] private Text TipText;
    [SerializeField] private Text AutoText;
    [SerializeField] private Text ButteryText;
    [SerializeField] private ShopItem ShopGiftItemPrefab;
    [SerializeField] private ShopItem ShopItemPrefab;
    [SerializeField] private ShopItem ShopAdsItemPrefab;
    [SerializeField] private Transform parent;
    [SerializeField] private ScrollRect shopScrollView;
    [SerializeField] private FreeItemTable freeItemTable;
    [SerializeField] private GameObject goldItemTable;
    [SerializeField] private GameObject goldItemTable2;
    private ObjectPool objectPool; // 对象池实例
    private ObjectPool giftobjectPool; // 对象池实例
    private ObjectPool adsObjectPool; // 对象池实例
    Dictionary<int, ShopItem> shopallItems = new Dictionary<int, ShopItem>();
    Dictionary<int, ShopItem> shopDynamicItems = new Dictionary<int, ShopItem>();
    List<ShopDataItem> shopallDataItems=new List<ShopDataItem>();
   

    protected void Start()
    {
        if (ShopItemPrefab == null)
        {
            ShopItemPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "ShopItem").GetComponent<ShopItem>();
        }
        
        if (ShopGiftItemPrefab == null)
        {
            ShopGiftItemPrefab= AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "ShopGiftItem").GetComponent<ShopItem>();
        }
        
        if (ShopAdsItemPrefab == null)
        {
            ShopAdsItemPrefab= AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "ShopAdsItem").GetComponent<ShopItem>();
        }
        
        // 初始化对象池
        objectPool = new ObjectPool(ShopItemPrefab.gameObject, ObjectPool.CreatePoolContainer(transform, "ShopItemPool"));
        giftobjectPool = new ObjectPool(ShopGiftItemPrefab.gameObject, ObjectPool.CreatePoolContainer(transform, "ShopGiftItemPool"));
        adsObjectPool = new ObjectPool(ShopAdsItemPrefab.gameObject, ObjectPool.CreatePoolContainer(transform, "ShopAdsItemPool"));
        
        shopallDataItems  =  ShopManager.shopManager.GetShopItems();
        CrateShopItem(shopallDataItems);

        CustomFlyInManager.Instance.ShopAutoObj = AutoIcon.gameObject;
        CustomFlyInManager.Instance.ShopGoldObj = CoinIcon.gameObject;
        CustomFlyInManager.Instance.ShopTipObj = TipIcon.gameObject;
        CustomFlyInManager.Instance.ShopButterflyObj = ButteryIcon.gameObject;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        AudioManager.Instance.PlaySoundEffect("ShowUI");
        InitUI();
        EventDispatcher.instance.OnChangeGoldUI += InitUI;
       
        HeaderText.text = MultilingualManager.Instance.GetString("Shop");
        ShopManager.shopManager.paysuccess = false;
      
        adsbtn.gameObject.SetActive(false);

        BuyRemoveAdsEvent();
        CustomFlyInManager.Instance.GoldObj=CoinIcon.gameObject;
        
        GameDataManager.Instance.UserData.CheckShopBuyData();
        GameDataManager.Instance.UserData.isHideShopRedPoint=true;
        GameDataManager.Instance.UserData.isShowDiscountGift=false;
        
        EventDispatcher.instance.TriggerUpdateLayerCoin(false,true);
    }

    private void InitUI(int value = 0, bool isanim = false)
    {
        if (value > 0 && isanim)
        {
            StartCoroutine(AnimateCoinAddition(value));
        }
        else
        {
            GoldText.text = GameDataManager.Instance.UserData.Gold.ToString();
        }
        AutoText.text = GameDataManager.Instance.UserData.toolInfo[104].count.ToString();
        ButteryText.text = GameDataManager.Instance.UserData.toolInfo[103].count.ToString();
        TipText.text = GameDataManager.Instance.UserData.toolInfo[102].count.ToString();
    }

    private IEnumerator AnimateCoinAddition(int amount)
    {
        int startValue = GameDataManager.Instance.UserData.Gold - amount;
        int targetValue = GameDataManager.Instance.UserData.Gold;
        float duration = 0.2f; // 动画持续时间
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration); // 归一化
            int currentValue = Mathf.RoundToInt(Mathf.Lerp(startValue, targetValue, t));
            GoldText.text = currentValue.ToString();
            yield return null;
        }
        GoldText.text = targetValue.ToString(); // 确保最终值正确显示
    }
   

    private void CrateShopItem(List<ShopDataItem> shopDataItems)
    {
        // 确定目标字典
        var targetDict = shopallItems;
        
        for (int i = 0; i < shopDataItems.Count; i++)
        {
            ShopDataItem shopDataItem = shopDataItems[i];
            // 跳过不需要处理的类型
            if (shopDataItem.type != 0 && shopDataItem.type != 1 && shopDataItem.type != 2) 
                continue;

            
            // 根据类型选择对象池
            var pool = shopDataItem.type == 2 ? giftobjectPool : objectPool;
            Transform itemparent = shopDataItem.type == 2 ? parent : goldItemTable.transform;
            
            if (shopDataItem.type == 1)
            {
                pool = adsObjectPool;
                itemparent = parent;
            }
    
            // 尝试获取或创建商品项
            if (targetDict.TryGetValue(shopDataItem.id, out var shopItem))
            {
                shopItem.transform.SetSiblingIndex(i);
                shopItem.gameObject.SetActive(true);
                shopItem.UpdateUI();                   
            }
            else
            {
                if (shopDataItem.id>=5&&shopDataItem.type==0)
                {
                    itemparent =goldItemTable2.transform;
                }
                
                shopItem = pool.GetObject<ShopItem>(itemparent);
                shopItem.SetShopData(shopDataItem);
                shopItem.transform.SetSiblingIndex(i);
                // 添加到对应字典
                targetDict.TryAdd(shopDataItem.id, shopItem);
        
                // 如果是首页商品，同时添加到完整字典
                if (!shopallItems.ContainsKey(shopDataItem.id))
                {
                    shopallItems.TryAdd(shopDataItem.id, shopItem);
                }
            }
        }
        
        freeItemTable.transform.SetAsLastSibling();
        
        goldItemTable.transform.SetAsLastSibling();
        goldItemTable2.transform.SetAsLastSibling();
    }

    protected override void InitializeUIComponents()
    {
        closeBtn.AddVibraClickAction(OnCloseBtn); // 绑定关闭按钮事件
        //pageBtn.AddClickAction(ClickOnPageBtn);
        //adsbtn.AddClick(OnAdsBtn);
    }


    private void OnCloseBtn()
    {
        //刷新道具
        EventDispatcher.instance.TriggerChangeGoldUI(0, false);
        base.Close(); // 隐藏面板
        
        if (GameCoreManager.Instance.PanelState == PanelState.MainMenuPanel)
        {
            SystemManager.Instance.ShowPanel(PanelType.PrimaryInterface);
        }else if (GameCoreManager.Instance.PanelState == PanelState.FinishHexPanel)
        {
            SystemManager.Instance.ShowPanel(PanelType.StageFinishView);
        }

        SystemManager.Instance.ShowPanel(PanelType.HeaderSection);

        //UIManager.Instance.ShowPanel(PanelName.TopContainer);
    }
    
    private void OnAdsBtn()
    {
        //ShopManager.shopManager.iapManager.UserInitiatedRestore(true);
    }

    
    private async void BuyRemoveAdsEvent()
    {
        await Task.Delay(100); // 等待1秒

        // foreach (ShopLimitData shopLimitData in GameDataManager.Instance.UserData.limitShopItems)
        // {
        //     if (shopLimitData.isget && !shopLimitData.isoverdate)
        //     {
        //         if(shopLimitData.adstype == (int)LimitRewordType.Remove7DayAds)
        //             ShopManager.shopManager.UpdateAdsBtnUIEvent(shopLimitData.gettime,false);
        //         if (shopLimitData.adstype == (int)LimitRewordType.RemoveAds)
        //         {
        //             ShopManager.shopManager.UpdateAdsBtnUIEvent("", false);
        //             return;
        //         }
        //             
        //     }
        // }
    }
    
    private void ShowBanner()
    {
        if (GameDataManager.Instance?.UserData.CurrentHexStage >= 7)
        {
            if (SystemManager.Instance.PanelIsShowing(PanelType.HexGamePlayArea))
            {
                // if(StageController.Instance.CurStageInfo.Puzzles.Count <= 9)
                //     AdsManager.Instance.ShowBannerAd();
            }
            //else
            //{
            //    AdsManager.Instance.ShowBannerAd();
            //}
        }   
    }
    
    public void OnPanelClosed()
    {
        foreach (var item in shopallItems)
        {
            // 根据是否在homeItemKeys中来设置active状态
            item.Value.gameObject.SetActive(false);
        }
    }

    protected override void OnDisable()
    {
        EventDispatcher.instance.OnChangeGoldUI -= InitUI;
    
        ShowBanner();

        if (SystemManager.Instance.PanelIsShowing(PanelType.ChessPlayArea) ||
            SystemManager.Instance.PanelIsShowing(PanelType.HexGamePlayArea))
        {
            EventDispatcher.instance.TriggerUpdateLayerCoin(false,true,false);
        }
        
        // if(!ShopManager.shopManager.paysuccess) 
        //     AdsManager.Instance.ShowRewardedPanel("store_gold");
        OnPanelClosed();
        //CustomFlyInManager.Instance.GoldObj=null;
    }
}
