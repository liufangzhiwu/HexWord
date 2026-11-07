using System;
using System.Collections;
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
///   ③ 广告追踪激励 S_G：开启后奖励 G 金币（只发一次），奖励 UI 挂在设置开关与引导页按钮上，
///      回游戏后用"领取金币奖励"弹窗（AdsAwardScreen）表现。
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

    /// <summary>等待主界面出现以展示激励弹窗的最长时间</summary>
    private const float RewardPopupTimeout = 15f;

    /// <summary>静默查询授权状态的最小间隔（秒）。启动 / 每次回前台都可能触发，做个节流。</summary>
    private const float StatusQueryThrottle = 3f;

    /// <summary>静默查询等待回包的最长时间（秒）。超时视为桥接不可用/回包丢失，允许重试。</summary>
    private const float StatusQueryTimeout = 5f;

    /// <summary>Pop_up 等待埋点就绪的最长时间（秒）。首启引导页出现得早，超时只打错误日志。</summary>
    private const float PopReportRetryTimeout = 10f;

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

    /// <summary>静默查询授权状态是否在进行中（鸿蒙是异步回包，避免连点/重入）</summary>
    private bool _statusQueryInFlight;

    /// <summary>上次静默查询的时间（realtimeSinceStartup），用于节流</summary>
    private float _lastStatusQueryTime = -999f;

    private int _pendingRewardGold;

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
    /// 玩法设置里是否要出现"广告关联"开关：
    /// 仅在"玩家拒绝过授权"且分层配置 AdTrackingStimulate 的 S=1 时出现。
    /// </summary>
    public bool ShouldShowSettingsEntry()
    {
        if (!PlatformSupportsTracking) return false;
        if (Status != AdTrackingStatus.Denied) return false;
        return TryResolveStimulate(out bool enabled, out int _) && enabled;
    }

    /// <summary>设置开关上要展示的激励金币数（没有激励返回 0）</summary>
    public int SettingsEntryRewardGold
    {
        get
        {
            if (!TryResolveStimulate(out bool enabled, out int gold)) return 0;
            return enabled ? gold : 0;
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
    /// 只在"已经决定过"（存档为 1 或 0）时才查：
    ///   - 存档为空表示还没走过引导页，查询有可能会拉起系统授权框；这种情况交给引导页流程处理。
    /// </summary>
    /// <param name="reason">日志用（启动 / 回前台）</param>
    public void SyncTrackingStatusFromPlatform(string reason)
    {
        UserData user = User;
        if (user == null) return;

        // 还没决定过授权 → 交给引导页流程，别在这里拉系统弹窗
        if (user.adTrackingEnabled != "1" && user.adTrackingEnabled != "0")
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
        user.adTrackingEnabled = want;
        if (GameDataManager.Instance != null) GameDataManager.Instance.CommitGameData();

        string layer = ShopManager.shopManager != null ? ShopManager.shopManager.GetCurrentUserLayer() : "(ShopManager 未初始化)";
        if (ShopManager.shopManager != null) ShopManager.shopManager.RefreshLayer();

        Debug.Log($"[AdTracking] {reason}: 授权状态 {before} → {want}（系统真实状态），已刷新用户分层 → {layer}");
        OnAuthorizationChanged?.Invoke(granted);
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

        // ★ bug34：启动时先静默核对系统里的真实授权状态（玩家可能在系统设置里改过），
        //   状态变了会写回存档并刷新分层。必须放在判断要不要弹引导页之前，
        //   这样"已经弹过引导页"的玩家也能拿到正确的分层。
        SyncTrackingStatusFromPlatform("启动");

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
            Debug.LogWarning("[AdTracking] 等不到 UserData，本次不弹引导（广告请求保持挂起）");
            if (ShopManager.shopManager != null) ShopManager.shopManager.LogStartupLayerAndConfig();
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
    /// 授权流程一结束就放行广告请求（鸿蒙）。
    /// 隐私还没同意时继续挂起：广告请求会触发 AdsKit 的 OAID 获取，等于绕过隐私弹窗。
    /// </summary>
    private static void ReleaseAdGate()
    {
        UserData user = User;
        if (user != null && !user.IsAgreePrivacy)
        {
            Debug.Log("[AdTracking] 隐私协议未同意，广告请求继续挂起");
            return;
        }

        if (Game.self != null) Game.self.AllowAdRequests();
    }


    #endregion

    #region 设置入口（广告关联）

    /// <summary>设置里点"广告关联"开关：出引导页（去开启模式）</summary>
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

        // 已经开启：直接刷新开关即可，不再打扰
        if (IsAuthorized)
        {
            Debug.Log("[AdTracking] 广告关联已开启，跳过跳转设置");
            OnAuthorizationChanged?.Invoke(true);
            return;
        }

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

        // 同一个结果重复回调（鸿蒙侧可能既走 TuanjieSendMessage 又走 Promise；
        // 登录补写也会调进来）：状态没变就不重复写盘/重复发奖
        if (!wasAwaiting && user.adTrackingEnabled == (granted ? "1" : "0")) return;

  
        _waitingSettingsReturn = false;

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
        if (wasAwaiting && granted) GrantReward();
    }

    
    #endregion

    #region 跳系统设置 & 回前台校验

    private void OpenSystemTrackingSettings()
    {
        _openedSystemSettings = true;

        if (AdTrackingPermission.IsSupported)
        {
            AdTrackingPermission.OpenSystemSettings();
            return;
        }

#if UNITY_OPENHARMONY
        // 鸿蒙没有统一的"广告追踪设置"深链：重新发起一次授权请求。
        // 若玩家之前拒绝过（系统不再弹标准授权框），ArkTS 侧会自动改拉
        // requestPermissionOnSetting 权限设置弹窗（带开关，官方二次授权路径），
        // 结果仍是 OnOAIDClosedSignal 回包；玩家取消/未开启则引导页保持"去开启"状态。
        if (Game.self != null) Game.self.RequestTrackingConsent();
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
    /// 鸿蒙兜底：从系统设置回前台后，如果 3 秒内没有收到授权回调，先按当前存档状态刷新一次设置开关。
    /// 注意：只刷新 UI，不清 _waitingSettingsReturn / _openedSystemSettings ——
    /// 真正的授权回调（OnOAIDClosedSignal）回来时仍按"设置入口发起"处理，埋点与激励不会丢。
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

        Debug.Log("[AdTracking] 鸿蒙系统设置返回后 3s 内无授权回调，先按当前存档状态刷新设置开关");
        OnAuthorizationChanged?.Invoke(IsAuthorized);
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
            _waitingSettingsReturn = false;
            _openedSystemSettings = false;
            if (_guide != null) _guide.SetBusy(false);
            MessageSystem.Instance?.ShowTip("尚未开启广告关联");
        }
    }

    #endregion

    #region 激励（S_G）

    private void GrantReward()
    {
        if (!TryResolveStimulate(out bool enabled, out int gold)) return;
        if (!enabled || gold <= 0) return;

        UserData user = User;
        if (user == null || user.adTrackingRewardClaimed) return;

        user.adTrackingRewardClaimed = true;   // 只能领一次
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

    /// <summary>进入游戏后补弹"领取金币"弹窗（复用 AdsAwardScreen 的表现）</summary>
    public void TryShowPendingReward()
    {
        if (_pendingRewardGold <= 0) return;
        StartCoroutine(ShowPendingRewardRoutine());
    }

    private IEnumerator ShowPendingRewardRoutine()
    {
        float waited = 0f;
        while (waited < RewardPopupTimeout &&
               (SystemManager.Instance == null || !SystemManager.Instance.PanelIsShowing(PanelType.PrimaryInterface)))
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (_pendingRewardGold <= 0) yield break;
        if (SystemManager.Instance == null)
        {
            Debug.LogWarning("[AdTracking] SystemManager 为空，金币弹窗跳过");
            yield break;
        }

        int gold = _pendingRewardGold;
        _pendingRewardGold = 0;

        UIWindow window = SystemManager.Instance.ShowPanel(PanelType.AdsAwardScreen);
        AdsAwardScreen screen = window != null ? window.GetComponent<AdsAwardScreen>() : null;
        if (screen == null)
        {
            Debug.LogWarning("[AdTracking] AdsAwardScreen 加载失败，金币已直接发放");
            yield break;
        }

        string tip = Localize("AdTrackingRewardTip").Replace("{0}", gold.ToString());
        screen.SetContent(false, gold, tip, null);
        EventDispatcher.instance.TriggerChangeGoldUI(0, true);
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
