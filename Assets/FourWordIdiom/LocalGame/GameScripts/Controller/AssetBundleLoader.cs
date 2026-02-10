using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.U2D;
using UnityEngine.Video;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 高级资源包管理系统 (预加载+同步接口版)
/// 核心逻辑：
/// 1. 游戏启动时调用 PreloadBundles 异步下载所有资源并常驻内存。
/// 2. 游戏进行中调用 LoadGameObject 等方法同步直接获取资源。
/// </summary>
public sealed class AssetBundleLoader
{
    #region 单例实现

    private static readonly Lazy<AssetBundleLoader> _instance =
        new Lazy<AssetBundleLoader>(() => new AssetBundleLoader());

    public static AssetBundleLoader SharedInstance => _instance.Value;

    private AssetBundleLoader()
    {
    }

    #endregion

    #region 资源缓存容器
    // 已加载的资源包缓存 [BundleName -> AssetBundle]
    private Dictionary<string, AssetBundle> _loadedBundles = new Dictionary<string, AssetBundle>();

    // 🔥 新增：真实文件名映射表 (逻辑名 -> 带Hash的物理名)
    // 例如: "gameinfo" -> "gameinfo_5d4s5d4s5d..."
    private Dictionary<string, string> _realBundleNames = new Dictionary<string, string>();

    // 是否已初始化 Manifest
    public bool IsManifestLoaded { get; private set; } = false;

    // Manifest 文件的名字 (通常等于你打包输出的文件夹名字，AssetBundleBuilder里是 "BuildBundles")
    // 如果你不知道，去打包目录下看那个没后缀的文件叫什么
    private const string MANIFEST_NAME = "BuildBundles";

    private string SourceBundlePath =>
        Path.Combine(Application.streamingAssetsPath, "Res/"); // 假设放在 StreamingAssets/Res

    private string LocalCachePath => Application.persistentDataPath; // 假设放在 StreamingAssets/Res

    #endregion

    #region 第一阶段：异步预加载 (必须在 Loading 界面调用)

    public async Task DownloadToCacheOnly(string bundleName)
    {
        string realFileName = GetRealFileName(bundleName);
        string sourceUrl = Path.Combine(SourceBundlePath, realFileName);
#if UNITY_EDITOR
        // 编辑器下 Path.Combine 可能产生反斜杠，UnityWebRequest 不喜欢
        sourceUrl = "file://" + sourceUrl.Replace("\\", "/");
#endif
        string savePath = Path.Combine(LocalCachePath, realFileName);
        string tempPath = savePath + ".temp"; // 🔥 先下载到 .download 临时文件

        // 1. 如果正式文件已存在，跳过
        // if (File.Exists(savePath)) return;

        // 2. 如果有残留的临时文件，先删掉
        if (File.Exists(tempPath)) File.Delete(tempPath);

        Debug.Log($"[下载] 源: {sourceUrl} -> 存: {savePath}");

        using (UnityWebRequest uwr = new UnityWebRequest(sourceUrl, UnityWebRequest.kHttpVerbGET))
        {
            // 下载到临时路径
            uwr.downloadHandler = new DownloadHandlerBuffer();

            var operation = uwr.SendWebRequest();
            while (!operation.isDone) await Task.Yield();

            if (uwr.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[下载失败] {bundleName}: {uwr.error}");
                // 🔥 失败了务必删除临时文件，防止占空间或下次误读
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
            else
            {
                // ✅ 下载成功且完整：将临时文件重命名为正式文件
                try
                {
                    byte[] encryptedData = uwr.downloadHandler.data;
                    byte[] decryptedData = SecurityProvider.RecoverBytes(encryptedData);
                    // if (File.Exists(tempPath)) File.Delete(tempPath);
                    File.WriteAllBytes(tempPath, decryptedData);
                    if (File.Exists(savePath)) File.Delete(savePath); // 双重保险
                    File.Move(tempPath, savePath);
                    Debug.Log($"[{bundleName}] 下载并保存完成 {savePath}");
                    encryptedData = null;
                    decryptedData = null;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[文件操作] 重命名失败: {e.Message}");
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
            }
        }
        // using 结束会自动 Dispose，释放 uwr 占用的微小内存
    }

    // -----------------------------------------------------------
    // 新增：加载单个资源包的方法 (公开)
    // -----------------------------------------------------------
    public async Task PreloadSingleBundle(string bundleName)
    {
        if (!IsManifestLoaded)
        {
            await LoadManifest();
        }

        bundleName = bundleName.ToLower();
        if (!_loadedBundles.ContainsKey(bundleName))
        {
            // 复用之前的内部加载逻辑
            await LoadBundleAsyncInternal(bundleName);
        }
    }

    /// <summary>
    /// 【核心入口】预加载列表中的所有资源包
    /// </summary>
    /// <param name="bundleNames">资源包名称列表</param>
    public async Task PreloadBundles(List<string> bundleNames)
    {
        if (!IsManifestLoaded) await LoadManifest();

        int total = bundleNames.Count;
        for (int i = 0; i < total; i++)
        {
            string name = bundleNames[i].ToLower(); // 统一转小写

            // 如果缓存里没有，才去下载
            if (!_loadedBundles.ContainsKey(name))
            {
                await LoadBundleAsyncInternal(name);
            }
        }
    }

    /// <summary>
    /// 内部异步加载逻辑 (包含下载+解密)
    /// </summary>
    private async Task LoadBundleAsyncInternal(string bundleName)
    {
        if (_loadedBundles.ContainsKey(bundleName)) return;

        string realFileName = GetRealFileName(bundleName);
#if UNITY_EDITOR
        string cachePath = Path.Combine(SourceBundlePath, realFileName); // 真机缓存路径
#else
        string cachePath = Path.Combine(LocalCachePath, realFileName); // 真机缓存路径
#endif
        if (!File.Exists(cachePath))
        {
            await DownloadToCacheOnly(bundleName);
        }

        // 如果下载完还是没有，说明失败了
        if (!File.Exists(cachePath)) return;
        try
        {
#if UNITY_EDITOR
            byte[] decryptedData = await Task.Run(() =>
                    SecurityProvider.RecoverBytes(File.ReadAllBytes(cachePath)));
            AssetBundle bundle = AssetBundle.LoadFromMemory(decryptedData);
#else
                 // LoadFromFile 内存占用最低，是微信小游戏的首选
                 AssetBundle bundle = AssetBundle.LoadFromFile(cachePath);
#endif
            if (bundle != null)
            {
                _loadedBundles[bundleName] = bundle;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Load] 文件加载失败，可能已损坏: {bundleName} {e}");
            // File.Delete(localPath); // 坏文件删掉
        }
    }

    #endregion

    #region 内部逻辑：Manifest 与 Hash 解析 (官方文档逻辑)

    public async Task LoadManifest()
    {
        string sourceUrl = Path.Combine(SourceBundlePath, MANIFEST_NAME); // 使用本地路径
#if UNITY_EDITOR
        // 🔥 编辑器下如果不是模拟模式，尝试直接从本地加载 Manifest
        if (File.Exists(sourceUrl))
        {
            var bundle = AssetBundle.LoadFromFile(sourceUrl);
            if (bundle != null)
            {
                var manifest = bundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
                ParseManifest(manifest);
                bundle.Unload(false);
                IsManifestLoaded = true;
                return;
            }
        }

        Debug.LogWarning("[Editor] 未找到 Manifest 文件，Hash 映射可能失效。");
        IsManifestLoaded = true; // 防止死循环
#endif
        // Manifest 文件本身不带 Hash，且不能被缓存（否则无法热更）
        // 这里的路径需要指向 Manifest 文件
        string uri = SourceBundlePath + MANIFEST_NAME;
        Debug.Log($"[Manifest] 开始下载清单: {uri}");

        using (UnityWebRequest request = new UnityWebRequest(uri, UnityWebRequest.kHttpVerbGET))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            // 尝试通过 Header 告诉 CDN 不要缓存 (部分 CDN 支持)
            request.SetRequestHeader("Cache-Control", "no-cache, no-store, must-revalidate");
            request.SetRequestHeader("Pragma", "no-cache");
            request.SetRequestHeader("Expires", "0");

            var op = request.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                byte[] data = request.downloadHandler.data;
                // 注意：Manifest通常不加密。如果你加密了，这里要解密。
                // 假设 Manifest 没有加密：
                AssetBundle manifestBundle = AssetBundle.LoadFromMemory(data);

                if (manifestBundle != null)
                {
                    AssetBundleManifest manifest = manifestBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
                    // 🔥 构建映射表
                    ParseManifest(manifest);
                    // 用完卸载
                    manifestBundle.Unload(false);
                    Debug.Log("[Manifest] 清单加载完毕，Hash 映射表已建立");
                }
            }
            else
                Debug.LogError($"[Manifest] 下载失败: {request.error}");

            IsManifestLoaded = true;
        }
    }

    // 将官方文档的解析逻辑集成进来
    private void ParseManifest(AssetBundleManifest manifest)
    {
        _realBundleNames.Clear();
        string[] allBundles = manifest.GetAllAssetBundles();

        foreach (var hashName in allBundles)
        {
            // 官方文档的正则：匹配 _hash32位.unity3d 或者单纯的 hash
            // 假设你的包名原本是 gameinfo，打包后变成 gameinfo_hash.unity3d
            // 我们需要把 gameinfo 提取出来作为 Key

            // 简易逻辑：假设你原来的包名不含 "_"，那么第一个 "_" 前面的就是逻辑名
            // 或者使用更稳健的逻辑：移除 Hash 后缀

            string logicName = hashName;

            // 移除 .unity3d 后缀
            // 移除 Hash 部分 (通常是最后32位字符)
            // 这里根据你的打包结果来定。如果你的包名是 gameinfo_hashstring
            // 可以简单地用 Split('_')[0] (如果你的包名本身不带下划线)

            // 下面是通用性较强的逻辑：
            // 假设文件名格式为: {Name}_{Hash}
            int lastUnderscore = hashName.LastIndexOf('_');
            if (lastUnderscore >= 0)
            {
                logicName = hashName.Substring(0, lastUnderscore);
            }

            // 🔥【关键】打印出来看看，key 到底变成了什么！
            // Debug.Log($"[Mapping] 映射关系: key[{logicName}] -> value[{hashName}]");

            if (!_realBundleNames.ContainsKey(logicName)) _realBundleNames.Add(logicName, hashName);
        }
    }

    private string GetRealFileName(string logicName)
    {
        // 如果是编辑器模式或没加载 Manifest，直接返回原名
        if (_realBundleNames.TryGetValue(logicName, out string hashName))
        {
            return hashName;
        }

        Debug.LogWarning($"[Hash] 未找到映射: {logicName}，将使用原始名称尝试下载");
        return logicName;
    }

    #endregion

    #region 第二阶段：同步资源获取接口 (业务逻辑调用这些)

    /// <summary>
    /// 同步加载 GameObject
    /// </summary>
    public GameObject LoadGameObject(string bundleName, string assetName)
    {
        bundleName = bundleName.ToLower();

        // 1. 编辑器模式快速加载
        if (ShouldUseEditorAssetDatabase())
        {
            var go = LoadFromEditorAsset<GameObject>(bundleName, assetName);
            if (go) return go;
        }

        // 2. 检查预加载缓存
        AssetBundle bundle = GetOrLoadBundleSync(bundleName);

        if (bundle != null)
        {
            return bundle.LoadAsset<GameObject>(assetName);
        }

        Debug.LogError($"[SyncLoad] 失败！资源包 '{bundleName}' 未预加载！请检查 Loading 流程。");
        return null;
    }

    /// <summary>
    /// 同步加载 TextAsset
    /// </summary>
    public TextAsset LoadTextFile(string bundleName, string assetName)
    {
        bundleName = bundleName.ToLower();
        if (ShouldUseEditorAssetDatabase())
        {
            var go = LoadFromEditorAsset<TextAsset>(bundleName, assetName);
            if (go) return go;
        }

        AssetBundle bundle = GetOrLoadBundleSync(bundleName);

        if (bundle != null)
        {
            return bundle.LoadAsset<TextAsset>(assetName);
        }

        Debug.LogError($"[SyncLoad] 失败！资源包 '{bundleName}' 未预加载！");
        return null;
    }

    /// <summary>
    /// 同步加载 Material
    /// </summary>
    public Material LoadMaterialResource(string bundleName, string assetName)
    {
        bundleName = bundleName.ToLower();
        if (ShouldUseEditorAssetDatabase())
        {
            var go = LoadFromEditorAsset<Material>(bundleName, assetName);
            if (go) return go;
        }

        AssetBundle bundle = GetOrLoadBundleSync(bundleName);

        if (bundle != null)
        {
            return bundle.LoadAsset<Material>(assetName);
        }

        Debug.LogError($"[SyncLoad] 失败！资源包 '{bundleName}' 未预加载！");
        return null;
    }

    /// <summary>
    /// 同步加载 Audio
    /// </summary>
    public AudioClip LoadAudioClip(string bundleName, string audioName)
    {
        bundleName = bundleName.ToLower();
        if (ShouldUseEditorAssetDatabase())
        {
            var go = LoadFromEditorAsset<AudioClip>(bundleName, audioName);
            if (go) return go;
        }

        AssetBundle bundle = GetOrLoadBundleSync(bundleName);

        if (bundle != null)
        {
            return bundle.LoadAsset<AudioClip>(audioName);
        }

        Debug.LogError($"[SyncLoad] 失败！资源包 '{bundleName}' {audioName} 未预加载！");
        return null;
    }

    /// <summary>
    /// 同步加载 ScriptableObject
    /// </summary>
    public ScriptableObject LoadScriptableObject(string bundleName, string assetName)
    {
        bundleName = bundleName.ToLower();
        if (ShouldUseEditorAssetDatabase())
        {
            var go = LoadFromEditorAsset<ScriptableObject>(bundleName, assetName);
            if (go) return go;
        }

        AssetBundle bundle = GetOrLoadBundleSync(bundleName);

        if (bundle != null)
        {
            return bundle.LoadAsset<ScriptableObject>(assetName);
        }

        Debug.LogError($"[SyncLoad] 失败！资源包 '{bundleName}' 未预加载！");
        return null;
    }

    /// <summary>
    /// 同步加载 Font
    /// </summary>
    public Font LoadFont(string bundleName, string fontName)
    {
        bundleName = bundleName.ToLower();
        if (ShouldUseEditorAssetDatabase())
        {
            var go = LoadFromEditorAsset<Font>(bundleName, fontName);
            if (go) return go;
        }

        AssetBundle bundle = GetOrLoadBundleSync(bundleName);

        if (bundle != null)
        {
            return bundle.LoadAsset<Font>(fontName);
        }

        Debug.LogError($"[SyncLoad] 失败！资源包 '{bundleName}' 未预加载！");
        return null;
    }

    #endregion

    #region 图集处理 (特殊逻辑)

    /// <summary>
    /// 加载图集 (同步)
    /// </summary>
    public SpriteAtlas LoadAtlas(string bundleName, string atlasName)
    {
        bundleName = bundleName.ToLower();

        if (ShouldUseEditorAssetDatabase())
        {
            return LoadFromEditorAsset<SpriteAtlas>(bundleName, atlasName);
        }

        // 3. 预加载检查
        AssetBundle bundle = GetOrLoadBundleSync(bundleName);

        if (bundle != null)
        {
            return bundle.LoadAsset<SpriteAtlas>(atlasName);
        }

        Debug.LogError($"[SyncLoad] 图集加载失败 '{atlasName}' @ Bundle:'{bundleName}' (可能未预加载)");
        return null;
    }

    /// <summary>
    /// 获取 Sprite (同步)
    /// </summary>
    public Sprite GetSpriteFromAtlas(string spriteName, string atlasName = "UI_Universal")
    {
        // 尝试获取图集 (如果之前 LoadAtlas 没调过，这里会尝试去 Bundle 里找，前提是 Bundle 已预加载)
        // 假设 atlasName 和 bundleName 是一样的，如果不一样，你需要传递 bundleName
        var atlas = LoadAtlas(atlasName, atlasName);

        if (atlas != null)
        {
            var sp = atlas.GetSprite(spriteName);
            if (sp == null) Debug.LogError($"图集 {atlasName} 中找不到 Sprite: {spriteName}");
            return sp;
        }

        return null;
    }

    public Sprite GetSpriteFromBundle(string bundleName, string spName)
    {
        bundleName = bundleName.ToLower();
        if (ShouldUseEditorAssetDatabase())
        {
            var go = LoadFromEditorAsset<Sprite>(bundleName, spName);
            if (go) return go;
        }

        AssetBundle bundle = GetOrLoadBundleSync(bundleName);

        if (bundle != null)
        {
            return bundle.LoadAsset<Sprite>(spName);
        }

        return null;
    }

    #endregion

    #region 辅助与管理

    private bool ShouldUseEditorAssetDatabase()
    {
#if UNITY_EDITOR
        return false;
#else
        return false;
#endif
    }

    private T LoadFromEditorAsset<T>(string bundleName, string assetName) where T : Object
    {
#if UNITY_EDITOR && !Unity_ResourceAb
        string[] paths = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName);
        foreach (var path in paths)
        {
            if (Path.GetFileNameWithoutExtension(path).Equals(assetName, StringComparison.OrdinalIgnoreCase))
                return AssetDatabase.LoadAssetAtPath<T>(path);
        }
#endif
        return null;
    }

    // ------------------------------------------------------
    // 🔥 新增：编辑器专用加载辅助函数
    // ------------------------------------------------------
    private T LoadAssetInEditor<T>(string assetName) where T : UnityEngine.Object
    {
#if UNITY_EDITOR
        // 1. 在项目中搜索该名字的资源
        string[] guids = AssetDatabase.FindAssets($"{assetName} t:{typeof(T).Name}");

        if (guids.Length == 0)
        {
            Debug.LogError($"[Editor] 未找到资源: {assetName}");
            return null;
        }

        // 2. 获取第一个匹配项的路径
        string path = AssetDatabase.GUIDToAssetPath(guids[0]);

        // 3. 加载
        return AssetDatabase.LoadAssetAtPath<T>(path);
#endif
        return null;
    }

    /// <summary>
    /// 卸载资源包
    /// </summary>
    public void ReleaseBundle(string bundleName, bool unload = false)
    {
        bundleName = bundleName.ToLower();
        if (_loadedBundles.TryGetValue(bundleName, out AssetBundle bundle))
        {
            bundle.Unload(unload);
            if (unload) _loadedBundles.Remove(bundleName);
            Debug.Log($"资源包已卸载: {bundleName}");
        }
    }

    /// <summary>
    /// 清空所有
    /// </summary>
    public void ClearAllResources()
    {
        foreach (var kvp in _loadedBundles)
        {
            if (kvp.Value != null) kvp.Value.Unload(true);
        }

        _loadedBundles.Clear();
        Debug.Log("所有资源已清理");
    }

    /// <summary>
    /// 🔥 核心辅助方法：尝试从内存获取，如果没有，尝试从本地硬盘同步加载
    /// </summary>
    private AssetBundle GetOrLoadBundleSync(string bundleName)
    {
        if (_loadedBundles.TryGetValue(bundleName, out AssetBundle bundle))
        {
            return bundle;
        }

        string realFileName = GetRealFileName(bundleName);

#if UNITY_EDITOR
        string finalPath = Path.Combine(SourceBundlePath, realFileName);
#else
        string finalPath = Path.Combine(LocalCachePath, realFileName);
#endif
        if (!File.Exists(finalPath)) return null;

        try
        {
#if UNITY_EDITOR
            byte[] fileData = File.ReadAllBytes(finalPath);
            byte[] decryptedData = SecurityProvider.RecoverBytes(fileData);
            // 同步从文件加载 AssetBundle
            bundle = AssetBundle.LoadFromMemory(decryptedData);
#else
                bundle = AssetBundle.LoadFromFile(finalPath);
#endif
            if (bundle != null)
            {
                // 加载成功后，务必存入缓存，防止下次重复 IO
                _loadedBundles.Add(bundleName, bundle);
                return bundle;
            }

            Debug.Log($" {bundleName} 加载后是空？");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SyncLoad] 硬盘文件存在但加载失败: {e.Message}");
            File.Delete(finalPath);
        }

        return null;
    }

    #endregion
}