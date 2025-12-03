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
    [SerializeField] private GameObject Showlimiticon;
    [SerializeField] private GameObject Enlimiticon;
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
        
        GameDataManager.Instance.UserData.curIsEnter = false;
        LimitTimeManager.Instance.OnDailyTimeUpdated += UpdateTimeDisplay; // 订阅事件
        DailyTaskManager.Instance.OnDailyButterflyTaskUI += UpdateButterflyTime;
        LimitTimeManager.Instance.OnLimitTimeBtnUI += UpdateProgress;       
        //FishInfoController.Instance.OnFishTimeUpdated += _matchFishtable.UpdateFishTime;
        //EventDispatcher.OnChangeHeadIconUpdateUI += UpdateHeadBtnUI;
        //_matchFishtable.CheckFishBtn();
        AudioManager.Instance.PlaySoundEffect("StageFinish");   
        StartCoroutine(PlayRewardSequence());
        
        //AdsManager.Instance.HideBannerAd();
        
        InitializeUI();
        UnlockBtnsUI();
        
        SetUIInteractable(true);
        
      
    }

    /// <summary>
    /// 初始化UI元素状态和数值
    /// </summary>
    private void InitializeUI()
    {
        _currentProgressSegment = 0;
        
        _StageNumberText.text = MultilingualManager.Instance.GetString("Level")+" " + GameDataManager.Instance.UserData.CurrentHexStage; 
        // CalculateProgressSegments();
        // int totalStagesInSegment = CalculateTotalStagesInSegment();
        // DetermineCurrentProgressSegment(totalStagesInSegment);
        //
        // int currentStageInSegment = CalculateCurrentStageInSegment(totalStagesInSegment);
        // sliderProgress = currentStageInSegment / (float)AppGameSettings.ProgressMilestones[_currentProgressSegment];        
        //
        // _progressText.text = $"0/{AppGameSettings.ProgressMilestones[_currentProgressSegment]}";
        //
        // _progressSlider.DOValue(sliderProgress, 0.6f)
        //     .OnComplete(() => UpdateProgressText(currentStageInSegment, sliderProgress));

        if (LimitTimeManager.Instance.IsComplete())
        {
            _progressSlider.transform.parent.gameObject.SetActive(false);
        }
        else
        {
            _progressSlider.transform.parent.gameObject.SetActive(true);
            StartCoroutine(WaitTimeUpdate());
        }
        
        
    }

    IEnumerator WaitTimeUpdate()
    {
        yield return new WaitForSeconds(1.5f);
        UpdateProgress();
    }
    
    private void UpdateProgress(bool isanim=true)
    {
        _progressSlider.transform.parent.gameObject.SetActive(true);
        int wordcount = LimitTimeManager.Instance.GetCurWordCount();
        LimitDataItem limitData = LimitTimeManager.Instance.CurlimitData;
        float durtime = !isanim?0f:1f;
        sliderProgress = (float)wordcount/limitData.num;    
        int oldProgress = wordcount-StageHexController.Instance.CurStageData.Puzzles.Count;
        _progressText.text = oldProgress + "/" + LimitTimeManager.Instance.CurlimitData.num;
    
        _progressSlider.DOValue(sliderProgress,durtime).OnComplete(() =>
        {
            _progressText.text = wordcount + "/" + limitData.num;
        });
    }
    
    IEnumerator ShowLimitTimeScreen()
    {
        yield return new WaitForSeconds(0.5f);
        
        if (sliderProgress >=1)
        {
            // 显示关卡金币
            // StartCoroutine(ShowGoldReward());
            Showlimiticon.SetActive(true);
            SystemManager.Instance.ShowPanel(PanelType.LimitTimeScreen);    
        }
        else
        {
            Showlimiticon.SetActive(false);
        }
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
        
         if (!LimitTimeManager.Instance.IsComplete()&&_limitBtnTable._limitTimeEventButton.gameObject.activeSelf)
         {
             _limitBtnTable.CheckAndShowLimitedTimeEvent(Enlimiticon.transform);
             yield return new WaitForSeconds(0.5f);
         }
        
        if (!GameDataManager.Instance.UserData.isAllCompleteTask&&_tasktable.TaskBtn.gameObject.activeSelf)
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

        //Animator.Play("ShowLevelBtn");
        
        StartCoroutine(ShowLimitTimeScreen());
        
        if (GameCoreManager.Instance.IsTrueAuto)
        {
            OnNextStageButtonClicked();
        }
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
   
    private void UnlockBtnsUI()
    {
        UnlockButton(_tasktable.TaskBtn,AppGameSettings.UnlockRequirements.DailyMissions,PanelType.DailyTasksScreen,
            "");

        UnlockButton(SignBtn, AppGameSettings.UnlockRequirements.SignInRewards, PanelType.SignWaterScreen,
            GameDataManager.Instance.UserData.signOpenTime);
        
        UnlockButton(_limitBtnTable._limitTimeEventButton, AppGameSettings.UnlockRequirements.TimeLimitMode, PanelType.LimitTimeScreen,
            GameDataManager.Instance.UserData.limitOpenTime);

        //UnlockButton(ranktable.RankBtn,StaticGameData.RankOpenLevel,PanelType.RankScreen, false);
    }

    private void UnlockButton(Button button, int unlockLevel, string panelName, string opentime)
    {
        int currentStage = GameDataManager.Instance.UserData.CurrentHexStage;
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
         //   SystemManager.Instance.ShowPanel(panelName);
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
        StageHexController.Instance.SetStageData(StageHexController.Instance.CurrentStage);
        SystemManager.Instance.ShowPanel(PanelType.HexGamePlayArea);
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
        return AssetBundleLoader.SharedInstance.GetSpriteFromAtlas(showIcon);
    }

    protected override void OnDisable()
    {
        LimitTimeManager.Instance.OnDailyTimeUpdated -= UpdateTimeDisplay; // 订阅事件
        DailyTaskManager.Instance.OnDailyButterflyTaskUI -= UpdateButterflyTime;
        LimitTimeManager.Instance.OnLimitTimeBtnUI -= UpdateProgress;       
        //FishInfoController.Instance.OnFishTimeUpdated -= _matchFishtable.UpdateFishTime;
        //EventDispatcher.OnChangeHeadIconUpdateUI -= UpdateHeadBtnUI;
        
        GameDataManager.Instance.UserData.ClearPuzzleVocabulary();
        base.OnDisable();
        EventDispatcher.instance.TriggerChangeGoldUI(AppGameSettings.LevelCompleteBonus, false);
        if (_treasureBoxEffect != null)
            _treasureBoxEffect.gameObject.SetActive(false);
    }
}