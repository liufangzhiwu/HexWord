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
    private string currentVersionInfo; // 显示当前版本信息
    private string oldVersionInfo; // 之前版本信息

    [MenuItem("Tools/资源打包/AssetBundle Builder")]
    public static void ShowWindow()
    {
        GetWindow<AssetBundleBuilder>("AssetBundle Builder");
    }

    private void OnGUI()
    {
        GUILayout.Label("AssetBundle Builder", EditorStyles.boldLabel);
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
        DisplayVersionInfo("当前资源版本:", currentVersionInfo);

        oldVersionInfo = GetCurrentVersionInfo(outputPath);
        DisplayVersionInfo("旧资源版本:", oldVersionInfo);

        if (GUILayout.Button("构建 热更包AssetBundles"))
        {
            BuildAssetBundles(hotfix: true);
        }

        if (GUILayout.Button("构建 整包AssetBundles"))
        {
            BuildAssetBundles(hotfix: false);
        }
    }

    private void DisplayVersionInfo(string label, string versionInfo)
    {
        GUILayout.Label(label, EditorStyles.boldLabel);
        EditorGUILayout.TextArea(string.IsNullOrEmpty(versionInfo) ? "没有可用的版本信息" : versionInfo, GUILayout.Height(200), GUILayout.ExpandWidth(true));
    }

    private void BuildAssetBundles(bool hotfix)
    {
        if (!Directory.Exists(folderPath))
        {
            Debug.LogError($"The folder {folderPath} does not exist.");
            return;
        }

        string[] assetTypes = new[] { "*.spriteatlas", "*.prefab", "*.csv", "*.mp4", "*.txt", "*.wav", "*.unity", "*.asset", "*.ttf","*.mat" };
        var assetPaths = assetTypes.SelectMany(assetType => Directory.GetFiles(folderPath, assetType, SearchOption.AllDirectories)).ToArray();
        var bundleNames = GetBundleNames(assetPaths);
        SetAssetBundleNames(assetPaths, bundleNames);

        //if (hotfix)
        //{
        //    CleanOldBuildFiles(hotfixPath); // 清理热更资源路径
        //    Directory.CreateDirectory(hotfixPath); // 创建热更资源路径
        //}

        var oldHashes = LoadOldHashes(Path.Combine(outputPath, "version.json"));
        var bundlesToHotfix = new List<BundleInfo>();
        var bundles = new List<BundleInfo>();

        // 构建 AssetBundles
        foreach (var bundleName in AssetDatabase.GetAllAssetBundleNames())
        {
            string bundlePath = Path.Combine(outputPath, bundleName);
            if (File.Exists(bundlePath))
            {
                string hash = ComputeFileHash(bundlePath);
                if (hotfix)
                {
                    oldHashes.TryGetValue(bundleName, out string oldHash);
                    if (oldHash != hash)
                    {
                        // 资源 Hash 不同，添加到热更列表
                        bundlesToHotfix.Add(new BundleInfo { name = bundleName, version = Application.version, hash = hash });
                        Debug.Log($"Changes detected for: {bundleName}. Added to hotfix.");
                    }
                }                
                else
                {
                    // 整包添加到资源列表
                    bundles.Add(new BundleInfo { name = bundleName, version = Application.version, hash = hash });
                }
            }
        }
      
        // 打包 AssetBundles
        BuildAssetBundles(outputPath);

        EncryptAssetBundle(outputPath);

        // 更新版本信息
        UpdateVersionFile(bundles, outputPath);
        CopyAssetBundlesToStreamingAssets(outputPath);
        Debug.Log("AssetBundles built successfully.");

        // 更新当前版本信息
        currentVersionInfo = GetCurrentVersionInfo(outputPath);
    }

    /// <summary>
    /// 加密指定路径下的 AssetBundle 文件。
    /// </summary>
    /// <param name="bundlePath">要加密的 AssetBundle 文件的路径。</param>
    public void EncryptAssetBundle(string bundlePath)
    {
        // 获取所有文件
        string[] files = Directory.GetFiles(bundlePath);
        if (files.Length == 0)
        {
            Debug.LogWarning("未找到任何 AssetBundle 文件在路径: " + bundlePath);
            return;
        }

        foreach (var file in files)
        {
            try
            {
                // 读取 AssetBundle 文件
                byte[] data = File.ReadAllBytes(file);
                // 加密数据
                byte[] encryptedData = SecurityProvider.SecureBytes(data);
                // 保存加密后的文件
                File.WriteAllBytes(file, encryptedData);
                Debug.Log("AssetBundle 加密完成: " + file);
            }
            catch (Exception ex)
            {
                Debug.LogError($"加密文件 {file} 时出错: {ex.Message}");
            }
        }
       
    }
    

    private static Dictionary<(string DirectoryName, string FileType), string> GetBundleNames(string[] assetPaths)
    {
        return assetPaths.Select(assetPath => (
            DirectoryName: Path.GetFileName(Path.GetDirectoryName(assetPath)),
            FileType: Path.GetExtension(assetPath).TrimStart('.')
        ))
        .GroupBy(asset => asset)
        .ToDictionary(g => g.Key, g => $"{g.Key.DirectoryName}");
        //.ToDictionary(g => g.Key, g => $"{g.Key.DirectoryName}_{g.Key.FileType}");
    }

    /// <summary>
    /// 设置资源
    /// </summary>
    /// <param name="assetPaths"></param>
    /// <param name="bundleNames"></param>
    private static void SetAssetBundleNames(string[] assetPaths, Dictionary<(string DirectoryName, string FileType), string> bundleNames)
    {
        foreach (string assetPath in assetPaths)
        {
            string relativePath = assetPath.Substring(assetPath.IndexOf("Assets"));
            string parentFolderName = Path.GetFileName(Path.GetDirectoryName(assetPath));
            string fileType = Path.GetExtension(assetPath).TrimStart('.');

            if (bundleNames.TryGetValue((DirectoryName: parentFolderName, FileType: fileType), out string assetBundleName))
            {
                AssetImporter assetImporter = AssetImporter.GetAtPath(relativePath);
                if (assetImporter != null)
                {
                    assetImporter.SetAssetBundleNameAndVariant(assetBundleName, "");
                    Debug.Log($"Set AssetBundle for {relativePath} to {assetBundleName}");
                }
                else
                {
                    Debug.LogError($"Failed to get AssetImporter for {relativePath}");
                }
            }
        }
    }

    private static void CleanOldBuildFiles(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
            Debug.Log("Deleted old build files.");
        }
    }

    private static void BuildAssetBundles(string outputPath)
    {
        CleanOldBuildFiles(outputPath);
        Directory.CreateDirectory(outputPath);
       
        BuildAssetBundleOptions options = BuildAssetBundleOptions.ChunkBasedCompression | 
                                          BuildAssetBundleOptions.CollectDependencies | 
                                          BuildAssetBundleOptions.DeterministicAssetBundle;
        
        // 获取当前选择的平台
        BuildTarget targetPlatform = EditorUserBuildSettings.activeBuildTarget;
        
        // 构建 AssetBundles
        BuildPipeline.BuildAssetBundles(outputPath, options, targetPlatform);
        //BuildPipeline.BuildAssetBundles(outputPath, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.CollectDependencies | BuildAssetBundleOptions.DeterministicAssetBundle, BuildTarget.Android);
    }

    private static void CopyAssetBundlesToStreamingAssets(string sourcePath)
    {
        string streamingAssetsPath = Path.Combine(Application.dataPath, "StreamingAssets");

        if (!Directory.Exists(streamingAssetsPath))
        {
            Directory.CreateDirectory(streamingAssetsPath);
        }

        foreach (var file in Directory.GetFiles(sourcePath))
        {
            string fileName = Path.GetFileName(file);
            string destinationFile = Path.Combine(streamingAssetsPath, fileName);
            File.Copy(file, destinationFile, true);
            Debug.Log($"Copied {fileName} to StreamingAssets.");
        }
    }

    /// <summary>
    /// 写入版本文件
    /// </summary>
    /// <param name="bundleInfos"></param>
    /// <param name="path"></param>
    private static void UpdateVersionFile(List<BundleInfo> bundleInfos,string path)
    {        
        var versionData = new VersionData { bundles = bundleInfos.ToArray() };
        string json = JsonConvert.SerializeObject(versionData, Formatting.Indented);
        //加密    
        string EnJson = SecurityProvider.ProtectData(json);
        File.WriteAllText(Path.Combine(path, "version.json"), EnJson);
        Debug.Log("Version file updated.");
    }

    //通过MD5生成哈希值来标识当前文件内容的唯一
    private static string ComputeFileHash(string filePath)
    {
        using (var md5 = MD5.Create())
        using (var stream = File.OpenRead(filePath))
        {
            var hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }

    private static Dictionary<string, string> LoadOldHashes(string versionFilePath)
    {
        if (!File.Exists(versionFilePath))
            return new Dictionary<string, string>();
        //解密
        string data = SecurityProvider.RestoreData(File.ReadAllText(versionFilePath));
        var versionData = JsonConvert.DeserializeObject<VersionData>(data);
        return versionData.bundles.ToDictionary(bundle => bundle.name, bundle => bundle.hash);
    }

    // 获取当前版本信息
    private string GetCurrentVersionInfo(string path)
    {
        //解密
        if (File.Exists(Path.Combine(path, "version.json")))
        {
            string data = SecurityProvider.RestoreData(File.ReadAllText(Path.Combine(path, "version.json")));
            var versionData = JsonConvert.DeserializeObject<VersionData>(data);
            //var versionData = JsonConvert.DeserializeObject<VersionData>(File.ReadAllText(Path.Combine(path, "version.json")));
            return string.Join("\n", versionData.bundles.Select(b => $"{b.name}: {b.version}, Hash: {b.hash}"));
        }
        return "没有可用的版本信息";
    }
}