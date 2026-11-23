using System;
using System.Collections;
using System.Collections.Generic;
using Middleware;
using UnityEngine;

/// <summary>
/// 广告追踪（关联应用）引导 / 权限 / 激励的统一流程编排。
///
/// 需求对应关系：
///   ① 首次启动（隐私弹完之后、加载页之上）出引导页 → 点"继续"出系统权限弹窗（盖在引导页上层）
///      → 系统弹窗关闭时，引导页同步关闭（允许/拒绝都关），继续走加载流程进游戏。
///      （系统弹窗"关闭"与"结果回调"是同一时刻，所以这里不要求玩家二次点"继续"）
///   ② 拒绝授权的玩家，玩法设置里出现"广告关联"开关（S=1 时才出现），点开关 → 引导页"去开启"
///      → 跳系统广告追踪设置页 → 回前台校验结果。
///   ③ 广告追踪激励 S_G（来源：shop_layer.csv 的 AdTrackingStimulate 列，按用户分层取）：
///      S=1 开 / S=0 关，G 为金币数。开启授权后奖励 G 金币，只发一次
///      （UserData.adTrackingRewardClaimed）。奖励 UI：
///        · 设置页 OptionsView：adsTrackTable 整行（S=0 隐藏）+ 行内 GoldTable 显示 G；
///        · 引导页 AdTrackingGuidance：按钮上方"+G"角标；
///      玩家返回游戏后，用"领取金币奖励"弹窗（AwardScreen，走 OverrideRewards 展示金币）表现。
///
/// 平台：
///   iOS        → AdTrackingPermission（ATT 原生桥）
///   鸿蒙        → Game.RequestTrackingConsent（ArkTS 侧 APP_TRACKING_CONSENT），结果经 PushMessageReceiver 回到 OnPermissionResult
///   其它/安卓   → 无该权限概念，流程整体不触发
/// </summary>
public class AdTrackingManager : MonoBehaviour
{
    public static AdTrackingManager Instance { get; private set; }

    /// <summary>授权状态变化（true=已同意），设置界面用它刷新"广告关联"开关</summary>
    public event Action<bool> OnAuthorizationChanged;

    #region 常量

    private const string GuidePrefabPath = "Privacy/AdTrackingGuidance";

    /// <summary>埋点名（通用数数方案：Pop_up / Pop_accept / Pop_refuse）</summary>
    private const string PopNameGuide = "广告追踪引导";
    private const string PopNameGuideSettings = "广告追踪引导-设置";
    /// <summary>系统权限弹窗（iOS ATT / 鸿蒙跟踪授权框）</summary>
    private const string PopNameTracking = "广告追踪";

    /// <summary>进入加载页后，等加载页渲染一帧再盖引导页（避免同帧创建导致第一帧看不到加载页）</summary>
    private const float GuideDelay = 0.05f;

    /// <summary>引导页已经出来后，等分层配置就绪再补上"+G 金币"标签的最长时间</summary>
    private const float LayerConfigWait = 3f;

    /// <summary>加载流程等待引导页结束的最长时间（兜底，防止卡在加载页）</summary>
    private const float StartupFlowTimeout = 90f;

    /// <summary>点"继续"关页后，延迟多久放行广告请求（避开系统授权框的弹出时序）</summary>
    private const float GateReleaseDelay = 2f;

    /// <summary>
    /// Update 里补放行广告请求的轮询间隔（秒）。
    /// 隐私同意 / UserData 就绪 / 引导流程收尾这三个条件可能以任意顺序到达，
    /// 用轮询兜住"当时不满足、之后满足了却没人再调 ReleaseAdGate"的情况。
    /// </summary>
    private const float AdGatePollInterval = 0.5f;

    /// <summary>等待主界面出现以展示激励弹窗的最长时间（首启场景：还在加载/引导里）</summary>
    private const float RewardPopupTimeout = 15f;

    /// <summary>
    /// 等"设置界面关掉"再弹金币的最长时间。
    /// 玩家从系统广告追踪设置返回时，设置页还开着，直接弹金币会叠在设置上面；
    /// 这里等他关掉设置页再弹，需求表现是"返回游戏时弹窗获得金币"。
    /// </summary>
    private const float RewardPopupWaitTimeout = 120f;

    /// <summary>静默查询授权状态的最小间隔（秒）。启动 / 每次回前台都可能触发，做个节流。</summary>
    private const float StatusQueryThrottle = 3f;

    /// <summary>静默查询等待回包的最长时间（秒）。超时视为桥接不可用/回包丢失，允许重试。</summary>
    private const float StatusQueryTimeout = 5f;

    /// <summary>Pop_up 等待埋点就绪的最长时间（秒）。首启引导页出现得早，超时只打错误日志。</summary>
    private const float PopReportRetryTimeout = 10f;

    /// <summary>等分层配置就绪再发激励的最长时间（秒）</summary>
    private const float RewardGrantRetryTimeout = 10f;

    #endregion

    #region 运行时状态

    private AdTrackingGuidance _guide;
    private AdTrackingGuidance.Mode _guideMode;
    private Transform _guideParent;

    private bool _startupFlowFinished = true;
    private bool _startupFlowStarted;
    private bool _waitingUserData;
    private bool _permissionRequested;
    private bool _waitingSettingsReturn;
    private bool _openedSystemSettings;

    /// <summary>本次进程内是否已做过"存档未决但引导页弹过"的启动补偿查询</summary>
    private bool _undeterminedRecoveryTried;

    /// <summary>广告请求是否已经放行（放行后 Update 不再轮询）</summary>
    private static bool _adGateReleased;

    /// <summary>"等隐私协议同意"的日志只打一次，避免 Update 轮询刷屏</summary>
    private static bool _adGateWaitLogged;

    /// <summary>Update 轮询计时</summary>
    private float _adGatePollTimer;

    /// <summary>静默查询授权状态是否在进行中（鸿蒙是异步回包，避免连点/重入）</summary>
    private bool _statusQueryInFlight;

    /// <summary>上次静默查询的时间（realtimeSinceStartup），用于节流</summary>
    private float _lastStatusQueryTime = -999f;

    private int _pendingRewardGold;

    /// <summary>"待领金币"弹窗是否已经在等（避免重复起协程）</summary>
    private bool _pendingRewardPopupRunning;

    /// <summary>
    /// 本次奖励是否要在"设置页还开着"时就弹（设置入口里同意广告追踪的场景）：
    /// 需求是设置界面同意后直接弹 AwardScreen 发奖励，不必等玩家关掉设置页。
    /// </summary>
    private bool _showRewardOverSettings;

    // ---- 授权前的激励快照（见 CaptureRewardBeforeAuthorization）----
    /// <summary>授权前分层对应的激励金币数（0 = 没有激励）</summary>
    private int _rewardGoldBeforeAuth;
    /// <summary>授权前的分层名（配置迟到时按它回头查表）</summary>
    private string _rewardLayerBeforeAuth;
    /// <summary>授权前是否已经成功读到过 AdTrackingStimulate 配置</summary>
    private bool _rewardResolvedBeforeAuth;

    /// <summary>本次引导页实例是否已经上报过 Pop_up（bug44：展示与上报必须是同一时刻，且只报一次）</summary>
    private bool _guidePopReported;

    #endregion

    #region 生命周期

    public static AdTrackingManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        GameObject go = new GameObject("[AdTrackingManager]");
        DontDestroyOnLoad(go);
        return go.AddComponent<AdTrackingManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Application.focusChanged += OnFocusChanged;
    }

    private void OnDestroy()
    {
        Application.focusChanged -= OnFocusChanged;
        if (_guide != null) _guide.OnPrimaryClicked -= HandleGuidePrimary;
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 广告请求放行的兜底轮询。
    ///
    /// 背景：广告请求只受"隐私协议已同意"约束（鸿蒙的广告追踪授权不再是前置条件）。
    /// 但 ReleaseAdGate 的调用点（引导流程结束 / 点继续 / 超时收尾）都发生在某个具体时刻，
    /// 一旦那一刻隐私还没同意（首次启动隐私弹窗较晚、UserData 慢到），之后就没有任何地方再调它，
    /// 结果整局广告请求被挂起（激励视频、横幅全废）。
    /// 这里每 0.5s 重试一次，隐私同意后立刻补放行；放行成功即停止轮询。
    /// </summary>
    private void Update()
    {
        if (_adGateReleased) return;

        _adGatePollTimer += Time.unscaledDeltaTime;
        if (_adGatePollTimer < AdGatePollInterval) return;
        _adGatePollTimer = 0f;

        ReleaseAdGate();
    }

    #endregion

    #region 对外查询

    /// <summary>当前平台是否有"广告追踪"授权概念（iOS / 鸿蒙 / 编辑器）</summary>
    public static bool PlatformSupportsTracking
    {
        get
        {
#if UNITY_IOS || UNITY_EDITOR || UNITY_OPENHARMONY
            return true;
#else
            return false;
#endif
        }
    }

    public AdTrackingStatus Status
    {
        get
        {
            if (AdTrackingPermission.IsSupported) return AdTrackingPermission.GetStatus();

            UserData user = User;
            if (user == null) return AdTrackingStatus.NotDetermined;
            if (user.adTrackingEnabled == "1") return AdTrackingStatus.Authorized;
            if (user.adTrackingEnabled == "0") return AdTrackingStatus.Denied;
            return AdTrackingStatus.NotDetermined;
        }
    }

    public bool IsAuthorized => Status == AdTrackingStatus.Authorized;

    /// <summary>
    /// UI 口径的授权状态（设置页开关、整行显隐、激励可领判定用）：
    /// **以存档 adTrackingEnabled 为准**，与用户分层（IAA00/01/02 与 IAA10/11/12）同源；
    /// 只有存档未决（""）时才回落到系统状态。
    ///
    /// ★ 为什么不能直接用 <see cref="Status"/>：在 iOS / 编辑器上 Status 走的是系统 ATT
    ///   （编辑器读 PlayerPrefs.att_mock_status），和存档是两套数据。一旦两者不一致，
    ///   就会出现"分层按 adTrackingEnabled=1 算成 IAA1x，设置页开关却按系统状态显示为关"的割裂。
    ///   存档由所有回调（OnPermissionResult / ApplyTrackingStatus）统一写入，且启动与回前台
    ///   都会按系统真实状态校正，所以 UI 用它既一致又不会长期偏离系统。
    ///   需要"系统真实状态"的地方（判断从系统设置返回的结果）仍然用 <see cref="Status"/>。
    /// </summary>
    public bool IsAuthorizedForUI
    {
        get
        {
            UserData user = User;
            if (user != null)
            {
                if (user.adTrackingEnabled == "1") return true;
                if (user.adTrackingEnabled == "0") return false;
            }

            // 存档还没决定过：iOS / 编辑器回落到系统状态，保证首次授权后立刻能显示为开
            return AdTrackingPermission.IsSupported
                   && AdTrackingPermission.GetStatus() == AdTrackingStatus.Authorized;
        }
    }

    /// <summary>
    /// 设置页是否展示「广告关联」整行：
    ///   · 已授权（含"首次启动就同意"）→ 常驻显示，开关呈"开"状态，玩家还能从这里回系统设置关掉；
    ///   · 未授权 → 只有当前分层 AdTrackingStimulate 的 S=1（有激励）时才显示。
    ///
    /// ★ 为什么已授权要单独放行：授权成功后用户分层会立刻从 IAA01/IAA02 切到 IAA11/IAA12（0_0），
    ///   若只看当前分层的 S，整行会直接消失，玩家在设置里既看不到"已开启"，也无法回到系统设置改回去。
    /// </summary>
    public bool ShouldShowSettingsRow()
    {
        if (!PlatformSupportsTracking) return false;
        if (IsAuthorizedForUI) return true;
        return TryResolveStimulate(out bool enabled, out int _) && enabled;
    }

    /// <summary>兼容旧名：语义已统一为 <see cref="ShouldShowSettingsRow"/>。</summary>
    public bool ShouldShowSettingsEntry() => ShouldShowSettingsRow();

    /// <summary>设置开关上要展示的激励金币数（没有激励返回 0）</summary>
    public int SettingsEntryRewardGold
    {
        get
        {
            if (!TryResolveStimulate(out bool enabled, out int gold)) return 0;
            return enabled ? gold : 0;
        }
    }

    /// <summary>
    /// 激励策略开关 S（AdTrackingStimulate = S_G）：
    ///   S=1 → 设置里出现「广告关联」行，开启授权后送 G 金币；
    ///   S=0 → 未授权时不展示这一行（不引导、不发奖）；已授权的玩家仍常驻显示（见 ShouldShowSettingsRow）。
    /// </summary>
    public bool StimulateEnabled
    {
        get { return TryResolveStimulate(out bool enabled, out int _) && enabled; }
    }

    /// <summary>激励金币数 G（未开启激励或配置缺失时返回 0）</summary>
    public int StimulateGold
    {
        get
        {
            if (!TryResolveStimulate(out bool enabled, out int gold)) return 0;
            return enabled ? gold : 0;
        }
    }

    /// <summary>
    /// 当前是否还有「开启即得 G 金币」可拿：S=1 且 G&gt;0 且没领过 且还没授权。
    /// 设置页 GoldTable 的显隐、引导页按钮角标的显隐都用它判断。
    /// </summary>
    public bool IsRewardClaimable
    {
        get
        {
            if (!PlatformSupportsTracking) return false;
            if (!TryResolveStimulate(out bool enabled, out int gold)) return false;
            if (!enabled || gold <= 0) return false;

            UserData user = User;
            if (user == null) return false;
            if (user.adTrackingRewardClaimed) return false;

            return !IsAuthorizedForUI;   // 已经开启过就没有"开启即送"了（与开关状态同源，避免"开关是开还给 +G"）
        }
    }

    #endregion

    #region 授权状态静默同步（bug34：改系统设置后再启动，IAA 分层不刷新）

    /// <summary>
    /// 静默核对系统里的真实广告追踪授权状态，与存档 <see cref="UserData.adTrackingEnabled"/> 不一致时写回并刷新用户分层。
    ///
    /// 背景（bug34）：玩家在游戏里先同意了广告关联，之后去【系统设置】把它改成禁止，再启动游戏。
    /// 因为引导页只弹一次（adTrackingGuideShown=true），启动流程不会再发起授权请求，
    /// 存档里的 adTrackingEnabled 一直是 "1"，于是 IAA 分层仍然按"已通过"算（IAA10/11/12），
    /// 正确结果应该是 IAA00/01/02。
    ///
    /// 平台差异：
    ///   iOS / 编辑器 → AdTrackingPermission.GetStatus() 直接读系统状态（只读，不弹框）；
    ///   鸿蒙         → 没有只读 API，走新增的静默查询桥接 Game.QueryTrackingConsent()
    ///                  （内部只调 requestPermissionsFromUser：已决定过时系统不弹框，直接返回同意/拒绝），
    ///                  结果经 PushMessageReceiver.OnOAIDQueryResult → OnTrackingStatusQueried 回来。
    ///
    /// 只在"可以确定不会拉起系统弹窗"时才查：
    ///   - iOS / 编辑器：GetStatus 是只读查询，存档为空也能对齐；
    ///   - 鸿蒙：只有存档为 1 或 0（已决定过）时才查，存档为空交给引导页流程处理。
    /// </summary>
    /// <param name="reason">日志用（启动 / 回前台）</param>
    public void SyncTrackingStatusFromPlatform(string reason)
    {
        UserData user = User;
        if (user == null) return;

        // 还没决定过授权时的两种处理：
        //   · iOS / 编辑器：GetStatus 是只读查询（编辑器读 att_mock_status，不弹框），可以直接对齐。
        //     否则会出现"存档是空、设置页按系统状态显示为开、用户分层却按未授权算"的割裂
        //     （典型场景：存档被清 / 换新号，但系统里 ATT 早就是已同意）。
        //   · 鸿蒙等无只读接口的平台：清空态一律交给引导页流程，别在这里拉系统弹窗。
        if (user.adTrackingEnabled != "1" && user.adTrackingEnabled != "0"
            && !AdTrackingPermission.IsSupported)
        {
            Debug.Log($"[AdTracking] {reason}静默核对跳过：授权状态还未决定（adTrackingEnabled=\"{user.adTrackingEnabled}\"）");
            return;
        }

        // 正在走"设置 → 去开启"流程时别插一脚，结果由 OnPermissionResult 统一处理
        if (_waitingSettingsReturn || _openedSystemSettings)
        {
            Debug.Log($"[AdTracking] {reason}静默核对跳过：正在系统设置授权流程中");
            return;
        }

        if (_statusQueryInFlight)
        {
            // 上次查询迟迟没有回包（ArkTS 桥接未更新 / 消息丢失）→ 超时后允许重试
            if (Time.realtimeSinceStartup - _lastStatusQueryTime < StatusQueryTimeout) return;
            _statusQueryInFlight = false;
        }

        if (Time.realtimeSinceStartup - _lastStatusQueryTime < StatusQueryThrottle) return;

        if (AdTrackingPermission.IsSupported)
        {
            // iOS / 编辑器：直接读，无副作用
            AdTrackingStatus status = AdTrackingPermission.GetStatus();
            if (status != AdTrackingStatus.Authorized && status != AdTrackingStatus.Denied)
            {
                Debug.Log($"[AdTracking] {reason}静默核对跳过：系统状态未决定/不支持（{status}）");
                return;
            }

            _lastStatusQueryTime = Time.realtimeSinceStartup;
            ApplyTrackingStatus(status == AdTrackingStatus.Authorized, $"{reason}系统状态={status}");
            return;
        }

#if UNITY_OPENHARMONY && !UNITY_EDITOR
        // 鸿蒙：异步静默查询，结果由 OnOAIDQueryResult 回来
        if (Game.self == null) return;

        _lastStatusQueryTime = Time.realtimeSinceStartup;
        _statusQueryInFlight = true;
        Debug.Log($"[AdTracking] {reason}静默核对：向 ArkTS 侧查询 APP_TRACKING_CONSENT 当前状态");
        Game.self.QueryTrackingConsent();
#else
        Debug.Log($"[AdTracking] {reason}静默核对跳过：当前平台没有可读的授权状态");
#endif
    }

    /// <summary>
    /// 鸿蒙静默查询回包（ArkTS TuanjieSendMessage → PushMessageReceiver.OnOAIDQueryResult）。
    /// 只做状态同步 + 分层刷新，不走埋点、不发激励、不动引导页。
    /// </summary>
    public void OnTrackingStatusQueried(bool granted)
    {
        _statusQueryInFlight = false;
        ApplyTrackingStatus(granted, "鸿蒙静默查询");
    }

    /// <summary>
    /// 鸿蒙静默查询回包：**未决**（ArkTS 回 '-1'）。
    ///
    /// 触发场景：系统压根没弹过广告追踪授权框 —— 手机系统设置里关闭了广告标识符 /
    /// 限制了广告跟踪、此前拒绝过、企业策略禁用、查询异常等。
    /// 这类玩家从未做过选择，**不能写 adTrackingEnabled**（写 "0" 会让 IAA 分层判成未授权、
    /// 设置页开关显示为关且永远打不开、激励拿不到）。
    /// 这里只清掉 in-flight 标记，存档保持为空（"未决"）。
    /// </summary>
    public void OnTrackingStatusUndetermined()
    {
        _statusQueryInFlight = false;

        UserData user = User;
        string cur = user != null ? user.adTrackingEnabled : "(UserData 未就绪)";
        Debug.Log($"[AdTracking] 鸿蒙静默查询：授权状态未决（系统未弹框 / 已禁用广告标识符），" +
                  $"存档保持 \"{cur}\" 不变，不做分层写入");
    }

    /// <summary>
    /// 把核对到的授权状态写回存档，变化时刷新用户分层（IAA 分层依赖该字段）。
    /// </summary>
    private void ApplyTrackingStatus(bool granted, string reason)
    {
        UserData user = User;
        if (user == null) return;

        string want = granted ? "1" : "0";
        if (user.adTrackingEnabled == want)
        {
            Debug.Log($"[AdTracking] {reason}: 授权状态无变化（{want}），分层保持");
            return;
        }

        string before = user.adTrackingEnabled;

        // 同 OnPermissionResult：授前先定激励金额（分层会随授权状态切换）
        if (granted) CaptureRewardBeforeAuthorization();

        user.adTrackingEnabled = want;
        if (GameDataManager.Instance != null) GameDataManager.Instance.CommitGameData();

        string layer = ShopManager.shopManager != null ? ShopManager.shopManager.GetCurrentUserLayer() : "(ShopManager 未初始化)";
        if (ShopManager.shopManager != null) ShopManager.shopManager.RefreshLayer();

        Debug.Log($"[AdTracking] {reason}: 授权状态 {before} → {want}（系统真实状态），已刷新用户分层 → {layer}");
        OnAuthorizationChanged?.Invoke(granted);

        // ★ 补发路径：这次变化来自"静默核对系统状态"，不是我们主动发起的授权请求，
        //   说明玩家在系统设置里自己开启了（或者我们的授权回调丢了：进程被系统杀掉等）。
        //   需求是"开启广告追踪激励 → 奖励 G 金币，只能领一次"，所以这里补发一次；
        //   GrantReward 内部用 adTrackingRewardClaimed 兜住重复。
        if (granted) StartCoroutine(GrantRewardWhenReady());
    }

    #endregion

    #region 启动流程（加载页）

    /// <summary>加载页出现时调用：决定要不要出引导页，并让加载流程等它结束</summary>
    public void BeginStartupFlow(Transform uiParent)
    {
        if (_startupFlowStarted) return;   // 一次进程内只走一次

        _guideParent = uiParent;

        // UserData 还没就绪属于时序问题，不占用"只走一次"的机会，等它就绪再判断
        if (User == null)
        {
            Debug.LogWarning("[AdTracking] UserData 还没加载，等它就绪后再判断是否弹引导");
            if (!_waitingUserData) StartCoroutine(BeginStartupFlowWhenReady(uiParent));
            return;
        }

        _startupFlowStarted = true;

#if UNITY_EDITOR
        // ★ 编辑器专用：模拟状态被手动清成"未决定"（菜单 重置 / 当前状态-未决定），
        //   但存档还留着上一次的 1/0 → 把存档也清回未决，本轮重新走引导页、结果重新随机。
        //   真机不会出现这种组合（ATT / 鸿蒙的授权状态不会自己回到未决定），只在编辑器生效。
        if (AdTrackingPermission.GetStatus() == AdTrackingStatus.NotDetermined
            && !string.IsNullOrEmpty(User.adTrackingEnabled))
        {
            User.adTrackingEnabled = "";
            User.adTrackingGuideShown = false;
            if (GameDataManager.Instance != null) GameDataManager.Instance.CommitGameData();
            Debug.Log("[AdTracking][编辑器] 模拟状态=未决定 → 存档同步清回未决，本轮重新走引导页（结果按策略重新掷）");
        }
#endif

        // ★ bug34：启动时先静默核对系统里的真实授权状态（玩家可能在系统设置里改过），
        //   状态变了会写回存档并刷新分层。必须放在判断要不要弹引导页之前，
        //   这样"已经弹过引导页"的玩家也能拿到正确的分层。
        SyncTrackingStatusFromPlatform("启动");

        // ★ 补偿：引导页弹过、但授权状态还是空（同意后回包前被杀 / 桥接消息丢）→ 静默补查一次。
        //   上面的 SyncTrackingStatusFromPlatform 对"未决"是主动跳过的，所以单独走这一条。
        TryRecoverUndeterminedStatusOnStartup();

        if (!ShouldRunStartupFlow(out string skipReason))
        {
            // 系统里已有授权结果 / 已经弹过 / 隐私还没同意 → 不弹引导，直接放行加载
            //（横幅由玩法里的 ShowBanner 触发，加载页不主动拉）
            Debug.Log($"[AdTracking] 本次加载页不弹广告追踪引导：{skipReason}");
            FinishStartupFlow();
            return;
        }

        Debug.Log("[AdTracking] 加载页已显示，准备弹出广告追踪引导页");
        _startupFlowFinished = false;
        StartCoroutine(StartupGuideRoutine());
    }

    /// <summary>
    /// 启动补偿：引导页已经弹过（adTrackingGuideShown=true），但存档里的授权状态仍是空字符串。
    ///
    /// 出现的场景：玩家在系统授权框上点了"同意"，但回包（鸿蒙 TuanjieSendMessage / iOS 回调）
    /// 在进程被杀、桥接未就绪、消息丢失时没到 Unity → adTrackingEnabled 一直是 ""，
    /// 结果是 IAA 分层按"未授权"算、设置页开关显示为关，和系统真实状态不一致。
    ///
    /// 与 <see cref="SyncTrackingStatusFromPlatform"/> 的区别：那边对"未决"主动跳过（怕拉起系统框），
    /// 这里只在"引导页确实弹过"的前提下补查一次，说明我们此前已经申请过该权限。
    /// 一次进程内只尝试一次。
    /// </summary>
    private void TryRecoverUndeterminedStatusOnStartup()
    {
        if (!PlatformSupportsTracking || _undeterminedRecoveryTried) return;

        UserData user = User;
        if (user == null) return;
        if (!user.IsAgreePrivacy) return;                 // 隐私还没同意，先不碰系统接口
        if (!user.adTrackingGuideShown) return;           // 没弹过引导 → 交给引导流程处理
        if (user.adTrackingEnabled == "1" || user.adTrackingEnabled == "0") return;

        _undeterminedRecoveryTried = true;

        // iOS / 编辑器：GetStatus 是只读查询，不会弹框，可以直接读
        if (AdTrackingPermission.IsSupported)
        {
            AdTrackingStatus status = AdTrackingPermission.GetStatus();
            if (status == AdTrackingStatus.Authorized || status == AdTrackingStatus.Denied)
            {
                Debug.Log($"[AdTracking] 启动补偿：存档未决但系统状态为 {status}，写回归档");
                ApplyTrackingStatus(status == AdTrackingStatus.Authorized, "启动补偿（系统状态）");
            }
            else
            {
                Debug.Log($"[AdTracking] 启动补偿跳过：系统状态仍为 {status}");
            }
            return;
        }

#if UNITY_OPENHARMONY && !UNITY_EDITOR
        // 鸿蒙没有只读接口：QueryTrackingConsent 内部走 requestPermissionsFromUser。
        // 引导页弹过 = 此前已申请过该权限，系统不会再弹标准框（已同意立刻返回 0 / 已拒绝返回 -1）；
        // 只有"引导页是超时自动关闭、根本没真正申请过"这一种情况可能弹一次系统框。
        if (Game.self == null) return;

        if (_statusQueryInFlight &&
            Time.realtimeSinceStartup - _lastStatusQueryTime < StatusQueryTimeout) return;

        _statusQueryInFlight = true;
        _lastStatusQueryTime = Time.realtimeSinceStartup;
        Debug.Log("[AdTracking] 启动补偿：存档未决但引导页已弹过，静默查询系统授权状态");
        Game.self.QueryTrackingConsent();
#endif
    }

    /// <summary>加载流程在 allowSceneActivation 之前等待它，保证"引导页 → 加载页"的顺序</summary>
    public IEnumerator WaitStartupFlowFinished()
    {
        float waited = 0f;
        while (!_startupFlowFinished && waited < StartupFlowTimeout)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!_startupFlowFinished)
        {
            // ★ bug44：玩家在引导页上静默（不点"继续"）超过 StartupFlowTimeout 会被这里强制放行，
            //   随后加载场景卸载 → 引导页跟着被销毁，但 _guide 引用还在、Pop_up 也可能没补上。
            //   现在按"玩家未操作、超时自动进游戏"统一收尾：补一次曝光 + 正常关页 + 走标准收尾流程。
            Debug.LogWarning("[AdTracking] 等待引导流程超时，按“玩家未操作”收尾并强制放行加载");
            CloseGuideAndFinishStartupFlow("引导页静默超时自动进入游戏");
        }
    }

    private bool ShouldRunStartupFlow(out string skipReason)
    {
        skipReason = null;

        if (!PlatformSupportsTracking)
        {
            skipReason = "当前平台没有广告追踪授权（安卓 GAID 无运行时授权）";
            return false;
        }

#if UNITY_EDITOR
        // 调试开关：PlayerPrefs.SetInt("att_mock_always_show", 1) 后每次进加载页都弹，方便反复验证
        if (PlayerPrefs.GetInt("att_mock_always_show", 0) == 1)
        {
            skipReason = null;
            return true;
        }
#endif

        UserData user = User;
        if (user == null)
        {
            skipReason = "UserData 还没加载";
            return false;
        }

        if (!user.IsAgreePrivacy)
        {
            skipReason = "隐私协议还没同意（IsAgreePrivacy=false）";
            return false;
        }

        // ★ bug112：广告追踪引导只对新号展示。
        //   老用户升级时 adTrackingGuideShown 是新增字段（旧存档反序列化后为 false），
        //   只判这一个标记会让所有老玩家升级后必弹一次。
        //   迁移（FeatureUnlockMigration）已经给老号置成已看过，这里再兜一层，
        //   防止登录失败 / 迁移未执行时把引导又弹给有进度的老玩家。
        bool isNewAccount = user.TutorialProgress <= 0
                            && Mathf.Max(user.CurrentHexStage, user.CurrentChessStage) <= AppGameSettings.FirstLevel
                            && user.TotalEarnedGold <= 0
                            && !user.isJoinedZenRank;
        if (!isNewAccount)
        {
            user.adTrackingGuideShown = true;
            if (GameDataManager.Instance != null) GameDataManager.Instance.CommitGameData();
            skipReason = "非新账号（老用户 / 已有进度），广告追踪引导只对新号展示";
            return false;
        }

        if (user.adTrackingGuideShown)
        {
            skipReason = "引导页已经弹过（adTrackingGuideShown=true，只弹一次；调试可清 PlayerPrefs/存档）";
            return false;
        }

        if (user.adTrackingRewardClaimed)
        {
            skipReason = "激励金币已领取过（adTrackingRewardClaimed=true）";
            return false;
        }

        AdTrackingStatus status = Status;
        if (status != AdTrackingStatus.NotDetermined)
        {
            skipReason = $"系统里已有授权结果（{status}），不再打扰";
            return false;
        }

        return true;
    }

    /// <summary>UserData 晚于加载页就绪时，等它到位再走一遍启动判断（避免整局都不弹引导/广告被永久挂起）</summary>
    private IEnumerator BeginStartupFlowWhenReady(Transform uiParent)
    {
        _waitingUserData = true;

        float waited = 0f;
        while (User == null && waited < 10f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        _waitingUserData = false;

        if (User == null)
        {
            Debug.LogWarning("[AdTracking] 等不到 UserData，本次不弹引导；收尾启动流程（广告放行交给 Update 轮询）");
            if (ShopManager.shopManager != null) ShopManager.shopManager.LogStartupLayerAndConfig();

            // ★ 必须走一次收尾：否则 _startupFlowFinished 停在 false、ReleaseAdGate 也不会被调用，
            //   广告请求会被永久挂起（激励视频/横幅整局不可用）。这里只做状态收尾，
            //   UserData 到位后 Update 里的轮询会自动补放行。
            FinishStartupFlow();
            yield break;
        }

        BeginStartupFlow(uiParent);
    }

    private IEnumerator StartupGuideRoutine()
    {
        // 先让加载页渲染出来（这一帧玩家能看到加载界面），紧接着盖上引导页
        yield return new WaitForSeconds(1.2f);

        UserData user = User;
        if (user != null)
        {
            user.adTrackingGuideShown = true;
            if (GameDataManager.Instance != null) GameDataManager.Instance.CommitGameData();
        }

        _permissionRequested = false;

        // ★ bug44：Pop_up 不再在"准备弹"时上报，统一挪到 ShowGuide 真正把页面显示出来之后
        //   （见 ShowGuide 里的 ReportPopShowSafe）。原来在实例化之前就上报，
        //   一旦 prefab/父节点异常没真正显示，就会出现"有曝光无弹窗"的脏数据；
        //   反过来在"静默超时自动进游戏"等异常收尾路径上，也容易和展示时机错位。
        // 首启时账号/商店可能还没初始化完，先按当前能拿到的激励展示，拿到配置后再补 "+G"
        ShowGuide(AdTrackingGuidance.Mode.Startup, ResolveRewardGold());
    }


    private void FinishStartupFlow()
    {
        _startupFlowFinished = true;
        ReleaseAdGate();

        // 启动日志：当前用户分层 + 正在执行的配置（每次启动一次，ShopManager 内部去重）
        if (ShopManager.shopManager != null) ShopManager.shopManager.LogStartupLayerAndConfig();
    }

    /// <summary>
    /// 放行广告请求。
    ///
    /// ★ 口径变更：**只等隐私协议同意**，不再等广告追踪授权流程。
    ///   原因：鸿蒙不是每个用户都会看到广告追踪系统弹窗（系统设置里可以关闭广告标识符 /
    ///   限制广告跟踪），这类用户永远拿不到"授权结果"，把它当硬前置会让整局无广告；
    ///   而且 AdsKit 在未授权时只是拿不到 OAID（非个性化、低 eCPM），并不会请求失败。
    ///   追踪授权结果只用于用户分层（IAA）与设置页开关。
    ///
    /// 调用点：FinishStartupFlow / 点"继续"后延迟放行 / BeginStartupFlowWhenReady 超时 /
    ///        Update 兜底轮询。方法幂等，重复调用无副作用。
    /// </summary>
    private static void ReleaseAdGate()
    {
        if (_adGateReleased) return;

        UserData user = User;
        if (user == null)
        {
            // UserData 还没到（隐私状态未知）：先不放行，Update 会继续重试
            if (!_adGateWaitLogged)
            {
                _adGateWaitLogged = true;
                Debug.Log("[AdTracking] UserData 未就绪，广告请求暂缓（Update 轮询等待隐私状态）");
            }
            return;
        }

        if (!user.IsAgreePrivacy)
        {
            // 隐私还没同意：继续挂起，Update 里每 0.5s 重试一次，同意后立刻放行
            if (!_adGateWaitLogged)
            {
                _adGateWaitLogged = true;
                Debug.Log("[AdTracking] 隐私协议未同意，广告请求暂缓（同意后由 Update 自动放行）");
            }
            return;
        }

        _adGateReleased = true;
        _adGateWaitLogged = false;
        Debug.Log("[AdTracking] 隐私协议已同意，放行广告请求（不再等待广告追踪授权结果）");

        if (Game.self != null) Game.self.AllowAdRequests();
    }


    #endregion

    #region 设置入口（广告关联）

    /// <summary>
    /// 引导页已经显示、但分层配置还没回来时，等配置就绪把"+G"角标补上。
    /// 超时就不补（不影响流程，只是少了奖励提示）。
    /// </summary>
    private IEnumerator RefreshGuideRewardWhenReady()
    {
        float waited = 0f;
        while (waited < LayerConfigWait)
        {
            if (TryResolveStimulate(out bool enabled, out int gold) && enabled && gold > 0)
            {
                if (_guide != null)
                {
                    _guide.SetRewardGold(gold);
                    Debug.Log($"[AdTracking] 分层配置就绪，补上引导页奖励角标 +{gold}");
                }
                yield break;
            }

            waited += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    /// <summary>设置入口（广告关联）</summary>
    public void OpenSettingsGuide(Transform uiParent = null)
    {
        if (_guide != null) return;

        if (uiParent != null) _guideParent = uiParent;
        _permissionRequested = false;
        // Pop_up(广告追踪引导-设置) 同样在 ShowGuide 真正显示后再报（bug44）
        ShowGuide(AdTrackingGuidance.Mode.Settings, ResolveRewardGold());
    }

    /// <summary>
    /// 设置界面「广告关联」开关点击（需求：点开关直接调起系统「当前应用广告追踪」设置界面）：
    ///   - 鸿蒙：requestPermissionsFromUser 已被拒过 → 自动走 requestPermissionOnSetting（应用权限设置弹窗，带开关）；
    ///   - iOS ：跳系统设置页；
    ///   - 玩家关闭该界面时，系统回调（鸿蒙 OnOAIDClosedSignal / iOS 回前台校验）读回最新授权状态，
    ///     经 OnPermissionResult 落盘并通知设置界面的 Toggle 刷新。
    /// 不再先弹内部引导页，避免"点了没反应"。
    /// </summary>
    public void OpenSystemTrackingSettingsFromEntry()
    {
        if (!PlatformSupportsTracking)
        {
            MessageSystem.Instance?.ShowTip("当前系统无需设置广告关联");
            return;
        }

        // ★ 需求：无论广告关联当前是开启还是关闭，点击都要调起系统的「当前应用广告关联设置」。
        //   所以这里不再因为 IsAuthorized 直接短路返回。
        _waitingSettingsReturn = true;
        _permissionRequested = false;

        ReportPopShow(PopNameTracking);
        OpenSystemTrackingSettings();
    }

    #endregion

    #region 引导页

    private void ShowGuide(AdTrackingGuidance.Mode mode, int rewardGold)
    {
        if (_guide != null) return;

        Transform parent = ResolveUiParent(_guideParent);
        GameObject prefab = Resources.Load<GameObject>(GuidePrefabPath);
        if (prefab == null || parent == null)
        {
            Debug.LogError($"[AdTracking] 引导页加载失败：prefab={(prefab != null)}, parent={(parent != null)}");
            if (mode == AdTrackingGuidance.Mode.Startup) FinishStartupFlow();
            return;
        }

        GameObject go = Instantiate(prefab, parent, false);
        go.transform.SetAsLastSibling(); // 覆盖在加载页/设置页之上

        _guideMode = mode;
        _guide = go.GetComponent<AdTrackingGuidance>();
        if (_guide == null)
        {
            Debug.LogError("[AdTracking] 引导页缺少 AdTrackingGuidance 组件");
            Destroy(go);
            if (mode == AdTrackingGuidance.Mode.Startup) FinishStartupFlow();
            return;
        }

        _guide.OnPrimaryClicked += HandleGuidePrimary;
        _guide.Setup(mode, rewardGold);

        // 首启时引导页弹得很早（加载页 1.2s 后），分层配置可能还没就绪 → rewardGold 取到 0，
        // 角标就空了。这里在 LayerConfigWait 内等配置到位再补一次（只补 UI，不影响发奖）。
        if (rewardGold <= 0) StartCoroutine(RefreshGuideRewardWhenReady());

        // ★ bug44：页面真正显示出来这一刻才上报 Pop_up，并且每个实例只报一次。
        //   （原来在 StartupGuideRoutine / OpenSettingsGuide 里"先报再弹"，
        //     展示失败时会产生脏曝光；静默超时自动进游戏时也拿不到对应曝光。）
        ReportGuidePopOnce(mode == AdTrackingGuidance.Mode.Startup ? PopNameGuide : PopNameGuideSettings);
    }

    /// <summary>
    /// 引导页 Pop_up 上报（每个引导页实例只报一次）。
    /// 埋点 SDK 还没就绪时改为延迟重试，避免首启第一条曝光被静默丢弃（bug44）。
    /// </summary>
    private void ReportGuidePopOnce(string popName)
    {
        if (_guidePopReported) return;
        _guidePopReported = true;
        ReportPopShowSafe(popName);
    }

    private void HandleGuidePrimary()
    {
        if (_guide == null) return;
    

        if (_guideMode == AdTrackingGuidance.Mode.Startup)
        {
            // 需求：点"继续"后【立刻】关闭引导页并放行加载，不再等系统授权回调。
            // 之前是等 OnPermissionResult 才关页，玩家会卡在引导页上（真机 6~8 秒无响应），
            // 因为鸿蒙这边 requestPermissionsFromUser / requestPermissionOnSetting 的结果
            // 回来得慢，甚至要等系统设置弹窗交互完。
            // 授权结果异步回来只做：状态落盘 + 埋点 + 分层刷新 + 激励发放。
            Debug.Log("[AdTracking] 玩家点击继续：关闭引导页并继续加载，同时拉起系统授权框");

            _permissionRequested = true;

            CloseGuide();                 // 只关 UI
            _startupFlowFinished = true;  // 放行加载流程

            StartCoroutine(RequestPermissionNextFrame());   // 下一帧再拉系统授权框
            StartCoroutine(ReleaseAdGateDelayed(GateReleaseDelay));
            return;
        }

        // 设置入口："去开启" → 跳系统广告追踪设置页
        _waitingSettingsReturn = true;
        _guide.SetBusy(true);
        OpenSystemTrackingSettings();
    }

    /// <summary>
    /// 下一帧再发系统授权请求：保证引导页的 Destroy 和加载页的第一帧先跑完，
    /// 避免点"继续"瞬间界面停住（真机上表现为"点了没反应"）。
    /// </summary>
    private IEnumerator RequestPermissionNextFrame()
    {
        yield return null;
        RequestPermission();
    }

    /// <summary>
    /// 延迟放行广告请求：系统授权框刚拉起时先别发广告请求，
    /// 免得 AdsKit 取 OAID 的授权框和我们的授权框撞在一起弹两个。
    /// 广告请求闸门本身是幂等的，结果先回来也不会重复放行。
    /// </summary>
    private IEnumerator ReleaseAdGateDelayed(float delay)
    {
        float waited = 0f;
        while (waited < delay)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        ReleaseAdGate();
    }

    private void CloseGuide()
    {
        if (_guide == null) return;

        _guide.OnPrimaryClicked -= HandleGuidePrimary;
        _guide.Close();
        _guide = null;
        _guidePopReported = false;   // 下一次引导页实例重新计一次曝光（bug44）
    }

    /// <summary>
    /// 关闭引导页并放行加载流程。
    /// 需求：系统权限弹窗关闭时，广告追踪引导页要同时关闭（允许/拒绝都关）。
    /// </summary>
    private void CloseGuideAndFinishStartupFlow(string reason)
    {
        if (_guide != null) Debug.Log($"[AdTracking] 关闭广告追踪引导页：{reason}");

        CloseGuide();
        _permissionRequested = false;
        _waitingSettingsReturn = false;
        FinishStartupFlow();
    }

    #endregion

    #region 权限请求与结果

    /// <summary>发起系统授权请求（iOS 弹窗 / 鸿蒙权限框）</summary>
    private void RequestPermission()
    {
        // 系统权限弹窗弹出（原生弹窗的"弹出"时机拿不到，用发起时刻兜底，保证漏斗有头）
        ReportPopShow(PopNameTracking);

        if (AdTrackingPermission.IsSupported)
        {
            AdTrackingPermission.Request(status => OnPermissionResult(status == AdTrackingStatus.Authorized));
            return;
        }

#if UNITY_OPENHARMONY
        // 鸿蒙：只请求 APP_TRACKING_CONSENT（不再顺带 LoadBanner，避免 Banner 的加载时机
        // 影响权限弹窗的弹出顺序）。结果由 ArkTS 侧 TuanjieSendMessage
        //PushMessageReceiver.OnOAIDClosedSignal 回到 Unity（允许/拒绝都会回）
        if (Game.self != null) Game.self.RequestTrackingConsent();
#else
        OnPermissionResult(false);
#endif
    }

    /// <summary>
    /// 授权结果统一入口（iOS 原生回调 / 鸿蒙 OnOAIDClosedSignal / 登录补写状态都会进来）。
    /// 只有"本次流程主动发起"的结果才会走埋点与奖励，登录补写只做状态同步。
    /// </summary>
    public void OnPermissionResult(bool granted)
    {
        UserData user = User;
        if (user == null) return;
     
        bool fromSettings = _waitingSettingsReturn;
        // 首启引导页：点"继续"时已经把页关掉了，_permissionRequested 一直为 true，
        // 这次回来的结果要按"我方发起"处理（否则状态没变化会被当成重复回调直接 return，
        // 埋点和激励都会丢）
        bool wasAwaiting = fromSettings || _openedSystemSettings || _permissionRequested;

        // ★ 设置页只是"进去看看"（原本已开启、关闭设置后仍然是开启）：
        //   只同步开关状态，不算一次新的授权转化（否则每次点按钮都会多报一次 Pop_accept）
        if (fromSettings && granted && user.adTrackingEnabled == "1")
        {
            _waitingSettingsReturn = false;
            _openedSystemSettings = false;
            Debug.Log("[AdTracking] 设置页返回：授权状态未变化（仍为已开启），仅刷新开关");
            OnAuthorizationChanged?.Invoke(true);
            return;
        }

        // 同一个结果重复回调（鸿蒙侧可能既走 TuanjieSendMessage 又走 Promise；
        // 登录补写也会调进来）：状态没变就不重复写盘/重复发奖
        if (!wasAwaiting && user.adTrackingEnabled == (granted ? "1" : "0")) return;

  
        _waitingSettingsReturn = false;

        // ★ 必须在写 adTrackingEnabled 之前把激励金额定下来：
        //   授权状态一变，分层会立刻从 IAA01/IAA02（1_200/1_500）切到 IAA11/IAA12（0_0），
        //   之后再读配置就会读到"关闭"，奖励发不出来。
        if (granted) CaptureRewardBeforeAuthorization();

        user.adTrackingEnabled = granted ? "1" : "0";
        if (GameDataManager.Instance != null) GameDataManager.Instance.CommitGameData();

        // ---------- 埋点（通用数数方案：系统权限弹窗结果） ----------
        if (granted) ReportPopAccept(PopNameTracking);
        else ReportPopRefuse(PopNameTracking);

        _openedSystemSettings = false;

        // ---------- 分层刷新（广告追踪权限是 IAA 分层依据） ----------
        RefreshUserLayer(granted);

        // ---------- 引导页：系统弹窗关闭时同步关闭 ----------
        // 首次启动场景：只有"本次真的发起过请求"才关页（避免登录补写状态把引导页提前关掉），
        //   允许/拒绝都关，玩家不用再点一次"继续"（不关就会卡在引导页，要等 StartupFlowTimeout 才放行）；
        // 设置入口场景：只有"已开启"才关（拒绝的话留页，方便玩家再试一次）。
        bool closeGuide = granted || (_permissionRequested && !fromSettings);
        if (closeGuide)
        {
            CloseGuideAndFinishStartupFlow(fromSettings
                ? "系统设置返回，广告关联已开启"
                : $"系统权限弹窗已关闭，结果 granted={granted}");
        }
        else if (_guide != null)
        {
            _guide.OnPermissionReturned(granted);
            if (fromSettings && !granted) MessageSystem.Instance?.ShowTip("尚未开启广告关联");
        }
        else if (fromSettings && !granted)
        {
            // 设置界面直接跳系统设置（没有内部引导页）时，返回未开启也要给个提示
            MessageSystem.Instance?.ShowTip("尚未开启广告关联");
        }

        // ---------- 通知界面（设置里的"广告关联"开关） ----------
        OnAuthorizationChanged?.Invoke(granted);

        // ---------- 激励发放（同意才发，且只发一次） ----------
        // 走"等配置就绪"的协程：首启阶段可能 ShopManager 的分层表还没加载完，
        // 直接发会读到空配置而静默漏发。
        //
        // ★ 设置界面里同意的：奖励弹窗不等设置页关闭，直接盖在上面弹（需求）。
        _showRewardOverSettings = fromSettings && granted;
        if (wasAwaiting && granted) StartCoroutine(GrantRewardWhenReady());
    }

    
    #endregion

    #region 跳系统设置 & 回前台校验

    private void OpenSystemTrackingSettings()
    {
        _openedSystemSettings = true;

        if (AdTrackingPermission.IsSupported)
        {
            AdTrackingPermission.OpenSystemSettings();

#if UNITY_EDITOR
            // ★ 编辑器没有真实的系统设置页，App 也不会失焦/回前台，OnFocusChanged 不会触发，
            //   于是 CheckSettingsResultRoutine 永远跑不到 → 点开关后状态不变。
            //   这里按"玩家关掉系统设置页返回游戏"处理，走和真机完全相同的返回校验分支，
            //   保证编辑器里"开 → 关 → 开"整条链路可验证。
            StartCoroutine(CheckSettingsResultRoutine());
#endif
            return;
        }

#if UNITY_OPENHARMONY
        // 鸿蒙：直接拉起「当前应用广告关联设置」权限弹窗（带开关）。
        //   以前是 RequestTrackingConsent（先走标准授权请求）：已开启时系统会静默直接返回，
        //   玩家点了按钮看不到任何界面。现在无条件调 requestPermissionOnSetting，
        //   关闭弹窗后由 SendOAID 按最新状态回 OnOAIDClosedSignal（'1'/'0'）。
        if (Game.self != null) Game.self.OpenTrackingSettings();
#else
        MessageSystem.Instance?.ShowTip("请在系统设置中开启广告关联");
#endif
    }

    private void OnFocusChanged(bool hasFocus)
    {
        if (!hasFocus) return;

        // ★ bug34：玩家可能刚去系统设置里把"广告关联"改掉了，回前台时静默核对一次。
        //   正在走"设置 → 去开启"流程时跳过，避免和下面的结果判定抢同一个结果。
        if (!_waitingSettingsReturn) SyncTrackingStatusFromPlatform("回前台");

        if (!_waitingSettingsReturn) return;

        if (!AdTrackingPermission.IsSupported)
        {
            // 鸿蒙：授权结果由 OnOAIDClosedSignal 回传（玩家关闭权限设置弹窗时触发）。
            // 这里只做兜底：回前台后迟迟收不到回调，就按当前存档状态刷新一次设置开关，避免界面卡在"等待"。
            StartCoroutine(HarmonySettingsFallbackRoutine());
            return;
        }

        StartCoroutine(CheckSettingsResultRoutine());
    }

    /// <summary>
    /// 鸿蒙兜底：从系统设置回前台后，如果 3 秒内没有收到 ArkTS 的授权回调，
    /// 就主动向系统静默查询一次最新授权状态（不弹框），保证关闭设置界面后开关/分层是同步的。
    /// 注意：会先清掉等待标志，否则静默查询会被 SyncTrackingStatusFromPlatform 的
    /// "正在系统设置流程中" 判断挡掉。
    /// </summary>
    private IEnumerator HarmonySettingsFallbackRoutine()
    {
        float waited = 0f;
        while (waited < 3f && _waitingSettingsReturn)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!_waitingSettingsReturn) yield break;   // 回调已经处理过

        // 没等到 ArkTS 回包：主动向系统查一次最新授权状态
        // （需求：关闭广告关联设置界面后要拿到当前状态，不能只刷缓存）
        Debug.Log("[AdTracking] 鸿蒙设置界面返回后 3s 内无回包 → 主动静默查询最新授权状态");
        _waitingSettingsReturn = false;
        _openedSystemSettings = false;
        SyncTrackingStatusFromPlatform("系统设置返回");

        // 状态没变化时 SyncTrackingStatusFromPlatform 不会回调 → 这里再刷一次开关，保证界面同步
        OnAuthorizationChanged?.Invoke(IsAuthorizedForUI);
    }

    private IEnumerator CheckSettingsResultRoutine()
    {
        yield return new WaitForSeconds(0.4f); // 等系统设置页动画结束
        if (!_waitingSettingsReturn) yield break;

        if (IsAuthorized)
        {
            // 关闭引导页 / 放行加载流程都在 OnPermissionResult 里统一处理
            OnPermissionResult(true);
        }
        else
        {
            // ★ 玩家在系统设置里把开关关掉了：存档必须同步成 "0"（用户分层依据），
            //   否则会出现"分层按 adTrackingEnabled=1、设置页却显示关"的割裂。
            //   这里直接用系统读到的真实结果写档，不走 OnPermissionResult（那条路会跑埋点/激励）。
            if (Status == AdTrackingStatus.Denied || Status == AdTrackingStatus.Restricted)
                ApplyTrackingStatus(false, "iOS 系统设置返回未开启");
            else
                OnAuthorizationChanged?.Invoke(IsAuthorizedForUI);   // 未决/受限等：只刷一次 UI

            _waitingSettingsReturn = false;
            _openedSystemSettings = false;
            if (_guide != null) _guide.SetBusy(false);
            MessageSystem.Instance?.ShowTip("尚未开启广告关联");
        }
    }

    #endregion

    #region 激励（S_G）

    /// <summary>
    /// 在写 adTrackingEnabled **之前**调用：用"授权前的分层"把激励金币定下来。
    ///
    /// 为什么不能等发奖时再读配置：广告追踪权限本身就是 IAA 分层的输入之一，
    /// 玩家一开启，分层立刻从 IAA01/IAA02（AdTrackingStimulate=1_200 / 1_500）切成
    /// IAA11/IAA12（0_0）。发奖时再读就会读到"关闭"，奖励永远发不出去。
    ///
    /// 配置可能还没缓存（首启早期），这时先记下分层名，交给 GrantRewardWhenReady 回头查表。
    /// </summary>
    private void CaptureRewardBeforeAuthorization()
    {
        // 同一个授予结果可能重复回调（鸿蒙既走 SendMessage 又走 Promise）。
        // 已经锁定过就不要再覆盖：第二次进来时状态已是"已开启"，会读到 IAA11 的 0_0 把奖励冲掉。
        if (_rewardGoldBeforeAuth > 0) return;

        _rewardGoldBeforeAuth = 0;
        _rewardLayerBeforeAuth = null;
        _rewardResolvedBeforeAuth = false;

        if (ShopManager.shopManager == null) return;

        _rewardLayerBeforeAuth = ShopManager.shopManager.GetCurrentUserLayer();

        if (!TryResolveStimulate(out bool enabled, out int gold))
        {
            // 分层表还没就绪 → 保留 layer 名，稍后按名字查
            Debug.LogWarning($"[AdTracking] 授权前激励配置暂不可用（分层={_rewardLayerBeforeAuth}），稍后按分层名重试");
            return;
        }

        _rewardResolvedBeforeAuth = true;
        if (enabled && gold > 0)
        {
            _rewardGoldBeforeAuth = gold;
            Debug.Log($"[AdTracking] 授权前锁定激励：分层={_rewardLayerBeforeAuth}，+{gold} 金币");
        }
        else
        {
            Debug.Log($"[AdTracking] 授权前分层 {_rewardLayerBeforeAuth} 未开启广告追踪激励（S=0）");
        }
    }

    /// <summary>
    /// 发奖（使用 <see cref="CaptureRewardBeforeAuthorization"/> 锁定的金币数）：
    /// 配置迟到时先按授权前的分层名回头查表，最多等 <see cref="RewardGrantRetryTimeout"/> 秒。
    /// </summary>
    private IEnumerator GrantRewardWhenReady()
    {
        float waited = 0f;
        while (waited < RewardGrantRetryTimeout)
        {
            UserData user = User;
            if (user == null || user.adTrackingRewardClaimed) yield break;   // 没数据 / 已经领过

            // ① 授权前已经读到配置：按快照处理，不需要再等
            if (_rewardResolvedBeforeAuth)
            {
                if (_rewardGoldBeforeAuth > 0) GrantReward();
                yield break;
            }

            // ② 配置迟到：按授权前的分层名回头查表
            if (ShopManager.shopManager != null
                && ShopManager.shopManager.TryGetAdTrackingStimulateByLayer(
                       _rewardLayerBeforeAuth, out bool enabled, out int gold))
            {
                _rewardResolvedBeforeAuth = true;
                if (enabled && gold > 0)
                {
                    _rewardGoldBeforeAuth = gold;
                    GrantReward();
                }
                yield break;
            }

            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        Debug.LogWarning("[AdTracking] 等分层配置超时，本次激励发放跳过（adTrackingRewardClaimed 仍为 false）");
    }

    private void GrantReward()
    {
        UserData user = User;
        if (user == null || user.adTrackingRewardClaimed) return;   // 只能领一次

        int gold = _rewardGoldBeforeAuth;
        if (gold <= 0) return;

        user.adTrackingRewardClaimed = true;
        user.Gold += gold;
        if (GameDataManager.Instance != null) GameDataManager.Instance.CommitGameData();

        // 埋点：金币资源变动（change_reason = 广告追踪激励）
        if (Game.self != null && Game.self.Analytics != null)
        {
            AnalyticMgr.ResourceChange(ResId.Gold, gold, ResReason.Other, string.Empty,
                reasonIndex: ResReason.IdxAdTracking, reasonCategory: ResReason.Other);
        }

        _pendingRewardGold = gold;
        Debug.Log($"[AdTracking] 广告追踪激励发放：+{gold} 金币");

        // 已经在大厅里（设置入口开启的场景）→ 立刻弹；首启还在加载页 → 交给 GameLobby 的 GameCoreManager 补弹
        if (SystemManager.Instance != null && SystemManager.Instance.PanelIsShowing(PanelType.PrimaryInterface))
        {
            TryShowPendingReward();
        }
    }

    /// <summary>进入游戏后补弹"领取金币"弹窗（用 AwardScreen 的领取 + 飞入表现）</summary>
    public void TryShowPendingReward()
    {
        // 发奖与大厅启动都会调这里，加个在途标记，避免同一笔奖励起两条协程
        if (_pendingRewardGold <= 0 || _pendingRewardPopupRunning) return;
        StartCoroutine(ShowPendingRewardRoutine());
    }

    private IEnumerator ShowPendingRewardRoutine()
    {
        _pendingRewardPopupRunning = true;

        // ① 等主界面出来（首启场景：还在加载页/引导页，大厅刚创建时 PrimaryInterface 还没显示）
        float waited = 0f;
        while (waited < RewardPopupTimeout && !IsLobbyReady())
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        // ② 设置页还开着时：
        //    · 设置界面里同意的（_showRewardOverSettings）→ 直接收掉设置页并立刻弹 AwardScreen，
        //      不等玩家手动关（AwardScreen 在 PopPanel，设置页在 UpPopPanel，不收掉会被挡在后面）；
        //    · 其它路径（授权回调迟到 / 启动静默核对补发）→ 仍等设置页自己关掉再弹。
        bool overSettings = _showRewardOverSettings;
        _showRewardOverSettings = false;

        if (SystemManager.Instance == null)
        {
            _pendingRewardPopupRunning = false;
            Debug.LogWarning("[AdTracking] SystemManager 为空，金币弹窗跳过");
            yield break;
        }

        if (overSettings && IsSettingsShowing())
        {
            SystemManager.Instance.HidePanel(PanelType.OptionsView);

            float closeWait = 0f;
            while (closeWait < 0.6f && IsSettingsShowing())
            {
                closeWait += Time.unscaledDeltaTime;
                yield return null;
            }

            Debug.Log("[AdTracking] 设置页内同意 → 收起设置页，直接弹 AwardScreen 发放奖励");
        }
        else if (!overSettings)
        {
            waited = 0f;
            while (waited < RewardPopupWaitTimeout && IsSettingsShowing())
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        if (_pendingRewardGold <= 0)
        {
            _pendingRewardPopupRunning = false;
            yield break;
        }

        int gold = _pendingRewardGold;
        _pendingRewardGold = 0;

        // ★ AwardScreen 展示"非商店奖励"的方式：静态 OverrideRewards（同分享奖励），
        //   面板关闭时自己会清空。这里只放一项金币。
        AwardScreen.OverrideRewards = new List<RewardItem>
        {
            new RewardItem { type = (int)LimitRewordType.Coins, amount = gold }
        };

        UIWindow window = SystemManager.Instance.ShowPanel(PanelType.AwardScreen);
        AwardScreen screen = window != null ? window.GetComponent<AwardScreen>() : null;
        if (screen == null)
        {
            AwardScreen.OverrideRewards = null;
            _pendingRewardPopupRunning = false;
            Debug.LogWarning("[AdTracking] AwardScreen 加载失败，金币已直接发放");
            yield break;
        }

        Debug.Log($"[AdTracking] 广告追踪激励弹窗已弹出（AwardScreen，+{gold} 金币，来源=设置页内同意:{overSettings}）");
        EventDispatcher.instance.TriggerChangeGoldUI(0, true);

        _pendingRewardPopupRunning = false;
    }

    /// <summary>大厅主页是否已经显示（金币弹窗挂在这里）</summary>
    private static bool IsLobbyReady()
    {
        return SystemManager.Instance != null
               && SystemManager.Instance.PanelIsShowing(PanelType.PrimaryInterface);
    }

    /// <summary>设置页是否还开着（从系统设置返回后要先等它关掉再弹金币）</summary>
    private static bool IsSettingsShowing()
    {
        return SystemManager.Instance != null
               && SystemManager.Instance.PanelIsShowing(PanelType.OptionsView);
    }

    private int ResolveRewardGold()
    {
        return TryResolveStimulate(out bool enabled, out int gold) && enabled ? gold : 0;
    }

    /// <summary>读取分层配置 AdTrackingStimulate（S_G）</summary>
    private bool TryResolveStimulate(out bool enabled, out int gold)
    {
        enabled = false;
        gold = 0;
        if (ShopManager.shopManager == null) return false;
        return ShopManager.shopManager.TryGetAdTrackingStimulate(out enabled, out gold);
    }

    #endregion

    #region 工具

    private static UserData User => GameDataManager.Instance != null ? GameDataManager.Instance.UserData : null;

    /// <summary>
    /// 编辑器调试用：清掉"已弹过 / 已领奖 / 模拟授权状态"，重新进游戏就会再弹一次引导页。
    /// 只在编辑器生效（真机不编译这段）。
    /// </summary>
    public static void ResetGuideStateForDebug()
    {
#if UNITY_EDITOR
        PlayerPrefs.DeleteKey("att_mock_status");
        PlayerPrefs.DeleteKey("att_mock_deny");
        PlayerPrefs.Save();

        UserData user = User;
        if (user != null)
        {
            user.adTrackingGuideShown = false;
            user.adTrackingRewardClaimed = false;
            user.adTrackingEnabled = "";
            if (GameDataManager.Instance != null) GameDataManager.Instance.CommitGameData();
        }

        Debug.Log("[AdTracking] 调试重置完成：下次进入加载页会重新弹出广告追踪引导页");
#endif
    }

    private void RefreshUserLayer(bool granted)
    {
        if (ShopManager.shopManager == null) return;

        string layer = ShopManager.shopManager.GetCurrentUserLayer();
        ShopManager.shopManager.RefreshLayer();
        Debug.Log($"[AdTracking] 授权状态写入 adTrackingEnabled={(granted ? 1 : 0)}，刷新用户分层 → {layer}");
    }

    private Transform ResolveUiParent(Transform preferred)
    {
        if (preferred != null)
        {
            Canvas canvas = preferred.GetComponentInParent<Canvas>();
            return canvas != null ? canvas.transform : preferred;
        }

        if (SystemManager.Instance != null && SystemManager.Instance._uiRoot != null)
        {
            Canvas canvas = SystemManager.Instance._uiRoot.GetComponentInParent<Canvas>();
            return canvas != null ? canvas.transform : SystemManager.Instance._uiRoot;
        }

        Canvas any = FindObjectOfType<Canvas>();
        return any != null ? any.transform : null;
    }

    private static string Localize(string key)
    {
        string value = MultilingualManager.Instance != null
            ? MultilingualManager.Instance.GetString(key)
            : key;
        return string.IsNullOrEmpty(value) ? key : value.Replace("\\n", "\n");
    }

    // 埋点在编辑器/未初始化时可能为空，统一保护，避免引导流程因埋点异常中断
    private static void ReportPopShow(string popName)
    {
        if (Game.self == null || Game.self.Analytics == null) return;
        AnalyticMgr.PopShow(popName);
    }

    /// <summary>
    /// 带重试的 Pop_up 上报：首启引导页出现得很早（加载页 1.2s 后），
    /// 埋点 SDK / Analytics 实例可能还没就绪，直接调就会把这条曝光丢掉（bug44）。
    /// 这里等到就绪再报，最多等 <see cref="PopReportRetryTimeout"/> 秒。
    /// </summary>
    private void ReportPopShowSafe(string popName)
    {
        if (Game.self != null && Game.self.Analytics != null && AnalyticMgr.IsSdkReady)
        {
            AnalyticMgr.PopShow(popName);
            return;
        }

        Debug.LogWarning($"[AdTracking] 埋点 SDK 未就绪，Pop_up({popName}) 延迟重试");
        StartCoroutine(ReportPopShowWhenReady(popName));
    }

    private IEnumerator ReportPopShowWhenReady(string popName)
    {
        float waited = 0f;
        while (waited < PopReportRetryTimeout)
        {
            if (Game.self != null && Game.self.Analytics != null && AnalyticMgr.IsSdkReady)
            {
                AnalyticMgr.PopShow(popName);
                Debug.Log($"[AdTracking] 埋点就绪，补报 Pop_up({popName})，等待 {waited:F2}s");
                yield break;
            }

            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        // 兜底：等不到就绪状态也补报一次（宁可时间偏移，也不要整条曝光丢失）
        Debug.LogError($"[AdTracking] 埋点等待 {PopReportRetryTimeout}s 仍未就绪，Pop_up({popName}) 强制补报");
        if (Game.self != null && Game.self.Analytics != null)
            AnalyticMgr.PopShow(popName);
    }

    private static void ReportPopAccept(string popName)
    {
        if (Game.self == null || Game.self.Analytics == null) return;
        AnalyticMgr.PopAccept(popName);
    }

    private static void ReportPopRefuse(string popName)
    {
        if (Game.self == null || Game.self.Analytics == null) return;
        AnalyticMgr.PopRefuse(popName);
    }

    #endregion
}
