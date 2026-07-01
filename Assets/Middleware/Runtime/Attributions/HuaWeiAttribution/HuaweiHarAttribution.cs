#if UNITY_OPENHARMONY
using System;
using UnityEngine;

namespace Middleware
{
    public class HuaweiHarAttribution : IAttribute
    {
        private static OpenHarmonyJSObject attributionBridge;
        private bool isInitialized = false;

        public void Init(float delay)
        {
            UnityTimer.Delay(delay, () =>
            {
                attributionBridge = new OpenHarmonyJSObject("AttributionBridge");
                if (attributionBridge == null)
                {
                    Debug.LogError("[HuaweiAttr] Failed to create OpenHarmonyJSObject");
                    return;
                }

                // 调用 ArkTS 侧的 init，内部会并行获取归因数据、OAID、Token
                attributionBridge.Call("init");
                isInitialized = true;
                Debug.Log("[HuaweiAttr] AttributionBridge initialized");
            });
        }


        /// <summary>
        /// 上报激活（actionType = 1）
        /// </summary>
        public void ReportConversion(string eventCode)
        {
            if (!CheckBridge()) return;
            long actionTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            attributionBridge.Call("reportConversion", eventCode,actionTime);
            Debug.Log($"[HuaweiAttr] ReportActivation called,actionTime={eventCode} actionTime={actionTime}");
        }

        /// <summary>
        /// 上报留存（actionType = 3）
        /// </summary>
        public void ReportRetention(long actionTime)
        {
            if (!CheckBridge()) return;
            attributionBridge.Call("reportRetention", actionTime);
            Debug.Log($"[HuaweiAttr] ReportRetention called, actionTime={actionTime}");
        }

        /// <summary>
        /// 上报付费（actionType = 4），amount 单位为分
        /// </summary>
        public void ReportPurchase(long actionTime, decimal amount, string currency = "CNY")
        {
            if (!CheckBridge()) return;
            // 金额转为元（保留两位小数），传 double 给 ArkTS
            double amountInYuan = (double)amount / 100.0;
            attributionBridge.Call("reportPurchase", actionTime, amountInYuan, currency ?? "CNY");
            Debug.Log($"[HuaweiAttr] ReportPurchase called, actionTime={actionTime}, amount={amountInYuan}, currency={currency}");
        }

        private bool CheckBridge()
        {
            if (!isInitialized || attributionBridge == null)
            {
                Debug.LogError("[HuaweiAttr] AttributionBridge not initialized");
                return false;
            }
            return true;
        }
    }
}
#endif