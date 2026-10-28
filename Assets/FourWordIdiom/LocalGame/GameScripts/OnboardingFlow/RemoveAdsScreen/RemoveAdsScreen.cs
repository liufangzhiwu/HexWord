using System;
using System.Collections;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 限时去广告状态说明弹窗（RemoveAdsScreen）。
/// 触发：结算页点击「限时去广告」BUFF 图标 / 商店购买去广告后。
/// 内容：标题、状态说明、剩余时间倒计时；点「了解」或关闭按钮关闭弹窗。
/// </summary>
public class RemoveAdsScreen : UIWindow
{
    [SerializeField] private Button closeBtn;    // 关闭按钮
    [SerializeField] private Button okBtn;       // 了解按钮
    [SerializeField] private Text titleText;     // 标题
    [SerializeField] private Text descText;      // 状态说明
    [SerializeField] private Text timeText;      // 剩余时间倒计时

    private Coroutine _tickCoroutine;

    protected override void OnEnable()
    {
        base.OnEnable();
        InitUI();
        RefreshTimeText();

        if (_tickCoroutine != null)
            StopCoroutine(_tickCoroutine);
        _tickCoroutine = StartCoroutine(Tick());
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (_tickCoroutine != null)
        {
            StopCoroutine(_tickCoroutine);
            _tickCoroutine = null;
        }
    }

    protected override void InitializeUIComponents()
    {
        if (closeBtn != null) closeBtn.AddVibraClickAction(OnCloseClick);
        if (okBtn != null) okBtn.AddVibraClickAction(OnCloseClick);
    }

    private void InitUI()
    {
        if (titleText != null)
            titleText.text = GetLocalized("RemoveAdsTitle", "限时去广告");

        if (descText != null)
            descText.text = GetLocalized("RemoveAdsDesc", "当前已去除主动弹出的广告，剩余时间如下");

        bool permanent = ShopManager.shopManager != null && ShopManager.shopManager.IsRemoveAdsOwned();

        if (permanent)
        {
            if (titleText != null)
                titleText.text = GetLocalized("RemoveAdsPermanentTitle", "已去除广告");
            if (descText != null)
                descText.text = GetLocalized("RemoveAdsPermanentDesc", "已永久去除主动弹出的广告");
        }
    }

    private IEnumerator Tick()
    {
        var wait = new WaitForSeconds(1f);
        while (true)
        {
            RefreshTimeText();
            yield return wait;
        }
    }

    private void RefreshTimeText()
    {
        if (timeText == null) return;

        double remain = 0;
        if (ShopManager.shopManager != null)
            remain = ShopManager.shopManager.GetRemoveAdsRemainSeconds();

        bool permanent = ShopManager.shopManager != null && ShopManager.shopManager.IsRemoveAdsOwned();

        if (permanent)
        {
            timeText.text = GetLocalized("RemoveAdsPermanent", "永久有效");
            return;
        }

        if (remain <= 0)
        {
            timeText.text = GetLocalized("RemoveAdsExpired", "已结束");
            return;
        }

        timeText.text = FormatRemain(remain);
    }

    private string FormatRemain(double seconds)
    {
        TimeSpan span = TimeSpan.FromSeconds(seconds);
        int days = (int)span.TotalDays;
        if (days > 0)
            return $"{days}天 {span.Hours:00}:{span.Minutes:00}:{span.Seconds:00}";
        if (span.Hours > 0)
            return $"{span.Hours:00}:{span.Minutes:00}:{span.Seconds:00}";
        return $"{span.Minutes:00}:{span.Seconds:00}";
    }

    /// <summary>多语言取值，缺失时回退中文兜底。</summary>
    private string GetLocalized(string key, string fallback)
    {
        if (MultilingualManager.Instance == null) return fallback;
        string value = MultilingualManager.Instance.GetString(key);
        if (string.IsNullOrEmpty(value) || value == key) return fallback;
        return value;
    }

    private void OnCloseClick()
    {
        base.Close();
    }
}
