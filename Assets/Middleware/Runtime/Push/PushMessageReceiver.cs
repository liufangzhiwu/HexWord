using UnityEngine;
using Middleware;

/// <summary>
/// 挂在场景中一个常驻 GameObject 上
/// GameObject 名字必须为 "PushMessageReceiver"
/// 供 ArkTS 侧通过 TuanjieSendMessage 调用
/// </summary>
public class PushMessageReceiver : MonoBehaviour
{
    private static PushMessageReceiver _instance;

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[PushMessageReceiver] Awake, 实例已创建");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ============================================================
    // ★ 由 ArkTS 侧 TuanjieSendMessage 调用
    //   ArkTS 侧调用：
    //     Tuanjie.TuanjieSendMessage('PushMessageReceiver',
    //                                'OnSettingsClosedSignal',
    //                                enabled ? '1' : '0')
    //
    //   注意：
    //   1. 方法必须是 public
    //   2. 参数必须是 string（SendMessage 只支持字符串）
    //   3. 方法名要和 ArkTS 侧传的第二个参数完全一致
    // ============================================================
    public void OnSettingsClosedSignal(string enabledStr)
    {
        int enabled = 0;
        int.TryParse(enabledStr, out enabled);
        Debug.Log($"[PushMessageReceiver] OnSettingsClosedSignal 收到, enabledStr={enabledStr}, enabled={enabled}");

        // 转发给 Push_harmony 处理
        Game.self.Pushs.HandleSettingsClosedFromJS(enabled == 1);
    }
}