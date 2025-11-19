using UnityEngine;
using System.Collections;
using DG.Tweening;
using UnityEngine.Pool;

public enum MessageShowType
{
    List,
    Window,
}

/// <summary>
/// 重构后的提示管理系统 - 优化对象池管理和动画流程
/// 改进点：
/// 1. 使用泛型对象池替代原始实现
/// 2. 分离动画逻辑与核心管理
/// 3. 增加错误处理机制
/// 4. 优化协程结构
/// 5. 添加资源加载保护
/// </summary>
public class MessageSystem : MonoBehaviour
{
    #region 单例模式
    public static MessageSystem Instance { get; private set; }
    #endregion

    #region 常量定义
    private const float FADE_DURATION = 0.2f;
    private const float DISPLAY_DURATION = 1f;
    private const int TOP_POSITION_Y = 0;
    private const int BOTTOM_POSITION_Y = -700;
    private const int HIDDEN_POSITION_Y = -400;
    #endregion

    #region 序列化字段
    [Header("资源配置")]
    [SerializeField] private string bundleName = "commonitem";
    [SerializeField] private string prefabName = "MessageWindow";

    [Header("位置设置")]
    [SerializeField] private int topPosition = TOP_POSITION_Y;
    [SerializeField] private int bottomPosition = BOTTOM_POSITION_Y;
    #endregion

    #region 私有字段
    private GameObject _tipPrefab;
    private ObjectPool<MessageWindow> _tipPool;
    private bool _isActiveTip;
    #endregion

    #region Unity生命周期
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private IEnumerator Start()
    {
        yield return InitializeTipSystem();
    }
    #endregion

    #region 初始化系统
    /// <summary>
    /// 异步初始化提示系统
    /// </summary>
    private IEnumerator InitializeTipSystem()
    {
        yield return LoadTipPrefab();
        CreateObjectPool();
    }

    /// <summary>
    /// 加载提示预制件
    /// </summary>
    private IEnumerator LoadTipPrefab()
    {
        var loadRequest = AdvancedBundleLoader.SharedInstance.LoadGameObject(
            bundleName,
            prefabName
        );

        yield return loadRequest;

        if (loadRequest == null)
        {
            Debug.LogError($"提示预制件加载失败: {bundleName}/{prefabName}");
            yield break;
        }

        _tipPrefab = loadRequest;
    }

    /// <summary>
    /// 创建对象池
    /// </summary>
    private void CreateObjectPool()
    {
        if (_tipPrefab == null)
        {
            Debug.LogError("无法创建对象池：未加载提示预制件");
            return;
        }

        _tipPool = new ObjectPool<MessageWindow>(
            createFunc: () => Instantiate(_tipPrefab, transform).GetComponent<MessageWindow>(),
            actionOnGet: panel => panel.gameObject.SetActive(true),
            actionOnRelease: panel => panel.gameObject.SetActive(false),
            actionOnDestroy: panel => Destroy(panel.gameObject),
            collectionCheck: false,
            defaultCapacity: 3,
            maxSize: 10
        );
    }
    #endregion

    #region 提示显示接口
    /// <summary>
    /// 显示提示信息（主入口）
    /// </summary>
    public void ShowTip(string message, bool isBottom = false, MessageShowType showType = MessageShowType.List)
    {
        if (_isActiveTip || !ValidateResources()) return;

        AudioManager.Instance.PlaySoundEffect("tips");
        StartCoroutine(DisplayTipRoutine(message, isBottom,showType));
    }

    /// <summary>
    /// 验证系统资源状态
    /// </summary>
    private bool ValidateResources()
    {
        if (_tipPool == null)
        {
            Debug.LogWarning("对象池未初始化");
            return false;
        }
        return true;
    }
    #endregion

    #region 提示显示协程
    /// <summary>
    /// 提示显示主协程
    /// </summary>
    private IEnumerator DisplayTipRoutine(string message, bool isBottom,MessageShowType showType)
    {
        _isActiveTip = true;

        MessageWindow tip = _tipPool.Get();
        SetupTipContent(tip, message, showType);

        if (showType == MessageShowType.List)
        {
            // 入场动画
            yield return PlayEnterAnimation(tip, isBottom);

            // 持续显示
            yield return new WaitForSeconds(DISPLAY_DURATION);

            // 退场动画
            yield return PlayExitAnimation(tip, isBottom);
            
            CleanupTip(tip);
            _isActiveTip = false;
        }
        else
        {
            tip.gameObject.SetActive(true);
            tip.transform.DOLocalMoveY(topPosition, 0.01f);
            // 入场动画
            //yield return PlayEnterAnimation(tip, isBottom);
            tip.GetComponent<CanvasGroup>().DOFade(1f, FADE_DURATION);

            _isActiveTip = false;
        }

      
    }

    /// <summary>
    /// 设置提示内容
    /// </summary>
    private void SetupTipContent(MessageWindow panel, string message, MessageShowType showType)
    {
        if (showType == MessageShowType.List)
        {
            panel.ListObject.gameObject.SetActive(true);
            panel.WindowObject.gameObject.SetActive(false);
            panel.StageText.text = MultilingualManager.Instance.GetString(message);
            panel.transform.localPosition = new Vector3(0, HIDDEN_POSITION_Y, 0);
            panel.GetComponent<CanvasGroup>().alpha = 0f;
        }
        else
        {
            panel.ListObject.gameObject.SetActive(false);
            panel.WindowObject.gameObject.SetActive(true);
            panel.WindowsStageText.text = MultilingualManager.Instance.GetString(message);
            panel.transform.localPosition = new Vector3(0, HIDDEN_POSITION_Y, 0);
            //panel.GetComponent<CanvasGroup>().alpha = 0f;
        }
        
       
    }

    /// <summary>
    /// 播放入场动画
    /// </summary>
    private IEnumerator PlayEnterAnimation(MessageWindow panel, bool isBottom)
    {
        int targetY = isBottom ? bottomPosition : topPosition;

        Sequence enterSequence = DOTween.Sequence();
        enterSequence.Join(panel.transform.DOLocalMoveY(targetY, FADE_DURATION));
        enterSequence.Join(panel.GetComponent<CanvasGroup>().DOFade(1f, FADE_DURATION));

        yield return enterSequence.WaitForCompletion();
    }

    /// <summary>
    /// 播放退场动画
    /// </summary>
    private IEnumerator PlayExitAnimation(MessageWindow panel, bool isBottom)
    {
        int targetY = isBottom ? bottomPosition - 200 : topPosition + 200;

        Sequence exitSequence = DOTween.Sequence();
        exitSequence.Join(panel.transform.DOLocalMoveY(targetY, FADE_DURATION));
        exitSequence.Join(panel.GetComponent<CanvasGroup>().DOFade(0f, FADE_DURATION));

        yield return exitSequence.WaitForCompletion();
    }

    /// <summary>
    /// 清理提示对象
    /// </summary>
    private void CleanupTip(MessageWindow panel)
    {
        _tipPool.Release(panel);
    }
    #endregion
}