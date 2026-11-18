#if UNITY_EDITOR
using Middleware;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 编辑器下调试「广告追踪」用的菜单（Tools/广告追踪/...）。
///
/// 编辑器没有真实的 ATT / 系统设置，授权状态靠 PlayerPrefs 模拟：
///   att_mock_status      → 0 未决定 / 1 受限 / 2 拒绝 / 3 同意（当前"系统状态"）
///   att_mock_mode        → 授权请求的返回策略：0 随机（默认）/ 1 固定同意 / 2 固定拒绝
///   att_mock_always_show  → 1 时每次进加载页都弹引导页
/// 这里提供一键切换，省得自己改 PlayerPrefs 或改代码。
/// </summary>
public static class AdTrackingEditorMenu
{
    private const string MenuRoot = "Tools/广告追踪/";
    private const string AlwaysShowKey = "att_mock_always_show";

    // ---------------- 授权请求的返回策略 ----------------

    [MenuItem(MenuRoot + "请求结果/随机同意或拒绝（默认）", false, 1)]
    private static void ModeRandom()
    {
        SetMode(AdTrackingPermission.EditorMockMode.Random);
    }

    [MenuItem(MenuRoot + "请求结果/固定同意", false, 2)]
    private static void ModeAlwaysAllow()
    {
        SetMode(AdTrackingPermission.EditorMockMode.AlwaysAllow);
    }

    [MenuItem(MenuRoot + "请求结果/固定拒绝", false, 3)]
    private static void ModeAlwaysDeny()
    {
        SetMode(AdTrackingPermission.EditorMockMode.AlwaysDeny);
    }

    // ---------------- 当前"系统状态"（模拟玩家手机上的设置） ----------------

    [MenuItem(MenuRoot + "当前状态/已同意（Authorized）", false, 10)]
    private static void MockAuthorized()
    {
        AdTrackingPermission.SetMockStatus(AdTrackingStatus.Authorized);
        RefreshInPlayMode();
    }

    [MenuItem(MenuRoot + "当前状态/已拒绝（Denied）", false, 11)]
    private static void MockDenied()
    {
        AdTrackingPermission.SetMockStatus(AdTrackingStatus.Denied);
        RefreshInPlayMode();
    }

    [MenuItem(MenuRoot + "当前状态/未决定（NotDetermined，下次请求重新掷）", false, 12)]
    private static void MockNotDetermined()
    {
        AdTrackingPermission.ResetMockStatus();
        RefreshInPlayMode();
    }

    // ---------------- 其它 ----------------

    [MenuItem(MenuRoot + "切换「每次加载页都弹引导页」", false, 20)]
    private static void ToggleAlwaysShow()
    {
        int next = PlayerPrefs.GetInt(AlwaysShowKey, 0) == 1 ? 0 : 1;
        PlayerPrefs.SetInt(AlwaysShowKey, next);
        PlayerPrefs.Save();
        Debug.Log($"[AdTracking] att_mock_always_show = {next}");
    }

    [MenuItem(MenuRoot + "重置引导页 / 激励 / 模拟状态", false, 21)]
    private static void ResetAll()
    {
        AdTrackingManager.ResetGuideStateForDebug();
        AdTrackingPermission.ResetMockStatus();
        Debug.Log("[AdTracking] 已重置：下次进加载页会重新弹引导页，激励可再次领取，请求结果按当前策略重掷");
    }

    [MenuItem(MenuRoot + "打印当前状态", false, 30)]
    private static void PrintStatus()
    {
        AdTrackingStatus mock = AdTrackingPermission.GetStatus();
        string save = "(未运行)";
        string layer = "(未运行)";

        if (Application.isPlaying && GameDataManager.Instance != null)
        {
            UserData user = GameDataManager.Instance.UserData;
            if (user != null)
            {
                save = $"adTrackingEnabled=\"{user.adTrackingEnabled}\" " +
                       $"guideShown={user.adTrackingGuideShown} " +
                       $"rewardClaimed={user.adTrackingRewardClaimed}";
            }

            if (ShopManager.shopManager != null)
                layer = ShopManager.shopManager.GetCurrentUserLayer();
        }

        Debug.Log($"[AdTracking] 模拟系统状态={mock} | 请求策略={AdTrackingPermission.MockMode} | 存档 {save} | 用户分层={layer}");
    }

    private static void SetMode(AdTrackingPermission.EditorMockMode mode)
    {
        AdTrackingPermission.MockMode = mode;
    }

    /// <summary>运行中改了模拟状态，顺手刷一次分层和设置页开关（等价于"系统状态变了"）。</summary>
    private static void RefreshInPlayMode()
    {
        if (!Application.isPlaying) return;
        if (AdTrackingManager.Instance == null) return;

        // 走静默核对：状态有变化会写回存档、刷新分层并通知设置页
        AdTrackingManager.Instance.SyncTrackingStatusFromPlatform("编辑器菜单");
    }
}
#endif
