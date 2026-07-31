#if UNITY_ANDROID||UNITY_IOS
using UnityEngine;

namespace Middleware
{
    
    public class AndoridAttribution : IAttribute
    {

        //private static OpenHarmonyJSObject attributionBridge;
     
        public void Init(float delay)
        {
            
            UnityTimer.Delay(delay, () =>
            {
                // 注意：这里使用 OpenHarmonyJSObject，参数是类名
                // attributionBridge = new OpenHarmonyJSObject("AttributionBridge");
                // if (attributionBridge == null)
                // {
                //     Debug.LogError("Failed to create OpenHarmonyJSObject for AttributionBridge");
                // }

            });
            

        }

        public void ReportConversion(string eventCode)
        {

            // if (attributionBridge != null)
            // {
            //     // 调用对象上的方法，注意方法名与ArkTS中定义的一致（小写开头）
            //     attributionBridge.Call("reportConversion", eventCode);
            // }
            // else
            // {
            //     Debug.LogError("AttributionBridge is null, please check the registration.");
            // }
            
            Debug.Log($"安卓环境，模拟上报事件: {eventCode}");
        }

        public void ReportPurchase(long actionTime, decimal amount, string currency = "CNY")
        {
            throw new System.NotImplementedException();
        }

        public void ReportRetention(long actionTime)
        {
            throw new System.NotImplementedException();
        }
    }
}
#endif
