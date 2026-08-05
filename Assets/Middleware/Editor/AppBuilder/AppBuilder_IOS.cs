
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Middleware
{
    public partial class AppBuilder
    {
        [MenuItem("Tools/切换平台/IOS", false, 103)]
        private static void SwitchToApple()
        {
            SwitchPlatform(BuildTarget.iOS);
        }
        
        // iOS
        [MenuItem("Tools/自动化打包/IOS/Debug", false, 114)]
        public static void BuildIOSDebug()
        {
            BuildIOS(new BuildParam()
            {
                BuildVersion = "1.0.0",
                IsBuildRelease = false,
                IsBuildShowLog = true,
                Channel = Channel.None
            });
        }

        [MenuItem("Tools/自动化打包/IOS/Release", false, 115)]
        public static void BuildIOSRelease()
        {
            BuildIOS(new BuildParam()
            {
                BuildVersion = "1.0.0",
                IsBuildRelease = true,
                IsBuildShowLog = false,
                Channel = Channel.None
            });
        }
        
        private static void BuildIOS(BuildParam buildParam)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
            {
                Debug.LogError("先切换平台");
                return;
            }

            SetDefineSymbols(BuildTargetGroup.iOS, buildParam);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.iOS, ManagedStrippingLevel.Low);
            PlayerSettings.bundleVersion = buildParam.BuildVersion;
            PlayerSettings.iOS.buildNumber = GenBuildNumber().ToString();

            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            PlayerSettings.iOS.targetOSVersionString = "13.0";

            PlayerSettings.iOS.appleDeveloperTeamID = "xxx";
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.iOS.appInBackgroundBehavior = iOSAppInBackgroundBehavior.Custom;
            PlayerSettings.iOS.backgroundModes = iOSBackgroundMode.RemoteNotification | iOSBackgroundMode.Fetch;

            // iOS 特有设置（通过 partial 方法）
            ApplyPlatformIOSSettings(Channel.IOS);

            AssetBundleBuilder.BuildAssetBundles(false);
            var outputDir = Path.GetFullPath($"{Application.dataPath}/../output/IOS");
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
            var xcodePath = Path.Combine(outputDir, "xcode");

            var opts = !buildParam.IsBuildRelease ? BuildOptions.Development : BuildOptions.None;
            var report = BuildPipeline.BuildPlayer(GetBuildScenes(), xcodePath, BuildTarget.iOS, opts);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("打包失败");
                return;
            }

            Debug.Log("打包成功");
            Application.OpenURL(@"file://" + xcodePath);
        }
        
        public static void ApplyPlatformIOSSettings(Channel channel)
        {
            if (channel != Channel.IOS) return;

            PlayerSettings.applicationIdentifier = "chengyu.idiom.hexa.zen.xiaomi";
            PlayerSettings.productName = "成语消：禅意之境";

            // 小米渠道签名（可能需单独证书）
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName =
                Path.GetFullPath($"{Application.dataPath}/../platform/Android/user.keystore");
            PlayerSettings.Android.keystorePass = "hex123456";
            PlayerSettings.Android.keyaliasName = "liu";
            PlayerSettings.Android.keyaliasPass = "hex123456";
        }
    }
}
