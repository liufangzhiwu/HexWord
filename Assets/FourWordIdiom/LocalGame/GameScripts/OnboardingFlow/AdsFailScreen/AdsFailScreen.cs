using System.Collections.Generic;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 广告播放失败弹窗（AdsFailScreen）。
/// 当用户拉起系统支付弹窗后未完成购买（取消/失败），返回游戏时弹出，
/// 展示「取消购买」标题、描述、本次可获得的奖励，以及继续购买/关闭按钮。
/// </summary>
public class AdsFailScreen : UIWindow
{
    [SerializeField] private Button closeBtn;              // 关闭按钮（CloseBtn）
    [SerializeField] private Text title;                   // 标题（HeaderText）
    [SerializeField] private Text descText;                // 描述（adsDes）
    [SerializeField] private Text countText;                // 描述（adsDes）
    [SerializeField] private Image toolIcon;                // 描述（adsDes）
    [SerializeField] private Image toolIcon2;                // 描述（adsDes）
    [SerializeField] private Transform discountTable;                // 描述（adsDes）
    [SerializeField] private Button BuyBtn;              // 继续购买按钮（ClaimBtn）
    [SerializeField] private Button ShareBtn;              // 继续购买按钮（ClaimBtn）

    protected override void OnEnable()
    {
        base.OnEnable();
        InitUI();
    }

    protected override void InitializeUIComponents()
    {
        if (closeBtn != null) closeBtn.AddVibraClickAction(OnCloseClick);
        if (BuyBtn != null) BuyBtn.AddVibraClickAction(OnContinueClick);
    }

    private void InitUI()
    {
        if (title != null)
            title.text = GetLocalized("CancelPurchase", "换种方式");

        if (descText != null)
            descText.text = GetLocalized("CancelPurchaseDes", "视频暂不可用，换种方式获取吧");

      
        if (closeBtn != null)
        {
            var btnText = closeBtn.GetComponentInChildren<Text>();
            if (btnText != null)
                btnText.text = GetLocalized("Close", "关闭");
        }
      
    }

    /// <summary>多语言取值，缺失时回退到中文兜底。</summary>
    private string GetLocalized(string key, string fallback)
    {
        if (MultilingualManager.Instance == null) return fallback;
        string value = MultilingualManager.Instance.GetString(key);
        if (string.IsNullOrEmpty(value) || value == key) return fallback;
        return value;
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

    private void OnCloseClick()
    {
        base.Close();
    }

    private void OnContinueClick()
    {
        base.Close();
    }
}
