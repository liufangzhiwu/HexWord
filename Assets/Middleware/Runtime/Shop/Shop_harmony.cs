#if UNITY_OPENHARMONY
using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using OpenHarmonyKits.Signal;
using OpenHarmonyKits.Param;
using System.Collections.Generic;
using System.Linq;
using BestHTTP.Extensions;
using UnityEngine.EventSystems;

namespace Middleware
{
    // ============================================================
    //  本地持久化的“待处理订单”，用于跨进程的幂等补单
    // ============================================================
    [Serializable]
    public class PendingOrder
    {
        public string orderId;        // 平台订单号 (purchaseOrderId)
        public string productId;
        public string price;
        public string currency;
        public int    productType;    // 对应 ProductType 枚举
        public string purchaseToken;
        public bool   granted;        // 是否已经发放过奖励（幂等标志）
    }

    [Serializable]
    public class PendingOrderList
    {
        public List<PendingOrder> orders = new List<PendingOrder>();
    }

    public static class PendingOrderStore
    {
        private static string FilePath =>
            Path.Combine(Application.persistentDataPath, "pending_orders.json");

        private static PendingOrderList _cache;

        private static PendingOrderList Load()
        {
            if (_cache != null) return _cache;
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    _cache = JsonUtility.FromJson<PendingOrderList>(json)
                             ?? new PendingOrderList();
                }
                else
                {
                    _cache = new PendingOrderList();
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[PendingOrderStore] Load failed: " + e);
                _cache = new PendingOrderList();
            }
            return _cache;
        }

        private static void Save()
        {
            try
            {
                var json = JsonUtility.ToJson(_cache);
                // 先写临时文件再替换，尽量防止写文件时被杀导致文件损坏
                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogError("[PendingOrderStore] Save failed: " + e);
            }
        }

        public static List<PendingOrder> All()
        {
            return Load().orders;
        }

        public static PendingOrder Get(string orderId)
        {
            if (string.IsNullOrEmpty(orderId)) return null;
            return Load().orders.Find(o => o.orderId == orderId);
        }

        /// <summary>
        /// 添加或更新；若已存在，保留已有的 granted 状态。
        /// </summary>
        public static void AddOrUpdate(PendingOrder order)
        {
            if (order == null || string.IsNullOrEmpty(order.orderId)) return;
            var list = Load();
            var existing = list.orders.Find(o => o.orderId == order.orderId);
            if (existing != null)
            {
                existing.productId     = order.productId;
                existing.price         = order.price;
                existing.currency      = order.currency;
                existing.productType   = order.productType;
                existing.purchaseToken = order.purchaseToken;
                // granted 保持不变
            }
            else
            {
                list.orders.Add(order);
            }
            Save();
        }

        public static void MarkGranted(string orderId)
        {
            var o = Get(orderId);
            if (o != null && !o.granted)
            {
                o.granted = true;
                Save();
                Debug.Log("[PendingOrderStore] MarkGranted: " + orderId);
            }
        }

        public static void Remove(string orderId)
        {
            if (string.IsNullOrEmpty(orderId)) return;
            var list = Load();
            int n = list.orders.RemoveAll(o => o.orderId == orderId);
            if (n > 0)
            {
                Save();
                Debug.Log("[PendingOrderStore] Remove: " + orderId);
            }
        }

        public static void RemoveByToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return;
            var list = Load();
            int n = list.orders.RemoveAll(o => o.purchaseToken == token);
            if (n > 0)
            {
                Save();
                Debug.Log("[PendingOrderStore] RemoveByToken: " + token);
            }
        }

        public static bool IsGranted(string orderId)
        {
            var o = Get(orderId);
            return o != null && o.granted;
        }
    }

    // ============================================================
    //  商店主类
    // ============================================================
    public class Shop_harmony : IShop
    {
        private FinishPurchaseParameter purchaseParam = null;

        private string purchaseResult = "";

        ProductData productData = new ProductData();

        // consumable products lists
        string[] m_storeIDList = { };
        // nonconsumable products lists
        string[] m_storeNonconsumeIDList = { };
        // AutoRenewable produccts List
        string[] m_storeAutoRenewableIDList = { };

        public Action<ProductItem> buySuccessAction;
        Action<string> buyFailedAction;

        ProductItem productItem = new ProductItem();

        bool InitSucceed = false;
        public ShopDataItem CurrentShopDataItem { get; set; }

        // ------------------------------------------------------------
        //  初始化
        // ------------------------------------------------------------
        public void Init(float delay)
        {
            VerifyPayEnv();// init on start
            LoadProductData();
            Register();
        }

        private void Register()
        {
            SignalHandler.Instance.RegisterSignalDelegate<IAP_InitSignal>(OnIAPInitTrigger);
            SignalHandler.Instance.RegisterSignalDelegate<IAP_QueryProductsSignal>(OnQueryProductTrigger);
            SignalHandler.Instance.RegisterSignalDelegate<IAP_PurchaseSignal>(OnStartPurchaseTrigger);
            SignalHandler.Instance.RegisterSignalDelegate<IAP_ConsumePurchase>(OnConsumePurchaseTrigger);
            SignalHandler.Instance.RegisterSignalDelegate<IAP_StartSubscribeSignal>(OnStartSubscribeTrigger);
            SignalHandler.Instance.RegisterSignalDelegate<IAP_QueryOwnedPurchasesSignal>(OnCheckPurchaseInfo);
        }

        private void OnDestroy()
        {
            if (SignalHandler.Instance != null)
            {
                SignalHandler.Instance.UnRegisterSignalDelegate<IAP_InitSignal>(OnIAPInitTrigger);
                SignalHandler.Instance.UnRegisterSignalDelegate<IAP_QueryProductsSignal>(OnQueryProductTrigger);
                SignalHandler.Instance.UnRegisterSignalDelegate<IAP_PurchaseSignal>(OnStartPurchaseTrigger);
                SignalHandler.Instance.UnRegisterSignalDelegate<IAP_ConsumePurchase>(OnConsumePurchaseTrigger);
                SignalHandler.Instance.UnRegisterSignalDelegate<IAP_StartSubscribeSignal>(OnStartSubscribeTrigger);
                SignalHandler.Instance.UnRegisterSignalDelegate<IAP_QueryOwnedPurchasesSignal>(OnCheckPurchaseInfo);
            }
        }

        public void VerifyPayEnv()
        {
            OHSDKKitManager.Instance.InitIAP();
        }

        public void QuerySubscription()
        {
            OHSDKKitManager.Instance.QueryIAPList(ProductType.AUTORENEWABLE, m_storeAutoRenewableIDList);
        }

        public void QueryConsumable()
        {
            OHSDKKitManager.Instance.QueryIAPList(ProductType.CONSUMABLE, m_storeIDList);
        }

        public void QueryUnconsumable()
        {
            OHSDKKitManager.Instance.QueryIAPList(ProductType.NONCONSUMABLE, m_storeNonconsumeIDList);
        }

        private void LoadProductData()
        {
            foreach (var shopDataItem in ShopManager.shopManager.GetBuyShopItems())
            {
                productData.products.Add(new Product()
                {
                    id   = shopDataItem.GetProduceName(),
                    type = (ProductType)shopDataItem.purchaseType,
                });
            }

            if (productData.products.Count > 0)
            {
                List<string> consumableList    = new List<string>();
                List<string> nonConsumableList = new List<string>();
                List<string> autoRenewableList = new List<string>();

                foreach (var product in productData.products)
                {
                    switch (product.type)
                    {
                        case ProductType.CONSUMABLE:
                            consumableList.Add(product.id);
                            break;
                        case ProductType.NONCONSUMABLE:
                            nonConsumableList.Add(product.id);
                            break;
                        case ProductType.AUTORENEWABLE:
                            autoRenewableList.Add(product.id);
                            break;
                    }
                }
                m_storeIDList            = consumableList.ToArray();
                m_storeNonconsumeIDList  = nonConsumableList.ToArray();
                m_storeAutoRenewableIDList = autoRenewableList.ToArray();
            }
            else
            {
                Debug.LogError("ProductData asset not found!");
            }
        }

        public bool IsProductOk(string productId)
        {
            Product product = productData.products.Find(x => x.id == productId);
            if (product != null && InitSucceed)
            {
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------
        //  发起购买
        // ------------------------------------------------------------
        public void Purchase(string productId,
                             Action<ProductItem> successAction,
                             Action<string> failedAction)
        {
            buyFailedAction = failedAction;
            buySuccessAction = successAction;

            Product product = productData.products.Find(x => x.id == productId);
            if (product == null)
            {
                Debug.LogError("[IAP] product not found in productData: " + productId);
                buyFailedAction?.Invoke("product_not_found");
                return;
            }

            PurchaseParameter purchaseParameter = new PurchaseParameter();
            purchaseParameter.productId   = product.id;
            purchaseParameter.productType = product.type;
            OHSDKKitManager.Instance.StartPurchase(purchaseParameter);
        }

        void IShop.Restore(Action<bool, ProductItem[]> restoreCallback)
        {
            Restore(restoreCallback);
        }

        public void Restore(Action<bool, ProductItem[]> restoreCallback)
        {
            // 1) 先处理本地遗留的待补单
            RetryAllPendingOrders();

            // 2) 再查询商品列表 & 已购订单（已购查询里可能还会再补一批）
            QueryConsumable();
            ConfirmCheckPurchase();
            restoreCallback?.Invoke(false, null);
        }

        // ------------------------------------------------------------
        //  IAP 初始化回调：在这里做“开机补单”
        // ------------------------------------------------------------
        private void OnIAPInitTrigger(SignalBase signal)
        {
            if (signal.hasError())
            {
                Debug.Log("[IAPInit Error ] " + "\n "
                   + "Code : " + signal.code + " \n Message : " + signal.message + "\n");
                return;
            }

            IAP_InitSignal targetSignal = (IAP_InitSignal)signal;
            Debug.Log("[IAPInit Success]" + "\n" + targetSignal.successMessage);

            InitSucceed = true;

          
        }

        /// <summary>
        /// 遍历本地待处理订单：
        ///   未发奖的先补发，然后重新请求消耗。
        /// </summary>
        private void RetryAllPendingOrders()
        {
            var all = PendingOrderStore.All();
            if (all == null || all.Count == 0) return;

            Debug.Log("[IAP] RetryAllPendingOrders count=" + all.Count);
            // 拷贝一份，避免遍历时被修改
            var snapshot = new List<PendingOrder>(all);
            foreach (var order in snapshot)
            {
                GrantRewardIfNeeded(order);
                ConsumePendingOrder(order);
            }
        }

        // ------------------------------------------------------------
        //  购买成功回调
        // ------------------------------------------------------------
        public void OnStartPurchaseTrigger(SignalBase signal)
        {
            if (signal.hasError())
            {
                Debug.Log("[StartPurchase Error ] " + "\n "
                  + "Code : " + signal.code + " \n Message : " + signal.message + "\n");
                buyFailedAction?.Invoke(signal.message);
                return;
            }

            IAP_PurchaseSignal targetSignal = (IAP_PurchaseSignal)signal;
            var payload = targetSignal.purchaseOrderPayload;

            purchaseResult = "ProductToken " + payload.purchaseToken + "\n"
                             + "Product Order Id " + payload.purchaseOrderId + "\n";
            Debug.Log("the purchaseResult" + purchaseResult);
            Debug.Log("[StartPurchase Success]" + "\n" + purchaseResult);

            // ① 先落盘
            var pending = new PendingOrder
            {
                orderId       = payload.purchaseOrderId,
                productId     = payload.productId,
                price         = payload.price.ToString(),
                currency      = payload.currency,
                productType   = payload.productType,
                purchaseToken = payload.purchaseToken,
                granted       = false,
            };
            PendingOrderStore.AddOrUpdate(pending);

            // 保留原来的 productItem 变量，兼容外部可能的引用
            productItem = new ProductItem
            {
                order_id        = payload.purchaseOrderId,
                IsoCurrencyCode = payload.currency,
                ItemName        = payload.productId,
                ProductId       = payload.productId,
                LocalizedPrice  = payload.price,
            };

            // ② 立即发奖（幂等）
            GrantRewardIfNeeded(pending);

            // ③ 通知平台消耗
            ConsumePendingOrder(pending);
        }

        public void OnStartSubscribeTrigger(SignalBase signal)
        {
            if (!signal.hasError())
            {
                IAP_StartSubscribeSignal targetSignal = (IAP_StartSubscribeSignal)signal;
                purchaseResult = "applicationId " + targetSignal.subGroupStatusPayload.applicationId + "\n"
                     + "subGroupId " + targetSignal.subGroupStatusPayload.subGroupId + "\n";
                Debug.Log("the purchaseResult" + purchaseResult);
                Debug.Log("[StartSubscribe Success]" + "\n");
                Debug.Log(purchaseResult);
            }
            else
            {
                Debug.Log("[StartSubscribe Error ] " + "\n "
                 + "Code : " + signal.code + " \n Message : " + signal.message + "\n");
            }
        }

        // ------------------------------------------------------------
        //  消耗成功回调：只负责清表，不再发奖
        // ------------------------------------------------------------
        public void OnConsumePurchaseTrigger(SignalBase signal)
        {
            if (signal.hasError())
            {
                Debug.Log("[ConsumePurchase Error ] " + "\n "
                 + "Code : " + signal.code + " \n Message : " + signal.message + "\n");
                // 保留本地记录，下次启动会重试
                return;
            }

            IAP_ConsumePurchase target = (IAP_ConsumePurchase)signal;
            Debug.Log("[ConsumePurchase Success]" + "\n");
            Debug.Log("Consume Purchase Success. purchaseToken is "
                      + target.purchaseToken + " purchase type " + target.productType + "\n");

            // 平台侧已完成 -> 从本地待处理表移除
            PendingOrderStore.RemoveByToken(target.purchaseToken);
        }

        // ------------------------------------------------------------
        //  已购查询回调
        // ------------------------------------------------------------
        public void OnCheckPurchaseInfo(SignalBase signal)
        {
            if (signal.hasError())
            {
                Debug.Log("[CheckPurchase Error ] " + "\n "
                 + "Code : " + signal.code + " \n Message : " + signal.message + "\n");
                return;
            }

            IAP_QueryOwnedPurchasesSignal targetSignal = (IAP_QueryOwnedPurchasesSignal)signal;
            Debug.Log("[CheckPurchase Success]" + "\n");
            Debug.Log("Product Type: " + targetSignal.productType + "\n");
            Debug.Log("Query Type: " + targetSignal.queryType + "\n");

            if (targetSignal.purchaseDataArray == null || targetSignal.purchaseDataArray.Length == 0)
            {
                Debug.Log("No purchase data available.\n");
                return;
            }

            Debug.Log($"products nums{targetSignal.purchaseDataArray.Length}");
            Debug.Log("Purchase Data List:\n");

            foreach (var purchaseData in targetSignal.purchaseDataArray)
            {
                Debug.Log("purchaseToken: " + purchaseData.purchaseToken
                          + "\n purchaseOrderId: " + purchaseData.purchaseOrderId
                          + "\n" + purchaseData.finishStatus);

                // 只处理平台侧尚未完成的订单
                if (purchaseData.finishStatus != "2") continue;

                var pending = new PendingOrder
                {
                    orderId       = purchaseData.purchaseOrderId,
                    productId     = purchaseData.productId,
                    price         = purchaseData.price.ToString(),
                    currency      = purchaseData.currency,
                    productType   = (int)purchaseData.productType,
                    purchaseToken = purchaseData.purchaseToken,
                    granted       = false,
                };
                PendingOrderStore.AddOrUpdate(pending);

                productItem = new ProductItem
                {
                    order_id        = purchaseData.purchaseOrderId,
                    IsoCurrencyCode = purchaseData.currency,
                    ItemName        = purchaseData.productId,
                    ProductId       = purchaseData.productId,
                    LocalizedPrice  = purchaseData.price,
                };

                // 先发奖（幂等）-> 再消耗
                GrantRewardIfNeeded(pending);
                ConsumePendingOrder(pending);
            }
        }

        // ------------------------------------------------------------
        //  查询商品回调
        // ------------------------------------------------------------
        public void OnQueryProductTrigger(SignalBase signal)
        {
            if (!signal.hasError())
            {
                IAP_QueryProductsSignal targetSignal = (IAP_QueryProductsSignal)signal;
                Debug.Log("[QueryProduct Success]" + "\n" + "productType is " + targetSignal.productType);
                foreach (var productId in targetSignal.productIds)
                {
                    Debug.Log("query productId is " + productId);
                }
                foreach (var product in targetSignal.products)
                {
                    Debug.Log("receive productId is " + product.id);
                }
            }
            else
            {
                Debug.Log("[QueryProduct Error ] " + "\n "
                   + "Code : " + signal.code + " \n Message : " + signal.message + "\n");
            }
        }

        // ------------------------------------------------------------
        //  注意：OnDisable 中不要再调用 ConsumePurchase
        //  未完成的订单交给“启动补单”处理
        // ------------------------------------------------------------
        private void OnDisable()
        {
            // 不再主动消耗；保留 purchaseParam 只用于日志/调试
            if (purchaseParam != null)
            {
                Debug.Log("[IAP] OnDisable with pending purchaseParam (skipped consume): "
                          + purchaseParam.purchaseOrderId);
                purchaseParam = null;
            }
        }

        // ------------------------------------------------------------
        //  内部辅助：发奖 & 消耗
        // ------------------------------------------------------------
        private void GrantRewardIfNeeded(PendingOrder order)
        {
            if (order == null) return;

            if (PendingOrderStore.IsGranted(order.orderId))
            {
                Debug.Log("[IAP] already granted, skip: " + order.orderId);
                return;
            }

            try
            {
                if (buySuccessAction != null)
                {
                    buySuccessAction.Invoke(productItem);
                }
                else
                {
                    OnPurchaseSuccess(productItem);
                }

                // 只有发奖成功后才落盘标记，避免“标记成功但发奖异常”导致漏发
                PendingOrderStore.MarkGranted(order.orderId);
                Debug.Log("[IAP] Reward granted: " + order.orderId);
            }
            catch (Exception e)
            {
                // 不标记为已发，下次启动/下次查询会重试
                Debug.LogError("[IAP] Grant reward failed, will retry: " + order.orderId + "\n" + e);
            }
        }

        public void OnPurchaseSuccess(ProductItem item)
        {
            //todo 关闭loading界面
            Debug.Log("购买成功: " + item.ProductId);

            ShopDataItem shopDataItem = ShopManager.shopManager.GetProduct(item.ProductId);

            Debug.Log("获取恢复购买商品ID: " + shopDataItem.produceNameId);
            if (shopDataItem != null)
            {
                Game.self.Shop.CurrentShopDataItem = shopDataItem.DeepCopy();

                SystemManager.Instance.ShowPanel(PanelType.AwardScreen);

                foreach (var dataitem in shopDataItem.productContent)
                {
                    int count = int.Parse(dataitem[1]);
                    int type = int.Parse(dataitem[0]);
                    //items.Add(new AnalyticMgr.Item { item_name = type.ToString(), quantity = count });
                    switch (type)
                    {
                        case (int)LimitRewordType.Coins:
                            GameDataManager.Instance.UserData.UpdateGold(count, false, false, "商店购买" + item.ItemName);
                            break;
                        case (int)LimitRewordType.Butterfly:
                            GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Butterfly, count,
                                "商店购买" + item.ItemName);
                            break;
                        case (int)LimitRewordType.Tipstool: //放大镜道具，整个词语提示
                            GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, count,
                                "商店购买" + item.ItemName);
                            break;
                        case (int)LimitRewordType.AutoComplete: //提示灯道具，单个字符提示
                            GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.AutoComplete, count,
                                "商店购买" + item.ItemName);
                            break;
                        case (int)LimitRewordType.RemoveAds:
                        case (int)LimitRewordType.Remove7DayAds:
                            //BuyRemoveAdsEvent(type);
                            break;
                    }
                }
            }

            ShopManager.shopManager.paysuccess = true;
            bool firstPay = GameDataManager.Instance.UserData.TotalPayTimes == 0;
            if (firstPay)
                GameDataManager.Instance.UserData.firstPayTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            GameDataManager.Instance.UserData.lastPayTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            GameDataManager.Instance.UserData.TotalPayTimes++;
            GameDataManager.Instance.UserData.TotalRevenue += item.LocalizedPrice;
            DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedShopBuy, 1);

            if (!UIUtilities.isEditMode)
            {
                AnalyticMgr.PurchaseFinished(item, firstPay);
            }

        }

        private void ConsumePendingOrder(PendingOrder order)
        {
            if (order == null) return;

            var param = new FinishPurchaseParameter
            {
                productType     = (ProductType)order.productType,
                purchaseToken   = order.purchaseToken,
                purchaseOrderId = order.orderId,
            };
            purchaseParam = param; // 保留原有字段引用

            Debug.Log("[IAP] ConsumePurchase request. purchaseToken="
                      + param.purchaseToken + " purchaseOrderId=" + param.purchaseOrderId);
            OHSDKKitManager.Instance.ConsumePurchase(param);
        }

        // ------------------------------------------------------------
        //  已购订单查询
        // ------------------------------------------------------------
        private void ConfirmCheckPurchase()
        {
            QueryPurchasesParameter queryPurchasesParameter = new QueryPurchasesParameter();
            queryPurchasesParameter.productType = ProductType.CONSUMABLE;
            queryPurchasesParameter.queryType   = PurchaseQueryType.ALL;
            OHSDKKitManager.Instance.CheckPurchase(queryPurchasesParameter);
        }
    }
}
#endif