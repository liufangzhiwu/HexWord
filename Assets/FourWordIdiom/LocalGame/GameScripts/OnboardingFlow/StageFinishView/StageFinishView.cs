using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Middleware;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 关卡完成界面控制器
/// 处理关卡完成后的UI展示和交互逻辑
/// </summary>
public class StageFinishView : UIWindow
{
    [Header("UI References")]
    [SerializeField] private Button SignBtn;
    [SerializeField] private LimitBtnTable _limitBtnTable;
    [SerializeField] private MatchFishTable _matchFishtable;
    [SerializeField] private TaskTable _tasktable;

    [SerializeField] private GameObject hardStageTable;          // 困难模式
    [SerializeField] private GameObject extrahardStageTable;     // 特别困难模式
  
    [Header("禅意飞行特效")]
    [SerializeField] private ZenRankButton _zenRankBtn;    // 🌟 这里把类型改成具体的脚本类型

    [SerializeField] private Button Content;
    [SerializeField] private GameObject Showlimiticon;
    [SerializeField] private GameObject Enlimiticon;
    [SerializeField] private Button _nextStageButton;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private Text _StageNumberText;
    //[SerializeField] private Toggle _puzzletoggle;
    [SerializeField] private Text progressText;
    [SerializeField] private GameObject _butterflyTimerDisplay;
    [SerializeField] private Image logo;

    private GameObject _treasureBoxEffect;
    private bool isGetGold = false;
    private bool _hasPlayedRewardSequence = false;
    private void Start()
    {
        logo.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromBundle(ToolUtil.GetLanguageBundle(), "ui_logo");
    }

    protected override void InitializeUIComponents()
    {
        _nextStageButton.AddVibraClickAction(OnNextStageButtonClicked);
        _limitBtnTable._limitTimeEventButton.AddClickAction(OnLimitTimeEventButtonClicked);

        Content.onClick.AddListener(() =>
        {
            SystemManager.Instance.ShowPanel(PanelType.LimitTimeScreen);
        });
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        _zenRankBtn.gameObject.SetActive(false);
        //_zenRankBtn.GetComponent<ZenRankButton>().CheckRankProgress();
        GameCoreManager.Instance.PanelState = PanelState.FinishHexPanel;
        if (GameDataManager.Instance.UserData.curIsEnter)
        {
            _hasPlayedRewardSequence = false;
        }
        InitializeUI();
        UnlockBtnsUI();

        GameDataManager.Instance.UserData.curIsEnter = false;

        if (LimitTimeManager.Instance != null)
        {
            LimitTimeManager.Instance.OnDailyTimeUpdated += UpdateTimeDisplay;
            LimitTimeManager.Instance.OnLimitTimeBtnUI += UpdateSliderProgress;
        }

        if (DailyTaskManager.Instance != null)
            DailyTaskManager.Instance.OnDailyButterflyTaskUI += UpdateButterflyTime;

        if (FishInfoController.Instance != null)
            FishInfoController.Instance.OnFishTimeUpdated += _matchFishtable.UpdateFishTime;

        _matchFishtable.CheckFishBtn();
        AudioManager.Instance.PlaySoundEffect("StageFinish");
        StartCoroutine(CheckCompletedState());
        DailyTaskManager.Instance.UpateButterflyTaskUI();

        SetUIInteractable(true);
        
        if (GameDataManager.Instance.UserData.ischangetheme)
        {
            int times=1;
            if (GameDataManager.Instance.UserData.ThemeItemUses.Keys.Contains(GameDataManager.Instance.UserData.userthemeid))
            {
                times=GameDataManager.Instance.UserData.ThemeItemUses[GameDataManager.Instance.UserData.userthemeid];
            }
            AnalyticMgr.ThemeUse(GameDataManager.Instance.UserData.userthemeid,times);
        }
        
        //StartCoroutine(CheckZenRankBtn());
        
        bool isShowWinSign = StreakManager.Instance.IsCanShowWinSign();
        if (isShowWinSign)
        {
            SystemManager.Instance.ShowPanel(PanelType.SignWinScreen);
        }
    }



    private IEnumerator CheckCompletedState()
    {
        yield return UpdateFishRankUI();
#if Unity_ShowLog || UNITY_EDITOR
        if (GameCoreManager.Instance.IsTrueAuto)
        {
            yield return new WaitForSeconds(1.2f);
            OnNextStageButtonClicked();
        }
#endif
    }

    /// <summary>
    /// 初始化UI元素状态和数值
    /// </summary>
    private void InitializeUI()
    {
        isGetGold = false;

        // 设置关卡文本
        int stage = GameDataManager.Instance.UserData.CurrentHexStage;
        _StageNumberText.text = MultilingualManager.Instance.GetString("Level") + " " + stage;

        // 限时活动进度显示控制
        if (LimitTimeManager.Instance != null && LimitTimeManager.Instance.IsComplete())
            progressSlider.transform.parent.gameObject.SetActive(false);
        else
            progressSlider.transform.parent.gameObject.SetActive(true);

        // 直接同步显示当前进度
        //UpdateSliderProgress();
      

        // 难度模式显示
        ChessStageController.Instance.CurLevelMode = GetLevelDifficulty(stage);
        switch (ChessStageController.Instance.CurLevelMode)
        {
            case LevelModes.Normal:
                hardStageTable.gameObject.SetActive(false);
                extrahardStageTable.gameObject.SetActive(false);
                break;
            case LevelModes.Hard:
                hardStageTable.gameObject.SetActive(true);
                extrahardStageTable.gameObject.SetActive(false);
                break;
            case LevelModes.ExtraHard:
                hardStageTable.gameObject.SetActive(false);
                extrahardStageTable.gameObject.SetActive(true);
                break;
        }
        
        if (!LimitTimeManager.Instance.IsComplete() && _limitBtnTable._limitTimeEventButton.gameObject.activeSelf)
        {
            Content.gameObject.SetActive(true);
            StartCoroutine(UpdateProgress());
        }
        else
        {
            Content.gameObject.SetActive(false);
        }
        if (!_hasPlayedRewardSequence)
        {
            _hasPlayedRewardSequence = true; // 标记为已播放
            _nextStageButton.gameObject.SetActive(false); // 只有首次进入才先隐藏，等动画播完再显示
            StartCoroutine(PlayRewardSequence());
        }
        else
        {
            // 已经播放过了（从排行榜返回），直接把下一步按钮显示出来，防止界面卡死
            _nextStageButton.gameObject.SetActive(true);
            CanvasGroup cg = _nextStageButton.gameObject.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1;

            // 维持限时活动图标的状态
            if (_limitBtnTable._limitTimeEventButton.gameObject.activeSelf && LimitTimeManager.Instance != null &&
                GetCompletedWordCount() >= LimitTimeManager.Instance.CurlimitData.num)
            {
                Showlimiticon.SetActive(true);
            }
        }
        DailyTaskManager.Instance.UpdateMaxButterflyTime();
    }

    LevelModes GetLevelDifficulty(int levelNumber)
    {
        if (levelNumber % 5 == 0)
        {
            if ((levelNumber / 5) % 2 == 1)
                return LevelModes.Hard;
            else
                return LevelModes.ExtraHard;
        }
        return LevelModes.Normal;
    }

    /// <summary>
    /// 获取当前限时活动已完成词语数
    /// </summary>
    private int GetCompletedWordCount()
    {
        if (LimitTimeManager.Instance == null)
            return 0;
        return LimitTimeManager.Instance.GetCurWordCount();
    }

    /// <summary>
    /// 带动画更新进度条
    /// </summary>
    IEnumerator UpdateProgress()
    {
        UpdateSliderProgress();
        
        yield return new WaitForSeconds(1f);
        
        if (LimitTimeManager.Instance == null) yield break;

        progressSlider.transform.parent.gameObject.SetActive(true);
        int wordCount = GetCompletedWordCount();
        LimitDataItem limitData = LimitTimeManager.Instance.CurlimitData;
        if (limitData == null) yield break;
        
        float targetProgress = Mathf.Clamp01((float)wordCount / limitData.num);
        progressText.text = $"{wordCount}/{limitData.num}";

        // 如果目标进度与当前值差异极小，直接赋值，避免无意义动画
        if (Mathf.Abs(progressSlider.value - targetProgress) < 0.01f)
        {
            progressSlider.value = targetProgress;
            yield return null;;
        }

        // 平滑动画
        progressSlider.DOValue(targetProgress, 0.8f).SetEase(Ease.OutQuad);
    }

    /// <summary>
    /// 无动画同步更新进度（用于事件回调）
    /// </summary>
    private void UpdateSliderProgress()
    {
        if (LimitTimeManager.Instance == null) return;

        int wordCount = GetCompletedWordCount();
        LimitDataItem limitData = LimitTimeManager.Instance.CurlimitData;
        if (limitData == null) return;

        progressSlider.value = Mathf.Clamp01((float)wordCount / limitData.num);
        progressText.text = $"{wordCount}/{limitData.num}";
    }

    private void UpdateTimeDisplay(string time)
    {
        if (!string.IsNullOrEmpty(time))
        {
            _tasktable.taskTime.text = time;
        }
    }

    private void UpdateButterflyTime(string time = "")
    {
        bool shouldActivate = GameDataManager.Instance.UserData.butterflyTaskIsOpen;
        if (_butterflyTimerDisplay.activeSelf != shouldActivate)
        {
            _butterflyTimerDisplay.gameObject.SetActive(shouldActivate);
        }

        if (shouldActivate)
        {
            _butterflyTimerDisplay.GetComponentInChildren<Text>().text = time;
        }
    }

    /// <summary>
    /// 播放奖励获取序列动画
    /// </summary>
    private IEnumerator PlayRewardSequence()
    {
        isGetGold = false;
        _tasktable.taskEffect.gameObject.SetActive(false);
        _matchFishtable.matchEffect.gameObject.SetActive(false);

        // ==========================================
        // 🌟 新增：先播放莲花飞向禅意榜的动画！
        // ==========================================
        // if (_zenRankBtn.gameObject.activeSelf)
        // {
        //     yield return StartCoroutine(PlayZenLotusFlyAnim());
        // }
        
        if (_limitBtnTable._limitTimeEventButton.gameObject.activeSelf)
        {
            _limitBtnTable.CheckAndShowLimitedTimeEvent(Enlimiticon.transform);
            yield return new WaitForSeconds(0.5f);
            
            // 根据进度决定是否自动弹出限时活动面板
            if (LimitTimeManager.Instance != null && GetCompletedWordCount() >= LimitTimeManager.Instance.CurlimitData.num)
            {
                Showlimiticon.SetActive(true);
                SystemManager.Instance.ShowPanel(PanelType.LimitTimeScreen);
            }
            else
            {
                Showlimiticon.SetActive(false);
            }
        }
        if (!_nextStageButton.gameObject.activeSelf)
        {
            _nextStageButton.gameObject.SetActive(true);
            CanvasGroup cg = _nextStageButton.gameObject.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0;
                cg.DOFade(1, 0.3f);
            }
        }

        if (!GameDataManager.Instance.UserData.isAllCompleteTask && _tasktable.TaskBtn.gameObject.activeSelf)
        {
            _tasktable.CheckTasksScreen();
            yield return new WaitForSeconds(1.5f);
        }

        if (FishInfoController.Instance != null && FishInfoController.Instance.IsShowFishProgressAnim() && _matchFishtable.FishBtn.gameObject.activeSelf)
        {
            _matchFishtable.ShowFishWordAnim();
            StartCoroutine(UpdateFishRankUI());
            //yield return new WaitForSeconds(1.2f);
        }

        // 等待限时活动进度更新
        //yield return new WaitForSeconds(1.5f);
    }
    /// <summary>
    /// 播放莲花飞向禅意排行榜的动画
    /// </summary>
    private IEnumerator PlayZenLotusFlyAnim()
    {
        // 1. 获取本局获得的禅意数量（替换为你实际增加的变量，比如这里测试用 +15）
        int addZenCount = StageHexController.Instance.PuzzleZenCount; // 比如：GameDataManager.Instance.UserData.AddZenCount

        // 如果增加了禅意，且排行榜按钮开启了，才播动画
        if (addZenCount > 0 && _zenRankBtn != null && _zenRankBtn.gameObject.activeSelf)
        {
            // 初始化莲花状态（放在屏幕中央或者特定初始位置）
            GameObject prefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "lotus_icon");
            GameObject lotusInstance = Instantiate(prefab, transform,false);
            
            Canvas canvas = lotusInstance.GetComponent<Canvas>();
            if (canvas == null) canvas = lotusInstance.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingLayerName = "PopPanel"; // 保持和你前面配置的一样
            canvas.sortingOrder = 10;
            GraphicRaycaster raycaster = lotusInstance.GetComponent<GraphicRaycaster>();
            if (raycaster == null) raycaster = lotusInstance.AddComponent<GraphicRaycaster>();
            raycaster.enabled = false;
            CanvasGroup cg = lotusInstance.GetComponent<CanvasGroup>();
            if (cg == null) cg = lotusInstance.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.alpha = 1f; // 如果你想稍微有点透明度，可以改成 0.9f
            lotusInstance.SetActive(true);
            Text textComponent = lotusInstance.GetComponentInChildren<Text>();
            if (textComponent != null) textComponent.text = "+" + addZenCount;
            
            lotusInstance.transform.localPosition = Vector3.zero; 
            lotusInstance.transform.localScale = Vector3.one;
            // 2. 使用 DOTween 创建连续动画序列
            Sequence seq = DOTween.Sequence();
            seq.SetLink(lotusInstance);
            // 动作1：向上浮现一段距离 (耗时 0.5秒，缓动输出)
            seq.Append(lotusInstance.transform.DOLocalMoveY(100f, 0.5f).SetRelative(true).SetEase(Ease.OutQuad));
            
            // 动作2：停顿展示一下数字，让玩家看清楚加了多少 (耗时 0.3秒)
            seq.AppendInterval(0.3f);
            
            // 动作3：朝排行榜按钮飞过去！
            // 使用 DOMove 飞向目标的世界坐标，同时缩小莲花
            seq.Append(lotusInstance.transform.DOMove(_zenRankBtn.transform.position, 0.6f).SetEase(Ease.InBack));
            seq.Join(lotusInstance.transform.DOScale(0.3f, 0.6f)); // 边飞边缩小到 30%
            
            // 动作4：飞到目标后的回调
            seq.OnComplete(() =>
            {
                // 1. 飞到了，把飞行道具隐藏
                Destroy(lotusInstance);
                
                // 2. 通知目标按钮：“砸到你了，请播放你的震动特效和刷新逻辑！”
                _zenRankBtn.PlayAbsorbEffect(addZenCount);
                
                // 🌟 (可选) 在这里调用刷新排行榜按钮文字/总数的逻辑
               
            });

            // 阻塞等待动画大部分播完，再让结算界面去播下一个（比如灯笼）的动画
            yield return new WaitForSeconds(1.5f);
        }
    }
    
    private IEnumerator UpdateFishRankUI()
    {
        if (FishInfoController.Instance != null)
        {
            FishInfoController.Instance.RoundResultFishRank();
            _matchFishtable.UpdateFishRank();

            while (_matchFishtable.FishBtn.gameObject.activeSelf)
            {
                yield return new WaitForSeconds(1);
                FishInfoController.Instance.RoundResultFishRank();
                _matchFishtable.UpdateFishRank();
            }
        }
        yield return null;
    }
    private IEnumerator CheckZenRankBtn()
    {
        WaitForSeconds waitTime = new WaitForSeconds(0.5f);
        while (_zenRankBtn.gameObject.activeSelf)
        {
            yield return waitTime;
            _zenRankBtn.GetComponent<ZenRankButton>().CheckRankProgress();
        }
    }

    private void UnlockBtnsUI()
    {
        UnlockButton(_tasktable.TaskBtn, AppGameSettings.UnlockRequirements.DailyMissions, PanelType.DailyTasksScreen,
            GameDataManager.Instance.FishUserSave.opentime);

        UnlockButton(_limitBtnTable._limitTimeEventButton, AppGameSettings.UnlockRequirements.TimeLimitMode, PanelType.LimitTimeScreen,
            GameDataManager.Instance.UserData.limitOpenTime);
    }

    private void UnlockButton(Button button, int unlockLevel, string panelName, string opentime)
    {
        int currentStage = Mathf.Max(GameDataManager.Instance.UserData.CurrentHexStage, GameDataManager.Instance.UserData.CurrentChessStage);
        bool isUnlocked = currentStage >= unlockLevel || !string.IsNullOrEmpty(opentime);
        button.gameObject.SetActive(isUnlocked);
    }

    private void OnNextStageButtonClicked()
    {
        _hasPlayedRewardSequence = false;
        SetUIInteractable(false);
        SystemManager.Instance.HidePanel(PanelType.HeaderSection, true, LoadNextStage);
        Close();
    }

    private void Close()
    {
        base.Close();
    }

    private void OnLimitTimeEventButtonClicked()
    {
        SystemManager.Instance.ShowPanel(PanelType.LimitTimeScreen);
    }

    private void LoadNextStage()
    {
        StageHexController.Instance.SetStageData(StageHexController.Instance.CurrentStage);
        SystemManager.Instance.ShowPanel(PanelType.HexGamePlayArea);
    }

    private void SetUIInteractable(bool isInteractable)
    {
        GetComponent<CanvasGroup>().interactable = isInteractable;
    }

    private Sprite LoadheadIcon(string showIcon)
    {
        return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(showIcon);
    }

    protected override void OnDisable()
    {
        if (LimitTimeManager.Instance != null)
        {
            LimitTimeManager.Instance.OnDailyTimeUpdated -= UpdateTimeDisplay;
            LimitTimeManager.Instance.OnLimitTimeBtnUI -= UpdateSliderProgress;
        }
        if (DailyTaskManager.Instance != null)
            DailyTaskManager.Instance.OnDailyButterflyTaskUI -= UpdateButterflyTime;
        if (FishInfoController.Instance != null)
            FishInfoController.Instance.OnFishTimeUpdated -= _matchFishtable.UpdateFishTime;

        //GameDataManager.Instance?.UserData.ClearPuzzleVocabulary();
        StageHexController.Instance.LimitPuzzlecount = 0;
        EventDispatcher.instance.TriggerChangeGoldUI(AppGameSettings.LevelCompleteBonus, false);
        if (_treasureBoxEffect != null)
            _treasureBoxEffect.gameObject.SetActive(false);
        base.OnDisable();
    }
}