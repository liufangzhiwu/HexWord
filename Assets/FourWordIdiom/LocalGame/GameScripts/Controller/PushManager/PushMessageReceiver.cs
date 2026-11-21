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
    // ★ 由 ArkTS 侧 TuanjieSendMessage 调用（页面重新获得焦点）
    //   ArkTS 侧：SDKManager.OnWindowFocusGained()
    //             → TuanjieSendMessage('PushMessageReceiver', 'OnWindowFocusGained', '1')
    //
    //   为什么需要：鸿蒙的支付收银台是模态 UIExtension，关闭后主窗口不一定再给
    //   onForeground / windowStage RESUMED，游戏既收不到「回到前台」，也可能收不到
    //   支付结果回调 —— 界面就会一直停在「等待支付」的 loading 上（表现为卡住）。
    //   页面 onFocus 是这种情况唯一稳定的信号，这里转给商店层收口。
    // ============================================================
    public void OnWindowFocusGained(string _)
    {
        Debug.Log("[PushMessageReceiver] OnWindowFocusGained 收到");
#if UNITY_OPENHARMONY && !UNITY_EDITOR
        if (Game.self != null && Game.self.Shop is Middleware.Shop_harmony shop)
        {
            shop.OnWindowFocusGained();
        }
#endif
    }

    // ============================================================
    // ★ 鸿蒙系统返回手势（返回键 / 全面屏侧滑）
    //   ArkTS 侧调用：
    //     entry/pages/Index.ets onBackPress()
    //       → SDKManager.NotifyBackGesture()
    //         → TuanjieSendMessage('PushMessageReceiver', 'OnBackGestureSignal', '1')
    //
    //   与安卓的 KeyCode.Escape 走同一个出口 BackGestureRouter.HandleBackGesture()，
    //   保证两端分流规则完全一致（路由内部做了去抖，两条通道同时到达也只处理一次）。
    // ============================================================
    public void OnBackGestureSignal(string _)
    {
        Debug.Log("[PushMessageReceiver] OnBackGestureSignal 收到（鸿蒙侧滑返回）");
        BackGestureRouter.HandleBackGesture();
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

    // ============================================================
    // ★ 广告追踪授权状态「静默查询」回包（bug34）
    //   ArkTS 侧（SendOAID.QueryTrackingConsent）调用：
    //     Tuanjie.TuanjieSendMessage('PushMessageReceiver', 'OnOAIDQueryResult', state)
    //     其中 state：'1' 已授权 / '0' 玩家拒绝（系统确实弹过框）/ '-1' 未决（系统没弹框）
    //
    //   ★ 为什么要 '-1'：鸿蒙不是每个用户都会看到广告追踪系统弹窗（手机设置里可关闭
    //     广告标识符 / 限制广告跟踪），这类用户 requestPermissionsFromUser 静默返回 -1。
    //     如果把查询结果直接当成"玩家拒绝"写进存档，会把从未做过选择的用户判成
    //     IAA 未授权分层，且设置页开关永远打不开（激励拿不到）。收到 '-1' 时保持存档为空。
    //
    //   与 OnOAIDClosedSignal 的区别：
    //     - 这次查询不会弹系统框，也不代表玩家刚做了授权操作，
    //       所以【不】走埋点（Pop_accept/Pop_refuse）、不动引导页；
    //     - 只把系统里的真实状态同步到存档，并在变化时刷新用户分层；
    //     - 唯一例外：状态从"未开启"变成"已开启"（玩家在系统设置里自己开了，或授权回调丢了）
    //       时，由 AdTrackingManager 补发一次广告追踪激励（GrantReward 内部保证只发一次）。
    // ============================================================
    public void OnOAIDQueryResult(string enabledStr)
    {
        int enabled = 0;
        int.TryParse(enabledStr, out enabled);

        // < 0 → 未决：系统没弹框 / 查询异常，玩家并没有做出选择，不能写档
        bool undetermined = enabled < 0;
        bool granted = enabled == 1;
        Debug.Log($"[PushMessageReceiver] OnOAIDQueryResult 收到, enabledStr={enabledStr}, " +
                  $"undetermined={undetermined}, granted={granted}");

        AdTrackingManager manager = AdTrackingManager.Instance;
        if (manager != null)
        {
            if (undetermined) manager.OnTrackingStatusUndetermined();
            else manager.OnTrackingStatusQueried(granted);
            return;
        }

        // 未决：保持存档为空（写成 "0" 会让分层与设置页开关永久错位）
        if (undetermined) return;

        // 兜底：管理器尚未创建时直接同步存档 + 刷新分层
        var userData = GameDataManager.Instance != null ? GameDataManager.Instance.UserData : null;
        if (userData == null) return;

        string want = granted ? "1" : "0";
        if (userData.adTrackingEnabled == want) return;

        userData.adTrackingEnabled = want;
        if (ShopManager.shopManager != null)
        {
            string curLayer = ShopManager.shopManager.GetCurrentUserLayer();
            ShopManager.shopManager.RefreshLayer();
            Debug.Log($"[PushMessageReceiver] 广告追踪状态静默同步（兜底路径）：granted={granted}，分层={curLayer}");
        }
    }
}