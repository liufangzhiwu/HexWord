using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 广告追踪（关联应用）引导页。
///
/// 交互约定（对应需求《广告跟踪引导 & 广告权限弹窗出现时机》）：
///   1. 首次加载：覆盖在加载页上方出现，按钮文案 = "继续"；
///      点"继续" → 请求系统广告追踪权限（系统弹窗盖在本页上层）；
///      系统弹窗关闭后回到本页，再点一次"继续" → 关闭本页，继续走加载流程。
///   2. 设置入口（玩家之前拒绝过）：按钮文案 = "去开启"；
///      点"去开启" → 跳到系统设置页，回到游戏后由 AdTrackingManager 校验结果。
///
/// 本脚本只负责"长什么样/点了算不算数"，流程编排与奖励发放都在 AdTrackingManager。
/// </summary>
public class AdTrackingGuidance : MonoBehaviour
{
    public enum Mode
    {
        /// <summary>首次启动（隐私之后、加载页之上）</summary>
        Startup = 0,
        /// <summary>设置里"广告关联"入口（去开启）</summary>
        Settings = 1,
    }

    [SerializeField] private Text _titleText;
    [SerializeField] private HyperlinkText _descriptionText;
    [SerializeField] private Button _confirmButton;

    /// <summary>主按钮点击（继续 / 去开启）</summary>
    public event Action OnPrimaryClicked;

    private Text _buttonLabel;
    private Text _applicationLabel;
    private Text _rewardBadge;

    /// <summary>当前展示模式（Startup 首次启动 / Settings 去开启）</summary>
    public Mode CurrentMode { get; private set; }

    private static readonly Color RewardColor = new Color(0.95f, 0.65f, 0.12f, 1f);

    private void Awake()
    {
        if (_confirmButton != null)
        {
            _confirmButton.onClick.RemoveAllListeners();
            _confirmButton.onClick.AddListener(HandlePrimaryClick);
        }

        if (_descriptionText != null)
        {
            _descriptionText.onHyperlinkClick = url =>
            {
                if (!string.IsNullOrEmpty(url)) Application.OpenURL(url);
            };
        }

        CacheButtonLabel();
    }

    /// <summary>注入内容并展示（由 AdTrackingManager 调用）</summary>
    public void Setup(Mode mode, int rewardGold)
    {
        CurrentMode = mode;

        SetText(_titleText, Localize("AllowAssociation"));
        SetText(_descriptionText, Localize("AssociationText"));

        CacheButtonLabel();
        SetText(_buttonLabel, Localize(mode == Mode.Startup ? "Continue" : "GoAndStart"));

        EnsureApplicationLabel();
        EnsureRewardBadge(rewardGold);

        SetBusy(false);
    }

    /// <summary>请求系统弹窗/跳系统设置期间禁用按钮，避免连点</summary>
    public void SetBusy(bool busy)
    {
        if (_confirmButton != null) _confirmButton.interactable = !busy;
    }

    /// <summary>权限结果回来后解锁按钮（文案保持不变，玩家再点一次"继续"才关闭）</summary>
    public void OnPermissionReturned(bool granted)
    {
        SetBusy(false);
    }

    /// <summary>分层配置晚于弹窗就绪时，补上/刷新"+G 金币"奖励标签</summary>
    public void RefreshReward(int gold)
    {
        if (gold <= 0)
        {
            if (_rewardBadge != null) _rewardBadge.gameObject.SetActive(false);
            return;
        }

        EnsureRewardBadge(gold);
    }

    public void Close()
    {
        Destroy(gameObject);
    }

    private void HandlePrimaryClick()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySoundEffect("Button");
        AudioTrigger();
        OnPrimaryClicked?.Invoke();
    }

    private void AudioTrigger()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.TriggerVibration(40, 50);
    }

    // ============================================================
    // 运行时补充的 UI（不改预制体结构）
    // ============================================================

    /// <summary>说明文案上方补一个"关联应用"小标题</summary>
    private void EnsureApplicationLabel()
    {
        if (_applicationLabel != null || _descriptionText == null) return;

        RectTransform descRect = _descriptionText.rectTransform;
        _applicationLabel = CreateText(
            descRect.parent,
            "RelatedApplicationLabel",
            Localize("RelatedApplication"),
            _titleText != null ? _titleText : _descriptionText,
            new Vector2(0f, descRect.anchoredPosition.y + 78f),
            new Vector2(descRect.rect.width, 70f),
            _titleText != null ? _titleText.color : _descriptionText.color,
            52);
    }

    /// <summary>按钮上方补"+G 金币"奖励标签（挂在引导页按钮上）</summary>
    private void EnsureRewardBadge(int gold)
    {
        if (gold <= 0 || _confirmButton == null) return;

        if (_rewardBadge == null)
        {
            RectTransform btnRect = _confirmButton.transform as RectTransform;
            _rewardBadge = CreateText(
                btnRect.parent,
                "RewardBadge",
                string.Empty,
                _titleText != null ? _titleText : _descriptionText,
                new Vector2(0f, btnRect.anchoredPosition.y + 120f),
                new Vector2(620f, 64f),
                RewardColor,
                46);
        }

        _rewardBadge.text = Localize("AdTrackingRewardBadge").Replace("{0}", gold.ToString());
        _rewardBadge.gameObject.SetActive(true);
    }

    private Text CreateText(Transform parent, string name, string content, Text styleSource,
        Vector2 anchoredPosition, Vector2 size, Color color, int fontSize)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);

        Text text = go.AddComponent<Text>();
        text.text = content;
        text.color = color;
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        if (styleSource != null) text.font = styleSource.font;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        return text;
    }

    // ============================================================
    // 工具
    // ============================================================

    private void CacheButtonLabel()
    {
        if (_buttonLabel != null || _confirmButton == null) return;
        _buttonLabel = _confirmButton.GetComponentInChildren<Text>(true);
    }

    private static void SetText(Text target, string content)
    {
        if (target != null) target.text = content;
    }

    /// <summary>取多语言文案，并把表格里的 "\n" 转成真换行（通用文本表约定）</summary>
    private static string Localize(string key)
    {
        string value = MultilingualManager.Instance != null
            ? MultilingualManager.Instance.GetString(key)
            : key;

        if (string.IsNullOrEmpty(value)) return key;
        return value.Replace("\\n", "\n");
    }
}
