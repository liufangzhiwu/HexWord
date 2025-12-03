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
    private static string bankWordsPath = "Assets/FourWordIdiom/MultipleData/StageDatas/GameInfo/ChinSimWordBan.txt";
    private static string stagesFolderPath = "Assets/FourWordIdiom/MultipleData/StageDatas/StageInfos/chineseStage";
    private static string outputPath = "Assets/MissingWordsReport.txt";

    // 数据存储
    private HashSet<string> bankWords = new HashSet<string>();
    private Dictionary<string, List<string>> missingWordsByStage = new Dictionary<string, List<string>>();
    private int totalStagesChecked = 0;
    private int totalWordsChecked = 0;
    private int totalMissingWords = 0;

    // UI相关
    private Vector2 scrollPosition;
    private bool showDetails = true;
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
            $"缺失词语总数: {totalMissingWords}",
            MessageType.None
        );

        EditorGUILayout.Space();

        // 显示结果
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
                             $"有缺失的关卡: {missingWordsByStage.Count} 个";

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
        List<string> stageWords = ExtractWordsFromStage(ExtractNumberFromFileName(stageName), content);

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
    }

    /// <summary>
    /// 从关卡内容中提取词语
    /// </summary>
    private List<string> ExtractWordsFromStage(int stageId, string content)
    {
        List<string> words = new List<string>();

      
        // 首先找到"russ"字段
        int russStart = content.IndexOf($"\"{stageId}_russ\":");
        if (russStart == -1)
        {
            // 如果没有找到，可能是不同的命名方式
            return words;
        }
        
        // 找到字段值的开始位置
        int valueStart = content.IndexOf(':', russStart) + 1;
        int valueEnd = -1;
        
        // 确定值的结束位置
        char startChar = content[valueStart];
        
        if (startChar == '"')
        {
            // 字符串值
            valueEnd = content.IndexOf('"', valueStart + 1);
            while (valueEnd < content.Length - 1 && content[valueEnd - 1] == '\\')
            {
                valueEnd = content.IndexOf('"', valueEnd + 1);
            }
        }
        else if (startChar == '[')
        {
            // 数组值
            valueEnd = content.IndexOf(']', valueStart) + 1;
        }
        else if (startChar == '{')
        {
            // 对象值
            valueEnd = content.IndexOf('}', valueStart) + 1;
        }
        else
        {
            // 简单值
            valueEnd = content.IndexOfAny(new char[] { ',', '}', '\n', '\r' }, valueStart);
        }
        
        if (valueEnd == -1)
        {
            valueEnd = content.Length;
        }
        
        string russValue = content.Substring(valueStart, valueEnd - valueStart).Trim();
        
        // 移除引号
        if (russValue.StartsWith("\"") && russValue.EndsWith("\""))
        {
            russValue = russValue.Substring(1, russValue.Length - 2);
            // 处理转义字符
            russValue = russValue.Replace("\\\"", "\"");
        }
        
        // 按管道符分割成语条目
        string[] phraseEntries = russValue.Split('|');
        
        foreach (string entry in phraseEntries)
        {
            if (string.IsNullOrWhiteSpace(entry)) continue;
            
            // 提取词语
            // 格式示例："无主题"_国泰民安_1:1_国_7_6#1_泰_7_5#1_民_7_4#1_安_7_3
            // 我们需要提取"国泰民安"这个部分
            
            // 按'_'分割
            string[] parts = entry.Split('_');
            if (parts.Length >= 2)
            {
                // 第二个部分是词语名称
                string word = parts[1].Trim('"');
                
                // 检查是否为有效的词语（至少2个字符）
                if (!string.IsNullOrEmpty(word) && word.Length >= 3)
                {
                    words.Add(word);
                }
            }
        }

        return words.Distinct().ToList();
    }

    /// <summary>
    /// 导出报告
    /// </summary>
    private void ExportReport()
    {
        if (missingWordsByStage.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "没有缺失的词语，无需导出报告", "确定");
            return;
        }

        try
        {
            StringBuilder report = new StringBuilder();

            report.AppendLine("=== 缺失词语检查报告 ===");
            report.AppendLine($"生成时间: {System.DateTime.Now}");
            report.AppendLine($"词库文件: {bankWordsPath}");
            report.AppendLine($"检查关卡数: {totalStagesChecked}");
            report.AppendLine($"检查词语数: {totalWordsChecked}");
            report.AppendLine($"缺失词语总数: {totalMissingWords}");
            report.AppendLine();

            // 按关卡分类，并按数字顺序排序
            report.AppendLine("=== 按关卡分类（按数字顺序） ===");
            report.AppendLine();

            // 按数字顺序排序关卡
            var sortedStages = missingWordsByStage.Keys.OrderBy(k => 
            {
                return ExtractNumberFromFileName(k);
            }).ToList();

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

            // 所有缺失词语列表（去重）
            report.AppendLine("=== 所有缺失词语（去重） ===");
            report.AppendLine();

            var uniqueMissingWords = allMissingWords.Distinct().OrderBy(w => w).ToList();

            for (int i = 0; i < uniqueMissingWords.Count; i++)
            {
                report.AppendLine($"{i + 1}. {uniqueMissingWords[i]}");
            }

            // 统计信息
            report.AppendLine();
            report.AppendLine("=== 统计信息 ===");
            report.AppendLine($"总关卡数: {totalStagesChecked}");
            report.AppendLine($"有缺失的关卡数: {missingWordsByStage.Count}");
            report.AppendLine($"缺失词语种类数: {uniqueMissingWords.Count}");
            report.AppendLine($"缺失词语总次数: {totalMissingWords}");

            // 写入文件
            File.WriteAllText(outputPath, report.ToString(), Encoding.UTF8);

            // 提示用户
            bool openFile = EditorUtility.DisplayDialog("成功",
                $"报告已生成到: {outputPath}\n\n" +
                $"有缺失的关卡: {missingWordsByStage.Count} 个\n" +
                $"缺失词语种类: {uniqueMissingWords.Count} 个\n\n" +
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