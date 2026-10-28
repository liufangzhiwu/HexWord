using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// OpenHarmony 工程导出入口（薄封装）。
///
/// 真正的打包流程已经并入 Middleware.AppBuilder（Tools/自动化打包/Harmony/Debug|Release），
/// 这里只保留两个入口，方便无人值守 / 命令行场景：
///   A) 语义桥接调用 HarmonyExportBuilder.Start()  —— Unity GUI 已打开时用，异步执行。
///   B) 命令行调用 Unity.exe -executeMethod HarmonyExportBuilder.RunBatch —— 无人值守时用。
///
/// 导出目录由环境变量 HARMONY_OUT_DIR 覆盖；成功会写 harmony_export.done 标记文件。
/// </summary>
public class HarmonyExportBuilder
{
    private const string LogPath = @"C:\Users\Administrator\WorkBuddy\2026-10-26-16-34-55\harmony_export.log";
    private const string DoneMarker = @"C:\Users\Administrator\WorkBuddy\2026-10-26-16-34-55\harmony_export.done";
    private const string DefaultOutDir = @"D:\GitFork\HexWord\output\harmony_fix_1.3.4";
    private static bool _started;

    /// <summary>导出目录，可由环境变量 HARMONY_OUT_DIR 覆盖。</summary>
    public static string OutDir
    {
        get
        {
            var env = Environment.GetEnvironmentVariable("HARMONY_OUT_DIR");
            return string.IsNullOrEmpty(env) ? DefaultOutDir : env;
        }
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + Environment.NewLine, Encoding.UTF8); }
        catch { }
    }

    /// <summary>调度一次异步导出（立即返回，构建在下一个编辑器 tick 执行）。</summary>
    public static string Start()
    {
        if (_started) return "already running, see " + LogPath;
        File.WriteAllText(LogPath, "[" + DateTime.Now.ToString("HH:mm:ss") + "] Harmony export scheduled" + Environment.NewLine, Encoding.UTF8);
        EditorApplication.update += Tick;
        return "scheduled -> " + LogPath;
    }

    /// <summary>
    /// 同步导出，供命令行调用：
    /// Unity.exe -batchmode -quit -projectPath &lt;proj&gt; -executeMethod HarmonyExportBuilder.RunBatch
    /// 执行失败时以非 0 退出码结束，便于 CI / 脚本判断。
    /// </summary>
    public static void RunBatch()
    {
        try
        {
            File.WriteAllText(LogPath, "[" + DateTime.Now.ToString("HH:mm:ss") + "] Harmony export (batch) started" + Environment.NewLine, Encoding.UTF8);
            DoExport();
        }
        catch (Exception e)
        {
            Log("BATCH EXCEPTION: " + e);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    private static void Tick()
    {
        if (_started) return;
        _started = true;
        EditorApplication.update -= Tick;
        DoExport();
    }

    private static void DoExport()
    {
        try
        {
            Log("=== BEGIN HARMONY EXPORT ===");
            Log("unity=" + Application.unityVersion + " target=" + EditorUserBuildSettings.activeBuildTarget);

            var outDir = OutDir;
            var param = new Middleware.AppBuilder.BuildParam
            {
                BuildVersion = Middleware.AppBuilder.HarmonyVersion,
                IsBuildRelease = false,
                IsBuildShowLog = true,
                Channel = Middleware.AppBuilder.Channel.None
            };

            var hap = Middleware.AppBuilder.RunHarmonyPipeline(param, outDir, out var error);
            if (string.IsNullOrEmpty(hap))
            {
                Log("RESULT=Failed " + error);
                Log("=== END HARMONY EXPORT (FAILED) ===");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            Log("RESULT=Succeeded hap=" + hap);
            File.WriteAllText(DoneMarker, "OK " + DateTime.Now.ToString("s") + " hap=" + hap, Encoding.UTF8);
            Log("=== END HARMONY EXPORT (OK) ===");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Log("EXCEPTION: " + e);
            Log("=== END HARMONY EXPORT (with exception) ===");
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
