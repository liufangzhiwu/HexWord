using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Middleware
{
    public partial class AppBuilder
    {
        public const string DefineRelease = "Unity_Release";
        public const string DefineShowLog = "Unity_ShowLog";
        public const string DefineResourceAb = "Unity_ResourceAb";

        // 渠道枚举
        public enum Channel
        {
            None,           // 未指定（默认华为）
            Android,
            HuaweiAndroid,
            Honor,
            Xiaomi,
            OPPO,
            GOOGLE,
            IOS
            // 可继续扩展 VIVO, Meizu 等
        }

        public class BuildParam
        {
            public string BuildVersion;
            public bool IsBuildRelease;
            public bool IsBuildShowLog;
            public Channel Channel = Channel.None;

            public override string ToString()
            {
                return $"{nameof(BuildVersion)}: {BuildVersion}, {nameof(IsBuildRelease)}: {IsBuildRelease}, {nameof(IsBuildShowLog)}: {IsBuildShowLog}, {nameof(Channel)}: {Channel}";
            }
        }
      

        #region 编辑器一键打包

        // Android 默认（华为）
        [MenuItem("Tools/自动化打包/Android/Debug", false, 112)]
        private void BuildAndroidDebug()
        {
            BuildAndroid(new BuildParam()
            {
                BuildVersion = "1.0.0",
                IsBuildRelease = false,
                IsBuildShowLog = true,
                Channel = Channel.HuaweiAndroid
            });
        }

        [MenuItem("Tools/自动化打包/Android/Release", false, 113)]
        private void BuildAndroidRelease()
        {
            BuildAndroid(new BuildParam()
            {
                BuildVersion = "1.0.0",
                IsBuildRelease = true,
                IsBuildShowLog = false,
                Channel = Channel.HuaweiAndroid
            });
        }
      
      

        #endregion

        #region 切换平台

        [MenuItem("Tools/切换平台/Android", false, 102)]
        private static void SwitchToAndroid()
        {
            SwitchPlatform(BuildTarget.Android);
        }

        private static void SwitchPlatform(BuildTarget targetPlatform, Channel channel = Channel.Android)
        {
            switch (channel)
            {
                case Channel.Android:
                    CleanXiaomiFolder();
                    CopyHuaweiFolderIfEmpty();
                    break;
                case Channel.HuaweiAndroid:
                    CleanXiaomiFolder();
                    CopyHuaweiFolderIfEmpty();
                    break;
                case Channel.Honor:
                    break;
                case Channel.Xiaomi:
                    CleanHuaweiFolder();
                    CopyXiaomiFolderIfEmpty();
                    break;
                case Channel.OPPO:
                    break;
                case Channel.GOOGLE:
                    break;
                case Channel.IOS:
                    break;
            }
            AssetDatabase.Refresh();
            
            if (EditorUserBuildSettings.activeBuildTarget == targetPlatform)
                return;
            
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(targetPlatform),
                targetPlatform);
            Debug.Log("切换平台成功");
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 递归复制目录
        /// </summary>
        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            // 创建目标目录
            Directory.CreateDirectory(targetDir);

            // 复制所有文件
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, destFile, true); // true 表示覆盖
            }

            // 递归复制子目录
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(targetDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir);
            }
        }

        #endregion

        // ---------- 公共打包方法 ----------
        private static void BuildAndroid(BuildParam buildParam)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.LogError("先切换平台");
                return;
            }

            // 设置宏（包含渠道宏）
            SetDefineSymbols(BuildTargetGroup.Android, buildParam);

            // 通用打包设置
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            EditorUserBuildSettings.development = !buildParam.IsBuildRelease;
            EditorUserBuildSettings.buildAppBundle = buildParam.IsBuildRelease;
            EditorUserBuildSettings.androidCreateSymbols = buildParam.IsBuildRelease
                ? AndroidCreateSymbols.Public
                : AndroidCreateSymbols.Disabled;
            PlayerSettings.Android.minifyRelease = buildParam.IsBuildRelease;
            PlayerSettings.bundleVersion = buildParam.BuildVersion;
            PlayerSettings.Android.bundleVersionCode = GenBuildNumber();
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Low);
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel35;

            switch (buildParam.Channel)
            {
                case Channel.HuaweiAndroid:
                    ApplyPlatformHuaweiAndroidSettings(buildParam.Channel);
                    break;
                case Channel.Honor:
                    ApplyPlatformHonorSettings(buildParam.Channel);
                    break;
                case Channel.Xiaomi:
                    ApplyPlatformXiaomiSettings(buildParam.Channel);
                    break;
                case Channel.OPPO:
                    ApplyPlatformOppoSettings(buildParam.Channel);
                    break;
                case Channel.GOOGLE:
                    ApplyPlatformGoogleSettings(buildParam.Channel);
                    break;
            }

            // 打资源包
            AssetBundleBuilder.BuildAssetBundles(false);

            // 生成输出文件名
            var outputDir = Path.GetFullPath($"{Application.dataPath}/../output/Android");
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
            var symbolDefine = buildParam.IsBuildRelease ? "release" : "debug";
            var version = PlayerSettings.bundleVersion.Replace(".", "");
            var extName = buildParam.IsBuildRelease ? "aab" : "apk";
            string channelSuffix = buildParam.Channel == Channel.None ? "huawei" : buildParam.Channel.ToString().ToLower();
            var apkPath = $"{outputDir}/{symbolDefine}_{version}_{channelSuffix}_{DateTime.Now:yyyy-MM-dd-HHmmss}.{extName}";

            var opts = !buildParam.IsBuildRelease ? BuildOptions.Development : BuildOptions.None;
            var report = BuildPipeline.BuildPlayer(GetBuildScenes(), apkPath, BuildTarget.Android, opts);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("打包失败");
                return;
            }

            Debug.Log("打包成功: " + apkPath);
            Application.OpenURL(@"file://" + outputDir);
        }

        private static void SetDefineSymbols(BuildTargetGroup target, BuildParam buildParam)
        {
            var defines = new List<string>();
            if (buildParam.IsBuildRelease)
                defines.Add(DefineRelease);
            if (buildParam.IsBuildShowLog)
                defines.Add(DefineShowLog);

            // 添加渠道宏
            switch (buildParam.Channel)
            {
                case Channel.HuaweiAndroid: defines.Add("UNITY_HUAWEI"); break;
                case Channel.Honor:         defines.Add("UNITY_HONOR"); break;
                case Channel.Xiaomi:        defines.Add("UNITY_XIAOMI"); break;
                case Channel.OPPO:          defines.Add("UNITY_OPPO"); break;
                case Channel.GOOGLE:          defines.Add("UNITY_GOOGLE"); break;
                // None 不添加
            }

            PlayerSettings.SetScriptingDefineSymbolsForGroup(target, defines.ToArray());
        }

        private static int GenBuildNumber()
        {
            var nowDate = DateTime.Now;
            var strBuildNumber =
                $"{nowDate.Year - 2000}{nowDate.Month:00}{nowDate.Day:00}{(nowDate.Hour * 60 + nowDate.Minute) / 15}";
            return int.Parse(strBuildNumber);
        }

        private static string[] GetBuildScenes()
        {
            var names = new List<string>();
            foreach (var e in EditorBuildSettings.scenes)
            {
                if (e == null) continue;
                if (e.enabled) names.Add(e.path);
            }
            return names.ToArray();
        }
    }
}