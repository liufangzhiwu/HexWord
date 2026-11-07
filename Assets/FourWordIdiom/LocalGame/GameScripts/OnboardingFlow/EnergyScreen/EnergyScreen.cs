using System.Collections;
using System.Collections.Generic;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

public class EnergyScreen : UIWindow
{
    [Header("UI Elements")]
    [SerializeField] private Button backButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Text nextEnergyTimeText;
    [SerializeField] private Button adsButton;
    [SerializeField] private Button buyButton;
    [SerializeField] private Text titleText;
    [SerializeField] private Text descriptionText;
    [Tooltip("体力图标（count_bg/icon）：无限体力期间换成 InfiniteEnergy 图标，留空则运行时自动绑定")]
    [SerializeField] private Image energyIcon;
    private Coroutine _timerCoroutine;

    // ======================= ★ 无限体力状态 =======================
    /// <summary>无限体力期间「获得下一个体力时间」这行替换成剩余时间说明</summary>
    private const string UnlimitedEnergyTips = "无限体力剩余：";

    /// <summary>通用图集（UI_Universal）里用到的按钮图片</summary>
    private const string SpriteButtonNormal = "UI_button_Bg";   // 可点击态
    private const string SpriteCanNotGold   = "NoCanGold";      // 金币按钮不可用
    private const string SpriteCanNotGoldAlt = "noCanGold";     // 兼容小写文件名
    private const string SpriteCanNotAds    = "NoCanAds";       // 广告按钮不可用
    /// <summary>无限体力期间的体力图标</summary>
    private const string SpriteInfiniteEnergy = "InfiniteEnergy";

    private string _defaultDescription;
    private bool _defaultDescriptionCached;
    /// <summary>体力图标原始 sprite（无限体力结束后还原用）</summary>
    private Sprite _defaultEnergyIconSprite;
    private bool _defaultEnergyIconCached;
    /// <summary>当前是否处于「无限体力」表现（幂等，避免每秒重写）</summary>
    private bool _isUnlimitedActive;
    /// <summary>图集取图缓存（含取不到时的 null，避免重复请求/重复报错）</summary>
    private readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();
    /// <summary>按钮子节点（icon / 文字）原始的显隐状态（隐藏/还原用）</summary>
    private readonly Dictionary<Transform, bool> _defaultChildActive = new Dictionary<Transform, bool>();

    // Start is called before the first frame update
    void Start()
    {
        backButton.AddClickAction(OnCloseClicked);
        closeButton.AddVibraClickAction(OnCloseClicked);
        adsButton.AddClickAction(OnAdsClicked);
        buyButton.AddClickAction(OnGoldBuyClicked);

        // 加个安全判断，防止子节点改变报错
        if (buyButton.transform.childCount > 1)
        {
            buyButton.transform.GetChild(1).GetComponent<Text>().text = AppGameSettings.ShopItems.EnergyCost.ToString();
        }
        titleText.text = MultilingualManager.Instance.GetString("MorePower");
        descriptionText.text = MultilingualManager.Instance.GetString("TimeToPower");

        // ★ 注意：无限体力期间按钮下的 icon / 文字会被隐藏，
        //   这里必须用 includeInactive = true，否则首次打开时会取到 null 而抛异常。
        Text adsLabel = adsButton.GetComponentInChildren<Text>(true);
        if (adsLabel != null)
            adsLabel.text = MultilingualManager.Instance.GetString("FreePower").Substring(0,2);

        // ★ 缓存提示文案原文，无限体力结束后要还原
        if (descriptionText != null)
        {
            _defaultDescription = descriptionText.text;
            _defaultDescriptionCached = true;

            // 首次打开时 OnEnable 里的定时器会先跑一帧（比 Start 早），若已处于无限体力状态，
            // 这里会被上面这行覆盖回默认文案，需要再盖回来。
            if (_isUnlimitedActive)
                descriptionText.text = UnlimitedEnergyTips;
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        // EventDispatcher.instance.TriggerHighlightGoldAndEnergy(true);
        AnalyticMgr.VideoAdShow("体力广告");
        if (_timerCoroutine != null) StopCoroutine(_timerCoroutine);
        _timerCoroutine = StartCoroutine(CheckShowNextEnergyTime());
    }

    private IEnumerator CheckShowNextEnergyTime()
    {
        WaitForSeconds wait = new WaitForSeconds(1f);
       
        while (true)
        {
            var userData = GameDataManager.Instance.UserData;
            // 顺手核算一下体力，防止玩家打开界面发呆跨越了30分钟恢复点
            userData.CalculateEnergyRegen();

            // ★ 无限体力：两个按钮置灰禁用，时间位置显示剩余时长
            bool unlimited = userData.HasUnlimitedEnergy();
            ApplyUnlimitedEnergyState(unlimited);

            if (unlimited)
            {
                // 提示行已改成「无限体力剩余：」，这里只放倒计时，避免撑破 count_bg 的宽度
                nextEnergyTimeText.text = FormatRemainTime(userData.GetUnlimitedEnergyRemainSeconds());
            }
            else if (userData.Energy >= UserData.MAX_NATURAL_ENERGY)
            {
                // 👇 修复 1：体力满了不要显示 00:00，显示中文“已满”
                nextEnergyTimeText.text = "已满"; 
            }
            else
            {
                // 👇 修复 2：把 1799 这种纯秒数，格式化成 29:59 玩家才看得懂
                int remainSeconds = userData.GetNextEnergyRegenSeconds();
                int mins = remainSeconds / 60;
                int secs = remainSeconds % 60;
                nextEnergyTimeText.text = string.Format("{0:00}:{1:00}", mins, secs);
            }
            yield return wait;
        }
    }

    // ======================= ★ 无限体力 =======================

    /// <summary>
    /// 无限体力剩余时长格式化：超过 1 小时用 H:MM:SS，否则 MM:SS。
    /// </summary>
    private static string FormatRemainTime(double remainSeconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt((float)remainSeconds));
        int hours = total / 3600;
        int mins = (total % 3600) / 60;
        int secs = total % 60;

        return hours > 0
            ? string.Format("{0}:{1:00}:{2:00}", hours, mins, secs)
            : string.Format("{0:00}:{1:00}", mins, secs);
    }

    /// <summary>
    /// 按「是否处于无限体力」切换弹窗表现（幂等，状态没变直接返回）：
    ///  - 是 → 金币/广告按钮不可点击，并换成通用图集里的 NoCanGold / NoCanAds 图；
    ///  - 否 → 恢复可点击，图片换回通用图集里的 UI_button_Bg。
    /// </summary>
    private void ApplyUnlimitedEnergyState(bool unlimited)
    {
        if (_isUnlimitedActive == unlimited) return;
        _isUnlimitedActive = unlimited;

        SetButtonUnavailable(buyButton, unlimited, SpriteCanNotGold, SpriteCanNotGoldAlt);
        SetButtonUnavailable(adsButton, unlimited, SpriteCanNotAds, null);

        // ★ 体力图标：无限体力期间换成通用图集里的 InfiniteEnergy，结束后还原
        ApplyEnergyIcon(unlimited);

        if (descriptionText != null)
        {
            if (unlimited)
                descriptionText.text = UnlimitedEnergyTips;
            else if (_defaultDescriptionCached)
                descriptionText.text = _defaultDescription;
        }
    }

    /// <summary>
    /// 切换体力图标：无限体力 → 通用图集 UI_Universal 的 InfiniteEnergy；结束 → 还原为原图标。
    /// </summary>
    private void ApplyEnergyIcon(bool unlimited)
    {
        EnsureEnergyIcon();
        if (energyIcon == null) return;

        // 只在第一次切换前缓存原图，保证无限体力结束后能准确还原
        if (!_defaultEnergyIconCached)
        {
            _defaultEnergyIconSprite = energyIcon.sprite;
            _defaultEnergyIconCached = true;
        }

        if (unlimited)
        {
            Sprite sprite = GetAtlasSprite(SpriteInfiniteEnergy);
            if (sprite != null)
                energyIcon.sprite = sprite;
        }
        else
        {
            energyIcon.sprite = _defaultEnergyIconSprite;
        }
    }

    /// <summary>
    /// 自动绑定体力图标（预制体里是 count_bg/icon，和倒计时文本同级）。
    /// Inspector 已指定则跳过。
    /// </summary>
    private void EnsureEnergyIcon()
    {
        if (energyIcon != null || nextEnergyTimeText == null) return;

        Transform parent = nextEnergyTimeText.transform.parent;   // count_bg
        if (parent == null) return;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child == null || child == nextEnergyTimeText.transform) continue;

            Image image = child.GetComponent<Image>();
            if (image != null)
            {
                energyIcon = image;
                return;
            }
        }
    }

    /// <summary>
    /// 不可用 → 换 NoCan 图 + 隐藏 icon/文字 + 不可点击；恢复 → 换 UI_button_Bg + 还原子节点 + 可点击。
    /// 只改按钮背景 Image 的 sprite 与子节点显隐，不动颜色。
    /// </summary>
    private void SetButtonUnavailable(Button button, bool unavailable, string unavailableSprite, string unavailableSpriteAlt)
    {
        if (button == null) return;

        Image image = button.targetGraphic as Image;
        if (image == null) image = button.GetComponent<Image>();

        if (image != null)
        {
            Sprite sprite = unavailable
                ? GetAtlasSprite(unavailableSprite, unavailableSpriteAlt)
                : GetAtlasSprite(SpriteButtonNormal);

            if (sprite != null) image.sprite = sprite;
        }

        // ★ 按钮下的 icon / 文本：不可用时整体隐藏，恢复时按各自原始状态还原
        for (int i = 0; i < button.transform.childCount; i++)
        {
            Transform child = button.transform.GetChild(i);
            if (child == null) continue;

            if (!_defaultChildActive.TryGetValue(child, out bool defaultActive))
            {
                defaultActive = child.gameObject.activeSelf;
                _defaultChildActive[child] = defaultActive;
            }

            child.gameObject.SetActive(unavailable ? false : defaultActive);
        }

        if (unavailable)
        {
            // 让 Unity 默认的 disabledColor（半透明灰）不要再叠在 NoCan 图上
            ColorBlock colors = button.colors;
            if (colors.disabledColor != Color.white)
            {
                colors.disabledColor = Color.white;
                button.colors = colors;
            }
        }

        button.interactable = !unavailable;
    }

    /// <summary>从通用图集取图并缓存（取不到时返回 null，只警告一次）。</summary>
    private Sprite GetAtlasSprite(string spriteName, string fallbackName = null)
    {
        if (string.IsNullOrEmpty(spriteName)) return null;
        if (_spriteCache.TryGetValue(spriteName, out Sprite cached)) return cached;

        Sprite sprite = null;
        if (AdvancedBundleLoader.SharedInstance != null)
        {
            sprite = AdvancedBundleLoader.SharedInstance.TryGetSpriteFromAtlas(spriteName);

            if (sprite == null && !string.IsNullOrEmpty(fallbackName))
                sprite = AdvancedBundleLoader.SharedInstance.TryGetSpriteFromAtlas(fallbackName);
        }

        _spriteCache[spriteName] = sprite;
        if (sprite == null)
            Debug.LogWarning($"[EnergyScreen] 通用图集 UI_Universal 缺少图片: {spriteName}");

        return sprite;
    }
    
    private void OnGoldBuyClicked()
    {
        if (AppGameSettings.ShopItems.EnergyCost > GameDataManager.Instance.UserData.Gold)
        {        
            MessageSystem.Instance.ShowTip("TipGoldInsufficient");
            return;
        }
        
        GameDataManager.Instance.UserData.UpdateGold(-AppGameSettings.ShopItems.EnergyCost,true,true,"金币购买体力");
        GameDataManager.Instance.UserData.AddBonusEnergy(1, "金币购买");
        MessageSystem.Instance.ShowTip("体力购买成功！");
        GameDataManager.Instance.CommitGameData();
    }

    private void OnAdsClicked()
    {
        AnalyticMgr.VideoAdClick("体力广告");
        AdRuleManager.Instance.TryShowRewardVideo(Define.AdKey.RewardAdIdStoreGold, call =>
        {
            MessageSystem.Instance.HideLoadingAnimation();
            if (call)
            {
                GameDataManager.Instance.UserData.AddBonusEnergy(1, "看广告获取");
                AnalyticMgr.VideoAdSuccess("体力广告");
                MessageSystem.Instance.ShowTip("体力恢复成功！");
                GameDataManager.Instance.UserData.totalSeeAds++;
            }
            else
            {
                MessageSystem.Instance.ShowTip("广告加载失败，请稍后重试。");
                AnalyticMgr.VideoAdFail("体力广告");
            }
        });
    }
    private void OnCloseClicked()
    {
        SystemManager.Instance.HidePanel(PanelType.EnergyScreen);
    }

    protected override void OnDisable()
    {
        // EventDispatcher.instance.TriggerHighlightGoldAndEnergy(false);
        if (_timerCoroutine != null)
        {
            StopCoroutine(_timerCoroutine);
            _timerCoroutine = null;
        }
        base.OnDisable();
    }
}
