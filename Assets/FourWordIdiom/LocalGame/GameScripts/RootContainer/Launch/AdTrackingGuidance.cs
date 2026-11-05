using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 广告追踪（关联应用）引导页。
///
/// 交互约定（对应需求《广告跟踪引导 & 广告权限弹窗出现时机》）：
///   1. 首次加载：覆盖在加载页上方出现，按钮文案 = "继续"；
///      点"继续" → 请求系统广告追踪权限（系统弹窗盖在本页上层）；
///      系统弹窗关闭时（允许/拒绝），本页由 AdTrackingManager 同步关闭，继续走加载流程。
///   2. 设置入口（玩家之前拒绝过）：按钮文案 = "去开启"；
///      点"去开启" → 跳到系统设置页/再次请求授权，回到游戏后由 AdTrackingManager 校验结果。
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

        CacheButtonLabel();
    }

    /// <summary>注入内容并展示（由 AdTrackingManager 调用）</summary>
    public void Setup(Mode mode, int rewardGold)
    {
        CurrentMode = mode;

        SetText(_titleText, Localize("AllowAssociation"));

        CacheButtonLabel();
        SetText(_buttonLabel, Localize(mode == Mode.Startup ? "Continue" : "GoAndStart"));

        SetBusy(false);
    }

    /// <summary>请求系统弹窗/跳系统设置期间禁用按钮，避免连点</summary>
    public void SetBusy(bool busy)
    {
        if (_confirmButton != null) _confirmButton.interactable = !busy;
    }

    /// <summary>
    /// 权限结果回来后解锁按钮。
    /// 首次启动场景：结果回来的同时本页就会被 AdTrackingManager 关掉，这里主要服务于
    /// "设置入口 + 玩家再次拒绝"这种需要留在本页重试的场景。
    /// </summary>
    public void OnPermissionReturned(bool granted)
    {
        SetBusy(false);
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
