using System;
using System.IO;
using System.Text;
using UnityEngine;
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

    [Header("调试开关（开发/测试用）")]
    [Tooltip("勾选后：每次启动时清除「永久去广告」状态 + 对应购买记录（彻底回到没买过去广告的新号），" +
             "方便反复验证去广告购买 → 广告链路 → IAA 分层；不影响其它数据。" +
             "清除时会写入「补单拦截」标记，避免重启后被平台已购查询补发回来；" +
             "玩家重新购买永久去广告、或 GM 重置存档时会自动解除拦截。")]
    public bool clearPermanentRemoveAdsOnStart = false;

    [Header("商店日志")]
    [Tooltip("记录商店 / 礼包相关日志（进店、推荐位决策、购买、售罄、限购等关键事件）。默认开启。")]
    public bool enableShopLog = true;

    [Tooltip("额外记录「每张商品卡每次刷新」的细节（价格布局 / 描边色 / 礼包内容逐项 / 图标排版）。" +
             "默认关闭：一次进店会有上百条，只排查具体卡片问题时再勾。")]
    public bool enableShopVerboseLog = false;

    [Tooltip("是否同时输出到 Unity 控制台（仅编辑器生效）。设备上只写日志文件，" +
             "避免 Debug.Log 的调用栈捕获开销 —— 这正是商店日志拖慢帧率的原因。")]
    public bool echoShopLogToConsole = true;

    [Header("日志落盘")]
    [Tooltip("日志攒批写盘的间隔（秒）。原来每条 Debug.Log 都会开关一次文件，是主线程 IO 尖峰的主要来源。")]
    [SerializeField] private float flushInterval = 2f;
    [Tooltip("缓冲日志超过该字符数时立刻落盘")]
    [SerializeField] private int flushThresholdChars = 16 * 1024;

    /// <summary>
    /// 启动时是否清除「永久去广告」状态（供 ShopManager 在初始化时读取）。
    /// 由 LogManager.Awake 从 Inspector 上的开关同步过来。
    /// </summary>
    public static bool ClearPermanentRemoveAdsOnStart { get; private set; }

    /// <summary>
    /// 是否记录商店 / 礼包关键日志（默认 true）。
    /// </summary>
    public static bool EnableShopLog { get; private set; }

    /// <summary>
    /// 是否记录商品卡逐卡细节（默认 false）。
    /// </summary>
    public static bool EnableShopVerboseLog { get; private set; }

    // ======================= 商店日志快捷入口 =======================

    /// <summary>
    /// 商店关键日志（进店 / 推荐位决策 / 购买 / 售罄 / 限购）。
    ///
    /// ★ 为什么不直接 Debug.Log：
    ///   商店这条链路一次进店几十上百条，而每条 Debug.Log 都会触发 Unity 的日志回调 + 捕获托管调用栈
    ///   （移动端单条约 0.3~1ms）。这里直接写进文件缓冲，只有一次字符串拼接的成本，
    ///   并且和普通日志共用同一套批量落盘逻辑。编辑器里仍可选输出到控制台。
    /// </summary>
    public static void ShopLog(string message)
    {
        if (!EnableShopLog) return;
        WriteShopLine($"[Shop] {message}");
    }

    /// <summary>商品卡逐卡细节日志（默认关闭，见 enableShopVerboseLog）。</summary>
    public static void ShopLogDetail(string message)
    {
        if (!EnableShopVerboseLog) return;
        WriteShopLine($"[Shop] {message}");
    }

    private static void WriteShopLine(string line)
    {
        // ★ 商店日志不受 Unity_ShowLog 总闸限制：
        //   正式包（GameCoreManager 里把 Debug.unityLogger.logEnabled 关掉）也要照写日志文件，
        //   这样测试在真机上拉 logs/log_0.txt 就能看到全部 [Shop] 关键日志（礼包 / 推荐位 / 购买 / 视频点）。
        LogManager mgr = Instance;
        if (mgr == null)
        {
            // LogManager 还没起来，退回普通日志
            Debug.Log(line);
            return;
        }

#if UNITY_EDITOR
        // 编辑器里直接进 Console，方便边跑边看；仍然只落盘一次（走 HandleLog）
        if (mgr.echoShopLogToConsole)
        {
            Debug.Log(line);
            return;
        }
#endif

        mgr.AppendDirectLine(line);
    }

    /// <summary>
    /// 绕过 Debug.Log 直接把一行写进日志缓冲。
    /// </summary>
    private void AppendDirectLine(string line)
    {
        string entry = $"{GetNowText()}: [Info] {line}";

        lock (fileLock)
        {
            pendingLog.AppendLine(entry);
        }

        logBuilder.AppendLine(entry);
        if (logBuilder.Length > maxBuilderChars)
            logBuilder.Remove(0, logBuilder.Length - maxBuilderChars);

        if (pendingLog.Length >= flushThresholdChars)
            FlushLogFile();
    }

    /// <summary>
    /// 按秒缓存的「当前时间」文本。
    /// 原来每条日志都 DateTime.Now.ToString() 一次（格式化 + 分配），商店日志一多就是纯浪费。
    /// </summary>
    private string GetNowText()
    {
        DateTime now = DateTime.Now;
        long second = now.Ticks / TimeSpan.TicksPerSecond;
        if (second != cachedTimeSecond)
        {
            cachedTimeSecond = second;
            cachedTimeText = now.ToString("yyyy-MM-dd HH:mm:ss");
        }
        return cachedTimeText;
    }

    private string logFilePath;
    private const long maxFileSize = 10 * 1024 * 1024; // 10MB
    private int logFileIndex = 0;

    /// <summary>DebugMenu 用的最近日志缓存（有上限，不会无限增长）</summary>
    public StringBuilder logBuilder = new StringBuilder();
    private const int maxBuilderChars = 200 * 1024;

    /// <summary>待落盘缓冲：按时间 / 体积批量写入，避免每条日志一次磁盘 IO</summary>
    private readonly StringBuilder pendingLog = new StringBuilder();
    private readonly object fileLock = new object();
    private bool isFlushing;
    private DateTime lastFlushTimeUtc = DateTime.UtcNow;

    /// <summary>按秒缓存的时间文本（见 GetNowText）</summary>
    private string cachedTimeText = "";
    private long cachedTimeSecond = -1;

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
        EnableShopLog = enableShopLog;
        EnableShopVerboseLog = enableShopVerboseLog;
        if (ClearPermanentRemoveAdsOnStart)
            Debug.Log("[LogManager] 已开启「启动时清除永久去广告状态」开关，稍后由 ShopManager 执行清理");

        CreateLogFile();
        // 注册日志回调
        Application.logMessageReceived += HandleLog;   
        
        //Debug.unityLogger.logEnabled = !isRelease;
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

    void Update()
    {
        // 到点把缓冲的日志一次性落盘（原来每条日志都要开关一次文件）
        if (pendingLog.Length == 0) return;
        if ((DateTime.UtcNow - lastFlushTimeUtc).TotalSeconds < flushInterval) return;
        FlushLogFile();
    }

    void OnApplicationPause(bool pauseState)
    {
        if (pauseState) FlushLogFile();
    }

    void OnApplicationQuit()
    {
        FlushLogFile();
    }

    void OnDisable()
    {
        FlushLogFile();
    }

    void OnDestroy()
    {
        // 注销日志回调
        Application.logMessageReceived -= HandleLog;
        FlushLogFile();
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        LogLevel level = LogLevel.Info;
        if (type == LogType.Warning) level = LogLevel.Warning;
        if (type == LogType.Error || type == LogType.Exception) level = LogLevel.Error;

        // 创建日志条目（时间文本按秒缓存，避免每条都 DateTime 格式化）
        string logEntry = $"{GetNowText()}: [{level}] {logString}";

        // ★ 只给 Warning / Error / Exception 附带调用栈：
        //   Info 级日志附栈只会让日志文件翻倍、写入更慢，排查时也用不上。
        if (type != LogType.Log)
            logEntry += $"\n{stackTrace}";

        // DebugMenu 展示用
        logBuilder.AppendLine(logEntry);
        if (logBuilder.Length > maxBuilderChars)
            logBuilder.Remove(0, logBuilder.Length - maxBuilderChars);

        // 落盘：先入缓冲，批量写（后台线程打日志时也要安全，这里加锁）
        lock (fileLock)
        {
            pendingLog.AppendLine(logEntry);
        }

        // 错误 / 异常立刻落盘，避免崩进程时丢现场；普通 Info 走批量
        if (type == LogType.Error || type == LogType.Exception)
            FlushLogFile();
        else if (pendingLog.Length >= flushThresholdChars)
            FlushLogFile();
    }

    /// <summary>
    /// 把缓冲区里的日志一次性写入文件。
    /// 注意：这里绝对不能再用 Debug.Log / Debug.LogWarning 报错 —— 会重新进入 HandleLog 造成递归。
    /// </summary>
    public void FlushLogFile()
    {
        if (isFlushing) return;
        if (pendingLog.Length == 0) return;

        string content;
        lock (fileLock)
        {
            content = pendingLog.ToString();
            pendingLog.Length = 0;
        }

        isFlushing = true;
        lastFlushTimeUtc = DateTime.UtcNow;
        try
        {
            string fullPath = GetCurrentLogPath();

            // 使用锁确保同一时间只有一个线程可以写入文件
            lock (fileLock)
            {
                using (StreamWriter writer = new StreamWriter(fullPath, true))
                {
                    writer.Write(content);
                }
            }
        }
        catch (Exception)
        {
            // 写日志失败可以接受，但不能再打日志（会递归）
        }
        finally
        {
            isFlushing = false;
        }
    }

    /// <summary>取当前日志文件路径；超过单文件上限时自动换下一个。</summary>
    private string GetCurrentLogPath()
    {
        string fullPath = Path.Combine(logFilePath, $"log_{logFileIndex}.txt");

        if (File.Exists(fullPath) && new FileInfo(fullPath).Length >= maxFileSize)
        {
            logFileIndex++;
            fullPath = Path.Combine(logFilePath, $"log_{logFileIndex}.txt");
        }

        return fullPath;
    }

}
