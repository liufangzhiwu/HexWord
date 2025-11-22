using System.Collections.Generic;
using System.Linq;
using Middleware;
using UnityEngine;

public class MultilingualManager:MonoBehaviour
{
    public static MultilingualManager Instance;
    private Dictionary<string, string> localizedStrings = new Dictionary<string, string>();
    private Dictionary<string, string> localizedNames = new Dictionary<string, string>();
    private Dictionary<string, string> pinziLocalized = new Dictionary<string, string>();

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

    public void LoadLocalization()
    {
        // 从AssetBundle中加载CSV文件
        TextAsset defCsvFile = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "multilingual");
        localizedStrings = ToolUtil.ParseCvsLanguage(defCsvFile,"multilingual");
        TextAsset pinCsvFile = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "pingzi_lang");
        pinziLocalized = ToolUtil.ParseCvsLanguage(pinCsvFile,"pingzi_lang");
        
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
       
        return key;
    }
    
    
    public void LoadLocalizationNameTable()
    {
        // 从AssetBundle中加载CSV文件
        TextAsset csvFile = AdvancedBundleLoader.SharedInstance.LoadTextFile(ToolUtil.GetLanguageBundle(), "config_choiceNiCheng");
        localizedNames = ToolUtil.ParseCvsLanguage(csvFile,"config_choiceNiCheng");
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
        TextAsset textAsset = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo","NoneedLetter");
        if (textAsset == null)
        {
            Debug.LogError("Could not load the dictionary file.");
            return;
        }
        
        if (textAsset != null)
        {
            string[] words = textAsset.text.Split('\n');
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


