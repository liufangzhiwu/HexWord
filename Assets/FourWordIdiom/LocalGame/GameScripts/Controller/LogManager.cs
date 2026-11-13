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

    public static void ShopLog(string msg)
    {
        Debug.Log(msg);
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
        //if (type == LogType.Error || type == LogType.Exception)
        //{
            logEntry += $"\n{stackTrace}";
        //}

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