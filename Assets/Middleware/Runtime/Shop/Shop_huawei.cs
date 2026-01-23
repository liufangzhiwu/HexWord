#if UNITY_huawei
using System;
using HuaweiService;
using HuaweiService.IAP;
using UnityEngine;
using Exception = System.Exception;

namespace Middleware
{
    public class Shop_huawei: IShop
    {
        public List productInfoList;
        public ProductInfo info;

        private bool _isEnvReady;
        private Action<ProductItem> _successAction;
        private Action<string> _failedAction;
        public void Init(float delay)
        {
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
                if(returnCode == OrderStatusCode.ORDER_STATE_SUCCESS)
                    _isEnvReady = true;
                
                ObtainOwnedPurchases();
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
                                
                            }
                        }
                    }else if (status.getStatusCode() == OrderStatusCode.ORDER_ACCOUNT_AREA_NOT_SUPPORTED)
                    {
                        // 用户当前登录的华为帐号所在的服务地不在华为IAP支持结算的国家/地区中
                        UnityMainThreadDispatcher.Instance().Enqueue(() =>
                        {
                            MessageSystem.Instance.ShowTip("帐号所在的服务地不在华为IAP支持结算的国家/地区中");
                        });
                    }
                }
                else
                {
                    // 其他外部错误
                    Debug.LogError("exception msg is " + exception.ToString()); 
                }
            }));
        }
        /// <summary>
        /// 创建订单？
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
            }else if (type == "Non-Consumables")
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
            
            Debug.Log("购买返回requestcode"+requestcode);
            
            if (requestcode == 6666)
            {
                Activity activity = new UnityPlayerActivity();
                PurchaseResultInfo purchaseResultInfo =
                    Iap.getIapClient(activity).parsePurchaseResultInfoFromIntent(data);

                int purchaseCode = purchaseResultInfo.getReturnCode();
                
                Debug.Log("购买返回Code"+purchaseCode);
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
                        // 检查是否存在未发货商品， 要进行补单? 
                        ObtainOwnedPurchases();
                        break;
                    case OrderStatusCode.ORDER_STATE_SUCCESS:
                        string inAppPurchaseData = purchaseResultInfo.getInAppPurchaseData();
                        // string inAppDataSignature = purchaseResultInfo.getInAppDataSignature();
                        InAppPurchaseData  inAppPurchaseDataBean = new InAppPurchaseData(inAppPurchaseData);
                        
                        string token = inAppPurchaseDataBean.getPurchaseToken();
                        Debug.Log("通知发货订单"+token+"支付状态"+inAppPurchaseDataBean.getPurchaseState());
                        
                        if (inAppPurchaseDataBean.getPurchaseState() == 0)
                        {
                            ProductItem productItem = new ProductItem
                            {
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
        /// 消耗型商品的补单流程
        /// </summary>
        private void ObtainOwnedPurchases(int type = 0)
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
                    if (inAppPurchaseDataList.size() < 1) return;
                    
                    ConsumeOwnedPurchaseReq req = new ConsumeOwnedPurchaseReq();
                    string purchaseToken = "";
                    string inAppPurchaseDataStr  = HmsClassHelper.ConvertObject<InAppPurchaseData>(inAppPurchaseDataList.get(0)).ToString();
                    InAppPurchaseData inAppPurchaseDataBean = new InAppPurchaseData(inAppPurchaseDataStr);
                    purchaseToken = inAppPurchaseDataBean.getPurchaseToken();
                    req.setPurchaseToken(purchaseToken);
                    Task task2 = Iap.getIapClient(activity).consumeOwnedPurchase(req);
                    task2.addOnSuccessListener(new HuaweiOnsuccessListener<ConsumeOwnedPurchaseResult>(_res =>
                    {
                        if (_res?.getReturnCode() == 0)
                        {
                            Debug.Log("发货通知成功");
                        }
                    })).addOnFailureListener(new HuaweiOnFailureListener(ex =>
                    {
                        Debug.Log("通知华为已发货失败！");
                    }));
                }
            })).addOnFailureListener(new HuaweiOnFailureListener(exception =>
            {
                Debug.LogWarning("通知华为测完成交易失败！" + exception.ToString());
            }));

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

        public void Restore(Action<bool, ProductItem[]> restoreCallback)
        {
            throw new NotImplementedException();
        }
    }
}
#endif