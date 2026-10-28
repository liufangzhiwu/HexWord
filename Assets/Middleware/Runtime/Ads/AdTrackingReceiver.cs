using UnityEngine;

namespace Middleware
{
    /// <summary>
    /// 广告追踪权限的原生回调接收器。
    /// iOS 侧 AdTrackingBridge.mm 通过 UnitySendMessage("AdTrackingReceiver", "OnTrackingResult", "0..3") 调用。
    /// （运行时由 AdTrackingPermission 自动创建，不需要挂场景）
    /// </summary>
    public class AdTrackingReceiver : MonoBehaviour
    {
        public void OnTrackingResult(string value)
        {
            AdTrackingPermission.HandleNativeResult(value);
        }
    }
}
