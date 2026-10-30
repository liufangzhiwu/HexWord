using System;
using UnityEngine;

/// <summary>
/// 鸿蒙 UI 线程分享信号接收器。
///
/// ArkTS 侧（ShareHelper.ts）通过
///   Tuanjie.TuanjieSendMessage("HarmonyShareReceiver", "OnShareSignal", signal)
/// 调用，等价于 GameObject.SendMessage，因此：
///   1. 场景里必须存在名为 "HarmonyShareReceiver" 的 GameObject（本类由 AdsFailScreen 首次分享时动态创建并常驻）；
///   2. 方法必须 public，且只接收一个 string 参数。
///
/// 信号：uiStarted / dismiss / showFail / copied / copyFail
/// </summary>
public class HarmonyShareSignalReceiver : MonoBehaviour
{
    /// <summary>UI 线程分享信号（在主线程触发）。</summary>
    public static event Action<string> SignalReceived;

    public void OnShareSignal(string signal)
    {
        Debug.Log($"[HarmonyShare] 收到 UI 信号: {signal}");
        SignalReceived?.Invoke(signal);
    }
}
