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

        var userData = GameDataManager.Instance != null ? GameDataManager.Instance.UserData : null;
        if (userData == null) return;
        
        bool hasAdTracking = false;
        if (string.IsNullOrEmpty(userData.adTrackingEnabled))
        {
            int enabled = 0;
            int.TryParse(enabledStr, out enabled);
            Debug.Log($"[PushMessageReceiver] OnOAIDClosedSignal 收到, enabledStr={enabledStr}, enabled={enabled}");

            // ---------- 1. 埋点上报 ----------
            if (enabled == 1)
            {
                AnalyticMgr.PopAccept(popName: "广告追踪");
            }
            else
            {
                AnalyticMgr.PopRefuse(popName: "广告追踪");
            }
            
            // ---------- 2. 记录广告追踪状态到 UserData（供分层判断） ----------
            hasAdTracking = enabled == 1;
          
            userData.adTrackingEnabled = enabled.ToString();
            Debug.Log($"[PushMessageReceiver] 广告追踪权限状态已写入 UserData.adTrackingEnabled={hasAdTracking}");
        }

        // ---------- 3. 刷新用户分层并打印日志 ----------
        if (ShopManager.shopManager != null)
        {
            string curLayer = ShopManager.shopManager.GetCurrentUserLayer();
            ShopManager.shopManager.RefreshLayer();

            bool isPaid = userData.TotalPayTimes > 0;
            Debug.Log($"[Shop] 广告追踪回调触发分层刷新: " +
                      $"hasAdTracking={hasAdTracking}, isPaid={isPaid}, " +
                      $"分层 {curLayer}");
        }
        else
        {
            Debug.LogWarning("[PushMessageReceiver] ShopManager.shopManager 为空，跳过分层刷新");
        }
    }
}