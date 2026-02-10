using UnityEngine;
using UnityEditor;
using System.IO;

public class FindMissingScriptsInProject : EditorWindow
{
    [MenuItem("Tools/扫描全项目 Prefab 的 Missing Script")]
    public static void ScanAllPrefabs()
    {
        // 获取项目中所有 Prefab 的 GUID
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        
        Debug.Log($"开始扫描 {guids.Length} 个 Prefab...");
        int count = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab != null)
            {
                // 获取自身和所有子物体上的组件
                Component[] components = prefab.GetComponentsInChildren<Component>(true);
                
                foreach (Component c in components)
                {
                    // 核心判断：组件引用是 null，但它占据了一个位置
                    if (c == null)
                    {
                        Debug.LogError($"[找到坏掉的 Prefab] 路径: {path} \n(点击此日志可跳转)", prefab);
                        count++;
                        break; // 一个 Prefab 报一次就够了
                    }
                }
            }
        }

        Debug.Log($"扫描结束。共发现 {count} 个有问题的 Prefab。");
    }
}