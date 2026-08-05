
using System.IO;
using UnityEngine;
using UnityEditor;

namespace Middleware
{
    public partial class AppBuilder
    {

        [MenuItem("Tools/自动化打包/Android/荣耀/Debug", false, 130)]
        private static void BuildHonorDebug()
        {
            BuildAndroid(new BuildParam()
            { 
                BuildVersion = "1.0.0", 
                IsBuildRelease = false,
                IsBuildShowLog = true, 
                Channel = Channel.Honor 
            });
        }

        [MenuItem("Tools/自动化打包/Android/荣耀/Release", false, 131)]
        private static void BuildHonorRelease()
        {
            BuildAndroid(new BuildParam()
            {
                BuildVersion = "1.0.0",
                IsBuildRelease = true,
                IsBuildShowLog = false,
                Channel = Channel.Honor
            });
        } 
        
        private static void ApplyPlatformHonorSettings(Channel channel)
        {
            if (channel != Channel.Honor) return;

            PlayerSettings.applicationIdentifier = "chengyu.idiom.hexa.zen.honor";
            PlayerSettings.productName = "成语消：禅意之境";

            // 荣耀使用HMS，签名同华为
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName =
                Path.GetFullPath($"{Application.dataPath}/../platform/Android/user.keystore");
            PlayerSettings.Android.keystorePass = "hex123456";
            PlayerSettings.Android.keyaliasName = "liu";
            PlayerSettings.Android.keyaliasPass = "hex123456";
        }
    }
}
