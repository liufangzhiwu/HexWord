using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DG.Tweening;
using Middleware;
using Newtonsoft.Json;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

public class ChessBoxItem
{
    public int Id;
    public int Level;
    public List<KeyValuePair<int, int>> Rewards;
}
public class ChessFinishView : UIWindow
{
    [Header("活动按钮")] 
    // [SerializeField]
    [SerializeField] private Button _signBtn;
    [SerializeField] private LimitBtnTable _limitBtnTable;
    [SerializeField] private MatchFishTable _matchFishtable;
    [SerializeField] private TaskTable _tasktable;
    [Space]
    [Header("结算功能")]
    [SerializeField] private Button nextBtn;
    [SerializeField] private GameObject goldRewardIcon;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private Text progressText;
    [SerializeField] private GameObject _butterflyTimerDisplay;
    
    private GameObject _treasureBoxEffect;
    private int _currentProgressSegment = 0;
    private float _sliderProgress;

    private readonly List<ChessBoxItem> _rewardBoxes = new List<ChessBoxItem>();
    private ChessBoxItem _currentReward;   // 当前的宝箱
    protected override void Awake()
    {
        base.Awake();
        TextAsset data = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "ChessTreasureTable");
        if(data != null)
            ParseRewardBoxItems(data.text);
        else 
            Debug.LogWarning("ChessTreasureTable not found");
    }

    protected override void InitializeUIComponents()
    {
        nextBtn.AddClickAction(OnNextButtonClick);
        _limitBtnTable._limitTimeEventButton.AddClickAction(OnLimitTimeEventButtonClicked);
        _signBtn.AddClickAction(ShowSignInPanel);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        InitializeUI();
        UnlockBtnsUI();
        
        GameDataManager.Instance.UserData.curIsEnter = false;
        LimitTimeManager.Instance.OnDailyTimeUpdated += UpdateTimeDisplay;
        DailyTaskManager.Instance.OnDailyButterflyTaskUI += UpdateButterflyTime;
        //FishInfoController.Instance.OnFishTimeUpdated += _matchFishtable.UpdateFishTime;
        _matchFishtable.CheckFishBtn();
        AudioManager.Instance.PlaySoundEffect("StageFinish");   
        
        StartCoroutine(PlayRewardSequence());
        StartCoroutine(UpdateFishRankUI());
        //DailyTaskManager.Instance.UpateButterflyTaskUI();
    }

    private void InitializeUI()
    {
        _currentProgressSegment = 0;
        
        nextBtn.GetComponentInChildren<Text>(true).text = MultilingualManager.Instance.GetString("Level")+" "+ GameDataManager.Instance.UserData.CurrentChessStage;
        progressSlider.value = 0;
   
        CalculateProgressSegments();
        int totalStagesInSegment = CalculateTotalStagesInSegment();
        DetermineCurrentProgressSegment(totalStagesInSegment);
        
        int currentStageInSegment = CalculateCurrentStageInSegment(totalStagesInSegment);
        _sliderProgress = currentStageInSegment / (float)_currentReward.Level;
        progressText.text = $"0/{_currentReward.Level}";
        progressSlider.DOValue(_sliderProgress, 0.6f)
            .OnComplete(() => UpdateProgressText(currentStageInSegment, _sliderProgress));
        
        SetUIInteractable(true);
        goldRewardIcon.GetComponent<Image>().enabled = true;
    }

    private void UpdateTimeDisplay(string time)
    {
        if(!string.IsNullOrEmpty(time))
            _tasktable.taskTime.text = time;
    }

    private void UpdateButterflyTime(string time = "")
    {
        bool shouldActivate = GameDataManager.Instance.UserData.butterflyTaskIsOpen;
        if (_butterflyTimerDisplay.activeSelf != shouldActivate)
        {
            _butterflyTimerDisplay.gameObject.SetActive(shouldActivate);
        }

        if (shouldActivate)
            _butterflyTimerDisplay.GetComponentInChildren<Text>().text = time;
    }
    /// <summary>
    /// 播放奖励获取序列动画
    /// </summary>
    private IEnumerator PlayRewardSequence()
    {
        _tasktable.taskEffect.gameObject.SetActive(false);
        _matchFishtable.matchEffect.gameObject.SetActive(false);
        if (_sliderProgress >= 1)
        {
            StartCoroutine(ShowGoldReward());
            yield return new WaitForSeconds(0.6f);
        }

        if (!LimitTimeManager.Instance.IsComplete() && _limitBtnTable._limitTimeEventButton.gameObject.activeSelf)
        {
            _limitBtnTable.CheckAndShowLimitedTimeEvent();
            yield return new WaitForSeconds(0.5f);
        }

        if (!GameDataManager.Instance.UserData.isAllCompleteTask && _tasktable.TaskBtn.gameObject.activeSelf)
        {
            _tasktable.CheckTasksScreen();
            yield return new WaitForSeconds(1.5f);
        }

        // if (FishInfoController.Instance.IsShowFishProgressAnim() && _matchFishtable.FishBtn.gameObject.activeSelf)
        // {
        //     _matchFishtable.ShowFishWordAnim();
        //     StartCoroutine(UpdateFishRankUI());
        //     yield return new WaitForSeconds(1.2f);
        // }
        
    }

    private void GiveReward(int kvpKey, int kvpValue)
    {
        if(kvpKey == 1)
         GameDataManager.Instance.UserData.UpdateGold(kvpValue, false,false, "结算获得");
        else if (kvpKey == 2)
            GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, kvpValue, "结算获得");
        else if(kvpKey == 3)
            GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.AutoComplete, kvpValue, "结算获得");
    }

    private IEnumerator UpdateFishRankUI()
    {
        //FishInfoController.Instance.RoundResultFishRank();
        _matchFishtable.UpdateFishRank();

        while (_matchFishtable.FishBtn.gameObject.activeSelf)
        {
            yield return new WaitForSeconds(1f);
            //FishInfoController.Instance.RoundResultFishRank();
            _matchFishtable.UpdateFishRank();
        }
    }

    private void OnNextButtonClick()
    {
        SetUIInteractable(false);
        SystemManager.Instance.HidePanel(PanelType.HeaderSection, false, LoadNextStage);
        Close();
    }

    private void LoadNextStage()
    {
        ChessStageController.Instance.SetStageData(ChessStageController.Instance.CurrentStage);
        SystemManager.Instance.ShowPanel(PanelType.ChessPlayArea);
    }

    /// <summary>
    /// 计算当前所在的进度段
    /// </summary>
    private void CalculateProgressSegments()
    {
        int accumulatedProgress = 0;
        _currentReward = _rewardBoxes[^1];
        for (int i = 0; i < _rewardBoxes.Count; i++)
        {
            accumulatedProgress += _rewardBoxes[i].Level;
            if (ChessStageController.Instance.CurrStageInfo.StageNumber <= accumulatedProgress)
            {
                _currentProgressSegment = i;
                _currentReward = _rewardBoxes[i];
                break;
            }
        }
    }

    /// <summary>
    /// 计算当前段内的总关卡数
    /// </summary>
    /// <returns></returns>
    private int CalculateTotalStagesInSegment()
    {
        int total = 0;
        foreach (var segment in _rewardBoxes)
        {
            total += segment.Level;
        }
        return total;
    }

    /// <summary>
    /// 确定当前进度段
    /// </summary>
    /// <param name="totalStages"></param>
    private void DetermineCurrentProgressSegment(int totalStages)
    {
        if(ChessStageController.Instance.CurrStageInfo.StageNumber > totalStages)
            _currentProgressSegment = _rewardBoxes.Count - 1;
    }

    /// <summary>
    /// 计算当前段内的关卡序号
    /// </summary>
    private int CalculateCurrentStageInSegment(int totalStages)
    {
        int currentStage = ChessStageController.Instance.CurrStageInfo.StageNumber;

        int segmentStart = 1;
        for (int i = 0; i < _rewardBoxes.Count; i++)
        {
            int segmentEnd = segmentStart + _rewardBoxes[i].Level - 1;
            if (currentStage <= segmentEnd)
            {
                return currentStage - segmentStart + 1;
            }

            segmentStart = segmentEnd + 1;
        }

        int postMilestoneStage = currentStage - totalStages;
        int stageInSegment = (postMilestoneStage - 1) % 20 + 1;
        return stageInSegment;
    }

    /// <summary>
    /// 更新进度文本显示
    /// </summary>
    private void UpdateProgressText(int currentStage, float progress)
    {
        progressText.text = progress >= 1
            ? $"{_currentReward.Level} / {_currentReward.Level}"
            : $"{currentStage}/{_currentReward.Level}";
    }


  
    
    private IEnumerator ShowGoldReward()
    {
        yield return new WaitForSeconds(0.6f);
        PlayTreasureBoxAnimation(true);
        // _windowAnimator.Play("StageGold");
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
            _treasureBoxEffect = Instantiate(ConfigManager.Instance.SpineObject2, goldRewardIcon.transform);
        }

        _treasureBoxEffect.gameObject.SetActive(true);
        
        if (isPlay)
        {
            _treasureBoxEffect.GetComponent<SkeletonAnimation>().AnimationState.SetAnimation(0, "idle01", false);
            _treasureBoxEffect.GetComponent<SkeletonAnimation>().DOPlay();
            goldRewardIcon.GetComponent<Image>().enabled = false;
            AudioManager.Instance.PlaySoundEffect("OpenStageBox");
        }
    }

    private void PlayGoldFlyAnimation()
    {
        CustomFlyInManager.Instance.FlyInGold(goldRewardIcon.transform, () =>
        { 
            foreach (KeyValuePair<int,int > kvp in _currentReward.Rewards)
            {
                GiveReward(kvp.Key, kvp.Value);
            }
            EventDispatcher.instance.TriggerChangeGoldUI(_currentReward.Rewards[0].Value, true);
        });
    }

    #region 解析宝箱配置
    // 解析宝箱奖励
    private void ParseRewardBoxItems(string dataText)
    {
        _rewardBoxes.Clear();
        
        string[] lines = dataText.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        for (int i = 2; i < lines.Length; i++)
        {
            string[] fields = ToolUtil.SplitCSVLine(lines[i]);
         
            if (fields.Length >= 3)
            {
                int id = int.Parse(fields[0]);
                int level = int.Parse(fields[1]);
                ChessBoxItem chessBoxItem = new ChessBoxItem
                {
                    Id = id,
                    Level = level,
                    Rewards = new List<KeyValuePair<int, int>>(),
                };
                string[] rewardPairs  = fields[2].Split('#', System.StringSplitOptions.RemoveEmptyEntries);
                foreach (var pair in rewardPairs )
                {
                    string[] kv = pair.Split(',', System.StringSplitOptions.RemoveEmptyEntries);
                    int itemId = int.Parse(kv[0]);
                    int count = int.Parse(kv[1]);
                    // chessBoxItem.Rewards.Add(new Dictionary<int, int>{{itemId, count}});
                    chessBoxItem.Rewards.Add(new KeyValuePair<int, int>(itemId, count));
                }
                _rewardBoxes.Add(chessBoxItem);
            }
        }
    }

    #endregion
    private void UnlockBtnsUI()
    {
        UnlockButton(_tasktable.TaskBtn,AppGameSettings.UnlockRequirements.DailyMissions, PanelType.DailyTasksScreen,
            GameDataManager.Instance.FishUserSave.opentime);
        UnlockButton(_signBtn, AppGameSettings.UnlockRequirements.SignInRewards, PanelType.SignWaterScreen,
            GameDataManager.Instance.UserData.signOpenTime);
        UnlockButton(_limitBtnTable._limitTimeEventButton, AppGameSettings.UnlockRequirements.TimeLimitMode, PanelType.LimitTimeScreen,
            GameDataManager.Instance.UserData.limitOpenTime);
    }

    private void UnlockButton(Button button, int unlockLevel, string panelName, string openTime)
    {
        int currentStage = Mathf.Max(GameDataManager.Instance.UserData.CurrentChessStage,GameDataManager.Instance.UserData.CurrentHexStage);
        bool isUnlocked = currentStage >= unlockLevel || !string.IsNullOrEmpty(openTime);
        
        button.gameObject.SetActive(isUnlocked);

        if (!isUnlocked) return;
        if (currentStage != unlockLevel) return;
        
        AudioManager.Instance.PlaySoundEffect("BtnUnlock");
        SystemManager.Instance.ShowPanel(panelName);
    }

    /// <summary>
    /// 限时活动按钮点击处理
    /// </summary>
    private void OnLimitTimeEventButtonClicked()
    {
        SystemManager.Instance.ShowPanel(PanelType.LimitTimeScreen);
    }

    private void ShowSignInPanel()
    {
        SystemManager.Instance.ShowPanel(PanelType.SignWaterScreen);
    }
    
    /// <summary>
    /// 设置UI交互状态
    /// </summary>
    private void SetUIInteractable(bool isInteractable)
    {
        GetComponent<CanvasGroup>().interactable = isInteractable;
    }

    protected override void OnDisable()
    {
        LimitTimeManager.Instance.OnDailyTimeUpdated -= UpdateTimeDisplay;
        DailyTaskManager.Instance.OnDailyButterflyTaskUI -= UpdateButterflyTime;
        //FishInfoController.Instance.OnFishTimeUpdated -= _matchFishtable.UpdateFishTime;
        
        GameDataManager.Instance.UserData.ClearPuzzleVocabulary();
        base.OnDisable();
        EventDispatcher.instance.TriggerChangeGoldUI(AppGameSettings.LevelCompleteBonus, false);
        if(_treasureBoxEffect != null)
            _treasureBoxEffect.gameObject.SetActive(false);
    }
}
