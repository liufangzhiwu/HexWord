
using System.IO;
using UnityEngine;
using UnityEditor;

namespace Middleware
{
    public partial class AppBuilder
    {

        [MenuItem("Tools/自动化打包/Android/谷歌/Debug", false, 160)]
        private static void BuildGoogleDebug()
        {
            BuildAndroid(new BuildParam()
            {
                BuildVersion = "1.0.0",
                IsBuildRelease = false,
                IsBuildShowLog = true,
                Channel = Channel.HuaweiAndroid
            });
        }

        [MenuItem("Tools/自动化打包/Android/谷歌/Release", false, 161)]
        private static void BuildGoogleRelease()
        {
            BuildAndroid(new BuildParam()
            {
                BuildVersion = "1.0.0",
                IsBuildRelease = true,
                IsBuildShowLog = false,
                Channel = Channel.HuaweiAndroid
            });
        } 

        
        private static void ApplyPlatformGoogleSettings(Channel channel)
        {
            if (channel != Channel.HuaweiAndroid) return;

            // 华为渠道特有设置
            PlayerSettings.applicationIdentifier = "chengyu.idiom.hexa.zen.huawei";
            PlayerSettings.productName = "成语消：禅意之境";
            // 签名
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName =
                Path.GetFullPath($"{Application.dataPath}/../platform/Android/user.keystore");
            PlayerSettings.Android.keystorePass = "hex123456";
            PlayerSettings.Android.keyaliasName = "liu";
            PlayerSettings.Android.keyaliasPass = "hex123456";

            // 如果有华为专用图标可在此设置
            // SetDefaultIcon(2);
        }
    }
}
