using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.U2D;
using UnityEngine.Video;
using Object = UnityEngine.Object;

/// <summary>
/// 高级资源包管理系统 (非MonoBehaviour版本)
/// 主要功能：
/// 1. 加密资源包加载与解密
/// 2. 多平台路径自动处理
/// 3. 资源缓存与内存管理
/// 4. 支持多种资源类型加载
/// 5. 线程安全的单例实现
/// </summary>
public sealed class AdvancedBundleLoader
{
    #region 单例实现
    private static readonly Lazy<AdvancedBundleLoader> _instance = 
        new Lazy<AdvancedBundleLoader>(() => new AdvancedBundleLoader());
    
    public static AdvancedBundleLoader SharedInstance => _instance.Value;
    
    private AdvancedBundleLoader() { }
    #endregion

    #region 资源缓存
    // 已加载的资源包缓存字典 [资源包名:资源包实例]
    private Dictionary<string, AssetBundle> _loadedBundles = new Dictionary<string, AssetBundle>();
    
    // 图集资源缓存字典 [图集名:图集实例]
    private Dictionary<string, SpriteAtlas> _spriteAtlasCache = new Dictionary<string, SpriteAtlas>();
    
    // 当前加载的视频资源
    public VideoClip CurrentVideo { get; private set; }
    #endregion

    #region 资源路径处理
    /// <summary>
    /// 获取平台适配的资源请求路径
    /// </summary>
    /// <param name="fileName">资源文件名</param>
    /// <returns>完整资源路径</returns>
    private string GetPlatformAdaptedPath(string fileName)
    {
        string fullPath = Path.Combine(Application.streamingAssetsPath, "Res", fileName);

        // 平台特定路径处理
        switch (Application.platform)
        {
            case RuntimePlatform.Android:
                // Android平台特殊处理
                break;
                
            case RuntimePlatform.WindowsPlayer:
            case RuntimePlatform.WindowsEditor:
                fullPath = "file:///" + fullPath;
                break;
                
            case RuntimePlatform.OSXPlayer:
            case RuntimePlatform.OSXEditor:
            case RuntimePlatform.IPhonePlayer:
                fullPath = "file://" + fullPath;
                break;
        }

        return fullPath;
    }
    #endregion

    #region 核心加载逻辑
    /// <summary>
    /// 从StreamingAssets加载原始字节数据
    /// </summary>
    /// <param name="bundleName">资源包名称</param>
    /// <returns>资源字节数组</returns>
    private byte[] LoadRawBundleData(string bundleName)
    {
        string uri = GetPlatformAdaptedPath(bundleName);
        Debug.Log($"资源加载路径: {uri}");
        
        using (UnityWebRequest request = UnityWebRequest.Get(uri))
        {
            var operation = request.SendWebRequest();
            
            // 同步等待请求完成
            while (!operation.isDone) { /* 等待 */ }

            if (request.result == UnityWebRequest.Result.ConnectionError || 
                request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"资源加载错误: {request.error}");
                return null;
            }
            
            return request.downloadHandler.data;
        }
    }
    
    /// <summary>
    /// 加载并解密加密的资源包
    /// </summary>
    /// <param name="encryptedData">加密的资源数据</param>
    /// <returns>解密后的资源包</returns>
    private AssetBundle LoadDecryptedBundle(byte[] encryptedData)
    {
        // 解密过程
        byte[] decryptedData = SecurityProvider.RecoverBytes(encryptedData);
        
        // 从内存加载解密后的资源包
        return AssetBundle.LoadFromMemory(decryptedData);
    }

    /// <summary>
    /// 核心资源包加载方法
    /// </summary>
    /// <param name="bundleName">资源包名称</param>
    /// <returns>加载的资源包实例</returns>
    private AssetBundle LoadBundleInternal(string bundleName)
    {
        if (!_loadedBundles.ContainsKey(bundleName))
        {
            byte[] rawData = LoadRawBundleData(bundleName);
            AssetBundle bundle = LoadDecryptedBundle(rawData);
            _loadedBundles[bundleName] = bundle;
        }
        return _loadedBundles[bundleName];
    }

    private T LoadFromEditorAsset<T>(string bundleName, string assetName) where T : Object
    {
#if UNITY_EDITOR && !Unity_ResourceAb
        var paths = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName);
        foreach (var path in paths)
        {
            if (Path.GetFileNameWithoutExtension(path).Equals(assetName))
                return AssetDatabase.LoadAssetAtPath<T>(path);
        }
        Debug.LogError($"{assetName}不在{bundleName}中");
#endif
        return null;
    }
    #endregion

    #region 资源加载接口
    /// <summary>
    /// 加载预制体资源
    /// </summary>
    public GameObject LoadGameObject(string bundleName, string assetName)
    {
        var go = LoadFromEditorAsset<GameObject>(bundleName, assetName);
        if (go) return go;
        
        AssetBundle bundle = LoadBundleInternal(bundleName);
        return bundle?.LoadAsset<GameObject>(assetName);
    }

    /// <summary>
    /// 加载文本资源
    /// </summary>
    public TextAsset LoadTextFile(string bundleName, string assetName)
    {
        var go = LoadFromEditorAsset<TextAsset>(bundleName, assetName);
        if (go) return go;
        
        AssetBundle bundle = LoadBundleInternal(bundleName.ToLower());
        return bundle?.LoadAsset<TextAsset>(assetName);
    }
    
    /// <summary>
    /// 加载材质资源
    /// </summary>
    public Material LoadMaterialResource(string bundleName, string assetName)
    {
        var go = LoadFromEditorAsset<Material>(bundleName, assetName);
        if (go) return go;
        
        AssetBundle bundle = LoadBundleInternal(bundleName);
        Material mat = bundle?.LoadAsset<Material>(assetName);
        if (mat == null)
            Debug.LogError($"材质加载失败: {assetName} @ {bundleName}");
        return mat;
    }

    /// <summary>
    /// 加载精灵图集
    /// </summary>
    public SpriteAtlas LoadAtlas(string bundleName, string atlasName)
    {
        Debug.Log($"正在加载图集: {atlasName} | 资源包: {bundleName}");
        var go = LoadFromEditorAsset<SpriteAtlas>(bundleName, atlasName);
        if (go)
        {
            if (!_spriteAtlasCache.ContainsKey(atlasName))
                _spriteAtlasCache.TryAdd(atlasName, go);
            return go;
        }
        
        var bundle = LoadBundleInternal(bundleName);
        var atlas = bundle?.LoadAsset<SpriteAtlas>(atlasName);
        if (!_spriteAtlasCache.ContainsKey(atlasName))
            _spriteAtlasCache.TryAdd(atlasName, atlas);
        return atlas;
    }

    public Sprite GetSpriteFromBundle(string bundleName, string spName)
    {
        var go = LoadFromEditorAsset<Sprite>(bundleName, spName);
        if (go) return go;
        
        var bundle = LoadBundleInternal(bundleName);
        var mat = bundle?.LoadAsset<Sprite>(spName);
        if (mat == null)
            Debug.LogError($"图片加载失败: {spName} @ {bundleName}");
        return mat;
    }

    /// <summary>
    /// 从缓存图集中获取精灵
    /// </summary>
    public Sprite GetSpriteFromAtlas(string spriteName, string atlasName = "UI_Universal")
    {
        if (_spriteAtlasCache.TryGetValue(atlasName, out var spriteAtlas))
        {
            var sprite =  spriteAtlas?.GetSprite(spriteName);
            if (sprite == null)
                Debug.LogError($"精灵不存在: {spriteName} @ {atlasName}");
            return sprite;
        }
        
        var atlas = LoadAtlas(atlasName.ToLower(), atlasName);
        if (atlas == null)
        {
            Debug.LogError($"图集未加载: {atlasName}");
            return null;
        }
        
        var sp = atlas.GetSprite(spriteName);
        if(sp == null)
            Debug.LogError($"图集@ {atlasName} 不存在精灵: {spriteName} ");
        return sp;
    }
    
    /// <summary>
    /// 加载音频资源
    /// </summary>
    public AudioClip LoadAudioClip(string bundleName, string audioName)
    {          
        var go = LoadFromEditorAsset<AudioClip>(bundleName, audioName);
        if (go) return go;
        
        AssetBundle bundle = LoadBundleInternal(bundleName);
        return bundle?.LoadAsset<AudioClip>(audioName);
    }
    
    /// <summary>
    /// 加载ScriptableObject配置
    /// </summary>
    public ScriptableObject LoadScriptableObject(string bundleName, string assetName)
    {
        var go = LoadFromEditorAsset<ScriptableObject>(bundleName, assetName);
        if (go) return go;
        
        AssetBundle bundle = LoadBundleInternal(bundleName);
        return bundle?.LoadAsset<ScriptableObject>(assetName);
    }
    
    /// <summary>
    /// 加载字体资源
    /// </summary>
    public Font LoadFont(string bundleName, string fontName)
    {
        var go = LoadFromEditorAsset<Font>(bundleName, fontName);
        if (go) return go;
        
        AssetBundle bundle = LoadBundleInternal(bundleName);
        return bundle?.LoadAsset<Font>(fontName);
    }
    
    #endregion

    #region 资源管理
    /// <summary>
    /// 卸载指定资源包
    /// </summary>
    public void ReleaseBundle(string bundleName)
    {
        if (_loadedBundles.TryGetValue(bundleName, out AssetBundle bundle))
        {
            bundle.Unload(true);
            _loadedBundles.Remove(bundleName);
            Debug.Log($"资源包已卸载: {bundleName}");
        }
        else
        {
            Debug.LogError($"资源包未加载: {bundleName}");
        }
    }

    /// <summary>
    /// 清空所有资源缓存
    /// </summary>
    public void ClearAllResources()
    {
        foreach (var bundle in _loadedBundles.Values)
        {
            bundle.Unload(true);
        }
        
        _loadedBundles.Clear();
        _spriteAtlasCache.Clear();
        CurrentVideo = null;
        
        Debug.Log("所有资源已清理");
    }
    #endregion
}