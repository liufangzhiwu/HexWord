using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

/**
 * 时间与刷新控制 (ChessPlayArea.Timer.cs)
 * 负责倒计时与 Update
 */
public partial class ChessPlayArea
{
    private void Update()
    {
        // 防抖：限制单帧最大时间流逝为 0.5 秒，防止切后台回来瞬间蒸发大量时间
        float dt = Mathf.Min(Time.deltaTime, 0.5f);
        // ==========================================
        // 🌟 1. 绝对防御：如果弹窗打开或在看广告，冻结一切！
        // ==========================================
        if (IsGamePausedByUI()) 
        {
            // 为了保证看广告回来后连击条不断，在暂停期间不断将连击时间戳后移，完美抵消流逝的时间
            if (ChessStageController.Instance.PuzzleComboCount > 0)
            {
                ChessStageController.Instance.LastCorrectWordTimestamp += dt;
            }
            return; // 结束执行，倒计时绝对不走！
        }
        // 2. 常规的开关检查 只有在计时器运行，且时间大于0时才倒计时
        if (!_isTimerRunning || _remainingTime <= 0) return;
        
        // ==========================================
        // 🌟 3. 真实活跃时间累加
        // ==========================================
        _remainingTime -= dt;
        CurrStageData.RemainingTime = Mathf.Max(0, _remainingTime);
        
        CurrStageData.TotalActiveSeconds += dt; // 关卡总活跃时长 (上报用)
        _currentWordActiveSeconds += dt;        // 单个词寻找时长 (断连击用)
   
        // --- 新增：累加卡关等待时间 ---
        if (_isStuckTimerRunning && !_hasTriggeredHintReminderThisLevel)
        {
            _stuckTimer += dt;
        }
        
        // ==========================================
        // 4. 警告与超时处理
        // ==========================================
        if (_remainingTime <= 60f && !_isWarningTriggered)
        {
            TriggerTimeWarning();
        }
        if (_remainingTime <= 0f)
        {
            _remainingTime = 0f;
            _isTimerRunning = false;
            bool isActuallyWon = chessboardGrid != null && 
                                 (chessboardGrid.GameOver || 
                                  (chessboardGrid.GridList.Count > 0 && 
                                   chessboardGrid.GridList.Values.All(item => item.CurrState == TileState.Success || item.IsOK)));
            if(!isActuallyWon)
                HandleTimeOut();
        }      
        UpdateTimerUI();
        
        // 5. 连击进度条检查
        if (ChessStageController.Instance.PuzzleComboCount > 0)
        {
            ChessStageController.Instance.CheckAndResetComboOnIdle();
            // float comboProgress = ChessStageController.Instance.GetComboTimeProgress();
            if (ChessStageController.Instance.PuzzleComboCount <= 0)
            {
                if (_comboScreenFX != null) _comboScreenFX.SetActive(false);
                if (chessboardGrid != null) chessboardGrid.hasPlayedComboSoundThisChain = false;
            }
        }
        if (ChessStageController.Instance.PuzzleComboCount <= 0)
        {
            if (_comboScreenFX != null && _comboScreenFX.activeSelf) 
            {
                _comboScreenFX.SetActive(false);
            }
            if (chessboardGrid != null) chessboardGrid.hasPlayedComboSoundThisChain = false;
        }
    }
      
    #region 计时与生命周期更新
    /// <summary>
    /// 🌟 核心拦截器：检查是否有阻挡计时的弹窗，或者广告正在播放
    /// </summary>
    private bool IsGamePausedByUI()
    {
        // 如果有任何遮挡屏幕的弹窗出现，时间停止！
        bool isPopShowing = 
            SystemManager.Instance.PanelIsShowing(PanelType.LevelWordScreen) ||   // 词典弹窗
            SystemManager.Instance.PanelIsShowing(PanelType.GetItemScreen) ||     // 道具购买弹窗
            SystemManager.Instance.PanelIsShowing(PanelType.ShopScreen) ||
            SystemManager.Instance.PanelIsShowing(PanelType.PauseGameScreen) ||   // 暂停弹窗
            SystemManager.Instance.PanelIsShowing(PanelType.FailGameScreen) ||    // 失败弹窗
            SystemManager.Instance.PanelIsShowing(PanelType.ContinueGameWindow) ||// 重连弹窗
            SystemManager.Instance.PanelIsShowing(PanelType.RateUsScreen);           // 评分弹窗
            
        // 完美涵盖看广告期间！广告SDK只要处于播放状态，时间停止！
        bool isAdPlaying = Game.self.Ads != null && Game.self.Ads.IsPlaying;
        
        return isPopShowing || isAdPlaying;
    }
  
    /// <summary>
    /// 🌟 玩家进行了任何有效操作（点击棋盘、词块、道具），尝试唤醒计时器
    /// </summary>
    public void NotifyPlayerInteraction()
    {
        // 1. 如果时间已经耗尽（GameOver状态），绝对不启动！防止狂点重跑
        if (_remainingTime <= 0f) return;
        
        // 2. 如果游戏已经结束，不启动
        if (chessboardGrid != null && chessboardGrid.GameOver) return;
        
        // 3. 如果当前有弹窗遮挡（如暂停、购买道具），不启动
        if (IsGamePausedByUI()) return;
        
        // 4. 如果已经在运行中，跳过
        if (_isTimerRunning) return;

        // 满足所有条件，正式启动计时！
        _isTimerRunning = true;
        // Debug.Log("🌟 玩家触发交互，倒计时正式开始！");
    }
    private void UpdateTimerUI()
    {
        if (_timerText == null) return;
        
        float safeTime = Mathf.Max(0, _remainingTime);
        int minutes = Mathf.FloorToInt(safeTime / 60F);
        int seconds = Mathf.FloorToInt(safeTime % 60f);
        _timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        
        bool hasLevelWords = ChessStageController.Instance.CurrStageData.FoundTargetPuzzles.Count > 0;
        // 只有当状态发生改变（从隐藏变成需要显示）时，才重新重置并播放动画
        if (hasLevelWords && !PuzzleBtn.gameObject.activeSelf)
        {
            PuzzleBtn.gameObject.SetActive(true);
            PuzzleBtn.interactable = true;
            CanvasGroup pbcg = PuzzleBtn.GetComponent<CanvasGroup>();
            if (pbcg != null)
            {
                pbcg.DOKill();
                pbcg.alpha = 0f;
                pbcg.DOFade(1f, 0.5f);
            }
        }
        else if (!hasLevelWords && PuzzleBtn.gameObject.activeSelf)
        {
            // 从显示变成隐藏
            PuzzleBtn.GetComponent<CanvasGroup>()?.DOKill();
            PuzzleBtn.gameObject.SetActive(false);
        }
    }
    
    /// <summary>
    /// 触发时间警告表现（红色背景 + 单次呼吸放大）
    /// </summary>
    private void TriggerTimeWarning()
    {
        _isWarningTriggered = true;
        if (gameTimeBg != null)
        {
            gameTimeBg.gameObject.SetActive(true);
            gameTimeBg.color = new Color(gameTimeBg.color.r, gameTimeBg.color.g, gameTimeBg.color.b, 1f);
            // 2. 杀掉旧动画，执行一次呼吸效果 (变大再缩回)
            gameTimeBg.transform.DOKill();
            gameTimeBg.DOFade(.15f, 1.5f).SetEase(Ease.InOutSine).SetLoops(4, LoopType.Yoyo)
                .OnComplete(()=>{ gameTimeBg.gameObject.SetActive(false); });
        }
    }
    
    #endregion
    
    /// <summary>
    /// 复活接口：看完广告后调用此方法重新开始
    /// </summary>
    /// <param name="addSeconds">复活额外给的秒数，默认给 60 秒</param>
    public void ReviveGame(float addSeconds = 60f)
    {
        _remainingTime += addSeconds;
        CurrStageData.RemainingTime = _remainingTime;
        _isWarningTriggered = false;
        
        // 恢复 UI 状态
        _timerText.color = Color.white; 
        _timerText.transform.DOKill(); 
        _timerText.transform.localScale = Vector3.one; 
        Outline outline = _timerText.GetComponent<Outline>();
        if (outline != null) outline.effectColor = new Color(0, 0, 0, 0.5f); // 假设你原本的描边是半透明黑色，按需修改
        
        ResetTimeWarning();
        UpdateTimerUI();
        _isTimerRunning = true; // 重新跑秒
        EventDispatcher.instance.TriggerChangeTopRaycast(true); // 解除屏幕屏蔽
    }
    
    /// <summary>
    /// 重置时间警告特效（文字变白、取消描边变红、停止心跳动画）
    /// </summary>
    private void ResetTimeWarning()
    {
        _isWarningTriggered = false;
        if (gameTimeBg != null)
        {
            // 停止动画，恢复缩放
            gameTimeBg.transform.DOKill();
            gameTimeBg.transform.localScale = new Vector3(0.9f,0.9f,0.9f);
            gameTimeBg.gameObject.SetActive(false);
        }
    }
    
    /// <summary>
    /// 刷新蝶蛹圆环进度
    /// </summary>
    /// <param name="currentScore">当前获得的总分</param>
    /// <param name="isInstant">是否瞬间刷满(不播动画，用于界面刚打开时)</param>
    public void UpdatePupaProgress(int currentScore, bool isInstant)
    {
        if (pupaObj.activeSelf)
        {
            int threshold = ButterfliesManager.Instance.GetScoreThresholdForPupa();
            
            // 🌟 核心机制：如果总分超过了阈值(比如拿了150分，阈值60)，取余数得出30，让进度条循环显示！
            // 防止除以0报错，且保留当 currentScore 正好等于 threshold 时，视觉上呈现满环
            float targetFill = Mathf.Clamp01((float)currentScore / threshold);
            
            Text progressText = pupaObj.GetComponentInChildren<Text>(true);
            bool isJustCompleted = (targetFill >= 1f && pupaProgressBar.fillAmount < 1f);
            if (targetFill < 1f) progressText.text = "+1"; 
            
            pupaProgressBar.DOKill();
            if (isInstant)
            {
                // 界面刚打开，瞬间设置，不播平滑动画
                pupaProgressBar.fillAmount = targetFill;
                progressText.gameObject.SetActive(targetFill >= 1f); // 满了才显示数字
                if (targetFill >= 1f) progressText.text = "+1";
            }
            else
            {
                // 游戏进行中加分，花 0.3 秒平滑过渡过去
                // 平滑动画赋值（游戏中途）
                pupaProgressBar.DOFillAmount(targetFill, 0.3f).SetEase(Ease.OutQuad).OnComplete(() => 
                {
                    progressText.gameObject.SetActive(targetFill >= 1f); // 动画涨满了才显示数字
                    
                    // 如果是这次才刚刚达标满格，触发发光粒子特效！
                    if (isJustCompleted)
                    {
                        progressText.text = "+1";
                        PlayPupaCompleteEffect();
                    }
                });
            }
        }
    }

}
