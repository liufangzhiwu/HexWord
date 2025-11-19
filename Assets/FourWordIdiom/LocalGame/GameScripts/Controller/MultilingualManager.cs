using System.Collections.Generic;
using UnityEngine;

public class MultilingualManager:MonoBehaviour
{
    public static MultilingualManager Instance;
    private Dictionary<string, Dictionary<string, string>> localizedStrings=new Dictionary<string, Dictionary<string, string>>();

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
        TextAsset csvFile = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "Multilingual");
        //TextAsset csvFile = Resources.Load<TextAsset>("locales");
        if (csvFile == null)
        {
            Debug.LogError("加载多语言文件 Failed to load CSV file from AssetBundle.");
            return;
        }      
        // 处理CSV内容的逻辑
        var lines = csvFile.text.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        var headers = lines[0].Split(',');

        for (int i = 1; i < lines.Length; i++)
        {
            var values = lines[i].Split(',');
            var key = values[0];

            for (int j = 1; j < headers.Length; j++)
            {
                var langCode = headers[j].Trim();
                if (!localizedStrings.ContainsKey(langCode))
                {
                    localizedStrings[langCode] = new Dictionary<string, string>();
                }
                localizedStrings[langCode][key] = values[j];                   
            }
        }
    }

    public string GetString(string key)
    {
        string languageCode = AppGameSettings.SystemLanguage;
        if (localizedStrings.ContainsKey(languageCode))
        {              
            Dictionary<string, string> keyValuePairs = localizedStrings[languageCode];
           
            if (keyValuePairs.ContainsKey(key))
            {
                return keyValuePairs[key];
            }
        }
        return key;
    }

}


