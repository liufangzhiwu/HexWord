using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class BeginnerBonusPanel : UIWindow
{
    [Header("顶部与倒计时")]
    [SerializeField] private Text timerText;
    [SerializeField] private Button closeBtn;

    [Header("进度与奖励展示")]
    [SerializeField] private Slider progressSlider;
    [SerializeField] private Text progressText;
    [SerializeField] private Text descText;

    [Header("核心按钮与扫光")]
    [SerializeField] private Button actionBtn;
    [SerializeField] private Text actionBtnText;
    
    private Coroutine _timerCoroutine;
    private Tween _sweepTween;

    protected override void InitializeUIComponents()
    {
        closeBtn.AddClickAction(OnCloseClicked);
        actionBtn.AddClickAction(OnActionClicked);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        UpdateView();
        StartTimer();
        EventDispatcher.instance.TriggerUpdateLayerCoin(true, false, false,true);
    }

    protected override void OnDisable()
    {
        if (_timerCoroutine != null)
        {
            StopCoroutine(_timerCoroutine);
            _timerCoroutine = null;
        }
        _sweepTween?.Kill();
        EventDispatcher.instance.TriggerUpdateLayerCoin(false, true, false);
        base.OnDisable();
    }

    private void UpdateView()
    {
        var state = BeginnerBonusManager.Instance.GetCurrentState();
        int progress = BeginnerBonusManager.Instance.GetCurrentProgress();
        int target = BeginnerBonusManager.TARGET_STAGE;
        
        float targetValue = Mathf.Clamp01((float)progress / target);
        progressSlider.DOKill(); // 清除旧动画防止鬼畜
        progressText.transform.DOKill();
        bool isFromFinish = GameCoreManager.Instance.PanelState == PanelState.FinishPingPanel;
        
        if (isFromFinish && progress > 0)
        {
            // 刚打完关卡：先把进度条强行退回到"通关前"的状态 (即减去本局打通的1关)
            int oldProgress = BeginnerBonusManager.Instance.GetPreviousMilestone(progress);
            float oldValue = Mathf.Clamp01((float)oldProgress / target);
            progressSlider.value = oldValue;
            progressText.text = $"{oldProgress}/{target}"; // 文本初始状态
            
            // 延迟 0.3 秒后（等面板彻底打开），花 0.6 秒平滑涨到最新进度
            progressSlider.DOValue(targetValue, 0.6f)
                .SetEase(Ease.OutQuad)
                .SetDelay(0.3f);
            // 文本数字同步平滑增加
            int tempProgress = oldProgress;
            DOTween.To(() => tempProgress, x => 
                {
                    tempProgress = x;
                    progressText.text = $"{tempProgress}/{target}";
                }, progress, 0.6f)
                .SetEase(Ease.OutQuad)
                .SetDelay(0.3f)
                .OnComplete(() => 
                {
                    // 数字跳到最新时，文本做一个“砰”的放大特效提示玩家
                    progressText.transform.localScale = Vector3.one;
                    progressText.transform.DOPunchScale(Vector3.one * 0.15f, 0.3f, 10, 1);
                });
        }
        else
        {
            // 从大厅主动点开：不需要动画，瞬间拉到真实进度
            progressSlider.value = targetValue;
            progressText.text = $"{progress}/{target}";
        }
        
        // 根据活动状态切换文案
        switch (state)
        {
            case BeginnerBonusState.Start:
                descText.text = MultilingualManager.Instance.GetString("BonusRule", "hudie");
                actionBtnText.text = MultilingualManager.Instance.GetString("JoinNow", "hudie");
                break;

            case BeginnerBonusState.InProgress:
                descText.text = MultilingualManager.Instance.GetString("BonusRule", "hudie");
                actionBtnText.text = MultilingualManager.Instance.GetString("JoinNow", "hudie");
                break;

            case BeginnerBonusState.Completed:
                descText.text = MultilingualManager.Instance.GetString("BonusaChieve", "hudie");
                actionBtnText.text = MultilingualManager.Instance.GetString("ReceiveAward", "hudie");
                break;

            default:
                actionBtnText.text = MultilingualManager.Instance.GetString("ContinueChallenge", "hudie");
                break;
        }
    }

    private void StartTimer()
    {
        if (_timerCoroutine != null) StopCoroutine(_timerCoroutine);
        _timerCoroutine = StartCoroutine(TimerRoutine());
    }

    private IEnumerator TimerRoutine()
    {
        var wait = new WaitForSeconds(1f);
        while (gameObject.activeInHierarchy)
        {
            TimeSpan remain = BeginnerBonusManager.Instance.GetRemainingTime();
            if (remain <= TimeSpan.Zero)
            {
                timerText.text = "00:00:00";
                UpdateView();
                yield break;
            }
            timerText.text = $"{remain.Hours:00}:{remain.Minutes:00}:{remain.Seconds:00}";
            yield return wait;
        }
    }
    
    private void OnCloseClicked()
    {
        SystemManager.Instance.HidePanel(PanelType.BeginnerBonusPanel);
        // 如果结算页面正在后台，会自动露出来
    }

    private void OnActionClicked()
    {
        var state = BeginnerBonusManager.Instance.GetCurrentState();

        if (state == BeginnerBonusState.Completed)
        {
            actionBtn.interactable = false; // 立即防重复连击
            BeginnerBonusManager.Instance.ClaimReward();
            CustomFlyInManager.Instance.FlyInGold(actionBtn.transform, () => {
                // 触发顶部货币栏更新
                EventDispatcher.instance.TriggerChangeGoldUI(BeginnerBonusManager.REWARD_GOLD, true);
            });
            EventDispatcher.instance.TriggerBeginnerBonusClaimed();
            DOVirtual.DelayedCall(0.3f, () =>
            {
                SystemManager.Instance.HidePanel(PanelType.BeginnerBonusPanel);
            });
            return;
        }

        // ==========================================
        // 点击“立即参与”或“继续闯关”时，必须先清理所有底层 UI,关闭弹窗并立即开始下一关
        // ==========================================
        
        // 1. 先把自己(新手活动弹窗)关掉
        SystemManager.Instance.HidePanel(PanelType.BeginnerBonusPanel);

        // 2. 如果是从【大厅/首页】点击进来的，隐藏大厅
        if (SystemManager.Instance.PanelIsShowing(PanelType.PrimaryInterface))
        {
            SystemManager.Instance.HidePanel(PanelType.PrimaryInterface);
        }
        
        // 3. 如果是从【结算页】刚通关弹出来的，隐藏结算页
        if (SystemManager.Instance.PanelIsShowing(PanelType.ChessFinishView))
        {
            // 只要这里隐藏了，结算页里的协程判定就会自动 yield break 安全退出
            SystemManager.Instance.HidePanel(PanelType.ChessFinishView);
        }
        
        // 4. 隐藏大厅和结算页共用的【顶部资源栏 (金币/体力等)】
        if (SystemManager.Instance.PanelIsShowing(PanelType.HeaderSection))
        {
            SystemManager.Instance.HidePanel(PanelType.HeaderSection);
        }
        
        // 5. 环境清理干净后，正式加载并进入下一关
        int nextStage = GameDataManager.Instance.UserData.CurrentChessStage;
        ChessStageController.Instance.SetStageData(nextStage);
        SystemManager.Instance.ShowPanel(PanelType.ChessPlayArea);
    }
}
