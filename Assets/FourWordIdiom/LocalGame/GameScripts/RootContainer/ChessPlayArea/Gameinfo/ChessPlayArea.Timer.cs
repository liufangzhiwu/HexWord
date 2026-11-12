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
    // ======================= 倒计时提醒参数 =======================
    /// <summary>剩余 1 分钟的时间文字颜色 #FF3737</summary>
    private static readonly Color TimeWarningColor = new Color32(255, 55, 55, 255);
    /// <summary>最后 10 秒的时间文字颜色（更深一档）</summary>
    private static readonly Color TimeDangerColor = new Color32(255, 0, 0, 255);
    /// <summary>倒计时提醒音效（闹钟）</summary>
    private const string ClockSfxName = "clock";
    /// <summary>时间到红屏呼吸的透明度下限 30/255</summary>
    private const float TimeUpRedAlphaMin = 30f / 255f;
    /// <summary>时间到：四面泛红呼吸时长</summary>
    private const float TimeUpRedDuration = 1f;
    /// <summary>时间到：时间数字放大呼吸时长</summary>
    private const float TimeUpTextBreathDuration = 1f;
    /// <summary>横幅出现后多久弹失败界面</summary>
    private const float TimeUpFailDelay = 2.5f;
    /// <summary>1 分钟提醒音效 clock.ogg 的时长，红屏闪烁与文字晃动都跟着它走完全程</summary>
    private const float ClockSfxDuration = 2.5f;
    /// <summary>提醒动效分段数（6 段 = 3 次完整呼吸，单段约 0.42s）</summary>
    private const int WarningBreathLoops = 6;
    private static float WarningBreathStep => ClockSfxDuration / WarningBreathLoops;
    /// <summary>提醒时时间文字的放大峰值</summary>
    private const float WarningTextPeakScale = 1.18f;
    /// <summary>「时间到！」横幅停留时长（加上进出场约 5s）</summary>
    private const float TimeUpBannerHold = 4.6f;

    /// <summary>最后 10 秒提醒是否已触发</summary>
    private bool _isDangerTriggered;
    /// <summary>时间到红屏（ChessPlayArea/TimeNoEnough）</summary>
    private Image _timeNoEnoughRed;
    /// <summary>1 分钟闪红的呼吸 Tween（时间到时需要先掐掉，避免它把 5s 循环复位）</summary>
    private Tween _warnRedTween;
    /// <summary>「时间到！」横幅实例（复活时需要提前收掉）</summary>
    private GameObject _timeUpBanner;

    private Coroutine _emojiTipCoroutine;
    private int _lastDisplayedSeconds = -1;
    private Image _pupaIcon;

    /// <summary>
    /// 是否处于「不限时」状态（复活后开启）：
    /// 开启后时间不再流逝、不弹 1 分钟/10 秒提醒、也不会再因时间耗尽失败；
    /// 只在当前这一局内有效，退出关卡 / 重新进关 / 重开游戏都回到原来的倒计时逻辑。
    /// </summary>
    private bool _isUnlimitedTime;
    /// <summary>外部（暂停界面等）查询当前是否处于不限时状态</summary>
    public bool IsUnlimitedTime => _isUnlimitedTime;
    /// <summary>不限时状态下的展示文案（复活弹窗、暂停界面共用）</summary>
    public const string UnlimitedTimeText = "不限时";

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

        // ==========================================
        // 🌟 1.5 不限时（复活后）：不倒数、不提醒、不超时，
        //      但连击判定、破冰等其它局内逻辑照常跑
        // ==========================================
        if (_isUnlimitedTime)
        {
            // 通关 / 结算阶段计时器会被置为停止，此时局内逻辑（连击、破冰）也一起停
            if (!_isTimerRunning) return;

            // 时间不流逝，但活跃时长 / 卡关时长这些上报与统计照常累加
            CurrStageData.TotalActiveSeconds += dt;
            _currentWordActiveSeconds += dt;
            if (_isStuckTimerRunning && !_hasTriggeredHintReminderThisLevel)
            {
                _stuckTimer += dt;
            }

            UpdateComboState();
            UpdateIceBreakTimer(dt);
            return;
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
        // 最后 10 秒优先（如果关卡一进来就只剩 10 秒，不要和 1 分钟提醒叠着播两遍）
        if (_remainingTime <= 10f && !_isDangerTriggered)
        {
            TriggerTimeDanger();
        }
        else if (_remainingTime <= 60f && !_isWarningTriggered)
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
        UpdateComboState();
        UpdateIceBreakTimer(dt);
    }

    /// <summary>
    /// 连击进度条检查（倒计时正常 / 不限时两种状态都要跑，所以抽出来）
    /// </summary>
    private void UpdateComboState()
    {
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
        _hasPlayerOperatedThisLevel = true;
    }
    private void UpdateTimerUI()
    {
        if (_timerText == null) return;
        // 不限时状态下界面不显示倒计时，不需要刷新文本
        if (_isUnlimitedTime) return;
        
        float safeTime = Mathf.Max(0, _remainingTime);
        int totalSeconds = Mathf.CeilToInt(safeTime);
        if (totalSeconds == _lastDisplayedSeconds) return;
        
        _lastDisplayedSeconds = totalSeconds;
        int minutes = totalSeconds / 60;
        int seconds = _lastDisplayedSeconds % 60;
        // _timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        _timerText.text = $"{minutes:00}:{seconds:00}";
        
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
    /// 倒计时进入 1 分钟：时间文字变红 + 放大动效 + 音效（原有背景呼吸保留）
    /// </summary>
    private void TriggerTimeWarning()
    {
        _isWarningTriggered = true;

        SetTimerTextColor(TimeWarningColor);
        PlayWarningTimerShake();
        AudioManager.Instance.PlaySoundEffect(ClockSfxName);

        // ★ 倒计时音效响起的同时，四周泛红跟着音效闪完全程（与 0 秒那套同一个节点）
        PlayTimeUpRedPulse();

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

    /// <summary>
    /// 最后 10 秒：和 1 分钟提醒同一套表现（文字晃动 + 红屏闪烁 + 闹钟音效），
    /// 只是文字用更深的红，强调"马上要结束了"。
    /// </summary>
    private void TriggerTimeDanger()
    {
        _isDangerTriggered = true;
        // 直接进入 10 秒档时，避免 1 分钟那次再补一遍
        _isWarningTriggered = true;

        SetTimerTextColor(TimeDangerColor);
        PlayWarningTimerShake();
        AudioManager.Instance.PlaySoundEffect(ClockSfxName);
        PlayTimeUpRedPulse();
    }

    private void SetTimerTextColor(Color color)
    {
        if (_timerText != null) _timerText.color = color;
    }

    #endregion

    #region 时间到表现（红屏呼吸 → 闹钟音效 → 「时间到！」横幅）

    /// <summary>
    /// 时间到整套演出：
    /// 0~1s：时间数字呼吸 + 四周泛红呼吸 + 闹钟音效；
    /// 横幅立刻出现、总共停留约 5s；横幅出现约 2.5s 后由调用方弹失败界面。
    /// </summary>
    private IEnumerator PlayTimeUpSequence()
    {
        // 0. 时间数字变色 + 1s 放大呼吸
        SetTimerTextColor(TimeDangerColor);
        PlayTimeUpTextBreath(TimeUpTextBreathDuration);

        // 1. 全屏红屏呼吸（TimeNoEnough，#FF3737，约 1s）
        PlayTimeUpRedScreen(TimeUpRedDuration);

        // 2. 闹钟音效
        AudioManager.Instance.PlaySoundEffect(ClockSfxName, 0, 0.35f);

        // 3. 「时间到！」横幅：与红屏并行，停留约 5s
        StartCoroutine(ShowTimeUpBanner());

        // 4. 横幅显示约 2.5s 后再弹失败界面
        yield return new WaitForSeconds(TimeUpFailDelay);
    }

    /// <summary>时间到那一刻，时间数字的放大呼吸（1s）</summary>
    private void PlayTimeUpTextBreath(float duration)
    {
        if (_timerText == null) return;

        _timerText.transform.DOKill();
        _timerText.transform.localScale = Vector3.one;

        // duration 内做 2 次完整呼吸：4 段 yoyo
        float step = Mathf.Max(0.05f, duration / 4f);
        _timerText.transform.DOScale(1.25f, step)
            .SetEase(Ease.InOutSine)
            .SetLoops(4, LoopType.Yoyo)
            .OnComplete(() =>
            {
                if (_timerText != null) _timerText.transform.localScale = Vector3.one;
            });
    }

    /// <summary>
    /// 全屏红色呼吸：使用 ChessPlayArea/TimeNoEnough。
    /// 该节点的贴图「红色闪红」本身就是"四周泛红"的暗角图（中心透明、边缘最高 alpha≈139），
    /// 按美术本意只做暗角闪红、不铺整屏红底，所以这里只驱动它自己的 alpha：30/255 ↔ 255/255。
    /// </summary>
    private void PlayTimeUpRedScreen(float duration)
    {
        Image red = GetTimeNoEnoughImage();
        if (red == null) return;

        // 1 分钟那次闪红如果还没播完，先掐掉，避免它的 OnComplete 把这次的循环提前复位
        if (_warnRedTween != null)
        {
            _warnRedTween.Kill();
            _warnRedTween = null;
        }

        red.gameObject.SetActive(true);
        // 顶到最上层：白屏/遮罩/玩法元素都排在它前面，不顶上来会被盖住
        red.transform.SetAsLastSibling();
        red.raycastTarget = false;
        red.DOKill();

        // 保留贴图原色（它本身是红的），只动透明度
        Color c = red.color;
        c.a = TimeUpRedAlphaMin;
        red.color = c;

        Debug.Log($"[ChessPlayArea] 时间到红屏起播: node={red.name}, active={red.gameObject.activeInHierarchy}, " +
                  $"alpha={c.a:F3}");

        // duration 内跑 2 次完整呼吸（4 段 yoyo）；1s 档约 0.25s 一段
        int loops = duration >= 3f ? 6 : 4;
        float step = Mathf.Max(0.08f, duration / loops);
        red.DOFade(1f, step)
            .SetEase(Ease.InOutSine)
            .SetLoops(loops, LoopType.Yoyo)
            .OnComplete(() =>
            {
                // 呼吸结束再淡出，避免突然消失
                red.DOFade(0f, 0.15f).OnComplete(() => ResetTimeUpRedScreen());
            });
    }

    /// <summary>
    /// 1 分钟提醒时时间文字的"晃动"：跟着 clock 音效全程呼吸放大（1 ↔ 1.18），
    /// 与四周泛红同一节奏，音效放完才停。
    /// </summary>
    private void PlayWarningTimerShake()
    {
        if (_timerText == null) return;

        _timerText.transform.DOKill();
        _timerText.transform.localScale = Vector3.one;
        _timerText.transform.DOScale(WarningTextPeakScale, WarningBreathStep)
            .SetEase(Ease.InOutSine)
            .SetLoops(WarningBreathLoops, LoopType.Yoyo)
            .OnComplete(() =>
            {
                if (_timerText != null) _timerText.transform.localScale = Vector3.one;
            });
    }

    /// <summary>
    /// 四周泛红"闪一下"：1 分钟提醒音效响起时用，跟着音效全程呼吸（30/255 ↔ 255/255），约 2.5s。
    /// 复用同一个 TimeNoEnough 节点，与 0 秒那套 5s 循环互斥（起播前会 Kill 掉前一段）。
    /// </summary>
    private void PlayTimeUpRedPulse()
    {
        Image red = GetTimeNoEnoughImage();
        if (red == null) return;

        red.gameObject.SetActive(true);
        red.transform.SetAsLastSibling();
        red.raycastTarget = false;
        red.DOKill();

        Color c = red.color;
        c.a = TimeUpRedAlphaMin;
        red.color = c;

        _warnRedTween = red.DOFade(1f, WarningBreathStep)
            .SetEase(Ease.InOutSine)
            .SetLoops(WarningBreathLoops, LoopType.Yoyo)
            .OnComplete(() =>
            {
                // 呼吸结束时停在 30/255，再淡到 0 收尾，避免一下跳没
                red.DOFade(0f, 0.15f).OnComplete(ResetTimeUpRedScreen);
            });
    }

    /// <summary>缓存 TimeNoEnough 节点（找不到就返回 null，不影响主流程）</summary>
    private Image GetTimeNoEnoughImage()
    {
        if (_timeNoEnoughRed != null) return _timeNoEnoughRed;

        Transform t = transform.Find("TimeNoEnough");
        if (t == null)
        {
            // 兜底：层级被调整过时，递归找同名节点
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == "TimeNoEnough") { t = all[i]; break; }
            }
        }

        if (t == null)
        {
            Debug.LogWarning("[ChessPlayArea] 未找到 TimeNoEnough，跳过时间到红屏呼吸");
            return null;
        }

        _timeNoEnoughRed = t.GetComponent<Image>();
        if (_timeNoEnoughRed == null)
            Debug.LogWarning("[ChessPlayArea] TimeNoEnough 上没有 Image 组件");

        return _timeNoEnoughRed;
    }

    /// <summary>复位红屏（复活 / 换关时调用），避免残留在屏幕上</summary>
    private void ResetTimeUpRedScreen()
    {
        if (_warnRedTween != null)
        {
            _warnRedTween.Kill();
            _warnRedTween = null;
        }

        if (_timeNoEnoughRed == null) return;

        _timeNoEnoughRed.DOKill();

        Color c = _timeNoEnoughRed.color;
        c.a = 0f;
        _timeNoEnoughRed.color = c;
        _timeNoEnoughRed.gameObject.SetActive(false);
    }

    /// <summary>
    /// 「时间到！」横幅：运行时拼一个 UI，避免依赖预制体。
    /// 底图取 Resources/TimeUp/timeup_banner，取不到就退化成纯色块。
    /// </summary>
    private IEnumerator ShowTimeUpBanner()
    {
        // 上一次的横幅还在就直接收掉，避免叠出两条
        DestroyTimeUpBanner();

        GameObject root = new GameObject("TimeUpBanner", typeof(RectTransform), typeof(CanvasGroup));
        _timeUpBanner = root;
        RectTransform rootRT = root.GetComponent<RectTransform>();
        rootRT.SetParent(transform, false);
        rootRT.SetAsLastSibling();          // 盖在玩法界面最上层
        rootRT.anchorMin = rootRT.anchorMax = new Vector2(0.5f, 0.5f);
        rootRT.pivot = new Vector2(0.5f, 0.5f);
        rootRT.sizeDelta = new Vector2(820f, 208f);   // 底图 1023x259，按比例收缩
        Vector2 centerPos = new Vector2(0f, 120f);

        CanvasGroup cg = root.GetComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        // 底图
        GameObject bgObj = new GameObject("bg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform bgRT = bgObj.GetComponent<RectTransform>();
        bgRT.SetParent(rootRT, false);
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;

        Image bg = bgObj.GetComponent<Image>();
        Sprite bannerSprite = LoadTimeUpBannerSprite();
        if (bannerSprite != null)
        {
            bg.sprite = bannerSprite;
            bg.type = Image.Type.Simple;
            bg.preserveAspect = true;
        }
        else
        {
            // 兜底：没有图就画一块同色圆角底
            bg.color = TimeWarningColor;
        }
        bg.raycastTarget = false;

        // 文案
        GameObject txtObj = new GameObject("txt", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        RectTransform txtRT = txtObj.GetComponent<RectTransform>();
        txtRT.SetParent(rootRT, false);
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero;
        txtRT.offsetMax = Vector2.zero;

        Text txt = txtObj.GetComponent<Text>();
        txt.font = _timerText != null ? _timerText.font : null;   // 复用计时器字体，保证有字形
        txt.fontSize = 64;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.text = GetTimeUpText();
        txt.raycastTarget = false;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;

        // 入场：从上方落下
        rootRT.anchoredPosition = centerPos + new Vector2(0f, 140f);
        cg.DOFade(1f, 0.18f);
        rootRT.DOAnchorPos(centerPos, 0.26f).SetEase(Ease.OutBack);

        yield return new WaitForSeconds(TimeUpBannerHold);

        // 退场：向上飘走并淡出
        cg.DOFade(0f, 0.18f);
        rootRT.DOAnchorPos(centerPos + new Vector2(0f, 70f), 0.2f).SetEase(Ease.InQuad);

        yield return new WaitForSeconds(0.22f);

        cg.DOKill();
        rootRT.DOKill();
        if (_timeUpBanner == root) _timeUpBanner = null;
        Destroy(root);
    }

    /// <summary>提前收掉「时间到！」横幅（复活时用，避免横幅飘在局内）</summary>
    private void DestroyTimeUpBanner()
    {
        if (_timeUpBanner == null) return;

        Destroy(_timeUpBanner);
        _timeUpBanner = null;
    }

    /// <summary>「时间到！」文案（缺多语言时兜底中文）</summary>
    private static string GetTimeUpText()
    {
        string text = MultilingualManager.Instance != null
            ? MultilingualManager.Instance.GetString("TimeUp", "hudie")
            : null;
        if (string.IsNullOrEmpty(text) || text == "TimeUp")
            text = "时间到！";
        return text;
    }

    private static Sprite _timeUpBannerSprite;

    /// <summary>
    /// 加载横幅底图。用 Texture2D + Sprite.Create 而不是直接 Load&lt;Sprite&gt;，
    /// 这样即使图片按 Default Texture 导入也能用。
    /// </summary>
    private static Sprite LoadTimeUpBannerSprite()
    {
        if (_timeUpBannerSprite != null) return _timeUpBannerSprite;

        Texture2D tex = Resources.Load<Texture2D>("TimeUp/timeup_banner");
        if (tex == null)
        {
            Debug.LogWarning("[ChessPlayArea] 未找到 Resources/TimeUp/timeup_banner，时间到横幅使用纯色底");
            return null;
        }

        _timeUpBannerSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        return _timeUpBannerSprite;
    }

    #endregion
    
    /// <summary>
    /// 复活接口：看完广告后调用此方法重新开始。
    /// ★ 复活后进入「不限时」状态：不再倒计时、界面不显示倒计时，也不会再因时间耗尽失败。
    /// </summary>
    /// <param name="fallbackSeconds">
    /// 内部保留的兜底秒数（默认 60 秒）。仅用于存档 / 防逃逸判定 / 暂停存档，
    /// 界面上不显示也不会流逝 —— 复活后不会再触发时间到。
    /// </param>
    /// <param name="autoUseGrantedButterfly">
    /// 本次复活是否赠送了蝴蝶（FailGameScreen.GrantFreeButterToolIfNeeded 的结果）。
    /// 为 true 且场上空位 &gt; 5 时，立刻把这只蝴蝶飞出去提示（bug47）。
    /// </param>
    public void ReviveGame(float fallbackSeconds = 60f, bool autoUseGrantedButterfly = false)
    {
        // ★ 不限时：内部仍留一份有限的剩余时间，保证存档 / 暂停 / 防逃逸判定还走原来的逻辑
        //   （退出关卡依旧扣体力 + 清档；暂停时存档也不会被写成 0 而误判成"失败界面强退"）
        _remainingTime += fallbackSeconds;
        CurrStageData.RemainingTime = _remainingTime;
        _isWarningTriggered = false;
        _isDangerTriggered = false;
        
        // 恢复 UI 状态
        _timerText.color = Color.white; 
        _timerText.transform.DOKill(); 
        _timerText.transform.localScale = Vector3.one; 
        Outline outline = _timerText.GetComponent<Outline>();
        if (outline != null) outline.effectColor = new Color(0, 0, 0, 0.5f); // 假设你原本的描边是半透明黑色，按需修改
        
        ResetTimeWarning();
        ResetTimeUpRedScreen();   // 复活时清掉时间到的红屏
        DestroyTimeUpBanner();    // 复活时收掉可能还在显示的「时间到！」横幅

        // ★ 进入不限时：关掉倒计时显示，后续 Update 不再扣时间
        SetUnlimitedTime(true);

        _isTimerRunning = true; // 计时标记保持"运行中"，其它依赖该状态的逻辑（连击/破冰）不受影响
        EventDispatcher.instance.TriggerChangeTopRaycast(true); // 解除屏幕屏蔽

        // ★ bug47：复活后如果背包里有蝴蝶、且场上空位 > 5，立刻飞出去提示一次
        //   （复活赠送的蝴蝶就是在这条链路上发的；这里不区分复活方式，避免漏掉广告/免费复活）
        TryAutoUseButterflyAfterRevive(autoUseGrantedButterfly);
    }

    /// <summary>
    /// 切换「不限时」状态：开启时不显示倒计时组件（底板 + 时间文字），关闭时恢复显示。
    /// 重新进关 / 重新开局时由 SetupGameData 调回 false，所以不限时只在复活的这一局内有效。
    /// </summary>
    private void SetUnlimitedTime(bool unlimited)
    {
        _isUnlimitedTime = unlimited;
        SetTimerUIVisible(!unlimited);

        if (unlimited)
        {
            // 不限时就不该再出现「时间到」的红屏/横幅残留
            ResetTimeUpRedScreen();
            DestroyTimeUpBanner();
        }
    }

    /// <summary>整体显隐倒计时组件（timerBg：时间底板 + 倒计时文字）</summary>
    private void SetTimerUIVisible(bool visible)
    {
        Transform timerRoot = _timerText != null ? _timerText.transform.parent : null;
        if (timerRoot != null)
        {
            if (timerRoot.gameObject.activeSelf != visible)
                timerRoot.gameObject.SetActive(visible);
            return;
        }

        if (_timerText != null && _timerText.gameObject.activeSelf != visible)
            _timerText.gameObject.SetActive(visible);
    }
    
    /// <summary>
    /// 重置时间警告特效（文字变白、取消描边变红、停止心跳动画）
    /// </summary>
    private void ResetTimeWarning()
    {
        _isWarningTriggered = false;
        _isDangerTriggered = false;

        // 时间文字恢复白色（1 分钟 / 10 秒提醒会把文字染红）
        if (_timerText != null)
        {
            _timerText.transform.DOKill();
            _timerText.transform.localScale = Vector3.one;
            _timerText.color = Color.white;
        }

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
    private void UpdatePupaProgress(int currentScore, bool isInstant)
    {
        if (!pupaObj.activeSelf) return;
        int threshold = ButterfliesManager.Instance.GetScoreThresholdForPupa();
            
        // 核心机制：如果总分超过了阈值(比如拿了150分，阈值60)，取余数得出30，让进度条循环显示！
        // 防止除以0报错，且保留当 currentScore 正好等于 threshold 时，视觉上呈现满环
        float targetFill = Mathf.Clamp01((float)currentScore / threshold);
      
        Text progressText = pupaObj.GetComponentInChildren<Text>(true);
        bool isJustCompleted = (targetFill >= 1f && pupaProgressBar.fillAmount < 1f);
        
        pupaProgressBar.DOKill();
        if (isInstant)
        {
            pupaProgressBar.fillAmount = targetFill;
            // 瞬间刷新时，如果是满的且之前没文字，强行显示+1
            if (targetFill >= 1f)
            {
                progressText.gameObject.SetActive(true);
                progressText.text = "+1";
                _pupaIcon.color = Color.white;
            }
            // 没满且没有叶子奖励时，才安全隐藏文字
            else
            {
                progressText.gameObject.SetActive(false);
                progressText.text = "+0"; // 强行清空数值，防止下一次解析错误
                _pupaIcon.color = new Color(1f, 1f, 1f, 0.7f);
            }
        }
        else
        {
            // ==============================================
            // 游戏进行中模式：兼顾叶子奖励与正常涨分
            // ==============================================
            // 1. 获取当前是否已经有奖励在展示（比如刚才叶子飞过来的）
            int currentVisualCount = 0;
            if (progressText.gameObject.activeSelf)
            {
                string currentStr = progressText.text.Replace("+", "").Trim();
                int.TryParse(currentStr, out currentVisualCount);
            }
            // 2. 只要进度满了，或者已经有叶子奖励展示了，就保持高亮状态
            Color targetColor = (targetFill >= 1f || currentVisualCount > 0) ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
            _pupaIcon.color = targetColor;
            
            // 3. 游戏进行中加分，花 0.3 秒平滑过渡过去
            pupaProgressBar.DOFillAmount(targetFill, 0.3f).SetEase(Ease.OutQuad).OnComplete(() => 
            {
                if (isJustCompleted)
                {
                    // 刚刚涨满时，利用叠加特效方法增加数值，并播放爆点！
                    PlayPupaCollectVisualEffect(1);
                    PlayPupaCompleteEffect();
                }
                else
                {
                    // 没涨满的情况，做一次安全检查，绝对不隐藏掉叶子已经给的奖励
                    if (progressText.gameObject.activeSelf)
                    {
                        string currentStr = progressText.text.Replace("+", "").Trim();
                        int.TryParse(currentStr, out int count);
                        if (count == 0 && targetFill < 1f)
                        {
                            progressText.gameObject.SetActive(false);
                        }
                    }
                }
            });
        }
    }
    
    /// <summary>
    /// 显示 Emoji 关闭提示气泡
    /// </summary>
    private void ShowEmojiTip()
    {
        if (emojiTip == null) return;
        emojiTip.SetActive(true);
        Canvas canvas = emojiTip.GetComponent<Canvas>();
        if (canvas == null) canvas = emojiTip.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingLayerName = UIPanelLayer.BasePanel; 
        canvas.sortingOrder = 10;
        
        emojiTip.GetComponentInChildren<Text>().text =
            MultilingualManager.Instance.GetString("EmojiOff", "pingzi");
        // 动态确保气泡可以被点击关闭
        Button tipBtn = emojiTip.GetComponent<Button>();
        if (tipBtn == null) tipBtn = emojiTip.AddComponent<Button>();
        tipBtn.onClick.RemoveAllListeners();
        tipBtn.onClick.AddListener(HideEmojiTip);
    
        // 开启 10 秒倒计时自动关闭
        if (_emojiTipCoroutine != null) StopCoroutine(_emojiTipCoroutine);
        _emojiTipCoroutine = StartCoroutine(AutoHideEmojiTipDelay(10f));
    }

    /// <summary>
    /// 隐藏 Emoji 关闭提示气泡
    /// </summary>
    private void HideEmojiTip()
    {
        if (emojiTip != null) emojiTip.SetActive(false);
        if (_emojiTipCoroutine != null)
        {
            StopCoroutine(_emojiTipCoroutine);
            _emojiTipCoroutine = null;
        }
    }

    private IEnumerator AutoHideEmojiTipDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideEmojiTip();
    }

}
