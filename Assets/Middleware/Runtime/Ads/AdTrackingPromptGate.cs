using UnityEngine;

namespace Middleware
{
    /// <summary>
    /// 广告追踪（APP_TRACKING_CONSENT）系统弹窗的全局闸门。
    ///
    /// 需求（鸿蒙）：**引导页点「继续」之后，才允许弹出系统广告追踪权限框**。
    ///
    /// 为什么需要一个独立闸门（而不是只靠调用点自觉）：
    ///   1. 鸿蒙的 `requestPermissionsFromUser` 在权限"未决"时**一定弹系统框**，
    ///      所以"启动 / 回前台静默核对"这类查询，只要在引导页显示期间跑，就会把系统框抢在
    ///      "继续"之前弹出来（这正是本次要修的现象）；
    ///   2. 追踪授权入口散落在 Game（三个桥接）+ AdTrackingManager（引导/设置），
    ///      任何一处漏判都会破坏时序，放在桥接层做最后一道硬约束最可靠。
    ///
    /// 两道开关：
    ///   · <see cref="IsStartupGuidePending"/>：首启引导页正在等玩家点「继续」→ 期间**禁止任何**
    ///     可能拉起系统框的调用（查询、请求）；
    ///   · <see cref="AllowOnce"/> / <see cref="Consume"/>：一次性放行券，只有"点继续 / 玩家主动点
    ///     设置里的广告关联"时才发一张，桥接方法消费掉才真正调用系统接口。
    /// </summary>
    public static class AdTrackingPromptGate
    {
        /// <summary>首启引导页是否正在等待玩家点「继续」</summary>
        public static bool IsStartupGuidePending { get; private set; }

        /// <summary>一次性的系统弹窗放行券（Consume 后失效，必须重新 AllowOnce）</summary>
        private static bool _promptAllowedOnce;

        /// <summary>
        /// 静默查询是否可行：引导页等待期间一律不查 —— 鸿蒙的查询接口在未决时会弹框，
        /// 与"静默"语义不符，必须等引导页结束再核对。
        /// </summary>
        public static bool CanQuerySilently => !IsStartupGuidePending;

        /// <summary>引导页开始/结束等待时切换（由 AdTrackingManager 维护）</summary>
        public static void SetStartupGuidePending(bool pending)
        {
            if (IsStartupGuidePending == pending) return;
            IsStartupGuidePending = pending;
            Debug.Log($"[AdTracking][闸门] 引导页等待状态 = {pending}" +
                      "（true 期间禁止任何可能拉起系统追踪弹窗的调用）");
        }

        /// <summary>发一张放行券：引导页点「继续」/ 玩家主动点设置里的「广告关联」时调用</summary>
        public static void AllowOnce()
        {
            _promptAllowedOnce = true;
        }

        /// <summary>
        /// 消费放行券。true = 本次允许拉起系统追踪弹窗（券已用掉）；
        /// false = 没有任何用户动作授权这次弹窗，调用方必须放弃（并打日志）。
        /// </summary>
        public static bool Consume()
        {
            if (!_promptAllowedOnce) return false;
            _promptAllowedOnce = false;
            return true;
        }
    }
}
