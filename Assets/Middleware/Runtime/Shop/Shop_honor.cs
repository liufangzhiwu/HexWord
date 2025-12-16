using System;
using UnityEngine;

namespace Middleware
{
    public class Shop_honor : IShop
    {
        private AndroidJavaObject _currentActivity;
        public void Init(float delay)
        {
            UnityTimer.Delay(delay + 1f, () =>
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    _currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    _currentActivity.Call("obtainOwnedPurchases");
                }
            });
        }
        public bool IsProductOk(string productId)
        {
            return true;
        }

        public void Purchase(string productId, Action<ProductItem> successAction, Action<string> failedAction)
        {
            _currentActivity.Call("orderWithPMS", productId);
        }

        public void Restore(Action<bool, ProductItem[]> restoreCallback)
        {
            
        }
    }
}