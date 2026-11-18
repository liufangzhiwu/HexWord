using UnityEngine;
using System.IO;
using System.Text;
using UnityEngine.UI;
using UnityEngine.UIElements;

public enum LogLevel
{
    Info,
    Warning,
    Error
}

public class LogManager : MonoBehaviour
{
    public static LogManager Instance;
    //是否为发布版本
    public bool isRelease=false;

    [Header("调试开关（开发/测试用，正式包请保持关闭）")]
    [Tooltip("勾选后：每次启动时清除「永久去广告」状态 + 对应购买记录（彻底回到没买过去广告的新号），" +
             "方便反复验证去广告购买 → 广告链路 → IAA 分层；不影响其它数据。" +
             "清除时会写入「补单拦截」标记，避免重启后被平台已购查询补发回来；" +
             "玩家重新购买永久去广告、或 GM 重置存档时会自动解除拦截。")]
    public bool clearPermanentRemoveAdsOnStart = false;

    /// <summary>
    /// 启动时是否清除「永久去广告」状态（供 ShopManager 在初始化时读取）。
    /// 由 LogManager.Awake 从 Inspector 上的开关同步过来。
    /// </summary>
    public static bool ClearPermanentRemoveAdsOnStart { get; private set; }

    [Header("日志堆栈开关（默认关闭，日志里不再带一长串方法调用栈）")]
    [Tooltip("勾选后：每条普通日志都会附带调用堆栈（方法名/行号）。\n" +
             "默认不勾选——Console / logcat 里只输出日志内容本身。\n" +
             "排查某个具体问题时再临时打开，或运行时调用 LogManager.SetStackTraceEnabled(true)。")]
    public bool printStackTrace = true;

    [Tooltip("错误 / 异常日志是否始终附带脚本堆栈（建议保持勾选，方便定位崩溃）。\n" +
             "取消勾选后，错误日志同样不带堆栈，只保留报错内容。")]
    public bool keepErrorStackTrace = true;

    /// <summary>当前是否打印普通日志的调用堆栈（由 Inspector 开关同步，可通过 SetStackTraceEnabled 运行时切换）。</summary>
    public static bool PrintStackTrace { get; private set; }

    /// <summary>当前错误/异常日志是否保留脚本堆栈。</summary>
    public static bool KeepErrorStackTrace { get; private set; }

    private string logFilePath;
    private const long maxFileSize = 10 * 1024 * 1024; // 10MB
    private int logFileIndex = 0;

    public StringBuilder logBuilder = new StringBuilder();   

    void Awake()
    {
        // 确保只有一个 LogManager 实例
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 在场景切换时不销毁
        }

        // 调试开关：清理「永久去广告」状态（ShopManager 初始化时会执行）
        ClearPermanentRemoveAdsOnStart = clearPermanentRemoveAdsOnStart;
        if (ClearPermanentRemoveAdsOnStart)
            Debug.Log("[LogManager] 已开启「启动时清除永久去广告状态」开关，稍后由 ShopManager 执行清理");

        // 日志堆栈开关：统一应用到 Unity 控制台 / logcat / 日志文件
        ApplyStackTraceSetting();

        CreateLogFile();
        // 注册日志回调
        Application.logMessageReceived += HandleLog;   
        
        //Debug.unityLogger.logEnabled = !isRelease;
    }

    /// <summary>
    /// 应用「是否打印调用堆栈」开关。
    /// Unity 的 Debug.Log 默认会在 logcat / Console 里跟一大串方法调用栈
    /// （ShopManager:DecideWinner → System.Linq… → UnityEngine.Events…），这里按开关关掉。
    /// </summary>
    public void ApplyStackTraceSetting()
    {
        PrintStackTrace = printStackTrace;
        KeepErrorStackTrace = keepErrorStackTrace;

        StackTraceLogType normalType = printStackTrace ? StackTraceLogType.ScriptOnly : StackTraceLogType.None;
        StackTraceLogType errorType = (printStackTrace || keepErrorStackTrace) ? StackTraceLogType.ScriptOnly : StackTraceLogType.None;

        Application.SetStackTraceLogType(LogType.Log, normalType);
        Application.SetStackTraceLogType(LogType.Warning, normalType);
        Application.SetStackTraceLogType(LogType.Assert, normalType);
        Application.SetStackTraceLogType(LogType.Error, errorType);
        Application.SetStackTraceLogType(LogType.Exception, errorType);

        Debug.Log($"[LogManager] 日志堆栈: 普通日志={(printStackTrace ? "开启" : "关闭")}, 错误日志={(errorType == StackTraceLogType.None ? "关闭" : "保留脚本堆栈")}");
    }

    /// <summary>
    /// 运行时切换「是否打印调用堆栈」（比如临时排查问题、或在 GM / 调试菜单里挂个按钮）。
    /// </summary>
    public static void SetStackTraceEnabled(bool enabled)
    {
        if (Instance != null)
        {
            Instance.printStackTrace = enabled;
            Instance.ApplyStackTraceSetting();
        }
        else
        {
            PrintStackTrace = enabled;
            StackTraceLogType type = enabled ? StackTraceLogType.ScriptOnly : StackTraceLogType.None;
            Application.SetStackTraceLogType(LogType.Log, type);
            Application.SetStackTraceLogType(LogType.Warning, type);
            Application.SetStackTraceLogType(LogType.Assert, type);
        }
    }

    public void CreateLogFile()
    {
        // 设置日志文件路径
        logFilePath = Path.Combine(Application.persistentDataPath, "logs");
        // 创建日志文件
        if (Directory.Exists(logFilePath))
        {
            Directory.Delete(logFilePath, true);
        }
        Directory.CreateDirectory(logFilePath);
    }

    public static void ShopLog(string msg)
    {
        Debug.Log("[Shop]"+msg);
    }

    void OnDestroy()
    {
        // 注销日志回调
        Application.logMessageReceived -= HandleLog;
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        LogLevel level = LogLevel.Info;
        if (type == LogType.Warning) level = LogLevel.Warning;
        if (type == LogType.Error || type == LogType.Exception) level = LogLevel.Error;

        // 创建日志条目
        string logEntry = $"{System.DateTime.Now}: [{level}] {logString}";

        // 堆栈：默认不落盘（避免日志文件被调用栈淹没），开关打开或错误日志保留时才追加
        bool needStackTrace = PrintStackTrace
                              || (KeepErrorStackTrace && (type == LogType.Error || type == LogType.Exception));
        if (needStackTrace && !string.IsNullOrEmpty(stackTrace))
            logEntry += $"\n{stackTrace}";

        logBuilder.AppendLine(logEntry);
        //Debug.Log(logEntry); // 在控制台输出

        // 将日志信息写入文件
        WriteLog(logEntry);
        //File.AppendAllText(logFilePath, logEntry + "\n");
    }


    private readonly object fileLock = new object();

    void WriteLog(string logEntry)
    {
        string logFileName = $"log_{logFileIndex}.txt";
        string fullPath = Path.Combine(logFilePath, logFileName);

        // 检查文件大小
        if (File.Exists(fullPath) && new FileInfo(fullPath).Length >= maxFileSize)
        {
            logFileIndex++;
            logFileName = $"log_{logFileIndex}.txt";
            fullPath = Path.Combine(logFilePath, logFileName);
        }
    
        // 使用锁确保同一时间只有一个线程可以写入文件
        lock (fileLock)
        {
            try
            {
                // 写入日志
                using (StreamWriter writer = new StreamWriter(fullPath, true))
                {
                    writer.WriteLine(logEntry);
                }
            }
            catch (IOException ex)
            {
                // 如果发生共享冲突，可以稍后重试或记录到其他地方
                Debug.LogWarning($"Failed to write log: {ex.Message}");
            
                // 可选：将日志暂时缓存，稍后重试
                // 或者使用 File.AppendAllText 的另一个重载版本
            }
        }
    }

}