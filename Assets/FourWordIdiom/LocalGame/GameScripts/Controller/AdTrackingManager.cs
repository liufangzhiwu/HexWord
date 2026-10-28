using System;
using System.Collections;
using Middleware;
using UnityEngine;

/// <summary>
/// 广告追踪（关联应用）引导 / 权限 / 激励的统一流程编排。
///
/// 需求对应关系：
///   ① 首次启动（隐私弹完之后、加载页之上）出引导页 → 点"继续"出系统权限弹窗（盖在引导页上层）
///      → 授权结果回来后仍停在引导页 → 再点"继续"关闭引导页，继续走加载流程进游戏。
///   ② 拒绝授权的玩家，玩法设置里出现"广告关联"开关（S=1 时才出现），点开关 → 引导页"去开启"
///      → 跳系统广告追踪设置页 → 回前台校验结果。
///   ③ 广告追踪激励 S_G：开启后奖励 G 金币（只发一次），奖励 UI 挂在设置开关与引导页按钮上，
///      回游戏后用"领取金币奖励"弹窗（AdsAwardScreen）表现。
///
/// 平台：
///   iOS        → AdTrackingPermission（ATT 原生桥）
///   鸿蒙        → Game.LoadAds_OAID（ArkTS 侧 APP_TRACKING_CONSENT），结果经 PushMessageReceiver 回到 OnPermissionResult
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

    /// <summary>等待系统授权结果的最长时间（鸿蒙没有回调时的兜底）</summary>
    private const float PermissionTimeout = 25f;

    /// <summary>等待主界面出现以展示激励弹窗的最长时间</summary>
    private const float RewardPopupTimeout = 15f;

    #endregion

    #region 运行时状态

    private AdTrackingGuidance _guide;
    private AdTrackingGuidance.Mode _guideMode;
    private Transform _guideParent;

    private bool _startupFlowFinished = true;
    private bool _startupFlowStarted;
    private bool _permissionRequested;
    private bool _awaitingPermissionResult;
    private bool _waitingSettingsReturn;
    private bool _openedSystemSettings;

    private int _pendingRewardGold;

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

    #region 启动流程（加载页）

    /// <summary>加载页出现时调用：决定要不要出引导页，并让加载流程等它结束</summary>
    public void BeginStartupFlow(Transform uiParent)
    {
        if (_startupFlowStarted) return;   // 一次进程内只走一次

        _guideParent = uiParent;

        // UserData 还没就绪属于时序问题，不占用"只走一次"的机会，等下次加载页激活再试
        if (User == null)
        {
            Debug.LogWarning("[AdTracking] UserData 还没加载，本次跳过，等下次加载页再弹");
            return;
        }

        _startupFlowStarted = true;

        if (!ShouldRunStartupFlow(out string skipReason))
        {
            // 系统里已有授权结果 / 已经弹过 / 隐私还没同意 → 不弹引导，但鸿蒙仍要先拉起 Banner
            Debug.Log($"[AdTracking] 本次加载页不弹广告追踪引导：{skipReason}");
            if (Status != AdTrackingStatus.Denied) LoadBannerIfNeeded();
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
            Debug.LogWarning("[AdTracking] 等待引导流程超时，强制放行加载");
            _startupFlowFinished = true;
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

        ReportPopShow(PopNameGuide);
        _permissionRequested = false;

        // 首启时账号/商店可能还没初始化完，先按当前能拿到的激励展示，拿到配置后再补 "+G"
        ShowGuide(AdTrackingGuidance.Mode.Startup, ResolveRewardGold());
        StartCoroutine(RefreshGuideRewardWhenReady());
    }

    /// <summary>分层配置晚于引导页就绪时，把"+G 金币"标签补上（不影响弹窗时机）</summary>
    private IEnumerator RefreshGuideRewardWhenReady()
    {
        float waited = 0f;
        while (waited < LayerConfigWait)
        {
            if (_guide == null) yield break;

            int gold = ResolveRewardGold();
            if (gold > 0)
            {
                _guide.RefreshReward(gold);
                yield break;
            }

            waited += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void FinishStartupFlow()
    {
        _startupFlowFinished = true;
    }

    #endregion

    #region 设置入口（广告关联）

    /// <summary>设置里点"广告关联"开关：出引导页（去开启模式）</summary>
    public void OpenSettingsGuide(Transform uiParent = null)
    {
        if (_guide != null) return;

        if (uiParent != null) _guideParent = uiParent;
        _permissionRequested = false;
        ReportPopShow(PopNameGuideSettings);
        ShowGuide(AdTrackingGuidance.Mode.Settings, ResolveRewardGold());
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
    }

    private void HandleGuidePrimary()
    {
        if (_guide == null) return;

        if (_guideMode == AdTrackingGuidance.Mode.Startup)
        {
            // 第一次"继续" → 请求系统权限；第二次"继续" → 关闭引导页，继续加载进游戏
            if (!_permissionRequested)
            {
                _permissionRequested = true;
                _guide.SetBusy(true);
                RequestPermission();
            }
            else
            {
                CloseGuide();
                FinishStartupFlow();
            }
            return;
        }

        // 设置入口："去开启" → 跳系统广告追踪设置页
        _waitingSettingsReturn = true;
        _guide.SetBusy(true);
        OpenSystemTrackingSettings();
    }

    private void CloseGuide()
    {
        if (_guide == null) return;

        _guide.OnPrimaryClicked -= HandleGuidePrimary;
        _guide.Close();
        _guide = null;
    }

    #endregion

    #region 权限请求与结果

    /// <summary>发起系统授权请求（iOS 弹窗 / 鸿蒙权限框）</summary>
    private void RequestPermission()
    {
        _awaitingPermissionResult = true;

        // 系统权限弹窗弹出（原生弹窗的"弹出"时机拿不到，用发起时刻兜底，保证漏斗有头）
        ReportPopShow(PopNameTracking);

        if (AdTrackingPermission.IsSupported)
        {
            AdTrackingPermission.Request(status => OnPermissionResult(status == AdTrackingStatus.Authorized));
            return;
        }

#if UNITY_OPENHARMONY && !UNITY_EDITOR
        // 鸿蒙：LoadBanner 内部会拉起 APP_TRACKING_CONSENT 授权框，
        // 结果由 ArkTS 侧 TuanjieSendMessage → PushMessageReceiver.OnOAIDClosedSignal 回到 Unity
        if (Game.self != null) Game.self.LoadAds_OAID();
        StartCoroutine(PermissionTimeoutRoutine());
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

        bool fromSystemDialog = _awaitingPermissionResult;
        bool fromSettings = _waitingSettingsReturn;
        bool wasAwaiting = fromSystemDialog || fromSettings || _openedSystemSettings;

        _awaitingPermissionResult = false;
        _waitingSettingsReturn = false;

        user.adTrackingEnabled = granted ? "1" : "0";
        if (GameDataManager.Instance != null) GameDataManager.Instance.CommitGameData();

        // ---------- 埋点（通用数数方案：系统权限弹窗结果） ----------
        if (fromSystemDialog)
        {
            if (granted) ReportPopAccept(PopNameTracking);
            else ReportPopRefuse(PopNameTracking);
        }
        else if (_openedSystemSettings && granted)
        {
            // 从系统设置里手动打开，也算一次"同意"，保证漏斗有出口
            ReportPopAccept(PopNameTracking);
        }

        _openedSystemSettings = false;

        // ---------- 分层刷新（广告追踪权限是 IAA 分层依据） ----------
        RefreshUserLayer(granted);

        if (_guide != null) _guide.OnPermissionReturned(granted);

        // ---------- 通知界面（设置里的"广告关联"开关） ----------
        OnAuthorizationChanged?.Invoke(granted);

        // ---------- 激励发放（同意才发，且只发一次） ----------
        if (wasAwaiting && granted) GrantReward();
    }

    private IEnumerator PermissionTimeoutRoutine()
    {
        yield return new WaitForSeconds(PermissionTimeout);
        if (!_awaitingPermissionResult) yield break;

        Debug.LogWarning("[AdTracking] 等待系统授权结果超时，解锁引导页按钮");
        _awaitingPermissionResult = false;
        _waitingSettingsReturn = false;
        _openedSystemSettings = false;
        if (_guide != null) _guide.SetBusy(false);
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

#if UNITY_OPENHARMONY && !UNITY_EDITOR
        // 鸿蒙没有统一的"广告追踪设置"深链：重新发起一次授权请求，
        // 若玩家之前选了"不再询问"，系统会直接返回拒绝，此时引导页保持"去开启"状态。
        if (Game.self != null) Game.self.LoadAds_OAID();
        StartCoroutine(PermissionTimeoutRoutine());
#else
        MessageSystem.Instance?.ShowTip("请在系统设置中开启广告关联");
#endif
    }

    private void OnFocusChanged(bool hasFocus)
    {
        if (!hasFocus) return;
        if (!_waitingSettingsReturn) return;
        if (!AdTrackingPermission.IsSupported) return; // 鸿蒙靠回调，不轮询

        StartCoroutine(CheckSettingsResultRoutine());
    }

    private IEnumerator CheckSettingsResultRoutine()
    {
        yield return new WaitForSeconds(0.4f); // 等系统设置页动画结束
        if (!_waitingSettingsReturn) yield break;

        if (IsAuthorized)
        {
            OnPermissionResult(true);
            CloseGuide();
            FinishStartupFlow();
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
            AnalyticMgr.ResourceChange(ResId.Gold, gold, ResReason.AdTrackingReward, string.Empty);
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

    private void LoadBannerIfNeeded()
    {
#if UNITY_OPENHARMONY && !UNITY_EDITOR
        if (Game.self != null) Game.self.LoadAds_OAID();
#endif
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
