#if UNITY_OPENHARMON
using UnityEngine;

namespace Middleware
{
    
    public class HuaweiHarAttribution : IAttribute
    {

        private static OpenHarmonyJSObject attributionBridge;
     
        public void Init(float delay)
        {
            UnityTimer.Delay(delay, () =>
            {
                // 注意：这里使用 OpenHarmonyJSObject，参数是类名
                attributionBridge = new OpenHarmonyJSObject("AttributionBridge");
                if (attributionBridge == null)
                {
                    Debug.LogError("Failed to create OpenHarmonyJSObject for AttributionBridge");
                }
            });
        }

        public void ReportConversion(int eventCode)
        {
            if (attributionBridge != null)
            {
                // 调用对象上的方法，注意方法名与ArkTS中定义的一致（小写开头）
                attributionBridge.Call("reportConversion", eventCode);
            }
            else
            {
                Debug.LogError("AttributionBridge is null, please check the registration.");
            }
        }
    }
}
#endif
