using System;
using System.Collections;
using Middleware.Network.Model;
using Newtonsoft.Json;
using UnityEngine;

namespace Middleware.Network.Api
{
    public class PaymentApi
    {
        private HTTPClient httpClient;

        public PaymentApi(HTTPClient client)
        {
            httpClient = client;
        }
        
        class CreateOrderRequest
        {
            public int amount; // 单位：分
            public string desc; // 商品描述
            public string product_id; // 商品ID（后端新增的字段）
        }
        
        /// <summary>
        /// 请求后端创建订单，获取微信支付参数
        /// </summary>
        public IEnumerator CreateOrder(string productId, int priceInCents, string productName, Action<CreateOrderResponse> onSuccess, Action<string> onFail)
        {
            var reqData = new CreateOrderRequest
            {
                amount = priceInCents, // 注意：微信支付金额单位是分
                desc = productName,
                product_id = productId
            };

            Debug.Log($"[Payment] 创建订单: {JsonConvert.SerializeObject(reqData)}");

            // 对应 Laravel 路由: Route::post('payment/create', ...)
            yield return httpClient.Post<CreateOrderResponse>("payment/create", reqData,
                (response) =>
                {
                    onSuccess?.Invoke(response);
                },
                (error) =>
                {
                    onFail?.Invoke(error);
                });
        }
    }
}