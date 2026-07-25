using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ContinueGameWindow : UIWindow, IPointerDownHandler, IPointerUpHandler
{
    [Header("面板节点")]
    [SerializeField] private GameObject mainPanel;      // 主提示界面
    [SerializeField] private GameObject confirmPanel;   // 二次确认退出界面
    
    [Header("主界面按钮")]
    [SerializeField] private Text titleText;
    [SerializeField] private Image _snapshotImage;
    [SerializeField] private Text tipText;
    [SerializeField] private Button continueBtn; // 继续游戏按钮
    [SerializeField] private Button quitBtn;     // 退出/重新开始按钮
    [SerializeField] private Text timeText;    // 上局时间
    
    [Header("二次确认界面按钮")]
    [SerializeField] private Text conTitleText;
    [SerializeField] private Text conTipText;
    [SerializeField] private Button realQuitBtn;    // 真的要退出
    [SerializeField] private Button cancelQuitBtn;  // 取消退出，回到主面板
    [SerializeField] private GameObject confirmPupaIcon;
    
    private Action _onContinue;
    private Action _onQuit;
    private CanvasGroup _canvasGroup;
    private Coroutine _longPressCoroutine;
    private bool _isHiddenByLongPress = false;
    
    protected override void InitializeUIComponents()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        
        // 主面板：继续游戏
        continueBtn.AddClickAction(() => 
        {
            _onContinue?.Invoke();
            SystemManager.Instance.HidePanel(PanelType.ContinueGameWindow);
            ChessStageController.Instance.CurrStageData.IsPausedOrFailed = false;
            GameDataManager.Instance.CommitGameData();
        });

        // 主面板：点击放弃，弹出二次确认
        quitBtn.AddClickAction(() => 
        {
            mainPanel.SetActive(false);
            confirmPanel.SetActive(true);
            timeText.gameObject.SetActive(false);
            EventDispatcher.instance.TriggerHighlightHeaderUI(true);
        });

        // 二次确认：真的退出
        realQuitBtn.AddClickAction(() => 
        {
           
            SystemManager.Instance.HidePanel(PanelType.ContinueGameWindow);
            SystemManager.Instance.HidePanel(PanelType.HeaderSection, true, () =>
            {
                EventDispatcher.instance.TriggerHighlightHeaderUI(false);
                _onQuit?.Invoke();
            });
            if (ChessStageController.Instance.CurrStageData != null && 
                ChessStageController.Instance.CurrStageData.FoundTargetPuzzles.Count > 0)
            {
                AnalyticMgr.LevelExit();
            }
            ChessGuideSystem.Instance.CloseGuide();
        });

        // 二次确认：取消退出，退回主面板
        cancelQuitBtn.AddVibraClickAction(() => 
        {
            mainPanel.SetActive(true);
            confirmPanel.SetActive(false);
            timeText.gameObject.SetActive(true);
            EventDispatcher.instance.TriggerHighlightHeaderUI(false);
        });
        
        titleText.text = MultilingualManager.Instance.GetString("ContinueLevel","pingzi");
        continueBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("Continue","pingzi");
        quitBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("Quit","pingzi");
        
        conTitleText.text = MultilingualManager.Instance.GetString("YouSure","pingzi");
        conTipText.text = MultilingualManager.Instance.GetString("WillLose","pingzi");
        realQuitBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("ContinueQuit","pingzi");
        cancelQuitBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("ThinkAgain","pingzi");
    }
    protected override void OnEnable()
    {
        base.OnEnable();
        mainPanel.SetActive(true);
        confirmPanel.SetActive(false);
        // 🌟 我打开了，通知头部把体力和蝶蛹提层亮出来！
        // 🌟 玩家点开了暂停，视为进入“嫌疑状态”，立即存入硬盘！
        ChessStageController.Instance.CurrStageData.IsPausedOrFailed = true;
        GameDataManager.Instance.CommitGameData();
       
    }
    /// <summary>
    /// 动态传入回调事件
    /// </summary>
    public void Init(float remainTime,Sprite boardSnapshot,Action onContinue, Action onQuit)
    {
        _onContinue = onContinue;
        _onQuit = onQuit;
        // 🌟 将照片赋给 UI
        if (_snapshotImage != null && boardSnapshot != null)
        {
            _snapshotImage.sprite = boardSnapshot;
            // 保持图片的比例不被拉伸
            _snapshotImage.preserveAspect = true; 
        }
        UpdateRemainingTimeUI(remainTime);
        RefreshPupaIconVisibility();
    }
    private void UpdateRemainingTimeUI(float remainTime)
    {
        timeText.gameObject.SetActive(true);
        if (timeText == null) return;

        int minutes = Mathf.FloorToInt(remainTime / 60F);
        int seconds = Mathf.FloorToInt(remainTime - minutes * 60);
        timeText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        
        // 调试用，确保数据传进来了
        Debug.Log($"[ContinueWindow] 初始化剩余时间: {timeText.text}");
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
            if (confirmPanel.activeSelf) 
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
        if (_snapshotImage != null && _snapshotImage.sprite != null)
        {
            Destroy(_snapshotImage.sprite.texture);
            Destroy(_snapshotImage.sprite);
            _snapshotImage.sprite = null;
        }
        // 🌟 我关闭了，通知头部恢复原状！
        EventDispatcher.instance.TriggerHighlightHeaderUI(false);
        base.OnDisable();
    }
}
