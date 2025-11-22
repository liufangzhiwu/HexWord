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

    public class ProductItem
    {
        public string ProductId;
        public string IsoCurrencyCode;
        public float LocalizedPrice;
    }
}