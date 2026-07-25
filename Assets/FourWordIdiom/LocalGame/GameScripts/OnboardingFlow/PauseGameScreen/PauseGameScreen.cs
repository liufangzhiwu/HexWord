using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

public class PauseGameScreen : UIWindow
{
    [Header("面板节点")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject continuePanel;
    [Header("声音控制")]
    [SerializeField] private Toggle musicToggle; // 音乐开关
    [SerializeField] private Toggle soundsToggle; // 音效开关
    [SerializeField] private Toggle vibrateToggle; // 震动开关
    
    [SerializeField] private GameObject muHandle; // 音乐开关的视觉手柄
    [SerializeField] private GameObject soHandle; // 音效开关的视觉手柄
    [SerializeField] private GameObject viHandle; // 震动开关的视觉手柄
    [SerializeField] private Text musicText; // 音乐文本显示
    [SerializeField] private Text soundText; // 音效文本显示
    [SerializeField] private Text vibrateText; // 震动文本显示
    
    [Header("主暂停界面按钮")]
    [SerializeField] private Text titleText;
    [SerializeField] private Button jumpBtn;
    [SerializeField] private Button continueBtn;
    [SerializeField] private Button exitBtn;
    [SerializeField] private Text gameTimeText;
    
    [Header("二次确认界面按钮")]
    [SerializeField] private Text conTitleText;
    [SerializeField] private Text conTipText;
    [SerializeField] private Button realExitBtn;
    [SerializeField] private Button returnGame;
    [SerializeField] private GameObject confirmPupaIcon;
    
    Sprite Opensprite;
    Sprite Closesprite;

    protected override void Awake()
    {
        base.Awake();
        Opensprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("UI_Icon_OpenToggle");
        Closesprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("UI_Icon_CloseToggle");
    }

    // Start is called before the first frame update
    void Start()
    {
        //closeBtn.AddClickAction(OnCloseClicked);
        continueBtn.AddVibraClickAction(OnCloseClicked);
        returnGame.AddVibraClickAction(OnCloseClicked);
        jumpBtn.AddVibraClickAction(OnJumpClicked);
        exitBtn.AddVibraClickAction(OnExitClicked);
        realExitBtn.AddVibraClickAction(OnRealExitClicked);
        
        musicText.text = MultilingualManager.Instance.GetString("Music").ToUpper(); // 音乐文本
        soundText.text = MultilingualManager.Instance.GetString("Sounds").ToUpper(); // 音效文本
        musicToggle.onValueChanged.AddListener(ToggleMusic); // 绑定音乐开关变更事件
        soundsToggle.onValueChanged.AddListener(ToggleSounds); // 绑定音效开关变更事件
        vibrateToggle.onValueChanged.AddListener(ToggleVibrate); // 绑定音效开关变更事件

        titleText.text = MultilingualManager.Instance.GetString("Pause");
        jumpBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("SkipLevel");
        continueBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("Continue");
        exitBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("Exit");
        
        conTitleText.text = MultilingualManager.Instance.GetString("AreYouSure");
        conTipText.text = MultilingualManager.Instance.GetString("YouWillLose");
        realExitBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("ConfirmExit");
        returnGame.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("LetMeThink");
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        pausePanel.SetActive(true);
        continuePanel.SetActive(false);
        
        float rawTime = ChessStageController.Instance.CurrStageData.RemainingTime;
        float safeTime = Mathf.Max(0, rawTime);
        int minutes = Mathf.FloorToInt(safeTime / 60F);
        int seconds = Mathf.FloorToInt(safeTime % 60f);
        gameTimeText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        gameTimeText.gameObject.SetActive(true);
        
        RefreshPupaIconVisibility();
        
        musicToggle.isOn = GameDataManager.Instance.UserData.IsMusicOn; // 更新音乐开关状态
        soundsToggle.isOn = GameDataManager.Instance.UserData.IsSoundOn; // 更新音效开关状态
        vibrateToggle.isOn = GameDataManager.Instance.UserData.IsVibrationOn; // 更新音效开关状态
        
        SetToggleVisuals(muHandle, musicToggle.isOn);
        SetToggleVisuals(soHandle, soundsToggle.isOn);
        SetToggleVisuals(viHandle, vibrateToggle.isOn);
        
        // 🌟 玩家点开了暂停，视为进入“嫌疑状态”，立即存入硬盘！
        ChessStageController.Instance.CurrStageData.IsPausedOrFailed = true;
        GameDataManager.Instance.CommitGameData();
    }
    private void ToggleMusic(bool isOn)
    {
        GameDataManager.Instance.UserData.IsMusicOn = isOn; // 保存音乐开关状态
        AudioManager.Instance.ToggleMusic();; // 切换音乐状态
        UpdateToggleVisuals(muHandle, isOn); // 更新音乐手柄视觉
    }
    private void ToggleSounds(bool isOn)
    {
        GameDataManager.Instance.UserData.IsSoundOn = isOn; // 保存音效开关状态
        UpdateToggleVisuals(soHandle, isOn); // 更新音效手柄视觉
    }
    
    private void ToggleVibrate(bool isOn)
    {
        GameDataManager.Instance.UserData.IsVibrationOn = isOn; // 保存音效开关状态
        UpdateToggleVisuals(viHandle, isOn); // 更新音效手柄视觉
    }
    
    private void UpdateToggleVisuals(GameObject handle, bool isOn, float time = 0.2f)
    {
        handle.GetComponent<Image>().sprite = isOn ? Opensprite : Closesprite;
        // 带动画更新位置
        float targetPosition = isOn ? 64 : -64;
        handle.transform.DOLocalMoveX(targetPosition, time);
    }
    private void SetToggleVisuals(GameObject handle, bool isOn)
    {
        handle.GetComponent<Image>().sprite = isOn ? Opensprite : Closesprite;
        // 直接设置位置，不带动画
        handle.transform.localPosition = new Vector3(isOn ? 64 : -64, handle.transform.localPosition.y, handle.transform.localPosition.z);
    }
    private void OnCloseClicked()
    {
        SystemManager.Instance.HidePanel(PanelType.PauseGameScreen);
        ChessStageController.Instance.CurrStageData.IsPausedOrFailed = false;
        GameDataManager.Instance.CommitGameData();
        ChessPlayArea.Instance.ResumeGame();
    }

    private void OnJumpClicked()
    {
        AnalyticMgr.VideoAdClick("跳关广告");
        AdRuleManager.Instance.TryShowRewardVideo(Define.AdKey.RewardAdIdStoreGold,HandleVideoResult);
    }

    private void HandleVideoResult(bool isShow)
    {
        if (isShow)
        {
            // ==========================================
            // 🌟 核心修改：跳过关卡，强行抹除一切临时收益
            // ==========================================
            // ==========================================
            // 继续执行 UI 跳转与 0 收益结算
            // ==========================================
            SystemManager.Instance.HidePanel(PanelType.ChessLearningGuide);
            SystemManager.Instance.HidePanel(PanelType.PauseGameScreen);
        
            ChessPlayArea.Instance.ResumeGame();
            ChessPlayArea.Instance.GamePlayOver(isJump: true);
            GameDataManager.Instance.UserData.totalSeeAds++;
            AnalyticMgr.VideoAdSuccess("跳关广告");
        }
        else
        {
            MessageSystem.Instance.ShowTip("广告加载失败，请稍后重试。");
            AnalyticMgr.VideoAdFail("跳关广告");
        }
    }

    private void OnExitClicked()
    {
        pausePanel.SetActive(false);
        continuePanel.SetActive(true);
        gameTimeText.gameObject.SetActive(false);
        EventDispatcher.instance.TriggerHighlightHeaderUI(true);
    }
    /// <summary>
    /// 在二次确认面板点击【取消退出/继续游戏】
    /// </summary>
    private void OnRealExitClicked()
    {
        SystemManager.Instance.HidePanel(PanelType.ChessLearningGuide);
        // 退出损失体力和连击分, 并清理当前关卡游戏状态
        SystemManager.Instance.HidePanel(PanelType.HeaderSection, true, () =>
        {
            ChessPlayArea.Instance.QuitGameAndDeductEnergy();
            EventDispatcher.instance.TriggerHighlightHeaderUI(false);
        });
        if (ChessStageController.Instance.CurrStageData != null && 
            ChessStageController.Instance.CurrStageData.FoundTargetPuzzles.Count > 0)
        {
            AnalyticMgr.LevelExit();
        }
        SystemManager.Instance.HidePanel(PanelType.PauseGameScreen);
        
    }
    /// <summary>
    /// 根据当前关卡能力，刷新蝶蛹图标的显示
    /// </summary>
    private void RefreshPupaIconVisibility()
    {
        if (confirmPupaIcon == null) return;

        // 1. 获取当前关卡的理论最高分，判断本关【有没有资格】掉落
        int optimalScore = ChessStageController.Instance.OptimalTotalScore;
        bool canLevelProduce = ButterfliesManager.Instance.CanShowPupaProgressBarThisLevel(optimalScore);
        
        // 2. 获取玩家【当前】的分数，和拿到蝶蛹需要的【门槛分数】
        int currentScore = ChessStageController.Instance.CurrentTotalScore;
        int threshold = ButterfliesManager.Instance.GetScoreThresholdForPupa();

        // 3. 判断当前分数是否已经够换至少一个蝶蛹
        bool currentlyHasPupa = currentScore >= threshold;

        // 4. 只有这两个条件同时满足，才显示警告图标：
        // (本关有掉落资格) 并且 (玩家当前分数已经达标了)
        bool shouldShowWarning = canLevelProduce && currentlyHasPupa;
        
        confirmPupaIcon.SetActive(shouldShowWarning);
    }
    protected override void OnDisable()
    {
        // 🌟 界面关闭时，通知 HeaderSection 取消高亮
        EventDispatcher.instance.TriggerHighlightHeaderUI(false);
        base.OnDisable();
    }
}
