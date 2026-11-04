using DG.Tweening;
using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>
/// 增强版UI界面基类 - 提供完整的UI生命周期管理和交互功能
/// Enhanced UI Base Class with full lifecycle management
/// </summary>
public abstract class UIWindow : MonoBehaviour, IPointerDownHandler
{
    #region 枚举和委托/Enums & Delegates

    public enum WindowState
    {
        Closed,
        Opening,
        Opened,
        Closing
    }

    public enum CloseMethod
    {
        Default,
        Immediate,
        Animated
    }

    public delegate void WindowEventHandler(UIWindow window);
    public delegate void WindowStateHandler(UIWindow window, WindowState state);

    #endregion

    #region 字段/Fields

    [Header("UI Settings")]
    [Tooltip("界面显示名称")]
    private string _windowName;

    [Tooltip("界面功能分类")]
     private string _windowCategory;

    [Tooltip("界面层级 (值越大显示在越上层)")]
    private int _windowLayer;

    [Tooltip("是否阻止背景点击穿透")]
    private bool _blockBackgroundClick;

    [Tooltip("界面动画控制器")]
    protected Animator _windowAnimator;

    [Header("Transition Settings")]
    [Tooltip("打开动画时间")]
    [SerializeField] protected float _openDuration = 0.3f;

    [Tooltip("关闭动画时间")]
    [SerializeField] protected float _closeDuration = 0.3f;

    // 窗口状态
    private WindowState _currentState = WindowState.Closed;
    private Canvas _windowCanvas;
    //private CanvasGroup _canvasGroup;

    // 事件系统
    private UnityEvent _onWindowOpened = new UnityEvent();
    private UnityEvent _onWindowClosed = new UnityEvent();
    private UnityEvent<WindowState> _onStateChanged = new UnityEvent<WindowState>();

    #endregion

    #region 属性/Properties

    public void SetWindowName(string name) => _windowName = name;
    public string WindowCategory => _windowCategory;
    public void SetWindowCategory(string category) => _windowCategory = category;
    public string WindowName => _windowName;   
    public int WindowLayer => _windowLayer;
    public WindowState CurrentState => _currentState;
    public bool IsWindowVisible => gameObject.activeSelf;  

    #endregion

    #region 生命周期/Lifecycle Methods

    protected virtual void Awake()
    {
        // 自动获取必要组件
        if (!_windowAnimator) TryGetComponent(out _windowAnimator);
        //TryGetComponent(out _canvasGroup);
        TryGetComponent(out _windowCanvas);

        // 初始化UI元素
        InitializeUIComponents();
    }

    protected virtual void OnEnable()
    {
        UpdateWindowState(WindowState.Opening);
        StartCoroutine(PlayOpenAnimation());
    }

    protected virtual void OnDisable()
    {
        UpdateWindowState(WindowState.Closed);
        ClearAllEventListeners();
        StopAllCoroutines();

        // 安全网：跳过入场时临时拉高过播放倍速，若界面在恢复前就被关闭，这里兜底还原，
        // 避免下次显示时整条动画以 100 倍速一闪而过。
        if (_windowAnimator != null) _windowAnimator.speed = 1f;
    }

    #endregion

    #region 公共方法/Public Methods

    /// <summary>
    /// 打开界面 (带参数)
    /// Open window with parameters
    /// </summary>
    public virtual void Open()
    {
        //if (CurrentState != WindowState.Closed) return;

        gameObject.SetActive(true);
        _onWindowOpened?.Invoke();
    }

    /// <summary>
    /// 关闭界面
    /// Close window
    /// </summary>
    public virtual void Close()
    {
        //if (CurrentState != WindowState.Opened) return;

        UpdateWindowState(WindowState.Closing);

        // switch (method)
        // {
        //     case CloseMethod.Immediate:
        //         CloseImmediately();
        //         break;
        //
        //     case CloseMethod.Animated:
        //         PlayCloseAnimation();
        //         break;
        //
        //     default:
                if (_windowAnimator != null)
                    PlayCloseAnimation();
                else
                    CloseImmediately();
        //         break;
        // }
    }

    /// <summary>
    /// 设置界面交互状态
    /// Set window interactable state
    /// </summary>
    public void SetInteractable(bool interactable)
    {
        //if (_canvasGroup)
        //{
        //    _canvasGroup.interactable = interactable;
        //    _canvasGroup.blocksRaycasts = interactable;
        //}
    }

    /// <summary>
    /// 设置界面层级
    /// Set window layer
    /// </summary>
    public void SetLayer(int layer)
    {
        _windowLayer = layer;
        if (_windowCanvas)
            _windowCanvas.sortingOrder = layer;
    }
    
    /// <summary>
    /// 播放bool类型的动画
    /// </summary>
    /// <param name="animationName"></param>
    /// <param name="playOnStart"></param>
    public void PlayNameAnimationBool(string animationName, bool playOnStart = true)
    {
        _windowAnimator.SetBool(animationName, playOnStart);
    }

    #endregion

    #region 事件管理/Event Management

    public void AddOpenListener(UnityAction callback) => _onWindowOpened.AddListener(callback);
    public void RemoveOpenListener(UnityAction callback) => _onWindowOpened.RemoveListener(callback);

    public void AddCloseListener(UnityAction callback) => _onWindowClosed.AddListener(callback);   

    public void AddStateChangeListener(UnityAction<WindowState> callback) => _onStateChanged.AddListener(callback);
    public void RemoveStateChangeListener(UnityAction<WindowState> callback) => _onStateChanged.RemoveListener(callback);

    private void ClearAllEventListeners()
    {
        _onWindowOpened.RemoveAllListeners();
        _onWindowClosed.RemoveAllListeners();
        _onStateChanged.RemoveAllListeners();
    }

    #endregion

    #region 背景点击处理/Background Click Handling

    public void OnPointerDown(PointerEventData eventData)
    {
        // 背景点击关闭处理
        if (_blockBackgroundClick && eventData.pointerPress == gameObject)
        {
            Close();
        }
    }

    public virtual void OnHideAnimationEnd()
    {

        SetInteractable(false);

        // 恢复默认动画
        if (_windowAnimator != null)
            _windowAnimator.SetBool("IsHidden", false);

        // 动画结束处理
        CloseImmediately();     

        //PlayCloseAnimation();
    }

    #endregion

    #region 受保护方法/Protected Methods

    /// <summary>
    /// 初始化UI组件
    /// Initialize UI components
    /// </summary>
    protected virtual void InitializeUIComponents() { }

    /// <summary>
    /// 自定义打开动画
    /// Custom open animation
    /// </summary>
    protected virtual IEnumerator CustomOpenAnimation()
    {
        //if (_canvasGroup)
        //{
        //    _canvasGroup.alpha = 0;
        //    yield return _canvasGroup.DOFade(1, _openDuration).WaitForCompletion();
        //}
        yield return null;
    }

    /// <summary>
    /// 自定义关闭动画
    /// Custom close animation
    /// </summary>
    protected virtual void CustomCloseAnimation()
    {
        if (_windowAnimator != null)
            _windowAnimator.SetBool("IsHidden", true);
        else
        {
            _onWindowClosed?.Invoke();
            gameObject.SetActive(false);
        }      
    }

    #endregion

    #region 私有方法/Private Methods

    private void UpdateWindowState(WindowState newState)
    {
        if (_currentState == newState) return;

        _currentState = newState;
        _onStateChanged?.Invoke(newState);
    }

    private IEnumerator PlayOpenAnimation()
    {
        SetInteractable(false);

        // 播放自定义或默认动画
        yield return StartCoroutine(CustomOpenAnimation());

        // 动画结束处理
        UpdateWindowState(WindowState.Opened);
        SetInteractable(true);
    }

    private void PlayCloseAnimation()
    {
        SetInteractable(false);

        // 播放自定义或默认动画
       CustomCloseAnimation();       
    }

    /// <summary>
    /// 以指定倍速播放入场动画，播完后自动还原为正常速度。
    /// 用于「入场要更快」但不希望改动画资源本身的场景（例如大厅/顶栏展示提速）。
    /// </summary>
    /// <param name="speed">播放倍速，2 表示快一倍</param>
    /// <remarks>
    /// 不能用 WindowState 判断是否在入场：HeaderSection / PrimaryInterface 等界面重写了 OnEnable
    /// 且没有调用 base.OnEnable()，状态机根本不会进入 Opening。这里直接看 Animator 当前状态是否还没播完。
    /// </remarks>
    public void PlayEnterAtSpeed(float speed)
    {
        if (_windowAnimator == null || !_windowAnimator.isActiveAndEnabled) return;
        if (!gameObject.activeInHierarchy) return;

        // 注意：这里不能按 normalizedTime 提前 return。
        // 界面刚 SetActive(true) 时 Animator 还没开始求值，读到的可能是上一次的旧状态（已播完），
        // 那样就会被误判成"没在入场"而整个加速失效。这里一律设置倍速，由下面的还原协程兜底。
        AnimatorStateInfo state = _windowAnimator.GetCurrentAnimatorStateInfo(0);
        _windowAnimator.speed = Mathf.Max(0.01f, speed);
        // 超时按「原始时长 / 倍速」再留 0.5s 余量，避免状态机没进预期状态时倍速残留
        float timeout = (state.length > 0f ? state.length / Mathf.Max(0.01f, speed) : 1f) + 0.5f;
        StartCoroutine(RestoreAnimatorSpeedRoutine(timeout));
    }

    /// <summary>
    /// 跳过本次入场动画，让界面瞬间到位。
    /// 用于「道具弹窗 + 顶部金币栏」这类必须同帧出现的组合：
    /// 弹窗是瞬显的，若顶栏还在播入场动画，金币栏就会明显慢半拍。
    /// </summary>
    public void SkipEnterTransition()
    {
        if (_windowAnimator != null)
        {
            // 一帧内跑完即可，不必等状态自然结束
            _windowAnimator.speed = 100f;
            StartCoroutine(RestoreAnimatorSpeedRoutine(0.2f));
        }
    }

    private IEnumerator RestoreAnimatorSpeedRoutine(float maxWaitSeconds)
    {
        float deadline = Time.unscaledTime + maxWaitSeconds;
        yield return null;

        // 等入场状态播完再还原；如果被 Hide/切状态打断，走超时兜底
        while (_windowAnimator != null &&
               Time.unscaledTime < deadline &&
               _windowAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
        {
            yield return null;
        }

        if (_windowAnimator != null) _windowAnimator.speed = 1f;
    }

    private void CloseImmediately()
    {
        _onWindowClosed?.Invoke();
        gameObject.SetActive(false);       
    }

    #endregion
}