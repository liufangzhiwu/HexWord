using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 侧滑（Android 系统返回键 / 全面屏侧滑返回）弹出的确认面板。
///
/// 两种形态（由调用方通过 Show(Mode) 决定）：
///   ExitApp  —— Start 场景：隐藏关闭按钮；主按钮文案「取消」，点击 = 关闭面板（真正退出由 QuitBtn 负责）
///   GoLevel  —— GameLobby 场景（主页 / 结算界面）：显示关闭按钮；主按钮文案「去闯关」，点击 = 进入当前模式的关卡
///
/// 面板全局只保留一份实例（挂在 Game._uiRoot 下，跨场景常驻），避免原来每次侧滑都 Instantiate 一份叠层。
/// </summary>
public class QuitPanel : MonoBehaviour
{
    public enum Mode
    {
        ExitApp,   // Start 场景
        GoLevel    // GameLobby 场景（主页 / 结算）
    }

    private const string LevelButtonNodeName = "GoLevelBtn";
    private const string CloseButtonNodeName = "close";
    private const string RejectButtonNodeName = "QuitBtn";

    private const string CancelKey = "Cancel";
    private const string CancelFallback = "取消";
    private const string GoLevelKey = "GoLevel";
    private const string GoLevelFallback = "去闯关";

    [Header("按钮（预制体里已按名字兜底查找，留空也能用）")]
    // 预制体里主按钮（GoLevelBtn）历史上序列化名是 _callbackButton，用 FormerlySerializedAs 保住这条绑定
    [FormerlySerializedAs("_callbackButton")]
    [SerializeField] private Button _levelButton;
    [SerializeField] private Button _rejectButton;
    // 右上角 close 按钮（预制体里默认是不可见的），Start 场景隐藏、大厅/结算显示
    [SerializeField] private Button _closeButton;

    private static QuitPanel _current;

    private Mode _mode = Mode.ExitApp;
    private Text _levelLabel;
    private bool _bound;

    #region 对外接口

    public static bool IsShowing => _current != null && _current.gameObject.activeSelf;

    /// <summary>显示（或复用）退出确认面板，并切到指定形态。</summary>
    public static QuitPanel Show(Mode mode)
    {
        if (_current == null)
        {
            GameObject prefab = Resources.Load<GameObject>("Privacy/QuitPanel");
            if (prefab == null)
            {
                Debug.LogError("[QuitPanel] 找不到 Resources/Privacy/QuitPanel.prefab");
                return null;
            }

            Transform root = Middleware.Game.self != null ? Middleware.Game.self._uiRoot : null;
            GameObject instance = Instantiate(prefab, root);
            instance.name = "QuitPanel";
            _current = instance.GetComponent<QuitPanel>();
            if (_current == null) _current = instance.AddComponent<QuitPanel>();
        }

        _current.gameObject.SetActive(true);
        _current.transform.SetAsLastSibling();   // 保证盖在其它界面之上
        _current.ApplyMode(mode);
       
        return _current;
    }

    /// <summary>关闭面板（等价于点「取消」/「关闭」）。</summary>
    public static void Hide()
    {
        if (_current == null) return;
        _current.gameObject.SetActive(false);
    }

    #endregion

    #region 生命周期

    private void Awake()
    {
        ResolveNodes();
        BindButtons();
    }

    private void OnEnable()
    {
        // Awake 里可能因为预制体字段缺失没绑上，这里再兜一次
        ResolveNodes();
        BindButtons();
        ApplyMode(_mode);
    }

    #endregion

    #region 内部实现

    /// <summary>预制体/代码字段可能没对上，统一按节点名兜底查找。</summary>
    private void ResolveNodes()
    {
        if (_levelButton == null) _levelButton = FindButton(LevelButtonNodeName);
        if (_closeButton == null) _closeButton = FindButton(CloseButtonNodeName);
        if (_rejectButton == null) _rejectButton = FindButton(RejectButtonNodeName);

        if (_levelButton != null)
            _levelLabel = _levelButton.GetComponentInChildren<Text>(true);
    }

    private Button FindButton(string nodeName)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].name == nodeName) return buttons[i];
        }
        return null;
    }

    private void BindButtons()
    {
        if (_bound) return;
        if (_levelButton == null && _rejectButton == null && _closeButton == null) return;

        if (_levelButton != null) _levelButton.AddClickAction(OnLevelClick);
        if (_closeButton != null) _closeButton.AddClickAction(OnCloseClick);
        if (_rejectButton != null) _rejectButton.AddClickAction(OnQuitClick);
        _bound = true;
    }

    private void ApplyMode(Mode mode)
    {
        _mode = mode;

        // 关闭按钮：Start 场景隐藏，大厅/结算显示
        if (_closeButton != null)
            _closeButton.gameObject.SetActive(mode == Mode.GoLevel);

        // 主按钮文案
        if (_levelLabel != null)
            _levelLabel.text = mode == Mode.GoLevel ? GetText(GoLevelKey, GoLevelFallback)
                                                    : GetText(CancelKey, CancelFallback);
    }

    private static string GetText(string key, string fallback)
    {
        string text = MultilingualManager.Instance != null
            ? MultilingualManager.Instance.GetString(key)
            : key;
        // GetString 取不到时会把 key 原样返回，这里回退到需求给定的中文文案
        return string.IsNullOrEmpty(text) || text == key ? fallback : text;
    }

    private void OnLevelClick()
    {
        if (_mode == Mode.ExitApp)
        {
            // Start 场景：「取消」= 关掉面板
            Hide();
            return;
        }

        // GameLobby：「去闯关」= 进入当前模式的关卡
        Hide();
        UIWindow primary = SystemManager.Instance != null
            ? SystemManager.Instance.GetPanel(PanelType.PrimaryInterface)
            : null;

        PrimaryInterface primaryInterface = primary as PrimaryInterface;
        if (primaryInterface != null)
            primaryInterface.EnterStageFromBackGesture();
        else
            Debug.LogWarning("[QuitPanel] 取不到 PrimaryInterface，无法进入关卡");
    }

    private void OnCloseClick()
    {
        Hide();
    }

    private void OnQuitClick()
    {
        Application.Quit();
    }

    #endregion
}
