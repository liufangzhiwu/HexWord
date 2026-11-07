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

        // ------------------------------------------------------------
        //  恢复购买 / 启动补单
        // ------------------------------------------------------------
        private Action<bool, ProductItem[]> _restoreCallback;
        private readonly List<ProductItem> _restoreItems = new List<ProductItem>();
        private int _restoreQueriesPending;
        private bool _restoreInProgress;

        /// <summary>
        /// 恢复购买（也是启动补单入口：GameCoreManager 进大厅时会调 Restore(null)）。
        ///
        /// 三步：
        ///   1) 本地待处理订单（pending_orders.json）→ 没发过奖的直接补发；
        ///   2) 平台已购查询（消耗型）→ 平台侧未核销的订单补发 + 核销；
        ///   3) 平台已购查询（非消耗型）→ 永久去广告这类没有核销流程，只能靠这里核对补发（bug51）。
        ///
        /// 旧实现是「同步立刻回调 (false, null)」，所以按钮永远弹「没有可恢复的内购」，
        /// 而且从来没查过非消耗型商品，永久去广告丢了就再也补不回来。
        /// </summary>
        public void Restore(Action<bool, ProductItem[]> restoreCallback)
        {
            Debug.Log("[IAP] Restore 开始：本地待补单 + 平台已购查询（消耗型 / 非消耗型）");

            _restoreCallback = restoreCallback;
            _restoreItems.Clear();
            _restoreInProgress = true;

            // 1) 本地遗留的待补单
            RetryAllPendingOrders();

            // 2) 平台已购查询（两个查询各自会回一次 OnCheckPurchaseInfo）
            QueryConsumable();            // 顺带刷新一次消耗型商品列表（保持原行为）
            _restoreQueriesPending = 2;
            ConfirmCheckPurchase(ProductType.CONSUMABLE);
            ConfirmCheckPurchase(ProductType.NONCONSUMABLE);

            // 3) 兜底：某些环境查询不回来（未登录 / 网络异常）时别把 UI 卡死
            if (Game.self != null) Game.self.StartCoroutine(CoRestoreTimeout(4f));
            else CompleteRestore("Game.self 为空");
        }

        private System.Collections.IEnumerator CoRestoreTimeout(float seconds)
        {
            float waited = 0f;
            while (waited < seconds && _restoreInProgress && _restoreQueriesPending > 0)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (_restoreInProgress) CompleteRestore($"{seconds}s 超时兜底");
        }

        /// <summary>每次已购查询回包后调一次（成功/失败都要调，否则计数永远不归零）</summary>
        private void NotifyRestoreQueryDone()
        {
            if (!_restoreInProgress) return;

            _restoreQueriesPending--;
            if (_restoreQueriesPending <= 0) CompleteRestore("已购查询完成");
        }

        private void CompleteRestore(string reason)
        {
            if (!_restoreInProgress) return;
            _restoreInProgress = false;

            Action<bool, ProductItem[]> cb = _restoreCallback;
            _restoreCallback = null;

            var items = _restoreItems.ToArray();
            Debug.Log($"[IAP] Restore 完成（{reason}）：本次补发 {items.Length} 笔");

            cb?.Invoke(items.Length > 0, items.Length > 0 ? items : null);
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
                FinishOrderAfterGrant(order);
            }
        }

        /// <summary>
        /// 发奖成功后收尾：消耗型通知平台核销；非消耗型（永久去广告）没有核销流程，直接清掉待处理记录。
        /// 以前非消耗型也去调 ConsumePurchase，平台必然报错，记录就永远躺在表里，每次启动都重试一遍（bug51）。
        /// </summary>
        private void FinishOrderAfterGrant(PendingOrder order)
        {
            if (order == null) return;
            if (!PendingOrderStore.IsGranted(order.orderId))
            {
                // 没发成功就留着，下次启动/下次查询重试
                return;
            }

            if (order.productType == (int)ProductType.NONCONSUMABLE)
            {
                PendingOrderStore.Remove(order.orderId);
                Debug.Log("[IAP] 非消耗型商品无需核销，已从待处理表移除: " + order.orderId);
                return;
            }

            ConsumePendingOrder(order);
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

            // ③ 付费归因回传（应用归因服务：只登记转化事件，金额不上送）
            long actionTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Game.self?.Attributes?.ReportPurchase(actionTime, payload.price, payload.currency);

            // ④ 通知平台消耗（非消耗型直接收尾，不调核销）
            FinishOrderAfterGrant(pending);
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
            try
            {
                HandleCheckPurchaseInfo(signal);
            }
            finally
            {
                // 一次查询（成功/失败/空结果）都算这次已购查询结束，Restore 靠它收口
                NotifyRestoreQueryDone();
            }
        }

        private void HandleCheckPurchaseInfo(SignalBase signal)
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
                          + "\n purchaseType: " + purchaseData.productType
                          + "\n finishStatus: " + purchaseData.finishStatus);

                // 非消耗型（永久去广告）：没有"核销"流程，finishStatus 不一定是 2，
                // 只要平台侧还查得到就算已购（bug51：以前这里被 finishStatus 过滤掉，永远补不回来）
                bool isNonConsumable = purchaseData.productType == ProductType.NONCONSUMABLE;

                // 消耗型：只处理平台侧尚未完成的订单
                if (!isNonConsumable && purchaseData.finishStatus != "2") continue;

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

                // 先发奖（幂等，内部按购买记录 orderId 去重）-> 再收尾（消耗型核销 / 非消耗型清记录）
                // 非消耗型：不受 pending 的 granted 标记限制，交给业务层按「购买记录」判重
                //（老版本可能已经标记过 granted 但奖励丢了，这里要能自愈，bug51）
                GrantRewardIfNeeded(pending, ignoreGrantedFlag: isNonConsumable);
                FinishOrderAfterGrant(pending);
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
        private void GrantRewardIfNeeded(PendingOrder order, bool ignoreGrantedFlag = false)
        {
            if (order == null) return;

            if (!ignoreGrantedFlag && PendingOrderStore.IsGranted(order.orderId))
            {
                Debug.Log("[IAP] already granted, skip: " + order.orderId);
                return;
            }

            // 按订单构造 ProductItem（不要用共享的 productItem 字段，多订单串行时会串单）
            var orderItem = new ProductItem
            {
                order_id        = order.orderId,
                ProductId       = order.productId,
                ItemName        = order.productId,
                IsoCurrencyCode = order.currency,
                LocalizedPrice  = ParsePrice(order.price),
            };

            try
            {
                // 恢复购买 / 启动补单阶段一律走业务层的统一补发：
                // 这时候 buySuccessAction 可能是上一次购买残留的界面回调（界面早关了），
                // 直接调它会把奖励发到错误的界面、而且漏掉体力/去广告/购买记录。
                bool useRestoreDelivery = buySuccessAction == null || _restoreInProgress;

                if (!useRestoreDelivery)
                {
                    // 正常应用内购买：交给发起购买的界面（商店 / 礼包弹窗 / 广告失败弹窗）发货
                    buySuccessAction.Invoke(orderItem);
                }
                else
                {
                    if (ShopManager.shopManager == null)
                    {
                        Debug.LogWarning("[IAP] ShopManager 未就绪，补发稍后重试: " + order.orderId);
                        return;
                    }

                    bool delivered = ShopManager.shopManager.DeliverRestoredPurchase(orderItem, "启动补单");
                    if (delivered) _restoreItems.Add(orderItem);
                }

                // ★ 发奖后先落盘，再标记已发：
                //   旧实现先 MarkGranted（写 pending_orders.json）才等下次自然存档，
                //   玩家在这中间杀进程 → 奖励和购买记录全丢，且因为已标记 granted 再也不会补发（bug50 / bug51）
                if (GameDataManager.Instance != null) GameDataManager.Instance.CommitGameData();

                PendingOrderStore.MarkGranted(order.orderId);
                Debug.Log("[IAP] Reward granted: " + order.orderId);
            }
            catch (Exception e)
            {
                // 不标记为已发，下次启动/下次查询会重试
                Debug.LogError("[IAP] Grant reward failed, will retry: " + order.orderId + "\n" + e);
            }
        }

        /// <summary>订单价格是字符串（分），补发时要还原成 float 用于购买记录/统计</summary>
        private static float ParsePrice(string price)
        {
            if (string.IsNullOrEmpty(price)) return 0f;
            float v;
            return float.TryParse(price, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out v) ? v : 0f;
        }

        /// <summary>
        /// 兼容旧入口。
        /// 旧实现自己写了一份发货 switch：只有金币 / 蝴蝶 / 提示 / 火箭，去广告是空 break，
        /// 无限体力 / 复活道具 / 购买记录全都没有（bug49、bug50）。
        /// 现在统一交给 ShopManager.DeliverRestoredPurchase，保证补发内容与正常购买一致。
        /// </summary>
        public void OnPurchaseSuccess(ProductItem item)
        {
            Debug.Log("[IAP] OnPurchaseSuccess（兼容入口）: " + (item != null ? item.ProductId : "null"));

            if (ShopManager.shopManager == null)
            {
                Debug.LogWarning("[IAP] ShopManager 未就绪，补发失败: " + (item != null ? item.ProductId : "null"));
                return;
            }

            ShopManager.shopManager.DeliverRestoredPurchase(item, "补单");
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
        //  已购订单查询（消耗型 / 非消耗型都要查：永久去广告属于非消耗型，没有核销流程）
        // ------------------------------------------------------------
        private void ConfirmCheckPurchase(ProductType productType)
        {
            QueryPurchasesParameter queryPurchasesParameter = new QueryPurchasesParameter();
            queryPurchasesParameter.productType = productType;
            queryPurchasesParameter.queryType   = PurchaseQueryType.ALL;
            OHSDKKitManager.Instance.CheckPurchase(queryPurchasesParameter);
        }
    }
}
#endif