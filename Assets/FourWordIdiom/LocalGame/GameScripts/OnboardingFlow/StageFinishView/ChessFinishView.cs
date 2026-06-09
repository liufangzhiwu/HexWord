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
    [SerializeField] private Button _signBtn;
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
   
    [Space]
    [Header("横幅鼓励提示 UI (新增)")]
    [Header("结算页激励文案UI节点 (需手动挂载)")]
    [SerializeField] private GameObject _encourageTitleRoot;   // 顶部标题的父节点 (比如"新纪录！")
    [SerializeField] private Text _encourageTitleText;
    [SerializeField] private GameObject _encouragePhraseRoot;  // 激励长文案的父节点
    [SerializeField] private Text _encouragePhraseText;
    [SerializeField] private Image _encourageEmojiIcon;
    
    private GameObject _treasureBoxEffect;
    private int _currentProgressSegment = 0;
    private float sliderProgress;
    
    protected override void InitializeUIComponents()
    {
        nextBtn.AddClickAction(OnNextButtonClick);
        _limitBtnTable._limitTimeEventButton.AddClickAction(OnLimitTimeEventButtonClicked);
        _signBtn.AddClickAction(ShowSignInPanel);
        Content.onClick.AddListener(() =>
        {
            SystemManager.Instance.ShowPanel(PanelType.LimitTimeScreen);
        });
        butterflyBtn.AddClickAction(OnButterflyClick);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        SystemManager.Instance.ShowPanel(PanelType.HeaderSection);
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
        DailyTaskManager.Instance.UpateButterflyTaskUI();
        butterflyBtn.gameObject.SetActive(ButterfliesManager.Instance.IsOpen);
        int oldPupaCount = GameDataManager.Instance.ButterflyData.currPupa - ChessStageController.Instance.EarnedPupaThisLevel;
        UpdateButterflyProgressUI(Mathf.Max(0, oldPupaCount),true);

        if (GameDataManager.Instance.UserData.ischangetheme)
        {
            int times=1;
            if (GameDataManager.Instance.UserData.ThemeItemUses.Keys.Contains(GameDataManager.Instance.UserData.userthemeid))
            {
                times=GameDataManager.Instance.UserData.ThemeItemUses[GameDataManager.Instance.UserData.userthemeid];
            }
            AnalyticMgr.ThemeUse(GameDataManager.Instance.UserData.userthemeid,times);
        }
        nextBtn.gameObject.SetActive(false);
        
        StartCoroutine(CheckZenRankBtn());
      
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
        // _centerLotusImage.gameObject.SetActive(true);
        _centerLotusImage.color = new Color32(255, 255, 255, 0);
        _currentProgressSegment = 0;
        
        // 设置关卡文本
        int Stage = GameDataManager.Instance.UserData.CurrentChessStage;
        string rawText = MultilingualManager.Instance.GetString("ScheduleLess", "pingzi");
        reachLevelText.text = rawText.Replace("X", Stage.ToString());
        
        if (LimitTimeManager.Instance.IsComplete())
            progressSlider.transform.parent.gameObject.SetActive(false);
        else 
            progressSlider.transform.parent.gameObject.SetActive(true);

      
        
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
        levelText.text = MultilingualManager.Instance.GetString("Level")+" " +Stage;
        zenScoreText.text = ChessStageController.Instance.CurrentTotalScore.ToString();
        
        if (!LimitTimeManager.Instance.IsComplete() && _limitBtnTable._limitTimeEventButton.gameObject.activeSelf)
        {
            Content.gameObject.SetActive(true);
            StartCoroutine(UpdateProgress());
        }
        else
        {
            Content.gameObject.SetActive(false);
        }
        
        RenderEncouragementTexts();
        StartCoroutine(PlayRewardSequence());
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
        if (bannerStyle != 1 && bannerStyle != 2)
        {
            Debug.Log($"当前横幅类型为 {bannerStyle}，不满足结算页文案展示条件 (需为 1 或 2)，强制隐藏。");
            return; 
        }
        // 2. 扔骰子 (0-99)，根据你的配置表概率控制显示
        int rollTitle = UnityEngine.Random.Range(0, 100);
        int rollPhrase = UnityEngine.Random.Range(0, 100);

        // 3. 处理位置5（顶部标题）
        if (rule.TitleRate > 0 && rollTitle < rule.TitleRate)
        {
            if (_encourageTitleRoot != null)
            {
                _encourageTitleRoot.SetActive(true);
                _encourageTitleText.text = MultilingualManager.Instance.GetString(rule.LongTextKey,"pingzi");
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
                string formattedX = ChessStageController.Instance.BeyondPercent.ToString("F2");
                // if (rawText.Contains("{0}"))
                // {
                //     rawText = rawText.Replace("{0}", formattedX);
                // }
                _encouragePhraseText.text = "\ud83c\udf1f" +  phraseText + string.Format(rawText, formattedX);
                // 加载表情 Emoji
                if (_encourageEmojiIcon != null && !string.IsNullOrEmpty(rule.EmojiKey))
                {
                    var emojiSprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("commonitem", rule.EmojiKey);
                    if (emojiSprite != null) _encourageEmojiIcon.sprite = emojiSprite;
                }
            }
        }
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
    }
    /// <summary>
    /// 带动画更新进度条
    /// </summary>
    IEnumerator UpdateProgress()
    {
        UpdateSliderProgress();
        yield return new WaitForSeconds(1f);
        
        if (LimitTimeManager.Instance == null) yield return null;

        progressSlider.transform.parent.gameObject.SetActive(true);
        int wordCount = GetCompletedWordCount();
        LimitDataItem limitData = LimitTimeManager.Instance.CurlimitData;
        if (limitData == null) yield return null;

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
    
    private void UpdateSliderProgress()
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
        int leafCollected = ChessStageController.Instance.CurrStageData.CollectedLeaves;
        if (leafCollected > 0)
        {
            EventDispatcher.instance.TriggerUpdateLayerCoin(true,false,false);
        }
        _tasktable.taskEffect.gameObject.SetActive(false);
        _matchFishtable.matchEffect.gameObject.SetActive(false);

        if (_limitBtnTable._limitTimeEventButton.gameObject.activeSelf)
        {
            _limitBtnTable.CheckAndShowLimitedTimeEvent(Enlimiticon.transform);
            yield return new WaitForSeconds(0.5f);
            
            // 根据进度决定是否自动弹出限时活动面板
            if (LimitTimeManager.Instance != null &&
                GetCompletedWordCount() >= LimitTimeManager.Instance.CurlimitData.num)
            {
                Showlimiticon.SetActive(true);
                SystemManager.Instance.ShowPanel(PanelType.LimitTimeScreen);
            }
            else
            {
                Showlimiticon.SetActive(false);
            }
            
            nextBtn.gameObject.GetComponent<CanvasGroup>().alpha = 0;
            nextBtn.gameObject.SetActive(true);
            nextBtn.gameObject.GetComponent<CanvasGroup>().DOFade(1, 0.3f);
        }
        
        // // ==========================================
        // // 🌟 新增：先播放莲花飞向禅意榜的动画！
        // // ==========================================
        if (_zenRankBtn.gameObject.activeSelf && ChessStageController.Instance.CurrentTotalScore > 0)
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
        if (leafReward != null && leafReward.Count > 0 )
        {
            CustomFlyInManager.Instance.FlyInGold(Content.transform,() =>
            {
                GameDataManager.Instance.UserData.UpdateGold(leafReward[0].Value, true, true, "树叶收集结算获得");
                // EventDispatcher.instance.TriggerChangeGoldUI(leafReward[0].Value, true);
            });
            yield return new WaitForSeconds(0.5f);
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
            //yield return new WaitForSeconds(1.2f);
        }
        
        // 等待限时活动进度更新
        //yield return new WaitForSeconds(0.5f);
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
        int addZenCount = ChessStageController.Instance.CurrentTotalScore; // 比如：GameDataManager.Instance.UserData.AddZenCount

        // 如果增加了禅意，且排行榜按钮开启了，才播动画
        if (addZenCount > 0 && _zenRankBtn != null && _zenRankBtn.gameObject.activeSelf)
        {
            Vector3 startPos = _centerLotusImage != null ? _centerLotusImage.transform.position : zenScoreText.transform.position + new Vector3(0, 80f, 0);

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
            GameObject prefab = AssetBundleLoader.SharedInstance.LoadGameObject("commonitem", "lotus_icon");
            GameObject lotusInstance = Instantiate(prefab, _centerLotusImage.transform,false);
            
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
                Destroy(lotusInstance);
                
                // 2. 通知目标按钮：“砸到你了，请播放你的震动特效和刷新逻辑！”
                _zenRankBtn.PlayAbsorbEffect(addZenCount);
                isAnimFinished = true;
                
                // 🌟 (可选) 在这里调用刷新排行榜按钮文字/总数的逻辑
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
            // 加载蝶蛹预制体 (和你在 ButterfliesManager 里用的是同一个)
            GameObject prefab = AssetBundleLoader.SharedInstance.LoadGameObject("commonitem", "Pupa");
            if (prefab == null) yield break;

            // 循环生成蝶蛹 (获得几个就飞几次)
            // for (int i = 0; i < earnedPupa; i++)
            // {
                GameObject pupaInstance = Instantiate(prefab, transform, false);
                
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
                
                bool isAnimFinished = false;
                
                // 动作3：飞到了！
                seq.OnComplete(() =>
                {
                  
                    AudioManager.Instance.PlaySoundEffect("getPupa"); // 播放获得音效
                    
                    // 🌟 核心：飞到一个，进度条就涨一点！
                    // int oldPupaCount = GameDataManager.Instance.ButterflyData.currPupa - earnedPupa;
                    // int currentVisualPupa = oldPupaCount + (i + 1); 
                    UpdateButterflyProgressUI(GameDataManager.Instance.ButterflyData.currPupa); // 触发 UI 更新动画
                    
                    // 可选：让按钮有个被击中的弹跳反馈
                    butterflyBtn.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 10, 1);
                    
                    isAnimFinished = true;
                    Destroy(pupaInstance);
                });

                // 等这只蝶蛹飞完
                yield return new WaitUntil(() => isAnimFinished);
                
                // 稍微等 0.2 秒再飞下一只
                yield return new WaitForSeconds(0.2f);
            // }
            
            // 所有蝶蛹都飞完了，稍微停顿一下再继续后面的流程
            // yield return new WaitForSeconds(0.8f);
        }
    }

    private void OnButterflyClick()
    {
        
        if (GameCoreManager.Instance.PanelState == PanelState.MainMenuPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.PrimaryInterface);
        }else if (GameCoreManager.Instance.PanelState == PanelState.FinishHexPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.StageFinishView);
        }else if (GameCoreManager.Instance.PanelState == PanelState.FinishPingPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.ChessFinishView);
        }
        
        SystemManager.Instance.HidePanel(PanelType.HeaderSection , true, () =>
        {
            SystemManager.Instance.ShowPanel(PanelType.ButterflyHome);
        });
    }
    
    protected override void OnDisable()
    {
        
        LimitTimeManager.Instance.OnDailyTimeUpdated -= UpdateTimeDisplay;
        DailyTaskManager.Instance.OnDailyButterflyTaskUI -= UpdateButterflyTime;
        LimitTimeManager.Instance.OnLimitTimeBtnUI -= UpdateSliderProgress;
        FishInfoController.Instance.OnFishTimeUpdated -= _matchFishtable.UpdateFishTime;
        
        GameDataManager.Instance?.UserData.ClearPuzzleVocabulary();
     
        EventDispatcher.instance.TriggerUpdateLayerCoin(false,false,false);
        EventDispatcher.instance.TriggerChangeGoldUI(AppGameSettings.LevelCompleteBonus, false);
        if(_treasureBoxEffect != null)
            _treasureBoxEffect.gameObject.SetActive(false);
        base.OnDisable();
    }
}
