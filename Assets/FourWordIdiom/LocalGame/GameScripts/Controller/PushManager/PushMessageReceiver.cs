using UnityEngine;
using Middleware;

/// <summary>
/// 挂在场景中一个常驻 GameObject 上
/// GameObject 名字必须为 "PushMessageReceiver"
/// 供 ArkTS 侧通过 TuanjieSendMessage 调用
/// </summary>
public class PushMessageReceiver : MonoBehaviour
{
    public static PushMessageReceiver _instance;

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
        // 注意：Pop_up / Pop_accept / Pop_refuse("消息推送") 已经在 Push_harmony 内部上报
        //      （RequestEnableNotification 上报 pop_up，HandleSettingsClosedFromJS 上报 accept/refuse），
        //      这里再报一次会出现"一次授权两个 pop_up"的重复数据，故移除。
        Game.self.Pushs.HandleSettingsClosedFromJS(enabled == 1);
    }

    // ============================================================
    // ★ 由 ArkTS 侧 TuanjieSendMessage 调用
    //   ArkTS 侧调用：
    //     Tuanjie.TuanjieSendMessage('PushMessageReceiver',
    //                                'OnOAIDClosedSignal',
    //                                '1' or '0')
    //
    //   注意：
    //   1. 方法必须是 public
    //   2. 参数必须是 string（SendMessage 只支持字符串）
    //   3. 方法名要和 ArkTS 侧传的第二个参数完全一致
    //
    //   ★ 语义：广告追踪权限是否同意的回调
    //     enabled == 1 → 玩家同意广告追踪权限
    //     enabled == 0 → 玩家拒绝广告追踪权限
    //   这一步是用户分层的关键输入之一（IAA 层 vs IAP 层细分）。
    // ============================================================
    public void OnOAIDClosedSignal(string enabledStr)
    {
        int enabled = 0;
        int.TryParse(enabledStr, out enabled);
        bool granted = enabled == 1;
        Debug.Log($"[PushMessageReceiver] OnOAIDClosedSignal 收到, enabledStr={enabledStr}, granted={granted}");

        // 广告追踪的授权状态写入 / 分层刷新 / 埋点 / 激励发放，统一由 AdTrackingManager 编排，
        // 避免和"引导页 → 系统弹窗"的流程各写一份导致状态/埋点重复。
        AdTrackingManager manager = AdTrackingManager.Instance;
        if (manager != null)
        {
            manager.OnPermissionResult(granted);
            return;
        }

        // 兜底：管理器尚未创建（例如登录补写授权状态的极早期调用）
        var userData = GameDataManager.Instance != null ? GameDataManager.Instance.UserData : null;
        if (userData == null) return;

        userData.adTrackingEnabled = granted ? "1" : "0";
        if (ShopManager.shopManager != null)
        {
            string curLayer = ShopManager.shopManager.GetCurrentUserLayer();
            ShopManager.shopManager.RefreshLayer();
            Debug.Log($"[PushMessageReceiver] 广告追踪状态写入（兜底路径）：granted={granted}，分层={curLayer}");
        }
    }
}