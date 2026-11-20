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

        // ------------------------------------------------------------
        //  支付会话（会话号 + 状态机）
        //  ──────────────────────────────────────────────────────────
        //  鸿蒙的支付收银台是 UIExtension 模态窗口：
        //    · 用户「取消 / 超时」返回时，平台回调可能收不到；
        //    · 玩家在收银台里慢慢输密码时，游戏进程仍在跑。
        //  历史上用「发起即计时 120s」判超时，导致钱还没付就被判失败 →
        //  先弹「购买失败」、回调被清空，之后再买还会被平台侧未核销订单挡住。
        //
        //  现在的规则：
        //    · 收银台开着 → 不计时（_cashierOpen）；
        //    · 收到「已离开收银台」信号 → 等 CallbackGraceSeconds；
        //    · 仍无回调 → 先主动核单，命中就按成功发货，未命中才按取消收口；
        //    · 迟到回调不覆盖当前轮次（_purchaseInProgress + _pendingProductId 校验）。
        // ------------------------------------------------------------
        private int _purchaseSession;
        private Coroutine _purchaseWatchdog;

        /// <summary>本轮支付是否仍在进行（发起 → 收口）。同时用作并发互斥 + 迟到回调判定。</summary>
        private bool _purchaseInProgress;

        /// <summary>本轮发起支付的商品 id，用于识别「迟到回调属于哪一轮」，防止串单。</summary>
        private string _pendingProductId;

        /// <summary>
        /// 收银台是否还在前台。为 true 时看门狗**不计时**——
        /// 玩家可能正在慢慢输支付密码，这段时间既不能判失败、也不能弹任何提示。
        /// </summary>
        private bool _cashierOpen;

        /// <summary>离开收银台后，等待平台支付回调的时间（秒）。</summary>
        private const float CallbackGraceSeconds = 5f;

        /// <summary>
        /// 收银台从未关闭时的极端兜底（秒）：超过就静默收口（只放掉 loading，不判失败）。
        /// 这是为了界面不能永远挂在 loading 上，而不是用来判支付结果。
        /// </summary>
        private const float CashierHardCapSeconds = 180f;

        /// <summary>本轮是否正在主动核单（已购查询）中。</summary>
        private bool _reconciling;

        /// <summary>核单时两次已购查询（消耗型 / 非消耗型）还剩几个没回包。</summary>
        private int _reconcileQueriesPending;

        /// <summary>核销失败的重试次数（成功后归零）。</summary>
        private int _consumeRetryAttempt;

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
            // ★ 同一时间只允许一笔支付。
            //   以前没有互斥：玩家连点会并发两笔，回调互相覆盖（buySuccessAction 被后一笔顶掉、
            //   前一笔的奖励发到了后一笔的界面上），表现为「付了钱没反应 / 之后唤不起支付」。
            if (_purchaseInProgress)
            {
                Debug.LogWarning("[IAP] 已有一笔支付进行中（" + _pendingProductId + "），忽略本次重复请求: " + productId);
                // 调用方在点按钮时已经挂过全屏 loading，这里放掉，避免重复点击后界面卡住
                MessageSystem.Instance?.HideLoadingAnimation();
                return;
            }

            buyFailedAction = failedAction;
            buySuccessAction = successAction;

            Product product = productData.products.Find(x => x.id == productId);
            if (product == null)
            {
                Debug.LogError("[IAP] product not found in productData: " + productId);
                ClosePurchaseSessionAsFailed("product_not_found");
                return;
            }

            _purchaseInProgress = true;
            _pendingProductId = product.id;
            _cashierOpen = true;   // 收银台即将打开：看门狗在它关掉之前不计时

            PurchaseParameter purchaseParameter = new PurchaseParameter();
            purchaseParameter.productId   = product.id;
            purchaseParameter.productType = product.type;
            OHSDKKitManager.Instance.StartPurchase(purchaseParameter);

            // ★ 发起后挂看门狗：平台不回调也必须能在界面上收口
            BeginPurchaseWatchdog();
        }

        // ------------------------------------------------------------
        //  支付看门狗
        //
        //  ★ 不能用「墙上时间」判超时。
        //    鸿蒙收银台是模态 UIExtension，玩家在里面慢慢输密码时游戏进程仍在跑，
        //    旧实现 120s 一到就把这轮判死 → 钱付了却先弹「购买失败」，
        //    回调被清空后再也发不出货，下一次购买还会被平台侧未核销订单挡住。
        //
        //  现在改成两段式：
        //    ① 收银台还开着（_cashierOpen）→ 完全不计时，只等平台回调；
        //       只有超过 CashierHardCapSeconds 这种极端情况才静默收口（只放 loading，不判失败）。
        //    ② 收到「已离开收银台」信号（页面 onFocus / 回到前台 / 平台回调）后，
        //       才开始等待 CallbackGraceSeconds；仍无回调 → 先主动查一次已购（查到就发货并收口），
        //       查不到才按取消收口（这时弹「取消购买」才是准确的）。
        // ------------------------------------------------------------
        private void BeginPurchaseWatchdog()
        {
            // ★ 不能重置 _cashierOpen：调用方（Purchase / OnWindowFocusGained）刚设好这个状态，
            //   重置会让看门狗一启动就以为「收银台已关闭」，退化成旧的墙上时间判失败。
            EndPurchaseWatchdog(resetCashier: false);
            int session = ++_purchaseSession;

            if (Game.self == null)
            {
                Debug.LogWarning("[IAP] Game.self 为空，无法启动支付看门狗（仅日志告警）");
                return;
            }
            _purchaseWatchdog = Game.self.StartCoroutine(CoPurchaseWatchdog(session));
        }

        /// <summary>作废当前这一轮（会话号自增，正在跑的协程会在下一帧退出）</summary>
        private void EndPurchaseWatchdog(bool resetCashier = true)
        {
            _purchaseSession++;
            if (_purchaseWatchdog != null && Game.self != null)
            {
                Game.self.StopCoroutine(_purchaseWatchdog);
            }
            _purchaseWatchdog = null;
            if (resetCashier) _cashierOpen = false;
            _reconciling = false;
            _reconcileQueriesPending = 0;
        }

        private System.Collections.IEnumerator CoPurchaseWatchdog(int session)
        {
            // ① 收银台开着：不计时（只保留一个极端兜底）
            float cashierWaited = 0f;
            while (_cashierOpen && session == _purchaseSession)
            {
                cashierWaited += Time.unscaledDeltaTime;
                if (cashierWaited >= CashierHardCapSeconds)
                {
                    Debug.LogError("[IAP] 收银台打开 " + CashierHardCapSeconds
                                   + "s 仍未关闭且无回调，静默收口（不判失败，只放掉 loading）");
                    _purchaseWatchdog = null;
                    ClosePurchaseSessionNeutral("cashier_hard_cap");
                    yield break;
                }
                yield return null;
            }

            if (session != _purchaseSession) yield break;

            // ② 已离开收银台：给平台回调留出 CallbackGraceSeconds
            float waited = 0f;
            while (waited < CallbackGraceSeconds)
            {
                if (session != _purchaseSession) yield break;
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (session != _purchaseSession) yield break;

            // ③ 还没回调 → 先主动核单（钱可能已经付了，只是回调丢了）
            _purchaseWatchdog = null;
            Debug.LogWarning("[IAP] 离开收银台 " + CallbackGraceSeconds + "s 仍未收到支付回调，主动核对订单");
            yield return CoReconcileByQuery(session);

            if (session != _purchaseSession) yield break;   // 核单命中，已经按成功收口

            // ④ 核单也没有本次订单 → 按取消收口
            Debug.LogWarning("[IAP] 核单未发现本次订单，按取消处理");
            ClosePurchaseSessionAsFailed("purchase_canceled_no_callback");
        }

        /// <summary>
        /// 主动核单：查一次已购（消耗型 + 非消耗型）。
        /// 命中本商品的订单会走 GrantRewardIfNeeded 正常发货并收口（不给玩家弹失败）；
        /// 查不到就交回看门狗按取消处理。
        /// </summary>
        private System.Collections.IEnumerator CoReconcileByQuery(int session)
        {
            if (session != _purchaseSession) yield break;

            _reconciling = true;
            _reconcileQueriesPending = 2;
            ConfirmCheckPurchase(ProductType.CONSUMABLE);
            ConfirmCheckPurchase(ProductType.NONCONSUMABLE);

            float waited = 0f;
            while (waited < 3f && _reconcileQueriesPending > 0 && session == _purchaseSession)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            _reconcileQueriesPending = 0;
            _reconciling = false;
        }

        /// <summary>
        /// 停掉看门狗但**保留本轮会话**（_purchaseInProgress 不清），
        /// 用于「发起支付报错，准备先核单」的场景，避免看门狗和核单两条路径同时收口。
        /// </summary>
        private void StopWatchdogKeepSession()
        {
            _cashierOpen = false;
            if (_purchaseWatchdog != null && Game.self != null)
            {
                Game.self.StopCoroutine(_purchaseWatchdog);
            }
            _purchaseWatchdog = null;
            _purchaseSession++;   // 让正在跑的看门狗协程在下一帧退出
        }

        /// <summary>
        /// 发起支付失败后的核单：查一次已购。
        ///   · 命中本轮商品 → 说明钱其实付过了（回调丢了 / 订单未核销），按成功补发并核销；
        ///   · 未命中 → 才把这次失败报给界面。
        /// </summary>
        private System.Collections.IEnumerator CoPurchaseErrorReconcile(int session, string errorMessage)
        {
            Debug.LogWarning("[IAP] 发起支付失败，先核单确认是否为上一笔未核销订单阻塞: " + errorMessage);

            _reconciling = true;
            _reconcileQueriesPending = 2;
            ConfirmCheckPurchase(ProductType.CONSUMABLE);
            ConfirmCheckPurchase(ProductType.NONCONSUMABLE);

            float waited = 0f;
            while (waited < 3f && _reconcileQueriesPending > 0 && session == _purchaseSession)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            _reconcileQueriesPending = 0;
            _reconciling = false;

            if (session != _purchaseSession) yield break;   // 核单命中，已按成功收口

            Debug.LogWarning("[IAP] 核单未发现可补发订单，按失败处理: " + errorMessage);
            ClosePurchaseSessionAsFailed(errorMessage);
        }

        /// <summary>
        /// 本轮支付以失败/取消收口：清掉界面回调、作废看门狗，保证界面一定被释放。
        /// 同时把 buySuccessAction 置空——万一平台回调姗姗来迟（钱已付），
        /// GrantRewardIfNeeded 会自动改走「补单发货」，奖励不会丢。
        /// </summary>
        private void ClosePurchaseSessionAsFailed(string reason)
        {
            Action<string> failed = buyFailedAction;
            buyFailedAction = null;
            buySuccessAction = null;

            _purchaseInProgress = false;
            _pendingProductId = null;
            EndPurchaseWatchdog();

            failed?.Invoke(reason);
        }

        /// <summary>
        /// 正常成功收口：停看门狗、清回调（调用前必须已经发过奖）。
        /// </summary>
        private void ClosePurchaseSessionAsSuccess(string reason)
        {
            if (!_purchaseInProgress)
            {
                // 迟到回调：只补单，不动当前会话
                return;
            }

            _purchaseInProgress = false;
            _pendingProductId = null;
            EndPurchaseWatchdog();
            buyFailedAction = null;
            buySuccessAction = null;

            // 兜底放掉 loading：核单命中但奖励此前已发（IsGranted 命中被跳过）时，
            // 界面拿不到成功回调，这里必须把全屏遮罩收掉，否则会一直卡着。
            MessageSystem.Instance?.HideLoadingAnimation();

            Debug.Log("[IAP] 本轮支付已正常收口: " + reason);
        }

        /// <summary>
        /// 静默收口：只放掉界面 loading，**不弹任何失败提示**。
        /// 用于「收银台超长打开」这种非玩家责任的场景，避免钱付了却提示失败。
        /// </summary>
        private void ClosePurchaseSessionNeutral(string reason)
        {
            bool hadSession = _purchaseInProgress;
            _purchaseInProgress = false;
            _pendingProductId = null;
            buyFailedAction = null;
            buySuccessAction = null;
            EndPurchaseWatchdog();
            MessageSystem.Instance?.HideLoadingAnimation();

            if (hadSession) Debug.LogWarning("[IAP] 本轮支付静默收口（不弹失败）: " + reason);
        }

        // ------------------------------------------------------------
        //  页面重新获得焦点（ArkTS: Index.ets onFocus → PushMessageReceiver）
        //
        //  这是「玩家已经离开收银台」的唯一可靠信号：系统既可能不给 onForeground，
        //  也可能不给支付结果回调。这里只做一件事——把收银台标记为已关闭，
        //  让看门狗开始计时等待回调（旧实现是「焦点回来 1.5s 就判取消」，
        //  网络回调晚到 2s 就会被误判成失败，钱付了却提示失败）。
        // ------------------------------------------------------------
        public void OnWindowFocusGained()
        {
            if (!_purchaseInProgress) return;

            Debug.Log("[IAP] 页面重新获得焦点，收银台视为已关闭，开始等待支付回调");
            _cashierOpen = false;

            // 看门狗可能因为极端兜底已经停了：这时补一个，保证这一轮最终一定会收口
            if (_purchaseWatchdog == null && Game.self != null) BeginPurchaseWatchdog();
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
                // ★ 取消/失败一定要把本轮收口：以前这里只调 buyFailedAction，
                //   如果平台回调压根没到（或 code 类型对不上被判成成功），界面就永久卡在 loading
                if (!_purchaseInProgress)
                {
                    // 本轮早已收口（典型：核单/取消之后平台才把错误码补回来），别再弹一次
                    Debug.LogWarning("[IAP] 收到过期的支付错误回调，忽略: " + signal.message);
                    return;
                }

                // 用户主动取消（1001860000）：直接按取消收口，不必核单
                string codeText = signal.code + " " + signal.message;
                bool isUserCancel = codeText.Contains("1001860000");

                if (isUserCancel || Game.self == null)
                {
                    ClosePurchaseSessionAsFailed(signal.message);
                    return;
                }

                // ★ 发起支付直接失败，很可能是「上一笔未核销订单」把平台挡住了
                //   （真机表现：付过一次之后再也拉不起收银台，每次都弹购买失败）。
                //   先核一次单：确认没有可补发的订单，再判失败；有就按成功补发。
                //   ★ 失败原因带上平台错误码：弹窗要按原因展示「购买失败」的具体文案。
                string failReason = string.IsNullOrWhiteSpace(codeText) ? "purchase_failed" : codeText.Trim();
                StopWatchdogKeepSession();
                Game.self.StartCoroutine(CoPurchaseErrorReconcile(_purchaseSession, failReason));
                return;
            }

            IAP_PurchaseSignal targetSignal = (IAP_PurchaseSignal)signal;
            var payload = targetSignal.purchaseOrderPayload;

            // ★ 平台错误 JSON 一旦被反序列化成「成功」（code 类型不匹配时会发生），
            //   payload 就是空的：以前会直接 NRE 或按空订单号发奖，界面再也不收口。
            //   这里统一按失败处理，宁可让玩家再点一次，也不能卡死或白送。
            //   订单号缺失时退化用 purchaseToken（与已购查询链路的口径一致）。
            string orderId = payload != null ? payload.purchaseOrderId : null;
            if (string.IsNullOrEmpty(orderId) && payload != null) orderId = payload.purchaseToken;

            if (payload == null || string.IsNullOrEmpty(orderId))
            {
                Debug.LogError("[IAP] 购买回调缺少订单号，按失败处理。productId=" + targetSignal.productId
                               + " code=" + signal.code + " message=" + signal.message);
                if (_purchaseInProgress)
                    ClosePurchaseSessionAsFailed(string.IsNullOrEmpty(signal.message) ? "invalid_purchase_payload" : signal.message);
                return;
            }

            // ★ 串单保护：判断这笔回调属不属于「当前这一轮」。
            //   旧实现无条件 EndPurchaseWatchdog + 清空回调：一笔迟到的旧回调会把
            //   「下一笔正在进行的支付」的看门狗和界面回调一起打掉，
            //   于是下一笔即使付了钱也没有任何反馈 → 玩家感觉「之后唤不起支付」。
            bool belongsToCurrentRound =
                _purchaseInProgress &&
                (string.IsNullOrEmpty(_pendingProductId) || payload.productId == _pendingProductId);

            if (!belongsToCurrentRound)
            {
                Debug.LogWarning("[IAP] 收到不属于当前轮次的支付回调（可能上一笔迟到），只补单不改当前会话。"
                                 + "payload.productId=" + payload.productId
                                 + " 当前轮商品=" + (_pendingProductId ?? "无")
                                 + " inProgress=" + _purchaseInProgress);
            }

            purchaseResult = "ProductToken " + payload.purchaseToken + "\n"
                             + "Product Order Id " + payload.purchaseOrderId + "\n";
            Debug.Log("the purchaseResult" + purchaseResult);
            Debug.Log("[StartPurchase Success]" + "\n" + purchaseResult);

            // ① 先落盘
            var pending = new PendingOrder
            {
                orderId       = orderId,
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
                order_id        = orderId,
                IsoCurrencyCode = payload.currency,
                ItemName        = payload.productId,
                ProductId       = payload.productId,
                LocalizedPrice  = payload.price,
            };

            // ② 立即发奖（幂等）。
            //   只有属于本轮才交给界面回调发货；迟到回调走补单通道，避免发到错误的界面上。
            GrantRewardIfNeeded(pending, deliverToUi: belongsToCurrentRound);

            // ③ 付费归因回传（应用归因服务：只登记转化事件，金额不上送）
            long actionTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Game.self?.Attributes?.ReportPurchase(actionTime, payload.price, payload.currency);

            // ④ 通知平台消耗（非消耗型直接收尾，不调核销）
            FinishOrderAfterGrant(pending);

            // ⑤ 只有属于本轮才收口（停看门狗、清回调），否则会打掉正在进行的那一轮
            if (belongsToCurrentRound) ClosePurchaseSessionAsSuccess("callback_success");
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

                // ★ 这两种错误说明"这单在平台侧本来就算完成了"，重试没有意义，直接清本地记录：
                //   1001860053 = 重复发货（已核销过）；1001860052 = 未拥有该商品（订单已失效/被回收）。
                //   已交付（finishStatus=1）的订单走"只核销"分支时会命中这里，
                //   不能因为重复核销失败就退避重试刷日志、还把记录留在待处理表里。
                string consumeCode = ("" + signal.code).Trim();
                if (consumeCode.Contains("1001860053") || consumeCode.Contains("1001860052"))
                {
                    Debug.Log("[IAP] 平台侧已核销/订单已失效，视为完成，清本地记录: code=" + consumeCode);
                    _consumeRetryAttempt = 0;
                    if (purchaseParam != null && !string.IsNullOrEmpty(purchaseParam.purchaseToken))
                        PendingOrderStore.RemoveByToken(purchaseParam.purchaseToken);
                    purchaseParam = null;
                    return;
                }

                // ★ 核销失败必须重试：鸿蒙 IAP 里「未核销」的消耗型订单会一直挡住下一笔购买，
                //   真机表现就是「付过一次之后再点购买，收银台拉不起来，一直弹购买失败」。
                //   旧实现只打一行日志等下次启动，玩家当场就再也买不了东西。
                RetryConsumeAfterFailure(++_consumeRetryAttempt);
                return;
            }

            IAP_ConsumePurchase target = (IAP_ConsumePurchase)signal;
            Debug.Log("[ConsumePurchase Success]" + "\n");
            Debug.Log("Consume Purchase Success. purchaseToken is "
                      + target.purchaseToken + " purchase type " + target.productType + "\n");

            _consumeRetryAttempt = 0;

            // 平台侧已完成 -> 从本地待处理表移除
            PendingOrderStore.RemoveByToken(target.purchaseToken);
        }

        /// <summary>
        /// 核销失败后的退避重试（5s / 15s / 45s，最多 3 次）。
        /// 仍然失败就留在待处理表里，下次启动或下次已购查询会继续尝试。
        /// </summary>
        private void RetryConsumeAfterFailure(int attempt)
        {
            if (purchaseParam == null)
            {
                Debug.LogWarning("[IAP] 核销失败但没有可重试的订单参数，等下次启动补核销");
                return;
            }

            if (attempt > 3)
            {
                Debug.LogError("[IAP] 核销重试 3 次仍失败，留待下次启动/下次已购查询重试: "
                               + purchaseParam.purchaseOrderId);
                return;
            }

            if (Game.self == null) return;

            float delay = attempt == 1 ? 5f : (attempt == 2 ? 15f : 45f);
            Debug.LogWarning("[IAP] 核销失败，将在 " + delay + "s 后重试第 " + attempt + " 次: "
                             + purchaseParam.purchaseOrderId);
            Game.self.StartCoroutine(CoRetryConsume(purchaseParam, attempt, delay));
        }

        private System.Collections.IEnumerator CoRetryConsume(FinishPurchaseParameter param, int attempt, float delay)
        {
            float waited = 0f;
            while (waited < delay)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (param == null) yield break;

            Debug.LogWarning("[IAP] 核销重试第 " + attempt + " 次: " + param.purchaseOrderId);
            OHSDKKitManager.Instance.ConsumePurchase(param);
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
                // 核单链路：两次查询（消耗型 / 非消耗型）各算一次（Restore 的查询不算）
                if (_reconciling && _reconcileQueriesPending > 0) _reconcileQueriesPending--;

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
                bool isNonConsumable = purchaseData.productType == (int)ProductType.NONCONSUMABLE;

                // ★ 订单号是补发幂等的唯一 key（业务层按 PurchaseRecord.orderId 判重）。
                //   有些渠道/场景会返回空 purchaseOrderId，此时 order_id 为空会让
                //   HasPurchaseRecordForOrder("") 恒为 false → 每次启动都重新发一遍。
                //   退化用 purchaseToken（同一笔订单稳定不变），再不行才用商品名兜底（非消耗型最多发一次）。
                string orderId = purchaseData.purchaseOrderId;
                if (string.IsNullOrEmpty(orderId))
                {
                    orderId = !string.IsNullOrEmpty(purchaseData.purchaseToken)
                        ? purchaseData.purchaseToken
                        : (isNonConsumable ? "nonconsumable_" + purchaseData.productId : "");

                    Debug.LogWarning("[IAP] 平台未返回 purchaseOrderId，改用兜底 key 去重: " + orderId
                                     + " productId=" + purchaseData.productId);
                }

                // ★ finishStatus 是「发货状态」，不是「付款状态」（鸿蒙 PurchaseOrderPayload）：
                //     "1" = 已交付（平台侧已确认发货，即已经 finishPurchase 过）
                //     "2" = 未交付（钱付了，还没确认发货）
                //   以前用 `finishStatus == "2"` 一把判定「是否已支付」，把三种完全不同的情况
                //   压成了一个结论（已交付的历史单会被当成"未确认支付"）。这里拆成三态：
                //     ① 已交付 + 本地无记录  → 不补发，跳过（历史单 / 已在别处发货）
                //     ② 已交付 + 本地有记录  → 不重复发货，只做核销收尾
                //     ③ 未交付 + (有 purchaseTime / 本地有记录 / 平台标 "2") → 真的付了没发货 → 补发 + 核销
                //     （purchaseTime 为空 = 订单未完成，官方字段口径）
                bool platformDelivered = purchaseData.finishStatus == "1";
                bool hasPurchaseTime   = !string.IsNullOrEmpty(purchaseData.purchaseTime);
                bool knownPaid = !string.IsNullOrEmpty(purchaseData.purchaseToken)
                                 && PendingOrderStore.Get(orderId) != null;

                if (!isNonConsumable)
                {
                    // ① / ② 平台侧已交付：绝不补发（防止清档/重装后历史消耗型订单被批量重发）
                    if (platformDelivered)
                    {
                        if (!knownPaid)
                        {
                            // ★ 仅在「正在核单」且正是本轮要买的那个商品时，才尝试把它核销掉：
                            //   这是唯一可能"平台说已交付、却仍挡住同商品再次购买"的场景（卡单死锁）。
                            //   平时的启动补单（queryType=ALL 会返回全部历史已交付订单）绝不逐单核销，
                            //   否则每启动一次就把历史订单全调一遍 finishPurchase → 触发 1001860004 调用过频。
                            bool couldBlockCurrentPurchase = _reconciling
                                && !string.IsNullOrEmpty(_pendingProductId)
                                && purchaseData.productId == _pendingProductId;

                            if (couldBlockCurrentPurchase)
                            {
                                Debug.LogWarning("[IAP] 核单遇到平台侧已交付的同商品订单，尝试核销以解除阻塞（不补发）: "
                                                 + "productId=" + purchaseData.productId
                                                 + " orderId=" + orderId);
                                ConsumePendingOrder(new PendingOrder
                                {
                                    orderId       = orderId,
                                    productId     = purchaseData.productId,
                                    price         = purchaseData.price.ToString(),
                                    currency      = purchaseData.currency,
                                    productType   = (int)purchaseData.productType,
                                    purchaseToken = purchaseData.purchaseToken,
                                    granted       = false,
                                });
                                continue;
                            }

                            Debug.Log("[IAP] 跳过平台侧已交付的消耗型订单（历史单/已在别处发过货）: "
                                      + "productId=" + purchaseData.productId
                                      + " orderId=" + orderId
                                      + " purchaseTime=" + purchaseData.purchaseTime);
                            continue;
                        }

                        // ② 本地还留着 pending：说明成功回调到过但没核销完。
                        //   先补一次发奖（内部按 granted 幂等），再只做核销收尾 —— 不再调 finishPurchase 之前那套重复发货流程。
                        var deliveredOrder = new PendingOrder
                        {
                            orderId       = orderId,
                            productId     = purchaseData.productId,
                            price         = purchaseData.price.ToString(),
                            currency      = purchaseData.currency,
                            productType   = (int)purchaseData.productType,
                            purchaseToken = purchaseData.purchaseToken,
                            granted       = false,
                        };
                        Debug.LogWarning("[IAP] 订单平台侧已交付且本地仍有待处理记录，先补发再核销（不重复发货）: "
                                         + "orderId=" + orderId
                                         + " granted=" + PendingOrderStore.IsGranted(orderId));
                        GrantRewardIfNeeded(deliveredOrder, ignoreGrantedFlag: false, deliverToUi: false);

                        // 发奖没成功（例如 ShopManager 还没就绪）就不核销：
                        // 一旦核销，平台侧就没有这笔单了，奖励再也补不回来。
                        if (!PendingOrderStore.IsGranted(orderId))
                        {
                            Debug.LogWarning("[IAP] 已交付订单补发未成功，暂不核销，留待下次: " + orderId);
                            continue;
                        }

                        ConsumePendingOrder(deliveredOrder);
                        continue;
                    }

                    // ③ 未交付：有 purchaseTime / 本地有成功回调记录 / 平台明确标"未交付" → 认为钱付了
                    //   （finishStatus=="2" 本身就是"已下单、未发货"，保持与旧行为一致；
                    //     purchaseTime 为空 = 订单未完成，官方字段口径）
                    bool hasPaidEvidence = hasPurchaseTime || knownPaid || purchaseData.finishStatus == "2";
                    if (!hasPaidEvidence)
                    {
                        // 订单根本没下成功（purchaseTime 为空）或字段全空：
                        // 既不发货（防止白送），也不核销（防止把玩家已付的订单核销掉却丢了货）。
                        Debug.LogWarning("[IAP][待核对] 消耗型订单未完成支付，本次不发货也不核销。"
                                         + " finishStatus=" + purchaseData.finishStatus
                                         + " purchaseTime=" + purchaseData.purchaseTime
                                         + " productId=" + purchaseData.productId
                                         + " orderId=" + orderId
                                         + " token=" + purchaseData.purchaseToken);
                        continue;
                    }

                    if (knownPaid)
                    {
                        Debug.LogWarning("[IAP] 该订单本地有成功回调记录（finishStatus=" + purchaseData.finishStatus
                                         + " purchaseTime=" + purchaseData.purchaseTime
                                         + "），按已支付处理并补发 + 核销: " + orderId);
                    }
                }

                var pending = new PendingOrder
                {
                    orderId       = orderId,
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
                    order_id        = orderId,
                    IsoCurrencyCode = purchaseData.currency,
                    ItemName        = purchaseData.productId,
                    ProductId       = purchaseData.productId,
                    LocalizedPrice  = purchaseData.price,
                };

                // 先发奖（幂等，内部按购买记录 orderId 去重）-> 再收尾（消耗型核销 / 非消耗型清记录）
                // 非消耗型：不受 pending 的 granted 标记限制，交给业务层按「购买记录」判重
                //（老版本可能已经标记过 granted 但奖励丢了，这里要能自愈，bug51）
                bool isCurrentRoundProduct = _purchaseInProgress &&
                    (string.IsNullOrEmpty(_pendingProductId) || purchaseData.productId == _pendingProductId);

                GrantRewardIfNeeded(pending, ignoreGrantedFlag: isNonConsumable,
                                    deliverToUi: isCurrentRoundProduct);
                FinishOrderAfterGrant(pending);

                // ★ 核单期间查到了本轮这笔订单：说明钱付了、只是回调丢了，
                //   按成功收口（界面走的是正常成功回调，不弹失败）
                if (_reconciling && isCurrentRoundProduct)
                {
                    Debug.Log("[IAP] 核单命中本轮订单，按支付成功收口: " + orderId);
                    ClosePurchaseSessionAsSuccess("reconcile_query_hit");
                }
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
        private void GrantRewardIfNeeded(PendingOrder order, bool ignoreGrantedFlag = false, bool deliverToUi = true)
        {
            if (order == null) return;

            // ★ 订单号为空绝不放奖：空 key 无法幂等判重，会变成「每来一次回调就发一次」
            if (string.IsNullOrEmpty(order.orderId))
            {
                Debug.LogError("[IAP] 订单号为空，跳过发奖（防止重复发放）");
                return;
            }

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
                // 恢复购买 / 启动补单 / 迟到回调一律走业务层的统一补发：
                // 这时候 buySuccessAction 可能是上一次购买残留的界面回调（界面早关了），
                // 直接调它会把奖励发到错误的界面、而且漏掉体力/去广告/购买记录。
                bool useRestoreDelivery = !deliverToUi || buySuccessAction == null || _restoreInProgress;

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