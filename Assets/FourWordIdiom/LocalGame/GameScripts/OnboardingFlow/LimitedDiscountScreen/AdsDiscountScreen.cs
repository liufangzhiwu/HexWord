// using System;
// using System.Collections;
// using System.Collections.Generic;
// using System.Data;
// using System.Globalization;
// using System.Linq;
// using System.Threading.Tasks;
// using DG.Tweening;
// using UnityEngine;
// //using UnityEngine.Purchasing;
// using UnityEngine.UI;
//
// public class AdsDiscountScreen : UIWindow
// {
//     [SerializeField] private Button closeBtn; // 关闭按钮
//     [SerializeField] private Text title; // 音效文本显示
//     [SerializeField] private Text timeText; // 语言选择文本显示
//     [SerializeField] private Text priceText; // 价格
//     [SerializeField] private Text discountText; // 折扣前价格
//     [SerializeField] private Transform parent;
//     [SerializeField] private GiftItem giftItempPefab;
//     [SerializeField] private GameObject discountObj; 
//     [SerializeField] private GameObject circle; 
//     [SerializeField] private Button ClaimBtn;
//     private ObjectPool objectPool; // 对象池实例
//     private ShopDataItem currentShopItem;
//     //private ShopLimitData shopLimitData;
//     private List<GiftItem> GiftItems=new List<GiftItem>();
//
//     protected void Start()
//     {
//         
//         if (giftItempPefab == null)
//         {
//             giftItempPefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "DiscountItem").GetComponent<GiftItem>();
//         }
//         objectPool = new ObjectPool(giftItempPefab.gameObject, ObjectPool.CreatePoolContainer(transform, "GiftItemPool"));
//         
//     }
//     
//     protected override void InitializeUIComponents()
//     {
//         closeBtn.AddClickAction(OnCloseBtn); // 绑定关闭按钮事件
//         ClaimBtn.AddClickAction(OnBuyButtonClicked);
//     }
//
//     protected override void OnEnable()
//     {
//         base.OnEnable();
//         currentShopItem = ShopManager.shopManager.curshopAdsItem;
//         //shopLimitData=GameDataManager.instance.UserData.limitShopItems.Find(item => item.id == currentShopItem.id);
//         
//         InitUI();
//         //EventDispatcher.instance.TriggerUpdateLayerCoin(true,false);
//         
//         InitGiftItems();
//         
//         StartCoroutine(UpdateTime());
//         AudioManager.Instance.PlaySoundEffect("ShowUI");
//     }
//
//     private void InitUI()
//     {
//         if (currentShopItem == null) return;
//
//         title.text = MultilingualManager.Instance?.GetString(currentShopItem.name) ?? currentShopItem.name;
//
//         bool hasDiscount = !string.IsNullOrEmpty(currentShopItem.discount);
//         discountObj.SetActive(hasDiscount);
//         discountText.gameObject.SetActive(hasDiscount);
//
//         // 调整价格文本位置
//         priceText.GetComponent<RectTransform>().anchoredPosition =
//             hasDiscount ? new Vector2(93, 0) : Vector2.zero;
//
//         InitPriceText(hasDiscount);
//     }
//
//     private void InitPriceText(bool needDiscount)
//     {
//         if (currentShopItem == null)
//         {
//             Debug.LogWarning("当前商店项为空");
//             ShowLoadingState(true);
//             return;
//         }
//
//         Debug.Log($"礼包弹窗界面获取商品内购名称: {currentShopItem.GetProduceName()}");
//
//         //Product product = ShopManager.shopManager?.GetProduct(currentShopItem.GetProduceName());
//         //if (product == null || product.metadata == null)
//         //{
//         //    Debug.LogWarning($"无法获取商品信息: {currentShopItem.GetProduceName()}");
//         //    ShowLoadingState(true);
//         //    return;
//         //}
//
//         try
//         {
//
// #if UNITY_IOS
//             decimal price = product.metadata.localizedPrice;
//             string currencyCode = product.metadata.isoCurrencyCode;
//
//             Debug.Log($"商品价格: {price} ({currencyCode})");
//
//             // 获取合适的文化信息
//             CultureInfo culture = UIUtilities.GetCultureForCurrency(currencyCode);
// #else
//             float price = currentShopItem.price;
//             // 获取合适的文化信息
//             CultureInfo culture = UIUtilities.GetCultureForCurrency("");
// #endif
//             
//             // 格式化价格
//             priceText.text = UIUtilities.FormatCurrency(price,culture );
//
//             // 处理折扣
//             if (needDiscount)
//             {
//                 if (float.TryParse(currentShopItem.discount.TrimEnd('%'), out float discountPercent))
//                 {
//                     decimal discountRate = (decimal)(discountPercent / 100f);
//                     decimal originalPrice = (decimal) price / discountRate;
//                     discountText.text = UIUtilities.FormatCurrency(originalPrice, culture);
//                     discountObj.GetComponentInChildren<Text>().text = $"{currentShopItem.discount}{MultilingualManager.Instance.GetString("ShopDiscount")}";
//                    
//                 }
//                 else
//                 {
//                     Debug.LogWarning($"折扣格式无效: {currentShopItem.discount}");
//                     discountText.text = "N/A";
//                 }
//             }
//
//             ShowLoadingState(false);
//         }
//         catch (Exception ex)
//         {
//             Debug.LogError($"初始化价格文本时出错: {ex.Message}");
//             ShowLoadingState(true);
//         }
//     }
//
//     private void ShowLoadingState(bool isLoading)
//     {
//         circle.gameObject.SetActive(isLoading);
//         priceText.gameObject.SetActive(!isLoading);
//        
//         if (!string.IsNullOrEmpty(currentShopItem.discount))
//         {
//             discountText.gameObject.SetActive(!isLoading);
//         }
//     }
//
//     private string Gettime()
//     {
//         DateTime endtime = DateTime.Parse(shopLimitData.endtime);
//         TimeSpan timeSpan = endtime.Subtract(DateTime.Now);
//       
//         if (timeSpan.TotalMinutes > 0)
//         {
//             timeText.text = UIUtilities.FormatTimeRemaining(timeSpan);
//         }
//
//         // 输出倒计时
//         return timeSpan.TotalMinutes.ToString();
//     }
//
//     private IEnumerator UpdateTime()
//     {
//         yield return new WaitForSeconds(0.2f);
//         string time = Gettime();
//         while (true)
//         {
//             time = Gettime();
//             if (string.IsNullOrEmpty(time))
//             {
//                 shopLimitData.isopen = false;
//                 shopLimitData.endtime=null;
//                 OnCloseBtn();
//                 break; // 如果时间为空，退出循环
//             }
//           
//             yield return new WaitForSeconds(1); // 等待 60 秒
//         }
//     }
//
//     private async void InitGiftItems()
//     {
//         await Task.Delay(10);
//         
//         for (int i = 0; i < currentShopItem.productContent.Count; i++)
//         {
//             List<string> itemdata=currentShopItem.productContent[i];
//             if (GiftItems.Count > i)
//             {
//                 GiftItem giftItem =GiftItems[i];
//                 giftItem.SetShopData(itemdata, currentShopItem.id, currentShopItem.pointDes);
//             }
//             else
//             {
//                 GiftItem giftItem = objectPool.GetObject<GiftItem>(parent);
//                 giftItem.SetShopData(itemdata, currentShopItem.id, currentShopItem.pointDes);
//                 GiftItems.Add(giftItem);
//             }
//         }
//     }
//     
//     private void OnBuyButtonClicked()
//     {
//         //ShopManager.shopManager.OnBuyGoldTestButtonClicked(currentShopItem.GetProduceName()
//         //    ,
//         //    OnPurchaseSuccess,
//         //    OnPurchaseFailed);
//         // 处理购买逻辑
//         Debug.Log($"Buying: {currentShopItem.name}, Price: {currentShopItem.GetProduceName()}");
//     
//         string area = "";
//         //FirebaseManager.Instance.PayStart(currentShopItem.GetProduceName(),area,GameDataManager.instance.UserData.CurrentStage);
//     }
//     
//     //private void OnPurchaseSuccess(Product product)
//     //{
//     //    Debug.Log("购买成功: " + product.definition.id);
//        
//     //    if (currentShopItem.GetProduceName() == product.definition.id)
//     //    {
//     //        foreach (var dataitem in currentShopItem.productContent)
//     //        {
//     //            int count = int.Parse(dataitem[1]);
//     //            int type = int.Parse(dataitem[0]);
//                 
//     //            switch (type)
//     //            {
//     //                case (int)LimitRewordType.Coins:
//     //                    GameDataManager.instance.UserData.Gold += count;
//     //                    EventDispatcher.TriggerChangeGoldUI(count,true);
//     //                    break;
//     //                case (int)LimitRewordType.Butterfly:
//     //                    GameDataManager.instance.UserData.toolInfo[103].count += count;
//     //                    //EventManager.OnChangGoldUI?.Invoke(0, false);
//     //                    break;
//     //                case (int)LimitRewordType.Tipstool:
//     //                    GameDataManager.instance.UserData.toolInfo[102].count += count;
//     //                    //EventManager.OnChangGoldUI?.Invoke(0, false);
//     //                    break;
//     //                case (int)LimitRewordType.Resettool:
//     //                    GameDataManager.instance.UserData.toolInfo[101].count += count;
//     //                    //EventManager.OnChangGoldUI?.Invoke(0, false);
//     //                    break;
//     //                case (int)LimitRewordType.RemoveAds:
//     //                case (int)LimitRewordType.Remove7DayAds:
//     //                    ShopLimitData shopLimitData= GameDataManager.instance.UserData.limitShopItems.Find(item =>item.id == currentShopItem.id);
//     //                    if (shopLimitData != null)
//     //                    {
//     //                        shopLimitData.isoverdate = false;
//     //                        shopLimitData.isget = true;
//     //                        shopLimitData.gettime=DateTime.Now.ToString();
//     //                        shopLimitData.adstype = type;
//     //                    }
//     //                    //AdsManager.Instance.RemoveBannerAd();
//     //                    break;
//     //            }
//     //        }
//     //    }
//     //    // 获取商品价格和货币代码
//     //    //string currencyCode = product.metadata.isoCurrencyCode;
//     //    //float localizedPrice = (float)product.metadata.localizedPrice;
//     //    //string area = "";
//     //    //FirebaseManager.Instance.PaySuccess(product,1);
//     //    ShopManager.shopManager.paysuccess = true;
//     //    DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedShopBuy,1);
//     //    OnCloseBtn();
//     //    //AdjustManager.Instance.SendPurchaseEvent();
//     //    // 处理购买成功后的逻辑，例如增加游戏内货币
//     //}
//
//     private void OnPurchaseFailed(string error)
//     {
//         Debug.Log("购买失败: " + error);
//         string area = "";
//         //FirebaseManager.Instance.PayFailed(currentShopItem.GetProduceName(),area,GameDataManager.instance.UserData.CurrentStage,error);
//         // 处理购买失败后的逻辑，例如显示错误提示
//     }
//
//    
//     private void OnCloseBtn()
//     {
//         base.Close(); // 隐藏面板
//     }
//     
//     public override void OnHideAnimationEnd()
//     {
//         base.OnHideAnimationEnd();
//     }
//
//     protected override void OnDisable()
//     {
//         base.OnDisable();
//         ClaimBtn.interactable = true;
//         closeBtn.interactable = true;
//     }
// }
