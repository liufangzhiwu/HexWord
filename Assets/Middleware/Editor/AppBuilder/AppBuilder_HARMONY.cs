using System.IO;
using UnityEngine;
using UnityEditor;

namespace Middleware
{
    public partial class AppBuilder
    {
        
#if UNITY_2022_3_55 || UNITY_2022_3_61
        [MenuItem("Tools/切换平台/Harmony", false, 101)]
        private static void SwitchToHarmony()
        {
            SwitchPlatform(BuildTarget.OpenHarmony);
            if (AssetDatabase.IsValidFolder("Assets/GeneratedLocalRepo"))
                AssetDatabase.DeleteAsset("Assets/GeneratedLocalRepo");
        }
#endif
        
        #if UNITY_2022_3_55 || UNITY_2022_3_61
        [MenuItem("Tools/自动化打包/Harmony/Debug", false, 110)]
        private static void BuildHarmonyDebug()
        {
            BuildHarmony(new BuildParam()
            {
                BuildVersion = "1.5.0",
                IsBuildRelease = false,
                IsBuildShowLog = true,
                Channel = Channel.None
            });
        }

        [MenuItem("Tools/自动化打包/Harmony/Release", false, 111)]
        private static void BuildHarmonyRelease()
        {
            BuildHarmony(new BuildParam()
            {
                BuildVersion = "1.5.0",
                IsBuildRelease = true,
                IsBuildShowLog = false,
                Channel = Channel.None
            });
        }
        #endif

#if UNITY_2022_3_55 || UNITY_2022_3_61
        private static void BuildHarmony(BuildParam buildParam)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.OpenHarmony)
            {
                Debug.LogError("先切换平台");
                return;
            }

            SetDefineSymbols(BuildTargetGroup.OpenHarmony, buildParam);
            EditorUserBuildSettings.exportAsOpenHarmonyProject = true;
            EditorUserBuildSettings.development = false;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.OpenHarmony.bundleVersionCode = GenBuildNumber();
            PlayerSettings.OpenHarmony.targetArchitectures = OpenHarmonyArchitecture.ARM64;
            PlayerSettings.companyName = "HexaSpaceGames";
            PlayerSettings.productName = "成语消：禅意之境";
            PlayerSettings.applicationIdentifier = "chengyu.idiom.hexa.zen.huawei";
            PlayerSettings.OpenHarmony.useCustomKeystore = true;
            PlayerSettings.OpenHarmony.keystoreName =
                Path.GetFullPath($"{Application.dataPath}/../platform/Harmony/hexa.p12");
            PlayerSettings.OpenHarmony.keystorePass = "hexa123456";
            PlayerSettings.OpenHarmony.keyaliasName = "hexa";
            PlayerSettings.OpenHarmony.keyaliasPass = "hexa123456";
            PlayerSettings.OpenHarmony.openHarmonyAppID = "6917590527000396765";
            PlayerSettings.OpenHarmony.openHarmonyClientID = "6917590527000396765";
            var p7Name = buildParam.IsBuildRelease ? "hexa_releaseRelease.p7b" : "hexa_DebugDebug.p7b";
            var cerName = buildParam.IsBuildRelease ? "hexa_release.cer" : "hexa_debug.cer";
            PlayerSettings.OpenHarmony.openHarmonyProfile =
                Path.GetFullPath($"{Application.dataPath}/../platform/Harmony/{p7Name}");
            PlayerSettings.OpenHarmony.openHarmonyCertificate =
                Path.GetFullPath($"{Application.dataPath}/../platform/Harmony/{cerName}");

            AssetBundleBuilder.BuildAssetBundles(false);
            // 调用 Harmony 特有设置（如果有）
            ApplyHarmonySettings();

            // 以下打包代码被注释，如需启用请取消注释
            // var outputDir = Path.GetFullPath($"{Application.dataPath}/../output/Harmony");
            // if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
            // var harProject = Path.Combine(outputDir, "project");
            // var report = BuildPipeline.BuildPlayer(GetBuildScenes(), harProject, BuildTarget.OpenHarmony, BuildOptions.None);
            // ...
        }
        #endif

        // 实现 Harmony 特有设置（目前无额外设置，因为已在主方法中完成）
        private static void ApplyHarmonySettings()
        {
            // 如果有额外需要（如修改鸿蒙特定属性），可在此实现
            // 例如：PlayerSettings.OpenHarmony.xxx = ...;
            Debug.Log("Apply Harmony specific settings (if any)");
        }

        // 如果 Harmony 也需要覆盖 Android 的 ApplyPlatformSettings，但它独立于 Android，所以无需实现
    }
}
