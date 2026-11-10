using System;
using System.Collections;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class BeginnerBonusEntryBtn : MonoBehaviour
{
    [Header("UI 组件")]
    [SerializeField] private Text countdownText;
    [SerializeField] private GameObject claimTagObj;  // 达成后的“领取”标签/文本节点 (例如写着“可领取”或红点)
    [SerializeField] private GameObject redPointObj;  // 可选：红点提示
    [Header("边框进度条")]
    [SerializeField] private Image progressBorderImg; // 挂载类型为 Filled 的 Image
    
    private Button _btn;
    private Coroutine _timerRoutine;
    private Tween _claimBreathTween;
    
    private void Awake()
    {
        _btn = GetComponent<Button>();
        _btn.AddClickAction(OnClick);
    }

    private void OnEnable()
    {
        // 监听活动领奖事件
        EventDispatcher.instance.OnBeginnerBonusClaimed += OnBonusClaimed;
        
        RefreshVisibility();
        if (gameObject.activeSelf)
        {
            // 确保显示在右侧最上/最前位置
            transform.SetAsFirstSibling();
            if (_timerRoutine != null) StopCoroutine(_timerRoutine);
            _timerRoutine = StartCoroutine(StatusUpdateRoutine());
        }
    }

    private void OnDisable()
    {
        // 取消监听
        EventDispatcher.instance.OnBeginnerBonusClaimed -= OnBonusClaimed;
        
        if (_timerRoutine != null)
        {
            StopCoroutine(_timerRoutine);
            _timerRoutine = null;
        }
        StopClaimAnimation();
    }

    public void RefreshVisibility()
    {
        var state = BeginnerBonusManager.Instance.GetCurrentState();
        // 只有未开启 (NotStarted) 或已超时 (Expired) 时才隐藏
        // Completed (未领奖) 和 InProgress (进行中) 都要显示
        bool isAlive = (state == BeginnerBonusState.InProgress || 
                        state == BeginnerBonusState.Start || 
                        state == BeginnerBonusState.Completed);
        gameObject.SetActive(isAlive);
        
        if (!isAlive)
        {
            StopClaimAnimation();
            return;
        }

        UpdateVisualByState(state);
        UpdateBorderProgress(false);
    }
    /// <summary>
    /// 领奖后被动销毁入口
    /// </summary>
    private void OnBonusClaimed()
    {
        StopClaimAnimation();
        gameObject.SetActive(false); // 立即隐藏/自我销毁
    }
    
    /// <summary>
    /// 根据当前状态切换显示：倒计时 VS 领取状态
    /// </summary>
    private void UpdateVisualByState(BeginnerBonusState state)
    {
        if (state == BeginnerBonusState.Completed)
        {
            // 🌟 已达成：隐藏倒计时，显示领取标识
            if (countdownText != null) countdownText.gameObject.SetActive(false);
            if (claimTagObj != null) claimTagObj.SetActive(true);
            if (redPointObj != null) redPointObj.SetActive(true);

            StartClaimAnimation();
        }
        else
        {
            // 进行中：显示倒计时，隐藏领取标识
            if (claimTagObj != null) claimTagObj.SetActive(false);
            if (redPointObj != null) redPointObj.SetActive(false);
            if (countdownText != null) countdownText.gameObject.SetActive(true);

            StopClaimAnimation();
        }
    }
    
    /// <summary>
    /// 刷新边框进度
    /// </summary>
    public void UpdateBorderProgress(bool playAnim = true)
    {
        if (progressBorderImg == null) return;

        // 1. 获取当前进度 (5~30)
        int current = BeginnerBonusManager.Instance.GetCurrentProgress();
        int target = BeginnerBonusManager.TARGET_STAGE; // 30

        // 2. 计算目标填充比例 (0.0f ~ 1.0f)
        float targetFill = Mathf.Clamp01((float)current / target);

        // 3. 执行动画或瞬间赋值
        progressBorderImg.DOKill();
        if (playAnim)
        {
            // 平滑涨条 0.5 秒
            progressBorderImg.DOFillAmount(targetFill, 0.5f).SetEase(Ease.OutQuad);
        }
        else
        {
            progressBorderImg.fillAmount = targetFill;
        }
    }
    /// <summary>
    /// 初始化起跑线刻度（显示上一关旧进度，不播动画）
    /// </summary>
    public void SetPreviousBorderProgress()
    {
        if (progressBorderImg == null) return;

        int current = BeginnerBonusManager.Instance.GetCurrentProgress();
        // 起跑线比当前少 1 关
        int prevProgress = Mathf.Max(BeginnerBonusManager.UNLOCK_STAGE, current - 1);
        float prevFill = Mathf.Clamp01((float)prevProgress / BeginnerBonusManager.TARGET_STAGE);

        progressBorderImg.DOKill();
        progressBorderImg.fillAmount = prevFill;
    }
    private IEnumerator StatusUpdateRoutine()
    {
        var wait = new WaitForSeconds(1f);
        while (gameObject.activeInHierarchy)
        {
            var state = BeginnerBonusManager.Instance.GetCurrentState();

            if (state == BeginnerBonusState.Completed)
            {
                UpdateVisualByState(state);
                yield return wait;
                continue;
            }

            TimeSpan remain = BeginnerBonusManager.Instance.GetRemainingTime();
            if (remain <= TimeSpan.Zero)
            {
                RefreshVisibility(); // 超时直接隐藏
                yield break;
            }

            if (countdownText != null)
            {
                countdownText.text = $"{remain.Hours:00}:{remain.Minutes:00}:{remain.Seconds:00}";
            }

            yield return wait;
        }
    }
    /// <summary>
    /// 可领取状态时的吸引点击动效 (如轻微呼吸跳动)
    /// </summary>
    private void StartClaimAnimation()
    {
        if (_claimBreathTween != null && _claimBreathTween.IsActive()) return;

        transform.localScale = Vector3.one;
        _claimBreathTween = transform.DOScale(1.1f, 0.45f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
    }
    private void StopClaimAnimation()
    {
        if (_claimBreathTween != null)
        {
            _claimBreathTween.Kill();
            _claimBreathTween = null;
        }
        transform.localScale = Vector3.one;
    }
    
    private void OnClick()
    {
        SystemManager.Instance.ShowPanel(PanelType.BeginnerBonusPanel);
    }
}