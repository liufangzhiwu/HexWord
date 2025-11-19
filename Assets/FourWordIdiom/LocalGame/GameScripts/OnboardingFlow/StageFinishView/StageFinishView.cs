using System.Collections;
using DG.Tweening;
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
    //[SerializeField] private Button HeadBtn;
    [SerializeField] private LimitBtnTable _limitBtnTable;
    [SerializeField] private MatchFishTable _matchFishtable;
    [SerializeField] private TaskTable _tasktable;
    
    [SerializeField] private Button _nextStageButton;
    [SerializeField] private GameObject _goldRewardIcon;
    [SerializeField] private Slider _progressSlider;
    [SerializeField] private Text _StageNumberText;
    [SerializeField] private Toggle _puzzletoggle;
    [SerializeField] private Text _progressText;    
    [SerializeField] private GameObject _butterflyTimerDisplay;

    private GameObject _treasureBoxEffect;
    private int _currentProgressSegment = 0;
    private float sliderProgress;
  

    protected override void InitializeUIComponents()
    {
        _nextStageButton.AddClickAction(OnNextStageButtonClicked);
        _limitBtnTable._limitTimeEventButton.AddClickAction(OnLimitTimeEventButtonClicked);
        SignBtn.AddClickAction(ShowSignInPanel);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        InitializeUI();
        UnlockBtnsUI();
        
        GameDataManager.instance.UserData.curIsEnter = false;
        LimitTimeManager.instance.OnDailyTimeUpdated += UpdateTimeDisplay; // 订阅事件
        DailyTaskManager.Instance.OnDailyButterflyTaskUI += UpdateButterflyTime;
        //FishInfoController.Instance.OnFishTimeUpdated += _matchFishtable.UpdateFishTime;
        //EventDispatcher.OnChangeHeadIconUpdateUI += UpdateHeadBtnUI;
        //_matchFishtable.CheckFishBtn();
        AudioManager.Instance.PlaySoundEffect("StageFinish");   
        StartCoroutine(PlayRewardSequence());
        
        //AdsManager.Instance.HideBannerAd();
    }

    /// <summary>
    /// 初始化UI元素状态和数值
    /// </summary>
    private void InitializeUI()
    {
        //_rewardCountText.transform.DOLocalMoveY(-10, 0.1f);
        _currentProgressSegment = 0;
        
        _StageNumberText.text = MultilingualManager.Instance.GetString("Level")+" " + GameDataManager.instance.UserData.CurrentStage; 
        _progressSlider.value = 0;
        
        CalculateProgressSegments();
        int totalStagesInSegment = CalculateTotalStagesInSegment();
        DetermineCurrentProgressSegment(totalStagesInSegment);
        
        int currentStageInSegment = CalculateCurrentStageInSegment(totalStagesInSegment);
        sliderProgress = currentStageInSegment / (float)AppGameSettings.ProgressMilestones[_currentProgressSegment];        
        
        _progressText.text = $"0/{AppGameSettings.ProgressMilestones[_currentProgressSegment]}";
        
        _progressSlider.DOValue(sliderProgress, 0.6f)
            .OnComplete(() => UpdateProgressText(currentStageInSegment, sliderProgress));
        
        SetUIInteractable(true);
        _goldRewardIcon.GetComponent<Image>().enabled = true;
       
    }
    
    private void UpdateTimeDisplay(string time)
    {
        if (!string.IsNullOrEmpty(time))
        {
            _tasktable.taskTime.text = time; // 更新文本
        }
    }
    
    private void UpdateButterflyTime(string time="")
    {
        // bool shouldActivate = GameDataManager.instance.UserData.butterflyTaskIsOpen;
        // if (_butterflyTimerDisplay.activeSelf != shouldActivate)
        // {
        //     _butterflyTimerDisplay.gameObject.SetActive(shouldActivate);
        // }
        //
        // if (shouldActivate)
        // {
        //     _butterflyTimerDisplay.GetComponentInChildren<Text>().text=time;
        // }
    }
    
    // private void UpdateHeadBtnUI()
    // {
    //     Image headImage = HeadBtn.transform.GetChild(0).GetComponent<Image>();
    //     if (GameDataManager.instance.UserData.UserHeadId > 0)
    //     {
    //         headImage.sprite = LoadheadIcon("head" + GameDataManager.instance.UserData.UserHeadId);
    //         headImage.transform.gameObject.SetActive(true);
    //     }
    //     else
    //     {
    //         headImage.transform.gameObject.SetActive(false);
    //     }
    // }

    /// <summary>
    /// 播放奖励获取序列动画
    /// </summary>
    private IEnumerator PlayRewardSequence()
    {
        _tasktable.taskEffect.gameObject.SetActive(false);
        _matchFishtable.matchEffect.gameObject.SetActive(false);
        
        if (sliderProgress >=1)
        {
            yield return new WaitForSeconds(0.6f);
            GameDataManager.instance.UserData.UpdateGold(AppGameSettings.LevelCompleteBonus, false, false
                ,"结算获得");
        }
        
         if (!LimitTimeManager.instance.IsComplete()&&_limitBtnTable._limitTimeEventButton.gameObject.activeSelf)
         {
             _limitBtnTable.CheckAndShowLimitedTimeEvent();
             yield return new WaitForSeconds(0.5f);
         }
        
        if (!GameDataManager.instance.UserData.isAllCompleteTask&&_tasktable.TaskBtn.gameObject.activeSelf)
        {
            _tasktable.CheckTasksScreen();
            yield return new WaitForSeconds(1.5f);
        }
        //if (FishInfoController.Instance.IsShowFishProgressAnim()&&_matchFishtable.FishBtn.gameObject.activeSelf)
        //{
        //    _matchFishtable.ShowFishWordAnim();
        //    StartCoroutine(UpdateFishRankUI());
        //    yield return new WaitForSeconds(1.2f);
        //}
        
        if (sliderProgress >=1)
        {
            // 显示关卡金币
            StartCoroutine(ShowGoldReward());
            //yield return new WaitForSeconds(1.2f);
        }

        //Animator.Play("ShowLevelBtn");
    }
    
    private IEnumerator UpdateFishRankUI()
    {
        // 提取重复使用的SaveData引用
        //FishInfoController.Instance.RoundResultFishRank();
        _matchFishtable.UpdateFishRank();
        
        while (_matchFishtable.FishBtn.gameObject.activeSelf)
        {
            yield return new WaitForSeconds(1);
            //FishInfoController.Instance.RoundResultFishRank();
            _matchFishtable.UpdateFishRank();
        }
    }

    /// <summary>
    /// 计算当前所在的进度段
    /// </summary>
    private void CalculateProgressSegments()
    {
        int accumulatedProgress = 0;
        for (int i = 0; i < AppGameSettings.ProgressMilestones.Count; i++)
        {
            accumulatedProgress += AppGameSettings.ProgressMilestones[i];
            if (StageController.Instance.CurStageInfo.StageNumber <= accumulatedProgress)
            {
                _currentProgressSegment = i;
                break;
            }
        }
    }

    /// <summary>
    /// 计算当前段内的总关卡数
    /// </summary>
    private int CalculateTotalStagesInSegment()
    {
        int total = 0;
        foreach (int segment in AppGameSettings.ProgressMilestones)
        {
            total += segment;
        }
        return total;
    }

    /// <summary>
    /// 确定当前进度段
    /// </summary>
    private void DetermineCurrentProgressSegment(int totalStages)
    {
        if (StageController.Instance.CurStageInfo.StageNumber > totalStages)
        {
            _currentProgressSegment = AppGameSettings.ProgressMilestones.Count - 1;
        }
    }

    /// <summary>
    /// 计算当前段内的关卡序号
    /// </summary>
    private int CalculateCurrentStageInSegment(int totalStages)
    {
        int StageInSegment = StageController.Instance.CurStageInfo.StageNumber;
        if (totalStages >= StageInSegment)
        {
            StageInSegment = StageController.Instance.CurStageInfo.StageNumber % totalStages;
        }
        StageInSegment %= AppGameSettings.ProgressMilestones[_currentProgressSegment];
        return StageInSegment == 0 ? AppGameSettings.ProgressMilestones[_currentProgressSegment] : StageInSegment;
    }

    /// <summary>
    /// 更新进度文本显示
    /// </summary>
    private void UpdateProgressText(int currentStage, float progress)
    {
        _progressText.text = progress >= 1 
            ? $"{AppGameSettings.ProgressMilestones[_currentProgressSegment]}/{AppGameSettings.ProgressMilestones[_currentProgressSegment]}" 
            : $"{currentStage}/{AppGameSettings.ProgressMilestones[_currentProgressSegment]}";
    }

    /// <summary>
    /// 显示金币奖励
    /// </summary>
    private IEnumerator ShowGoldReward()
    {           
        yield return new WaitForSeconds(0.6f);
        PlayTreasureBoxAnimation(true);
        _windowAnimator.Play("StageGold");
        yield return new WaitForSeconds(1f);
        PlayGoldFlyAnimation();
    }

    /// <summary>
    /// 播放宝箱开启动画
    /// </summary>
    private void PlayTreasureBoxAnimation(bool isPlay)
    {
        if (_treasureBoxEffect == null)
        {
            _treasureBoxEffect = Instantiate(ConfigManager.Instance.SpineObject, _goldRewardIcon.transform);
        }

        _treasureBoxEffect.gameObject.SetActive(true);
        
        if (isPlay)
        {
            _treasureBoxEffect.GetComponent<SkeletonAnimation>().AnimationState.SetAnimation(0, "idle01", false);
            _treasureBoxEffect.GetComponent<SkeletonAnimation>().DOPlay();
            _goldRewardIcon.GetComponent<Image>().enabled = false;
            AudioManager.Instance.PlaySoundEffect("OpenStageBox");
        }
       
    }

    /// <summary>
    /// 播放金币飞入动画
    /// </summary>
    public void PlayGoldFlyAnimation()
    {            
        CustomFlyInManager.Instance.FlyInGold(_goldRewardIcon.transform, () =>
        {
            EventDispatcher.instance.TriggerChangeGoldUI(AppGameSettings.LevelCompleteBonus, true);
        });
    }
   
    private void UnlockBtnsUI()
    {
        UnlockButton(_tasktable.TaskBtn,AppGameSettings.UnlockRequirements.DailyMissions,PanelType.DailyTasksScreen,
            "");

        UnlockButton(SignBtn, AppGameSettings.UnlockRequirements.SignInRewards, PanelType.SignWaterScreen,
            GameDataManager.instance.UserData.signOpenTime);
        
        UnlockButton(_limitBtnTable._limitTimeEventButton, AppGameSettings.UnlockRequirements.TimeLimitMode, PanelType.LimitTimeScreen,
            GameDataManager.instance.UserData.limitOpenTime);

        //UnlockButton(ranktable.RankBtn,StaticGameData.RankOpenLevel,PanelType.RankScreen, false);
    }

    private void UnlockButton(Button button, int unlockLevel, string panelName, string opentime)
    {
        int currentStage = GameDataManager.instance.UserData.CurrentStage;
        bool isUnlocked = currentStage >= unlockLevel||!string.IsNullOrEmpty(opentime);
    
        button.gameObject.SetActive(isUnlocked);
    
        if (!isUnlocked) return;
        if (currentStage != unlockLevel) return;

        AudioManager.Instance.PlaySoundEffect("BtnUnlock");
    
        // if (playAnimation)
        // {
        //     button.GetComponent<Animator>().enabled = true;
        //     _progressSlider.transform.DOScaleZ(1, 1f).OnComplete(() =>
        //     {
        //         SystemManager.Instance.ShowPanel(panelName);
        //         button.GetComponent<Animator>().enabled = false;
        //     });
        // }
        // // 无动画版本（RankButton 专用）
        // else
        // {
            SystemManager.Instance.ShowPanel(panelName);
        //}
    }

    /// <summary>
    /// 下一关按钮点击处理
    /// </summary>
    private void OnNextStageButtonClicked()
    {
        SetUIInteractable(false); 
        SystemManager.Instance.HidePanel(PanelType.HeaderSection, true, LoadNextStage);
        Close();
    }

    /// <summary>
    /// 限时活动按钮点击处理
    /// </summary>
    private void OnLimitTimeEventButtonClicked()
    {
        // 限时活动按钮逻辑
        SystemManager.Instance.ShowPanel(PanelType.LimitTimeScreen);  
    }
    
    private void ShowSignInPanel()
    {
        SystemManager.Instance.ShowPanel(PanelType.SignWaterScreen);    
    }

    /// <summary>
    /// 加载下一关卡
    /// </summary>
    private void LoadNextStage()
    {
        StageController.Instance.SetStageData(StageController.Instance.CurrentStage);
        SystemManager.Instance.ShowPanel(PanelType.GamePlayArea);
    }

    /// <summary>
    /// 设置UI交互状态
    /// </summary>
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
        LimitTimeManager.instance.OnDailyTimeUpdated -= UpdateTimeDisplay; // 订阅事件
        DailyTaskManager.Instance.OnDailyButterflyTaskUI -= UpdateButterflyTime;
        //FishInfoController.Instance.OnFishTimeUpdated -= _matchFishtable.UpdateFishTime;
        //EventDispatcher.OnChangeHeadIconUpdateUI -= UpdateHeadBtnUI;
        
        GameDataManager.instance.UserData.ClearPuzzleVocabulary();
        base.OnDisable();
        EventDispatcher.instance.TriggerChangeGoldUI(AppGameSettings.LevelCompleteBonus, false);
        if (_treasureBoxEffect != null)
            _treasureBoxEffect.gameObject.SetActive(false);
    }
}