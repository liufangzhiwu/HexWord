using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Middleware;
using Newtonsoft.Json;
using UnityEngine;

public class MultilingualManager:MonoBehaviour
{
    public static MultilingualManager Instance;
    private Dictionary<string, string> localizedStrings = new Dictionary<string, string>();
    private Dictionary<string, string> localizedNames = new Dictionary<string, string>();
    private Dictionary<string, string> pinziLocalized = new Dictionary<string, string>();
    private Dictionary<string, string> butterfliesLocalized = new Dictionary<string, string>();

    // 屏蔽词存储集合（哈希集合提升查询性能）
    private HashSet<string> forbiddenWords = new HashSet<string>();
    
    private void Awake()
    {
        // 确保只有一个 AudioManager 实例
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 在场景切换时不销毁
        }
        else
        {
            Destroy(gameObject); // 销毁重复的实例
        }       
    }

    public void LoadLocalization(string defCsvFile)
    {
        localizedStrings = ToolUtil.ReadCvsLanguage(defCsvFile,"Multilingual");
        
        // 从AssetBundle中加载CSV文件
        // TextAsset defCsvFile = AssetBundleLoader.SharedInstance.LoadTextFile("gameinfo", "multilingual");
        //TextAsset pinCsvFile = AssetBundleLoader.SharedInstance.LoadTextFile("gameinfo", "pingzi_lang");
        //pinziLocalized = ToolUtil.ReadCvsLanguage(pinCsvFile,"pingzi_lang");
    }

    public string GetString(string key, string filename = "multilingual")
    {
        if (filename.Equals("multilingual"))
        {
            if (localizedStrings.TryGetValue(key, out string value))
            {
                return value;
            }
        }else if (filename.Equals("pingzi"))
        {
            if (pinziLocalized.TryGetValue(key, out string value))
            {
                return value;
            }
        }
        else if (filename.Equals("hudie"))
        {
            if (butterfliesLocalized.TryGetValue(key, out string value))
            {
                return value;
            }
        }
        return key;
    }
    
    
    public void LoadLocalizationNameTable()
    {
        string hudieCsvFile = ConfigManager.Instance.FetchConfig( "ButterflyLocales");
        butterfliesLocalized = ToolUtil.ParseCvsLanguage(hudieCsvFile,"ButterflyLocales");
        // 从AssetBundle中加载CSV文件
        string csvFile = ConfigManager.Instance.FetchConfig( "config_choiceNiCheng");
        if (csvFile != null)
            localizedNames = ToolUtil.ParseCvsLanguage(csvFile, "config_choiceNiCheng");
        else
            Debug.LogWarning("config_choiceNiCheng 在 gameinfo 中找不到！");

        InitbiddenWords();
    }

    
    /// <summary>
    /// 获取名称长度
    /// </summary>
    /// <returns></returns>
    public int GetNameLength()
    {
        return localizedNames.Count;
    }
    
    /// <summary>
    /// 随机获取名字
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public string GetName(int key)
    {
        if (key < 0 || key >= GetNameLength())
            return null;
        
        var val = key.ToString();
        foreach (var data in localizedNames)
        {
            if (data.Key == val)
                return data.Value;
        }
        return null;
    }


    /// <summary>
    /// 加载屏蔽词库
    /// </summary>
    public void InitbiddenWords()
    {
        // 加载 TextAsset
        string textAsset = ConfigManager.Instance.FetchConfig("NoneedLetter");
        if (textAsset == null)
        {
            Debug.LogError("Could not load the dictionary file.");
            return;
        }
        
        if (textAsset != null)
        {
            string[] words = textAsset.Split('\n');
            foreach (string word in words)
            {
                string cleanWord = word.Trim().ToLower();
                if (!string.IsNullOrEmpty(cleanWord))
                {
                    forbiddenWords.Add(cleanWord);
                }
            }
            Debug.Log($"Loaded {forbiddenWords.Count} forbidden words");
        }
    }
    
    
    // 快速检测是否存在敏感词
    public bool ContainsForbiddenWords(string input)
    {
        if (string.IsNullOrEmpty(input)) return false;

        string lowerInput = input.ToLower();
        foreach (string word in forbiddenWords)
        {
            if (lowerInput.Contains(word))
            {
                return true;
            }
        }
        return false;
    }
}


