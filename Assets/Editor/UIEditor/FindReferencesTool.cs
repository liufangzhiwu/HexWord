using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

public class FindReferencesTool : EditorWindow
{
    [MenuItem("Tools/查找 '内鬼' 组件 (Collider, Mask)")]
    public static void ShowWindow()
    {
        GetWindow<FindReferencesTool>("组件查找器");
    }

    void OnGUI()
    {
        GUILayout.Label("查找项目中引用的特定组件", EditorStyles.boldLabel);

        if (GUILayout.Button("开始扫描 (MeshCollider, EdgeCollider2D, SpriteMask)"))
        {
            FindMissingComponents();
        }
    }

    static void FindMissingComponents()
    {
        Debug.Log("=== 开始扫描 ===");
        
        // 1. 定义我们要找的类型
        System.Type[] typesToFind = new System.Type[] 
        { 
            typeof(EdgeCollider2D), 
            typeof(SpriteMask) ,
        };

        // 2. 扫描所有 Prefab
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab != null)
            {
                foreach (var type in typesToFind)
                {
                    var components = prefab.GetComponentsInChildren(type, true);
                    foreach (var comp in components)
                    {
                        Debug.LogError($"[Prefab 发现] 类型: {type.Name} | 路径: {path} | 物体名: {comp.gameObject.name}", comp.gameObject);
                    }
                }
            }
        }

        // 3. 扫描当前打开的场景
        foreach (var type in typesToFind)
        {
            var objects = GameObject.FindObjectsOfType(type);
            foreach (var obj in objects) // Check objects in scene
            {
                // FindObjectsOfType 返回的是 Component，转为 GameObject
                GameObject go = (obj as Component).gameObject;
                Debug.LogError($"[场景 发现] 类型: {type.Name} | 场景物体: {go.name}", go);
            }
        }

        Debug.Log("=== 扫描结束，请查看控制台报错信息（点击可跳转） ===");
    }
}