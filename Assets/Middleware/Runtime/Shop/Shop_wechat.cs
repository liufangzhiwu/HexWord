using System;
using System.Collections.Generic;
using Middleware.Network.Model;
using UnityEngine;
using WeChatWASM;

// using UnityEngine.Purchasing;
// using UnityEngine.Purchasing.Extension;

namespace Middleware
{

    //public class Shop_android : IShop, IDetailedStoreListener
    public class Shop_wechat : IShop
    {
        private bool isInitialized = false;
        private List<ShopDataItem> shopItems;

        public void Init(float delay)
        {
            UnityTimer.Delay(delay, () =>
            {
                isInitialized = true;
            });
            
            Debug.Log("微信支付商店初始化完成");
        }

        public bool IsProductOk(string productId)
        {
            
            if (!isInitialized) return false;
            
            // 2. iOS 平台检测 (微信小游戏iOS端禁止虚拟支付)
            // 如果你的游戏包含iOS端，必须在这里拦截并返回 false，或者弹窗提示不支持
#if UNITY_IOS || UNITY_IPHONE
            return false; // 根据微信规范，iOS端通常需要隐藏支付入口
#endif
            return true;
        }

        public void Purchase(string productId, Action<ProductItem> successAction, Action<string> failedAction)
        {
            if (!IsProductOk(productId))
            {
                failedAction?.Invoke("商品无效或当前平台不支持");
                return;
            }

            APIGateway.Instance.StartCoroutine(APIGateway.Instance.PaymentApi.CreateOrder(
                productId, 
                1,
                productId,
                (orderResponse) =>
                {
                    // 步骤 2: 后端创建成功，调起微信支付
                    CallWeChatPay(orderResponse, productId,productId , 1, successAction, failedAction);
                },
                (error) =>
                {
                    failedAction?.Invoke("创建订单失败: " + error);
                }
            ));
        }
        /// <summary>
        /// 调起微信 SDK 支付
        /// </summary>
        private void CallWeChatPay(CreateOrderResponse orderData, string productId, string name, int price, Action<ProductItem> onSuccess, Action<string> onFail)
        {
            var paramsData = orderData.pay_params;

            // 构造 RequestPaymentOption
            RequestMidasPaymentGameItemOption option = new RequestMidasPaymentGameItemOption();
            option.signature = paramsData.nonceStr;
            option.paySig = paramsData.paySign;
            option.signData = paramsData.package;
            // option.timeStamp = paramsData.timeStamp;
            // option.nonceStr = paramsData.nonceStr;
            // option.package = paramsData.package;
            // option.signType = paramsData.signType;
            // option.paySign = paramsData.paySign;
            
            option.success = (res) =>
            {
                Debug.Log("微信支付成功回调: " + JsonUtility.ToJson(res));
                
                // 构造返回给上层的 ProductItem
                ProductItem item = new ProductItem();
                item.ProductId = productId;
                item.order_id = orderData.order_id; // 存入后端订单号
                item.ItemName = name;
                item.LocalizedPrice = price / 100.0f; // 分转元
                item.IsoCurrencyCode = "CNY";

                // 设置发货完成回调 (通常是前端再次请求后端查询订单状态，或者纯UI展示)
                item.OnShipmentCompleted = (isShipped) =>
                {
                    Debug.Log($"订单 {orderData.order_id} 处理完毕: {isShipped}");
                };

                // 注意：微信回调成功只代表支付动作完成，实际发货以服务器 notify_url 回调为准
                // 建议这里稍微延迟一下再刷新用户数据
                onSuccess?.Invoke(item);
            };

            option.fail = (res) =>
            {
                Debug.LogError("微信支付失败: " + res.errMsg);
                onFail?.Invoke(res.errMsg); // 可能是用户取消，或余额不足
            };

            // 调起
            WX.RequestMidasPaymentGameItem(option);
        }

        public void Restore(Action<bool, ProductItem[]> restoreCallback)
        {
            Debug.Log("正在刷新用户资产...");
            APIGateway.Instance.StartCoroutine(APIGateway.Instance.LoginApi.GetUserData((data) =>
            {
                if (data != null)
                {
                    // 刷新本地数据管理器
                    // GameDataManager.Instance.LoadFromCloud(data);
                    restoreCallback?.Invoke(true, null);
                }
                else
                {
                    restoreCallback?.Invoke(false, null);
                }
            }));
        }
    }
}