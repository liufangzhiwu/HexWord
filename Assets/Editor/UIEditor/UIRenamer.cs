using UnityEditor;
using UnityEngine;
using System.IO;

public class UIRenamer : EditorWindow
{
    private string folderPath = "";
    private string prefix = "UI_Icon_";

    [MenuItem("Tools/UI 批量重命名")]
    static void Init()
    {
        UIRenamer window = GetWindow<UIRenamer>();
        window.Show();
    }

    void OnGUI()
    {
        GUILayout.Label("UI批量重命名工具", EditorStyles.boldLabel);

        // 文件夹选择部分
        EditorGUILayout.BeginHorizontal();
        {
            GUILayout.Label("目标文件夹:", GUILayout.Width(70));
            GUILayout.Label(folderPath, EditorStyles.textField);

            if (GUILayout.Button("浏览", GUILayout.Width(50)))
            {
                string absolutePath = EditorUtility.OpenFolderPanel("选择UI资源文件夹", Application.dataPath, "");

                if (!string.IsNullOrEmpty(absolutePath))
                {
                    // 转换为相对路径
                    if (absolutePath.StartsWith(Application.dataPath))
                    {
                        folderPath = "Assets" + absolutePath.Substring(Application.dataPath.Length);
                    }
                    else
                    {
                        Debug.LogError("必须选择Assets目录内的文件夹！");
                        folderPath = "";
                    }
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        // 前缀设置
        prefix = EditorGUILayout.TextField("命名前缀", prefix);

        // 执行按钮
        if (GUILayout.Button("执行批量重命名"))
        {
            if (!ValidatePath()) return;

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });

            int renamedCount = 0;
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetExtension(assetPath).ToLower() == ".png")
                {
                    string fileName = Path.GetFileNameWithoutExtension(assetPath);
                    string newName = $"{prefix}{fileName}";

                    // 跳过已符合命名规则的文件
                    if (fileName.StartsWith(prefix)) continue;

                    AssetDatabase.RenameAsset(assetPath, newName);
                    renamedCount++;
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"重命名完成！共处理 {renamedCount} 个文件");
        }
    }

    bool ValidatePath()
    {
        if (string.IsNullOrEmpty(folderPath))
        {
            Debug.LogError("请先选择目标文件夹！");
            return false;
        }

        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            Debug.LogError("无效的文件夹路径！");
            return false;
        }

        return true;
    }
}