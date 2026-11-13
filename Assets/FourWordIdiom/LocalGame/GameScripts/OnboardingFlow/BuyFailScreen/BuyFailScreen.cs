using System.Collections.Generic;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 取消购买弹窗（BuyFailScreen）。
/// 当用户拉起系统支付弹窗后未完成购买（取消/失败），返回游戏时弹出，
/// 展示「取消购买」标题、描述、本次可获得的奖励，以及继续购买/关闭按钮。
/// </summary>
public class BuyFailScreen : UIWindow
{
    [SerializeField] private Button closeBtn;              // 关闭按钮（CloseBtn）
    [SerializeField] private Text title;                   // 标题（HeaderText）
    [SerializeField] private Text descText;                // 描述（adsDes）
    [SerializeField] private List<GameObject> rewardList;  // 奖励项列表（AwardList 下的 IconOne 项）
    [SerializeField] private Button ClaimBtn;              // 继续购买按钮（ClaimBtn）

    private Canvas _canvas;                                // 自身 Canvas（用于强制置顶）
    private bool _closing;                                 // 关闭中标记（防连点 / 防重复关闭）
    private RectTransform _blocker;                        // 全屏点击拦截层（运行时创建）

    protected override void Awake()
    {
        base.Awake();
        TryGetComponent(out _canvas);
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
        if (title != null)
            title.text = GetLocalized("CancelPurchase", "取消购买");

        if (descText != null)
            descText.text = GetLocalized("CancelPurchaseDes", "购买已被取消");

        if (ClaimBtn != null)
        {
            var btnText = ClaimBtn.GetComponentInChildren<Text>();
            if (btnText != null)
                btnText.text = GetLocalized("Continue", "继续");
        }

        if (closeBtn != null)
        {
            var btnText = closeBtn.GetComponentInChildren<Text>();
            if (btnText != null)
                btnText.text = GetLocalized("Close", "关闭");
        }

        InitRewardUI();
    }

    /// <summary>多语言取值，缺失时回退到中文兜底。</summary>
    private string GetLocalized(string key, string fallback)
    {
        if (MultilingualManager.Instance == null) return fallback;
        string value = MultilingualManager.Instance.GetString(key);
        if (string.IsNullOrEmpty(value) || value == key) return fallback;
        return value;
    }

    private void InitRewardUI()
    {
        if (rewardList == null) return;
        foreach (var go in rewardList)
        {
            if (go != null) go.SetActive(false);
        }

        var item = Game.self.Shop != null ? Game.self.Shop.CurrentShopDataItem : null;
        if (item == null || item.productContent == null) return;

        int count = Mathf.Min(item.productContent.Count, rewardList.Count);
        for (int i = 0; i < count; i++)
        {
            var data = item.productContent[i];
            if (data == null || data.Count < 2) continue;

            int type;
            int amount;
            if (!int.TryParse(data[0], out type) || !int.TryParse(data[1], out amount)) continue;

            GameObject slot = rewardList[i];
            if (slot == null) continue;

            var icon = slot.GetComponentInChildren<Image>();
            var countText = slot.GetComponentInChildren<Text>();
            slot.SetActive(true);

            if (icon != null)
            {
                icon.sprite = GetSprite((LimitRewordType)type);
                icon.preserveAspect = true;
            }
            if (countText != null)
            {
                countText.text = "\u00d7" + amount;
            }
        }
    }

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
