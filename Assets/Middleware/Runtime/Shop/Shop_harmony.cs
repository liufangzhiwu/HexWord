#if UNITY_OPENHARMONY
using System;
using UnityEngine;
using UnityEngine.UI;
using OpenHarmonyKits.Signal;
using System.Collections.Generic;
using OpenHarmonyKits.Param;
using System.Linq;
using UnityEngine.EventSystems;

namespace Middleware
{
    public class Shop_harmony : IShop
    {
        
        List<PurchaseOrderPayload> purchaseDataList = new List<PurchaseOrderPayload>();//items that haven't finish purchase
        private FinishPurchaseParameter purchaseParam = null;//receive the info of item you want to finish purchase.Has to be private ,if public it won't be null
        
        private string purchaseResult = "";
        
        ProductData productData=new ProductData();
        //consumable products lists
        string[] m_storeIDList = { };
        //nonconsumable products lists
        string[] m_storeNonconsumeIDList = { };
        //AutoRenewable produccts List
        string[] m_storeAutoRenewableIDList = { };

        public Action<ProductItem> buySuccessAction;
        /// <summary>
        /// 恢复购买成功事件
        /// </summary>
        //public Action<ProductItem> RestoreSuccessAction;
        Action<string> buyFailedAction;
        
        ProductItem productItem = new ProductItem();
        
        bool InitSucceed = false;
        
        public void Init(float delay)
        {
            VerifyPayEnv();//init on start
            
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
                    id = shopDataItem.GetProduceName(),
                    type = (ProductType)shopDataItem.purchaseType,
                });
            }
          
            if (productData.products.Count > 0)
            {
                List<string> consumableList = new List<string>();
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
                m_storeIDList = consumableList.ToArray();
                m_storeNonconsumeIDList = nonConsumableList.ToArray();
                m_storeAutoRenewableIDList = autoRenewableList.ToArray();
            }
            else
            {
                Debug.LogError("ProductData asset not found!");
            }
        }

        public bool IsProductOk(string productId)
        {
            Product product= productData.products.Find(x => x.id == productId);
            if (product != null &&InitSucceed)
            {
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 开始购买商品
        /// </summary>
        /// <param name="selectedId"></param>
        /// <param name="selectedProductType"></param>
        public void Purchase(string productId, Action<ProductItem> successAction,Action<string> failedAction)
        {
            buyFailedAction = failedAction;
            buySuccessAction = successAction;
            
            PurchaseParameter purchaseParameter = new PurchaseParameter();
            Product product= productData.products.Find(x => x.id == productId);
            purchaseParameter.productId = product.id;
            purchaseParameter.productType = product.type;
            OHSDKKitManager.Instance.StartPurchase(purchaseParameter);
        }

        void IShop.Restore(Action<bool, ProductItem[]> restoreCallback)
        {
            Restore(restoreCallback);
        }

        public void Restore(Action<bool, ProductItem[]> restoreCallback)
        {
            restoreCallback?.Invoke(false, null);
        }
        
        
         private void OnIAPInitTrigger(SignalBase signal)
        {
            if (!signal.hasError())
            {
                IAP_InitSignal targetSignal = (IAP_InitSignal)signal;
                Debug.Log("[IAPInit Success]" + "\n" + targetSignal.successMessage);
                //QuerySubscription();
                QueryConsumable();
                //QueryUnconsumable();
                InitSucceed=true;
                ConfirmCheckPurchase();
            }
            else
            {
                Debug.Log("[IAPInit Error ] " + "\n "
                   + "Code : " + signal.code + " \n Message : " + signal.message + "\n");
            }
        }

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

    public void OnStartPurchaseTrigger(SignalBase signal)
    {
        if (!signal.hasError())
        {
            IAP_PurchaseSignal targetSignal = (IAP_PurchaseSignal)signal;

            purchaseResult = "ProductToken " + targetSignal.purchaseOrderPayload.purchaseToken + "\n"
                 + "Product Order Id " + targetSignal.purchaseOrderPayload.purchaseOrderId + "\n";

            purchaseParam = new FinishPurchaseParameter();

            purchaseParam.productType = (ProductType)targetSignal.purchaseOrderPayload.productType;

            purchaseParam.purchaseToken = targetSignal.purchaseOrderPayload.purchaseToken;

            purchaseParam.purchaseOrderId = targetSignal.purchaseOrderPayload.purchaseOrderId;

            Debug.Log("the purchaseResult" + purchaseResult);
            Debug.Log("[StartPurchase Success]" + "\n" + purchaseResult);
            
           purchaseDataList.Add(targetSignal.purchaseOrderPayload);
            
           productItem = new ProductItem
            {
                order_id=targetSignal.purchaseOrderPayload.purchaseOrderId,
                IsoCurrencyCode = targetSignal.purchaseOrderPayload.currency,
                ItemName = targetSignal.purchaseOrderPayload.productId,
                ProductId = targetSignal.purchaseOrderPayload.productId,
                LocalizedPrice = targetSignal.purchaseOrderPayload.price,
            };
            
            ConfirmFinishPurchase();
        }
        else
        {
            Debug.Log("[StartPurchase Error ] " + "\n "
              + "Code : " + signal.code + " \n Message : " + signal.message + "\n");
            
            buyFailedAction?.Invoke(signal.message);
        }

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

    public void OnConsumePurchaseTrigger(SignalBase signal)
    {
        if (!signal.hasError())
        {
            IAP_ConsumePurchase target = (IAP_ConsumePurchase)signal;
            Debug.Log("[ConsumePurchase Success]" + "\n");
            Debug.Log("Consume Purchase Success. purchaseToken is" + target.purchaseToken + "purchase type" + target.productType + "\n");
            
            ShopManager.shopManager.OnPurchaseSuccess(productItem);
        }
        else
        {
            Debug.Log("[ConsumePurchase Error ] " + "\n "
             + "Code : " + signal.code + " \n Message : " + signal.message + "\n");
        }
    }

    public void OnCheckPurchaseInfo(SignalBase signal)
    {
        if (!signal.hasError())
        {
            IAP_QueryOwnedPurchasesSignal targetSignal = (IAP_QueryOwnedPurchasesSignal)signal;
            Debug.Log("[CheckPurchase Success]" + "\n");
            Debug.Log("Product Type: " + targetSignal.productType + "\n");
            Debug.Log("Query Type: " + targetSignal.queryType + "\n");
            
            if (targetSignal.purchaseDataArray != null)
            {
                
                Debug.Log($"products nums{targetSignal.purchaseDataArray.Length}");
                Debug.Log("Purchase Data List:\n");

                foreach (var purchaseData in targetSignal.purchaseDataArray)
                {
                    //if (purchaseData.finishStatus == "2")//still haven't finish purchase,add to the list for user to finish purchase
                    //{
                    purchaseDataList.Add(purchaseData);
                    Debug.Log("1 purchase unfinished add");
                    // }
                    
                    productItem = new ProductItem
                    {
                        order_id  = purchaseData.purchaseOrderId,
                        IsoCurrencyCode = purchaseData.currency,
                        ItemName = purchaseData.productId,
                        ProductId = purchaseData.productId,
                        LocalizedPrice = purchaseData.price,
                    };
                    
                    Debug.Log("purchaseToken: " + purchaseData.purchaseToken + "\n purchaseOrderId: " + purchaseData.purchaseOrderId + "\n");
                    ConfirmFinishPurchase();
                }
            }
            else
            {
                Debug.Log("No purchase data available.\n");
            }
        }
        else
        {
            Debug.Log("[CheckPurchase Error ] " + "\n "
             + "Code : " + signal.code + " \n Message : " + signal.message + "\n");
        }
           
    }

    private void OnDisable()
    {
        if (purchaseParam != null)// purchaseParam has default null value
        {
            OHSDKKitManager.Instance.ConsumePurchase(purchaseParam);
            purchaseParam = null;
        }
    }
    

    /// <summary>
    /// 开始购买订阅商品
    /// </summary>
    private void ConfirmSubscriptionSelection()
    {
    // // get the only selected Toggle
    // Toggle selectedToggle = createSubscriptionToggleGroup.ActiveToggles().FirstOrDefault();
    // if (selectedToggle)
    // {
    //     string selectedSubscriptionId = selectedToggle.GetComponentInChildren<Text>().text;
    //     PurchaseParameter purchaseParameter = new PurchaseParameter();
    //     purchaseParameter.productId = selectedSubscriptionId;
    //     purchaseParameter.productType = ProductType.AUTORENEWABLE;
    //     OHSDKKitManager.Instance.StartPurchase(purchaseParameter);
    // }
    // subscriptionPanel.SetActive(false);
    }


    private void ConfirmCheckPurchase()
    {
        QueryPurchasesParameter queryPurchasesParameter = new QueryPurchasesParameter();

        // if (checkConsumable.isOn) {
        //     queryPurchasesParameter.productType = ProductType.CONSUMABLE;
        // }
        // if(checkUnconsumable.isOn)
        // {
        //     queryPurchasesParameter.productType = ProductType.NONCONSUMABLE;
        // }
        // if(checkSubscription.isOn)
        // {
        //     queryPurchasesParameter.productType = ProductType.AUTORENEWABLE;
        // }
        
        queryPurchasesParameter.productType = ProductType.CONSUMABLE;
        queryPurchasesParameter.queryType = PurchaseQueryType.ALL;
        OHSDKKitManager.Instance.CheckPurchase(queryPurchasesParameter);
    }

    private void ConfirmFinishPurchase()
    {
        if (purchaseDataList.Count == 0)
        {
            Debug.Log("No items to finish purchase.Please click Create Purchase/Subscription first");
            return;
        }
        
        List<string> idsToFinish = new List<string>();
        foreach (var purchaseOrderPayload in purchaseDataList.ToArray())
        {
            if (purchaseOrderPayload != null)
            {
                idsToFinish.Add(purchaseOrderPayload.purchaseOrderId);
            }
        }

        foreach (string id in idsToFinish)
        {
            var selectedPurchaseData = purchaseDataList.Find(p => p.purchaseOrderId == id);
            if (selectedPurchaseData != null)
            {
                purchaseParam = new FinishPurchaseParameter
                {
                    productType = (ProductType)selectedPurchaseData.productType,
                    purchaseToken = selectedPurchaseData.purchaseToken,
                    purchaseOrderId = selectedPurchaseData.purchaseOrderId
                };

                Debug.Log("[FinishPurchase Success] purchaseToken" + purchaseParam.purchaseToken + " purchaseOrderId: " + purchaseParam.purchaseOrderId + "\n");
                OHSDKKitManager.Instance.ConsumePurchase(purchaseParam);
                // remove the finished item from the list
                purchaseDataList.Remove(selectedPurchaseData);
            }
        }
    }
        
    }
}
#endif