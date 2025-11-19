using System.IO;
using UnityEditor;
using UnityEngine;
#if UNITY_IOS
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
#endif

/// <summary>
/// 打包自动配置文件
/// </summary>
public class CZYConfigEditor
{
#if UNITY_IOS
    [PostProcessBuildAttribute(100)]
    public static void onPostProcessBuild(BuildTarget target, string targetPath)
    {
        if (target != BuildTarget.iOS)
        {
            return;
        }

        string projPath = PBXProject.GetPBXProjectPath(targetPath);
        PBXProject proj = new PBXProject();
        proj.ReadFromString(File.ReadAllText(projPath));
        string unityTarget = proj.GetUnityFrameworkTargetGuid();

        #region 系统依赖库

        proj.AddFrameworkToProject(unityTarget, "StoreKit.framework", false);
        proj.AddFrameworkToProject(unityTarget, "iAd.framework", false);

        #endregion

        string content = proj.WriteToString();
        File.WriteAllText(projPath, content);
    }
#endif
}