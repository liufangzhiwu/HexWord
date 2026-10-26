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

    protected override void OnEnable()
    {
        base.OnEnable();
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

    private void OnCloseClick()
    {
        base.Close();
    }

    private void OnContinueClick()
    {
        base.Close();
    }
}
