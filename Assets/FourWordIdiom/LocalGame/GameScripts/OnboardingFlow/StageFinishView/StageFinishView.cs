using System;
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
    
    [SerializeField] private GameObject hardStageTable;          // 困难模式
    [SerializeField] private GameObject extrahardStageTable;          // 特别困难模式
    [Header("禅意飞行特效")]
    [SerializeField] private ZenRankButton _zenRankBtn;    // 🌟 这里把类型改成具体的脚本类型

    [SerializeField] private Button Content;
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
        Content.onClick.AddListener(() =>
        {
            SystemManager.Instance.ShowPanel(PanelType.LimitTimeScreen);
            
        });
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        _zenRankBtn.GetComponent<ZenRankButton>().CheckRankProgress();
        GameCoreManager.Instance.PanelState = PanelState.FinishHexPanel;
        
        GameDataManager.Instance.UserData.curIsEnter = false;
        LimitTimeManager.Instance.OnDailyTimeUpdated += UpdateTimeDisplay; // 订阅事件
        DailyTaskManager.Instance.OnDailyButterflyTaskUI += UpdateButterflyTime;
        LimitTimeManager.Instance.OnLimitTimeBtnUI += UpdateProgress; 
        FishInfoController.Instance.OnFishTimeUpdated += _matchFishtable.UpdateFishTime;
        //EventDispatcher.OnChangeHeadIconUpdateUI += UpdateHeadBtnUI;
        _matchFishtable.CheckFishBtn();
        AudioManager.Instance.PlaySoundEffect("StageFinish");   
        
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
        
        // 设置关卡文本
        int Stage = 0;
        switch (GameDataManager.Instance.UserData.levelMode)
        {
            // case 1:
            //     Stage = GameDataManager.Instance.UserData.CurrentStage != 0 ? 
            //         GameDataManager.Instance.UserData.CurrentStage : 1;
            //     sprite = LoadheadIcon("icon_xiao");
            //     break;
            case 2:
                Stage = GameDataManager.Instance.UserData.CurrentChessStage;
                //sprite = LoadheadIcon("icon_pinzi");
                break;
            case 3:
                Stage = GameDataManager.Instance.UserData.CurrentHexStage;
                //sprite = LoadheadIcon("icon_layer");
                break;
        }
        Stage=Stage==0?1:Stage;
        if (LimitTimeManager.Instance.IsComplete())
            _progressSlider.transform.parent.gameObject.SetActive(false);
        else
            _progressSlider.transform.parent.gameObject.SetActive(true);
       

        StartCoroutine(WaitTimeUpdate());
        StartCoroutine(PlayRewardSequence());
        
        StageHexController.Instance.CurLevelMode = GetLevelDifficulty(Stage);
        
        switch (StageHexController.Instance.CurLevelMode)
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
        _StageNumberText.text = MultilingualManager.Instance.GetString("Level")+" " + GameDataManager.Instance.UserData.CurrentHexStage;
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

    IEnumerator WaitTimeUpdate()
    {
        if (_progressSlider.value>=1)
        {
            if (LimitTimeManager.Instance.IsClaim())
            {
                SystemManager.Instance.ShowPanel(PanelType.LimitTimeScreen);
                yield break;
            }
            LimitDataItem limitData = LimitTimeManager.Instance.CurlimitData;
            _progressSlider.value = 0;
            _progressText.text = 0 + "/" + limitData.num;
        }

        if (_progressSlider.value <=0)
        {
            int wordcount = LimitTimeManager.Instance.GetCurWordCount();
            LimitDataItem limitData = LimitTimeManager.Instance.CurlimitData;
            if (limitData != null)
            {
                int oldProgress = wordcount-StageHexController.Instance.CurStageData.Puzzles.Count;
                oldProgress=Math.Max(oldProgress,0);
                _progressText.text = oldProgress + "/" + limitData.num;
            }
        }
        yield return new WaitForSeconds(1.5f);
        UpdateProgress();
    }
    
    private void UpdateProgress(bool isanim=true)
    {
        _progressSlider.transform.parent.gameObject.SetActive(true);
        int wordcount = LimitTimeManager.Instance.GetCurWordCount();
        LimitDataItem limitData = LimitTimeManager.Instance.CurlimitData;
        if (limitData == null) return;

        float durtime = !isanim?0f:0.8f;
        sliderProgress = (float)wordcount/limitData.num;    
        int oldProgress = wordcount-StageHexController.Instance.CurStageData.Puzzles.Count;
        oldProgress=Math.Max(oldProgress,0);
        wordcount=Math.Max(wordcount,0);
        _progressText.text = oldProgress + "/" + limitData.num;
        
        sliderProgress=Math.Max(sliderProgress,0.08f);

        if (isanim)
        {
            
            _progressSlider.DOValue(sliderProgress,durtime).OnComplete(() =>
            {
                _progressText.text = wordcount + "/" + limitData.num;
            });
        }
        else
        {
            _progressSlider.value = sliderProgress;
            _progressText.text = wordcount + "/" + limitData.num;
        }
    }
    
    IEnumerator ShowLimitTimeScreen()
    {
        yield return new WaitForSeconds(1.8f);
        
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
            _limitBtnTable.txtwordprogress.text = time; // 更新文本
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
        //_matchFishtable.matchEffect.gameObject.SetActive(false);
        // ==========================================
        // 🌟 新增：先播放莲花飞向禅意榜的动画！
        // ==========================================
        if (_zenRankBtn.gameObject.activeSelf)
        {
            yield return StartCoroutine(PlayZenLotusFlyAnim());
        }
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
        if (FishInfoController.Instance.IsShowFishProgressAnim()&&_matchFishtable.FishBtn.gameObject.activeSelf)
        {
            _matchFishtable.ShowFishWordAnim();
            StartCoroutine(UpdateFishRankUI());
            yield return new WaitForSeconds(1.2f);
        }

        //Animator.Play("ShowLevelBtn");
        
        StartCoroutine(ShowLimitTimeScreen());
        
        if (GameCoreManager.Instance.IsTrueAuto)
        {
            yield return new WaitForSeconds(1.2f);
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
        
        UpdateProgress(false);
        GameDataManager.Instance.UserData.ClearPuzzleVocabulary();
        base.OnDisable();
        EventDispatcher.instance.TriggerChangeGoldUI(AppGameSettings.LevelCompleteBonus, false);
        if (_treasureBoxEffect != null)
            _treasureBoxEffect.gameObject.SetActive(false);
    }
}