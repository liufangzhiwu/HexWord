using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    //[SerializeField] private Button _signBtn;
    [SerializeField] private LimitBtnTable _limitBtnTable;
    [SerializeField] private Text reachLevelText;
    [SerializeField] private MatchFishTable _matchFishtable;
    [SerializeField] private TaskTable _tasktable;
    [SerializeField] private Button butterflyBtn;
    [SerializeField] private GameObject hardStageTable;          // 困难模式
    [SerializeField] private GameObject extrahardStageTable;          // 特别困难模式

    [Header("禅意飞行特效")]
    [SerializeField] private Text zenScoreText;
    [SerializeField] private ZenRankButton _zenRankBtn;    // 🌟 这里把类型改成具体的脚本类型
    [SerializeField] private Image _centerLotusImage;     // 场景里中间那个荷花的 Image 组件
    // [SerializeField] private Sprite _darkLotusSprite;     // 准备好的【暗荷花】图片
    // [SerializeField] private Sprite _brightLotusSprite;   // 准备好的【亮荷花】图片
    
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
    [SerializeField] private GameObject ButterflyRedpoint;
   
    [Space]
    [Header("横幅鼓励提示 UI (新增)")]
    [Header("结算页激励文案UI节点 (需手动挂载)")]
    [SerializeField] private GameObject _encourageTitleRoot;   // 顶部标题的父节点 (比如"新纪录！")
    [SerializeField] private Text _encourageTitleText;
    [SerializeField] private GameObject _encouragePhraseRoot;  // 激励长文案的父节点
    [SerializeField] private Text _encouragePhraseText;
    [SerializeField] private Image _encourageEmojiIcon;
    
    private GameObject _treasureBoxEffect;
    private GameObject lotusInstance;
    private GameObject _pupaPrefab;
    private ObjectPool _pupaPool;
    private int _currentProgressSegment = 0;

    private float sliderProgress;

    // 🌟 新增：防御数据双重累加的标记
    private bool _isWordsExtracted = false;
// 标记是否已经播放过本局的奖励和动画
    private bool _hasPlayedRewardSequence = false;
    private bool isTriggerFirstWin;
    private bool isShowWinSign;
    
    protected override void InitializeUIComponents()
    {
        nextBtn.AddVibraClickAction(OnNextButtonClick);
        _limitBtnTable._limitTimeEventButton.AddClickAction(OnLimitTimeEventButtonClicked);
       
        Content.onClick.AddListener(() => { SystemManager.Instance.ShowPanel(PanelType.LimitTimeScreen); });
        butterflyBtn.AddClickAction(OnButterflyClick);
        
        // 初始化莲花状态（放在屏幕中央或者特定初始位置）
        GameObject prefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "lotus_icon");
        lotusInstance = Instantiate(prefab, _centerLotusImage.transform, false);
        _pupaPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "Pupa");
        _pupaPool = new ObjectPool(_pupaPrefab, transform, 2,PoolBehaviour.GameObject);
        CustomFlyInManager.Instance.finishlevelBtnObj=nextBtn.gameObject;
    }

    private IEnumerator FetchLeaderboardDataMod(string leaderboardName)
    {
       yield return StartCoroutine(ZenRankManager.Instance.FetchLeaderboardDataRoutine(leaderboardName));
       _zenRankBtn.SyncTextFromCache();
    }
    protected override void OnEnable()
    {
        base.OnEnable();
        SystemManager.Instance.ShowPanel(PanelType.HeaderSection);
        GameCoreManager.Instance.PanelState = PanelState.FinishPingPanel;
        // 强制先刷新按钮状态
        if (_zenRankBtn != null)
            _zenRankBtn.GetComponent<ZenRankButton>().CheckRankProgress();

        if (_zenRankBtn != null && _zenRankBtn.gameObject.activeSelf)
        {
            string boardId = GameDataManager.Instance.UserData.Zenlevel;
            if (!string.IsNullOrEmpty(boardId))
            {
                StartCoroutine(FetchLeaderboardDataMod(boardId));
            }
        }
        //  利用现有的 curIsEnter 判定是否是刚打完一局全新进入的
        if (GameDataManager.Instance.UserData.curIsEnter)
        {
            _hasPlayedRewardSequence = false;
        }
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
        DailyTaskManager.Instance.UpateButterflyTaskUI();
        butterflyBtn.gameObject.SetActive(ButterfliesManager.Instance.IsOpen);
        int oldPupaCount = GameDataManager.Instance.ButterflyData.currPupa -
                           ChessStageController.Instance.EarnedPupaThisLevel;
        UpdateButterflyProgressUI(Mathf.Max(0, oldPupaCount), true);

        if (GameDataManager.Instance.UserData.ischangetheme)
        {
            int times = 1;
            if (GameDataManager.Instance.UserData.ThemeItemUses.Keys.Contains(GameDataManager.Instance.UserData
                    .userthemeid))
            {
                times = GameDataManager.Instance.UserData.ThemeItemUses[GameDataManager.Instance.UserData.userthemeid];
            }

            AnalyticMgr.ThemeUse(GameDataManager.Instance.UserData.userthemeid, times);
        }

        // nextBtn.gameObject.SetActive(false);
        StartCoroutine(CheckZenRankBtn());
        
        isShowWinSign = StreakManager.Instance.IsCanShowWinSign();
        // bool isJump = ChessStageController.Instance.IsJump;

        //if (isShowWinSign&&!isJump)
        // if (isShowWinSign)
        // {
        //     SystemManager.Instance.ShowPanel(PanelType.SignWinScreen);
        // }
        // else
        // {
        //     CheckReturnFirstWinScreen();
        // }
        
        // StartCoroutine(PlayRewardSequence(isShowWinSign));
    }
    
    private void CheckReturnFirstWinScreen()
    {
        int curStreak = StreakManager.Instance.GetCurrentStreak();
        int offlineSeconds= PlayerPrefs.GetInt("offline_Seconds", 0);
        int goldcount= GameDataManager.Instance.UserData.Gold;
        int todaywinTime = GameDataManager.Instance.UserData.chessdayPassStageCount;
        int toolcount = GameDataManager.Instance.UserData.toolInfo[102].count +
                        GameDataManager.Instance.UserData.toolInfo[104].count;

        //1296000 15天
        isTriggerFirstWin = offlineSeconds >=1296000 && todaywinTime <= 1 &&goldcount<= 300
                                 && toolcount <= 3 && curStreak == 1;
        
        Debug.Log("是否触发回归奖励: "+isTriggerFirstWin+"离线时间(秒数): "+offlineSeconds+"今日拼字玩法通关次数: "+todaywinTime+"金币数量: "+goldcount+"道具数量: "+toolcount+"连胜天数: "+curStreak);
        
        // switch ((LevelType)GameDataManager.Instance.UserData.levelMode)
        // {
        //     case LevelType.ChessWord:
        //         if (isTriggerFirstWin)
        //         {
        //             SystemManager.Instance.ShowPanel(PanelType.ReturnFirstWinScreen);
        //         }
        //         break;
        // }

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

    /// <summary>
    /// 动态刷新顶部过关文本（处理限时活动“还差一关”的精准预判）
    /// </summary>
    private void UpdateLimitEventText()
    {
        int currStage = GameDataManager.Instance.UserData.CurrentChessStage;
        // 默认显示：普通关卡号
        string rawText = MultilingualManager.Instance.GetString("ScheduleLess", "pingzi");
        reachLevelText.text = rawText.Replace("X", currStage.ToString());

        if (LimitTimeManager.Instance == null || LimitTimeManager.Instance.CurlimitData == null)
            return;
        if (!_limitBtnTable._limitTimeEventButton.gameObject.activeSelf)
            return;
        int pendingWords = _isWordsExtracted ? 0 : ChessStageController.Instance.LimitPuzzleCount;
        int totalDone = GetCompletedWordCount() + pendingWords; // 现在是总词数
        int totalTarget = LimitTimeManager.Instance.CurlimitData.num;
        int remainWords = totalTarget - totalDone;

        // 已经完成或超额，保留默认文本
        if (remainWords <= 0) return;

        bool isDoubleTimeActive = LimitTimeManager.Instance.LimitTimeCanShow();
        // 逐关累加后续关卡词组数，直到满足剩余词数
        int need = remainWords;
        int nextStage = currStage;
        int maxStage = currStage + 50; // 安全边界，防止无限循环
        while (need > 0 && nextStage < maxStage)
        {
            // 获取下一关的词组数（按 pass 中的 '#' 分割计数）
            int wordCount = 5; // 默认兜底
            try
            {
                var conf = ChessStageController.Instance.PackInfos.Get(nextStage);
                if (conf != null && !string.IsNullOrEmpty(conf.pass))
                {
                    wordCount = conf.pass.Split('#').Length;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UI预判] 获取关卡 {nextStage} 配置失败，使用兜底数量 5。原因: {e.Message}");
            }

            if (isDoubleTimeActive)
            {
                wordCount *= 2;
            }

            need -= wordCount;
            if (need > 0)
            {
                nextStage++; // 如果扣完还是不够，再推入下一关
            }
        }

        // 修复：因为顺序调整了，现在 nextStage == currStage 就代表“只需要打接下来的这一关”
        if (nextStage == currStage)
        {
            reachLevelText.text = MultilingualManager.Instance.GetString("OneMore", "pingzi");
        }
        else
        {
            // 需要多关，替换文本中的占位符
            reachLevelText.text = rawText.Replace("X", nextStage.ToString());
        }
    }

    private void InitializeUI()
    {
        _isWordsExtracted = false; // 🌟 每次打开面板，重置提取标记
        // _centerLotusImage.gameObject.SetActive(true);
        if (progressText != null) progressText.gameObject.SetActive(false);
        _centerLotusImage.color = new Color32(255, 255, 255, 0);
        _currentProgressSegment = 0;
        // 面板刚打开时，底层还没加本局词数，直接拿旧进度设定起跑线
        if (LimitTimeManager.Instance.CurlimitData != null)
        {
            int oldWordCount = GetCompletedWordCount();
            progressSlider.value = Mathf.Clamp01((float)oldWordCount / LimitTimeManager.Instance.CurlimitData.num);
        }
        
        UpdateLimitEventText();
        if (LimitTimeManager.Instance.IsComplete())
            progressSlider.transform.parent.gameObject.SetActive(false);
        else
            progressSlider.transform.parent.gameObject.SetActive(true);

        // 设置关卡文本
        int Stage = GameDataManager.Instance.UserData.CurrentChessStage;
        ChessStageController.Instance.CurLevelMode = ChessStageController.Instance.GetLevelDifficultyMode(Stage);

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

        levelText.text = MultilingualManager.Instance.GetString("Level") + " " + Stage;
        zenScoreText.text = ChessStageController.Instance.CurrentTotalScore.ToString();

        if (!LimitTimeManager.Instance.IsComplete() && _limitBtnTable._limitTimeEventButton.gameObject.activeSelf)
        {
            Content.gameObject.SetActive(true);
            // StartCoroutine(UpdateProgress());
        }
        else
        {
            Content.gameObject.SetActive(false);
        }

        if (ChessStageController.Instance.IsCurrentStageSkipped)
        {
            _encourageTitleText.text = "";
            _encouragePhraseText.text = "";
        }
        else
        {
            RenderEncouragementTexts();
        }
        lotusInstance?.SetActive(false);
        // 拦截奖励序列：如果是第一次进，播放动画；如果是从榜单返回，直接展示按钮
        if (!_hasPlayedRewardSequence)
        {
            _hasPlayedRewardSequence = true; // 标记为已播放
            nextBtn.gameObject.SetActive(false); // 将隐藏逻辑移到这里：只有首次进入才先隐藏，等动画播完再显示
            StartCoroutine(PlayRewardSequence());
        }
        else
        {
            // 已经播放过了（从排行榜返回），直接把下一步按钮显示出来，防止界面卡死
            nextBtn.gameObject.SetActive(true);
            nextBtn.gameObject.GetComponent<CanvasGroup>().alpha = 1;

            // 维持限时活动图标的状态
            if (_limitBtnTable._limitTimeEventButton.gameObject.activeSelf && LimitTimeManager.Instance != null &&
                GetCompletedWordCount() >= LimitTimeManager.Instance.CurlimitData.num)
            {
                Showlimiticon.SetActive(true);
            }
        }
        DailyTaskManager.Instance.UpdateMaxButterflyTime();
    }

    /// <summary>
    /// 核心：从 Controller 提取配置，根据概率渲染文案
    /// </summary>
    private void RenderEncouragementTexts()
    {
        // 1. 获取已经判定好的规则
        var rule = ChessStageController.Instance.CurrentMatchedRule;
        int bannerStyle = ChessStageController.Instance.CurrentBannerStyle;
        // 兜底：先全部隐藏
        if (_encourageTitleRoot != null) _encourageTitleRoot.SetActive(false);
        if (_encouragePhraseRoot != null) _encouragePhraseRoot.SetActive(false);

        if (rule == null) return;

        // 2. 扔骰子 (0-99)，根据你的配置表概率控制显示
        int rollTitle = UnityEngine.Random.Range(0, 100);
        int rollPhrase = UnityEngine.Random.Range(0, 100);

        // 3. 处理位置5（顶部标题）
        if (rule.TitleRate > 0 && rollTitle < rule.TitleRate)
        {
            if (_encourageTitleRoot != null)
            {
                _encourageTitleRoot.SetActive(true);
                _encourageTitleText.text = MultilingualManager.Instance.GetString(rule.TitleKey, "pingzi");
                // 进场小动画（可选）
                _encourageTitleRoot.transform.DOPunchScale(Vector3.one * 0.1f, 0.4f);
            }
        }

        // 4. 处理位置6（结算页长文案及图标）
        if (rule.StimulateRate > 0 && rollPhrase < rule.StimulateRate)
        {
            if (_encouragePhraseRoot != null)
            {
                _encouragePhraseRoot.SetActive(true);

                // 组装文本（优先读长文案，没有长文案读短文案）
                string rawText = MultilingualManager.Instance.GetString(rule.LongTextKey, "pingzi");
                string phraseText = MultilingualManager.Instance.GetString(rule.PhraseKey, "pingzi");
                float formattedX = ChessStageController.Instance.DisplayZenPercent;
                _encouragePhraseText.text =
                    "\ud83c\udf1f" + string.Format(rawText, formattedX.ToString("F2")) + phraseText;
                // 加载表情 Emoji
                // if (_encourageEmojiIcon != null && !string.IsNullOrEmpty(rule.EmojiKey))
                // {
                //     var emojiSprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("commonitem", rule.EmojiKey);
                //     if (emojiSprite != null) _encourageEmojiIcon.sprite = emojiSprite;
                // }
            }
        }

        if (bannerStyle != 1 && bannerStyle != 2)
        {
            Debug.Log($"当前横幅类型为 {bannerStyle}，不满足结算页文案展示条件 (需为 1 或 2)，强制隐藏。");
            _encouragePhraseRoot.SetActive(false);
            return;
        }
    }

    private void UpdateTimeDisplay(string time)
    {
        if (!string.IsNullOrEmpty(time))
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
    /// 🌟 新增：刷新结算界面的蝶蛹进度展示
    /// </summary>
    private void UpdateButterflyProgressUI(int targetCount = -1, bool isInit = false)
    {
        // 1. 判断是否开启蝶园
        bool isOpen = ButterfliesManager.Instance.IsOpen;
        if (!isOpen) return;

        bool isAllCollected = ButterfliesManager.Instance.IsAllButterfliesCollected();
        Slider slider = butterflyBtn.GetComponentInChildren<Slider>(true);
        Text pupaText = butterflyBtn.GetComponentInChildren<Text>(true);
        if (isAllCollected)
        {
            // 全收集满：关掉进度条和进度文字
            if (slider != null) slider.gameObject.SetActive(false);
            if (pupaText != null) pupaText.gameObject.SetActive(false);
            return; // 结束执行
        }

        if (slider != null) slider.gameObject.SetActive(true);
        if (pupaText != null) pupaText.gameObject.SetActive(true);

        // 2. 获取目标配置
        ButterflyGrow butterflyGrow = ButterfliesManager.Instance.GetCurrentGrow();
        int displayCount = targetCount >= 0 ? targetCount : GameDataManager.Instance.ButterflyData.currPupa;

        // 3. 计算进度比例 (0.0f ~ 1.0f)
        float progressValue = 0f;
        if (butterflyGrow != null && butterflyGrow.Count > 0)
        {
            progressValue = Mathf.Clamp01((float)displayCount / butterflyGrow.Count);
        }

        // 4. 设置进度条 (支持自定义 ProgressBar 或原生 Image Filled)
        // 如果你用的是原生的 Image Filled 模式：
        if (slider != null)
        {
            slider.DOKill(); // 杀掉这根进度条上的旧动画，防止鬼畜抖动
            if (isInit) slider.value = progressValue;
            // DOValue 是 DOTween 专门给 Slider 写的魔法方法！花 0.5 秒平滑涨过去！
            else slider.DOValue(progressValue, 0.5f).SetEase(Ease.OutQuad);
        }

        // 5. 设置文字 (例如 "10 / 60")
        if (pupaText != null)
        {
            pupaText.text = $"{targetCount} / {butterflyGrow?.Count.ToString() ?? "&"}";
        }
        
        ButterflyRedpoint.gameObject.SetActive(ButterfliesManager.Instance.showButterflyRedPoint);
    }
    
    private void UpdateSliderProgress()
    {
        if (LimitTimeManager.Instance == null) return;
        if (DOTween.IsTweening(progressSlider)) return;
        
        int wordCount = GetCompletedWordCount();
        LimitDataItem limitData = LimitTimeManager.Instance.CurlimitData;
        if (limitData == null) return;
        float targetProgress = Mathf.Clamp01((float)wordCount / limitData.num);
        // 如果进度没变化（比如本局没获得词），直接瞬间赋值并结束
        if (targetProgress < progressSlider.value || Mathf.Abs(progressSlider.value - targetProgress) < 0.01f)
        {
            progressSlider.DOKill();
            progressSlider.value = targetProgress;
            UpdateLimitEventText();
            return;
        }
        // 🌟 核心动画逻辑
        progressSlider.DOKill(); // 清理旧动画
        Sequence seq = DOTween.Sequence();
        seq.AppendInterval(0.5f);
        seq.Append(progressSlider.DOValue(targetProgress, 0.8f).SetEase(Ease.OutQuad));
        seq.OnComplete(UpdateLimitEventText);
    }
    
    /// <summary>
    /// 获取当前限时活动已完成词语数
    /// </summary>
    private int GetCompletedWordCount()
    {
        if (LimitTimeManager.Instance == null) return 0;
        return LimitTimeManager.Instance.GetCurWordCount();
    }


    /// <summary>
    /// 播放奖励获取序列动画
    /// </summary>
    private IEnumerator PlayRewardSequence()
    {
        _tasktable.taskEffect.gameObject.SetActive(false);
        _matchFishtable.matchEffect.gameObject.SetActive(false);
        
        isShowWinSign = StreakManager.Instance.IsCanShowWinSign();
        bool isJump = ChessStageController.Instance.IsJump;

        if (isShowWinSign&&!isJump)
        {
            SystemManager.Instance.ShowPanel(PanelType.SignWinScreen);
        }
        else
        {
            CheckReturnFirstWinScreen();
        }
        
        if (isShowWinSign||isTriggerFirstWin)
        {
            nextBtn.gameObject.GetComponent<CanvasGroup>().alpha = 0;
            nextBtn.gameObject.SetActive(true);
            nextBtn.gameObject.GetComponent<CanvasGroup>().DOFade(1, 0.3f);
            
            yield return new WaitUntil(() => !SystemManager.Instance.PanelIsShowing(PanelType.SignWinScreen)
                                             &&!SystemManager.Instance.PanelIsShowing(PanelType.SevenSignScreen)
                                             &&!SystemManager.Instance.PanelIsShowing(PanelType.ReturnFirstWinScreen));
        }
        
           
        // ==========================================
        // 🌟 队列首位：全权交由专属协程处理禅修榜的三种状态
        // ==========================================
        yield return StartCoroutine(HandleZenRankFlowRoutine());
        
        AdRuleManager.Instance.TryShowInterstitial((issuccess) =>
        {
            if (issuccess)
            {
                AnalyticMgr.InsetAdSuccess("关卡插屏");
                GameDataManager.Instance.UserData.totalInsetSeeAds++;
            }
            else
            {
                AnalyticMgr.InsetAdFail("关卡插屏");
            }
        });
        int cachedLimitCount = ChessStageController.Instance.LimitPuzzleCount;
        int leafCollected = ChessStageController.Instance.CurrStageData.CollectedLeaves;
        if (leafCollected > 0)
        {
            EventDispatcher.instance.TriggerUpdateLayerCoin(true, false, false);
        }
        // =========================================
        // 🌟 队列第二位：限时活动逻辑及面板
        // =========================================
        if (_limitBtnTable._limitTimeEventButton.gameObject.activeSelf)
        {
            _limitBtnTable.CheckAndShowLimitedTimeEvent(Enlimiticon.transform);
            _isWordsExtracted = true; // 锁住预判文案
            yield return new WaitForSeconds(0.5f);

            // 根据进度决定是否自动弹出限时活动面板
            if (LimitTimeManager.Instance != null &&
                GetCompletedWordCount() >= LimitTimeManager.Instance.CurlimitData.num)
            {
                Showlimiticon.SetActive(true);
                SystemManager.Instance.ShowPanel(PanelType.LimitTimeScreen);

                yield return new WaitUntil(() => !SystemManager.Instance.PanelIsShowing(PanelType.LimitTimeScreen));
            }
            else
            {
                Showlimiticon.SetActive(false);
            }

            nextBtn.gameObject.GetComponent<CanvasGroup>().alpha = 0;
            nextBtn.gameObject.SetActive(true);
            nextBtn.gameObject.GetComponent<CanvasGroup>().DOFade(1, 0.3f);

            if (!isShowWinSign)
            {
                nextBtn.gameObject.GetComponent<CanvasGroup>().alpha = 0;
                nextBtn.gameObject.SetActive(true);
                nextBtn.gameObject.GetComponent<CanvasGroup>().DOFade(1, 0.3f);
            }
        }
        bool isRankActive = GameDataManager.Instance.UserData.isJoinedZenRank && 
                            ZenRankManager.Instance.RemainingSeconds > 0;
        // // ==========================================
        // // 🌟 新增：先播放莲花飞向禅意榜的动画！
        // // ==========================================
        if (isRankActive && _zenRankBtn.gameObject.activeSelf && ChessStageController.Instance.CurrentTotalScore > 0)
        {
            yield return StartCoroutine(PlayZenLotusFlyAnim());
        }

        if (butterflyBtn.gameObject.activeSelf && ChessStageController.Instance.EarnedPupaThisLevel > 0)
        {
            yield return StartCoroutine(PlayPupaFlyAnim());
        }

        // 🌟 步骤三：树叶额外金币起飞（从中央飞向 Header 金币槽）
        // ==========================================
        var leafReward = ChessStageController.Instance.GetAllLeafRewards(leafCollected);
        if (leafReward != null && leafReward.Count > 0)
        {
            CustomFlyInManager.Instance.FlyInGold(Content.transform, () =>
            {
                GameDataManager.Instance.UserData.UpdateGold(leafReward[0].Value, true, true, "树叶收集结算获得");
                // EventDispatcher.instance.TriggerChangeGoldUI(leafReward[0].Value, true);
            });
            yield return new WaitForSeconds(0.5f);
        }

        ChessStageController.Instance.LimitPuzzleCount = cachedLimitCount;
        if (!GameDataManager.Instance.UserData.isAllCompleteTask && _tasktable.TaskBtn.gameObject.activeSelf)
        {
            _tasktable.CheckTasksScreen();
            yield return new WaitForSeconds(1.5f);
        }

        ChessStageController.Instance.LimitPuzzleCount = 0;
        if (FishInfoController.Instance.IsShowFishProgressAnim() && _matchFishtable.FishBtn.gameObject.activeSelf)
        {
            _matchFishtable.ShowFishWordAnim();
            StartCoroutine(UpdateFishRankUI());
            //yield return new WaitForSeconds(1.2f);
        }

        // 等待限时活动进度更新
        //yield return new WaitForSeconds(0.5f);
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

    private IEnumerator CheckZenRankBtn()
    {
        WaitForSeconds waitTime = new WaitForSeconds(0.5f);
        while (_zenRankBtn.gameObject.activeSelf)
        {
            yield return waitTime;
            _zenRankBtn.GetComponent<ZenRankButton>().CheckRankProgress();
        }
    }

    private void OnNextButtonClick()
    {
        _hasPlayedRewardSequence = false; // 离开结算页去下一关时重置标记
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
        UnlockButton(_tasktable.TaskBtn, AppGameSettings.UnlockRequirements.DailyMissions, PanelType.DailyTasksScreen,
            GameDataManager.Instance.FishUserSave.opentime);
        UnlockButton(_limitBtnTable._limitTimeEventButton, AppGameSettings.UnlockRequirements.TimeLimitMode,
            PanelType.LimitTimeScreen,
            GameDataManager.Instance.UserData.limitOpenTime);
    }

    private void UnlockButton(Button button, int unlockLevel, string panelName, string openTime)
    {
        int currentStage = Mathf.Max(GameDataManager.Instance.UserData.CurrentChessStage,
            GameDataManager.Instance.UserData.CurrentHexStage);
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

    /// <summary>
    /// 播放莲花飞向禅意排行榜的动画
    /// </summary>
    private IEnumerator PlayZenLotusFlyAnim()
    {
        // 1. 获取本局获得的禅意数量（替换为你实际增加的变量，比如这里测试用 +15）
        int addZenCount =
            ChessStageController.Instance.CurrentTotalScore; // 比如：GameDataManager.Instance.UserData.AddZenCount

        // 如果增加了禅意，且排行榜按钮开启了，才播动画
        if (addZenCount > 0 && _zenRankBtn != null && _zenRankBtn.gameObject.activeSelf)
        {
            Vector3 startPos = _centerLotusImage != null
                ? _centerLotusImage.transform.position
                : zenScoreText.transform.position + new Vector3(0, 80f, 0);

            // ==========================================
            // 🌟 阶段一：原地的暗荷花，直接替换图片变成亮荷花！
            // ==========================================
            // 1. 直接替换图片
            // Image ligh = _centerLotusImage.transform.GetChild(0).GetComponent<Image>();
            _centerLotusImage.DOFade(1, 1.5f).OnComplete(() =>
            {
                // 2. 顺便给它一个“砰”地亮起的弹跳小动效 (放大到 1.15 倍再缩回来)
                _centerLotusImage.transform.DOScale(1.02f, 0.2f).SetLoops(1, LoopType.Yoyo);
            });
            // 3. 等待 0.4 秒，让玩家看清楚“它亮了”
            yield return new WaitForSeconds(1.75f);

            // 初始化莲花状态（放在屏幕中央或者特定初始位置）
            Canvas canvas = lotusInstance.GetComponent<Canvas>();
            if (canvas == null) canvas = lotusInstance.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingLayerName = "PopPanel"; // 保持和你前面配置的一样
            canvas.sortingOrder = 100;
            GraphicRaycaster raycaster = lotusInstance.GetComponent<GraphicRaycaster>();
            if (raycaster == null) raycaster = lotusInstance.AddComponent<GraphicRaycaster>();
            raycaster.enabled = false; // 飞行期间不要阻挡点击
            CanvasGroup cg = lotusInstance.GetComponent<CanvasGroup>();
            if (cg == null) cg = lotusInstance.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.alpha = 1f; // 如果你想稍微有点透明度，可以改成 0.9f
            lotusInstance.SetActive(true);
            Text textComponent = lotusInstance.GetComponentInChildren<Text>();
            if (textComponent != null) textComponent.text = "+" + addZenCount;

            lotusInstance.transform.position = _centerLotusImage.transform.position;
            lotusInstance.transform.localScale = Vector3.zero; // 先缩小，准备弹出
            // ==========================================
            // 第二步：计算绕开中心障碍物的弧线路径
            // ==========================================
            // Vector3 startPos = lotusInstance.transform.position;
            Vector3 endPos = _zenRankBtn.transform.position;
            // 取起点和终点的中心点
            Vector3 midPos = (startPos + endPos) / 2f;
            float distance = Vector3.Distance(startPos, endPos);

            // 🌟 1. 获取当前 Canvas 的真实物理边界
            RectTransform canvasRect = GetComponentInParent<Canvas>().GetComponent<RectTransform>();
            Vector3[] corners = new Vector3[4];
            canvasRect.GetWorldCorners(corners);

            // corners[0]是左下角，corners[2]是右上角
            float canvasRightX = corners[2].x;
            float canvasWidth = corners[2].x - corners[0].x;

            // 🌟 2. 划定护城河：屏幕最右侧往回退 10% 的宽度，作为绝对安全线
            float safeRightX = canvasRightX - (canvasWidth * 0.1f);

            // 🌟 3. 计算理想偏移量（把 0.4 稍微降到 0.35，对长屏幕来说弧度更自然）
            float idealTargetX = midPos.x + (distance * 0.35f);

            // 🌟 4. 核心数学拦截公式：控制点X <= 2 * 安全边界 - 中心点X
            float maxAllowedX = 2f * safeRightX - midPos.x;

            // 🌟 5. 最终取值：谁小听谁的（确保绝不越过安全线）
            midPos.x = Mathf.Min(idealTargetX, maxAllowedX) - 1.2f;

            // 构成 3 个点的路径数组
            Vector3[] pathPoints = new Vector3[] { startPos, midPos, endPos };

            // 2. 使用 DOTween 创建连续动画序列
            Sequence seq = DOTween.Sequence();
            seq.SetLink(lotusInstance);
            seq.AppendCallback(() => AudioManager.Instance.PlaySoundEffect("LotusPop", 0, 1));
            // 动作1：在起点瞬间弹出来，稍微悬浮一下给玩家看清楚
            seq.Append(lotusInstance.transform.DOScale(1.2f, 0.3f).SetEase(Ease.OutBack));
            // 动作2：停顿展示一下数字，让玩家看清楚加了多少 (耗时 0.3秒)
            seq.AppendInterval(0.3f);

            // 动作3：朝排行榜按钮飞过去！
            // PathType.CatmullRom 会让物体平滑地穿过我们设定的那个侧边中点
            seq.Append(lotusInstance.transform.DOPath(pathPoints, 1.0f, PathType.CatmullRom).SetEase(Ease.InCubic));
            // 边飞边缩小，表现出“飞入框内”的纵深感
            // seq.Join(lotusInstance.transform.DOScale(0.4f, 1.8f).SetEase(Ease.InQuad));
            bool isAnimFinished = false;
            // 动作4：飞到目标后的回调
            seq.OnComplete(() =>
            {
                // 1. 飞到了，把飞行道具隐藏
                lotusInstance.SetActive(false);
                // 2. 通知目标按钮：“砸到你了，请播放你的震动特效和刷新逻辑！”
                _zenRankBtn.PlayAbsorbEffect(addZenCount);
                isAnimFinished = true;

                // 🌟 (可选) 在这里调用刷新排行榜按钮文字/总数的逻辑
                if (_zenRankBtn != null)
                    _zenRankBtn.GetComponent<ZenRankButton>().FetchMyCurrentRank();
            });

            yield return new WaitUntil(() => isAnimFinished);
            // 阻塞等待动画大部分播完，再让结算界面去播下一个（比如灯笼）的动画
            yield return new WaitForSeconds(0.3f);
        }
    }

    /// <summary>
    /// 播放蝶蛹飞向蝴蝶按钮，并增加进度的动画
    /// </summary>
    private IEnumerator PlayPupaFlyAnim()
    {
        if (ButterfliesManager.Instance.IsAllButterfliesCollected()) yield break;
        int earnedPupa = ChessStageController.Instance.EarnedPupaThisLevel;

        // 如果本局获得了蝶蛹，才播放动画
        if (earnedPupa > 0 && butterflyBtn != null)
        {
            int initialPupa = GameDataManager.Instance.ButterflyData.currPupa - earnedPupa;
            int completedCount = 0;
            bool allDone = false;
            // 循环生成蝶蛹 (获得几个就飞几次)
            for (int i = 0; i < earnedPupa; i++)
            {
                GameObject pupaInstance = _pupaPool.GetObject();

                Canvas canvas = pupaInstance.GetComponent<Canvas>();
                // 2. 只有真没找到，才去添加
                if (canvas == null)
                {
                    canvas = pupaInstance.AddComponent<Canvas>();
                }

                canvas.overrideSorting = true;
                canvas.sortingLayerName = "PopPanel";
                canvas.sortingOrder = 10;

                pupaInstance.SetActive(true);
                pupaInstance.transform.localPosition = Vector3.zero; // 从屏幕中间开始
                pupaInstance.transform.localScale = Vector3.one;

                Sequence seq = DOTween.Sequence();
                seq.SetLink(pupaInstance);

                // 动作1：向上弹起
                seq.Append(pupaInstance.transform.DOLocalMoveY(150f, 0.4f).SetRelative(true).SetEase(Ease.OutQuad));

                // 动作2：飞向蝴蝶按钮，同时缩小
                seq.Append(pupaInstance.transform.DOMove(butterflyBtn.transform.position, 0.6f).SetEase(Ease.InBack));
                seq.Join(pupaInstance.transform.DOScale(0.5f, 0.6f));
                
                // 动作3：飞到了！
                seq.OnComplete(() =>
                {
                    AudioManager.Instance.PlaySoundEffect("getPupa"); // 播放获得音效
                    completedCount++;
                    int currentVisualPupa = initialPupa + completedCount;
                    // 🌟 核心：飞到一个，进度条就涨一点！
                    // int oldPupaCount = GameDataManager.Instance.ButterflyData.currPupa - earnedPupa;
                    // int currentVisualPupa = oldPupaCount + (i + 1); 
                    UpdateButterflyProgressUI(GameDataManager.Instance.ButterflyData.currPupa); // 触发 UI 更新动画

                    // 可选：让按钮有个被击中的弹跳反馈
                    butterflyBtn.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 10, 1);
                    if (completedCount >= earnedPupa)
                        allDone = true;

                    _pupaPool.ReturnObjectToPool(pupaInstance.GetComponent<PoolObject>());
                });
                // 每个蝶蛹启动后，间隔 0.2 秒再启动下一个（最后一个不用等）
                if (i < earnedPupa - 1)
                    yield return new WaitForSeconds(0.2f);
            }
            // 等待所有蝶蛹动画彻底结束
            yield return new WaitUntil(() => allDone);
            // 所有蝶蛹都飞完了，稍微停顿一下再继续后面的流程
            // yield return new WaitForSeconds(0.8f);
        }
    }

    private void OnButterflyClick()
    {
        if (GameCoreManager.Instance.PanelState == PanelState.MainMenuPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.PrimaryInterface);
        }
        else if (GameCoreManager.Instance.PanelState == PanelState.FinishXiaoPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.StageFinishView);
        }
        else if (GameCoreManager.Instance.PanelState == PanelState.FinishPingPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.ChessFinishView);
        }

        SystemManager.Instance.HidePanel(PanelType.HeaderSection, true,
            () => { SystemManager.Instance.ShowPanel(PanelType.ButterflyHome); });
    }

    /// <summary>
    /// 统筹处理禅修榜结算、加入雷达、名次变动的三重状态
    /// </summary>
    private IEnumerator HandleZenRankFlowRoutine()
    {
        // 0. 前置检查：功能是否已经解锁？没解锁直接跳过
        bool isZenUnlocked = _zenRankBtn != null && _zenRankBtn.gameObject.activeSelf;
        if (!isZenUnlocked) yield break;
  
        var userData = GameDataManager.Instance.UserData;
           
        bool isFirstUnlockZen = !userData.isJoinedZenRank &&
                                userData.CurrentChessStage == AppGameSettings.UnlockRequirements.ZenOpenLevel;
        if (isFirstUnlockZen)
        {
            // ---------- 检查昵称（与 ZenRankButton 点击逻辑一致）----------
            // 判断是否允许弹出起名窗
            if (userData.CanShowCharInfoPopup())
            {
                // 标记已弹出，避免后续重复弹窗
                userData.MarkCharInfoPopupShown();
                // 弹出起名面板，阻塞等待关闭
                SystemManager.Instance.ShowPanel(PanelType.RewardNamePanel);
                yield return new WaitUntil(() => !SystemManager.Instance.PanelIsShowing(PanelType.RewardNamePanel));
            }
            // ---------- 昵称检查结束 ----------
            
            UIWindow radarWindow = SystemManager.Instance.ShowPanel(PanelType.ZenRankStartScreen);
            if (radarWindow != null)
            {
                ZenRankStartScreen startScreen = radarWindow.GetComponent<ZenRankStartScreen>();
                startScreen.SetSourcePanel(PanelType.ChessFinishView);
                startScreen.SetForcedMode(true); // 🌟 开启强制模式，隐藏关闭按钮！
            }
            
            // 既然强制弹出了匹配页，玩家必须点击匹配，匹配完会自动触发进下一关
            // 此时结算页会被强行关闭。所以在这里使用死循环挂起，不让后续代码继续执行！
            yield return new WaitUntil(() => !SystemManager.Instance.PanelIsShowing(PanelType.ZenRankStartScreen));
            yield break; // 匹配结束界面关闭后，直接退出协程
        }
        // ==========================================
        // 🌟【关键修复】：静默检查赛季状态 (绝对不在这里弹窗)
        // ==========================================
        bool hasSettlement = false;
        bool isCheckFinished = false;
        // 直接调用底层 API 请求，不走带 UI 的 Routine
        yield return APIGateway.Instance.LeaderboardApi.CheckZenSettlement((res) =>
        {
            if (res != null)
            {
                hasSettlement = res.has_settlement;
            }
            isCheckFinished = true;
        });

        yield return new WaitUntil(() => isCheckFinished);
        // 如果服务器标记已有结算，或者本地倒计时已经归零，说明处于“赛季交替期”
        if (hasSettlement || ZenRankManager.Instance.RemainingSeconds <= 0)
        {
            Debug.Log("[ZenFlow] 检测到赛季已结束或有待领取的奖励。静默拦截：胜利页不弹结算，跳过排名变化动画。");
            // 直接退出流程！玩家点继续后会回到大厅，由大厅统一管理赛季结算弹窗的弹出。
            yield break; 
        }
        // ==========================================
        int addZenCount = ChessStageController.Instance.CurrentTotalScore;
        // 纯净判断：加入了、赛季没结束、得分>0 -> 弹名次变化面板！
        if (userData.isJoinedZenRank && ZenRankManager.Instance.RemainingSeconds > 0 && addZenCount > 0)
        {
            yield return StartCoroutine(CheckAndShowZenRankChange(addZenCount));
        }
    }

    // ==========================================
    // 串行弹窗 1：禅修榜排名变动面板 (已清理净化)
    // ==========================================
    private IEnumerator CheckAndShowZenRankChange(int addZenCount)
    {
        float waitTime = 0f;
        while (ZenRankManager.Instance.IsFetching)
        {
            waitTime += Time.deltaTime;
            if (waitTime > 5f) yield break; // 超时保护
            yield return null;
        }
        var myData = ZenRankManager.Instance.MyCurrentRankData;
        Debug.Log($"【Rank Debug - Flow】是否进入排名变化加载之前 myData: {(myData != null ? "非空" : "空")}");
        
        if (myData != null)
        {
            // 1. 严格使用缓存中的旧数据，如果没缓存(首次)，说明旧分数就是现在分数减去新增
            // int realOldScore = Mathf.Max(0, myData.score - addZenCount);
            int realOldScore = ZenRankManager.Instance.CachedOldScore;
            int realOldRank = ZenRankManager.Instance.CachedOldRank;
            Debug.Log($"【Rank Debug - Flow】[计算前] 从缓存读取到的真实旧分数: {realOldScore}, 旧排名: {realOldRank}, 准备加上的本局得分: {addZenCount}");
            if (realOldScore == 0 && realOldRank == 0 && myData != null)
            {
                // 真实旧分数 = 服务器最新分数 - 本局加分 (最低不小于0，保护雷达入榜底分)
                realOldScore = Mathf.Max(0, myData.score - addZenCount);
    
                // 如果倒推出来的旧分数是0，说明之前完全没分，名次强制给0以触发飞升动画。
                // 如果有底分，暂时借用最新名次进行兜底。
                realOldRank = (realOldScore == 0) ? 0 : Mathf.Max(1, myData.rank);
    
                Debug.Log($"【Rank Debug - Fix】缓存为空，倒推出真实旧数据 -> 旧分数: {realOldScore}, 旧排名: {realOldRank}");
            }else 
            if (realOldRank <= 0)
            {
                realOldScore = 0; // 旧分数强制修正为0
                realOldRank = 0;  // 之前没上榜，旧名次强制修正为0（让UI显示 "-" 并播放飞升动画）
                Debug.Log("【Rank Debug - Flow】检测到之前未上榜(realOldRank <= 0)，将旧分数和排名强行重置为 0。");
            }
            
            // ==========================================
            // 前端“乐观预测” (解决 Laravel 队列未处理完的问题)
            // ==========================================
            // 不管服务器有没有加分，我们自己先把本局得分加上去！
            int expectedNewScore = realOldScore + addZenCount;
            int optimisticNewRank;
            // 如果服务器下发的分数已经 >= 我们预期的分数，
            // 说明后端队列瞬间处理完了！此时直接使用服务器的真实排名，跳过预测！
            if (myData.score >= expectedNewScore)
            {
                optimisticNewRank = myData.rank;
                expectedNewScore = myData.score;
                Debug.Log($"【Rank Debug - Flow】[命中真实数据] 服务器队列已处理完毕，跳过预测，直接使用最新真实排名:{optimisticNewRank}");
            }
            else
            {
                optimisticNewRank = ZenRankManager.Instance.PredictMyRealRank(realOldRank, expectedNewScore);
                Debug.Log($"【Rank Debug - Flow】[预测结果] 服务器数据未同步，执行预测新排名:{optimisticNewRank}");
            }
            Debug.Log($"【Rank Debug - Flow】传入面板的值 -> 旧分数:{realOldScore}, 新分数:{expectedNewScore}, 新增差值:{expectedNewScore - realOldScore}, 最终应用排名:{optimisticNewRank}");
            // 传入智能抓取的“环境玩家”数据，UI 会自动计算谁在上谁在下！
            // List<ZenRankState> contextPlayers = ZenRankManager.Instance.GetContextPlayersForAnimation(realOldRank, myData.rank);
            List<ZenRankState> contextPlayers = ZenRankManager.Instance.GetContextPlayersForAnimation(realOldRank, optimisticNewRank);
            // ⚠️ 删除了强行把 newRank 置为 1 的假逻辑，没上榜就是 <=0，UI层会显示为 "-"
            UIWindow window = SystemManager.Instance.ShowPanel(PanelType.ZenRankChangePanel);
            if (window != null)
            {
                ZenRankChangePanel rankChangePanel = window.GetComponent<ZenRankChangePanel>();
                string levelCode = GameDataManager.Instance.UserData.Zenlevel;
                string levelName = ZenRankManager.Instance.ZenStates.FirstOrDefault(s => s.Code == levelCode)?.Name ?? "";
          
                rankChangePanel.PlayRankChange(
                    realOldRank, optimisticNewRank,
                    realOldScore, expectedNewScore,
                    levelCode, levelName, ZenRankManager.Instance.RemainingSeconds,
                    contextPlayers // 传入真实环境数据
                );

                yield return new WaitUntil(() => !SystemManager.Instance.PanelIsShowing(PanelType.ZenRankChangePanel));
                yield return new WaitForSeconds(0.3f);
            }

            // 动画播完，同步缓存防止重复播
            ZenRankManager.Instance.CachedOldScore = expectedNewScore;
            ZenRankManager.Instance.CachedOldRank = optimisticNewRank;
            // ZenRankManager.Instance.SyncCachedRank();
            Debug.Log($"【Rank Debug - Cache】动画播放完毕，已将最新预测值存入缓存。当前 Cache 旧分数变更为: {ZenRankManager.Instance.CachedOldScore}");
        }
    }

    protected override void OnDisable()
    {
        LimitTimeManager.Instance.OnDailyTimeUpdated -= UpdateTimeDisplay;
        DailyTaskManager.Instance.OnDailyButterflyTaskUI -= UpdateButterflyTime;
        LimitTimeManager.Instance.OnLimitTimeBtnUI -= UpdateSliderProgress;
        FishInfoController.Instance.OnFishTimeUpdated -= _matchFishtable.UpdateFishTime;

        GameDataManager.Instance?.UserData.ClearPuzzleVocabulary();

        // EventDispatcher.instance.TriggerUpdateLayerCoin(false,false,false);
        EventDispatcher.instance.TriggerChangeGoldUI(AppGameSettings.LevelCompleteBonus, false);
        if (_treasureBoxEffect != null)
            _treasureBoxEffect.gameObject.SetActive(false);
        base.OnDisable();
    }
}