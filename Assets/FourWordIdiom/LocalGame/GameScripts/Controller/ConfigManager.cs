using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class ConfigManager : MonoBehaviour
{
    private Dictionary<string,Dictionary<string,string>> adjustTable=new Dictionary<string, Dictionary<string,string>>();
    public bool isRelease=false;
    public bool isLog=false;
    
    public static ConfigManager Instance;
    
    // 暂存箱：Key=文件名, Value=配置文本内容
    private Dictionary<string, string> _configCache = new Dictionary<string, string>();
    private const string BUNDLE_NAME = "gameinfo";
    
    private readonly string[] _targetFiles = new string[] 
    { 
        "Multilingual",
        "GameConfig",
        "ButterflyCollectionTable",
        "ButterflyLocales", 
        "ButterflySceneTable",
        "ChinSimWordBan",
        "config_choiceNiCheng", 
        "NoneedLetter",
        "dailytask",
        "limittime",
        "MatchConfig",
        "MatchRobatTable",
        "shop",
    };
    
    private bool Loading = false;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); 
        }
        else
        {
            Destroy(gameObject);
        }            
    }

    public async Task CacheAllConfigs()
    {
        await AssetBundleLoader.SharedInstance.PreloadSingleBundle(BUNDLE_NAME);
        try
        {
            // 2. 遍历列表，把文本读出来存进字典
            foreach (string fileName in _targetFiles)
            {
                TextAsset txt = AssetBundleLoader.SharedInstance.LoadTextFile(BUNDLE_NAME, fileName);
                if (txt != null)
                {
                    if (fileName == "Multilingual")
                    { 
                        MultilingualManager.Instance.LoadLocalization(txt.text);
                    }
                    else
                    {
                        // 【关键】txt.text 会在堆内存生成一个新的字符串对象
                        // 存入字典后，它就和原始的 TextAsset 没关系了
                        if (!_configCache.ContainsKey(fileName))
                        {
                            _configCache.Add(fileName, txt.text);
                        } 
                    }
                }
                else
                {
                    Debug.LogWarning($"[Config] 包里找不到文件: {fileName}");
                }
            }

            Debug.Log($"[Config] 已缓存 {_configCache.Count} 个配置文件");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Config] 有错误: {e}");
        }
        finally
        {
            // 3. 【立即卸载】
            // 传 true！彻底销毁 Bundle 和 TextAsset。
            // 字典里的 string 因为是独立副本，会被保留下来。
            AssetBundleLoader.SharedInstance.ReleaseBundle(BUNDLE_NAME, true);
            Debug.Log($"[Config] 语言包加载完毕");
        }
    }
    
    /// <summary>
    /// 【第二步】各管理器调用：取数据 -> 移除缓存 (阅后即焚)
    /// </summary>
    public string FetchConfig(string fileName)
    {
        if (_configCache.TryGetValue(fileName, out string content))
        {
            // 【关键】取走数据的同时，从字典移除！
            // 这样这串巨大的字符串就会失去引用，等待下一次 GC 被回收
            _configCache.Remove(fileName);
            return content;
        }

        Debug.LogError($"[Config] 缓存中没有文件: {fileName} (可能已被取走或未加载)");
        return null;
    }
    
    public void LoadAdjustTable()
    {
        // 从AssetBundle中加载CSV文件
        // TextAsset csvFile = AssetBundleLoader.SharedInstance.LoadTextFile("gameinfo", "GameConfig");
        string csvFile = FetchConfig( "GameConfig");
        if (csvFile == null)
        {
            Debug.LogError("加载多语言文件 Failed to load CSV file from AssetBundle.");
            return;
        }      
        // 处理CSV内容的逻辑
        var lines = csvFile.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        var headers = lines[0].Split(',');

        for (int i = 1; i < lines.Length; i++)
        {
            var values = lines[i].Split(',');
            var key = values[0];

            for (int j = 1; j < headers.Length; j++)
            {
                var langCode = headers[j].Trim();
                if (!adjustTable.ContainsKey(langCode))
                {
                    adjustTable[langCode] = new Dictionary<string, string>();
                }
                adjustTable[langCode][key] = values[j];                   
            }
        }
    }


    //根据不同语言找到对应参数
    public string GetString(string key)
    {
        string languageCode = GameDataManager.Instance.UserData.LanguageCode; 
        
        // string languagekey = "Japanese";
        // if (languageCode == "CS")
        // {
        //     languagekey = "ChineseSimplified";
        // }
        // else if (languageCode == "CT")
        // {
        //     languagekey = "ChineseTraditional";
        // }  
        
        
        if (adjustTable.ContainsKey(languageCode))
        {              
            Dictionary<string, string> keyValuePairs = adjustTable[languageCode];
            //Debug.LogError("找到多语言数据" + keyValuePairs);
            if (keyValuePairs.ContainsKey(key))
            {
                return keyValuePairs[key];
            }
        }
        return key;
    }       

}