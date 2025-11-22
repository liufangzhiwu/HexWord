using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;

public class LevelSplitterWindow : EditorWindow
{
    private string inputFilePath = "";
    private Vector2 scrollPosition;
    private string outputPath = "Assets/FourWordIdiom/MultipleData/Localization/ChineseSimplified/stage";
    private bool includeSampleData = true;
    private string fileContentPreview = "";

    [MenuItem("Tools/关卡拆分工具")]
    public static void ShowWindow()
    {
        GetWindow<LevelSplitterWindow>("关卡拆分工具");
    }

    void OnGUI()
    {
        GUILayout.Label("关卡数据拆分工具", EditorStyles.boldLabel);
        
        EditorGUILayout.Space();
        GUILayout.Label("选择关卡数据文件:");
        EditorGUILayout.BeginHorizontal();
        inputFilePath = EditorGUILayout.TextField(inputFilePath);
        if (GUILayout.Button("浏览", GUILayout.Width(60)))
        {
            string path = EditorUtility.OpenFilePanel("选择关卡数据文件", Application.dataPath, "txt");
            if (!string.IsNullOrEmpty(path))
            {
                inputFilePath = path;
                // 预览文件内容
                PreviewFileContent();
            }
        }
        EditorGUILayout.EndHorizontal();

        // 显示文件内容预览
        if (!string.IsNullOrEmpty(fileContentPreview))
        {
            EditorGUILayout.Space();
            GUILayout.Label("文件内容预览:");
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(150));
            EditorGUILayout.TextArea(fileContentPreview, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.Space();
        outputPath = EditorGUILayout.TextField("输出路径:", outputPath);
        
        if (GUILayout.Button("选择输出文件夹"))
        {
            string selectedPath = EditorUtility.SaveFolderPanel("选择输出文件夹", outputPath, "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                if (!selectedPath.StartsWith(Application.dataPath))
                {
                    EditorUtility.DisplayDialog("错误", "输出路径必须在Assets文件夹内!", "确定");
                }
                else
                {
                    outputPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                }
            }
            
            Repaint();
        }

        EditorGUILayout.Space();
        includeSampleData = EditorGUILayout.Toggle("包含示例数据", includeSampleData);
        
        if (includeSampleData && GUILayout.Button("加载示例数据"))
        {
            fileContentPreview = @"1;0;5;5;五颜六色,0,3:兴高采烈,0,2:画龙点睛,0,1;0,0,五:1,0,兴:1,1,颜:2,0,高:2,1,六:3,0,采:3,1,色:4,0,睛:4,1,点:4,2,龙:4,3,画:4,4,烈;0,1,2;
2;0;6;5;笨鸟先飞,0,3:学富五车,0,2:一诺千金,0,1:青出于蓝,0,1;0,0,笨:1,0,学:1,1,鸟:2,0,富:2,1,先:3,0,蓝:3,1,于:3,2,出:3,3,青:3,4,五:3,5,飞:4,0,车:4,1,金:4,2,千:4,3,诺:4,4,一;0,1:2,3;";
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("拆分关卡数据", GUILayout.Height(30)))
        {
            if (string.IsNullOrEmpty(inputFilePath) && string.IsNullOrEmpty(fileContentPreview))
            {
                EditorUtility.DisplayDialog("错误", "请选择关卡数据文件或加载示例数据", "确定");
                return;
            }

            string dataToProcess = "";
            
            if (!string.IsNullOrEmpty(inputFilePath) && File.Exists(inputFilePath))
            {
                dataToProcess = File.ReadAllText(inputFilePath);
            }
            else if (!string.IsNullOrEmpty(fileContentPreview))
            {
                dataToProcess = fileContentPreview;
            }
            
            if (string.IsNullOrEmpty(dataToProcess))
            {
                EditorUtility.DisplayDialog("错误", "没有可处理的关卡数据", "确定");
                return;
            }

            SplitLevels(dataToProcess);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("完成", "关卡数据拆分完成!", "确定");
        }
    }

    private void PreviewFileContent()
    {
        if (!string.IsNullOrEmpty(inputFilePath) && File.Exists(inputFilePath))
        {
            try
            {
                // 只读取文件的前几行作为预览
                string[] lines = File.ReadAllLines(inputFilePath);
                fileContentPreview = string.Join("\n", lines);
                
                // 如果内容太长，只显示前10行
                if (lines.Length > 10)
                {
                    fileContentPreview = string.Join("\n", lines, 0, 10) + "\n...";
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"读取文件失败: {e.Message}");
                fileContentPreview = "读取文件失败";
            }
        }
    }

    private void SplitLevels(string data)
    {
        // 确保输出目录存在
        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        // 按行分割数据
        string[] lines = data.Split('\n');
        int processedCount = 0;

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // 提取关卡序号（第一个分号前的内容）
            string[] parts = line.Trim().Split(';');
            if (parts.Length < 1)
                continue;

            string levelId = parts[0];
            
            // 创建文件名
            string filename = $"{levelId}.txt";
            string fullPath = Path.Combine(outputPath, filename);
            
            // 写入文件
            using (StreamWriter writer = new StreamWriter(fullPath, false, System.Text.Encoding.UTF8))
            {
                writer.Write(line.Trim());
            }
            
            processedCount++;
            Debug.Log($"已创建关卡文件: {filename}");
        }
        
        Debug.Log($"处理完成! 共创建 {processedCount} 个关卡文件.");
    }
}