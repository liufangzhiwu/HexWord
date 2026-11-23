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
    private GameObject loadingPanel;
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
      
        EnsureLoadingPanel();

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
    
    /// <summary>loading 遮罩的最上层排序值：商店等弹窗面板在 RewardPanel/20~52，这里直接拉到很高，保证盖住一切。</summary>
    private const int LoadingCanvasSortingOrder = 30000;

    /// <summary>
    /// 确保 loading 面板已实例化（Start 里是异步加载的，进店这类调用可能比它早）。
    /// 返回 false 表示确实拿不到预制体（资源缺失），调用方按"没有遮罩"降级处理。
    /// </summary>
    private bool EnsureLoadingPanel()
    {
        if (loadingPanel != null) return true;

        GameObject prefab;
        try
        {
            prefab = AdvancedBundleLoader.SharedInstance.LoadGameObject(bundleName, "LoadingScreen");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MessageSystem] LoadingScreen 预制体加载异常: {e.Message}");
            return false;
        }

        if (prefab == null)
        {
            Debug.LogError($"[MessageSystem] LoadingScreen 预制体加载失败: {bundleName}/LoadingScreen");
            return false;
        }

        loadingPanel = Instantiate(prefab, transform); // 实例化加载面板

        // ★ LoadingScreen 自带的 Canvas 是 TipsPanel/0；
        //   商店这类面板是 RewardPanel/20~52，排序层更高但层内 order 更大，仍可能压在遮罩下面。
        //   这里把遮罩强制拉到最上层，保证任何界面打开时 loading 都能盖住内容。
        Canvas loadingCanvas = loadingPanel.GetComponent<Canvas>();
        if (loadingCanvas != null)
        {
            loadingCanvas.overrideSorting = true;
            loadingCanvas.sortingOrder = LoadingCanvasSortingOrder;
        }
        else
        {
            Debug.LogWarning("[MessageSystem] LoadingScreen 根节点没有 Canvas，遮罩可能压不住其它界面");
        }

        loadingPanel.gameObject.SetActive(false);
        return true;
    }

    public void ShowLoadingAnimation()
    {
        // loading 面板是 Start 里异步加载出来的，极端情况下（例如开机第一屏就进商店）可能还没就绪，
        // 这里现场补一次，实在拿不到才跳过（不空引用崩掉）
        if (!EnsureLoadingPanel())
        {
            Debug.LogWarning("[MessageSystem] loading 面板尚未就绪，跳过本次 ShowLoadingAnimation");
            return;
        }

        loadingPanel.SetActive(true); // 显示加载动画Panel

        // ★ 兜底：LoadingScreen 是 alpha 0.9 的全屏遮罩（raycast 全屏拦截），
        //   任何一条链路只要漏掉 HideLoadingAnimation，玩家看到的就是「游戏卡住」。
        //   这里加最长显示时间，超时自动隐藏并打日志，便于定位漏关的地方。
        if (_loadingGuard != null) StopCoroutine(_loadingGuard);
        _loadingGuard = StartCoroutine(CoLoadingGuard(LoadingMaxSeconds));
    }

    public void HideLoadingAnimation()
    {
        if (_loadingGuard != null)
        {
            StopCoroutine(_loadingGuard);
            _loadingGuard = null;
        }
        if (loadingPanel != null) loadingPanel.SetActive(false); // 隐藏加载动画Panel
    }

    /// <summary>loading 面板最长显示时间（秒），超过即强制隐藏</summary>
    private const float LoadingMaxSeconds = 150f;

    private Coroutine _loadingGuard;

    private IEnumerator CoLoadingGuard(float seconds)
    {
        float waited = 0f;
        while (waited < seconds)
        {
            if (loadingPanel == null || !loadingPanel.activeSelf) yield break;
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (loadingPanel != null && loadingPanel.activeSelf)
        {
            Debug.LogError($"[MessageSystem] loading 面板超过 {seconds:0}s 未关闭，强制隐藏（疑似回调丢失）");
            loadingPanel.SetActive(false);
        }
        _loadingGuard = null;
    }

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