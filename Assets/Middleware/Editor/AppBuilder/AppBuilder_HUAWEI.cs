
using System.IO;
using PlasticPipe.Certificates;
using UnityEngine;
using UnityEditor;

namespace Middleware
{
    public partial class AppBuilder
    {
        
        [MenuItem("Tools/切换平台/Android_Huawei", false, 102)]
        private static void SwitchToHuaweiAndroid()
        {
            SwitchPlatform(BuildTarget.Android, Channel.HuaweiAndroid);
        }

        [MenuItem("Tools/自动化打包/Android/华为/Debug", false, 120)]
        private static void BuildHuaweiDebug()
        {
            BuildAndroid(new BuildParam()
            {
                BuildVersion = "1.0.0",
                IsBuildRelease = false,
                IsBuildShowLog = true,
                Channel = Channel.HuaweiAndroid
            });
        }

        [MenuItem("Tools/自动化打包/Android/华为/Release", false, 121)]
        private static void BuildHuaweiRelease()
        {
            BuildAndroid(new BuildParam()
            {
                BuildVersion = "1.0.0",
                IsBuildRelease = true,
                IsBuildShowLog = false,
                Channel = Channel.HuaweiAndroid
            });
        } 
        
        
        /// <summary>
        /// 清空 Assets/PlatformPlugins/Huawei/ 下的所有内容，但保留目录本身
        /// </summary>
        private static void CleanHuaweiFolder()
        {
            string huaweiPath = Path.Combine(Application.dataPath, "PlatformPlugins", "Huawei");
            if (!Directory.Exists(huaweiPath))
            {
                Directory.CreateDirectory(huaweiPath);
                return;
            }

            // 删除所有子目录和文件
            foreach (var dir in Directory.GetDirectories(huaweiPath))
            {
                Directory.Delete(dir, true); // true 表示递归删除
            }
            foreach (var file in Directory.GetFiles(huaweiPath))
            {
                File.Delete(file);
            }
            Debug.Log($"已清理 Huawei 文件夹: {huaweiPath}");
        }
        
        
        /// <summary>
        /// 如果 Assets/PlatformPlugins/Huawei/ 为空，则从项目根目录的 PlatformPlugins/Huawei/ 复制内容
        /// </summary>
        private static void CopyHuaweiFolderIfEmpty()
        {
            string targetHuaweiPath = Path.Combine(Application.dataPath, "PlatformPlugins", "Huawei");
            string sourceHuaweiPath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, // 项目根目录（Assets 的父级）
                "PlatformPlugins",
                "Huawei"
            );

            // 确保目标目录存在
            if (!Directory.Exists(targetHuaweiPath))
                Directory.CreateDirectory(targetHuaweiPath);

            // 检查目标是否为空（无任何文件和子目录）
            bool isEmpty = Directory.GetFiles(targetHuaweiPath, "*", SearchOption.AllDirectories).Length == 0;
            if (!isEmpty)
            {
                Debug.Log("Huawei 文件夹非空，跳过拷贝");
                return;
            }

            // 检查源是否存在
            if (!Directory.Exists(sourceHuaweiPath))
            {
                Debug.LogWarning($"源 Huawei 文件夹不存在: {sourceHuaweiPath}，跳过拷贝");
                return;
            }

            // 执行拷贝（覆盖目标）
            CopyDirectory(sourceHuaweiPath, targetHuaweiPath);
            Debug.Log($"已从 {sourceHuaweiPath} 拷贝内容到 {targetHuaweiPath}");
        }

        
        private static void ApplyPlatformHuaweiAndroidSettings(Channel channel)
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
