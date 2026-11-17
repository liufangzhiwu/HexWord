using System.Collections.Generic;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 购买未完成弹窗（BuyFailScreen）。
/// 玩家拉起系统收银台后没走完流程，返回游戏时弹出。两种形态：
///   · 玩家自己取消（或返回后无回调、核单也查不到订单）：
///       标题「取消购买」/ 描述「购买已被取消」/ 主按钮「继续」
///   · 其它失败（平台报错、商品异常、回包异常等）：
///       标题「购买失败」/ 描述 = 具体失败原因 / 主按钮「关闭」，同时隐藏右上角关闭按钮
/// </summary>
public class BuyFailScreen : UIWindow
{
    [SerializeField] private Button closeBtn;              // 关闭按钮（CloseBtn）
    [SerializeField] private Text title;                   // 标题（HeaderText）
    [SerializeField] private Text descText;                // 描述（adsDes）
    [SerializeField] private Button ClaimBtn;              // 主按钮（继续购买 / 关闭）（ClaimBtn）

    private Canvas _canvas;                                // 自身 Canvas（用于强制置顶）
    private bool _closing;                                 // 关闭中标记（防连点 / 防重复关闭）
    private RectTransform _blocker;                        // 全屏点击拦截层（运行时创建）
    private bool _closeBtnDefaultActive = true;            // 关闭按钮在预制体里的默认显隐，用于取消态还原

    // ------------------------------------------------------------
    //  失败原因（由 ShopManager.OnPurchaseCanceled / OnPurchaseFailed 在 ShowPanel 之前写入）
    //  用静态字段而不是 ShowPanel 传参，是为了不依赖面板是否已经实例化 / 是否已激活。
    // ------------------------------------------------------------
    private static string s_reason;
    private static bool s_userCancel = true;   // 缺省按「玩家取消」处理，兼容老的调用方

    /// <summary>
    /// 设置本次弹窗要展示的失败信息。
    /// </summary>
    /// <param name="reason">失败原因（平台错误码/错误信息，或客户端内部 reason），可为空</param>
    /// <param name="userCancel">是否「玩家自己取消 / 主动返回」</param>
    public static void SetFailure(string reason, bool userCancel)
    {
        s_reason = reason;
        s_userCancel = userCancel;
    }

    /// <summary>
    /// 判断一个失败原因是否属于「玩家自己取消」。
    /// 1001860000 是鸿蒙 IAP 的取消码；purchase_canceled_no_callback 是「返回游戏但没回调且核单无订单」。
    /// </summary>
    public static bool LooksLikeUserCancel(string reason)
    {
        if (string.IsNullOrEmpty(reason)) return true;

        string r = reason.ToLowerInvariant();
        if (r.Contains("1001860000")) return true;
        if (r.Contains("cancel")) return true;
        return false;
    }

    protected override void Awake()
    {
        base.Awake();
        TryGetComponent(out _canvas);

        // 记录关闭按钮的默认显隐：失败态要隐藏它，取消态要还原回预制体的原始状态
        _closeBtnDefaultActive = closeBtn != null && closeBtn.gameObject.activeSelf;
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        _closing = false;
        SetButtonsInteractable(true);

        // ★ bug66：本界面与底下的弹窗（如 AdsFailScreen）Canvas sortingOrder 都是 100，
        //   「谁盖在谁上面」完全由层级顺序决定。若底层弹窗是后创建的，它就会盖在本界面之上，
        //   于是点击落在底层的「分享 / 购买」按钮上 —— 表现为点「继续」却调起分享或重新发起支付。
        //   显示时强制把自己排到最上面（同时改 sortingOrder 与同级顺序，两条都做才稳）。
        BringToFront();

        // 再加一层全屏透明遮罩：本界面预制体只有中间那块 Bg 能挡点击，
        // 屏幕其它区域（尤其是按钮缝隙）仍可能穿透到下层弹窗，这里一次性兜住。
        EnsureFullScreenBlocker();

        InitUI();
    }

    protected override void InitializeUIComponents()
    {
        if (closeBtn != null) closeBtn.AddVibraClickAction(OnCloseClick);
        if (ClaimBtn != null) ClaimBtn.AddVibraClickAction(OnContinueClick);
    }

    private void InitUI()
    {
        if (s_userCancel) InitCancelStyle();
        else InitFailedStyle();
    }

    /// <summary>玩家自己取消：标题「取消购买」、描述「购买已被取消」、主按钮「继续」，保留右上角关闭按钮</summary>
    private void InitCancelStyle()
    {
        if (title != null)
            title.text = GetLocalized("CancelPurchase", "取消购买");

        if (descText != null)
            descText.text = GetLocalized("CancelPurchaseDes", "购买已被取消");

        SetMainButtonText(GetLocalized("Continue", "继续"));

        // 取消态允许玩家点 X 关掉，还原成预制体的默认状态
        if (closeBtn != null) closeBtn.gameObject.SetActive(_closeBtnDefaultActive);
    }

    /// <summary>其它失败：标题「购买失败」、描述 = 具体失败原因、主按钮「关闭」，并隐藏右上角关闭按钮</summary>
    private void InitFailedStyle()
    {
        if (title != null)
            title.text = GetLocalized("PurchaseFailed", "购买失败");

        if (descText != null)
            descText.text = BuildFailDescription(s_reason);

        SetMainButtonText(GetLocalized("Close", "关闭"));

        // 失败态只保留主按钮「关闭」，避免两个关闭入口
        if (closeBtn != null) closeBtn.gameObject.SetActive(false);

        Debug.Log("[BuyFail] 购买失败弹窗，原因: " + (string.IsNullOrEmpty(s_reason) ? "(空)" : s_reason));
    }

    private void SetMainButtonText(string text)
    {
        if (ClaimBtn == null) return;
        var btnText = ClaimBtn.GetComponentInChildren<Text>();
        if (btnText != null) btnText.text = text;
    }

    /// <summary>
    /// 把失败原因转成玩家能看懂的描述：
    ///   · 已知客户端原因 → 专门文案；
    ///   · 其它（平台错误信息/错误码）→ 通用文案 + 错误码后缀，便于客服定位。
    /// </summary>
    private string BuildFailDescription(string reason)
    {
        if (!string.IsNullOrEmpty(reason))
        {
            string r = reason.ToLowerInvariant();

            if (r.Contains("product_not_found")
                || r.Contains("product_not_exist")
                || r.Contains("product_unavailable"))
                return GetLocalized("PurchaseFailedProduct", "商品信息异常，请稍后重试");

            if (r.Contains("invalid_purchase_payload")
                || r.Contains("payload")
                || r.Contains("order_failed")
                || r.Contains("order_error"))
                return GetLocalized("PurchaseFailedOrder", "支付结果异常，请稍后重试");

            if (r.Contains("network")
                || r.Contains("timeout")
                || r.Contains("connect"))
                return GetLocalized("PurchaseFailedNetwork", "网络异常，请稍后重试");
        }

        string baseText = GetLocalized("PurchaseFailedDes", "支付未完成，请稍后重试");

        // 平台错误码（连续 4 位以上数字）附在文案后面，方便客服定位；不展示原始英文报错
        string code = ExtractErrorCode(reason);
        return string.IsNullOrEmpty(code) ? baseText : baseText + "（错误码 " + code + "）";
    }

    /// <summary>从平台错误信息里挑出错误码（取最长的一段连续数字，至少 4 位）</summary>
    private static string ExtractErrorCode(string reason)
    {
        if (string.IsNullOrEmpty(reason)) return null;

        string best = null;
        int i = 0;
        while (i < reason.Length)
        {
            if (!char.IsDigit(reason[i])) { i++; continue; }

            int start = i;
            while (i < reason.Length && char.IsDigit(reason[i])) i++;

            string candidate = reason.Substring(start, i - start);
            if (candidate.Length >= 4 && (best == null || candidate.Length > best.Length))
                best = candidate;
        }

        return best;
    }

    /// <summary>多语言取值，缺失时回退到中文兜底。</summary>
    private string GetLocalized(string key, string fallback)
    {
        if (MultilingualManager.Instance == null) return fallback;
        string value = MultilingualManager.Instance.GetString(key);
        if (string.IsNullOrEmpty(value) || value == key) return fallback;
        return value;
    }

    /// <summary>把自己排到所有已显示弹窗之上（Canvas 排序 + 同父节点顺序双保险）。</summary>
    private void BringToFront()
    {
        transform.SetAsLastSibling();

        if (_canvas == null) TryGetComponent(out _canvas);
        if (_canvas == null) return;

        int maxOrder = 0;
        Transform parent = transform.parent;
        if (parent != null)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Canvas other = parent.GetChild(i).GetComponent<Canvas>();
                if (other == null || other == _canvas) continue;
                if (!other.gameObject.activeSelf) continue;
                if (other.sortingOrder > maxOrder) maxOrder = other.sortingOrder;
            }
        }

        if (maxOrder >= _canvas.sortingOrder)
            _canvas.sortingOrder = maxOrder + 1;
    }

    /// <summary>
    /// 给界面补一张全屏透明、可被射线命中的 Image（作为第一个子节点，画在最底下），
    /// 挡住整个屏幕的点击穿透。放在子节点而不是根节点，是为了不被入场/退场动画的
    /// m_Color / m_Alpha 轨道误改。
    /// </summary>
    private void EnsureFullScreenBlocker()
    {
        if (_blocker == null)
        {
            var go = new GameObject("ClickBlocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = gameObject.layer;

            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _blocker = rt;
        }

        // 排到最前面：渲染顺序在第一层，射线命中优先级最低，只在不被其它可见元素覆盖的区域生效
        _blocker.SetAsFirstSibling();

        var img = _blocker.GetComponent<Image>();
        if (img != null)
        {
            img.color = new Color(1f, 1f, 1f, 0f);   // 全透明，只做拦截，不影响观感
            img.raycastTarget = true;
        }
    }

    private void SetButtonsInteractable(bool value)
    {
        if (closeBtn != null) closeBtn.interactable = value;
        if (ClaimBtn != null) ClaimBtn.interactable = value;
    }

    private void OnCloseClick()
    {
        CloseSafely();
    }

    private void OnContinueClick()
    {
        // 「继续」= 关闭弹窗（不重发购买）。
        CloseSafely();
    }

    /// <summary>
    /// 统一关闭入口：立刻让两个按钮失效，再播关闭动画。
    /// 关闭动画（0.3s）期间界面仍在最上层，继续挡住点击，避免同一次连点穿透到底层的分享 / 购买按钮。
    /// </summary>
    private void CloseSafely()
    {
        if (_closing) return;
        _closing = true;

        SetButtonsInteractable(false);
        base.Close();
    }
}
