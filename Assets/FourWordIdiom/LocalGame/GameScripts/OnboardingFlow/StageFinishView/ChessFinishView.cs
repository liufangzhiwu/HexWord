using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
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
    
    [SerializeField] private GameObject hardStageTable;          // 困难模式
    [SerializeField] private GameObject extrahardStageTable;          // 特别困难模式
    
    [Header("禅意飞行特效")]
    [SerializeField] private ZenRankButton _zenRankBtn;    // 🌟 这里把类型改成具体的脚本类型
    
    [Space]
    [Header("结算功能")]
    [SerializeField] private Button Content;
    [SerializeField] private Button nextBtn;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private Text levelText;
    [SerializeField] private Text progressText;
    [SerializeField] private GameObject _butterflyTimerDisplay;
    [SerializeField] private GameObject Showlimiticon;
    [SerializeField] private GameObject Enlimiticon;
    
    private GameObject _treasureBoxEffect;
    private int _currentProgressSegment = 0;
    private float sliderProgress;

    private readonly List<ChessBoxItem> _rewardBoxes = new List<ChessBoxItem>();
    private ChessBoxItem _currentReward;   // 当前的宝箱
    private bool _isReward = false;
    protected override void Awake()
    {
        base.Awake();
    }

    protected override void InitializeUIComponents()
    {
        nextBtn.AddClickAction(OnNextButtonClick);
        _limitBtnTable._limitTimeEventButton.AddClickAction(OnLimitTimeEventButtonClicked);
        _signBtn.AddClickAction(ShowSignInPanel);
        Content.onClick.AddListener(() =>
        {
            SystemManager.Instance.ShowPanel(PanelType.LimitTimeScreen);
            
        });
    }

    protected override void OnEnable()
    {
        base.OnEnable();
              
        _zenRankBtn.GetComponent<ZenRankButton>().CheckRankProgress();
        GameCoreManager.Instance.PanelState = PanelState.FinishPingPanel;
        
        InitializeUI();
        UnlockBtnsUI();
        
        GameDataManager.Instance.UserData.curIsEnter = false;
        LimitTimeManager.Instance.OnDailyTimeUpdated += UpdateTimeDisplay;
        LimitTimeManager.Instance.OnLimitTimeBtnUI += UpdateSliderProgress;
        DailyTaskManager.Instance.OnDailyButterflyTaskUI += UpdateButterflyTime;
        FishInfoController.Instance.OnFishTimeUpdated += _matchFishtable.UpdateFishTime;
        _matchFishtable.CheckFishBtn();
        AudioManager.Instance.PlaySoundEffect("StageFinish");   
        
        StartCoroutine(CheckCompletedState());
        // DailyTaskManager.Instance.UpateButterflyTaskUI();
    }

    private IEnumerator CheckCompletedState()
    {
        yield return UpdateFishRankUI();
#if Unity_ShowLog || UNITY_EDITOR
        if (GameCoreManager.Instance.IsTrueAuto)
        {
            yield return new WaitForSeconds(1.2f);
            OnNextButtonClick();
        }
#endif
    }
    
    private void InitializeUI()
    {
        _currentProgressSegment = 0;
        
        // 设置关卡文本
        int Stage = GameDataManager.Instance.UserData.CurrentChessStage;
      
        if (LimitTimeManager.Instance.IsComplete())
            progressSlider.transform.parent.gameObject.SetActive(false);
        else 
            progressSlider.transform.parent.gameObject.SetActive(true);

      
        
        ChessStageController.Instance.CurLevelMode = GetLevelDifficulty(Stage);
        
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
        levelText.text = MultilingualManager.Instance.GetString("Level")+" " +Stage;
        
        
        StartCoroutine(UpdateProgress());
        StartCoroutine(PlayRewardSequence());
    }
    
    LevelModes GetLevelDifficulty(int levelNumber) {
        if (levelNumber % 5 == 0) {
            if ((levelNumber / 5) % 2 == 1) {
                return LevelModes.Hard;
            } else {
                return LevelModes.ExtraHard;
            }
        }
        return LevelModes.Normal;
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
    /// 带动画更新进度条
    /// </summary>
    IEnumerator UpdateProgress()
    {
        UpdateSliderProgress(false);
        yield return new WaitForSeconds(1f);
        
        if (LimitTimeManager.Instance == null) yield return null;

        progressSlider.transform.parent.gameObject.SetActive(true);
        int wordCount = GetCompletedWordCount();
        LimitDataItem limitData = LimitTimeManager.Instance.CurlimitData;
        if (limitData == null) yield return null;;

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
    
    private void UpdateSliderProgress(bool param)
    {
        
        if (LimitTimeManager.Instance == null) return;
        // if (DOTween.IsTweening(progressSlider)) return;
        int wordCount = GetCompletedWordCount();
        LimitDataItem limitData = LimitTimeManager.Instance.CurlimitData;
        if (limitData == null) return;

        progressSlider.value = Mathf.Clamp01((float)wordCount / limitData.num);
        progressText.text = $"{wordCount}/{limitData.num}";
       
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
    /// 播放奖励获取序列动画
    /// </summary>
    private IEnumerator PlayRewardSequence()
    {
        _tasktable.taskEffect.gameObject.SetActive(false);
        _matchFishtable.matchEffect.gameObject.SetActive(false);

        if (!LimitTimeManager.Instance.IsComplete() && _limitBtnTable._limitTimeEventButton.gameObject.activeSelf)
        {
            _limitBtnTable.CheckAndShowLimitedTimeEvent(Enlimiticon.transform);
            yield return new WaitForSeconds(0.5f);
        }
        
        // ==========================================
        // 🌟 新增：先播放莲花飞向禅意榜的动画！
        // ==========================================
        if (_zenRankBtn.gameObject.activeSelf)
        {
            yield return StartCoroutine(PlayZenLotusFlyAnim());
        }

        if (!GameDataManager.Instance.UserData.isAllCompleteTask && _tasktable.TaskBtn.gameObject.activeSelf)
        {
            _tasktable.CheckTasksScreen();
            yield return new WaitForSeconds(1.5f);
        }

        if (FishInfoController.Instance.IsShowFishProgressAnim() && _matchFishtable.FishBtn.gameObject.activeSelf)
        {
            _matchFishtable.ShowFishWordAnim();
            StartCoroutine(UpdateFishRankUI());
            yield return new WaitForSeconds(1.2f);
        }
        
        // 等待限时活动进度更新
        yield return new WaitForSeconds(1.5f);
        
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
        FishInfoController.Instance.RoundResultFishRank();
        _matchFishtable.UpdateFishRank();

        while (_matchFishtable.FishBtn.gameObject.activeSelf)
        {
            yield return new WaitForSeconds(1f);
            FishInfoController.Instance.RoundResultFishRank();
            _matchFishtable.UpdateFishRank();
        }
    }

    private void OnNextButtonClick()
    {
        SystemManager.Instance.HidePanel(PanelType.HeaderSection, false, LoadNextStage);
        Close();
    }

    private void LoadNextStage()
    {
        ChessStageController.Instance.SetStageData(ChessStageController.Instance.CurrentStage);
        SystemManager.Instance.ShowPanel(PanelType.ChessPlayArea);
    }

   
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
        //SystemManager.Instance.ShowPanel(panelName);
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
    /// 播放莲花飞向禅意排行榜的动画
    /// </summary>
    private IEnumerator PlayZenLotusFlyAnim()
    {
        // 1. 获取本局获得的禅意数量（替换为你实际增加的变量，比如这里测试用 +15）
        int addZenCount = ChessStageController.Instance.PuzzleSumCount; // 比如：GameDataManager.Instance.UserData.AddZenCount

        // 如果增加了禅意，且排行榜按钮开启了，才播动画
        if (addZenCount > 0 && _zenRankBtn != null && _zenRankBtn.gameObject.activeSelf)
        {
            // 初始化莲花状态（放在屏幕中央或者特定初始位置）
            GameObject prefab = AssetBundleLoader.SharedInstance.LoadGameObject("commonitem", "lotus_icon");
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

    protected override void OnDisable()
    {
        if (_isReward)
        {
            foreach (KeyValuePair<int,int > kvp in _currentReward.Rewards)
            {
                GiveReward(kvp.Key, kvp.Value);
            }

            _isReward = false;
        }
        LimitTimeManager.Instance.OnDailyTimeUpdated -= UpdateTimeDisplay;
        DailyTaskManager.Instance.OnDailyButterflyTaskUI -= UpdateButterflyTime;
        LimitTimeManager.Instance.OnLimitTimeBtnUI -= UpdateSliderProgress;
        FishInfoController.Instance.OnFishTimeUpdated -= _matchFishtable.UpdateFishTime;
        
        GameDataManager.Instance?.UserData.ClearPuzzleVocabulary();
        base.OnDisable();
        EventDispatcher.instance.TriggerChangeGoldUI(AppGameSettings.LevelCompleteBonus, false);
        if(_treasureBoxEffect != null)
            _treasureBoxEffect.gameObject.SetActive(false);
    }
}
