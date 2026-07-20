using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Middleware;
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
    private Dictionary<char, string> charToPinyinMap;
    
    public WordVocabularyManager()
    {
        entries = new Dictionary<string, DictionaryEntry>();
        charToPinyinMap = new Dictionary<char, string>(); // 🌟 初始化
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
        // 加载 TextAsset
        TextAsset textAsset = AdvancedBundleLoader.SharedInstance.LoadTextFile(ToolUtil.GetLanguageBundle(),"config_wordBan");
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
                //.Select(part => part.Replace(" ", "")) // 移除所有空格字符并修剪前后空格
                .ToArray(); // 转换为数组

                if (parts.Length >2)
                {
                    var word = parts[0].Trim();
                    var pinyin = parts[1];
                    var definition = parts.Length > 2 ? parts[2].Trim() : null;
                    //var synonyms = new List<string>(parts[3].Trim().Split(';'));
                    var example="";
                    // if (parts.Length > 3)
                    // {
                    //     example = parts[3].Trim().Length>2?parts[3]:""; 
                    // }
                    var synonym ="";
                    if (parts.Length > 3)
                    {
                        synonym = parts[3].Trim().Length>2?parts[3]:""; 
                    }

                    var entry = new DictionaryEntry(word, definition, pinyin, example, synonym);
                    entries[word] = entry; // 存入字典
                    
                    // ==========================================
                    // 🌟 核心逻辑：拆分成语，提取单字拼音
                    // ==========================================
                    string[] syllables = pinyin.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    // 遍历成语的每个字，和拼音一一对应
                    for (int i = 0; i < Math.Min(word.Length, syllables.Length); i++)
                    {
                        char c = word[i];
                        // 存入单字字典，如果遇到多音字，保留第一个读音即可
                        if (!charToPinyinMap.ContainsKey(c))
                        {
                            charToPinyinMap[c] = syllables[i];
                        }
                    }
                }
            }
        }
    }

    public DictionaryEntry GetEntry(string word)
    {
        entries.TryGetValue(word, out var entry);
        return entry;
    }
    
    // 🌟 3. 新增：供外部调用的单字拼音查询接口
    public string GetCharPinyin(string character)
    {
        if (string.IsNullOrEmpty(character)) return "";
        
        // 查字典，查到返回拼音（如 "gāng"），查不到返回原汉字兜底
        if (charToPinyinMap.TryGetValue(character[0], out string pinyin))
        {
            return pinyin;
        }
        return character; 
    }
}


