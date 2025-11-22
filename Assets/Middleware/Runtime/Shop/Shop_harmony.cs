#if UNITY_OPENHARMONY
using System;

namespace Middleware
{
    public class Shop_harmony : IShop
    {
        public void Init(float delay)
        {
            
        }

        public bool IsProductOk(string productId)
        {
            return false;
        }

        public void Purchase(string productId, Action<ProductItem> successAction, Action<string> failedAction)
        {
            failedAction?.Invoke("暂不支持鸿蒙平台支付");
        }
        

        public void Restore(Action<bool, ProductItem[]> restoreCallback)
        {
            restoreCallback?.Invoke(false, null);
        }
    }
}
#endif