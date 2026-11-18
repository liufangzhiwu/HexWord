using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Middleware;

public class OptionsView : UIWindow
{
    [SerializeField] private Button HideButton; // 关闭按钮
    [SerializeField] private Button autopushBtn; // 推送开关
    [SerializeField] private Button adsTrackBtn; // 广告关联开关
    
    [SerializeField] private Button MusicButton; // 音乐按钮
    [SerializeField] private Button SoundBtn; // 音效按钮
    [SerializeField] private Button VibrateBtn; // 震动按钮
    
    [SerializeField] private Button privacyBtn; // 隐私条款按钮
    [SerializeField] private Button termsBtn; // 服务协议按钮
    [SerializeField] private Button opinionBtn; // 语言选择按钮
    [SerializeField] private Button restoreBuyBtn; // 服务协议按钮
    [SerializeField] private Button copyButton;   // 复制id和包名
    
    [SerializeField] private GameObject muClose; // 音乐关闭的Icon
    [SerializeField] private GameObject soClose; // 音效关闭的Icon
    [SerializeField] private GameObject viClose; // 震动关闭的Icon
    
    [SerializeField] private GameObject auHandle; // 震动开关的视觉手柄
    [SerializeField] private GameObject adsHandle; // 广告开关的视觉手柄

    [Header("广告关联行的激励表现（留空会按节点名自动查找）")]
    //[SerializeField] private GameObject adsTrackTable;     // 整行：分层配置 AdTrackingStimulate 的 S=0 时隐藏
    [SerializeField] private GameObject adsTrackGoldTable; // 行内 GoldTable（金币图标 + 数量）
    [SerializeField] private Text adsTrackGoldText;         // GoldTable 里的数量文本

    [SerializeField] private Text VersionText;
    [SerializeField] private Text HeaderText;
    // [SerializeField] private Text musicText; // 音乐文本显示
    // [SerializeField] private Text soundText; // 音效文本显示
    // [SerializeField] private Text vibrateText; // 震动文本显示

    // 广告关联（广告追踪授权）开关：预制体里已有现成的一行（adsTrackBtn / adsHandle），常驻显示，
    // 这里只负责同步开关状态（不再运行时克隆行）
    private AdTrackingManager _adTracking;

    Sprite Opensprite;
    Sprite Closesprite;

    protected void Start()
    {
       
        AttachToggleListeners(); // 绑定开关监听器
        Opensprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("UI_Icon_OpenToggle");
        Closesprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("UI_Icon_CloseToggle");
        UpdateToggleStates(false); // 启用时更新状态，不带动画
        ResolveAdTrackingNodes();   // 广告关联行 / GoldTable 节点（预制体未绑定时按名字兜底）
        RefreshAdTrackingToggle(); // 广告关联开关（预制体常驻行，只同步开关状态）
        RefreshAdTrackingEntry();  // 广告追踪激励：整行显隐 + GoldTable 金币数
#if UNITY_OPENHARMONY
        opinionBtn.gameObject.SetActive(true);
        restoreBuyBtn.gameObject.SetActive(false);
#else
        opinionBtn.gameObject.SetActive(true);
        restoreBuyBtn.gameObject.SetActive(false);
#endif
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        AudioManager.Instance.PlaySoundEffect("ShowUI");
        EventDispatcher.instance.OnChangeGoldUI += UpdateAutoPushToggle; // 订阅语言更新事件           
        OnChangeLanguage(); // 更新语言显示
        opinionBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("EvaluateButton03");
        privacyBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("PrivacyPolicy");
        termsBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("TermsAndService");
        VersionText.text = "Ver " + Application.version;

        UpdateAutoPushToggle();
        ResolveAdTrackingNodes();
        RefreshAdTrackingToggle();
        RefreshAdTrackingEntry();
        SubscribeAdTrackingEvent();
    }


    private void UpdateAutoPushToggle(int value=0,bool enable=false)
    {
        SetToggleVisuals(auHandle, GameDataManager.Instance.UserData.IsAutoPush); // 带动画更新音效手柄视觉
    }
    
    private void UpdateToggleStates(bool animate)
    {
        muClose.SetActive(!GameDataManager.Instance.UserData.IsMusicOn);// 更新音乐开关状态
        soClose.SetActive(!GameDataManager.Instance.UserData.IsSoundOn);// 更新音效开关状态
        viClose.SetActive(!GameDataManager.Instance.UserData.IsVibrationOn); // 更新音效开关状态
        
        // 根据当前开关状态更新视觉效果
        if (animate)
        {
            UpdateToggleVisuals(auHandle, GameDataManager.Instance.UserData.IsAutoPush); // 带动画更新音效手柄视觉
        }
        else
        {
            SetToggleVisuals(auHandle, GameDataManager.Instance.UserData.IsAutoPush); // 带动画更新音效手柄视觉
        }
    }

    private void SetToggleVisuals(GameObject handle, bool isOn)
    {
        handle.GetComponent<Image>().sprite = isOn ? Opensprite : Closesprite;
        // 直接设置位置，不带动画
        handle.transform.localPosition = new Vector3(isOn ? 64 : -64, handle.transform.localPosition.y, handle.transform.localPosition.z);
    }

    private void AttachToggleListeners()
    {
        MusicButton.AddClickAction(ToggleMusic); // 绑定音乐开关变更事件
        SoundBtn.AddClickAction(ToggleSounds); // 绑定音效开关变更事件
        VibrateBtn.AddClickAction(ToggleVibrate); // 绑定震动开关变更事件

        // ★ 广告关联开关：预制体里已有现成的一行（adsTrackBtn / adsHandle），
        //   之前只声明字段却从未绑定点击，所以点了完全没反应。
        if (adsTrackBtn != null)
            adsTrackBtn.AddClickAction(OnClickAdTracking);
    }

    private void OnChangeLanguage()
    {
        // 更新语言按钮和文本显示
        // musicText.text = MultilingualManager.Instance.GetString("Music").ToUpper(); // 音乐文本
        // soundText.text = MultilingualManager.Instance.GetString("Sounds").ToUpper(); // 音效文本
        // vibrateText.text = MultilingualManager.Instance.GetString("Vibrate").ToUpper(); // 音效文本
        HeaderText.text = MultilingualManager.Instance.GetString("Settings").ToUpper();
       
    }

    private void ToggleMusic()
    {
        bool isMusicOn = GameDataManager.Instance.UserData.IsMusicOn;
        GameDataManager.Instance.UserData.IsMusicOn = !isMusicOn; // 保存音乐开关状态
        AudioManager.Instance.ToggleMusic();; // 切换音乐状态
        
        muClose.SetActive(!GameDataManager.Instance.UserData.IsMusicOn);// 更新音乐开关状态

        AudioManager.Instance.TriggerVibration(40, 50);
    }

    private void ToggleVibrate()
    {
        bool isVibrateOn = GameDataManager.Instance.UserData.IsVibrationOn;
        GameDataManager.Instance.UserData.IsVibrationOn = !isVibrateOn; // 保存音效开关状态
        
        viClose.SetActive(!GameDataManager.Instance.UserData.IsVibrationOn); // 更新音效开关状态
    }
    
    private void ToggleAutoPush()
    {
        Game.self.Pushs.ReSetRequestEnableNotification();
    }

    // ============================================================
    // 广告关联（广告追踪授权）开关
    //      预制体里已经有现成的一行（adsTrackBtn / adsHandle），且这一行常驻显示；
    //      点开关 → 系统「广告追踪」设置界面，返回后由 OnAuthorizationChanged 同步开关状态。
    // ============================================================

    /// <summary>同步广告关联开关的视觉状态（手柄开/关）。行本身常驻显示，不做显隐控制。</summary>
    private void RefreshAdTrackingToggle()
    {
        if (adsHandle == null) return;

        if (Opensprite == null || Closesprite == null)
        {
            Opensprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("UI_Icon_OpenToggle");
            Closesprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("UI_Icon_CloseToggle");
        }

        if (_adTracking == null) _adTracking = AdTrackingManager.Instance;
        if (_adTracking == null) _adTracking = AdTrackingManager.EnsureInstance();

        // 开关状态 = 存档口径（与用户分层同源），不要用 Status（iOS/编辑器上那是系统 ATT，
        // 会和 adTrackingEnabled=1 的分层判定打架，表现为"分层算授权了，开关却显示关"）
        bool isOn = _adTracking != null && _adTracking.IsAuthorizedForUI;
        SetToggleVisuals(adsHandle, isOn);
    }

    private void OnClickAdTracking()
    {
        if (_adTracking == null) _adTracking = AdTrackingManager.Instance;
        if (_adTracking == null) _adTracking = AdTrackingManager.EnsureInstance();
        if (_adTracking == null) return;

        // ★ 按需求：直接调起系统「当前应用广告追踪」设置界面
        //   （鸿蒙 = 应用权限设置弹窗 requestPermissionOnSetting；iOS = 系统设置/ATT）
        //   玩家关闭该界面后，系统回调会把最新授权状态同步回来，这里通过 OnAuthorizationChanged 刷新开关。
        _adTracking.OpenSystemTrackingSettingsFromEntry();
    }

    // ============================================================
    // 广告追踪激励（AdTrackingStimulate = S_G）
    //      S=1：显示整行 + 未领取时在行内 GoldTable 上显示"金币图标 + G"；
    //      S=0：整行隐藏（不引导、不发奖）；
    //      奖励领过一次后 GoldTable 不再出现（UserData.adTrackingRewardClaimed）。
    // ============================================================

    /// <summary>预制体没绑字段时，按节点名在自身层级里兜底查找（递归，避免依赖具体路径）。</summary>
    private void ResolveAdTrackingNodes()
    {
        // if (adsTrackTable == null)
        // {
        //     Transform t = FindDeep(transform, "adsTrackTable");
        //     if (t != null) adsTrackTable = t.gameObject;
        // }

        if (adsTrackGoldTable == null)
        {
            Transform t = FindDeep(transform, "GoldTable");
            if (t != null) adsTrackGoldTable = t.gameObject;
        }

        // // ★ 预制体里 adsTrackTable 这个序列化字段被误绑到了 GoldTable（和 adsTrackGoldTable 是同一个节点），
        // //   结果整行显隐实际去开关了"金币角标"，真正的行永远不受控。这里按名字纠正一次。
        // if (adsTrackTable != null && adsTrackTable == adsTrackGoldTable)
        // {
        //     Transform t = FindDeep(transform, "adsTrackTable");
        //     if (t != null) adsTrackTable = t.gameObject;
        //     else adsTrackTable = null;
        // }

        if (adsTrackGoldText == null && adsTrackGoldTable != null)
            adsTrackGoldText = adsTrackGoldTable.GetComponentInChildren<Text>(true);
    }

    /// <summary>同步"广告关联"整行与激励角标（GoldTable）。</summary>
    private void RefreshAdTrackingEntry()
    {
        ResolveAdTrackingNodes();

        if (_adTracking == null) _adTracking = AdTrackingManager.Instance;
        if (_adTracking == null) _adTracking = AdTrackingManager.EnsureInstance();

        // 整行显隐：平台支持，且「已授权」或「当前分层有激励（S=1）」
        // ★ 已授权必须放行：授权后分层切到 IAA11（0_0），只看 S 会让整行消失，
        //   首次启动就同意追踪的玩家进设置会看不到"已开启"状态。
        bool showRow = _adTracking != null && _adTracking.ShouldShowSettingsRow();
        //if (adsTrackTable != null) adsTrackTable.SetActive(showRow);

        // 激励角标：还有"开启即得 G"可拿时才出现
        bool showGold = showRow && _adTracking.IsRewardClaimable;
        if (adsTrackGoldTable != null) adsTrackGoldTable.SetActive(showGold);
        if (showGold && adsTrackGoldText != null)
            adsTrackGoldText.text = _adTracking.StimulateGold.ToString();
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == name) return child;
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private void SubscribeAdTrackingEvent()
    {
        if (_adTracking == null) _adTracking = AdTrackingManager.Instance;
        if (_adTracking == null) _adTracking = AdTrackingManager.EnsureInstance();
        if (_adTracking == null) return;

        _adTracking.OnAuthorizationChanged -= HandleAdAuthorizationChanged;
        _adTracking.OnAuthorizationChanged += HandleAdAuthorizationChanged;
    }

    private void HandleAdAuthorizationChanged(bool granted)
    {
        RefreshAdTrackingToggle();
        // 授权成功后：GoldTable 收起（奖励已经到手/不再可领），整行状态同步
        RefreshAdTrackingEntry();
    }

    private void ToggleSounds()
    {
        
        bool isOn =  GameDataManager.Instance.UserData.IsSoundOn;
        
        GameDataManager.Instance.UserData.IsSoundOn = !isOn; // 保存音效开关状态

        AudioManager.Instance.TriggerVibration(40, 50);
        
        soClose.SetActive(!GameDataManager.Instance.UserData.IsSoundOn);// 更新音效开关状态
    }

    private void UpdateToggleVisuals(GameObject handle, bool isOn, float time = 0.2f)
    {
        handle.GetComponent<Image>().sprite = isOn ? Opensprite : Closesprite;
        // 带动画更新位置
        float targetPosition = isOn ? 64 : -64;
        handle.transform.DOLocalMoveX(targetPosition, time);
      
    }

    protected override void InitializeUIComponents()
    {
        HideButton.AddVibraClickAction(OnHideButton); // 绑定关闭按钮事件
        privacyBtn.AddClickAction(OnprivacyBtn);
        termsBtn.AddClickAction(OntermsBtn);
        opinionBtn.AddClickAction(OnOpinionBtn);
        restoreBuyBtn.AddClickAction(OnRestoreBuyBtn);
        copyButton.AddClickAction(OnCopyPackageAndOpenId);
        
        autopushBtn.onClick.AddListener(ToggleAutoPush); // 绑定音效开关变更事件
        
        // 添加无用的点击监听器
        var buttons = new Button[] { HideButton, privacyBtn, termsBtn };
        foreach (var btn in buttons)
        {
            btn.onClick.AddListener(() => {
                // 无意义的回调
                if (Random.value > 0.85f)
                {
                    Debug.Log($"[OptionsView] Button clicked: {btn.name}");
                }
            });
        }
    }
    
    // private void OnClickmyThemeBtn()
    // {
    //     SystemManager.Instance.ShowPanel(PanelType.MyThemeScreen);
    //     OnHideButton();
    // }

    private void OnOpinionBtn()
    {
        Application.OpenURL(ConfigManager.Instance.GetString("OpinionUrl"));
    }

    private void OnprivacyBtn()
    {
        Application.OpenURL(ConfigManager.Instance.GetString("PrivacyUrl"));
    }

    private void OntermsBtn()
    {
        Application.OpenURL(ConfigManager.Instance.GetString("TermsUrl"));
    }

    private void OnHideButton()
    {
        base.Close(); // 隐藏面板

        // 无意义的额外操作
        if (Time.time > 10f)
        {
            // 这个值不会被使用
            float dummy = Mathf.Sin(Time.time);
        }
    }
    
    private void OnRestoreBuyBtn()
    {
        // 统一走 ShopManager 的恢复购买（loading + 真实查询 + 结果 toast），
        // 原来的 Restore(OnRestoreBack) 回调是空实现，玩家点了没有任何结果（bug51）
        if (ShopManager.shopManager != null)
        {
            ShopManager.shopManager.RestorePurchases();
            return;
        }

        Game.self.Shop.Restore(OnRestoreBack);
    }

    private void OnRestoreBack(bool success, ProductItem[] items)
    {
        MessageSystem.Instance?.HideLoadingAnimation();

        int count = items != null ? items.Length : 0;
        MessageSystem.Instance?.ShowTip(count > 0 ? $"已恢复 {count} 笔内购" : "没有可恢复的内购");
    }
    /// <summary>
    /// 复制包名和 OpenId 到系统剪贴板
    /// </summary>
    public void OnCopyPackageAndOpenId()
    {
        // 1. 获取包名 (Bundle Identifier)
        string packageName = Application.identifier;

        // 2. 获取 OpenId (结合你现有的 GameDataManager 数据结构)
        string openId = "暂无数据";
        
        // 加入判空保护，防止游戏刚启动还没加载完数据时报错
        if (GameDataManager.Instance != null && GameDataManager.Instance.UserData != null)
        {
            openId = GameDataManager.Instance.UserData.UserId;
            
            // 如果本地 UserId 为空，也可以顺便获取一下设备的标识
            if (string.IsNullOrEmpty(openId))
            {
                openId = "本地为空，当前设备号: " + Game.self.GetUniqueId();
            }
        }

        // 3. 拼接文本
        string copyText = $"包名: {packageName}\n 用户ID: {openId}";

        // 4. 写入剪贴板 (Unity 原生核心 API)
        GUIUtility.systemCopyBuffer = copyText;

        // 5. 日志输出 (在真机上你可以换成调用你游戏内的飘字/Toast 提示)
        Debug.Log("复制成功：\n" + copyText);
        
        // 示例：如果你有飘字组件，可以加上
        // ToastManager.Show("信息已复制");
        MessageSystem.Instance.ShowTip("信息已复制");
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        EventDispatcher.instance.OnChangeGoldUI -= UpdateAutoPushToggle; // 取消订阅（原来误写成 += 会重复累加）
        if (_adTracking != null) _adTracking.OnAuthorizationChanged -= HandleAdAuthorizationChanged;
    }
}