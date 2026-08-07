using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace PangleAdapterScripts.Editor
{
    public static class IosProcessScripts
    {
        private const string FileExtension = ".xcconfig";
        private static readonly string[] BULibs = new string[]
        {"-framework \"CSJAdSDK\"",
            "-framework \"BUAdSDK\"",
            "\"${PODS_ROOT}/BUAdSDK/BUAdSDK\"",
            "\"${PODS_ROOT}/Ads-CN/SDK\"",
            "\"${PODS_ROOT}/BURelyFoundation/Frameworks/SDK/BURelyFoundationSDWebImage\"",
            "\"${PODS_ROOT}/BURelyFoundation/Frameworks/SDK/BURelyFoundationAFN\"",
            "\"${PODS_ROOT}/BURelyFoundation/Frameworks/SDK/BURelyFoundationGecko\"",
            "\"${PODS_ROOT}/BURelyFoundation/Frameworks/SDK/BURelyFoundationFoundation\"",
            "\"${PODS_ROOT}/BURelyFoundation/Frameworks/SDK/BURelyFoundationHeader\"",
            "\"${PODS_ROOT}/BURelyFoundation/Frameworks/SDK/BURelyFoundationNETWork\"",
            "\"${PODS_ROOT}/BURelyFoundation/Frameworks/SDK/BURelyFoundationYYModel\"",
            "\"${PODS_ROOT}/BURelyFoundation/Frameworks/SDK/BURelyFoundationZFPlayer\"",
            "\"${PODS_ROOT}/BURelyFoundation/Frameworks/SDK/BURelyFoundationZip\"",
            "\"${PODS_ROOT}/BURelyFoundation/Frameworks/SDK/HM\"",
            "\"${PODS_ROOT}/Headers/Public/BURelyFoundation\"",
            "-l\"BURelyFoundationAFN\"",
            "-l\"BURelyFoundationFoundation\"",
            "-l\"BURelyFoundationGecko\"",
            "-l\"BURelyFoundationHeader\"",
            "-l\"BURelyFoundationNETWork\"",
            "-l\"BURelyFoundationSDWebImage\"",
            "-l\"BURelyFoundationYYModel\"",
            "-l\"BURelyFoundationZFPlayer\"",
            "-l\"BURelyFoundationZip\"",
            "-l\"HM\""
        };
        [PostProcessBuild(10002)]
        public static void OnPostprocessBuild(BuildTarget buildTarget, string path)
        {
            var pods = new DirectoryInfo(Path.Combine(path, "Pods"));
            var targetSupportFiles =
                new DirectoryInfo(Path.Combine(pods.FullName, "Target Support Files"));

            if (!targetSupportFiles.Exists)
            {
                Debug.Log("CSJM_Unity "+$"directory {targetSupportFiles.FullName} not exist");
                return;
            }

            var infos = GETFiles(targetSupportFiles);

            foreach (var fileInfo in infos)
            {
                if (fileInfo.DirectoryName.Contains("UnityFramework"))
                {
                    Debug.Log("CSJM_Unity "+"Skip "+ fileInfo.Name);
                    continue;
                }
                Debug.Log("CSJM_Unity "+"Processing "+ fileInfo.Name);
                ModifyFile(fileInfo);
            }
        }

        private static List<FileInfo> GETFiles(DirectoryInfo directoryInfo)
        {
            if (!directoryInfo.Exists)
            {
                return null;
            }

            var fileList = new List<FileInfo>();
            GETFiles(directoryInfo, fileList);
            return fileList;
        }

        private static void GETFiles(DirectoryInfo directoryInfo, List<FileInfo> infos)
        {
            if (!directoryInfo.Exists)
            {
                Debug.Log("CSJM_Unity "+$"{directoryInfo.Name} not exist");
                return;
            }

            infos.AddRange(directoryInfo.GetFiles().Where(fileInfo => fileInfo.Extension == FileExtension));

            foreach (var directory in directoryInfo.GetDirectories())
            {
                GETFiles(directory, infos);
            }
        }


        private static void ModifyFile(FileInfo fileInfo)
        {
            if (!fileInfo.Exists) return;
            var str = File.ReadAllText(fileInfo.FullName);

            foreach (string libs in BULibs)
            {
                str = str.Replace(libs, "");
            }
            File.WriteAllText(fileInfo.FullName, str);
        }
    }
}
