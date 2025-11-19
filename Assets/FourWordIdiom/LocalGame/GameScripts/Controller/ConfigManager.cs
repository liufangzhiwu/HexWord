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
    [HideInInspector] public GameObject SpineObject;

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

    async void Start()
    {
        //等待100毫秒（保证数据初始化成功）
        await Task.Delay(100);
        
        LoadAdjustTable();
        if (SpineObject == null)
        {
            SpineObject = Resources.Load<GameObject>("StageBox");
        }
        //Debug.unityLogger.logEnabled = isLog;
        Application.targetFrameRate = 60; // 平台设置为60帧
    }
    
    private void LoadAdjustTable()
    {
        // 从AssetBundle中加载CSV文件
        TextAsset csvFile = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "GameConfig");
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
        string languageCode = GameDataManager.instance.UserData.LanguageCode; 
        
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