
using System.IO;
using UnityEngine;
using UnityEditor;

namespace Middleware
{
    public partial class AppBuilder
    {
        [MenuItem("Tools/自动化打包/Android/OPPO/Debug", false, 150)]
        private static void BuildOPPODebug() => BuildAndroid(new BuildParam { BuildVersion = "1.0.0",  IsBuildRelease = false, IsBuildShowLog = true, Channel = Channel.OPPO });
        [MenuItem("Tools/自动化打包/Android/OPPO/Release", false, 151)]
        private static void BuildOPPORelease() => BuildAndroid(new BuildParam { BuildVersion = "1.0.0", IsBuildRelease = true, IsBuildShowLog = false, Channel = Channel.OPPO });

        
        private static void ApplyPlatformOppoSettings(Channel channel)
        {
            if (channel != Channel.OPPO) return;

            PlayerSettings.applicationIdentifier = "chengyu.idiom.hexa.zen.oppo";
            PlayerSettings.productName = "成语消：禅意之境";

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName =
                Path.GetFullPath($"{Application.dataPath}/../platform/Android/user.keystore");
            PlayerSettings.Android.keystorePass = "hex123456";
            PlayerSettings.Android.keyaliasName = "liu";
            PlayerSettings.Android.keyaliasPass = "hex123456";
        }
    }
}
