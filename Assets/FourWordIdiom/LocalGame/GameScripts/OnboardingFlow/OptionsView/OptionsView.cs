using DG.Tweening;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class OptionsView : UIWindow
{
    [SerializeField] private Button AccountQuitBtn; // 关闭按钮
    [SerializeField] private Button HideButton; // 关闭按钮
    [SerializeField] private Toggle vibrateToggle; // 震动开关
    [SerializeField] private Toggle musicToggle; // 音乐开关
    [SerializeField] private Toggle soundsToggle; // 音效开关

    [SerializeField] private Button privacyBtn; // 隐私条款按钮
    [SerializeField] private Button termsBtn; // 服务协议按钮
    [SerializeField] private Button restoreBuyBtn; // 服务协议按钮

    [SerializeField] private GameObject muHandle; // 音乐开关的视觉手柄
    [SerializeField] private GameObject soHandle; // 音效开关的视觉手柄
    [SerializeField] private GameObject viHandle; // 震动开关的视觉手柄

    [SerializeField] private Text VersionText;
    //[SerializeField] private Text HeaderText;
    [SerializeField] private Text musicText; // 音乐文本显示
    //[SerializeField] private Text soundText; // 音效文本显示
    //[SerializeField] private Text vibrateText; // 震动文本显示

    Sprite Opensprite;
    Sprite Closesprite;

    protected void Start()
    {
       
        AttachToggleListeners(); // 绑定开关监听器
        Opensprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("UI_Icon_OpenToggle");
        Closesprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("UI_Icon_CloseToggle");
        UpdateToggleStates(false); // 启用时更新状态，不带动画
        
    }

    protected override void OnEnable()
    {
        AudioManager.Instance.PlaySoundEffect("ShowUI");
        //EventManager.OnChangeLanguageUpdateUI += OnChangeLanguage; // 订阅语言更新事件           
        OnChangeLanguage(); // 更新语言显示
        //privacyBtn.GetComponentInChildren<Text>().text = LanguageManager.Instance.GetString("PrivacyPolicy");
        //termsBtn.GetComponentInChildren<Text>().text = LanguageManager.Instance.GetString("TermsAndService");
        VersionText.text = "Ver " + Application.version;
    }

    private void UpdateToggleStates(bool animate)
    {
        musicToggle.isOn = GameDataManager.instance.UserData.IsMusicOn; // 更新音乐开关状态
        soundsToggle.isOn = GameDataManager.instance.UserData.IsSoundOn; // 更新音效开关状态
        //vibrateToggle.isOn = GameDataManager.instance.UserData.IsVibrationOn; // 更新音效开关状态
        // 根据当前开关状态更新视觉效果
        if (animate)
        {
            UpdateToggleVisuals(muHandle, musicToggle.isOn); // 带动画更新音乐手柄视觉
            UpdateToggleVisuals(soHandle, soundsToggle.isOn); // 带动画更新音效手柄视觉
            UpdateToggleVisuals(viHandle, vibrateToggle.isOn); // 带动画更新音效手柄视觉
        }
        else
        {
            // 直接设置颜色和位置，不带动画
            SetToggleVisuals(muHandle, musicToggle.isOn);
            SetToggleVisuals(soHandle, soundsToggle.isOn);
            SetToggleVisuals(viHandle, vibrateToggle.isOn); // 带动画更新音效手柄视觉
        }
    }

    private void SetToggleVisuals(GameObject handle, bool isOn)
    {
        handle.GetComponent<Image>().sprite = isOn ? Opensprite : Closesprite;
        // 直接设置位置，不带动画
        handle.transform.localPosition = new Vector3(isOn ? 52 : -52, handle.transform.localPosition.y, handle.transform.localPosition.z);
    }

    private void AttachToggleListeners()
    {
        musicToggle.onValueChanged.AddListener(ToggleMusic); // 绑定音乐开关变更事件
        soundsToggle.onValueChanged.AddListener(ToggleSounds); // 绑定音效开关变更事件
        vibrateToggle.onValueChanged.AddListener(ToggleVibrate); // 绑定音效开关变更事件
        
        AccountQuitBtn.onClick.AddListener(AccountQuit);

        // 添加无用的点击监听器
        foreach (var toggle in new Toggle[] { musicToggle, soundsToggle, vibrateToggle })
        {
            toggle.onValueChanged.AddListener((value) => {
                // 无意义的回调
                if (Random.value > 0.8f)
                {
                    Debug.Log($"[OptionsView] Toggle state changed to {value}");
                }
            });
        }
    }
    
    private void AccountQuit()
    {
        SystemManager.Instance.ShowPanel(PanelType.AppRating);
        OnHideButton();
        //WaterManager.instance.ClearWater();
    }

    private void OnChangeLanguage()
    {
        // 更新语言按钮和文本显示
        //musicText.text = LanguageManager.Instance.GetString("Music").ToUpper(); // 音乐文本
        //soundText.text = LanguageManager.Instance.GetString("Sounds").ToUpper(); // 音效文本
        //vibrateText.text = LanguageManager.Instance.GetString("Vibrate").ToUpper(); // 音效文本
        //HeaderText.text = LanguageManager.Instance.GetString("Settings").ToUpper();
       
    }

    private void ToggleMusic(bool isOn)
    {
        GameDataManager.instance.UserData.IsMusicOn = isOn; // 保存音乐开关状态
        AudioManager.Instance.ToggleMusic(); // 切换音乐状态
        UpdateToggleVisuals(muHandle, isOn); // 更新音乐手柄视觉
        
    }

    private void ToggleVibrate(bool isOn)
    {
        //GameDataManager.instance.UserData.IsVibrationOn = isOn; // 保存音效开关状态
        UpdateToggleVisuals(viHandle, isOn); // 更新音效手柄视觉
    }

    private void ToggleSounds(bool isOn)
    {
        GameDataManager.instance.UserData.IsSoundOn = isOn; // 保存音效开关状态
        UpdateToggleVisuals(soHandle, isOn); // 更新音效手柄视觉

        // 无意义的额外操作
        if (!isOn)
        {
            // 这个值不会被使用
            float dummy = Mathf.Pow(Time.time, 0.5f);
        }
    }

    private void UpdateToggleVisuals(GameObject handle, bool isOn, float time = 0.2f)
    {
        handle.GetComponent<Image>().sprite = isOn ? Opensprite : Closesprite;
        // 带动画更新位置
        float targetPosition = isOn ? 55 : -55;
        handle.transform.DOLocalMoveX(targetPosition, time);

        // 添加无意义的额外动画
        if (Random.value > 0.7f)
        {
            handle.transform.DOPunchScale(new Vector3(0.1f, 0.1f, 0), 0.1f);
        }
    }

    protected override void InitializeUIComponents()
    {
        HideButton.AddClickAction(OnHideButton); // 绑定关闭按钮事件
        privacyBtn.AddClickAction(OnprivacyBtn);
        termsBtn.AddClickAction(OntermsBtn);
        restoreBuyBtn.AddClickAction(OnRestoreBuyBtn);
        
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

    private void OnprivacyBtn()
    {
        Application.OpenURL("https://mindwordplay.cn/ysxyb");
    }

    private void OntermsBtn()
    {
        Application.OpenURL("https://mindwordplay.cn/yhxyb");
    }

    private void OnHideButton()
    {
        base.Close(); // 隐藏面板
    }
    
    private void OnRestoreBuyBtn()
    {
        //ShopManager.shopManager.iapManager.UserInitiatedRestore(true);
    }
   

    public override void OnHideAnimationEnd()
    {
        base.OnHideAnimationEnd();
    }

    protected override void OnDisable()
    {
        //EventManager.OnChangeLanguageUpdateUI -= OnChangeLanguage; // 取消订阅以避免内存泄漏
    }
}