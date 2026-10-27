#if !UNITY_HUAWEI && !UNITY_OPENHARMONY
using UnityEngine;

namespace Middleware
{
    /// <summary>
    /// 未接入归因上报的平台（iOS、非华为安卓、编辑器等）的空实现。
    /// 保留原有接口调用不报错，避免各平台编译不过或运行时抛 NotImplementedException。
    /// </summary>
    public class EmptyAttribution : IAttribute
    {
        public void Init(float delay)
        {
            Debug.Log("[Attr] 当前平台未接入归因上报，使用空实现。");
        }

        public void ReportConversion(string eventCode)
        {
        }

        public void ReportPurchase(long actionTime, decimal amount, string currency = "CNY")
        {
        }

        public void ReportRetention(long actionTime)
        {
        }
    }
}
#endif
