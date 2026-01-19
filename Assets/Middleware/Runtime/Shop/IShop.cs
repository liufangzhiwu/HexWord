using System;

namespace Middleware
{
    public interface IShop
    {
        void Init(float delay);
        bool IsProductOk(string productId);
        void Purchase(string productId, Action<ProductItem> successAction, Action<string> failedAction);
        void Restore(Action<bool, ProductItem[]> restoreCallback);
    }

    /// <summary>
    /// 通用商品项
    /// </summary>
    public class ProductItem
    {
        public string order_id;
        public string ProductId;
        public string ItemName;
        public string IsoCurrencyCode;
        public float LocalizedPrice;

        public Action<bool> OnShipmentCompleted;
    }
}