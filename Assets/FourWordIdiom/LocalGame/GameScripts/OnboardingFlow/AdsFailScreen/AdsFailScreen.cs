using System;
using System.Collections;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 广告播放失败弹窗（AdsFailScreen / PanelType.AdFailed）。
/// 当激励视频达到「假失败」阈值（分层策略 S_F_T_A_B_C）时由
/// ShopManager.ShowAdFailedPanel 弹出，提供两条转化路径：
///
///   1. 分享换奖励（需求 C-STRAT-06）：分享后切回前台且间隔 > 3 秒判成功，
///      发灯泡1/复活1/金币50（ShopManager.OnShareReturned，toast
///      「分享成功 +1灯泡 +1复活 +50金币」），成功后关闭本弹窗；
///      每天限 F 次，达限后隐藏分享按钮；
///      判定信号：OnApplicationFocus / OnApplicationPause（切出/切回）+
///      鸿蒙面板 dismiss 兜底（见 ShareHarmonyProxy.tslib），延时去抖；
///      系统分享实现：Android Intent / 鸿蒙 Share Kit（ShareHarmonyProxy.tslib）；
///   2. 单道具商品按钮（需求 C-STRAT-07）：按触发道具类型映射商品
///      （灯泡→SingleGoods、复活→SingleClockS、金币→Gold6），
///      点击直接调起系统支付，购买成功复用 ShopManager.OnPurchaseSuccess（发奖 + AwardScreen）。
///
/// 弹出时由 ShopManager 记录触发道具类型（LastAdFailedToolType），本面板据此切换内容。
/// </summary>
public class AdsFailScreen : UIWindow
{
    [Header("基础")]
    [SerializeField] private Button closeBtn;       // CloseBtn：关闭
    [SerializeField] private Text title;            // HeaderText：标题
    [SerializeField] private Text descText;         // 描述文案
    [SerializeField] private Image toolIcon;        // 道具主图标（Icon）
    [SerializeField] private Image toolIcon2;       // 道具次图标（icon，可选）
    [SerializeField] private Image buytoolIcon;       // 道具次图标（icon，可选）

    [Header("路径1：分享换奖励")]
    [SerializeField] private Button ShareBtn;       // 分享按钮（每天 F 次，达限隐藏）
    [SerializeField] private Text shareBtnText;     // 分享按钮文案（shareText）
    [SerializeField] private Text countText;        // 剩余分享次数 / 商品数量（countText）

    [Header("路径2：单道具商品")]
    [SerializeField] private Button BuyBtn;         // 单道具商品按钮
    [SerializeField] private Text priceText;        // 商品价格（price）
    [SerializeField] private Text discountText;     // 折扣标签（discountText，取内购配置表）
    [SerializeField] private Transform discountTable; // 折扣标签容器（discountbg）

    // ======================= 分享状态（仅本面板生命周期内） =======================

    private bool _sharePending;        // 已点击分享、等待判定
    private bool _shareFocusLost;      // 分享后是否真的切出过前台
    private float _shareClickRealtime; // 点击时刻（realtimeSinceStartup）
    private int _shareDecisionToken;   // 判定定时器代号（撤销挂起的判定用）
    private bool _shareDecisionPending; // 是否有挂起的判定
    private bool _shareDecisionStrict;  // 挂起判定是否为「切回前台」严格判定

    /// <summary>分享成功判定最小间隔（秒），与 ShopManager.ShareSuccessMinSeconds 保持一致</summary>
    private const float ShareSuccessMinSeconds = 3f;

    /// <summary>切回前台后延时判定（秒），吸收分享面板开合造成的假回切抖动</summary>
    private const float ShareDecisionDelaySeconds = 0.5f;

    /// <summary>鸿蒙面板 dismiss 后兜底判定延时（秒），留出「切到目标应用」生命周期信号的到达窗口</summary>
    private const float ShareDismissDecisionDelaySeconds = 1.0f;

    protected override void OnEnable()
    {
        base.OnEnable();
        ResetShareState();
        InitUI();

#if UNITY_OPENHARMONY && !UNITY_EDITOR
        // ArkTS(UI 线程) 通过 TuanjieSendMessage -> HarmonyShareSignalReceiver 回传信号
        HarmonyShareSignalReceiver.SignalReceived -= OnHarmonyShareSignalFromUnity;
        HarmonyShareSignalReceiver.SignalReceived += OnHarmonyShareSignalFromUnity;
#endif
    }

    protected override void InitializeUIComponents()
    {
        if (closeBtn != null) closeBtn.AddVibraClickAction(OnCloseClick);
        if (BuyBtn != null) BuyBtn.AddVibraClickAction(OnBuyClick);
        if (ShareBtn != null) ShareBtn.AddVibraClickAction(OnShareClick);
    }

    /// <summary>应用切走/切回前台（Android chooser / 鸿蒙切到目标应用）。</summary>
    private void OnApplicationFocus(bool hasFocus)
    {
        HandleShareForeground(hasFocus);
    }

    /// <summary>
    /// 应用暂停/恢复。鸿蒙端优先依赖该信号：系统分享面板是模态子窗口，
    /// 开合不一定触发失焦，但切到目标应用必走 onBackground/onForeground
    /// （TuanjiePlayerAbilityBase → nativeOnPause/nativeOnResume）。
    /// </summary>
    private void OnApplicationPause(bool paused)
    {
        HandleShareForeground(!paused);
    }

    /// <summary>前后台信号统一入口：切出记「离开过」并撤销挂起判定；切回延时判定。</summary>
    private void HandleShareForeground(bool foreground)
    {
        if (!_sharePending) return;

        if (!foreground)
        {
            // 真正切出前台：记「离开过」；离开信号优先，撤销挂起的判定
            _shareFocusLost = true;
            CancelShareDecision();
            return;
        }

        // 切回前台：延时判定，吸收「分享面板开合 + 焦点抖动」造成的假回切
        ScheduleShareDecision(false);
    }

    /// <summary>
    /// 延时判定分享结果。
    /// dismissTriggered=false（切回前台触发）：严格要求「真的切出过」再判成功；
    /// dismissTriggered=true（鸿蒙面板 dismiss 兜底）：仅按间隔判定，
    /// 覆盖切目标应用时生命周期信号缺失的情况（与 Android chooser 取消防洞同级）。
    /// </summary>
    private void ScheduleShareDecision(bool dismissTriggered)
    {
        // 已有「切回前台」严格判定在排队时，不被 dismiss 兜底判定顶掉
        if (_shareDecisionPending && _shareDecisionStrict && dismissTriggered) return;

        CancelShareDecision();
        _shareDecisionPending = true;
        _shareDecisionStrict = !dismissTriggered;
        int token = ++_shareDecisionToken;
        float delay = dismissTriggered ? ShareDismissDecisionDelaySeconds : ShareDecisionDelaySeconds;
        UnityTimer.Delay(delay, () =>
        {
            if (token != _shareDecisionToken) return; // 已被新信号撤销
            _shareDecisionPending = false;
            FinalizeShare(dismissTriggered);
        });
    }

    private void CancelShareDecision()
    {
        _shareDecisionToken++;
        _shareDecisionPending = false;
    }

    /// <summary>判定分享结果并收口（发奖 + toast / 失败提示 / 关闭弹窗）。</summary>
    private void FinalizeShare(bool dismissTriggered)
    {
        if (!_sharePending) return;

        float elapsed = Time.realtimeSinceStartup - _shareClickRealtime;
        bool success = elapsed >= ShareSuccessMinSeconds
                       && (dismissTriggered || _shareFocusLost);

        Debug.Log($"[AdsFail] 分享判定: success={success}, trigger={(dismissTriggered ? "dismiss" : "foreground")}, " +
                  $"elapsed={elapsed:0.0}s, focusLost={_shareFocusLost}");

        ApplyShareResult(success);
    }

    /// <summary>
    /// 统一收口：成功发奖（OnShareReturned 弹「分享成功 +1灯泡 +1复活 +50金币」）后关闭本弹窗；
    /// 失败弹「分享失败」并刷新按钮。
    /// </summary>
    private void ApplyShareResult(bool success)
    {
        if (!_sharePending) return;
        _sharePending = false;
        CancelShareDecision();

        // 判定收口：让 ArkTS 侧释放 ShareController，避免系统分享单例残留
#if UNITY_OPENHARMONY && !UNITY_EDITOR
        ResetHarmonyShareProxy();
#endif

        bool granted = false;
        if (ShopManager.shopManager != null)
            granted = ShopManager.shopManager.OnShareReturned(success); // 内部校验每日上限/间隔并发奖+toast

        if (success && granted)
        {
            Close(); // 成功：toast 已由 OnShareReturned 弹出，关闭 AdsFailScreen 弹窗
            return;
        }

        if (!success)
            MessageSystem.Instance.ShowTip(GetLocalized("ShareFailTip", "分享失败，再试试吧"));

        RefreshShareUI(); // 刷新剩余次数/按钮显隐
    }

    private void OnDisable()
    {
        ResetShareState();

#if UNITY_OPENHARMONY && !UNITY_EDITOR
        HarmonyShareSignalReceiver.SignalReceived -= OnHarmonyShareSignalFromUnity;
#endif
    }

    // ======================= UI 初始化 =======================

    private void InitUI()
    {
        LimitRewordType toolType = ShopManager.shopManager != null
            ? ShopManager.shopManager.LastAdFailedToolType
            : LimitRewordType.Tipstool;

        if (title != null)
            title.text = GetLocalized("AdFailedTitle", "换种方式");

        if (descText != null)
            descText.text = GetLocalized("AdFailedDes", "视频暂不可用，换种方式获取吧");

        // 道具图标按触发类型切换
        Sprite iconSprite = GetSprite(toolType);
        if (iconSprite != null)
        {
            if (toolIcon != null) toolIcon.sprite = iconSprite;
            if (toolIcon2 != null) toolIcon2.sprite = iconSprite;
            if (buytoolIcon != null) buytoolIcon.sprite = iconSprite;
        }

        RefreshGoodsUI(toolType);
        RefreshShareUI();

        if (closeBtn != null)
        {
            var btnText = closeBtn.GetComponentInChildren<Text>();
            if (btnText != null)
                btnText.text = GetLocalized("Close", "关闭");
        }
    }

    /// <summary>分享按钮：每天 F 次，达限（或策略关闭）后隐藏。</summary>
    private void RefreshShareUI()
    {
        if (ShareBtn == null) return;

        bool available = ShopManager.shopManager != null
                         && ShopManager.shopManager.IsShareRewardAvailable();
        ShareBtn.gameObject.SetActive(available);

        if (available && countText != null && ShopManager.shopManager != null)
        {
            var p = ShopManager.shopManager.GetAdFailedParams();
            int remain = ShopManager.shopManager.GetShareRemainCount();
            countText.gameObject.SetActive(true);
            // 显示「剩余次数/上限次数」，如 1/2
            countText.text = string.Format(GetLocalized("ShareRemainFormat", "{0}/{1}"), remain, p.shareDailyLimit);
        }
        else if (countText != null && !available)
        {
            countText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 单道具商品按钮：按触发道具类型映射商品并显示价格/折扣/图标。
    /// 灯泡→SingleGoods，复活→SingleClockS，商店金币→Gold6（需求 C-STRAT-07）。
    /// 商品未在商店表配置时隐藏按钮。
    /// </summary>
    private void RefreshGoodsUI(LimitRewordType toolType)
    {
        ShopDataItem item = GetContextGoodsItem(toolType);
        if (item == null)
        {
            if (BuyBtn != null) BuyBtn.gameObject.SetActive(false);
            if (discountText != null) discountText.gameObject.SetActive(false);
            if (discountTable != null) discountTable.gameObject.SetActive(false);
            if (priceText != null) priceText.gameObject.SetActive(false);
            return;
        }

        if (BuyBtn != null) BuyBtn.gameObject.SetActive(true);

        if (priceText != null)
        {
            priceText.gameObject.SetActive(item.price > 0);
            priceText.text = item.price > 0 ? string.Format("¥{0:0.##}", item.price) : "";
        }

        // 折扣标签文案取内购配置表（discountZh 优先）
        if (discountText != null)
        {
            string discount = !string.IsNullOrEmpty(item.discountZh) ? item.discountZh : item.discount;
            bool hasDiscount = !string.IsNullOrEmpty(discount);
            discountText.gameObject.SetActive(hasDiscount);
            if (hasDiscount) discountText.text = discount;
        }
        if (discountTable != null && discountText == null)
            discountTable.gameObject.SetActive(!string.IsNullOrEmpty(item.discountZh) || !string.IsNullOrEmpty(item.discount));

        // 商品内容数量（如「灯泡 x1」），无内容时隐藏
        if (countText != null && ShareBtn != null && !ShareBtn.gameObject.activeSelf)
        {
            int count = GetGoodsMainCount(item, toolType);
            countText.gameObject.SetActive(count > 0);
            if (count > 0)
                countText.text = string.Format("x{0}", count);
        }
    }

    /// <summary>根据 ShopManager 记录的道具类型取对应单道具商品配置。</summary>
    private ShopDataItem GetContextGoodsItem(LimitRewordType toolType)
    {
        if (ShopManager.shopManager == null) return null;

        string productId;
        switch (toolType)
        {
            case LimitRewordType.ReStartTool: productId = ShopManager.ProductSingleClock; break; // 复活
            case LimitRewordType.Coins:       productId = ShopManager.ProductGold6;      break; // 商店金币
            default:                          productId = ShopManager.ProductSingleGoods; break; // 灯泡（默认）
        }

        return ShopManager.shopManager.GetShopItemByProduceName(productId);
    }

    /// <summary>取商品内容中与触发道具类型对应的主道具数量（展示用）。</summary>
    private int GetGoodsMainCount(ShopDataItem item, LimitRewordType toolType)
    {
        if (item == null || item.productContent == null) return 0;
        foreach (var row in item.productContent)
        {
            if (row == null || row.Count < 2) continue;
            int type;
            int count;
            if (int.TryParse(row[0], out type) && int.TryParse(row[1], out count))
            {
                if (type == (int)toolType) return count;
            }
        }
        return 0;
    }

    // ======================= 事件 =======================

    private void OnCloseClick()
    {
        base.Close();
    }

    /// <summary>点击单道具商品：直接调起系统支付；成功走 ShopManager.OnPurchaseSuccess（发奖+AwardScreen）。</summary>
    private void OnBuyClick()
    {
        LimitRewordType toolType = ShopManager.shopManager != null
            ? ShopManager.shopManager.LastAdFailedToolType
            : LimitRewordType.Tipstool;

        ShopDataItem item = GetContextGoodsItem(toolType);
        if (item == null || Game.self == null || Game.self.Shop == null)
        {
            MessageSystem.Instance.ShowTip(GetLocalized("GoodsNotFoundTip", "商品暂不可用"));
            return;
        }

        Debug.Log($"[AdsFail] 发起单道具购买: {item.produceNameId}, price={item.price}");
        Game.self.Shop.Purchase(
            item.GetProduceName(),
            productItem =>
            {
                // 复用商店统一的购买成功处理（发放内容 + 内购成功弹窗 AwardScreen + 购买记录）
                if (ShopManager.shopManager != null)
                    ShopManager.shopManager.OnPurchaseSuccess(productItem);
                Close();
            },
            error =>
            {
                Debug.Log($"[AdsFail] 单道具购买失败/取消: {item.produceNameId}, error={error}");
                // 取消/失败不弹额外窗：取消购买弹窗由 ShopManager.OnPurchaseCanceled 统一调度
            });
    }

    /// <summary>点击分享：记录时刻 → 调起系统分享 → 等切回前台判定（OnApplicationFocus）。</summary>
    private void OnShareClick()
    {
        // 分享判定未收口前忽略重复点击：避免连点创建多个 ShareController，
        // 系统侧 ModalShareController 是进程级单例，多实例只会互相占用。
        if (_sharePending)
        {
            Debug.Log("[AdsFail] 分享进行中，忽略重复点击");
            return;
        }

        _sharePending = true;
        _shareFocusLost = false;
        _shareClickRealtime = Time.realtimeSinceStartup;

        if (ShopManager.shopManager != null)
            ShopManager.shopManager.OnShareClicked();

        DoNativeShare();
    }

    // ======================= 系统分享（无插件：Android Intent / 鸿蒙 Share Kit） =======================

#if UNITY_OPENHARMONY && !UNITY_EDITOR
    /// <summary>鸿蒙 ArkTS 分享桥接（ShareHarmonyProxy.tslib），懒加载。</summary>
    private OpenHarmonyJSObject _harmonyShareProxy;

    /// <summary>
    /// 鸿蒙分享信号待处理队列（只存字符串，避免在回调线程里访问 UnityEngine 对象）。
    /// 由 UI 线程 TuanjieSendMessage → HarmonyShareSignalReceiver 写入，Update 在主线程消费。
    /// </summary>
    private string _pendingHarmonySignal;

    /// <summary>本次分享文案（面板失败降级复制时复用）</summary>
    private string _harmonyShareText;

    /// <summary>UI 线程分享通道是否已确认可用（收到 uiStarted 后置 true）</summary>
    private bool _harmonyUiStarted;

    /// <summary>UI 线程通道无响应时，降级复制文案的等待时长（秒）</summary>
    private const float HarmonyUiStartTimeout = 1.5f;
#endif

#if UNITY_OPENHARMONY && !UNITY_EDITOR
    /// <summary>
    /// 主线程消费鸿蒙分享信号。
    /// OpenHarmonyJSCallback 可能不在 Unity 主线程触发，回调里只入队，
    /// 统一在 Update 里处理，避免跨线程访问 Unity 对象导致原生崩溃。
    /// </summary>
    private void Update()
    {
        ProcessPendingHarmonySignal();
    }
#endif

    private void DoNativeShare()
    {
        string shareText = GetLocalized("ShareText", "我在玩《成语消：禅意之境》，一起来消成语吧！");

#if UNITY_OPENHARMONY && !UNITY_EDITOR
        DoHarmonyNativeShare(shareText);
#elif UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent"))
            {
                intent.Call<AndroidJavaObject>("setAction", "android.intent.action.SEND");
                intent.Call<AndroidJavaObject>("setType", "text/plain");
                intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", shareText);
                using (AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent"))
                using (AndroidJavaObject chooser = intentClass.CallStatic<AndroidJavaObject>(
                    "createChooser", intent, GetLocalized("ShareTitle", "分享到")))
                {
                    activity.Call("startActivity", chooser);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[AdsFail] 调起系统分享失败: {e.Message}");
            MessageSystem.Instance.ShowTip(GetLocalized("ShareFailTip", "分享失败，再试试吧"));
            _sharePending = false;
        }
#elif UNITY_IOS && !UNITY_EDITOR
        // iOS 分享待接入（Social API / ShareSheet）；先按失败提示，避免卡判定
        Debug.Log("[AdsFail] iOS 分享未接入，走失败分支");
        MessageSystem.Instance.ShowTip(GetLocalized("ShareFailTip", "分享失败，再试试吧"));
        _sharePending = false;
#else
#if UNITY_EDITOR
        // 编辑器：无法真实分享，延时模拟成功判定，方便验证发奖+关闭弹窗链路
        Debug.Log("[AdsFail] 编辑器环境模拟分享成功（延时判定）");
        SimulateShareSuccess();
#else
        // 其他平台（Windows/WebGL 等）无原生分享实现：延时模拟成功，避免卡分享流程
        Debug.Log("[AdsFail] 当前平台无原生分享实现，模拟分享成功");
        SimulateShareSuccess();
#endif
#endif
    }

    /// <summary>
    /// 无原生分享通道时的模拟成功（编辑器 / Windows / WebGL）：
    /// 延时超过 ShareSuccessMinSeconds 后按成功收口（发奖 toast + 关闭弹窗），
    /// 避开 ShopManager.OnShareReturned 的 3 秒间隔校验。
    /// </summary>
    private void SimulateShareSuccess()
    {
        UnityTimer.Delay(ShareSuccessMinSeconds + 0.5f, () =>
        {
            if (!_sharePending) return;
            ApplyShareResult(true);
        });
    }

#if UNITY_OPENHARMONY && !UNITY_EDITOR
    /// <summary>
    /// 鸿蒙：调起系统分享面板（Share Kit / systemShare，见 ShareHarmonyProxy.tslib）。
    /// 成功判定与 Android 一致——切出前台且间隔 > 3 秒后切回（OnApplicationFocus/OnApplicationPause）；
    /// 附带 dismiss/showFail 回调：取消态快速收口、拉起失败提示。
    /// </summary>
    private void DoHarmonyNativeShare(string shareText)
    {
        _harmonyShareText = shareText;
        _harmonyUiStarted = false;

        try
        {
            EnsureHarmonyShareObjects();

            string title = GetLocalized("ShareTitle", "分享到");
            // ShareHarmonyProxy 只做转发：真正的 ShareKit 调用在 UI 线程的 ShareHelper.ts
            // 结果由 UI 侧 TuanjieSendMessage 回调，不再使用 OpenHarmonyJSCallback
            _harmonyShareProxy.Call("ShareText", shareText, title, null);
            Debug.Log("[AdsFail] 已请求 UI 线程拉起鸿蒙分享面板");

            // UI 线程通道异常（模块未加载/消息未通）时收不到 uiStarted，
            // 这里兜底降级为复制文案，避免 _sharePending 永久挂住。
            UnityTimer.Delay(HarmonyUiStartTimeout, () =>
            {
                if (this == null) return;
                if (_sharePending && !_harmonyUiStarted)
                {
                    Debug.LogWarning("[AdsFail] UI 线程分享通道无响应，降级复制文案");
                    DoHarmonyCopyShare(_harmonyShareText);
                }
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"[AdsFail] 请求鸿蒙分享失败，降级复制文案: {e.Message}");
            DoHarmonyCopyShare(shareText);
        }
    }

    /// <summary>懒加载 ArkTS 桥接对象，并确保信号接收 GameObject 常驻。</summary>
    private void EnsureHarmonyShareObjects()
    {
        if (_harmonyShareProxy == null)
            _harmonyShareProxy = new OpenHarmonyJSObject("ShareHarmonyProxy");

        // ArkTS 侧通过 TuanjieSendMessage("HarmonyShareReceiver", "OnShareSignal", signal) 回传，
        // 需要同名 GameObject 常驻场景。
        if (GameObject.Find(HarmonyShareReceiverName) == null)
        {
            var receiver = new GameObject(HarmonyShareReceiverName);
            DontDestroyOnLoad(receiver);
            receiver.AddComponent<HarmonyShareSignalReceiver>();
        }
    }

    /// <summary>与 ArkTS 侧 TuanjieSendMessage 第一个参数保持一致。</summary>
    private const string HarmonyShareReceiverName = "HarmonyShareReceiver";

    /// <summary>
    /// 降级方案：复制分享文案到剪贴板（同样在 UI 线程执行）。
    /// 结果由 UI 侧通过 TuanjieSendMessage 回传 "copied" / "copyFail"。
    /// </summary>
    private void DoHarmonyCopyShare(string shareText)
    {
        Debug.Log("[AdsFail] 使用复制文案分享");
        try
        {
            EnsureHarmonyShareObjects();
            _harmonyShareProxy.Call("CopyText", shareText, null);
        }
        catch (Exception e)
        {
            Debug.LogError($"[AdsFail] 复制分享文案失败: {e.Message}");
            ApplyShareResult(false);
        }
    }

    /// <summary>
    /// 由 HarmonyShareSignalReceiver 转发（ArkTS 侧 TuanjieSendMessage 在主线程触发）。
    /// 这里只入队，交给 Update 统一处理。
    /// </summary>
    private void OnHarmonyShareSignalFromUnity(string signal)
    {
        _pendingHarmonySignal = signal;
    }

    /// <summary>主线程消费分享信号（由 Update 调用）。</summary>
    private void ProcessPendingHarmonySignal()
    {
        // 原子取走：回调线程写入与本方法读取不会互相覆盖
        string signal = System.Threading.Interlocked.Exchange(ref _pendingHarmonySignal, null);
        if (string.IsNullOrEmpty(signal)) return;

        Debug.Log($"[AdsFail] 鸿蒙分享信号: {signal}");

        if (!_sharePending) return; // 弹窗已关闭/判定已收口

        if (signal == "uiStarted")
        {
            // UI 线程已接管并调用 ShareKit，关闭「通道无响应」兜底计时器
            _harmonyUiStarted = true;
            Debug.Log("[AdsFail] UI 线程分享通道已确认");
        }
        else if (signal == "dismiss")
        {
            // 面板关闭：延时兜底判定；若随即切出前台（真去分享），
            // 该判定会被离开信号撤销，改由切回前台时严格判定
            ScheduleShareDecision(true);
        }
        else if (signal == "showFail")
        {
            // 系统分享面板不可用（ModalShareController 单例残留）→ 降级复制文案
            Debug.Log("[AdsFail] 系统分享面板不可用，降级为复制文案");
            DoHarmonyCopyShare(_harmonyShareText);
        }
        else if (signal == "copied")
        {
            // 复制成功即视为完成分享：把点击时间前移，跳过 3 秒间隔校验
            MessageSystem.Instance.ShowTip(GetLocalized("ShareCopiedTip", "分享文案已复制，去粘贴给好友吧"));
            _shareClickRealtime = Time.realtimeSinceStartup - (ShareSuccessMinSeconds + 1f);
            ApplyShareResult(true);
        }
        else if (signal == "copyFail")
        {
            CancelShareDecision();
            ApplyShareResult(false);
        }
    }

    /// <summary>
    /// 主动让 ArkTS 侧释放 ShareController。
    /// 分享成功时应用会切到目标 App，dismiss 回调可能丢失，旧控制器会一直
    /// 占用系统 ModalShareController 单例，导致下一次分享报
    /// 「single instance existed」。判定收口时补一次复位。
    /// </summary>
    private void ResetHarmonyShareProxy()
    {
        if (_harmonyShareProxy == null) return;
        try
        {
            _harmonyShareProxy.Call("Reset");
            Debug.Log("[AdsFail] 已请求 ArkTS 释放分享控制器");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AdsFail] 释放分享控制器失败（忽略）: {e.Message}");
        }
    }
#endif

    private void ResetShareState()
    {
        _sharePending = false;
        _shareFocusLost = false;
        CancelShareDecision();
    }

    // ======================= 工具 =======================

    /// <summary>按道具类型取图集图标（沿用工程图集名）。</summary>
    private Sprite GetSprite(LimitRewordType type)
    {
        switch (type)
        {
            case LimitRewordType.Coins: return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("gold0");
            case LimitRewordType.Butterfly: return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("UI_Icon_Butterfly");
            case LimitRewordType.Tipstool: return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("tipicon");
            case LimitRewordType.AutoComplete: return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("rocket");
            case LimitRewordType.ReStartTool: return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("give_3");
            case LimitRewordType.InfiniteEnergy: return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("InfiniteEnergy");
            case LimitRewordType.RemoveDayAds: return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("shopads");
            default: return null;
        }
    }

    /// <summary>多语言取值，缺失时回退中文兜底。</summary>
    private string GetLocalized(string key, string fallback)
    {
        if (MultilingualManager.Instance == null) return fallback;
        string value = MultilingualManager.Instance.GetString(key);
        if (string.IsNullOrEmpty(value) || value == key) return fallback;
        return value;
    }
}
