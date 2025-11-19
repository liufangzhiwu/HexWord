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
        else
        {
            Destroy(gameObject); // 销毁重复的实例
        }

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


    /// <summary>
    /// 将日志条目写入到日志文件中。
    /// 如果当前日志文件大小达到最大限制，将自动创建新的日志文件。
    /// </summary>
    /// <param name="logEntry">要写入的日志条目内容。</param>
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
        //File.AppendAllText(fullPath, logEntry + "\n");
        // 写入日志
        using (StreamWriter writer = new StreamWriter(fullPath, true))
        {
            writer.WriteLine(logEntry);
        }
    }

}