using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using Newtonsoft.Json;
using System;

public class AssetBundleBuilder : EditorWindow
{
    [System.Serializable]
    public class BundleInfo
    {
        public string name;
        public string version;
        public string hash;
    }

    [System.Serializable]
    public class VersionData
    {
        public BundleInfo[] bundles;
    }
    
    private static string folderPath = "Assets/FourWordIdiom/MultipleData"; // 默认路径
    private static string outputPath = "./BuildBundles"; // 确保此路径有效
    //private static string hotfixPath = "./HotfixBundles"; // 热更资源输出路径
    private static string currentVersionInfo; // 显示当前版本信息
    private static string oldVersionInfo; // 之前版本信息

    // [MenuItem("Tools/资源打包/AssetBundle Builder", false, 0)]
    // public static void ShowWindow()
    // {
    //     GetWindow<AssetBundleBuilder>("AssetBundle Builder");
    // }
    
    [MenuItem("Tools/资源打包/构建微信AB包 (官方Hash方案)")]
    public static void ShowWeChat()
    {
        GetWindow<AssetBundleBuilder>("AB Builder");
    }
    private void OnGUI()
    {
        GUILayout.Label("微信官方缓存方案打包器", EditorStyles.boldLabel);
        GUILayout.Label("选择资源文件夹:");
        folderPath = EditorGUILayout.TextField("资源路径:", folderPath);

        if (GUILayout.Button("选择文件夹"))
        {
            string path = EditorUtility.OpenFolderPanel("选择资源文件夹", folderPath, "");
            if (!string.IsNullOrEmpty(path))
            {
                folderPath = path.Replace("\\", "/"); // 统一路径分隔符
            }
        }

        GUILayout.Label("输出路径:");
        outputPath = EditorGUILayout.TextField("输出路径:", outputPath);
        
        if (GUILayout.Button("构建 AssetBundles (带 Hash)"))
        {
            BuildAssetBundles();
        }
    }

    private void DisplayVersionInfo(string label, string versionInfo)
    {
        GUILayout.Label(label, EditorStyles.boldLabel);
        EditorGUILayout.TextArea(string.IsNullOrEmpty(versionInfo) ? "没有可用的版本信息" : versionInfo, GUILayout.Height(200), GUILayout.ExpandWidth(true));
    }

    public static void BuildAssetBundles()
    {
        if (!Directory.Exists(folderPath))
        {
            Debug.LogError($"The folder {folderPath} does not exist.");
            return;
        }

        SetAssetBundleNames();

        if(Directory.Exists(outputPath)) Directory.Delete(outputPath, true);
        Directory.CreateDirectory(outputPath);

        BuildAssetBundleOptions options = BuildAssetBundleOptions.ChunkBasedCompression |
                                          BuildAssetBundleOptions.CollectFileDependencies |
                                          // BuildAssetBundleOptions.DeterministicAssetBundle |
                                          BuildAssetBundleOptions.AppendHashToAssetBundleName;

        AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(outputPath, options, BuildTarget.MiniGame);

        if (manifest == null)
        {
            Debug.LogError("打包失败！");
            return;
        }
        
        EncryptFiles(outputPath, manifest);
        
        CopyAssetBundlesToStreamingAssets(outputPath);
        
        Debug.Log("打包完成！请将 BuildBundles 文件夹上传至 CDN。");
        Debug.Log("注意：Manifest 文件(BuildBundles)不要开启缓存，其他文件开启缓存。");
        AssetDatabase.Refresh();
    }
    
    private static void EncryptFiles(string path, AssetBundleManifest manifest)
    {
        string[] allBundles = manifest.GetAllAssetBundles();
        foreach (string bundleName in allBundles)
        {
            string filePath = Path.Combine(path, bundleName);
            if (File.Exists(filePath))
            {
                try
                {
                    byte[] data = File.ReadAllBytes(filePath);
                    byte[] encryptedData = SecurityProvider.SecureBytes(data);
                    File.WriteAllBytes(filePath, encryptedData);
                    Debug.Log($"已加密: {bundleName}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"加密失败 {bundleName}: {ex.Message}");
                }
            }
        }
    }
    
    /// <summary>
    /// 设置资源
    /// </summary>
    /// <param name="assetPaths"></param>
    /// <param name="bundleNames"></param>
    private static void SetAssetBundleNames()
    {
                
        string[] assetTypes = new[] { "*.spriteatlas", "*.prefab", "*.csv", "*.mp4", "*.txt", "*.wav", "*.unity", "*.asset", "*.ttf","*.mat" };
        var assetPaths = assetTypes.SelectMany(assetType => Directory.GetFiles(folderPath, assetType, SearchOption.AllDirectories)).ToArray();
        
        foreach (string p in assetPaths)
        {
           string path = p.Replace("\\", "/");
           AssetImporter ai = AssetImporter.GetAtPath(path);
           if (ai != null)
           {
               string dirName = Path.GetFileName(Path.GetDirectoryName(path))?.ToLower();
               ai.SetAssetBundleNameAndVariant(dirName, "");
           }
        }

        AssetDatabase.RemoveUnusedAssetBundleNames();
    }

    private static void CopyAssetBundlesToStreamingAssets(string sourcePath)
    {
        var streamingAssetsPath = Path.Combine(Application.dataPath, "StreamingAssets","Res");
        if (Directory.Exists(streamingAssetsPath))
            Directory.Delete(streamingAssetsPath,true);
        Directory.CreateDirectory(streamingAssetsPath);

        foreach (var file in Directory.GetFiles(sourcePath))
        {
            string fileName = Path.GetFileName(file);
            string destinationFile = Path.Combine(streamingAssetsPath, fileName);
            File.Copy(file, destinationFile, true);
        }
        AssetDatabase.Refresh();
    }
}