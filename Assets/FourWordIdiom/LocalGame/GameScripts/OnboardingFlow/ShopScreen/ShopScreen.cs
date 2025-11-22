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
    [SerializeField] private Button pageBtn; // 关闭按钮
    [SerializeField] private Button adsbtn; // 关闭按钮
    [SerializeField] private Text HeaderText;
    [SerializeField] private Text GoldText;
    [SerializeField] private Text ButteryText;
    [SerializeField] private ShopItem ShopGiftItemPrefab;
    [SerializeField] private ShopItem ShopItemPrefab;
    [SerializeField] private Transform parent;
    [SerializeField] private ScrollRect shopScrollView;
    private ObjectPool objectPool; // 对象池实例
    private ObjectPool giftobjectPool; // 对象池实例
    Dictionary<int, ShopItem> shophomeItems = new Dictionary<int, ShopItem>();
    Dictionary<int, ShopItem> shopallItems = new Dictionary<int, ShopItem>();
    private List<ShopDataItem> shopDataItems=new List<ShopDataItem>();
    List<ShopDataItem> shopallDataItems=new List<ShopDataItem>();
    HashSet<int> homeItemKeys=new HashSet<int>();
   

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
        // 初始化对象池
        objectPool = new ObjectPool(ShopItemPrefab.gameObject, ObjectPool.CreatePoolContainer(transform, "ShopItemPool"));
        giftobjectPool = new ObjectPool(ShopGiftItemPrefab.gameObject, ObjectPool.CreatePoolContainer(transform, "ShopGiftItemPool"));
        
        shopallDataItems  =  ShopManager.shopManager.GetShopItems();
        shopDataItems=  ShopManager.shopManager.GetShopHomeItems();
        CrateShopItem(shopDataItems,true);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        AudioManager.Instance.PlaySoundEffect("ShowUI");
        InitUI();
        EventDispatcher.instance.OnChangeGoldUI += InitUI;
        ShopManager.shopManager.UpdateAdsBtnUI += InitAdsBtnUI;
        HeaderText.text = MultilingualManager.Instance.GetString("Shop");
        //AdsManager.Instance.HideBannerAd();
        ShopManager.shopManager.paysuccess = false;
        shopScrollView.enabled=false;
        pageBtn.gameObject.SetActive(true);
        adsbtn.gameObject.SetActive(false);
        
        if (shopDataItems.Count > 0)
        {
            shopDataItems = ShopManager.shopManager.GetShopHomeItems();
            CrateShopItem(shopDataItems,true);
            // 重置滚动位置到顶部
            shopScrollView.normalizedPosition = new Vector2(0, 1);
        }

        BuyRemoveAdsEvent();

        //GameDataManager.Instance.UserData.CheckShopBuyData();
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
        ButteryText.text = GameDataManager.Instance.UserData.toolInfo[103].count.ToString();
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

    private void InitAdsBtnUI(string gettime,bool updateui=false)
    {
        if (string.IsNullOrEmpty(gettime))
        {
            if (pageBtn.gameObject.activeSelf)
            {
                if (updateui)
                {
                    foreach (var item in shopallItems)
                    {
                        if (item.Key == 8 || item.Key == 13 || item.Key == 14)
                            // 根据是否在homeItemKeys中来设置active状态
                            item.Value.gameObject.SetActive(false);
                    }
                    shopDataItems = ShopManager.shopManager.GetShopHomeItems();
                    CrateShopItem(shopDataItems, true);
                }
               
               
            }
            else
            {
                if (updateui)
                {
                    foreach (var item in shophomeItems)
                    {
                        if (item.Key == 8 || item.Key == 13 || item.Key == 14)
                            // 根据是否在homeItemKeys中来设置active状态
                            item.Value.gameObject.SetActive(false);
                    }
                    shopDataItems = ShopManager.shopManager.GetShopItems();
                    CrateShopItem(shopDataItems, false);
                }                   
            }
            string redesday = MultilingualManager.Instance.GetString("RemoveADSign02");
            adsbtn.GetComponentInChildren<Text>().text = redesday;                
            adsbtn.gameObject.SetActive(true);
            return;
        }           
        
        int hour = 24*7;
        DateTime buyendTime = DateTime.Parse(gettime).AddHours(hour);
        TimeSpan timeSpan = buyendTime.Subtract(DateTime.Now);
        int day = (int)timeSpan.TotalDays + 1;
        string desday = MultilingualManager.Instance.GetString("RemoveADSign01");
        adsbtn.GetComponentInChildren<Text>().text = string.Format(desday,day);
        adsbtn.gameObject.SetActive(true);           
    }

    private void CrateShopItem(List<ShopDataItem> shopDataItems, bool isHome)
    {
        // 确定目标字典
        var targetDict = isHome ? shophomeItems : shopallItems;
        
        for (int i = 0; i < shopDataItems.Count; i++)
        {
            ShopDataItem shopDataItem = shopDataItems[i];
            // 跳过不需要处理的类型
            if (shopDataItem.type != 0 && shopDataItem.type != 1 && shopDataItem.type != 2) 
                continue;
            
            // 根据类型选择对象池
            var pool = shopDataItem.type == 2 ? giftobjectPool : objectPool;
    
            // 尝试获取或创建商品项
            if (targetDict.TryGetValue(shopDataItem.id, out var shopItem))
            {
                shopItem.transform.SetSiblingIndex(i);
                shopItem.gameObject.SetActive(true);
                shopItem.UpdateUI();                   
            }
            else
            {
                shopItem = pool.GetObject<ShopItem>(parent);
                shopItem.SetShopData(shopDataItem);
                shopItem.transform.SetSiblingIndex(i);
                // 添加到对应字典
                targetDict.TryAdd(shopDataItem.id, shopItem);
        
                // 如果是首页商品，同时添加到完整字典
                if (isHome && !shopallItems.ContainsKey(shopDataItem.id))
                {
                    shopallItems.TryAdd(shopDataItem.id, shopItem);
                }
            }
        }

        if (isHome && homeItemKeys.Count <= 0)
        {
            // 创建shophomeItems的键集合用于快速查找
            homeItemKeys = new HashSet<int>(shophomeItems.Keys);
        }

        if (UIUtilities.GetScreenRatio()<=0.95f)
        {
            pageBtn.transform.SetParent(parent);
        }
        
    }

    protected override void InitializeUIComponents()
    {
        closeBtn.AddClickAction(OnCloseBtn); // 绑定关闭按钮事件
        pageBtn.AddClickAction(ClickOnPageBtn);
        //adsbtn.AddClick(OnAdsBtn);
    }

    private void ClickOnPageBtn()
    {
        pageBtn.gameObject.SetActive(false);
        shopScrollView.enabled=true;
        shopallDataItems  =  ShopManager.shopManager.GetShopItems();
        CrateShopItem(shopallDataItems,false);
    }

    private void OnCloseBtn()
    {
        base.Close(); // 隐藏面板
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
        if (GameDataManager.Instance.UserData.CurrentHexStage >= 7)
        {
            if (SystemManager.Instance.PanelIsShowing(PanelType.GamePlayArea))
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
        ShopManager.shopManager.UpdateAdsBtnUI -= InitAdsBtnUI;
        ShowBanner();
        // if(!ShopManager.shopManager.paysuccess) 
        //     AdsManager.Instance.ShowRewardedPanel("store_gold");
        OnPanelClosed();
    }
}
