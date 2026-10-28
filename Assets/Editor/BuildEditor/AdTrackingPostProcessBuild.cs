#if UNITY_EDITOR && UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

/// <summary>
/// iOS 出包后处理：补齐广告追踪（ATT）必需的系统配置。
///   1. Info.plist 增加 NSUserTrackingUsageDescription —— 没有它，系统授权弹窗不会出现；
///   2. 弱链接 AppTrackingTransparency / AdSupport 两个 framework。
///
/// 说明：AdTrackingBridge.mm 里已用 @available(iOS 14) 做了兼容，低版本系统不会崩。
/// </summary>
public static class AdTrackingPostProcessBuild
{
    /// <summary>系统弹窗里的说明文案（会显示在 iOS 的"允许跟踪"弹窗中，需与产品文案一致）</summary>
    private const string TrackingUsageDescription =
        "允许关联后，我们可以更准确地为您展示感兴趣的内容，并减少不相关的广告打扰。您可以随时在系统设置中更改此设置。";

    [PostProcessBuild(90)]
    public static void OnPostProcessBuild(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS) return;

        AddFrameworks(buildPath);
        AddUsageDescription(buildPath);
    }

    private static void AddFrameworks(string buildPath)
    {
        string projPath = PBXProject.GetPBXProjectPath(buildPath);
        if (!File.Exists(projPath))
        {
            Debug.LogWarning($"[AdTracking] 找不到 Xcode 工程：{projPath}");
            return;
        }

        PBXProject proj = new PBXProject();
        proj.ReadFromFile(projPath);

        string targetGuid = proj.GetUnityFrameworkTargetGuid();
        // true = 弱链接，iOS 13 设备上不会因为缺 AppTrackingTransparency 而启动失败
        proj.AddFrameworkToProject(targetGuid, "AppTrackingTransparency.framework", true);
        proj.AddFrameworkToProject(targetGuid, "AdSupport.framework", true);

        proj.WriteToFile(projPath);
        Debug.Log("[AdTracking] 已补充 AppTrackingTransparency / AdSupport framework");
    }

    private static void AddUsageDescription(string buildPath)
    {
        string plistPath = Path.Combine(buildPath, "Info.plist");
        if (!File.Exists(plistPath))
        {
            Debug.LogWarning($"[AdTracking] 找不到 Info.plist：{plistPath}");
            return;
        }

        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        plist.root.SetString("NSUserTrackingUsageDescription", TrackingUsageDescription);

        plist.WriteToFile(plistPath);
        Debug.Log("[AdTracking] 已写入 NSUserTrackingUsageDescription");
    }
}
#endif
