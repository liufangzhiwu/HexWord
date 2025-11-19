using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;
//using UnityEngine.Purchasing;

public class ShopDataItem
{
    public int id;
    public string produceNameId;
    public string produceNameId_tw;
    public int purchaseType;
    public int type;
    public List<List<string>> productContent;
    public float price;
    public string showIcon;
    public string name;
    public string des;
    public string pointDes;
    public string isHomeDisplay;
    public int sort;
    public string homeSort;
    public List<string> unlocked;
    public string limitedTime;
    public string discount;

    /// <summary>
    /// 获取商品购买id
    /// </summary>
    /// <returns></returns>
    public string GetProduceName()
    {
        if(GameDataManager.instance.UserData.LanguageCode=="JS")
            return produceNameId.ToLower();
        if(GameDataManager.instance.UserData.LanguageCode=="CT")
            return produceNameId_tw;
        return produceNameId;
    }
}


public class ShopManager : MonoBehaviour
{
    private List<ShopDataItem> shopItems;
    /// <summary>
    /// 当前限时商店物品
    /// </summary>
    public ShopDataItem curshopAdsItem;
    public static ShopManager shopManager;
    //private List<ShopLimitData> limitDatas;
    
    //private Dictionary<int, ShopLimitData> shoplimitDatas;
    private List<ShopDataItem> _limitAdsGifts;

    public Action<string,bool> UpdateAdsBtnUI;

    public Dictionary<int,GameObject> shopItemsTipsPanel=new Dictionary<int, GameObject>();

    //public IAPManager iapManager; // 引用 IAPManager
    [HideInInspector] public bool paysuccess; //支付成功

    private void Awake()
    {
        if (shopManager == null)
        {
            shopManager = this;
            DontDestroyOnLoad(gameObject); // 保持广告管理器在场景切换时不销毁
        }
        else
        {
            Destroy(gameObject);
        }    
    }

    void Start()
    {
        TextAsset data = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "shop");
        if (data != null)
        {
            ParseShopItems(data.text);
        }
        else
        {
            Debug.LogError("Failed to load CSV data.");
        }

        paysuccess = false;

        Initialize();
    }

    public void Initialize()
    {
        // shoplimitDatas = GameDataManager.instance.UserData.limitShopItems
        //     .ToDictionary(x => x.id, x => x);
        // 初始化查找结构
        _limitAdsGifts = GetLimitAdsGifts();
    }
    
    
    // 在 Inspector 中将按钮的点击事件绑定到此方法
    //public void OnBuyGoldTestButtonClicked(string _productId, Action<Product> _successedCallback, Action<string> _failedCallback)
    //{
    //    if (iapManager != null)
    //    {
    //        // 现在shopItems列表中包含所有商品
    //        Debug.Log("进入购买逻辑: " + _productId);
    //        iapManager.Purchase(_productId,_successedCallback,_failedCallback); // 调用 IAPManager 的购买方法
    //    }
    //}

    //// 获取商品
    //public Product GetProduct(string _productId)
    //{
    //    if (iapManager != null)
    //    {
    //        // 现在shopItems列表中包含所有商品
    //        Debug.Log("获取购买商品: " + _productId);
    //        return iapManager.GetProduct(_productId); // 调用 IAPManager 的购买方法
    //    }

    //    return null;
    //}

    public void UpdateAdsBtnUIEvent(string gettime,bool updateui)
    {
        UpdateAdsBtnUI?.Invoke(gettime,updateui);
    }

    void ParseShopItems(string data)
    {
        // 将 CSV 数据转换为 JSON 格式
        ConvertCSVToJSON(data);

        // 现在shopItems列表中包含所有商品
        Debug.Log("Shop items loaded: " + shopItems.Count);
    }

    void ConvertCSVToJSON(string data)
    {
        // 用于构建 JSON 字符串
        List<ShopDataItem> items = new List<ShopDataItem>();
        string[] lines = data.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        for (int i = 2; i < lines.Length; i++) // 从第一行开始，跳过标题行
        {
            string[] fields = lines[i].Split(',');

            if (fields.Length >= 11) // 确保有足够的字段
            {
                int id = int.Parse(fields[0].Trim());
                string nameID = fields[1].Trim();
                string nameID_tw = fields[2].Trim();
                int purchaseType = int.Parse(fields[3].Trim());
              
                int type = int.Parse(fields[5].Trim());
                
                // Parse productContent
                List<List<string>> productContent = fields[6].Split('#')
                    .Select(group => group.Split(';').ToList())
                    .ToList();

                float price = float.Parse(fields[7].Trim().Trim('"')); // 去掉引号
                string showIcon = fields[8].Trim();
                string name = fields[9].Trim();
                string des = fields[10].Trim();
                string pointdes = fields[11].Trim();
                string isHomeDisplay = fields[12].Trim();
                int sort = int.Parse(fields[13].Trim());
                string homeSort = fields[14].Trim();
                List<string> unlocks = new List<string>(
                    fields[15].Trim().Split('#') // 假设用分号分隔
                );
                //int unlocks = int.Parse(fields[15].Trim());
                string limittime = fields[16].Trim();
                string discount = fields[17].Trim();

                ShopDataItem item = new ShopDataItem
                {
                    id = id,
                    produceNameId = nameID,
                    produceNameId_tw = nameID_tw,
                    purchaseType = purchaseType,
                    type = type,
                    productContent = productContent,
                    price = price,
                    showIcon = showIcon,
                    name = name,
                    des = des,
                    pointDes = pointdes,
                    isHomeDisplay = isHomeDisplay,
                    sort = sort,
                    homeSort = homeSort,
                    unlocked = unlocks,
                    limitedTime = limittime,
                    discount=discount,
                };
                items.Add(item);
            }
            else
            {
                Debug.LogWarning($"Skipping line {i + 1}: Not enough fields.");
            }
        }
        shopItems = items .OrderBy(item => item.sort).ToList();
    }
    
    //商店首页界面物品列表
    public List<ShopDataItem> GetShopHomeItems()
    {
        var type2Count = 0;
        var maxType2 = 3; // 最多允许的type2商品数量
        // shoplimitDatas = GameDataManager.instance.UserData.limitShopItems
        //     .ToDictionary(x => x.id, x => x);

        // 检查限购状态(永久去广告)
        //bool removeads = GameDataManager.instance.UserData.limitShopItems.Any(itemdata => itemdata.isget && !itemdata.isoverdate && itemdata.adstype == 6);
       
        return shopItems
            .Where(item =>
            {

                // if (removeads && (item.id == 8 || item.id == 13 || item.id == 14))
                // {
                //     return false;
                // }
                //if (buyshopDta!=null)
                //{
                //    if (buyshopDta.id == 13 && item.id == 13)
                //    {
                //        return false;
                //    }

                //    if(buyshopDta.id!=13&& (item.id == 8 || item.id == 13 || item.id == 14))
                //    {
                //        return false;
                //    }
                //}


                // 检查limitData是否存在且isopen为true
                // shoplimitDatas.TryGetValue(item.id, out var limitData);
                // if (limitData != null)
                // {
                //     if (!limitData.isopen||limitData.isget&&!limitData.isoverdate) return false;
                //
                //     if (!limitData.isget||limitData.isget&&limitData.isoverdate)
                //     {
                //         type2Count++;
                //         return true;
                //     }                    
                // }
                // 基础条件：必须显示在首页且未解锁
                if (item.isHomeDisplay == "1" && string.IsNullOrEmpty(item.unlocked[0]))
                {
                    if (item.type == 2)
                    {
                        if (type2Count < maxType2)
                        {
                            type2Count++;
                            return true;
                        }
                        return false;
                    }
                    return true;
                }
                return false;
            })
            .OrderBy(item => int.Parse(item.homeSort))
            .ToList();
    }

    /// <summary>
    /// 商店正常排序物品列表
    /// </summary>
    /// <returns></returns>
    public List<ShopDataItem> GetShopItems()
    {
        // 检查限购状态(永久去广告)
        //bool removeads = GameDataManager.instance.UserData.limitShopItems.Any(itemdata => itemdata.isget && !itemdata.isoverdate&&itemdata.adstype==6);

        // shopItems = shopItems.OrderBy(item => item.sort).ToList();
        // return shopItems;
        return shopItems
            .OrderBy(item => item.sort)
            .ToList();
    }
    
    /// <summary>
    /// 商店正常排序物品列表
    /// </summary>
    /// <returns></returns>
    public List<ShopDataItem> GetBuyShopItems()
    {        
        return shopItems.OrderBy(item => item.sort).ToList();
    }

    public ShopDataItem GetShopItem(int shopItemID)
    {
        return shopItems.FirstOrDefault(item => item.id == shopItemID);
    }

    //根据商品名称获取商品配置数据
    public ShopDataItem NameGetShopItem(string buyname)
    {
        return shopItems.FirstOrDefault(item => item.GetProduceName() == buyname);
    }

    private List<ShopDataItem> GetLimitAdsGifts()
    {
        return shopItems.FindAll(item =>!string.IsNullOrEmpty(item.limitedTime));
    }

    /// <summary>
    /// 限时礼包弹窗显示逻辑
    /// </summary>
    public async void ShowLimitAdsPanel()
    {
        // 检查限购状态
        //ShopLimitData buyshopDta = GameDataManager.instance.UserData.limitShopItems.Find(itemdata => itemdata.isget && !itemdata.isoverdate);

        // foreach (var item in _limitAdsGifts)
        // {
        //     // 判断是否到达解锁关卡
        //     if (!item.unlocked.Contains(StageController.Instance.CurStageInfo.StageNumber.ToString()))
        //         continue;
        //
        //
        //     if (buyshopDta != null)
        //     {
        //         if (buyshopDta.id == item.id && buyshopDta.adstype == (int)LimitRewordType.Remove7DayAds)
        //         {
        //             return;
        //         }
        //
        //         if (buyshopDta.adstype == (int)LimitRewordType.RemoveAds)
        //         {
        //             return;
        //         }
        //     }
        //
        //
        //     // 设置当前广告项目
        //     curshopAdsItem = item;
        //
        //     ShopLimitData shopdata = GameDataManager.instance.UserData.limitShopItems.Find(itemdata => itemdata.id == curshopAdsItem.id);
        //
        //     int hours = int.Parse(curshopAdsItem.limitedTime);
        //     DateTime endtime = DateTime.Now.AddHours(hours);
        //                 
        //     if (shopdata!=null)
        //     {
        //         if (!string.IsNullOrEmpty(shopdata.endtime))
        //         {
        //             DateTime buyendtime = DateTime.Parse(shopdata.endtime);
        //             TimeSpan timeSpan = DateTime.Now.Subtract(buyendtime);
        //             if (timeSpan.TotalMinutes <=0)
        //             {
        //                 endtime = buyendtime;
        //             }
        //         }
        //         shopdata.isopen = true;
        //         shopdata.endtime = endtime.ToString();
        //     }
        //     else
        //     {
        //         GameDataManager.instance.UserData.limitShopItems.Add(new ShopLimitData()
        //         {
        //             id = curshopAdsItem.id,
        //             endtime = endtime.ToString(),
        //             isopen = true,
        //             gettime = "",
        //             adstype = 0,
        //             isget = false,
        //             isoverdate = false,
        //         });
        //     }
        
            // 等待1秒后显示面板
            await Task.Delay(1500); // 1000毫秒 = 1秒
            SystemManager.Instance.ShowPanel(PanelType.AdsDiscountScreen);
            return;
        //}
    }

    /// <summary>
    /// 是否处于去除广告礼包期间
    /// </summary>
    /// <returns></returns>
    public bool IsRemoveAdsGift()
    {
        // if (shoplimitDatas == null) return false;
        //
        // foreach (var shopdata in shoplimitDatas)
        // {
        //     if (shopdata.Value.isget && !shopdata.Value.isoverdate)
        //     {
        //         return true;
        //     }
        // }
        
        return false;
    }
}