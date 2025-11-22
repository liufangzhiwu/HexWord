// #if UNITY_ANDROID
// using System;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.Purchasing;
// using UnityEngine.Purchasing.Extension;
//
// namespace Middleware
// {
//
//     public class Shop_android : IShop, IDetailedStoreListener
//     {
//         private List<ShopDataItem> shopItems;
//
//         public void Init(float delay)
//         {
//             UnityTimer.Delay(delay, () =>
//             {
//                 var module = StandardPurchasingModule.Instance();
//                 var builder = ConfigurationBuilder.Instance(module);
//                 foreach (var shopDataItem in ShopManager.shopManager.GetBuyShopItems())
//                 {
//                     builder.AddProduct(shopDataItem.GetProduceName(), (ProductType)shopDataItem.purchaseType);
//                 }
//
//                 Debug.Log($" Enter Init【Unity IAP】");
//                 UnityPurchasing.Initialize(this, builder);
//             });
//         }
//
//         public bool IsProductOk(string productId)
//         {
//             
//             if (!IsInitialized())
//             {
//                 Debug.LogError("Not initialized.");
//                 return false;
//             }
//
//             var product = _storeController.products.WithID(productId);
//             if (product == null || !product.availableToPurchase)
//             {
//                 Debug.LogError("Either is not found or is not available for purchase:" + productId);
//                 return false;
//             }
//
//             return true;
//         }
//
//         public void Purchase(string productId, Action<ProductItem> successAction, Action<string> failedAction)
//         {
//             if (!IsInitialized())
//             {
//                 Debug.Log("Please initialized first");
//                 _failedCallback?.Invoke("Not initialized.");
//                 return;
//             }
//
//             if (_isInPurchaseProgress)
//             {
//                 Debug.Log("Please wait, purchase in progress");
//                 _failedCallback?.Invoke("In progressing.");
//                 return;
//             }
//
//             _failedCallback = failedAction;
//             _successCallback = successAction;
//             var product = _storeController.products.WithID(productId);
//             if (product == null || !product.availableToPurchase)
//             {
//                 _failedCallback?.Invoke("Product not found or unavailable:" + productId);
//                 return;
//             }
//
//             Debug.Log($"【Unity IAP】开始购买: {product.metadata.localizedTitle}");
//             _isInPurchaseProgress = true;
//             _storeController.InitiatePurchase(product);
//         }
//
//
//         public void Restore(Action<bool, ProductItem[]> restoreCallback)
//         {
//             Debug.Log("开始iOS内购恢复...");
//             _appleExtension.RestoreTransactions((success, error) =>
//             {
//                 if (success)
//                 {
//                     Debug.Log("iOS恢复操作已完成");
//                     var restoredProducts = GetRestoredProducts();
//                     if (restoredProducts.Count > 0)
//                     {
//                         Debug.Log($"找到{restoredProducts.Count}个可恢复商品");
//                         restoreCallback?.Invoke(true, restoredProducts.ToArray());
//                     }
//                     else
//                     {
//                         Debug.Log("未找到可恢复的商品");
//                         restoreCallback?.Invoke(false, null);
//                     }
//                 }
//                 else
//                 {
//                     Debug.LogError($"恢复失败: {error}");
//                     restoreCallback?.Invoke(false, null);
//                 }
//             });
//         }
//
//         private List<ProductItem> GetRestoredProducts()
//         {
//             if (_storeController == null) return null;
//
//             var restoredProducts = new List<ProductItem>();
//             foreach (var product in _storeController.products.all)
//             {
//                 // 只检查非消耗品和订阅商品
//                 if (!product.hasReceipt || product.definition.type == ProductType.Consumable)
//                     continue;
//
//                 restoredProducts.Add(new ProductItem()
//                 {
//                     ProductId = product.definition.id,
//                     IsoCurrencyCode = product.metadata.isoCurrencyCode,
//                     LocalizedPrice = (float)product.metadata.localizedPrice
//                 });
//             }
//
//             return restoredProducts;
//         }
//
//
//         #region 基础逻辑
//
//         private IStoreController _storeController;
//         private IExtensionProvider _extensionProvider;
//         private IAppleExtensions _appleExtension;
//         private IGooglePlayStoreExtensions _googlePlayStoreExtensions;
//
//         private Action<string> _failedCallback;
//         private Action<ProductItem> _successCallback;
//         private bool _isInPurchaseProgress;
//         private const int RetryCount = 3; // 重试次数
//
//         private bool IsInitialized()
//         {
//             return _storeController != null && _extensionProvider != null;
//         }
//
//         private void RetryInitialize()
//         {
//             Debug.Log($"Unity IAP Initialization failed, wait 5 seconds and try again...number of retries");
//             Init(5f);
//         }
//
//
//         #endregion
//
//         #region IAP接口
//
//         public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs purchaseEvent)
//         {
//             var metadata = purchaseEvent.purchasedProduct.metadata;
//             Debug.Log($"OnPurchaseSuccess:{purchaseEvent.purchasedProduct.transactionID},{purchaseEvent.purchasedProduct.definition.id},{purchaseEvent.purchasedProduct.definition.storeSpecificId}," +
//                       $"{metadata.localizedTitle},{metadata.localizedDescription},{metadata.isoCurrencyCode},{metadata.localizedPrice}");
//             
//             var item = new ProductItem()
//             {
//                 ProductId = purchaseEvent.purchasedProduct.definition.id,
//                 IsoCurrencyCode = metadata.isoCurrencyCode,
//                 LocalizedPrice = (float)metadata.localizedPrice,
//             };
//             _successCallback?.Invoke(item);
//             _isInPurchaseProgress = false;
//             return PurchaseProcessingResult.Complete;
//         }
//
//         public void OnPurchaseFailed(Product product, PurchaseFailureReason f)
//         {
//             Debug.LogError("OnPurchaseFailed:" + product.transactionID + "  failureReason:" + f);
//             _isInPurchaseProgress = false;
//             _failedCallback?.Invoke(f.ToString());
//         }
//
//         public void OnPurchaseFailed(Product product, PurchaseFailureDescription f)
//         {
//             Debug.LogError("OnPurchaseFailed:" + product.transactionID + "  failureReason:" + f.reason);
//             _isInPurchaseProgress = false;
//             _failedCallback?.Invoke(f.message);
//         }
//
//         public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
//         {
//             Debug.Log("【Unity IAP】初始化成功 IAP initialize success");
//             _storeController = controller;
//             _extensionProvider = extensions;
//             _googlePlayStoreExtensions = extensions.GetExtension<IGooglePlayStoreExtensions>();
//             Debug.Log($"Load {_storeController.products.all.Length} Products");
//         }
//
//         public void OnInitializeFailed(InitializationFailureReason error)
//         {
//             Debug.LogError("Unity IAP 初始化失败" + error);
//             RetryInitialize();
//         }
//
//         public void OnInitializeFailed(InitializationFailureReason error, string message)
//         {
//             Debug.LogError("Unity IAP 初始化失败" + error);
//             RetryInitialize();
//         }
//
//         #endregion
//     }
// }
// #endif