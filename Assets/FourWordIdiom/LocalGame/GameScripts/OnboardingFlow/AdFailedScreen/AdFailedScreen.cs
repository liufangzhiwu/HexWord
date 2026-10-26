using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 广告获取失败弹窗（AdFailed）。
/// 当视频广告达到「假失败」阈值（分层策略 S/F/T/A/B/C）时弹出，引导付费。
/// 文案：标题「广告获取失败」，描述提示免费次数已用完，按钮「去商店」+「关闭」。
/// </summary>
public class AdFailedScreen : UIWindow
{
    [SerializeField] private Text titleText;    // 标题
    [SerializeField] private Text descText;     // 描述
    [SerializeField] private Button closeBtn;   // 关闭按钮
    [SerializeField] private Button goShopBtn;  // 去商店按钮（引导付费）

    protected override void OnEnable()
    {
        base.OnEnable();
        InitUI();
    }

    protected override void InitializeUIComponents()
    {
        if (closeBtn != null) closeBtn.AddVibraClickAction(OnClose);
        if (goShopBtn != null) goShopBtn.AddVibraClickAction(OnGoShop);
    }

    private void InitUI()
    {
        if (titleText != null)
            titleText.text = GetLocalized("AdFailedTitle", "广告获取失败");

        if (descText != null)
            descText.text = GetLocalized("AdFailedDes", "今日免费次数已用完，去商店看看吧");

        if (goShopBtn != null)
        {
            var btnText = goShopBtn.GetComponentInChildren<Text>();
            if (btnText != null)
                btnText.text = GetLocalized("GoShop", "去商店");
        }

        if (closeBtn != null)
        {
            var btnText = closeBtn.GetComponentInChildren<Text>();
            if (btnText != null)
                btnText.text = GetLocalized("Close", "关闭");
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

    private void OnClose()
    {
        base.Close();
    }

    private void OnGoShop()
    {
        base.Close();
        if (SystemManager.Instance != null)
            SystemManager.Instance.ShowPanel(PanelType.ShopScreen);
    }
}
