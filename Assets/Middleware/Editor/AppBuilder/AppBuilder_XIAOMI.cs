
using System.IO;
using UnityEngine;
using UnityEditor;

namespace Middleware
{
    public partial class AppBuilder
    {
        [MenuItem("Tools/切换平台/Android_Xiaomi", false, 102)]
        private static void SwitchToXiaomiAndroid()
        {
            SwitchPlatform(BuildTarget.Android, Channel.Xiaomi);
        }
        
        [MenuItem("Tools/自动化打包/Android/小米/Debug", false, 140)]
        private static void BuildXiaomiDebug() => BuildAndroid(new BuildParam { BuildVersion = "1.0.0",  IsBuildRelease = false, IsBuildShowLog = true, Channel = Channel.Xiaomi });
        [MenuItem("Tools/自动化打包/Android/小米/Release", false, 141)]
        private static void BuildXiaomiRelease() => BuildAndroid(new BuildParam { BuildVersion = "1.0.0",  IsBuildRelease = true, IsBuildShowLog = false, Channel = Channel.Xiaomi });

        
        /// <summary>
        /// 清空 Assets/PlatformPlugins/Xiaomi/ 下的所有内容，但保留目录本身
        /// </summary>
        private static void CleanXiaomiFolder()
        {
            string xiaomiPath = Path.Combine(Application.dataPath, "PlatformPlugins", "Xiaomi");
            if (!Directory.Exists(xiaomiPath))
            {
                Directory.CreateDirectory(xiaomiPath);
                return;
            }

            // 删除所有子目录和文件
            foreach (var dir in Directory.GetDirectories(xiaomiPath))
            {
                Directory.Delete(dir, true); // true 表示递归删除
            }
            foreach (var file in Directory.GetFiles(xiaomiPath))
            {
                File.Delete(file);
            }
            Debug.Log($"已清理 Xiaomi 文件夹: {xiaomiPath}");
        }
        
        
        /// <summary>
        /// 如果 Assets/PlatformPlugins/Huawei/ 为空，则从项目根目录的 PlatformPlugins/Huawei/ 复制内容
        /// </summary>
        private static void CopyXiaomiFolderIfEmpty()
        {
            string targetXiaomiPath = Path.Combine(Application.dataPath, "PlatformPlugins", "Xiaomi");
            string sourceXiaomiPath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, // 项目根目录（Assets 的父级）
                "PlatformPlugins",
                "Xiaomi"
            );

            // 确保目标目录存在
            if (!Directory.Exists(targetXiaomiPath))
                Directory.CreateDirectory(targetXiaomiPath);

            // 检查目标是否为空（无任何文件和子目录）
            bool isEmpty = Directory.GetFiles(targetXiaomiPath, "*", SearchOption.AllDirectories).Length == 0;
            if (!isEmpty)
            {
                Debug.Log("Xiaomi 文件夹非空，跳过拷贝");
                return;
            }

            // 检查源是否存在
            if (!Directory.Exists(sourceXiaomiPath))
            {
                Debug.LogWarning($"源 Xiaomi 文件夹不存在: {sourceXiaomiPath}，跳过拷贝");
                return;
            }

            // 执行拷贝（覆盖目标）
            CopyDirectory(sourceXiaomiPath, targetXiaomiPath);
            Debug.Log($"已从 {sourceXiaomiPath} 拷贝内容到 {targetXiaomiPath}");
        }
        
        
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
