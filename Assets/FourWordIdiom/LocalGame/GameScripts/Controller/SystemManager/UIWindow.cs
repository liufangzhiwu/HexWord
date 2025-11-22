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
    public virtual void Close(CloseMethod method = CloseMethod.Default)
    {
        //if (CurrentState != WindowState.Opened) return;

        UpdateWindowState(WindowState.Closing);

        switch (method)
        {
            case CloseMethod.Immediate:
                CloseImmediately();
                break;

            case CloseMethod.Animated:
                PlayCloseAnimation();
                break;

            default:
                if (_windowAnimator != null)
                    PlayCloseAnimation();
                else
                    CloseImmediately();
                break;
        }
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

    private void CloseImmediately()
    {
        _onWindowClosed?.Invoke();
        gameObject.SetActive(false);       
    }

    #endregion
}