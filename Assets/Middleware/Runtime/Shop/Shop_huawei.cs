#if UNITY_HUAWEI
using System;
using HuaweiService;
using HuaweiService.IAP;
using UnityEngine;
using Exception = System.Exception;

namespace Middleware
{
    public class Shop_huawei : IShop
    {
        public List productInfoList;
        public ProductInfo info;

        private bool _isEnvReady;
        private Action<ProductItem> _successAction;
        private Action<string> _failedAction;

        public ShopDataItem CurrentShopDataItem { get; set; }

        public void Init(float delay)
        {
            if (UIUtilities.isEditMode) return;

            UnityTimer.Delay(delay, () =>
            {
                var callback = new IapCallback();
                callback.setCallback(OnActivityResultCallback);
                IapActivity.setCallback(callback);

                IsEnvReady();
            });
        }

        private void IsEnvReady()
        {
            Activity activity = new UnityPlayerActivity();
            Task task = Iap.getIapClient(activity).isEnvReady();
            task.addOnSuccessListener(new HuaweiOnsuccessListener<IsEnvReadyResult>(result =>
            {
                if (result.getCarrierId() == null && result.getCountry() == null)
                {
                    Debug.LogError("Non-AppTouch scenarios");
                }
                else
                {
                    Debug.LogError("AppTouch scenarios");
                }

                int flag = result.getAccountFlag();
                int returnCode = result.getReturnCode();
                if (returnCode == OrderStatusCode.ORDER_STATE_SUCCESS)
                    _isEnvReady = true;

                // 【修改】不再在此处自动补单，补单延迟到进入主页后由外部调用 Restore
                // 原有补单调用已被移除，外部可在适当时机（如主页加载完成）调用 Restore
                Debug.Log("华为IAP环境准备就绪，等待外部触发补单");
            })).addOnFailureListener(new HuaweiOnFailureListener(exception =>
            {
                IapApiException apiException = HmsClassHelper.ConvertObject<IapApiException>(exception.obj);
                if (apiException != null)
                {
                    Status status = apiException.getStatus();
                    if (status.getStatusCode() == OrderStatusCode.ORDER_HWID_NOT_LOGIN)
                    {
                        if (status.hasResolution())
                        {
                            try
                            {
                                status.startResolutionForResult(activity, 6666);
                            }
                            catch (System.Exception e)
                            {
                                // ignore
                            }
                        }
                    }
                    else if (status.getStatusCode() == OrderStatusCode.ORDER_ACCOUNT_AREA_NOT_SUPPORTED)
                    {
                        UnityMainThreadDispatcher.Instance().Enqueue(() =>
                        {
                            MessageSystem.Instance.ShowTip("帐号所在的服务地不在华为IAP支持结算的国家/地区中");
                        });
                    }
                }
                else
                {
                    Debug.LogError("exception msg is " + exception.ToString());
                }
            }));
        }

        /// <summary>
        /// 创建订单
        /// priceType: 0：消耗型商品; 1：非消耗型商品; 2：订阅型商品
        /// </summary>
        /// <param name="type">消耗类型</param>
        /// <param name="productId">商品id</param>
        public void CreatePurchaseIntent(string type, string productId)
        {
            if (type == "Consumables")
            {
                IapActivity.setIntent("Consumables");
                IapActivity.setPriceType(0);
            }
            else if (type == "Non-Consumables")
            {
                IapActivity.setIntent("Non-Consumables");
                IapActivity.setPriceType(1);
            }
            else
            {
                IapActivity.setIntent("Subscription");
                IapActivity.setPriceType(2);
            }
            IapActivity.setConProductId(productId);
            IapActivity.start(new UnityPlayerActivity());
        }

        // 安卓回调
        private void OnActivityResultCallback(int requestcode, int resultcode, AndroidJavaObject obj)
        {
            var data = new Intent { obj = obj };

            Debug.Log("购买返回requestcode" + requestcode);

            if (requestcode == 6666)
            {
                Activity activity = new UnityPlayerActivity();
                PurchaseResultInfo purchaseResultInfo =
                    Iap.getIapClient(activity).parsePurchaseResultInfoFromIntent(data);

                int purchaseCode = purchaseResultInfo.getReturnCode();

                Debug.Log("购买返回Code" + purchaseCode);
                switch (purchaseResultInfo.getReturnCode())
                {
                    case OrderStatusCode.ORDER_STATE_CANCEL:
                        UnityMainThreadDispatcher.Instance().Enqueue(() =>
                        {
                            _failedAction?.Invoke(resultcode.ToString());
                        });
                        Debug.LogWarning("支付取消!");
                        break;

                    case OrderStatusCode.ORDER_STATE_FAILED:
                    case OrderStatusCode.ORDER_STATE_DEFAULT_CODE:
                    case OrderStatusCode.ORDER_PRODUCT_OWNED:
                        // 【补单】购买失败时检查是否存在未发货商品，自动补单（先发奖后消费）
                        Restore((success, items) =>
                        {
                            // 补单完成后，通知购买失败（原逻辑）
                            UnityMainThreadDispatcher.Instance().Enqueue(() =>
                            {
                                _failedAction?.Invoke(resultcode.ToString());
                            });
                        }, onRestoreItem: DeliverRewardForProduct);
                        break;

                    case OrderStatusCode.ORDER_STATE_SUCCESS:
                        string inAppPurchaseData = purchaseResultInfo.getInAppPurchaseData();
                        // string inAppDataSignature = purchaseResultInfo.getInAppDataSignature();
                        InAppPurchaseData inAppPurchaseDataBean = new InAppPurchaseData(inAppPurchaseData);

                        string token = inAppPurchaseDataBean.getPurchaseToken();
                        Debug.Log("通知发货订单" + token + "支付状态" + inAppPurchaseDataBean.getPurchaseState());

                        if (inAppPurchaseDataBean.getPurchaseState() == 0)
                        {
                            // ---- 获取付费信息 ----
                            long priceInFen = inAppPurchaseDataBean.getPrice();    // 单位：分
                            string currency = inAppPurchaseDataBean.getCurrency(); // 如 "CNY"
                            long actionTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                            // ---- 调用华为归因付费回传 ----
                            UnityMainThreadDispatcher.Instance().Enqueue(() =>
                            {
                                Game.self?.Attributes?.ReportPurchase(actionTime, priceInFen, currency);
                            });

                            ProductItem productItem = new ProductItem
                            {
                                order_id = inAppPurchaseDataBean.getOrderID(),
                                ItemName = inAppPurchaseDataBean.getProductId(),
                                IsoCurrencyCode = inAppPurchaseDataBean.getCurrency(),
                                ProductId = inAppPurchaseDataBean.getProductId(),
                                LocalizedPrice = inAppPurchaseDataBean.getPrice(),

                                OnShipmentCompleted = (bool handle) =>
                                {
                                    ConsumeOwnedPurchaseReq req = new ConsumeOwnedPurchaseReq();
                                    req.setPurchaseToken(token);
                                    Task task2 = Iap.getIapClient(activity).consumeOwnedPurchase(req);
                                    task2.addOnSuccessListener(new HuaweiOnsuccessListener<ConsumeOwnedPurchaseResult>(res =>
                                    {
                                        if (res?.getReturnCode() == 0)
                                        {
                                            Debug.Log("发货通知成功");
                                        }
                                    })).addOnFailureListener(new HuaweiOnFailureListener(ex =>
                                    {
                                        Debug.Log("通知华为已发货失败！");
                                    }));
                                }
                            };
                            UnityMainThreadDispatcher.Instance().Enqueue(() =>
                            {
                                _successAction?.Invoke(productItem);
                            });
                        }
                        break;

                    default:
                        UnityMainThreadDispatcher.Instance().Enqueue(() =>
                        {
                            _failedAction?.Invoke(resultcode.ToString());
                        });
                        break;
                }
            }
            else
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    _failedAction?.Invoke(resultcode.ToString());
                });
            }
        }

        /// <summary>
        /// 消耗型商品的补单流程（查询未消耗商品并消费）
        /// </summary>
        /// <param name="type">价格类型，0=消耗型</param>
        /// <param name="callback">补单结果回调</param>
        /// <param name="onRestoreItem">每个补单商品发货前的奖励发放回调（可选）</param>
        private void ObtainOwnedPurchases(int type = 0, Action<bool, ProductItem[]> callback = null, Action<ProductItem> onRestoreItem = null)
        {
            OwnedPurchasesReq ownedPurchasesReq = new OwnedPurchasesReq();
            ownedPurchasesReq.setPriceType(type);
            Activity activity = new UnityPlayerActivity();
            Task task = Iap.getIapClient(activity).obtainOwnedPurchases(ownedPurchasesReq);
            task.addOnSuccessListener(new HuaweiOnsuccessListener<OwnedPurchasesResult>(result =>
            {
                if (result != null && result.getInAppPurchaseDataList() != null)
                {
                    List inAppPurchaseDataList = result.getInAppPurchaseDataList();
                    if (inAppPurchaseDataList.size() < 1)
                    {
                        callback?.Invoke(false, null);
                        return;
                    }

                    // 注意：此处只处理列表中的第一个商品，如需全部处理可遍历
                    string inAppPurchaseDataStr = HmsClassHelper.ConvertObject<InAppPurchaseData>(inAppPurchaseDataList.get(0)).ToString();
                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                        InAppPurchaseData inAppPurchaseDataBean = new InAppPurchaseData(inAppPurchaseDataStr);
                        ProductItem[] items = new ProductItem[]
                        {
                            new ProductItem
                            {
                                IsoCurrencyCode = inAppPurchaseDataBean.getCurrency(),
                                ProductId = inAppPurchaseDataBean.getProductId(),
                                LocalizedPrice = inAppPurchaseDataBean.getPrice(),
                                OnShipmentCompleted = (ok) =>
                                {
                                    string purchaseToken = inAppPurchaseDataBean.getPurchaseToken();
                                    ConsumeOwnedPurchaseReq req = new ConsumeOwnedPurchaseReq();
                                    req.setPurchaseToken(purchaseToken);
                                    Task task2 = Iap.getIapClient(activity).consumeOwnedPurchase(req);
                                    task2.addOnSuccessListener(new HuaweiOnsuccessListener<ConsumeOwnedPurchaseResult>(
                                        _res =>
                                        {
                                            if (_res?.getReturnCode() == 0)
                                            {
                                                Debug.Log("补单消费成功");
                                            }
                                        })).addOnFailureListener(new HuaweiOnFailureListener(ex =>
                                    {
                                        Debug.Log("补单消费失败！");
                                    }));
                                }
                            }
                        };

                        // 先执行奖励发放，再消费（由外部调用OnShipmentCompleted）
                        if (onRestoreItem != null)
                        {
                            foreach (var item in items)
                            {
                                onRestoreItem(item);
                            }
                        }
                        // 然后消费（通知华为已发货）
                        foreach (var item in items)
                        {
                            item.OnShipmentCompleted?.Invoke(true);
                        }

                        callback?.Invoke(true, items);
                    });
                }
                else
                {
                    callback?.Invoke(false, null);
                }
            })).addOnFailureListener(new HuaweiOnFailureListener(exception =>
            {
                Debug.LogWarning("查询未消耗商品失败！" + exception.ToString());
                callback?.Invoke(false, null);
            }));
        }

        /// <summary>
        /// 补单入口（外部调用），用于主动触发补单
        /// </summary>
        /// <param name="restoreCallback">补单完成回调</param>
        /// <param name="onRestoreItem">每个补单商品的奖励发放逻辑（由调用方传入，建议传入 DeliverRewardForProduct）</param>
        public void Restore(Action<bool, ProductItem[]> restoreCallback, Action<ProductItem> onRestoreItem = null)
        {
            // 可选：检查环境是否就绪，若未就绪可等待或直接调用，IAP 会处理
            ObtainOwnedPurchases(0, restoreCallback, onRestoreItem);
        }

        // 保留无参重载，调用带参版本（不传发奖委托，但一般不推荐，建议外部显式传入发奖）
        public void Restore(Action<bool, ProductItem[]> restoreCallback)
        {
            Restore(restoreCallback, DeliverRewardForProduct);
        }

        /// <summary>
        /// 根据商品ID发放奖励（与OnPurchaseSuccess逻辑保持一致，但避免重复统计）
        /// </summary>
        private void DeliverRewardForProduct(ProductItem item)
        {
            if (item == null) return;

            string productId = item.ProductId;
            // 从全局ShopManager获取商品配置
            var shopDataItem = ShopManager.shopManager.GetProduct(productId);
            if (shopDataItem == null)
            {
                Debug.LogWarning("补单：未找到商品配置 " + productId);
                return;
            }

            Game.self.Shop.CurrentShopDataItem = shopDataItem.DeepCopy();
            SystemManager.Instance.ShowPanel(PanelType.AwardScreen);

            // 发放奖励（与OnPurchaseSuccess一致，但不累加支付次数和统计）
            foreach (var dataitem in shopDataItem.productContent)
            {
                int count = int.Parse(dataitem[1]);
                int type = int.Parse(dataitem[0]);
                Debug.Log("奖励发放类型"+type+"奖励发放数值"+count);
                switch (type)
                {
                    case (int)LimitRewordType.Coins:
                        GameDataManager.Instance.UserData.UpdateGold(count, false, false, "补单发货 " + productId);
                        break;
                    case (int)LimitRewordType.Butterfly:
                        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Butterfly, count, "补单发货 " + productId);
                        break;
                    case (int)LimitRewordType.Tipstool:
                        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, count, "补单发货 " + productId);
                        break;
                    case (int)LimitRewordType.AutoComplete:
                        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.AutoComplete, count, "补单发货 " + productId);
                        break;
                    case (int)LimitRewordType.RemoveAds:
                    case (int)LimitRewordType.Remove7DayAds:
                        BuyRemoveAdsEvent(type);
                        break;
                }
            }

            Debug.Log("补单奖励发放完成: " + productId);
        }

        /// <summary>
        /// 处理去广告（示例）
        /// </summary>
        private void BuyRemoveAdsEvent(int type)
        {
            // 实现去广告逻辑
        }

        public bool IsProductOk(string productId)
        {
            if (!_isEnvReady)
            {
                IsEnvReady();
            }
            return _isEnvReady;
        }

        public void Purchase(string productId, Action<ProductItem> successAction, Action<string> failedAction)
        {
            _successAction = successAction;
            _failedAction = failedAction;
            CreatePurchaseIntent("Consumables", productId);
        }
    }
}
#endif