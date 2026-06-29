using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Text;
using System.Linq;

public class WordCheckerTool : EditorWindow
{
    // 文件路径配置
    private static string bankWordsPath = "Assets/FourWordIdiom/MultipleData/StageDatas/Localization/ChineseSimplified/config_wordBan.txt";
    private static string stagesFolderPath = "Assets/FourWordIdiom/MultipleData/StageDatas/StageInfos/chineseStage";
    private static string outputPath = "Assets/MissingWordsReport.txt";

    // 数据存储
    private HashSet<string> bankWords = new HashSet<string>();
    private Dictionary<string, List<string>> missingWordsByStage = new Dictionary<string, List<string>>();
    private Dictionary<string, List<string>> duplicatesByStage = new Dictionary<string, List<string>>(); // 新增：存储重复字符信息
    private int totalStagesChecked = 0;
    private int totalWordsChecked = 0;
    private int totalMissingWords = 0;

    // UI相关
    private Vector2 scrollPosition;
    private Vector2 scrollPositionDuplicates; // 新增：重复信息滚动位置
    private bool showDetails = true;
    private bool showDuplicates = true;
    private string searchFilter = "";
    private List<string> allMissingWords = new List<string>();

    [MenuItem("Tools/六边形词语检查工具")]
    public static void ShowWindow()
    {
        var window = GetWindow<WordCheckerTool>("词语检查工具");
        window.minSize = new Vector2(600, 400);
    }

    void OnGUI()
    {
        GUILayout.Label("词语检查工具", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 路径显示
        EditorGUILayout.HelpBox($"词库路径: {bankWordsPath}\n关卡文件夹: {stagesFolderPath}", MessageType.Info);

        EditorGUILayout.Space();

        // 控制按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("加载词库", GUILayout.Width(100)))
        {
            LoadBankWords();
        }

        if (GUILayout.Button("开始检查所有关卡", GUILayout.Width(150)))
        {
            CheckAllStages();
        }

        if (GUILayout.Button("导出报告", GUILayout.Width(100)))
        {
            ExportReport();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // 搜索框
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("搜索缺失词语:", GUILayout.Width(100));
        searchFilter = EditorGUILayout.TextField(searchFilter);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // 统计信息
        EditorGUILayout.HelpBox(
            $"已检查关卡: {totalStagesChecked}\n" +
            $"检查词语总数: {totalWordsChecked}\n" +
            $"缺失词语总数: {totalMissingWords}\n" +
            $"有重复字符的关卡: {duplicatesByStage.Count}",
            MessageType.None
        );

        EditorGUILayout.Space();

        // 显示缺失词语详情
        showDetails = EditorGUILayout.Foldout(showDetails, $"缺失词语详情 ({missingWordsByStage.Count} 个关卡有缺失)");

        if (showDetails && missingWordsByStage.Count > 0)
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (var stage in missingWordsByStage.Keys.OrderBy(k => 
            {
                // 按数字顺序排序关卡
                return ExtractNumberFromFileName(k);
            }))
            {
                var missingWords = missingWordsByStage[stage];

                // 过滤搜索
                var filteredWords = missingWords.Where(w =>
                    string.IsNullOrEmpty(searchFilter) ||
                    w.Contains(searchFilter)
                ).ToList();

                if (filteredWords.Count == 0) continue;

                // 关卡标题
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"{stage} ({filteredWords.Count} 个缺失)", EditorStyles.boldLabel);

                // 显示缺失词语
                EditorGUI.indentLevel++;
                foreach (var word in filteredWords.OrderBy(w => w))
                {
                    EditorGUILayout.LabelField($"• {word}");
                }

                EditorGUI.indentLevel--;

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            EditorGUILayout.EndScrollView();
        }

        // 显示重复字符详情
        EditorGUILayout.Space();
        showDuplicates = EditorGUILayout.Foldout(showDuplicates, $"重复字符详情 ({duplicatesByStage.Count} 个关卡有重复)");

        if (showDuplicates && duplicatesByStage.Count > 0)
        {
            scrollPositionDuplicates = EditorGUILayout.BeginScrollView(scrollPositionDuplicates);

            foreach (var stage in duplicatesByStage.Keys.OrderBy(k => ExtractNumberFromFileName(k)))
            {
                var duplicates = duplicatesByStage[stage];

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"{stage} ({duplicates.Count} 处重复)", EditorStyles.boldLabel);

                EditorGUI.indentLevel++;
                foreach (var info in duplicates)
                {
                    EditorGUILayout.LabelField($"• {info}");
                }
                EditorGUI.indentLevel--;

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            EditorGUILayout.EndScrollView();
        }
    }

    /// <summary>
    /// 从文件名中提取数字
    /// </summary>
    private int ExtractNumberFromFileName(string fileName)
    {
        try
        {
            // 使用正则表达式提取文件名中的数字
            Regex regex = new Regex(@"\d+");
            Match match = regex.Match(fileName);
            
            if (match.Success && int.TryParse(match.Value, out int number))
            {
                return number;
            }
            
            // 如果没有数字，返回一个大数值让它排在后面
            return int.MaxValue;
        }
        catch
        {
            return int.MaxValue;
        }
    }

    /// <summary>
    /// 加载词库
    /// </summary>
    private void LoadBankWords()
    {
        if (!File.Exists(bankWordsPath))
        {
            EditorUtility.DisplayDialog("错误", $"词库文件不存在: {bankWordsPath}", "确定");
            return;
        }

        bankWords.Clear();

        try
        {
            string[] lines = File.ReadAllLines(bankWordsPath, Encoding.UTF8);
            ParseBankWords(lines);

            EditorUtility.DisplayDialog("成功", $"已加载词库，共 {bankWords.Count} 个词语", "确定");
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("错误", $"加载词库失败: {e.Message}", "确定");
        }
    }

    /// <summary>
    /// 解析词库文件
    /// </summary>
    private void ParseBankWords(string[] lines)
    {
        // 正则表达式匹配词语格式：#词语#拼音#解释#
        Regex regex = new Regex(@"^#([^#]+)#");

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            Match match = regex.Match(line);
            if (match.Success)
            {
                string word = match.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(word))
                {
                    bankWords.Add(word);
                }
            }
        }
    }

    /// <summary>
    /// 检查所有关卡
    /// </summary>
    private void CheckAllStages()
    {
        if (!Directory.Exists(stagesFolderPath))
        {
            EditorUtility.DisplayDialog("错误", $"关卡文件夹不存在: {stagesFolderPath}", "确定");
            return;
        }

        if (bankWords.Count == 0)
        {
            bool loadBank = EditorUtility.DisplayDialog("提示", "词库未加载，是否现在加载？", "是", "否");
            if (loadBank)
            {
                LoadBankWords();
                if (bankWords.Count == 0) return;
            }
            else
            {
                return;
            }
        }

        // 清空之前的结果
        missingWordsByStage.Clear();
        duplicatesByStage.Clear(); // 清空重复信息
        allMissingWords.Clear();
        totalStagesChecked = 0;
        totalWordsChecked = 0;
        totalMissingWords = 0;

        // 获取所有关卡文件并按数字顺序排序
        string[] stageFiles = Directory.GetFiles(stagesFolderPath, "*.json", SearchOption.AllDirectories);
        
        // 按文件名中的数字顺序排序
        stageFiles = stageFiles.OrderBy(filePath =>
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            return ExtractNumberFromFileName(fileName);
        }).ToArray();

        if (stageFiles.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", $"未找到关卡文件: {stagesFolderPath}", "确定");
            return;
        }

        // 显示进度条
        EditorUtility.DisplayProgressBar("正在检查关卡", "初始化...", 0);

        try
        {
            for (int i = 0; i < stageFiles.Length; i++)
            {
                string filePath = stageFiles[i];
                string fileName = Path.GetFileNameWithoutExtension(filePath);

                // 更新进度
                Debug.Log($"正在检查关卡: {fileName} (第{i + 1}/{stageFiles.Length}关)");

                // 检查单个关卡
                CheckSingleStage(filePath, fileName);

                totalStagesChecked++;
            }

            // 收集所有缺失词语
            foreach (var missingList in missingWordsByStage.Values)
            {
                allMissingWords.AddRange(missingList);
            }

            totalMissingWords = allMissingWords.Count;

            EditorUtility.ClearProgressBar();

            // 显示结果
            string message = $"检查完成!\n" +
                             $"已检查: {totalStagesChecked} 个关卡\n" +
                             $"检查词语: {totalWordsChecked} 个\n" +
                             $"缺失词语: {totalMissingWords} 个\n" +
                             $"有缺失的关卡: {missingWordsByStage.Count} 个\n" +
                             $"有重复字符的关卡: {duplicatesByStage.Count} 个";

            EditorUtility.DisplayDialog("完成", message, "确定");

            // 刷新UI
            Repaint();
        }
        catch (System.Exception e)
        {
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("错误", $"检查过程中出错: {e.Message}", "确定");
        }
    }

    /// <summary>
    /// 检查单个关卡
    /// </summary>
    private void CheckSingleStage(string filePath, string stageName)
    {
        string content = File.ReadAllText(filePath, Encoding.UTF8);
        int stageId = ExtractNumberFromFileName(stageName);
        
        // 解析russ字段，获取词语列表和位置映射
        List<string> stageWords = new List<string>();
        Dictionary<string, List<string>> positionToWords = new Dictionary<string, List<string>>();
        ParseRuss(stageId, content, stageWords, positionToWords);
        
        // 检查词语缺失
        List<string> missingWords = new List<string>();
        foreach (string word in stageWords)
        {
            totalWordsChecked++;
            Debug.Log($"检查词语: {word}");
            if (!bankWords.Contains(word))
            {
                if (!missingWords.Contains(word))
                {
                    missingWords.Add(word);
                    Debug.Log($"缺失词语: {word}");
                }
            }
        }
        if (missingWords.Count > 0)
        {
            missingWordsByStage[stageName] = missingWords;
        }
        
        // 解析pass字段，检查每个位置的字符重复
        Dictionary<string, List<string>> positionToChars = ParsePass(stageId, content);
        List<string> stageDuplicates = new List<string>();

        foreach (var kvp in positionToChars)
        {
            string position = kvp.Key;
            List<string> chars = kvp.Value;
            
            // 检查是否有重复字符
            var duplicates = chars.GroupBy(c => c).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicates.Count > 0)
            {
                // 获取该位置对应的词语
                List<string> relatedWords=new List<string>();
                if (positionToWords.ContainsKey(position))
                {
                    List<string> valueWords = positionToWords[position];
                    foreach (var word in valueWords)
                    {
                        if (word.Contains(duplicates[0]))
                        {
                            relatedWords.Add(word);
                        }
                    }
                }
                
                string wordsStr = relatedWords.Count > 0 ? string.Join(", ", relatedWords) : "未知词语";

                if (wordsStr.Contains(duplicates[0]))
                {
                    string duplicateInfo = $"位置 {position} 存在重复字符: {string.Join(", ", duplicates)}，涉及词语: {wordsStr}";
                    stageDuplicates.Add(duplicateInfo);
                     Debug.LogWarning($"关卡 {stageName} {duplicateInfo}");
                }
            }
        }

        if (stageDuplicates.Count > 0)
        {
            duplicatesByStage[stageName] = stageDuplicates;
        }
    }

    /// <summary>
    /// 解析russ字段，填充词语列表和位置映射
    /// </summary>
    private void ParseRuss(int stageId, string content, List<string> stageWords, Dictionary<string, List<string>> positionToWords)
    {
        string russField = $"\"{stageId}_russ\":";
        int russStart = content.IndexOf(russField);
        if (russStart == -1) return;
        
        // 提取russ值
        string russValue = ExtractFieldValue(content, russStart);
        if (string.IsNullOrEmpty(russValue)) return;
        
        // 按管道符分割成语条目
        string[] phraseEntries = russValue.Split('|');
        foreach (string entry in phraseEntries)
        {
            if (string.IsNullOrWhiteSpace(entry)) continue;
            
            // 格式："无主题"_国泰民安_1:1_国_7_6#1_泰_7_5#1_民_7_4#1_安_7_3
            // 提取词语名
            string[] wordparts = entry.Split(':');
            string[] parts = wordparts[1].Split('#');
            if (parts.Length >= 2)
            {
                string[] words = wordparts[0].Split('_');
                string word = words[1];
                if (!string.IsNullOrEmpty(word) && word.Length >= 3)
                {
                    if (!stageWords.Contains(word))
                        stageWords.Add(word);
                    
                    // 解析该词语的所有字符位置
                    // 从第2个部分开始，每个部分可能是类似 "1:1" 或 "国_7_6#1" 等
                    for (int i = 0; i < parts.Length; i++)
                    {
                        string part = parts[i];
                        // 跳过类似 "1:1" 这样的部分
                        //if (part.Contains(":")) continue;
                        
                        // 格式：字符_行_列#序号
                        string[] subParts = part.Split('_');
                        if (subParts.Length >= 3)
                        {
                            string ch = subParts[1];
                            string row = subParts[2];
                            string col = subParts[3];
                            string position = $"{row}_{col}";
                            
                            if (!positionToWords.ContainsKey(position))
                                positionToWords[position] = new List<string>();
                            if (!positionToWords[position].Contains(word))
                                positionToWords[position].Add(word);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 解析pass字段，返回位置到字符列表的映射
    /// </summary>
    private Dictionary<string, List<string>> ParsePass(int stageId, string content)
    {
        Dictionary<string, List<string>> result = new Dictionary<string, List<string>>();
        string passField = $"\"{stageId}_pass\":";
        int passStart = content.IndexOf(passField);
        if (passStart == -1) return result;
        
        // 提取pass值
        string passValue = ExtractFieldValue(content, passStart);
        if (string.IsNullOrEmpty(passValue)) return result;
        
        // 按管道符分割条目
        string[] entries = passValue.Split('|');
        foreach (string entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry)) continue;
            
            // 格式："位置:字符列表"，例如 "3_6:水_柳_心_修"
            int colonIndex = entry.IndexOf(':');
            if (colonIndex == -1) continue;
            
            string position = entry.Substring(0, colonIndex);
            string charsStr = entry.Substring(colonIndex + 1);
            
            List<string> chars = charsStr.Split('_').Where(c => !string.IsNullOrEmpty(c)).ToList();
            result[position] = chars;
        }
        
        return result;
    }

    /// <summary>
    /// 辅助方法：从JSON文本中提取指定字段的值（基于字段开始索引）
    /// </summary>
    private string ExtractFieldValue(string content, int fieldStart)
    {
        // 找到冒号后的起始位置
        int valueStart = content.IndexOf(':', fieldStart) + 1;
        // 跳过空白
        while (valueStart < content.Length && char.IsWhiteSpace(content[valueStart]))
            valueStart++;
        
        char startChar = content[valueStart];
        int valueEnd = -1;
        
        if (startChar == '"')
        {
            // 字符串值
            valueEnd = content.IndexOf('"', valueStart + 1);
            while (valueEnd < content.Length - 1 && content[valueEnd - 1] == '\\')
            {
                valueEnd = content.IndexOf('"', valueEnd + 1);
            }
            valueEnd++; // 包含结束引号
        }
        else if (startChar == '[')
        {
            // 数组值，找到匹配的]
            int depth = 1;
            int i = valueStart + 1;
            while (i < content.Length && depth > 0)
            {
                if (content[i] == '[') depth++;
                else if (content[i] == ']') depth--;
                i++;
            }
            valueEnd = i;
        }
        else if (startChar == '{')
        {
            // 对象值，找到匹配的}
            int depth = 1;
            int i = valueStart + 1;
            while (i < content.Length && depth > 0)
            {
                if (content[i] == '{') depth++;
                else if (content[i] == '}') depth--;
                i++;
            }
            valueEnd = i;
        }
        else
        {
            // 简单值（数字等），找到逗号或换行
            int i = valueStart;
            while (i < content.Length && !(content[i] == ',' || content[i] == '}' || content[i] == '\n' || content[i] == '\r'))
                i++;
            valueEnd = i;
        }
        
        if (valueEnd == -1) valueEnd = content.Length;
        
        string value = content.Substring(valueStart, valueEnd - valueStart).Trim();
        
        // 如果是字符串，移除引号并处理转义
        if (value.StartsWith("\"") && value.EndsWith("\""))
        {
            value = value.Substring(1, value.Length - 2);
            value = value.Replace("\\\"", "\"");
        }
        
        return value;
    }

    /// <summary>
    /// 导出报告
    /// </summary>
    private void ExportReport()
    {
        if (missingWordsByStage.Count == 0 && duplicatesByStage.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "没有缺失词语也没有重复字符，无需导出报告", "确定");
            return;
        }

        try
        {
            StringBuilder report = new StringBuilder();

            report.AppendLine("=== 词语检查报告 ===");
            report.AppendLine($"生成时间: {System.DateTime.Now}");
            report.AppendLine($"词库文件: {bankWordsPath}");
            report.AppendLine($"检查关卡数: {totalStagesChecked}");
            report.AppendLine($"检查词语数: {totalWordsChecked}");
            report.AppendLine($"缺失词语总数: {totalMissingWords}");
            report.AppendLine($"有重复字符的关卡数: {duplicatesByStage.Count}");
            report.AppendLine();

            // 按关卡分类（缺失词语）
            if (missingWordsByStage.Count > 0)
            {
                report.AppendLine("=== 缺失词语（按关卡分类） ===");
                report.AppendLine();

                var sortedStages = missingWordsByStage.Keys.OrderBy(k => ExtractNumberFromFileName(k)).ToList();
                foreach (var stage in sortedStages)
                {
                    var missingWords = missingWordsByStage[stage];
                    report.AppendLine($"【{stage}】({missingWords.Count} 个缺失)");
                    foreach (var word in missingWords.OrderBy(w => w))
                    {
                        report.AppendLine($"  • {word}");
                    }
                    report.AppendLine();
                }
            }

            // 重复字符信息
            if (duplicatesByStage.Count > 0)
            {
                report.AppendLine("=== 重复字符信息（同一格子出现相同字符） ===");
                report.AppendLine();

                var sortedDuplicatesStages = duplicatesByStage.Keys.OrderBy(k => ExtractNumberFromFileName(k)).ToList();
                foreach (var stage in sortedDuplicatesStages)
                {
                    var duplicates = duplicatesByStage[stage];
                    report.AppendLine($"【{stage}】({duplicates.Count} 处重复)");
                    foreach (var info in duplicates)
                    {
                        report.AppendLine($"  • {info}");
                    }
                    report.AppendLine();
                }
            }

            // 所有缺失词语列表（去重）
            if (allMissingWords.Count > 0)
            {
                report.AppendLine("=== 所有缺失词语（去重） ===");
                report.AppendLine();

                var uniqueMissingWords = allMissingWords.Distinct().OrderBy(w => w).ToList();
                for (int i = 0; i < uniqueMissingWords.Count; i++)
                {
                    report.AppendLine($"{i + 1}. {uniqueMissingWords[i]}");
                }
                report.AppendLine();
            }

            // 统计信息
            report.AppendLine("=== 统计信息 ===");
            report.AppendLine($"总关卡数: {totalStagesChecked}");
            report.AppendLine($"有缺失的关卡数: {missingWordsByStage.Count}");
            if (allMissingWords.Count > 0)
                report.AppendLine($"缺失词语种类数: {allMissingWords.Distinct().Count()}");
            report.AppendLine($"缺失词语总次数: {totalMissingWords}");
            report.AppendLine($"有重复字符的关卡数: {duplicatesByStage.Count}");

            // 写入文件
            File.WriteAllText(outputPath, report.ToString(), Encoding.UTF8);

            // 提示用户
            bool openFile = EditorUtility.DisplayDialog("成功",
                $"报告已生成到: {outputPath}\n\n" +
                $"有缺失的关卡: {missingWordsByStage.Count} 个\n" +
                $"缺失词语种类: {allMissingWords.Distinct().Count()} 个\n" +
                $"有重复字符的关卡: {duplicatesByStage.Count} 个\n\n" +
                "是否立即打开报告文件？",
                "是", "否");

            if (openFile)
            {
                EditorUtility.RevealInFinder(outputPath);
                System.Diagnostics.Process.Start(outputPath);
            }
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("错误", $"导出报告失败: {e.Message}", "确定");
        }
    }

    /// <summary>
    /// 检查词库中是否包含指定词语
    /// </summary>
    public bool CheckWordInBank(string word)
    {
        return bankWords.Contains(word);
    }

    /// <summary>
    /// 获取词库中的词语数量
    /// </summary>
    public int GetBankWordCount()
    {
        return bankWords.Count;
    }

    /// <summary>
    /// 获取所有缺失词语（去重）
    /// </summary>
    public List<string> GetAllMissingWords()
    {
        return allMissingWords.Distinct().ToList();
    }
}