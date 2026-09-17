using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Middleware;
using Xiaomi.GameSDK;

namespace Middleware
{
    public class Shop_xiaomi : IShop
    {
        public static Shop_xiaomi Instance { get; private set; }

        private bool _isInit;

        private Action<ProductItem> _successAction;
        private Action<string>      _failedAction;

        private string _currentCpOrderId;
        private string _currentProductId;

        // ========== 补单相关 ==========
        private List<PendingOrder> _pendingOrders = new List<PendingOrder>();
        private const string PendingOrderFile = "xiaomi_pending_orders.json";

        // UnitySendMessage 的目标 GameObject 名称（与 Java 端保持一致）
        private const string UnityHandlerObjectName = "XiaomiPayHandler";

        [Serializable]
        private class PendingOrder
        {
            public string CpOrderId;
            public string ProductId;
            public long   Timestamp;
        }

        [Serializable]
        private class Wrapper
        {
            public List<PendingOrder> Orders;
        }

        // ---------------------------------------------------------------------
        // IShop 接口
        // ---------------------------------------------------------------------

        public void Init(float delay)
        {
            Instance = this;

            // ★ 创建 Unity 端的回调接收 GameObject（与 Java 端 UnitySendMessage 目标名一致）
            EnsureCallbackReceiver();

            UnityTimer.Delay(delay, () =>
            {
                try
                {
                    _isInit = true;

                    // 启动时加载待补单列表并尝试补单
                    //LoadPendingOrders();
                    //ProcessPendingOrders();
                }
                catch (Exception e)
                {
                    Debug.LogError("[Shop_xiaomi] Init failed: " + e);
                }
            });
        }

        public bool IsProductOk(string productId) => true;

        public void Purchase(string productId, Action<ProductItem> successAction, Action<string> failedAction)
        {
            _successAction = successAction;
            _failedAction  = failedAction;

            _currentProductId = productId;
            _currentCpOrderId = GenerateOrderId();

            // ★★★ 关键改动：在调用支付之前先落盘，防止支付中途进程被杀导致订单丢失 ★★★
            AddPendingOrder(_currentCpOrderId, _currentProductId);

            try
            {
                SDKAndroid.Instance.OnProduceCodePay(
                    productId, 1, _currentCpOrderId, string.Empty,
                    new MiPayCallback(this));
            }
            catch (Exception e)
            {
                Debug.LogError("[Shop_xiaomi] Purchase exception: " + e);
                // 调用支付接口抛异常，说明订单没发起，直接移除
                RemovePendingOrder(_currentCpOrderId);
                InvokeFailed(e.Message);
            }
        }

        public void Restore(Action<bool, ProductItem[]> restoreCallback)
        {
            // 补单结果通过 _successAction 回传，Restore 回调只表示“补单流程已启动”
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                Debug.Log("[Shop_xiaomi] Restore triggered.");
            });

            LoadPendingOrders();
            ProcessPendingOrders();
        }

        // ---------------------------------------------------------------------
        // 补单核心逻辑
        // ---------------------------------------------------------------------

        private void AddPendingOrder(string cpOrderId, string productId)
        {
            _pendingOrders.Add(new PendingOrder
            {
                CpOrderId = cpOrderId,
                ProductId = productId,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            SavePendingOrders();
            Debug.Log("[Shop_xiaomi] Add _pendingOrders triggered."+cpOrderId+" 商品ID"+productId);
        }

        private void RemovePendingOrder(string cpOrderId)
        {
            _pendingOrders.RemoveAll(o => o.CpOrderId == cpOrderId);
            SavePendingOrders();
        }

        private void ProcessPendingOrders()
        {
            if (_pendingOrders.Count == 0)
            {
                Debug.Log("[Shop_xiaomi] No pending orders to restore.");
                return;
            }

            Debug.Log($"[Shop_xiaomi] Processing {_pendingOrders.Count} pending order(s)...");

            var ordersToCheck = new List<PendingOrder>(_pendingOrders);
            foreach (var order in ordersToCheck)
            {
                QueryOrderStatus(order);
            }
        }

        private void QueryOrderStatus(PendingOrder order)
        {
            try
            {
                using (AndroidJavaClass bridgeClass = new AndroidJavaClass(
                    "com.liufangzhiwu.chengyuxiao.mimo.MimoBridge"))
                {
                    AndroidJavaObject bridge = bridgeClass.CallStatic<AndroidJavaObject>("getInstance");
                    bridge.Call("queryOrderStatus", order.CpOrderId);
                    Debug.Log($"[Shop_xiaomi] QueryOrderStatus via MimoBridge: {order.CpOrderId}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Shop_xiaomi] QueryOrderStatus via MimoBridge exception: {e}");
            }
        }

        /// <summary>
        /// Android 层查询订单成功回调（由 XiaomiPayReceiver 转发）
        /// </summary>
        public void OnQueryOrderResult(string cpOrderId, string status)
        {
            Debug.Log($"[Shop_xiaomi] Order {cpOrderId} status: {status}");

            switch (status)
            {
                case "TRADE_SUCCESS":
                    // 支付成功，执行补发
                    RestoreShipment(cpOrderId);
                    break;

                case "WAIT_BUYER_PAY":
                    // ★ 未付款：用户点了支付但没完成。
                    RemovePendingOrder(cpOrderId);
                    Debug.Log($"[Shop_xiaomi] Order {cpOrderId}, removed it.");
                    break;

                case "TRADE_CLOSED":
                case "TRADE_FAIL":
                case "TRADE_TIMEOUT":
                case "REPEAT_PURCHASE":
                    // 终态：不可能再成功，移除
                    RemovePendingOrder(cpOrderId);
                    Debug.Log($"[Shop_xiaomi] Order {cpOrderId} is terminal ({status}), removed.");
                    break;

                default:
                    // 未知状态，保守保留，下次再查
                    Debug.LogWarning($"[Shop_xiaomi] Order {cpOrderId} unknown status: {status}, keep it.");
                    break;
            }
        }

        /// <summary>
        /// Android 层查询订单失败回调（由 XiaomiPayReceiver 转发）
        /// </summary>
        public void OnQueryOrderError(string cpOrderId, int code)
        {
            Debug.LogWarning($"[Shop_xiaomi] Query order {cpOrderId} failed, code={code}");
            // 查询失败不删除订单，下次启动时重试
        }

        private void RestoreShipment(string cpOrderId)
        {
            var pending = _pendingOrders.Find(o => o.CpOrderId == cpOrderId);
            if (pending == null) return;

            var item = new ProductItem
            {
                order_id        = cpOrderId,
                ProductId       = pending.ProductId,
                ItemName        = pending.ProductId,
                IsoCurrencyCode = string.Empty,
                LocalizedPrice  = 0f,

                OnShipmentCompleted = (bool ok) =>
                {
                    try
                    {
                        SDKAndroid.Instance.OnReportOrder(
                            cpOrderId, ok, ok ? null : "restore shipment failed");
                        Debug.Log($"[Shop_xiaomi] miReportOrder (restore), delivery={ok}");

                        if (ok)
                            RemovePendingOrder(cpOrderId);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[Shop_xiaomi] OnReportOrder (restore) error: {e}");
                    }
                }
            };

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                Debug.Log($"[Shop_xiaomi] Restoring shipment for order {cpOrderId}");
                ShopManager.shopManager.OnPurchaseSuccess(item);
            });
        }

        // ---------------------------------------------------------------------
        // 持久化
        // ---------------------------------------------------------------------

        private void SavePendingOrders()
        {
            try
            {
                string path = Path.Combine(Application.persistentDataPath, PendingOrderFile);
                string json = JsonUtility.ToJson(new Wrapper { Orders = _pendingOrders });
                File.WriteAllText(path, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Shop_xiaomi] SavePendingOrders error: {e}");
            }
        }

        private void LoadPendingOrders()
        {
            try
            {
                string path = Path.Combine(Application.persistentDataPath, PendingOrderFile);
                if (!File.Exists(path)) return;

                string json = File.ReadAllText(path);
                var wrapper = JsonUtility.FromJson<Wrapper>(json);
                if (wrapper?.Orders != null)
                {
                    _pendingOrders = wrapper.Orders;
                    Debug.Log($"[Shop_xiaomi] Loaded {_pendingOrders.Count} pending order(s).");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Shop_xiaomi] LoadPendingOrders error: {e}");
            }
        }

        // ---------------------------------------------------------------------
        // 支付完成回调
        // ---------------------------------------------------------------------

        internal void OnPayFinish(int code)
        {
            if (code == 0)
            {
                Debug.Log("[Shop_xiaomi] pay succeed, orderId=" + _currentCpOrderId);

                int price = (int)ShopManager.shopManager.GetProduct(_currentProductId).price*100;

                // ★ 订单已在 Purchase 时落盘，这里不再 AddPendingOrder，只负责发货
                var item = new ProductItem
                {
                    order_id        = _currentCpOrderId,
                    ProductId       = _currentProductId,
                    ItemName        = _currentProductId,
                    IsoCurrencyCode = string.Empty,
                    LocalizedPrice  = price,

                    OnShipmentCompleted = (bool ok) =>
                    {
                        try
                        {
                            SDKAndroid.Instance.OnReportOrder(
                                _currentCpOrderId, ok, ok ? null : "shipment failed");
                            Debug.Log("[Shop_xiaomi] miReportOrder, delivery=" + ok);

                            // ★ 发货成功后移除；发货失败保留，下次启动补单
                            if (ok)
                                RemovePendingOrder(_currentCpOrderId);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError("[Shop_xiaomi] OnReportOrder error: " + e);
                        }
                    }
                };

                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    _successAction?.Invoke(item);
                });
            }
            else
            {
                // ★ 支付失败/取消，订单不会成功，从待补单列表移除
                Debug.LogWarning("[Shop_xiaomi] pay failed, code=" + code);
                RemovePendingOrder(_currentCpOrderId);
                InvokeFailed(code.ToString());
            }
        }

        // ---------------------------------------------------------------------
        // 辅助
        // ---------------------------------------------------------------------

        private void InvokeFailed(string reason)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                _failedAction?.Invoke(reason);
            });
        }

        private static string GenerateOrderId()
        {
            return DateTime.Now.ToString("yyyyMMddHHmmssfff")
                   + UnityEngine.Random.Range(1000, 9999);
        }

        // ---------------------------------------------------------------------
        // UnitySendMessage 接收器（内部自动创建，无需手动挂载）
        // ---------------------------------------------------------------------

        private static bool _receiverCreated = false;

        /// <summary>
        /// 创建一个隐藏的 GameObject 作为 UnitySendMessage 目标
        /// </summary>
        private void EnsureCallbackReceiver()
        {
            if (_receiverCreated) return;

            var go = new GameObject(UnityHandlerObjectName);
            go.AddComponent<XiaomiPayReceiver>();
            UnityEngine.Object.DontDestroyOnLoad(go);

            _receiverCreated = true;
            Debug.Log($"[Shop_xiaomi] Created callback receiver GameObject: {UnityHandlerObjectName}");
        }

        /// <summary>
        /// UnitySendMessage 回调接收器（方法名与 Java 端严格对应）
        /// </summary>
        public class XiaomiPayReceiver : MonoBehaviour
        {
            /// <summary>
            /// Android 层查询订单成功回调
            /// 消息格式：cpOrderId|status
            /// </summary>
            public void OnQueryOrderResult(string message)
            {
                Debug.Log($"[XiaomiPayReceiver] OnQueryOrderResult: {message}");
                string[] parts = message.Split('|');
                if (parts.Length != 2)
                {
                    Debug.LogError($"[XiaomiPayReceiver] Invalid message format: {message}");
                    return;
                }
                Instance?.OnQueryOrderResult(parts[0], parts[1]);
            }

            /// <summary>
            /// Android 层查询订单失败回调
            /// 消息格式：cpOrderId|errorCode
            /// </summary>
            public void OnQueryOrderError(string message)
            {
                Debug.LogWarning($"[XiaomiPayReceiver] OnQueryOrderError: {message}");
                string[] parts = message.Split('|');
                if (parts.Length != 2)
                {
                    Debug.LogError($"[XiaomiPayReceiver] Invalid message format: {message}");
                    return;
                }
                if (int.TryParse(parts[1], out int code))
                {
                    Instance?.OnQueryOrderError(parts[0], code);
                }
            }
        }

        // ---------------------------------------------------------------------
        // 回调桥接
        // ---------------------------------------------------------------------

        private class MiPayCallback : IMiSDKPayCallback
        {
            private readonly Shop_xiaomi _owner;
            public MiPayCallback(Shop_xiaomi owner) { _owner = owner; }
            public void FinishPayProcess(int code) { _owner.OnPayFinish(code); }
        }
    }
}