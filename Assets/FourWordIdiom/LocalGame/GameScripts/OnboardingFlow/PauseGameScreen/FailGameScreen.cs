using System.Collections;
using System.Collections.Generic;
using Middleware;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FailGameScreen : UIWindow, IPointerDownHandler, IPointerUpHandler
{
    [Header("面板节点")]
    [SerializeField] private GameObject failPanel;
    [SerializeField] private GameObject continuePanel;
    [Header("主失败界面按钮")]
    [SerializeField] private Button addTimeBtn;
    [SerializeField] private Button exitBtn;
    [SerializeField] private Text reviveBtnText; 
    [SerializeField] private GameObject adIcon;
    [SerializeField] private Text timeText;    // 上局时间

    [Header("二次确认界面按钮")]
    [SerializeField] private Text conTitleText;
    [SerializeField] private Text conTipText;
    [SerializeField] private Button realExitBtn;
    [SerializeField] private Button returnGame;
    [SerializeField] private GameObject confirmPupaIcon;
    
    private CanvasGroup _canvasGroup;
    private Coroutine _longPressCoroutine;
    private bool _isHiddenByLongPress = false;
    
    // Start is called before the first frame update
    private void Start()
    {
        addTimeBtn.AddClickAction(OnReviveClicked);
        exitBtn.AddClickAction(() => SwitchPanel(false));
        returnGame.AddClickAction(() => SwitchPanel(true));
        realExitBtn.AddClickAction(OnRealExitClicked);
        
        conTitleText.text = MultilingualManager.Instance.GetString("AreYouSure");
        conTipText.text = MultilingualManager.Instance.GetString("YouWillLose");
        realExitBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("ConfirmExit");
        returnGame.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("LetMeThink");
        
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

    }
    
    protected override void OnEnable()
    {
        base.OnEnable();
        SwitchPanel(true); // 每次打开弹窗，强制显示主失败页
        RefreshReviveUI(); // 刷新复活按钮的UI表现 (是否免费)
        RefreshPupaIconVisibility();
        AnalyticMgr.LevelFailed();
        
        ChessStageController.Instance.CurrStageData.IsPausedOrFailed = true;
        GameDataManager.Instance.CommitGameData();
    }
    /// <summary>
    /// 面板切换助手
    /// </summary>
    private void SwitchPanel(bool showFailPanel)
    {
        float rawTime = ChessStageController.Instance.CurrStageData.RemainingTime;
        float safeTime = Mathf.Max(0, rawTime);
        int minutes = Mathf.FloorToInt(safeTime / 60F);
        int seconds = Mathf.FloorToInt(safeTime % 60f);
        timeText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        timeText.gameObject.SetActive(showFailPanel);
        
        if (failPanel != null) failPanel.SetActive(showFailPanel);
        if (continuePanel != null) continuePanel.SetActive(!showFailPanel);
        
        EventDispatcher.instance.TriggerHighlightHeaderUI(!showFailPanel);
    }
    
    /// <summary>
    /// 刷新复活按钮表现（免费还是看广告）
    /// </summary>
    private void RefreshReviveUI()
    {
        bool isFree = !GameDataManager.Instance.UserData.hasUsedFreeRevive;
        
        // TODO: 如果你想改变按钮上的文字或隐藏视频图标，可以在这里写
        if (isFree) {
            reviveBtnText.text = "免费复活";
            adIcon.SetActive(false);
        } else {
            reviveBtnText.text = "复活";
            adIcon.SetActive(true);
        }
    }
    
    /// <summary>
    /// 🌟 点击复活按钮：判断是免费还是看视频
    /// </summary>
    private void OnReviveClicked()
    {
        bool isFree = !GameDataManager.Instance.UserData.hasUsedFreeRevive;

        if (isFree)
        {
            Debug.Log("使用首次免费复活！");
            // 标记已使用并保存存档
            GameDataManager.Instance.UserData.hasUsedFreeRevive = true;
            
            // 执行复活
            ExecuteRevive();
        }
        else
        {
            Debug.Log("免费次数已用完，准备播放激励视频广告...");
            AnalyticMgr.VideoAdClick("复活广告");
            // 播放激励视频 (根据你项目实际的 Ads API 调用)
            AdRuleManager.Instance.TryShowRewardVideo(Define.AdKey.RewardAdIdStoreGold, success =>
            {
                if (success)
                {
                    // 广告播放成功
                    AnalyticMgr.VideoAdSuccess("复活广告"); 
                    ExecuteRevive();
                }
                else
                {
                    // 广告未看完或加载失败
                    MessageSystem.Instance.ShowTip("广告未播放完成，无法复活！");
                    AnalyticMgr.VideoAdFail("复活广告");
                }
            });
        }
    }
    /// <summary>
    /// 执行真正的复活并返回游戏
    /// </summary>
    private void ExecuteRevive()
    {
        SystemManager.Instance.HidePanel(PanelType.FailGameScreen);
        ChessStageController.Instance.CurrStageData.IsPausedOrFailed = false;
        GameDataManager.Instance.CommitGameData();
        ChessPlayArea.Instance.ReviveGame(60f);
    }
    /// <summary>
    /// 🌟 点击真正的退出按钮：扣分、扣体力、删存档
    /// </summary>
    private void OnRealExitClicked()
    {
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
        SystemManager.Instance.HidePanel(PanelType.FailGameScreen);
        ChessGuideSystem.Instance.CloseGuide();
    }
    /// <summary>
    /// 根据当前关卡能力，刷新蝶蛹图标的显示
    /// </summary>
    private void RefreshPupaIconVisibility()
    {
        if (confirmPupaIcon == null) return;
        
        // 1. 获取玩家当前的实时得分
        int currentScore = ChessStageController.Instance.CurrentTotalScore;
        
        // 2. 获取获得一个蝶蛹所需的积分门槛 (阈值)
        int threshold = ButterfliesManager.Instance.GetScoreThresholdForPupa();
        
        // 3. 判断当前分数是否已经够换至少一个蝶蛹
        bool currentlyHasPupa = currentScore >= threshold;

        // 4. 获取关卡理论最高分，判断关卡本身是否有资格产出
        int optimalScore = ChessStageController.Instance.OptimalTotalScore;
        bool canLevelProduce = ButterfliesManager.Instance.CanShowPupaProgressBarThisLevel(optimalScore);

        // 🌟 核心逻辑修复：
        // 只有关卡有资格产出，且玩家当前得分已经【实实在在】换到了蝶蛹，才显示图标
        bool shouldShow = canLevelProduce && currentlyHasPupa;
        confirmPupaIcon.SetActive(shouldShow);
    }
    // ==========================================
    // 🌟 4. 长按隐藏逻辑核心实现
    // ==========================================
    public void OnPointerDown(PointerEventData eventData)
    {
        // 当玩家手指按下弹窗的空白背景时触发 (点在按钮上会被按钮拦截，不会触发这里)
        if (_longPressCoroutine != null) StopCoroutine(_longPressCoroutine);
        _longPressCoroutine = StartCoroutine(LongPressRoutine());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // 松手时，立刻停止计时器
        if (_longPressCoroutine != null)
        {
            StopCoroutine(_longPressCoroutine);
            _longPressCoroutine = null;
        }

        // 如果弹窗之前已经被隐藏了，现在松手要让它重新出现
        if (_isHiddenByLongPress)
        {
            _isHiddenByLongPress = false;
            _canvasGroup.alpha = 1f; // 透明度恢复为1
            
            // 如果是在"二次确认界面"长按的，松手后要顺便恢复头部体力的提层高亮
            if (continuePanel.activeSelf) 
            {
                EventDispatcher.instance.TriggerHighlightHeaderUI(true);
            }
        }
    }
    
    private IEnumerator LongPressRoutine()
    {
        yield return new WaitForSeconds(0.25f); // 🌟 按住 0.25 秒算作长按，触发隐藏
        
        _isHiddenByLongPress = true;
        _canvasGroup.alpha = 0f; // 透明度变为0（弹窗消失，但依旧会拦截点击穿透）
        
        // 临时取消头部高亮，防止顶部UI挡住棋盘的视线
        EventDispatcher.instance.TriggerHighlightHeaderUI(false);
    }
    protected override void OnDisable()
    {
        // 🌟 我关闭了，通知头部恢复原状！
        EventDispatcher.instance.TriggerHighlightHeaderUI(false);
        base.OnDisable();
    }
}
