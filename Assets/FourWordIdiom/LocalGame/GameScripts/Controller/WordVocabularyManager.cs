using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class DictionaryEntry
{
    public string Word { get; set; }
    public string Definition { get; set; }//意味
    public string Pinyin { get; set; }
    public string Example { get; set; }//用例
    public string Synonym { get; set; }//近义词

    public DictionaryEntry(string word, string definition, string pinyin, string example, string synonym)
    {
        Word = word;
        Definition = definition;
        Pinyin = pinyin;
        Example = example;
        Synonym = synonym;
    }
}

public class WordVocabularyManager
{
    private static WordVocabularyManager _instance;
    private Dictionary<string, DictionaryEntry> entries;

    public WordVocabularyManager()
    {
        entries = new Dictionary<string, DictionaryEntry>();
    }

    // 单例访问点
    public static WordVocabularyManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new WordVocabularyManager();
            }
            return _instance;
        }
    }

    /// <summary>
    /// 加载词库
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    public async Task LoadEntriesAsync()
    {
        string txtname = "ChinSimWordBan";
        // switch (GameDataManager.instance.UserData.LanguageCode)
        // {
        //     case "Japanese":
        //         txtname = "JanWordBan";
        //         break;
        //     case "ChineseTraditional":
        //         txtname = "ChinTraWordBan";
        //         break;
        //     case "ChineseSimplified":
        //         txtname = "ChinSimWordBan";
        //         break;
        // }
        // 加载 TextAsset
        TextAsset textAsset = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo",txtname);
        if (textAsset == null)
        {
            Debug.LogError("Could not load the dictionary file.");
            return;
        }

        // 使用 StringReader 读取内容
        using (StringReader reader = new StringReader(textAsset.text))
        {
            string line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                // 按 '#' 拆分字符串
                var parts = line.Split(new[] { '#' }, StringSplitOptions.RemoveEmptyEntries)
                //.Select(part => part.Replace(" ", "").Trim()) // 移除所有空格字符并修剪前后空格
                .ToArray(); // 转换为数组

                if (parts.Length >2)
                {
                    var word = parts[0].Trim();
                    var pinyin = parts[1].Trim();
                    var definition = parts.Length > 2 ? parts[2].Trim() : null;
                    var synonyms = parts[3].Trim().Length>2?parts[3].Trim():""; 
                    var example="";
                    if (parts.Length > 3)
                    {
                        synonyms = parts[3].Trim().Length>2?parts[3].Trim():""; 
                    }
                    if (parts.Length > 4)
                    {
                        example = parts[4].Trim().Length>2?parts[4].Trim():""; 
                    }

                    var entry = new DictionaryEntry(word, definition, pinyin, example, synonyms);
                    entries[word] = entry; // 存入字典
                }
            }
        }
    }

    public DictionaryEntry GetEntry(string word)
    {
        entries.TryGetValue(word, out var entry);
        return entry;
    }
    
}


