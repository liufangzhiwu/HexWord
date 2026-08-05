
using System.IO;
using UnityEngine;
using UnityEditor;

namespace Middleware
{
    public partial class AppBuilder
    {
        [MenuItem("Tools/自动化打包/Android/小米/Debug", false, 140)]
        private static void BuildXiaomiDebug() => BuildAndroid(new BuildParam { BuildVersion = "1.0.0",  IsBuildRelease = false, IsBuildShowLog = true, Channel = Channel.Xiaomi });
        [MenuItem("Tools/自动化打包/Android/小米/Release", false, 141)]
        private static void BuildXiaomiRelease() => BuildAndroid(new BuildParam { BuildVersion = "1.0.0",  IsBuildRelease = true, IsBuildShowLog = false, Channel = Channel.Xiaomi });

        
        private static void ApplyPlatformXiaomiSettings(Channel channel)
        {
            if (channel != Channel.Xiaomi) return;

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
