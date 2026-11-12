using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

public class PauseGameScreen : UIWindow
{
    private const int SKIP_COOLDOWN_SECONDS = 100; // 跳关冷却时长（秒）

    [Header("面板节点")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject continuePanel;
    [Header("声音控制")]
    [SerializeField] private Toggle musicToggle; // 音乐开关
    [SerializeField] private Toggle soundsToggle; // 音效开关
    [SerializeField] private Toggle vibrateToggle; // 震动开关
    [SerializeField] private Toggle emojiToggle;   // emoji开关
    [SerializeField] private GameObject muHandle; // 音乐开关的视觉手柄
    [SerializeField] private GameObject soHandle; // 音效开关的视觉手柄
    [SerializeField] private GameObject viHandle; // 震动开关的视觉手柄
    [SerializeField] private GameObject emojiHandle; // emoji开关的视觉手柄
    [SerializeField] private Text musicText; // 音乐文本显示
    [SerializeField] private Text soundText; // 音效文本显示
    [SerializeField] private Text vibrateText; // 震动文本显示
    
    [Header("主暂停界面按钮")]
    [SerializeField] private Text titleText;
    [SerializeField] private Button closeBtn;
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
    private Text _jumpBtnText;                       // 跳关按钮文案（冷却倒计时会改写它）
    private Coroutine _jumpCooldownCoroutine;        // 跳关冷却倒计时协程

    protected override void Awake()
    {
        base.Awake();
        Opensprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("UI_Icon_OpenToggle");
        Closesprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("UI_Icon_CloseToggle");
        if (jumpBtn != null) _jumpBtnText = jumpBtn.GetComponentInChildren<Text>(true);
    }

    // Start is called before the first frame update
    void Start()
    {
        closeBtn.AddClickAction(OnCloseClicked);
        continueBtn.AddClickAction(OnCloseClicked);
        returnGame.AddClickAction(OnCloseClicked);
        jumpBtn.AddClickAction(OnJumpClicked);
        exitBtn.AddClickAction(OnExitClicked);
        realExitBtn.AddClickAction(OnRealExitClicked);
        
        musicText.text = MultilingualManager.Instance.GetString("Music").ToUpper(); // 音乐文本
        soundText.text = MultilingualManager.Instance.GetString("Sounds").ToUpper(); // 音效文本
        vibrateText.text = MultilingualManager.Instance.GetString("Vibrate").ToUpper(); // 音效文本
        musicToggle.onValueChanged.AddListener(ToggleMusic); // 绑定音乐开关变更事件
        soundsToggle.onValueChanged.AddListener(ToggleSounds); // 绑定音效开关变更事件
        vibrateToggle.onValueChanged.AddListener(ToggleVibrate); // 绑定音效开关变更事件
        if (emojiToggle != null) emojiToggle.onValueChanged.AddListener(ToggleEmoji); // emoji 开关（预制体没挂节点时跳过）
        
        titleText.text = MultilingualManager.Instance.GetString("Pause");
        if (_jumpBtnText != null) _jumpBtnText.text = MultilingualManager.Instance.GetString("SkipLevel");
        continueBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("Continue");
        exitBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("Exit");
        
        conTitleText.text = MultilingualManager.Instance.GetString("AreYouSure");
        conTipText.text = MultilingualManager.Instance.GetString("YouWillLose");
        realExitBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("ConfirmExit");
        returnGame.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("LetMeThink");

        // Start 在首次 OnEnable 之后执行，会覆盖掉 OnEnable 里写入的冷却文案，这里重新按时间戳刷一次
        RefreshJumpButtonState();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        pausePanel.SetActive(true);
        continuePanel.SetActive(false);
        AnalyticMgr.VideoAdShow("跳关广告");
        
        // ★ 复活后是「不限时」状态：暂停界面不显示倒计时，时间位置直接隐藏（不显示文案）
        if (ChessPlayArea.Instance != null && ChessPlayArea.Instance.IsUnlimitedTime)
        {
            gameTimeText.gameObject.SetActive(false);
        }
        else
        {
            float rawTime = ChessStageController.Instance.CurrStageData.RemainingTime;
            float safeTime = Mathf.Max(0, rawTime);
            int totalSeconds = Mathf.CeilToInt(safeTime);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            gameTimeText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
            gameTimeText.gameObject.SetActive(true);
        }
        
        RefreshPupaIconVisibility();
        
        musicToggle.isOn = GameDataManager.Instance.UserData.IsMusicOn; // 更新音乐开关状态
        soundsToggle.isOn = GameDataManager.Instance.UserData.IsSoundOn; // 更新音效开关状态
        vibrateToggle.isOn = GameDataManager.Instance.UserData.IsVibrationOn; // 更新音效开关状态
        if (emojiToggle != null)
        {
            emojiToggle.isOn = GameDataManager.Instance.UserData.IsEmojiOn; // 同步 emoji 开关状态
            SetToggleVisuals(emojiHandle, emojiToggle.isOn);                // 打开面板时先把手柄摆到正确位置
        }
        
        SetToggleVisuals(muHandle, musicToggle.isOn);
        SetToggleVisuals(soHandle, soundsToggle.isOn);
        SetToggleVisuals(viHandle, vibrateToggle.isOn);

        // ★ 玩家点开暂停 = 随时可能被杀的存档检查点：立刻把本局进度（棋盘 / 字盘 / 词本 / 剩余时间）落盘，
        //   这样暂停后关进程再进来，能恢复到暂停那一刻。
        //   注意：这里【不能】把 IsPausedOrFailed 置 true —— 那个标记代表「在失败界面强退」，
        //   置 true 会让下次启动走防逃逸分支（清档 + 扣体力），表现就是「暂停后杀进程，进度全丢」。
        SaveCurrentLevelProgress();

        // ★ 跳关冷却：每次打开暂停界面都按上次跳关时间戳重新判定，未冷却完则禁用按钮并走倒计时
        RefreshJumpButtonState();
    }

    /// <summary>
    /// 把当前关卡进度马上写入硬盘（暂停时调用，防止玩家直接杀进程导致进度丢失）。
    /// </summary>
    private void SaveCurrentLevelProgress()
    {
        ChessStageController ctrl = ChessStageController.Instance;
        if (ctrl == null || ctrl.CurrStageData == null) return;

        // 暂停不是失败，清掉失败标记，避免读档时被判定成「失败界面强退」
        ctrl.CurrStageData.IsPausedOrFailed = false;
        ctrl.CurrStageData.SaveToFile();

        Debug.Log($"[PauseGameScreen] 暂停存档检查点已写入：关卡 {ctrl.CurrStageData.StageId}，剩余 {ctrl.CurrStageData.RemainingTime:F1}s");
    }

    #region 跳关冷却

    /// <summary>
    /// 距离下次可跳关还剩多少秒（0 表示已可跳关）。
    /// 以 UTC 绝对时间戳计算，杀掉进程/重开游戏依然有效。
    /// </summary>
    private int GetSkipCooldownRemainSeconds()
    {
        var gm = GameDataManager.Instance;
        if (gm == null || gm.OverallRank == null) return 0;

        long lastTime = gm.OverallRank.LastSkipLevelTime;
        long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long elapsed = currentTime - lastTime;

        // lastTime<=0：从未跳过；elapsed<0：玩家把手机时间往回调了，一律视为冷却结束，避免卡死
        if (lastTime <= 0 || elapsed >= SKIP_COOLDOWN_SECONDS || elapsed < 0) return 0;

        return (int)(SKIP_COOLDOWN_SECONDS - elapsed);
    }

    /// <summary>
    /// 按冷却剩余时间刷新跳关按钮：冷却中禁用按钮并每秒刷新倒计时文案。
    /// </summary>
    private void RefreshJumpButtonState()
    {
        if (_jumpCooldownCoroutine != null)
        {
            StopCoroutine(_jumpCooldownCoroutine);
            _jumpCooldownCoroutine = null;
        }

        int remain = GetSkipCooldownRemainSeconds();
        if (remain <= 0)
        {
            ResetJumpButtonToReady();
        }
        else if (isActiveAndEnabled)
        {
            _jumpCooldownCoroutine = StartCoroutine(CoJumpCooldown(remain));
        }
    }

    private IEnumerator CoJumpCooldown(int remainingSeconds)
    {
        if (jumpBtn != null) jumpBtn.interactable = false;
        string originText = MultilingualManager.Instance.GetString("SkipLevel");

        while (remainingSeconds > 0)
        {
            if (_jumpBtnText != null)
            {
                _jumpBtnText.text = $"{originText} ({remainingSeconds}s)";
            }
            // 用真实时间，暂停面板会改 timeScale，用 WaitForSeconds 会卡住
            yield return new WaitForSecondsRealtime(1.0f);
            remainingSeconds--;
        }

        ResetJumpButtonToReady();
        _jumpCooldownCoroutine = null;
    }

    private void ResetJumpButtonToReady()
    {
        if (jumpBtn != null) jumpBtn.interactable = true;
        if (_jumpBtnText != null)
        {
            _jumpBtnText.text = MultilingualManager.Instance.GetString("SkipLevel");
        }
    }

    #endregion
    
    private void ToggleMusic(bool isOn)
    {
        GameDataManager.Instance.UserData.IsMusicOn = isOn; // 保存音乐开关状态
        AudioManager.Instance.ToggleMusic();; // 切换音乐状态
        UpdateToggleVisuals(muHandle, isOn); // 更新音乐手柄视觉
        AudioManager.Instance.TriggerVibration(40, 50);
    }
    private void ToggleSounds(bool isOn)
    {
        GameDataManager.Instance.UserData.IsSoundOn = isOn; // 保存音效开关状态
        UpdateToggleVisuals(soHandle, isOn); // 更新音效手柄视觉
        AudioManager.Instance.TriggerVibration(40, 50);
    }
    
    private void ToggleVibrate(bool isOn)
    {
        GameDataManager.Instance.UserData.IsVibrationOn = isOn; // 保存音效开关状态
        UpdateToggleVisuals(viHandle, isOn); // 更新音效手柄视觉
    }
    private void ToggleEmoji(bool isOn)
    {
        GameDataManager.Instance.UserData.IsEmojiOn = isOn; // 保存状态到存档
        if (emojiHandle != null) UpdateToggleVisuals(emojiHandle, isOn); // 平滑移动手柄与切换精灵
        AudioManager.Instance.TriggerVibration(40, 50);
        
        // 通知局内棋盘/字块实时切换 emoji 与文字显示
        EventDispatcher.instance.TriggerEmojiStateChanged(isOn);
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
        // 双保险：冷却期内即便按钮状态被外部改回可点，也不重复拉广告
        if (GetSkipCooldownRemainSeconds() > 0)
        {
            RefreshJumpButtonState();
            return;
        }

        AnalyticMgr.VideoAdClick("跳关广告");
        AdRuleManager.Instance.TryShowRewardVideo(Define.AdKey.RewardAdIdStoreGold,HandleVideoResult);
    }

    private void HandleVideoResult(bool isShow)
    {
        if (isShow)
        {
            // 🌟 记录本次跳关时间戳，启动 100 秒冷却
            GameDataManager.Instance.OverallRank.LastSkipLevelTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            // 立即落盘：跳关后界面马上关闭，不能指望后续结算存档，避免杀进程后冷却被绕过
            GameDataManager.Instance.OverallRank.SaveData();
            
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
        
        if (GameDataManager.Instance.UserData.HasUnlimitedEnergy())
        {
            OnRealExitClicked();
        }
        else
        {
            EventDispatcher.instance.TriggerHighlightHeaderUI(true);
        }
       
    }
    /// <summary>
    /// 在二次确认面板点击【取消退出/继续游戏】
    /// </summary>
    private void OnRealExitClicked()
    {
        if (ChessPlayArea.Instance != null)
        {
            if (ChessPlayArea.Instance.chessboardGrid != null)
            {
                ChessPlayArea.Instance.chessboardGrid.IsBlockInput = true;
            }
            // 锁死下方的待填字盘
            ChessBowlGrid.IsTutorialBlocking = true; 
        }
        EventDispatcher.instance.TriggerHighlightHeaderUI(false);
        SystemManager.Instance.HidePanel(PanelType.HeaderSection,false);
        // 退出损失体力和连击分, 并清理当前关卡游戏状态
        ChessPlayArea.Instance.QuitGameAndDeductEnergy();
        if (ChessStageController.Instance.CurrStageData != null && 
            ChessStageController.Instance.CurrStageData.FoundTargetPuzzles.Count > 0)
        {
            AnalyticMgr.LevelExit();
        }
        SystemManager.Instance.HidePanel(PanelType.PauseGameScreen);
        ChessGuideSystem.Instance.CloseGuide();
        
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
        // 停掉冷却倒计时，避免面板关闭后协程还在跑；下次 OnEnable 会按时间戳重新起算
        if (_jumpCooldownCoroutine != null)
        {
            StopCoroutine(_jumpCooldownCoroutine);
            _jumpCooldownCoroutine = null;
        }

        // 🌟 界面关闭时，通知 HeaderSection 取消高亮
        EventDispatcher.instance.TriggerHighlightHeaderUI(false);
        base.OnDisable();
    }
}
